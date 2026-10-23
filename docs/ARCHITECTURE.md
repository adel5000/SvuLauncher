# SVU Launcher Rewrite: Architecture Specification

## 1. Scope

What gets rewritten is **the VB6 launcher**: auth, catalog, provisioning and handoff. The conferencing engine is a third-party, closed **Linktivity WebDemo 4.0.1.7** client, and its protocol is server-defined. It is isolated behind `ISessionClient`:

- **Phase 1:** `LegacyLinktivityClient` runs the unmodified `linkjvm.jar` in a **32-bit, patched Java 8 runtime** (Temurin/Zulu 8 x86), replacing JRE 6u45. The natives are x86 JNI and need Java ≤ 8-era APIs (`Thread.stop`, `java.applet`).
- **Phase 2:** replace the engine with a supported platform (BigBlueButton, Teams, Zoom or another platform with deep links). Only a new `ISessionClient` is needed.

## 2. Stack evaluation

| Criterion | **C# / .NET 8** | Go | Modern Java (21 LTS, jlink) |
|---|---|---|---|
| Cold start | WPF+ReadyToRun ≈ 0.3–0.6 s. CLI core can be NativeAOT (<50 ms) | Excellent (<50 ms) | 0.5–1.5 s (CDS/AppCDS helps) |
| Footprint | Framework-dependent ~5 MB, self-contained trimmed ~60–80 MB | ~10–15 MB single exe | jlink image ~40–60 MB |
| SOAP RPC/encoded | Manageable (XLinq envelope, as in the prototype). `dotnet-svcutil` is document/literal only | Manual XML, no mature client | JAX-WS also dropped RPC/encoded; manual |
| Windows integration (DPAPI, Credential Manager, Job Objects, Core Audio, ShellExecute, MSIX) | **First-class** | P/Invoke via `x/sys/windows`, more glue | JNA/Panama; workable but heavier |
| Native GUI | WPF/WinUI 3 mature, RTL/Arabic solid. Avalonia for cross-platform | Weak (Fyne/Walk) | JavaFX/Swing; RTL OK, look dated |
| Could host linkjvm in-process | No (separate 32-bit JVM regardless) | No | **No either**: needs x86 JVM ≤ 8 and Applet API (removed in JDK 26) |
| Team/tooling | VS/Rider, MSIX, Intune | Good CI, weak UI tooling | Good |

**Recommendation: C# / .NET 8**, with a WPF shell (or WinUI 3 for Fluent styling) over a UI-agnostic core.
- Go wins on binary size and start time, but the launcher's real work is Windows-specific (credential vault, process/job control, audio endpoints, Arabic RTL UI), and Go's GUI and SOAP stories are weak.
- Modern Java buys nothing: the legacy engine cannot move to it anyway, because of the x86 JNI and Applet dependencies.
- If cross-platform becomes a goal after Phase 2, replace WPF with **Avalonia** and keep Core/Infrastructure unchanged.

## 3. Solution layout (implemented prototype)

```
SvuLauncher/
├─ global.json, Directory.Build.props        (C# 12, nullable, warnings-as-errors)
├─ src/
│  ├─ SvuLauncher.Core/                       net8.0, AOT-compatible, no Windows APIs
│  │  ├─ Model.cs                             Credentials (redacted ToString), SessionInfo, LaunchTicket…
│  │  ├─ Abstractions.cs                      IAuthService, ISessionCatalog, IMeetingStatusService,
│  │  │                                       ICredentialVault, ISessionClient, ISessionHandle
│  │  ├─ StudentUrlParser.cs                  URI/query parsing of studentURL (host, linkid, sessp)
│  │  ├─ LinkJvmCommand.cs                    argv contract of LinkJvm.parseArgs (7 positional args)
│  │  └─ LaunchOrchestrator.cs                sign-in → ticket → optional status gate → handoff
│  ├─ SvuLauncher.Infrastructure/             net8.0-windows
│  │  ├─ Soap/SoapClient.cs                   SOAP 1.1 RPC/encoded + doc/literal; HTTPS enforced
│  │  ├─ Soap/SvuisDirectoryClient.cs         checkUserPass, getPrograms, getCourses, getSessionsByProgCourse
│  │  ├─ Soap/MeetingStatusClient.cs          getMeetingLaunchCount (explicit insecure opt-in)
│  │  ├─ Security/DpapiCredentialVault.cs     DPAPI CurrentUser, atomic write
│  │  ├─ Legacy/LegacyLinktivityClient.cs     preflight (files, x86 PE, SHA-256 pin) + Process w/ ArgumentList
│  │  └─ LauncherServices.cs                  LauncherSettings (svulauncher.json) + shared composition root
│  ├─ SvuLauncher.App/                        WPF shell (MVVM, CommunityToolkit.Mvvm)
│  │  ├─ ViewModels/ShellViewModel.cs         sign-in page ⇄ sessions page
│  │  ├─ ViewModels/LoginViewModel.cs         sign in, remember-me via ICredentialVault
│  │  ├─ ViewModels/SessionsViewModel.cs      programs, option, session grid, launch, running-session tracking
│  │  ├─ Views/LoginView, Views/SessionsView  XAML; FlowDirection follows the UI culture (RTL for Arabic)
│  │  └─ MainWindow                           confirms before closing while a session runs
│  └─ SvuLauncher.Cli/                        console host (login/programs/courses/sessions/launch --dry-run)
├─ tests/SvuLauncher.Tests/                   33 xUnit tests (core, infrastructure, view models)
└─ docs/ ANALYSIS.md, ARCHITECTURE.md
```

Planned additions: Arabic/English `.resx` string resources (UI text is currently English-only), `SvuLauncher.Audio` (Core Audio `IAudioEndpointVolume` for the mic-mute feature that replaces `clsVolume`), a course filter once the meaning of the `option` parameter is confirmed, and MSIX packaging.

## 4. Design rules (each one maps to a legacy flaw)

| Rule | Replaces |
|---|---|
| HTTPS-only `SoapClient`. Plain HTTP needs an explicit, per-endpoint opt-in flag that is logged | S1 silent downgrade; HTTP-first meeting service |
| Credentials only in `ICredentialVault` (DPAPI or Credential Manager), never in the registry or logs. `Credentials.ToString()` is redacted | S3 plaintext `SaveSetting` |
| `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, validated tokens | space-joined argv, ShellExecute |
| Preflight: required files present, `java.exe` is x86, `linkjvm.jar` hash pinned | S9 `-aos` with no integrity check; `INVALID_INSTALL` late failure |
| Runtime and engine shipped read-only inside the MSIX/MSI. No self-extraction at run time | 7-Zip self-extraction, `SVU2014\` mkdir |
| Every failure surfaces as a typed exception or a structured log. No empty catch | `Utilities.exceptionHandler` (91 silent sites), VB `On Error` |
| Unicode UI with RTL resources | "change your system locale" dialog |
| Kill the engine's process tree on exit (`Kill(entireProcessTree)`; production: Job Object with `KILL_ON_JOB_CLOSE`) | orphaned `java.exe` |
| Writable engine state (`config.hash`) goes to a per-user copy of the engine config dir | probe re-run every launch because Program Files is read-only |

## 5. Core launch-and-auth flow

```
CLI/WPF ──► LaunchOrchestrator.SignInAsync(creds)
               └─► IAuthService.AuthenticateAsync      (svuis checkUserPass, HTTPS)
          ──► ISessionCatalog.GetSessionsAsync(...)     (getSessionsByProgCourse)
          ──► LaunchOrchestrator.LaunchAsync(session, user, prefs)
               ├─ StudentUrlParser.Parse(studentURL)   → host, linkId
               ├─ [optional] IMeetingStatusService     → block student if 0 && RequireHostStarted
               └─ ISessionClient.StartAsync(ticket)
                    └─ LegacyLinktivityClient: Preflight → java.exe -cp linkjvm.jar LinkJvm
                         host host 443 linkId userName User|Host EN   (cwd = Java\bin)
```

## 6. Server-side asks (outside the client's control)

1. Serve `NewSessions2.php` and the meeting-status service on HTTPS only. Retire the `WDemoServicestest` endpoint and its unauthenticated admin operations.
2. Replace password-per-call with a token (`checkUserPass` → short-lived bearer/session token).
3. Expose `studentURL` as structured fields (`host`, `linkId`, `port`) instead of a URL to scrape.
4. Long term: move off Linktivity, whose transport has no real encryption or server authentication (S4/S5).
