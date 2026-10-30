<h1 align="center">SVU Launcher</h1>

<p align="center">
  <strong>مشغّل جلسات الجامعة الافتراضية السورية</strong><br>
  <sub>A lightweight Windows launcher for SVU live lectures</sub>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/release-v1.0.0--preview-2563eb?style=flat-square" alt="Release v1.0.0-preview">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0b7650?style=flat-square" alt="Windows 10 and Windows 11">
  <img src="https://img.shields.io/badge/architecture-x64-4c5665?style=flat-square" alt="x64">
  <img src="https://img.shields.io/badge/framework-.NET%20%2F%20WPF-512bd4?style=flat-square" alt=".NET and WPF">
  <img src="https://img.shields.io/badge/languages-AR%20%7C%20EN-c48a2c?style=flat-square" alt="Arabic and English">
</p>

<p align="center">
  <a href="#ar">العربية</a> · <a href="#en">English</a>
</p>

<p align="center">
  <strong>Developer:</strong> Eng. Mohammad Adel Ayman Watad ·
  <a href="https://wa.me/963988038382">WhatsApp +963 988 038 382</a>
</p>

> ⚠️ **Independent project / مشروع مستقل**
>
> SVU Launcher is an independent, non-official project. It is **not affiliated with, endorsed by,
> or operated by the Syrian Virtual University administration**. The university’s name is used only
> to describe compatibility with its lecture services.
>
> هذا المشروع مستقل وغير رسمي، وليس تابعًا لإدارة الجامعة الافتراضية السورية ولا ممثلًا عنها.
> يُستخدم اسم الجامعة فقط لوصف التوافق مع خدمات المحاضرات.

---

<a id="ar"></a>

# 🇸🇾 العربية

دليل عملي لتثبيت **SVU Launcher** وتفعيله والبدء باستخدامه على Windows. الإصدار الحالي تجريبي:
`v1.0.0-preview`.

> **حمّل فقط من [صفحة الإصدارات الرسمية](../../releases):** لا تثق بملفات Setup من مصادر أخرى.
> لا تشارك مفتاح الترخيص أو كلمة مرور الجامعة مع أي شخص.

## المحتويات

- [نظرة عامة](#ar-overview)
- [متطلبات التشغيل](#ar-requirements)
- [الخطوة 1: التنزيل والتحقق](#ar-step-1)
- [الخطوة 2: التثبيت السريع](#ar-step-2)
- [الخطوة 3: التشغيل الأول وطلب الترخيص](#ar-step-3)
- [الخطوة 4: التفعيل](#ar-step-4)
- [الخطوة 5: تسجيل الدخول وبدء المحاضرة](#ar-step-5)
- [الدعم والتواصل](#ar-support)
- [الخصوصية والترخيص](#ar-privacy)
- [للمطورين](#ar-developers)

<a id="ar-overview"></a>

## نظرة عامة

**SVU Launcher** تطبيق سطح مكتب خفيف لطلاب الجامعة الافتراضية السورية. يجمع تسجيل الدخول إلى
حساب الجامعة، واستعراض البرامج والمواد والجلسات، والانتقال إلى قاعة المحاضرة في مكان واحد.

- واجهة عربية وإنجليزية مع دعم كامل للاتجاه من اليمين إلى اليسار.
- تثبيت لكل مستخدم دون الحاجة إلى صلاحيات مدير Windows.
- تفعيل بترخيص مرتبط بالجهاز، مع تحقق دوري من الخادم.
- تخزين آمن لبيانات الحساب باستخدام DPAPI.
- إصدار x64 قائم بذاته، لا يحتاج إلى تثبيت .NET بشكل منفصل.

> هذا الإصدار **معاينة**. قد تتغير بعض التفاصيل، ويُنصح دائمًا بالاحتفاظ بنسخة من مفتاح الترخيص.

<a id="ar-requirements"></a>

## متطلبات التشغيل

| المتطلب | التفاصيل |
|---|---|
| نظام التشغيل | Windows 10 أو Windows 11 |
| المعمارية | 64-bit / x64 |
| الاتصال | اتصال بالإنترنت عند التفعيل وعند التحقق الدوري |
| الحساب | حساب جامعي فعّال |
| الترخيص | مفتاح تفعيل بصيغة `SVUL-XXXXX-XXXXX-XXXXX-XXXXX` |
| اختياري | سماعات أو ميكروفون لحضور المحاضرات الصوتية |

<a id="ar-step-1"></a>

## الخطوة 1: التنزيل والتحقق

1. افتح [صفحة الإصدارات](../../releases) في هذا المستودع.
2. نزّل أحدث ملف `Setup.exe` الخاص بـ **SVU Launcher**.
3. تحقق من أن الملف قادم من صفحة الإصدارات الرسمية، وليس من رابط خارجي.
4. إذا ظهر تحذير من Windows SmartScreen، فتأكد أولًا من مصدر الملف. لا تتجاوز التحذير إلا إذا
   كنت واثقًا من أن التنزيل رسمي.

<p align="center">
  <img src="docs/images/01-download-release.png" alt="صفحة GitHub Releases وزر تنزيل Setup.exe" width="900">
</p>
<p align="center"><sub>الخطوة 1 — نزّل Setup.exe من صفحة الإصدارات الرسمية.</sub></p>

<a id="ar-step-2"></a>

## الخطوة 2: تثبيت خفيف دون صلاحيات مدير

1. شغّل ملف `Setup.exe`.
2. اتبع معالج التثبيت.
3. يمكنك تغيير مجلد التثبيت من صفحة اختيار المجلد.
4. اترك خيار **إنشاء اختصار على سطح المكتب** مفعّلًا إن رغبت.
5. أكمل التثبيت. لا يحتاج الطالب عادةً إلى صلاحيات مدير؛ التثبيت الافتراضي يكون لحساب المستخدم.

<p align="center">
  <img src="docs/images/02-installer-setup.png" alt="معالج تثبيت SvuLauncher" width="900">
</p>
<p align="center"><sub>الخطوة 2 — معالج التثبيت مع إمكانية اختيار مجلد مخصص.</sub></p>

<a id="ar-step-3"></a>

## الخطوة 3: التشغيل الأول وطلب الترخيص

1. شغّل **SVU Launcher** من سطح المكتب أو قائمة ابدأ.
2. افتح صفحة **الترخيص** أو **الإعدادات ← الترخيص**.
3. ابحث عن معرّف الجهاز، ويظهر عادةً بالشكل: `This PC: 7794-2663`.
4. أرسل إلى المطوّر عبر واتساب:
   - الاسم الكامل.
   - معرّف الجهاز / Machine Fingerprint.
   - أي ملاحظة عن مشكلة التفعيل إن وجدت.
5. لا ترسل كلمة مرور الجامعة أو أي بيانات حساسة.

<p align="center">
  <img src="docs/images/03-activation-screen.png" alt="شاشة التفعيل ومعرّف الجهاز في SVU Launcher" width="900">
</p>
<p align="center"><sub>الخطوة 3 — صفحة الترخيص تعرض معرّف الجهاز وحالة الاشتراك.</sub></p>

> **ملاحظة:** مفتاح الترخيص مرتبط بجهاز واحد. إذا غيّرت الجهاز أو أعدت تثبيت Windows، تواصل مع
> الدعم قبل محاولة تفعيل المفتاح على جهاز آخر.

<a id="ar-step-4"></a>

## الخطوة 4: التفعيل

1. بعد استلام المفتاح، الصقه في حقل **مفتاح الترخيص**.
2. اضغط **تفعيل**.
3. تأكد من ظهور الحالة **فعّال** مع تاريخ انتهاء الاشتراك.
4. يعمل التطبيق دون اتصال ضمن مهلة الترخيص المؤقتة، ثم يتحقق من الخادم دوريًا. أبقِ الجهاز متصلًا
   بالإنترنت بين الحين والآخر لتجديد التحقق.

صيغة المفتاح:

```text
SVUL-XXXXX-XXXXX-XXXXX-XXXXX
```

> إذا ظهرت رسالة **«أدخل مفتاحًا صحيحًا»**، تحقق من عدم وجود مسافات إضافية ومن أن المفتاح مخصص
> لهذا الجهاز. إذا استمرت المشكلة، أرسل صورة للخطأ إلى الدعم.

<a id="ar-step-5"></a>

## الخطوة 5: تسجيل الدخول وبدء المحاضرة

1. سجّل الدخول باسم المستخدم وكلمة مرور الجامعة.
2. اختر البرنامج أو المادة من القائمة.
3. استعرض الجلسات الظاهرة، مع حالتها ووقتها واسم المدرّس.
4. اضغط **بدء الجلسة** أو زر الدخول إلى المحاضرة.
5. انتظر تحميل قاعة المحاضرة، ثم استخدم أدوات الصوت والمحادثة والمشاركة حسب ما يتيحه المدرّس.

<p align="center">
  <img src="docs/images/04-sessions-dashboard.png" alt="قائمة الجلسات واختيار البرنامج والمادة" width="900">
</p>
<p align="center"><sub>الخطوة 5أ — اختر البرنامج أو المادة ثم اعرض الجلسات المتاحة.</sub></p>

<p align="center">
  <img src="docs/images/05-session-running.png" alt="قاعة المحاضرة بعد بدء الجلسة" width="900">
</p>
<p align="center"><sub>الخطوة 5ب — بدء قاعة المحاضرة والانضمام إليها.</sub></p>

> **نصيحة:** استخدم سماعات لتقليل الصدى، وتأكد من إعدادات الميكروفون قبل رفع اليد أو التحدث.

<a id="ar-support"></a>

## الدعم والتواصل

**المطوّر:** Eng. Mohammad Adel Ayman Watad

- واتساب: [+963 988 038 382](https://wa.me/963988038382)
- عند طلب الدعم، أرسل:
  - اسمك الكامل.
  - معرّف الجهاز `This PC: XXXX-XXXX`.
  - وصفًا قصيرًا للمشكلة.
  - لقطة شاشة للخطأ إن أمكن.

> **لا ترسل كلمة مرور الجامعة أبدًا.** لا يطلب المطوّر كلمة المرور لتفعيل الترخيص.

<a id="ar-privacy"></a>

## الخصوصية والترخيص

- تُخزّن بيانات الحساب المحفوظة محليًا باستخدام DPAPI.
- الترخيص مرتبط بجهاز واحد، ويتم التحقق منه دوريًا عبر خادم الترخيص.
- التطبيق لا يرسل بيانات حسابك إلى خادم الترخيص.
- لا تشارك مفتاح الترخيص علنًا؛ يمكن إلغاؤه أو إعادة ضبطه من لوحة الإدارة.

<a id="ar-developers"></a>

## للمطورين

المستودع حل Windows/.NET 8 مع واجهة WPF وخدمة ترخيص محلية للتطوير.

```powershell
dotnet build SvuLauncher.sln -c Debug
dotnet test tests\SvuLauncher.Tests\SvuLauncher.Tests.csproj
```

لبناء نسخة قابلة للتوزيع:

```powershell
dotnet publish src\SvuLauncher.App\SvuLauncher.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishDir="artifacts\publish\"
```

مثبّت Inno Setup الاختياري موجود في:

```text
tools\installer\SvuLauncher.iss
```

ملاحظات تقنية:

- `SvuLauncher.App` هي واجهة WPF ونقطة الدخول.
- `SvuLauncher.Core` يحتوي الترخيص والتنسيق الأساسي.
- `SvuLauncher.Infrastructure` يحتوي SOAP وDPAPI وخادم الترخيص.
- `SvuLauncher.Room` و`SvuLauncher.Codecs` لقاعة المحاضرة والترميزات.
- راجع `docs/LICENSING.md` و`docs/RELEASING.md` قبل النشر.

---

<a id="en"></a>

# 🇬🇧 English

A practical guide to installing, activating, and using **SVU Launcher** on Windows. The current
release is a preview: `v1.0.0-preview`.

> **Download only from the official [Releases page](../../releases).** Do not trust Setup files from
> other sources. Never share your licence key or your university password.

## Contents

- [Overview](#en-overview)
- [Requirements](#en-requirements)
- [Step 1: Download and verify](#en-step-1)
- [Step 2: Lightweight installation](#en-step-2)
- [Step 3: First launch and licence request](#en-step-3)
- [Step 4: Activation](#en-step-4)
- [Step 5: Student login and session launch](#en-step-5)
- [Support](#en-support)
- [Privacy and licensing](#en-privacy)
- [For developers](#en-developers)

<a id="en-overview"></a>

## Overview

**SVU Launcher** is a lightweight Windows desktop launcher for students of the Syrian Virtual
University. It brings together university sign-in, program and course browsing, session discovery,
and lecture-room hand-off in one focused app.

- Arabic and English UI with proper right-to-left layout.
- Per-user installation with no Windows Administrator requirement.
- PC-bound licence activation with periodic server validation.
- DPAPI-protected storage for saved account credentials.
- Self-contained x64 build; no separate .NET installation is required.

> This is a **preview** release. Details may change, and you should always keep a copy of your
> licence key.

<a id="en-requirements"></a>

## Requirements

| Requirement | Details |
|---|---|
| Operating system | Windows 10 or Windows 11 |
| Architecture | 64-bit / x64 |
| Connection | Internet access for activation and periodic licence checks |
| Account | An active SVU university account |
| Licence | A serial key in the form `SVUL-XXXXX-XXXXX-XXXXX-XXXXX` |
| Optional | Headphones or a microphone for voice lectures |

<a id="en-step-1"></a>

## Step 1: Download and verify

1. Open this repository’s [Releases page](../../releases).
2. Download the latest **SVU Launcher** `Setup.exe`.
3. Confirm that the file came from the official Releases page, not an external link.
4. If Windows SmartScreen shows a warning, verify the file source first. Do not bypass the warning
   unless you are certain the download is official.

<p align="center">
  <img src="docs/images/01-download-release.png" alt="GitHub Releases page with the Setup.exe download" width="900">
</p>
<p align="center"><sub>Step 1 — Download Setup.exe from the official Releases page.</sub></p>

<a id="en-step-2"></a>

## Step 2: Lightweight installation

1. Run `Setup.exe`.
2. Follow the installation wizard.
3. You can change the installation folder on the directory page.
4. Leave **Create a desktop shortcut** enabled if you want one.
5. Finish the installation. Students normally do not need Administrator rights; the default install
   is per-user.

<p align="center">
  <img src="docs/images/02-installer-setup.png" alt="SvuLauncher Inno Setup installation wizard" width="900">
</p>
<p align="center"><sub>Step 2 — The installer with an optional custom installation folder.</sub></p>

<a id="en-step-3"></a>

## Step 3: First launch and licence request

1. Launch **SVU Launcher** from the desktop or Start Menu.
2. Open the **Licence** page or **Settings → Licence**.
3. Find the machine fingerprint, usually shown as: `This PC: 7794-2663`.
4. Send the developer a WhatsApp message with:
   - Your full name.
   - The machine fingerprint / PC ID.
   - A short note if activation is failing.
5. Do not send your university password or any sensitive account data.

<p align="center">
  <img src="docs/images/03-activation-screen.png" alt="SVU Launcher activation screen with the machine fingerprint" width="900">
</p>
<p align="center"><sub>Step 3 — The licence page shows the machine ID and subscription status.</sub></p>

> **Note:** A licence key is bound to one PC. If you change computers or reinstall Windows, contact
> support before trying to activate the same key on another machine.

<a id="en-step-4"></a>

## Step 4: Activation

1. Paste the key you received into the **Licence key** field.
2. Press **Activate**.
3. Confirm that the status becomes **Active** and that the subscription expiry is shown.
4. The app works offline within its lease window and checks back with the licence server periodically.
   Keep the PC online from time to time so the lease can be refreshed.

Serial format:

```text
SVUL-XXXXX-XXXXX-XXXXX-XXXXX
```

> If you see **“This key has a typo”**, check for extra spaces and make sure the key belongs to this
> PC. If the problem continues, send a screenshot of the error to support.

<a id="en-step-5"></a>

## Step 5: Student login and session launch

1. Sign in with your SVU username and password.
2. Choose a program or course from the list.
3. Review the available sessions, including status, time, and tutor name.
4. Press **Start session** or the lecture entry button.
5. Wait for the lecture room to load, then use the voice, chat, and sharing controls the tutor makes
   available.

<p align="center">
  <img src="docs/images/04-sessions-dashboard.png" alt="SVU Launcher sessions list and course picker" width="900">
</p>
<p align="center"><sub>Step 5a — Choose a program or course and browse the available sessions.</sub></p>

<p align="center">
  <img src="docs/images/05-session-running.png" alt="SVU Launcher lecture room with an active session" width="900">
</p>
<p align="center"><sub>Step 5b — Launch the lecture room and join the session.</sub></p>

> **Tip:** Use headphones to reduce echo, and check your microphone settings before raising your hand
> or speaking.

<a id="en-support"></a>

## Support

**Developer:** Eng. Mohammad Adel Ayman Watad

- WhatsApp: [+963 988 038 382](https://wa.me/963988038382)
- When requesting support, include:
  - Your full name.
  - Your machine ID: `This PC: XXXX-XXXX`.
  - A short description of the problem.
  - A screenshot of the error if possible.

> **Never send your university password.** The developer does not need it to activate a licence.

<a id="en-privacy"></a>

## Privacy and licensing

- Saved account credentials are stored locally using DPAPI.
- A licence is bound to one PC and periodically checked against the licence server.
- Your account credentials are not sent to the licence server.
- Do not share your licence key publicly; it can be revoked or its seat can be reset from the
  administration dashboard.

<a id="en-developers"></a>

## For developers

The repository is a Windows/.NET 8 solution with a WPF shell and a local development licence server.

```powershell
dotnet build SvuLauncher.sln -c Debug
dotnet test tests\SvuLauncher.Tests\SvuLauncher.Tests.csproj
```

Build a distributable self-contained x64 release:

```powershell
dotnet publish src\SvuLauncher.App\SvuLauncher.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishDir="artifacts\publish\"
```

Optional Inno Setup installer:

```text
tools\installer\SvuLauncher.iss
```

Technical notes:

- `SvuLauncher.App` — WPF UI and application entry point.
- `SvuLauncher.Core` — licensing, launch orchestration, and UI-agnostic domain logic.
- `SvuLauncher.Infrastructure` — SOAP clients, DPAPI storage, hardware identity, and the licence
  server client.
- `SvuLauncher.Room` and `SvuLauncher.Codecs` — lecture-room protocol/media and codec support.
- Read `docs/LICENSING.md` and `docs/RELEASING.md` before publishing a build.

---

<p align="center">
  Built for SVU students · صُنع لطلاب الجامعة الافتراضية السورية<br>
  <sub>Eng. Mohammad Adel Ayman Watad · <a href="https://wa.me/963988038382">WhatsApp</a></sub>
</p>
