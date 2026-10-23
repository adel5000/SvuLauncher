# SVU Sessions Launcher: Reverse-Engineering Report

Target: `C:\Program Files (x86)\SVU\SVUSessionsLauncher` (777 files).
Method: static analysis only. The launcher was **not executed**. Two public WSDL documents were fetched read-only; no SOAP operation was invoked.

Evidence labels:
- **[O]** observed directly in shipped bytes, decompiled code or the live WSDL.
- **[I]** inferred from code structure. Not seen at runtime.
- **[U]** unresolved.

REA Evidence IDs: `ev_9e5e77ca…` (PE resource tree of `SvuSessionLauncher.exe`), `ev_8bb4cfa9…` (managed check: no CLI header, so not .NET). Java decompilation used CFR 0.152. VB6 cross-references used Capstone 5.0.7, because no REA native provider (Ghidra/IDA/Kuna) is configured on this host.

---

## 1. Artifact inventory

| Artifact | What it is | Notes |
|---|---|---|
| `SvuSessionLauncher.exe` (21.8 MB, PE32 x86) | **Visual Basic 6 native-code app** [O]. Project path `C:\SVUSessionsLauncher\Version9\svuClassLauncher.vbp`. Resource timestamp Oct 2017. | Not Java. Embeds 4 named resources (lang 0x2801 ar-SY) [O] |
| ├ `ZIP` resource (20.2 MB) | **7z** archive, written out as `java.7z` [O] | Bundled JRE + Linktivity client |
| ├ `SEVENZIPEXE` / `SEVENZIPDLL` | `7zG.exe` / `7z.dll` [O] | Run as `7zg.exe x java.7z -aos` [O] |
| └ `INSTALL` (97 KB PE) | InstallShield Compact setup "TSP_CODEC" by Bytescribe [O] | DSP Group TrueSpeech ACM codec (`tsd32.dll`) |
| `SvuSessionLauncher\Java\` | **Oracle JRE 1.6.0_45** (32-bit, 2013) [O] | Includes browser plug-ins (`npjp2.dll`, `jp2iexp.dll`), `jqs.exe` and `msvcr71.dll` |
| `Java\bin\linkjvm.jar` (375 KB, built 2005-10-26) | **Linktivity WebDemo 4.0.1.7** conferencing client, main class `LinkJvm` [O] | SHA-256 `DC2E3D72…C3E173A` |
| `Java\bin\lk*.dll`, `NativeSupport.dll`, `webdemvp.dll`, `cobrowse.dll`, `wdft*.dll`, `WIDiag*.dll`, `Recovery*.dll` | 32-bit JNI natives for screen share, remote control, voice/video, file transfer and IPC [O] | |
| `Java\bin\config.hash` | Java-serialized `Hashtable` with client defaults (`Version=&VERSION=4.0.1.7`, URL paths) [O] | Read through `ObjectInputStream` |
| `Java\bin\linktivity.properties`, `phrases.txt` | UI string bundle and chat canned phrases [O] | |

The VB6 project contains six objects [O]: `frmLogin` (txtUserName, txtPassword, chkSavePassword), `frmMain` (cmbPrograms, cmbCourses, cmbOptions, grdSessions MSFlexGrid, cmdLaunchSession, tmrCheckMic, chkMicMute; 35 methods), `frmAbout`, `frmLangMsg`, `GenModule` (39), `clsVolume` (61, winmm mixer).

## 2. External integrations

| Endpoint | Protocol | Operations | Notes |
|---|---|---|---|
| `https://svuis.svuonline.org/SVUIS/WebDemo_wservice/NewSessions2.php` | SOAP 1.1 RPC/encoded through MS SOAP Toolkit 3.0 (`mssoap30.dll`) [O] | `checkUserPass(userName, passWord)`, `getPrograms()`, `getCourses(programID)`, `getSessionsByProgCourse(userName, passWord, option, programID)`. The WSDL also has `getTodaySessions`, `getTodayTraining`, `isUserTrainer` [O] | **HTTPS first, then falls back to `http://` on init failure** [O, sub_0043aea0] |
| `http://sessions.svuonline.org/WDemoServicestest/Service1.asmx` | SOAP doc/literal, `tempuri.org` [O] | Launcher calls `getMeetingLaunchCount(_serverId, _tutorId, _meetingId)` [O]. The WSDL also exposes `createMeeting`, `deleteMeeting`, `creatNewUser`, `getTutors`, `getMeetingInfo` (guarded by `_password`/`_checkSum`) [O] | **HTTP tried first**. HTTPS did not respond during analysis [O]. A *test* service in production |
| Conferencing server `<host>:443` | Linktivity binary protocol [O] | 16-byte header: magic `10 04`, 3-byte length, flags, route, request code [O] | Default ladder: **plain TCP → HTTP CONNECT proxy → HTTP POST tunnel**. SSL only if `UseSsl=true`, which is not set [O] |
| `\\.\pipe\wAppLinkIpc` | Local named pipe (`NativeSupport.DispatchIpc`) [O] | Text commands `PING`, `DOCS`, `PLAY…`, `EXIT`, `STOP` [O] | No authentication in Java. Pipe DACL [U] |
| `http://<host>/InteractiveDemo/…` | Browser launched via `Runtime.exec(BrowserPath + " " + url)` [O] | Help, invite, address book, support email | |
| Registry | `HKCU\Software\VB and VBA Program Settings\<app>\Auth\{U,P}` [O]. `…\CurrentVersion\RunOnce` (NativeSupport `RebootToUrl`) [O] | | |

## 3. End-to-end sequence (launch → session handoff)

1. **Start**: VB6 `Sub Main`/`frmLogin` checks the system locale (`GetLocaleInfoA`, `TranslateCharsetInfo`). `frmLangMsg` links to "change the system locale" articles [O/I]. The non-Unicode VB6 UI needs an Arabic ANSI code page.
2. **Runtime provisioning** (`sub_00442a50`, `sub_00442450`, `sub_004450c0`): writes resource `ZIP` → `java.7z`, `SEVENZIPEXE` → `7zG.exe`, `SEVENZIPDLL` → `7z.dll`. Runs `7zg.exe x java.7z -aos` (skip existing files) and creates `SVU2014\` with `Dir`/`MkDir` [O]. The exact target root (`g_448034`) is [U]. The installed layout suggests `<AppDir>\SvuSessionLauncher\`.
3. **Codec check** (`sub_00442c60`): looks for `tsd32.dll`. If it is missing, extracts `INSTALL` → `tspcodec.exe` and runs it [O]. That needs admin.
4. **Login** (`frmLogin`, `sub_0043aea0`): creates `MSSOAPLib30.SoapClient30`. `mssoapinit(https…NewSessions2.php?wsdl)` is tried, then `http://…` [O]. It then calls `checkUserPass(user, pass)` late-bound [O]. Username → `g_448028`, password → `g_44802c`, role → `g_448030` [O]. "Save password" calls `SaveSetting <app>,"Auth","U",user` and `SaveSetting <app>,"Auth","P",password` in **plaintext REG_SZ** [O].
5. **Catalog** (`frmMain`): `getPrograms` (`sub_0043e300`), `getCourses` (`sub_0043e970`), then `getSessionsByProgCourse(user, pass, option, programID)` (`sub_0043cca0`). The password is re-sent on every call [O]. The response parser walks MSXML `IXMLDOMNode` children and compares `LCase(baseName)` with `sessionid`, `studenturl`, `sessionname`, `status`, `startdate`, `launcheddate`, `servernumber`, `tutorid`, `tutorname`, `classid` [O]. It extracts the host (cut at `"/de"`), `linkid=` and `sessp=` from `studenturl` with `InStr`/`Mid$` [O]. `sessp` is parsed but is not seen in the launch argv [O].
6. **Status** (`sub_0043c9b0`): `getMeetingLaunchCount(serverId, tutorId, meetingId)` over HTTP [O]. Whether the result gates student launch is [I].
7. **Launch** (`sub_00442530`):
   ```
   ShellExecuteEx(cbSize=0x3C, fMask=SEE_MASK_NOCLOSEPROCESS, lpFile="java.exe",
                  lpDirectory=g_448034 & "Java\bin\",
                  lpParameters="-cp linkjvm.jar LinkJvm " & host & " " & host & " 443 " & CStr(linkId) & " " & userName & " User EN")
   ```
   Role `"tutor"` → `" Host EN"` [O]. Exit is polled with `GetExitCodeProcess` [O/I].
8. **LinkJvm.main** (args ≥ 7, else `System.exit(87)`) [O]. The constructor prints every argv to stdout, then calls `readConfig()` (deserializes `./config.hash`) and `parseArgs()`: `Address=args[1]`, `Port=args[2]`, `SessionId=args[3]`, `UserId=args[4]`, `Type=args[5]`, `Language="_"+args[6]`, `CodeBase=http://args[0]/applets/` [O].
9. **run()**: checks that the 11 natives exist in `TemporaryDirectory` (`INVALID_INSTALL` otherwise). If no persisted `Connection` exists, it runs **TestApplet** (a connection probe across transports) and then `writeConfig()` [O]. A non-admin user cannot write under Program Files and the error is swallowed, so the probe likely repeats on every launch [I].
10. **Connect** (`ConnectionApplet` → `Connection.establishConnection`). Plain TCP sockets on 443 use a **canned TLS ClientHello** (`CLIENT_HELLO` bytes `16 03 01…` with the message header spliced in at offset 47) to pass firewalls [O]. Fallbacks are an HTTP CONNECT proxy (Basic proxy auth) and an HTTP POST tunnel [O].
11. **Join**: server message `4004` (node update) carries properties. `props[29]` becomes the **CAST-128 ECB key** (`setCipherKey`) and the cipher flag is applied [O]. GUI modules load. Server control messages `2001/2002/3009` load, start and stop native-backed controls [O].
12. **Runtime**: viewport/remote control (`lkhost.dll`: `BitBlt`, `OpenInputDesktop`, `keybd_event`, `mouse_event`), voice/video, cobrowse, file transfer, whiteboard, polling and recording [O]. An IPC thread serves `\\.\pipe\wAppLinkIpc` [O].

## 4. Security findings

| # | Severity | Finding | Evidence |
|---|---|---|---|
| S1 | **Critical** | **Credential downgrade:** if the HTTPS WSDL init fails (for example because an attacker blocks 443), the login service silently uses `http://` and `checkUserPass` sends the password in cleartext. | [O] `sub_0043aea0` @0x43b0bf/0x43b0ee |
| S2 | **High** | The password is re-sent in every `getSessionsByProgCourse` call, which widens the S1 exposure. | [O] WSDL |
| S3 | **High** | "Remember password" stores the **plaintext password** in `HKCU\Software\VB and VBA Program Settings\…\Auth\P`. | [O] `SaveSetting` (MSVBVM60 ord 690) @0x43b604. Not present on this host |
| S4 | **High** | Conferencing traffic is **plain TCP on port 443** disguised with a fake TLS hello. The optional "128-bit encryption" is CAST-128 in ECB mode with **no integrity check and a key sent in-band by the server**, so it gives no confidentiality against a passive observer and no server authentication. | [O] `Connection`, `Cipher`, `PeersStubApplet:577` |
| S5 | **High** | Server messages load native control modules, including remote input injection and file transfer. In WebInteractive mode, `RebootToUrl` writes `RunOnce` and calls `InitiateSystemShutdown`. Combined with S4, a MITM can drive these. | [O] code paths. Exact consent UX in natives is [U] |
| S6 | **High** | **EOL runtime**: JRE 1.6.0_45 (April 2013, many critical CVEs) plus unused browser-plugin surface. TLS is limited to 1.0 when SSL is used, and raw `SSLSocket` does **no hostname verification**. The truststore password is hardcoded (`"linktivity"`). | [O] |
| S7 | Medium | `readConfig()` deserializes `config.hash` from the CWD with `ObjectInputStream`, without filtering (pre-JEP 290). Exploitable only if the install dir is user-writable. **It is not writable on this host** (Admins/SYSTEM only). | [O] ACL check |
| S8 | Medium | Unauthenticated IPC pipe `wAppLinkIpc` (`STOP`/`EXIT` terminate the session). The DACL is [U]. | [O] strings/JNI |
| S9 | Medium | The launcher extracts the JRE with `-aos` (skip existing files) and has no integrity check, so a tampered or partial runtime persists. | [O] |
| S10 | Low | All argv is echoed to stdout. `Runtime.exec(String)` runs a browser command built from config plus a server-derived host. | [O] |
| S11 | Info (server-side) | A test SOAP service in production exposes admin operations (`createMeeting`, `deleteMeeting`, `creatNewUser`) over plain HTTP. Report this to SVU IT; do not probe it. | [O] WSDL only |

## 5. Silent error drops and reliability

- `linktivity.utilities.Utilities.exceptionHandler(Exception)` is an **empty method** called from **91 sites** [O]. Config writes, socket setup, SSL handshakes and URL errors all vanish.
- VB6 wraps SOAP init and calls in `On Error` traps (`__vbaOnError`). Failures appear only as a downgrade or an empty grid [O/I].
- The connection test re-runs whenever `config.hash` cannot be persisted [I]. This costs startup latency of several transport timeouts (`m_timeout=30`).
- `LinkJvm.destroy()` uses `Thread.stop()`, which throws on JDK 20+ [O].
- Positional argv joined with spaces: a username containing a space shifts every argument [O].

## 6. Brittle OS dependencies

VB6 runtime (`msvbvm60.dll`). `msflxgrd.ocx` (needs COM registration). **MS SOAP Toolkit 3.0** (retired, not shipped with Windows, probed via `\MSSoap\Binaries`). TrueSpeech ACM codec installer (admin). winmm mixer API for mic mute (legacy endpoint emulation). ANSI-only UI that requires the Arabic system locale. Windows-version table that ends at Server 2003. 32-bit only. Fixed port 443. 7-Zip extraction into the program directory.

## 7. Live verification (2026-10-19, one student account, read-only calls)

| Check | Result |
|---|---|
| `checkUserPass` over HTTPS through the rewrite | Returns `student`. This confirms the role inference in §3 step 4 |
| `isUserTrainer` | `0` for the student account |
| `getPrograms` | ~60 programs (`programID`, `programCode`); id 82 has an empty code |
| `getSessionsByProgCourse` `option` | = ItemData of the legacy `cmbOptions` items, read from the running client: **1** إظهار جلسات الطالب في صفوفه فقط (own classes), **2** إظهار جلسات الطالب لجميع صفوف مواده (all classes of own courses), **3** إظهار الجلسات بحسب البرنامج والمادة (by program and course; the only option that enables the program and course combos). Valid: **1–3**. `0`, `4` and `5` give SOAP fault `Call to a member function fetch() on boolean`. `option=3` with an extra `courseID` part timed out after 40 s |
| Legacy filter flow (observed, Frida on WinHTTP) | Selecting a program calls `getCourses(programID)` only. Sessions load **only on the Execute (تنفيذ) button**. For options 1/2 the program/course combos are disabled but keep their selection, and Execute still sends their ids (e.g. `option=1, programID=53, courseID=1426`). The server returned byte-identical responses for options 1/2 with those ids and with 0/0, so the rewrite sends 0. Option 3 with All/All returns the whole catalog (445 sessions, ~3.5 s) |
| Legacy grid row colour (static, `sub_0043f470`) | Rows get `CellBackColor = &H91C8C8` (the "محاضرة لغير صفوف الطالب" legend) when (a) the role is `student` and the session's `classID` (record +0x34) is 0, or (b) a per-session flag (+0x38) set only for tutors on their own session (username = tutorID) from a meeting-status check. No classID-0 row appeared in an option-3 capture of 450 sessions; statuses seen: Planned, Started, Done |
| Hidden 5th argument | The VB6 call passes 5 args (`user, pass, ItemData(cmb A), ItemData(cmb B), ItemData(cmb C)`). The WSDL declares 4. Adding `courseID` was accepted and changed nothing visible for this account |
| `getTodaySessions` | `option` 0/1 → empty. 2/3 → a **placeholder record** (`sessionName="Invalid Option"`, all other fields nil). Errors are returned as data |
| TLS | .NET/Schannel connects. Python 3.14/OpenSSL fails with `SSLV3_ALERT_HANDSHAKE_FAILURE`, which suggests the server only offers legacy cipher suites |
| Sessions for this account | None in any combination tried, so no real `studentURL` sample was captured and the launch could not be exercised |

## 8. Open questions

- [U] Exact extraction root `g_448034` and `SVU2014\` location: needs dynamic run or full VB6 decompilation (VB Decompiler/Ghidra).
- [U] The real `studentURL` format: the parser is tolerant, but a captured sample should be added as a test.
- [U] Server-side semantics of the hidden 5th argument: confirm with the service owner. The meaning of `option` 1–3 is now observed (§7).
- [U] Native DLL consent prompts and pipe security descriptor: needs Ghidra on `NativeSupport.dll`/`lkhost.dll`.
