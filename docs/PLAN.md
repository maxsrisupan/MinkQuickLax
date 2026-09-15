# MinkQuickLax — แผนลงมือ (Plan)

> **ความคืบหน้า:** M0–M10 เสร็จ · **M11** ออก [`v0.1.0`](https://github.com/maxsrisupan/MinkQuickLax/releases/tag/v0.1.0) แล้ว (2026-09-15) · ถัดไป: ผู้ใช้ตรวจรายการในหัวข้อ 8.2 ที่ยังไม่ติ๊ก (ต้องใช้มือหรือฮาร์ดแวร์จริง) แล้วแก้สิ่งที่เจอเป็น 0.1.x (แก้แล้ว 1 เรื่อง รอออกรุ่น ดู M11 "แก้หลังออก 0.1.0") · จากนั้นเริ่มเฟส 2 (หัวข้อ 7)
> อัปเดตล่าสุด: 2026-09-16
> **ข้อกำหนดอยู่ที่ [SPEC.md](SPEC.md)** เอกสารนี้บอกแค่ว่าทำอย่างไรและทำอะไรก่อน

## วิธีใช้เอกสารนี้
- ทำตามลำดับ milestone · เริ่มจากงานแรกที่ยังไม่ติ๊ก
- **ทำงานเสร็จแล้ว:** ติ๊ก `[x]` และแก้บรรทัด "ความคืบหน้า" ด้านบน
- **ทดสอบ spike แล้ว (M1):** เขียนผลลงหัวข้อ 11
- **ต้องเปลี่ยนการตัดสินใจ:** แก้ SPEC.md และเพิ่มแถวในหัวข้อ 8 ของ SPEC ก่อนแก้โค้ด
- **milestone จะนับว่าเสร็จเมื่อ**
  - build ผ่านโดยไม่มี warning และ test ผ่านทั้งหมด
  - ผ่านรายการตรวจของ milestone นั้น
  - ข้อความใหม่มีครบทั้งไทยและอังกฤษ

---

## 1. เครื่องมือและ library

ใช้เวอร์ชันเสถียรล่าสุดตอนเริ่ม M0 แล้วล็อกเวอร์ชันไว้ใน `Directory.Packages.props`

| ส่วน | เลือก | เหตุผล |
|---|---|---|
| ภาษา / runtime | C# · .NET 10 (LTS) | ใช้ได้ถึงปลายปี 2028 |
| UI | WPF | ทำหน้าต่างโปร่งใส ลอยบนสุด และคุยกับ Win32 ได้ง่าย |
| หน้าตั้งค่า | WPF เปล่ากับ style ของ app เอง (ไม่ใช้ WPF-UI) | หน้าตั้งค่าวาดตามสไตล์ Glass/HUD/Dot Matrix เหมือนแผ่นอื่นของ app (SPEC 4.8, หัวข้อ 12) |
| MVVM | `CommunityToolkit.Mvvm` | source generator ลดโค้ดซ้ำ |
| DI / host | `Microsoft.Extensions.Hosting` | จัดการ service และอายุของ object |
| Win32 interop | `Microsoft.Windows.CsWin32` | สร้าง P/Invoke และ COM ที่ถูกต้องให้อัตโนมัติ |
| icon ที่ tray | `H.NotifyIcon.Wpf` | รองรับ Windows 11 และจัดการกรณี Explorer รีสตาร์ท (ตรวจใน M1) |
| JSON | `System.Text.Json` + source generation | มากับ .NET และเร็ว |
| log | `Serilog` + `Serilog.Sinks.File` | log แบบหมุนไฟล์รายวัน เก็บ 7 วัน |
| คู่มือในแอป | `Microsoft.Web.WebView2` | เปิดไปตรงหัวข้อได้ |
| สร้างคู่มือ | `Markdig` (ใน `tools/ManualBuilder`) | ใช้ภาษาเดียวกับ app ไม่ต้องลง Node |
| SVG icon (F2) | `SharpVectors.Wpf` | WPF แสดง SVG เองไม่ได้ |
| ตัวติดตั้ง / อัปเดต | `Velopack` (NuGet) + `vpk` (dotnet tool) | ติดตั้งแบบรายผู้ใช้ อัปเดตแบบ delta แยกช่องทางได้ |
| test | xUnit v3 (`xunit.v3` 4.x) บน Microsoft.Testing.Platform | มาตรฐานของ .NET · `xunit.v3` 4.x ใช้ได้กับ `dotnet test` แบบใหม่เท่านั้น (หัวข้อ 12) |
| CI / release | GitHub Actions (`windows-latest`) | repo อยู่บน GitHub อยู่แล้ว |
| ฟอนต์ | IBM Plex Sans Thai, Chakra Petch, JetBrains Mono, Doto (ฝังใน app) | ตาม SPEC 5.7 · สัญญาอนุญาต OFL ทุกตัว |
| สัญญาอนุญาต | MIT | เข้ากับ library ทุกตัวข้างบน (MIT, Apache-2.0, BSD, OFL) |

---

## 2. โครงสร้าง repo

```
MinkQuickLax/
├─ CLAUDE.md                      คู่มือสำหรับ Claude ในแชทถัดไป
├─ README.md / README.th.md       หน้าแรกของ repo (อังกฤษ / ไทย)
├─ LICENSE                        MIT
├─ DESIGN-DISCUSSION.md           บันทึกการคุยออกแบบ (ไม่แก้แล้ว)
├─ MinkQuickLax.slnx
├─ global.json                    ล็อกเวอร์ชัน SDK + ใช้ Microsoft.Testing.Platform กับ dotnet test
├─ Directory.Build.props          ค่า build ร่วม: net10.0, nullable, warning เป็น error, version
├─ Directory.Packages.props       เวอร์ชัน NuGet ทั้งหมด
├─ .editorconfig
├─ docs/
│  ├─ SPEC.md
│  └─ PLAN.md
├─ src/
│  ├─ MinkQuickLax.Core/          net10.0 · ไม่อ้าง WPF หรือ Win32 · test ได้บนทุกเครื่อง
│  │  ├─ Model/                   Link, Group, Placement, AppSettings, IconSource
│  │  ├─ Config/                  ConfigStore, ConfigMigrator, BackupManager
│  │  ├─ Layout/                  SnapEngine, CollisionResolver, PositionMapper, TidyLayout
│  │  ├─ Imaging/                 CssFilters (สีโฮโลแกรม/ขาวดำของไอคอนตามสูตร CSS ของ mockup)
│  │  ├─ Launch/                  LinkKindDetector, LaunchRequest
│  │  └─ Abstractions/            IMonitorProvider, IRegistry (M7) · เวลาใช้ `TimeProvider` ของ .NET
│  ├─ MinkQuickLax.Platform/      net10.0-windows · Win32 ทั้งหมดอยู่ที่นี่
│  │  ├─ NativeMethods.txt        รายชื่อ API ให้ CsWin32 สร้าง
│  │  ├─ Interop/                 MessageWindow (หน้าต่างรับข้อความของระบบ ไม่ใช้ WPF)
│  │  ├─ Windowing/               WindowStyles, TopmostKeeper, DwmBackdrop
│  │  ├─ Input/                   MouseProximityTracker (Raw Input), HotkeyService (F2)
│  │  ├─ Displays/                MonitorProvider, DisplayChangeWatcher
│  │  ├─ Shell/                   AppScanner, IconExtractor, Launcher, FaviconFetcher, BrowserCatalog
│  │  ├─ SystemIntegration/       StartupRegistration, SingleInstance, SystemSettingsWatcher, FullscreenDetector (F2)
│  │  └─ Update/                  UpdateService (ห่อ Velopack)
│  └─ MinkQuickLax/               net10.0-windows · WPF · ได้ไฟล์ MinkQuickLax.exe
│     ├─ Program.cs               Main เอง: Velopack → single instance → host
│     ├─ AppServices.cs / AppShell.cs  DI + Serilog / ลำดับตอนเปิด-ปิด
│     ├─ App.xaml(.cs)
│     ├─ Styles/                  Surface.xaml, Controls.xaml, Theme.*.xaml, Design.cs (Glass/HUD/Dot), SurfaceFrame, StyledWindow
│     ├─ Surfaces/                IconWindow (รวมแบบโฟลเดอร์) + IconStyleVisuals (ชิ้นส่วน HUD/Dot Matrix), SurfaceWindow (Menu, Notice), TooltipWindow, GroupPanelWindow, SurfaceHost, EditToolbarWindow, DragOverlays
│     ├─ Settings/                SettingsWindow + Pages + ViewModels
│     ├─ Scanner/                 ScannerWindow + ViewModel
│     ├─ Manual/                  ManualWindow (WebView2), ManualService, ManualTopics (id ที่ app เปิดตรง)
│     ├─ Tray/                    TrayController
│     ├─ Services/                PlacementController, LinkActions, ProximityAnimator, ThemeService, ArrangeController, GroupController, IconCache, Localizer
│     ├─ Resources/Strings.resx   อังกฤษ (ค่าหลัก)
│     ├─ Resources/Strings.th.resx
│     ├─ Assets/Fonts/            IBM Plex Sans Thai, Chakra Petch, JetBrains Mono + ไฟล์ OFL
│     └─ Assets/AppIcon.ico
├─ manual/
│  ├─ th/*.md · en/*.md           เนื้อหาคู่มือ 1 ไฟล์ต่อ 1 บท
│  ├─ keywords.json               คำค้นเพิ่มของแต่ละหัวข้อ
│  ├─ template/                   index.html, manual.css, search.js, strings.json (ข้อความของหน้าคู่มือ)
│  └─ assets/                     รูปภาพ
├─ tools/
│  └─ ManualBuilder/              แปลง manual/ → HTML ภาษาละไฟล์ + ตรวจ id
├─ tests/                         สร้างโปรเจกต์ test พร้อม test แรกเท่านั้น (หัวข้อ 12)
│  ├─ Directory.Build.props       ค่าร่วมของโปรเจกต์ test (xunit.v3)
│  ├─ MinkQuickLax.Core.Tests/    รวม RepositoryRules/ ที่ตรวจกฎของ repo (resx ครบสองภาษา, Core ไม่อ้าง WPF/Win32, manifest)
│  ├─ MinkQuickLax.Platform.Tests/  test ที่ต้องรันบน Windows (สร้างเมื่อมี test แรก)
│  └─ ManualBuilder.Tests/        ตรวจ id, ลิงก์, คำค้น, ไฟล์ HTML และคู่มือจริงใน repo
├─ build/
│  ├─ publish.ps1                 dotnet publish self-contained win-x64
│  └─ pack.ps1                    vpk pack (+ upload)
└─ .github/workflows/
   ├─ ci.yml                      build + test ทุก push/PR
   └─ release.yml                 tag v* → pack → GitHub Release
```

---

## 3. สถาปัตยกรรม

### 3.1 ลำดับตอนเปิด app (`Program.Main`)
1. `VelopackApp.Build()` + hook ก่อนถอนการติดตั้ง (ลบค่าใน Run) → `.Run()` ต้องเป็นบรรทัดแรก
2. **SingleInstance:** จอง Mutex `Local\MinkQuickLax` ถ้ามีตัวอื่นรันอยู่ ส่งคำสั่ง (`show-settings`) ผ่าน named pipe แล้วออก
3. สร้าง host (DI, Serilog) → โหลดค่าตั้ง (กู้จากไฟล์สำรองถ้าจำเป็น)
4. ตั้งภาษาและสไตล์ → สร้าง tray → สร้างหน้าต่างของทุกชิ้นบนจอ
5. เริ่ม TopmostKeeper, DisplayChangeWatcher, MouseProximityTracker, SystemSettingsWatcher
6. ถ้าไม่ได้เปิดด้วย `--startup` และยังไม่มี link → เปิดหน้าสแกน
7. ตรวจอัปเดตแบบไม่รอผล

### 3.2 ชนิดหน้าต่าง

| หน้าต่าง | โปร่งใสแบบ layered | เบลอข้างหลัง | บนสุด | `WS_EX_TOOLWINDOW` | `WS_EX_NOACTIVATE` | หมายเหตุ |
|---|---|---|---|---|---|---|
| IconWindow | ✔ (วาดด้วย CPU) | | ✔ | ✔ | ✔ | 1 ชิ้นต่อ 1 ไอคอนเดี่ยว |
| IconWindow แบบโฟลเดอร์ | ✔ (วาดด้วย CPU) | | ✔ | ✔ | ✔ | 1 ชิ้นต่อ 1 กลุ่ม (หัวข้อ 12) |
| GroupPanelWindow | | accent acrylic (Win11 22H2+) | ✔ | ✔ | ✔ | สร้างตอนกาง ทำลายตอนหุบ · Esc ปิดได้ (`EscapeKeyWatcher`) |
| TooltipWindow | | accent acrylic | ✔ | ✔ | ✔ | มีตัวเดียวใช้ร่วมกัน · คลิกทะลุ (`WS_EX_TRANSPARENT`) |
| GlassMenuWindow | | accent acrylic | ✔ | ✔ | ✔ | เมนูคลิกขวาทั้งที่ไอคอนและที่ tray · ปิดเมื่อคลิกนอกเมนู (ดักด้วย Raw Input) |
| EditToolbarWindow | | accent acrylic | ✔ | ✔ | | รับแป้นพิมพ์ในโหมดแก้ไข |
| DragReadoutWindow | ✔ (วาดด้วย CPU) | | ✔ | ✔ | ✔ | คลิกทะลุ |
| ScannerWindow | | Acrylic ของระบบ | | | | หน้าต่างปกติ ไม่มีกรอบ · active ได้ จึงใช้ `DWMWA_SYSTEMBACKDROP_TYPE` ได้ |
| SettingsWindow | | Glass: Acrylic ของระบบ · HUD/Dot Matrix: ไม่เบลอ | | | | หน้าต่างปกติแบบเดียวกับ ScannerWindow วาดแผ่นตามสไตล์ |
| ManualWindow | | Mica | | | | WebView2 |

- **layered ใช้กับเบลอไม่ได้:** หน้าต่างที่เบลอต้องตั้ง `AllowsTransparency=False` + `WindowChrome` (`GlassFrameThickness=-1`) + พื้นหลังของ `CompositionTarget` โปร่งใส
- **accent acrylic:** `SetWindowCompositionAttribute(WCA_ACCENT_POLICY, ACCENT_ENABLE_ACRYLICBLURBEHIND)` เพราะ Acrylic ของระบบ (`DWMWA_SYSTEMBACKDROP_TYPE`) เป็นพื้นทึบเสมอบนหน้าต่างที่ไม่เคย active (S4) · มุมโค้งใช้ `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND` · ถ้าเรียกไม่สำเร็จให้ใช้พื้นทึบ `a = 0.92`
- **กลไกกลาง:** style ของหน้าต่างตั้งผ่าน `WindowStyles.Apply(hwnd, …)` ที่เดียว
- **สไตล์ (SPEC 5):** แผ่นทุกบานวาดด้วย `SurfaceFrame` ตัวเดียวที่เลือกรูปทรงตามสไตล์ · ไอคอนวาดด้วย visual ของแต่ละสไตล์ใน `IconWindow` · ค่าสี/ฟอนต์/มุมเป็น resource ที่ `ThemeService` เปลี่ยนตามสไตล์และธีม · HUD/Dot Matrix ไม่ใช้ accent acrylic

### 3.3 service หลัก

| service | อยู่ที่ | หน้าที่ |
|---|---|---|
| `ConfigStore` | Core | โหลด/บันทึก JSON · เขียนไฟล์ชั่วคราวแล้วแทนที่ · หน่วงบันทึก 500ms · สำรอง 10 ชุด |
| `ConfigMigrator` | Core | แปลงไฟล์ตาม `schemaVersion` ทีละขั้น |
| `PositionMapper` | Core | สัดส่วน ↔ pixel ตามพื้นที่ทำงานของจอ · เลือกจอสำรองเมื่อจอหาย |
| `SnapEngine` | Core | ชิดกริด 24px / ชิดแนว 8px · คืนเส้นช่วยจัดแนว |
| `CollisionResolver` | Core | หาช่องว่างที่ใกล้ที่สุด (ค้นเป็นวงรอบตามกริด) |
| `TidyLayout` | Core | คำนวณตำแหน่งตอนจัดเรียงใหม่ |
| `UndoHistory` | App | เก็บการเปลี่ยนแปลงในโหมดแก้ไข (Ctrl+Z / Ctrl+Y / ยกเลิก) |
| `PlacementController` | App | สร้าง/ทำลาย/อัปเดตหน้าต่างให้ตรงกับข้อมูล · ซ่อน/แสดงทั้งชุด |
| `EditModeController` | App | เข้า/ออกโหมดแก้ไข · ลาก · สร้างกลุ่มเมื่อลากค้าง |
| `MouseProximityTracker` | Platform | ใช้ Raw Input (`RIDEV_INPUTSINK`) บนหน้าต่าง message-only อ่านตำแหน่งเมาส์ ส่งไม่เกิน 30 ครั้ง/วินาที (ส่งตำแหน่งสุดท้ายตามหลังเสมอ) · แจ้งการกดปุ่มเมาส์ด้วย (ใช้ปิดเมนู/แผงเมื่อคลิกข้างนอก) · ไม่ใช้ timer ตอนเมาส์นิ่ง |
| `ProximityAnimator` | App | รับตำแหน่งจาก tracker แล้วคำนวณความทึบ/ขนาดเป้าหมาย · ไล่ค่าเองด้วย timer 33ms ที่ทำงานเฉพาะตอนมีชิ้นกำลังเปลี่ยน · ไม่ใช้ Storyboard ของ WPF เพราะกิน CPU มากกว่า (S3) |
| `TopmostKeeper` | Platform | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` แล้วสั่ง `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE…)` ให้ทุกชิ้น |
| `DisplayChangeWatcher` | Platform | ดัก `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `WM_SETTINGCHANGE` (พื้นที่ทำงาน), resume จาก sleep แล้วสั่งจัดตำแหน่งใหม่ |
| `MonitorProvider` | Platform | รายการจอ + id ที่คงที่ (`QueryDisplayConfig` → `monitorDevicePath`) + EDID (ผู้ผลิต, รุ่น, serial) ไว้จับคู่สำรอง + พื้นที่ทำงาน + DPI |
| `AppScanner` | Platform | ไล่รายการ `shell:AppsFolder` และ shortcut บน Desktop |
| `IconExtractor` | Platform | `IShellItemImageFactory.GetImage` 256px (`SIIGBF_ICONONLY`) → PNG ลง `IconCache` · ตรวจภาพที่เป็น icon เล็กในกรอบแล้วขอขนาดเล็กลง (S5) |
| `Launcher` | Platform | `ShellExecuteEx` (verb `runas` สำหรับ admin) · `shellApp` เปิดด้วย `shell:AppsFolder\<id>` · link เว็บที่เลือก browser ไว้ส่ง URL ให้ exe ของ browser นั้น |
| `BrowserCatalog` | Platform | รายชื่อ browser จาก `HKLM` และ `HKCU\SOFTWARE\Clients\StartMenuInternet` (ชื่อ, exe, icon) |
| `StartupRegistration` | Platform | อ่าน/เขียน `Run` + อ่าน `StartupApproved` |
| `SystemSettingsWatcher` | Platform | ธีมสว่าง/มืด, Transparency effects, Animation effects, High contrast, โหมดประหยัดแบต |
| `UpdateService` | Platform | Velopack `UpdateManager` + `GithubSource` · แยกช่องทาง |
| `Localizer` | App | สลับภาษาทันทีโดยให้ binding อ่านข้อความผ่าน indexer แล้วแจ้งเปลี่ยน |

### 3.4 ข้อควรระวังในการเขียน
- **ลากหน้าต่าง `WS_EX_NOACTIVATE`:** เขียน drag loop เองด้วย mouse capture + `SetWindowPos` ห้ามใช้ `DragMove()` เพราะต้องชิดกริดระหว่างลาก
- **อ่านปุ่ม Ctrl:** ใช้ `GetKeyState(VK_CONTROL)` แทน `Keyboard.Modifiers` เพราะหน้าต่างไม่มี focus
- **DPI:** ตั้ง PerMonitorV2 ใน `app.manifest` · คำนวณตำแหน่งเป็น pixel จริงของจอ แล้วค่อยแปลงเป็นหน่วยของ WPF
- **thread:** ทุกหน้าต่างอยู่บน UI thread เดียว · งานสแกนและดึง icon/favicon ทำนอก UI thread (ดึง icon ละ ~60ms)
- **หน้าต่าง layered วาดด้วย CPU:** ตั้ง `HwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly` รายหน้าต่างใน `SourceInitialized` · ประหยัด RAM ~40MB ต่อ 30 บานเทียบกับ GPU และ CPU ไม่เพิ่ม (S1, S3) · ห้ามตั้งทั้ง process เพราะหน้าตั้งค่าจะช้า
- **หน้าต่างที่ไม่เคย active:** ไม่มี focus · capture ของเมาส์ได้เฉพาะตอนเมาส์อยู่บนหน้าต่าง · Acrylic ของระบบไม่ทำงาน · การคลิกข้างนอกให้ดักด้วย Raw Input
- **ค่าใน Run ของ Windows:** ใช้ `Environment.ProcessPath` ได้เลย เพราะ Velopack วาง exe ไว้ที่ `…\current\` ซึ่ง path ไม่เปลี่ยนหลังอัปเดต (S6)
- **เปิด link:** ห้ามต่อ string เป็นคำสั่ง shell · ส่ง target กับ argument แยกกัน

---

## 4. ข้อมูล

### 4.1 ที่เก็บไฟล์

| อะไร | ที่ไหน | ถอนการติดตั้งแล้ว |
|---|---|---|
| ค่าตั้ง | `%AppData%\MinkQuickLax\config.json` | เก็บไว้ |
| ไฟล์สำรอง | `%AppData%\MinkQuickLax\backups\config-YYYYMMDD-HHmmss.json` (10 ชุด) | เก็บไว้ |
| icon cache | `%LocalAppData%\MinkQuickLax\cache\icons\` | ถูกลบได้ ไม่เป็นไร |
| log | `%LocalAppData%\MinkQuickLax\logs\` | ถูกลบได้ ไม่เป็นไร |
| ตัวโปรแกรม | จัดการโดย Velopack (ตรวจตำแหน่งจริงใน M1) | ถูกลบ |

### 4.2 `config.json` (schemaVersion 1)

```jsonc
{
  "schemaVersion": 1,
  "links": [
    {
      "id": "0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e",   // GUID แบบ N
      "name": "Visual Studio Code",
      "kind": "app",                              // app | shellApp | file | folder | url | command | msSettings
      "target": "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
      "arguments": "",
      "workingDirectory": "",
      "runAsAdmin": false,
      "icon": { "source": "auto" }                // auto | file | favicon | letter
    },
    {
      "id": "…",
      "name": "GitHub",
      "kind": "url",
      "target": "https://github.com",
      "icon": { "source": "favicon" },
      "browser": "Google Chrome"                  // ชื่อ key ใต้ StartMenuInternet · null หรือไม่มี = browser หลักของ Windows
    }
  ],
  "groups": [
    {
      "id": "…",
      "name": "งาน",
      "display": "folder",                        // folder | bar
      "columns": 4,
      "showLabels": true,
      "linkIds": ["0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e", "…"],
      "icon": { "source": "preview" }             // preview | file
    }
  ],
  "placements": [
    {
      "id": "…",
      "type": "link",                             // link | group (กลุ่มหนึ่งวางได้ 1 ที่)
      "refId": "0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e",
      "monitor": "\\\\?\\DISPLAY#DELA1B2#…",       // monitorDevicePath
      "monitorEdid": "DEL-A1B2-7XK3",             // EDID ผู้ผลิต-รุ่น-serial ใช้หาจอเมื่อสลับพอร์ต (ไม่มีก็ได้)
      "x": 0.92,                                  // จุดกลางไอคอน เป็นสัดส่วนของพื้นที่ทำงาน
      "y": 0.08
    }
  ],
  "settings": {
    "language": "system",                         // system | th | en
    "style": "glass",                             // glass | hud | dot
    "theme": "system",                            // system | light | dark
    "iconSize": 48,
    "idleOpacity": 0.7,
    "proximityRadius": 130,
    "magnify": true,
    "showLabels": false,
    "reduceMotion": "system",                     // system | on | off
    "launchOn": "singleClick",                    // singleClick | doubleClick
    "tooltipDelayMs": 350,
    "groupOpenOn": "click",                       // click | hover
    "gridSize": 24,
    "snap": "gridAndAlign",                       // gridAndAlign | grid | none
    "ctrlDragMove": true,
    "allowOverTaskbar": false,
    "trayClickToggles": true,
    "hideOnFullscreen": true,
    "checkForUpdates": true,
    "updateChannel": "stable",                    // stable | beta
    "hotkeys": { "quickSearch": "Win+Alt+Q", "toggleVisibility": null, "enabled": true },
    "glass": { "tint": "auto", "tintStrength": 0.18, "blur": true, "specular": true }
  }
}
```

- **"เปิดพร้อม Windows" ไม่อยู่ในไฟล์:** อ่านจาก registry ทุกครั้ง
- **อ่านไฟล์:** field ที่ไม่รู้จักให้เก็บไว้ ไม่ทิ้ง · ค่าที่ขาดใช้ค่าเริ่มต้น · ค่า enum ที่อ่านไม่ออกใช้ค่าแรกของ enum (`kind` ที่ไม่รู้จักเป็น `unknown`) แทนการถือว่าไฟล์เสีย
- **ตรวจความถูกต้องตอนโหลด**
  - `refId` ต้องมีจริง
  - `linkIds` ในกลุ่มต้องไม่ซ้ำ
  - กลุ่มหนึ่งมี placement ได้ไม่เกิน 1
  - ถ้าผิดให้ซ่อมแล้วเขียน log

---

## 5. ข้อตกลงในการเขียนโค้ด

- **ภาษาในโค้ด:** ชื่อ, comment, commit message เป็นภาษาอังกฤษ (open source) · เอกสารใน `docs/` เป็นภาษาไทย
- **commit:** รูปแบบ Conventional Commits เช่น `feat(layout): snap icons to grid`
- **ข้อความที่ผู้ใช้เห็น:** ต้องอยู่ใน `Strings.resx` และ `Strings.th.resx` เท่านั้น · มี test ตรวจว่า key ครบทั้งสองไฟล์
  - ชื่อ key เป็น `ส่วน_ชื่อ` แบบ PascalCase เช่น `Tray_Exit`, `Settings_IdleOpacity` · ใส่ `<comment>` ในไฟล์อังกฤษเมื่อความหมายไม่ชัด
  - ในโค้ดใช้ `Localizer.Instance.Bind("key")` หรือ `Localizer.Instance["key"]` · ใน XAML ใช้ `{Binding [key], Source={x:Static services:Localizer.Instance}}`
- **ค่าตัวเลขของดีไซน์:** (สี, ขนาด, เวลา) อยู่ใน `Styles/*.xaml` หรือค่าคงที่ที่มีชื่อ (`GlassDesign`, `HudDesign`, `DotDesign`) ห้ามเขียนตัวเลขลอย ๆ ใน code-behind
- **Core ห้ามอ้าง WPF หรือ Win32:** ถ้าต้องใช้ ให้ผ่าน interface ใน `Abstractions/`
- **Win32:** ประกาศผ่าน CsWin32 (`NativeMethods.txt`) ไม่เขียน `DllImport` เอง ยกเว้นตัวที่ CsWin32 ไม่มี
- **async:** งานที่รอ I/O ใช้ `async` · ห้าม `.Result` / `.Wait()` บน UI thread
- **error:** เปิด link ไม่สำเร็จให้แจ้งผู้ใช้เป็นข้อความที่อ่านเข้าใจ ไม่ใช่ exception message ดิบ และบันทึก log
- **ตั้งค่า build:** `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`

---

## 6. เฟส 1 — ลำดับงาน

### M0 · โครงโปรเจกต์
- [x] repo มี git อยู่แล้วแต่ยังไม่มี commit · เพิ่ม `.gitignore` (VisualStudio), `.gitattributes`, `.editorconfig` · ลบ `test.txt` (ไฟล์ว่าง) ถ้าผู้ใช้ตกลง
- [x] สร้าง repo สาธารณะบน GitHub (ยืนยันชื่อ repo กับผู้ใช้ก่อน) แล้ว push commit แรกเมื่อผู้ใช้สั่ง · repo มีอยู่แล้วที่ `maxsrisupan/MinkQuickLax` · commit ใช้อีเมล `maxsrisupan@gmail.com` (ตั้งเฉพาะ repo นี้)
- [x] สร้าง solution `.slnx` และโปรเจกต์ตามหัวข้อ 2 (ยังไม่ต้องมีโค้ด) · ยกเว้น Platform.Tests และ ManualBuilder.Tests ที่รอ test แรก (หัวข้อ 12)
- [x] `Directory.Build.props` (net10.0, nullable, warning เป็น error, `Version` = `0.1.0`) และ `Directory.Packages.props`
- [x] `LICENSE` (MIT), `README.md`, `README.th.md` (สั้น ๆ: ทำอะไร, สถานะ, วิธี build)
- [x] app เปล่าที่เปิดแล้วมี icon ที่ tray และเมนู "ออก"
- [x] `ci.yml`: restore → build → test บน `windows-latest`
- [x] เติมคำสั่ง build/test/run ลงใน `CLAUDE.md`
- **ตรวจ:** `dotnet build` และ `dotnet test` ผ่าน · CI เขียว · เปิด app แล้วเห็น icon ที่ tray
- **ผลตรวจ (2026-09-15):** build 0 warning · test ผ่าน 7/7 · เปิด app แล้ว icon ขึ้นในถาด (Windows 11 ใส่ไว้ใต้ "แสดงไอคอนที่ซ่อน") · เมนูแสดง "Exit" ตามภาษา Windows (en-US) และกดแล้ว app ปิด · RAM (private) ประมาณ 43MB · CI เขียว ([run แรก](https://github.com/maxsrisupan/MinkQuickLax/actions/runs/34879173757))

### M1 · Spike: พิสูจน์ของที่เสี่ยงก่อน
เขียนเป็นโค้ดทดลองใน branch `spike/*` แล้วบันทึกผลลงหัวข้อ 11 · ถ้าไม่ผ่านให้หยุดและเสนอทางเลือกก่อนทำต่อ

| # | ทดสอบ | ผ่านเมื่อ |
|---|---|---|
| S1 | หน้าต่างโปร่งใส บนสุด toolwindow noactivate 30 บาน | RAM ≤ 120MB · CPU ตอนนิ่งเกือบ 0% · ไม่โผล่ใน Alt+Tab · กดแล้ว focus ไม่หลุดจาก Notepad |
| S2 | ลากหน้าต่าง noactivate ด้วย drag loop เอง + ชิดกริด ข้ามจอที่ scale ต่างกัน | ลากลื่น ไม่กระตุก · ขนาดไม่เพี้ยนเมื่อข้ามจอ |
| S3 | ติดตามระยะเมาส์ด้วย Raw Input + ปรับความทึบ/ขนาด 30 บาน | CPU ตอนขยับเมาส์ ≤ 3% · ไม่ทำให้เมาส์หน่วง |
| S4 | หน้าต่าง Acrylic (WPF-UI หรือ DWM ตรง) บน Win11 · ไม่ layered · บนสุด · noactivate | เบลอจริง · มุม 8px · ไม่แย่ง focus · ใช้เป็นเมนูคลิกขวาได้ |
| S5 | สแกน `shell:AppsFolder` + ดึง icon 256px + เปิด app ทั้งแบบ Win32 และ Store | ได้รายชื่อครบเท่า Start Menu · icon คม · รู้ว่าได้ path จริงของ exe ไหม (มีผลกับ "เปิดตำแหน่งที่เก็บ" และ run as admin) |
| S6 | Velopack: ติดตั้ง → อัปเดตจาก GitHub Release (beta/stable) → ถอน ใน Windows Sandbox | ไม่มี UAC · shortcut มีแค่ใน Start Menu · path ของ exe ที่ใช้ใน Run ไม่เปลี่ยนหลังอัปเดต · hook ถอนการติดตั้งลบ Run ได้ |
| S7 | `H.NotifyIcon.Wpf`: Explorer รีสตาร์ท · เปิดเมนูของเราเองจาก icon ที่ tray | icon กลับมาเอง · เมนู Glass ขึ้นตรงตำแหน่ง |
| S8 | WebView2 เปิดไฟล์ HTML ในเครื่องแล้วไปตรง `#id` · กรณีไม่มี runtime | ไปตรงหัวข้อ · ไม่มี runtime แล้วเปิด browser แทนได้ |
| S9 | id ของจอจาก `QueryDisplayConfig` คงที่หลังรีบูต ถอดแล้วเสียบจอกลับ และสลับพอร์ต | id เดิมเมื่อจอเดิม |

- **ผล (2026-09-15):** ผ่านทั้ง 9 ข้อเท่าที่ทดสอบได้บนเครื่องจอเดียว ไม่มีข้อไหนต้องเปลี่ยนสถาปัตยกรรมหลัก · รายละเอียดและส่วนที่ต้องทดสอบด้วยมืออยู่ในหัวข้อ 11

### M2 · Core: ข้อมูลและการคำนวณตำแหน่ง
- [x] Model + JSON source generation ตามหัวข้อ 4.2 · มี `ConfigEdits` (แก้ข้อมูลโดยให้ link, กลุ่ม, ของบนจอสอดคล้องกัน) เพิ่ม
- [x] `ConfigStore`: เขียนไฟล์ชั่วคราวแล้วแทนที่, หน่วงบันทึก, สำรอง 10 ชุด, กู้จากไฟล์สำรอง, ตรวจและซ่อมข้อมูล
- [x] `ConfigMigrator` (มีขั้น 0→1 ไว้เป็นตัวอย่าง)
- [x] `PositionMapper`: สัดส่วน ↔ pixel, จอหายแล้วใช้จอหลัก, ดึงกลับเข้าจอ, ไม่ทับ taskbar
- [x] `SnapEngine`, `CollisionResolver`, `TidyLayout`
- [x] `LinkKindDetector`: path/URL → `kind`
- **ตรวจ:** unit test ครอบคลุมทุกข้อข้างบน รวมกรณีไฟล์เสีย จอหาย DPI 100/150/200%
- **ผลตรวจ (2026-09-15):** test ของ Core 115 ตัว (รวมทั้งหมด 120) ผ่าน · build 0 warning

### M3 · ไอคอนบนจอและ tray
- [x] Glass ResourceDictionary: สี ธีมสว่าง/มืด, brush, ค่าเวลา (SPEC 5.1–5.4) · สลับธีมตาม Windows ทันที · สีอยู่ใน `Styles/Theme.*.xaml` ความทึบ ขนาด และจังหวะอยู่ใน `Styles/Design.cs` (ย้ายโฟลเดอร์ใน M7)
- [x] `IconWindow`: วาด icon + เงา, ตัวอักษรเมื่อไม่มี icon, ความทึบตอนพัก, ใกล้แล้วชัด/ขยาย, เด้งตอนกด, ไอคอนจางและมี ! เมื่อเป้าหมายหาย
- [x] `TooltipWindow` (ชื่อไอคอน) และแบบแสดงชื่อใต้ไอคอน
- [x] `Launcher`: เปิดทุก `kind` ของ F1, run as admin, เปิดตำแหน่งที่เก็บ, แจ้ง error
- [x] `GlassMenuWindow` + เมนูคลิกขวาที่ไอคอน (SPEC 4.2) · รายการที่เป็นของ M4–M7 แสดงแต่กดไม่ได้จนกว่าจะทำ
- [x] `PlacementController`: สร้างหน้าต่างตามข้อมูล, ซ่อน/แสดงทั้งชุด · มี "จัดเรียงใหม่" และ "วางเพิ่มบนจอ" แล้ว
- [x] `TopmostKeeper`, `DisplayChangeWatcher` (อยู่ใน `SystemEvents`), `SingleInstance`
- [x] tray: คลิกซ้ายซ่อน/แสดง, เมนูคลิกขวา (SPEC 4.7), icon ของ app (.ico 16–256px ทำจากลูกแก้วสี `#2FD9BE/#FFC15A/#FF7250` ใน mockup) · สร้างซ้ำได้ด้วย `build/make-app-icon.ps1`
- [x] ค่าตั้งจาก `SystemSettingsWatcher` (`SystemSettings.Read` + `SystemEvents.SettingsChanged`): Transparency effects, Animation effects, High contrast, ประหยัดแบต
- **ตรวจ:** ใส่ link ใน config.json เอง แล้ว app แสดงไอคอน กดเปิดได้ · focus ไม่หลุด · ไม่อยู่ใน Alt+Tab · เปลี่ยนความละเอียด/ถอดจอแล้วตำแหน่งถูก
- **ผลตรวจ (2026-09-15):** config ทดสอบ 6 link (app, shellApp, folder, url, ไฟล์ที่หาย, ms-settings ชื่อไทย) แสดงครบ · icon จริงของ Notepad/Calculator/Windows, ตัวอักษร "G" และ "ที่" (ไม่หลุดวรรณยุกต์) · ไฟล์ที่หายจางและมี ! กดแล้วขึ้นกล่องแจ้ง · กด Calculator แล้วเปิดและขึ้นหน้าสุด · ชี้ค้างแล้วชื่อขึ้น · เมาส์เข้าใกล้แล้วชัดขึ้น (ความสว่าง 158→209) · เมนูคลิกขวาภาษาไทยขึ้นตรงเคอร์เซอร์และปิดเมื่อคลิกนอก · foreground ไม่เปลี่ยนตอน hover/เมนู · ทุกหน้าต่างเป็น TOOLWINDOW+NOACTIVATE ไม่มี APPWINDOW · คลิก tray แล้วซ่อนหมด tooltip เป็น "(ซ่อนอยู่)" · เปิดซ้ำแล้วตัวที่สองออกใน 189ms และตัวแรกแสดงไอคอนกลับ · จำลอง `WM_DISPLAYCHANGE` แล้วจัดตำแหน่งใหม่ไม่มี error · ไฟล์ค่าตั้งเสียจริง (backslash หาย) ถูกย้ายไปเก็บและเริ่มจากค่าเริ่มต้น · **30 ไอคอน: RAM 36MB, CPU ตอนนิ่ง 0.004%** · test ทั้งหมด 153 ผ่าน
- **ยังไม่ได้ตรวจบนเครื่องนี้ (อยู่ในหัวข้อ 8.2):** เปลี่ยนความละเอียด/ถอดจอจริง · เบลอของเมนูตอนเปิด Transparency effects ในตัว app จริง (โค้ดเดียวกับที่ผ่าน S4) · run as admin (มีหน้าต่าง UAC) · บังด้วยหน้าต่างบนสุดของ app อื่น

### M4 · โหมดแก้ไขและการลาก
- [x] `EditModeController` (ชื่อจริง `ArrangeController`): เข้า/ออก, ไอคอนสั่น, `EditToolbarWindow` (เพิ่ม, จัดเรียงใหม่, ยกเลิก, เสร็จ) · ปุ่ม "เพิ่ม" รอ M5
- [x] ลากพร้อมชิดกริด/แนว + เส้นช่วยจัดแนว + `DragReadoutWindow`
- [x] ปล่อยทับแล้วหาช่องว่าง · ไม่ทับ taskbar
- [x] แป้นพิมพ์: ลูกศร, Shift+ลูกศร, Delete, Ctrl+Z, Ctrl+Y (เพิ่ม Ctrl+Shift+Z = ทำซ้ำ, Enter = เสร็จ)
- [x] `UndoHistory` + ยกเลิกทั้งหมด
- [x] Ctrl+ลากในโหมดใช้งาน
- **ตรวจ:** ทำตาม SPEC 4.4 ครบทุกข้อที่เป็น F1 (ยกเว้นเรื่องกลุ่ม ซึ่งอยู่ใน M6)
- **ผลตรวจ (2026-09-15):** เข้าโหมดแก้ไขจากเมนูคลิกขวา แถบเครื่องมือขึ้นกลางด้านบนและเป็น foreground · ไอคอนสั่นไม่พร้อมกัน · คลิกไอคอนแล้วเลือก (มีวงสี Accent) ไม่เปิด app · ลากแล้วชิดกริด (1512, 432) · ลากผ่านแนวไอคอนอื่นมีเส้นประสีส้ม · ป้ายพิกัดตามเคอร์เซอร์ · ปล่อยทับ Notepad แล้วย้ายไปช่องว่างข้าง ๆ · ลูกศร +24px, Shift+ลง +1px, Ctrl+Z/Ctrl+Y, Delete แล้ว Ctrl+Z คืนมาได้ · "จัดเรียงใหม่" เลื่อนแบบ spring · "ยกเลิก" คืนตำแหน่งทั้งหมด · "เสร็จ" บันทึก · Ctrl+ลากตอนใช้งานย้ายได้และไม่เปิด app · ลากโดยไม่กด Ctrl แล้วปล่อยนอกไอคอนไม่เปิด app · test ทั้งหมด 158 ผ่าน
- **บั๊กที่เจอและแก้ระหว่างตรวจ:** คลิกไอคอนแล้ว WPF ย้าย keyboard focus ไปหน้าต่างไอคอนทำให้ปุ่มลัดหาย (คืน focus ให้แถบเครื่องมือหลังปล่อยเมาส์) · กดไอคอนแล้วลากออกไปปล่อยที่อื่นยังนับเป็นคลิก (ยกเลิกคลิกเหมือนปุ่มทั่วไป) · ค่าตั้งภาษาไทยถูกเขียนเป็น `\u0E..` (เปลี่ยน encoder ให้อ่านได้)

### M5 · เพิ่มและแก้ link
- [x] `AppScanner` + `ScannerWindow` (ค้นหา, กรอง, ติ๊กหลายตัว, แก้ชื่อ, เพิ่ม) · มีป้าย "เพิ่มแล้ว" สำหรับ link ที่มีอยู่ · ตัดตัวถอนการติดตั้งออก
- [x] เพิ่มเอง: ไฟล์, โฟลเดอร์, URL
- [x] `IconExtractor` + `IconCache` · icon จากไฟล์ PNG/ICO · `FaviconFetcher` (อ่าน `<link rel="icon">` แล้วค่อยลอง `/favicon.ico` · timeout 5 วินาที) · icon ที่ได้ตอนสแกนเก็บลง cache ทันทีเพื่อให้ตรงกับ Start
- [x] วางเพิ่มบนจอ, เอาออกจากจอ, ลบ link (ยืนยันพร้อมบอกจำนวนที่วาง) (ทำใน M3)
- [x] เปิดครั้งแรกแล้วยังไม่มี link → เปิดหน้าสแกน แล้ววางที่เลือกไว้มุมขวาบน · ไอคอนใหม่เด้งเข้าทีละตัว
- **ตรวจ:** สแกนเจอ app ทั้ง Win32 และ Store · เพิ่มครบทุก `kind` ของ F1 · link เดียววาง 2 ที่ แก้ชื่อแล้วเปลี่ยนทั้งคู่
- **ผลตรวจ (2026-09-15):** เปิดด้วยโฟลเดอร์ข้อมูลว่างแล้วหน้าสแกนขึ้นเอง พบ 222 รายการ (Start 212 + Desktop) มี icon และป้ายที่มา · ผู้ใช้ลองเองบนเครื่อง: ติ๊ก Visual Studio Code → เพิ่มลงจอ → ย้ายในโหมดจัดวาง → คลิกเปิดได้ · favicon ของ github.com และ wikipedia.org ดึงได้จริง (test ที่เปิดด้วย `MINKQUICKLAX_NETWORK_TESTS=1`) · test ทั้งหมด 165 ผ่าน
- **ยังต้องตรวจต่อ:** เพิ่มไฟล์/โฟลเดอร์/URL ผ่านหน้าสแกนบนเครื่องจริง (ตรรกะแยกประเภทมี test แล้ว) · "แก้ชื่อแล้วเปลี่ยนทั้งสองที่" ต้องมีหน้าแก้ link ก่อน จึงย้ายไปตรวจใน M7 (การอัปเดตหน้าต่างทุกบานเมื่อ link เปลี่ยนมีอยู่แล้วใน `PlacementController.Sync`)

### M6 · กลุ่ม
- [x] โฟลเดอร์ (แผ่นกระจกไม่เบลอ + ไอคอนย่อ 2×2) ทำเป็นแบบหนึ่งของ `IconWindow` แทน `FolderWindow` แยก (หัวข้อ 12)
- [x] `GroupPanelWindow`: กางตามทิศที่มีที่ว่าง, หุบเมื่อกด link/Esc/คลิกที่อื่น, ไม่แย่ง focus · กางแบบขยายจากโฟลเดอร์ 420ms
- [x] ลากไอคอนเดี่ยวเข้ากลุ่ม, ลาก link ออกจากแผง, จัดลำดับในแผง, ย้ายทั้งกลุ่ม (`GroupController` ดูแลการลากในแผง)
- [x] ลากค้าง 0.6 วินาทีบนไอคอนอื่นเพื่อสร้างกลุ่ม (เป้าหมายขยายและมีวงสี Accent เมื่อพร้อม)
- [x] เมนู "เพิ่มเข้ากลุ่ม…" (กลุ่มที่มีอยู่ หรือกลุ่มใหม่ข้างไอคอน), เมนูคลิกขวาที่กลุ่ม, เมนูคลิกขวาที่ link ในแผง ("เอาออกจากกลุ่ม") · "แก้ไขกลุ่ม…" กับ "แก้ไข…" ยังกดไม่ได้จนกว่าจะมีหน้าตั้งค่า (M7)
- **ตรวจ:** SPEC 4.3 และแถวเรื่องกลุ่มในตารางการลากของ SPEC 4.4 ครบ
- **ผลตรวจ (2026-09-15):** config ทดสอบมีกลุ่ม "งาน" (Calculator, Windows) · โฟลเดอร์แสดงไอคอนย่อของ link ข้างใน · คลิกแล้วแผงกางไปทางซ้ายเพราะโฟลเดอร์ชิดขอบขวา มีชื่อกลุ่มและชื่อ link · คลิก Calculator ในแผงแล้ว app เปิดและแผงหุบ · Esc ปิดแผงได้ และปล่อยปุ่ม Esc คืนให้ app อื่นทันทีหลังปิด · เมนูคลิกขวาที่กลุ่มครบตาม SPEC · ลาก GitHub ไปวางบนโฟลเดอร์แล้วเข้ากลุ่มและไอคอนเดี่ยวหาย · ลาก Home ไปค้างบน Notepad แล้วได้กลุ่มใหม่ตรงตำแหน่งของ Notepad · ลาก GitHub ในแผงไปช่องแรกแล้วลำดับเปลี่ยน · ลาก Windows ออกนอกแผงแล้วเป็นไอคอนเดี่ยวตรงที่ปล่อย (ชิดกริด) · ลากโฟลเดอร์ไปที่ใหม่ได้ · ลบกลุ่มแล้ว link ข้างในยังอยู่ · "เพิ่มเข้ากลุ่ม…" → "งาน" ใช้ได้ · คลิกขวาที่ link ในแผง → "เอาออกจากกลุ่ม" ใช้ได้
- **บั๊กที่เจอและแก้ระหว่างตรวจ:** มีการกดซ้ำเข้ามาระหว่างลาก (เมาส์ของผู้ใช้กับสคริปต์ทดสอบชนกัน) แล้วออกจากโหมดแก้ไข ทำให้เส้นช่วยจัดแนวและป้ายพิกัดค้างบนจอ · ตอนนี้กดซ้ำหรือออกจากโหมดแก้ไขระหว่างลากจะวางไอคอนลงตรงนั้นก่อนเสมอ · แผงที่ปิดระหว่างลาก link จะเก็บไอคอนที่ลากอยู่ด้วย

### M7 · หน้าตั้งค่า และ browser ของ link เว็บ
- [x] `StartupRegistration` ที่ตรงกับ Task Manager + ลงทะเบียนตอนเปิดครั้งแรก (เฉพาะตัวที่ติดตั้ง)
- [x] `BrowserCatalog` + `Link.Browser` + `Launcher` เปิด URL ด้วย browser ที่เลือก · เลือกได้ในหน้าสแกนและหน้าแก้ link (SPEC 4.1)
- [x] ระบบสไตล์: `SurfaceFrame` วาดแผ่นทุกบาน, resource ตามสไตล์และธีม, ฟอนต์ Doto · หน้าตาของ Glass ต้องเหมือนเดิม
- [x] `SettingsWindow` แบบแผ่นตามสไตล์ (ไม่ใช้ WPF-UI) + เมนูหมวดด้านซ้าย + ตัวควบคุมตาม SPEC 5.10 (ปุ่มแบ่งช่อง, ตัวเลือกสไตล์มีภาพตัวอย่าง, slider, สวิตช์, ช่องสีกระจก)
- [x] ปรับแล้วมีผลทันที · คืนค่าเริ่มต้นรายหมวด · ค่าขั้นสูงพับไว้
- [x] ช่องค้นหาค่าตั้ง (ดัชนีชื่อค่าตั้งไทย/อังกฤษ + คำค้น)
- [x] หมวด Link: รายการ, ค้นหา/กรอง, แก้ไขทุก field (รวม browser), "วางอยู่ที่ไหน"
- [x] หมวดกลุ่ม: สร้าง/ลบ, ชื่อ, จำนวนคอลัมน์, แสดงชื่อ, ลำดับ link
- [x] หมวดทั่วไป: เปิดพร้อม Windows · สลับภาษาทันที · อัปเดต
- [x] หมวดข้อมูล: สำรองตอนนี้, กู้คืน, เปิดโฟลเดอร์, คืนค่าทั้งหมด, ลบข้อมูลทั้งหมดและออก
- [x] หมวดเกี่ยวกับ: เวอร์ชัน, GitHub, สัญญาอนุญาต, คัดลอกข้อมูลแจ้งปัญหา
- [x] เมนู "แก้ไข…", "แก้ไขกลุ่ม…", หน้าตั้งค่าที่ tray, เปิด app ซ้ำแล้วหน้าตั้งค่าขึ้น, ปุ่ม "แก้ไข" ในกล่องแจ้งว่าไม่พบเป้าหมาย
- **ตรวจ:** ทุกค่าตั้ง F1 มีผลทันทีกับไอคอนจริง · ปิดใน Task Manager แล้วหน้าตั้งค่าแสดงว่าปิด · สลับภาษาแล้วไม่มีข้อความตกค้าง · link เว็บที่ตั้ง Chrome/Edge เปิดใน browser นั้น · แก้ชื่อ link ที่วาง 2 ที่แล้วเปลี่ยนทั้งคู่ (ยกมาจาก M5) · ใช้หน้าตั้งค่าด้วยแป้นพิมพ์ได้ครบ
- **ผลตรวจรอบแรก (2026-09-15):** หน้าตั้งค่าเปิดจากการเปิด app ซ้ำ ทุกหมวดแสดงครบ ตัวควบคุมตรงกับแผงปรับหน้าตาใน mockup (ปุ่มเลือกสไตล์มีภาพตัวอย่าง, slider แสดงค่า, สวิตช์, ช่องสีกระจก) · เลือก HUD และ Dot Matrix แล้วหน้าตั้งค่าและเมนูคลิกขวาเปลี่ยนทันที (HUD ตัดมุมโปร่งใส มีเส้นมุม cyan และเส้นสแกน · Dot Matrix แผ่นทึบมุมโค้ง ตัวเลือกที่เลือกกลับสี) · Glass ยังเหมือนเดิม · สลับเป็น English แล้วทั้งหน้าต่างเปลี่ยนทันที · ค้นหา "size" เจอ "ขนาดไอคอน" · แก้ชื่อ GitHub ที่วาง 2 ที่ในหน้าแก้ link แล้ว link และรายการเปลี่ยน · หน้าแก้ link แสดง browser ที่เลือกพร้อม icon · ลงทะเบียน browser ทดสอบชั่วคราว (Character Map) แล้วคลิกไอคอน GitHub เปิดด้วยโปรแกรมนั้นจริง จากนั้นลบรายการทดสอบออก · สำรอง/รายการไฟล์สำรองแสดงวันที่ · test ทั้งหมด 211 ผ่าน
- **บั๊กที่เจอและแก้ระหว่างตรวจ:** Slider รับค่าคงที่ int จาก XAML ไม่ได้ (ทำ `SliderLimits` เป็น double) · ComboBox แสดงชื่อ type แทนข้อความ (template ขาด `ContentTemplateSelector`) · หน้าตั้งค่าที่เปิดจากการเปิด app ซ้ำขึ้นหลังหน้าต่างอื่น (ตัวที่เปิดซ้ำเรียก `AllowSetForegroundWindow` ก่อนส่งคำสั่ง) · ตัวอักษรไทยใน JetBrains Mono และ Doto ใช้ฟอนต์ระบบตัวเล็ก (เพิ่ม IBM Plex Sans Thai เป็นฟอนต์สำรอง)
- **ยังต้องตรวจ:** ปิด "เปิดพร้อม Windows" ใน Task Manager แล้วหน้าตั้งค่าแสดงว่าปิด (ตรรกะมี test กับ registry ทดสอบแล้ว · ไม่ได้กดสวิตช์บนเครื่องนี้เพราะจะลงทะเบียนตัว build ทดสอบใน Run) · ลองกับ Chrome/Edge จริง · ใช้ด้วยแป้นพิมพ์ทั้งหน้าต่าง · ลากค่าตั้งแต่ละตัวแล้วดูผลบนไอคอนจริงทีละข้อ

### M8 · สไตล์ HUD และ Dot Matrix
- [x] resource ของ HUD และ Dot Matrix (สี ฟอนต์ มุม) ตาม SPEC 5.8, 5.9 · HUD ใช้สีมืดเสมอ · Dot Matrix ตามธีม
- [x] แผ่นพื้นทุกบาน (ชื่อไอคอน, เมนู, กล่องแจ้งเตือน, แถบเครื่องมือ, แผงกลุ่ม, หน้าสแกน, หน้าตั้งค่า) ตามสไตล์ · มุมที่ตัด/โค้งโปร่งใส
- [x] ไอคอน HUD: แผ่นรองตัดมุม, โฮโลแกรมตอนพัก (ภาพย้อมสีสร้างครั้งเดียวต่อภาพแล้วไล่ความทึบตาม t · หัวข้อ 12), เส้นสแกน, เรืองแสง, วงเล็บล็อกเป้า, เส้นแสงกวาด
- [x] ไอคอน Dot Matrix: วงกลม, หน้ากากเม็ดจุดที่รัศมีเปลี่ยนตาม t, ขาวดำตอนพัก, วงจุดหมุน
- [x] ชื่อไอคอนแบบป้ายด้านข้าง (ลำดับ ชื่อ ข้อมูล) ของ HUD และ Dot Matrix · ชื่อใต้ไอคอนตามสไตล์
- [x] โฟลเดอร์, โหมดแก้ไข (วงเล็บ amber / วงจุดหมุน), เส้นช่วยจัดแนว, ป้ายพิกัด, กดเปิด ตามสไตล์
- [x] เปลี่ยนสไตล์แล้วทุกชิ้นเปลี่ยนทันทีพร้อมจังหวะไล่ทีละตัว (ข้ามเมื่อลดภาพเคลื่อนไหว)
- **ตรวจ:** ภาพหน้าจอทุกชิ้นทั้ง 3 สไตล์ × ธีมสว่าง/มืด เทียบกับ mockup · 30 ไอคอน RAM และ CPU ยังอยู่ในเป้า (SPEC 6)
- **ผลตรวจ (2026-09-15):** จับภาพไอคอน ชื่อใต้ไอคอน ป้ายด้านข้าง เมนูคลิกขวา แถบเครื่องมือ โหมดแก้ไข และการลาก ใน HUD, Dot Matrix มืด, Dot Matrix สว่าง และ Glass มืด แล้วเทียบกับค่าใน `skins.css` ของ mockup
  - **HUD:** แผ่นรองตัดมุมมีเส้นทแยง · ภาพเป็นโฮโลแกรมมีเส้นสแกนตอนพัก และเป็นสีจริงพร้อมแสงเรืองและวงเล็บเมื่อชี้ · ป้ายด้านข้างมีเส้นเชื่อม ลำดับ `03` ชื่อ และบรรทัด `เว็บ · GITHUB.COM` ที่พิมพ์ทีละตัว ย้ายไปซ้ายเมื่อขวาไม่พอ · ชื่อใต้ไอคอนตัวพิมพ์ใหญ่บนพื้นเข้ม · เมนูมีหัว cyan ตัวพิมพ์ใหญ่ · แถบเครื่องมือมีสี่เหลี่ยม amber และปุ่ม "เสร็จ" ตัดมุม · โหมดแก้ไขมีวงเล็บ amber กะพริบ ไม่สั่น · เส้นช่วยจัดแนวเป็นเส้นประ cyan และป้ายพิกัดมุมเหลี่ยม
  - **Dot Matrix:** ไอคอนวงกลมเป็นเม็ดจุดขาวดำตอนพัก จุดขยายติดกันเป็นภาพสีจริงเมื่อชี้ พร้อมวงจุดหมุน · ป้ายด้านข้างมีเลข Doto ตัวใหญ่ · ชื่อใต้ไอคอนเป็นแคปซูล · เมนูมีจุดแดงนำหัวและเส้นคั่นเป็นจุด · แถบเครื่องมือมีจุดแดงและปุ่มแคปซูล · โหมดแก้ไขมีวงจุดทุกตัว · เส้นช่วยจัดแนวเป็นจุดแดง ป้ายพิกัดเป็นแคปซูลฟอนต์ Doto · ธีมสว่างเปลี่ยนแผ่นเป็นสีอ่อนทั้งหมด
  - **Glass:** หน้าตาเหมือนก่อน M8 (ชื่อขึ้นเหนือไอคอน, เงา, สั่นในโหมดแก้ไข)
  - **เปลี่ยนสไตล์ขณะเปิดอยู่** (เลือกในหน้าตั้งค่า): Glass → HUD ทุกไอคอนล็อกเป้าและกวาดแสงไล่ทีละตัว · HUD → Dot Matrix จุดเต็มวงแล้วหดกลับ · กลับเป็น Glass ครบ ไม่มีวงเล็บหรือวงจุดค้าง
  - **30 ไอคอน** (Core Ultra 7 165H, 22 thread · ขยับเมาส์วนในไอคอนทีละตัว 12 วินาที): CPU ตอนนิ่ง Glass 0.007% / HUD 0.028% / Dot Matrix 0.15% (ขณะมีไอคอนที่ชี้อยู่หมุนวงจุด) · ตอนขยับเมาส์ 2.6% / 2.8% / 2.4% · private working set 91–107MB (เป้า 120MB) · โหมดแก้ไข Glass 3.8% (สั่น มีมาตั้งแต่ M4) / HUD 0.36% / Dot Matrix 2.5%
  - test ทั้งหมด 220 ผ่าน (ข้าม 2 ที่ต้องใช้อินเทอร์เน็ต)
- **บั๊กที่เจอและแก้ระหว่างตรวจ:** ช่องว่างระหว่างเม็ดจุดและส่วนโปร่งใสของภาพ icon คลิกทะลุไปหน้าต่างข้างหลัง (ใส่พื้นทึบ 1/255 ให้ทั้งช่องไอคอน ตามที่ mockup แนะนำ) · เปิด app ครั้งแรกโดยไม่ขยับเมาส์แล้วค่าความใกล้เป็น 0 แม้เมาส์อยู่บนไอคอน (อ่านตำแหน่งเมาส์จริงตอนคำนวณใหม่) · เปลี่ยนสไตล์ตอนชื่อไอคอนขึ้นอยู่แล้วป้ายยังเป็นแบบเดิม (ซ่อนก่อน) · ไอคอนที่ชี้อยู่ตอนเปลี่ยนสไตล์ไม่มีวงเล็บ/วงจุด · วงจุดหมุนที่ 60fps กิน CPU 0.5% ต่อไอคอนที่ชี้ (ลดเป็น 20fps เหลือ 0.15%)
- **ยังต้องตรวจ (รอบแรก):** ภาพตอนกดเปิด · กล่องแจ้งเตือน แผงกลุ่ม หน้าสแกนใน HUD/Dot Matrix หลังเปลี่ยนไอคอน (ตรวจแผ่นพื้นแล้วใน M7) · ลดภาพเคลื่อนไหว · High contrast · จอ scale 125–150% (ภาพตอนกดเปิดและลดภาพเคลื่อนไหวตรวจแล้วด้านล่าง)
- **ปรับตาม mockup รอบสอง (2026-09-15):** ผู้ใช้ขอให้ไอคอน โฟลเดอร์ ป้ายด้านข้าง โหมดแก้ไข และเส้นช่วยจัดแนวตรงกับ mockup · เรนเดอร์ mockup ใน Edge headless (ชี้ โหมดแก้ไข ลาก) เทียบกับภาพจาก app จริงใน HUD, Dot Matrix มืด/สว่าง แล้วแก้
  - **สีโฮโลแกรม (HUD):** สูตรเดิมมืดกว่า mockup (เทา 128 ได้ 64,138,162 แทน 87,196,235) · ใช้ matrix ของ CSS filter ชุดเดียวกับ mockup ทีละขั้น (`Core/Imaging/CssFilters`) · test เทียบกับค่าที่ Edge เรนเดอร์ (โฮโลแกรม 9 สี ขาวดำ 7 สี) ตรงทุกไบต์ (ขาวดำของ Dot Matrix ก็ตรงอยู่แล้ว ย้ายมาใช้ที่เดียวกัน)
  - **เส้นช่วยจัดแนว:** ถ้าระยะเท่ากัน เส้นไปอยู่ที่ขอบของไอคอนข้าง ๆ แทนแนวกลางไอคอน (ไอคอนบนกริด 72px เจอแทบทุกครั้ง) · แก้ให้แบบกลาง↔กลางชนะ มี test · เส้นยาวตลอดพื้นที่ทำงานเหมือน mockup
  - **ป้ายพิกัด:** อยู่ข้างไอคอน (ขวา หรือซ้ายเมื่อไม่พอ) กลางแนวตั้ง ข้อความ `X 0456  Y 0312` แทน `456, 312` ข้างเมาส์ · ทุกสไตล์เหมือน mockup
  - **ความใกล้ t:** วัดจากขอบไอคอน จึงเป็น 1 ทั้งไอคอนตาม SPEC 5.8 · เดิมวัดจากจุดกลาง ที่ขอบไอคอนได้ 0.82 เม็ดจุดของ Dot Matrix จึงยังไม่ติดกัน
  - **โหมดแก้ไขและการลาก:** ไม่ขยายตามเมาส์ (เดิม 1.16 × 1.12 จนวงเล็บ HUD ล้นหน้าต่างไอคอนและถูกตัดเหลือมุมเดียว) · ไอคอนที่กำลังลากวงเล็บ amber ค้างไม่กะพริบ · วงเล็บและจุดในแถบเครื่องมือกะพริบแบบดับหมดครึ่งรอบ (เดิมเหลือ 15–20%) จุดในแถบ HUD รอบละ 1 วินาที Dot Matrix 1.2 วินาที
  - **โฟลเดอร์:** ไอคอนย่อเล็กเกินไป (~10px) · Glass 36% ตาม SPEC 5.2 (เดิมได้จริง 30%) · HUD 32% ไม่ชนมุมตัด · Dot Matrix 30% อยู่ในวงกลม
  - **ป้ายด้านข้าง:** จางเข้า 150ms และเลื่อนออกจากไอคอน 6px แบบ spring ตาม mockup · Glass ยังขึ้นทันที (หัวข้อ 12)
  - **แคปซูลของ Dot Matrix:** `Border` ของ WPF ที่มุม 99 วาดเป็นวงรี (ปุ่ม "เสร็จ" ป้ายพิกัด ปุ่มแบ่งช่องในหน้าตั้งค่า) · เพิ่ม `RoundedBorder` · ชื่อใต้ไอคอนเป็นแคปซูลจริง · ภาพตัวอย่าง Dot Matrix บนตัวเลือกที่เลือกอยู่มองไม่เห็นเม็ดจุด (ใช้สีตัวอักษรของปุ่มแทนสีหลักของสไตล์)
  - **ผลตรวจ:** ภาพหลังแก้ของไอคอน โฟลเดอร์ ป้ายด้านข้าง (ขวาและซ้าย) ชื่อใต้ไอคอน โหมดแก้ไข การลาก และแถบเครื่องมือ ใน HUD, Dot Matrix มืด/สว่าง ตรงกับ mockup · Glass มีเฉพาะป้ายพิกัด เส้นช่วยจัดแนว และขนาดไอคอนย่อในโฟลเดอร์ที่เปลี่ยน · หน้าตั้งค่าหมวดหน้าตาทั้ง 3 สไตล์ · build 0 warning · test ทั้งหมด 263 (ผ่าน 261 ข้าม 2 ที่ต้องใช้อินเทอร์เน็ต)
  - **ยังต่างจาก mockup:** mockup ไม่มีโฟลเดอร์ ขนาดไอคอนย่อของ HUD/Dot Matrix จึงกำหนดเอง · เส้นแสงกวาดของ HUD ผู้ใช้เลือกให้วิ่งบนลงล่างตาม mockup แล้ว (SPEC หัวข้อ 8) ออกใน 0.1.0-beta.2
- **ตรวจเพิ่มบนเครื่องที่สอง (2026-09-15, i7-12700KF, 2 จอ 2560×1440 scale 100%, Windows 11 build 26200, ปิด Transparency และ Animation effects):**
  - **กดเปิด** (link ทดสอบชี้ `rundll32.exe` ที่ไม่เปิดหน้าต่าง · ตั้ง "ลดภาพเคลื่อนไหว" ของ app เป็นปิด): Glass เด้งพร้อมวงขาวกระจาย · HUD กรอบ cyan ขยายและเส้นแสงกวาด (หลังแก้ วิ่งบนลงล่าง: 31ms กลางแผ่น → 127ms ขอบล่าง) · Dot Matrix วงจุดกระจายออก
  - **ลดภาพเคลื่อนไหวตาม Windows:** Animation effects ปิดอยู่ กดเปิดแล้วไม่มีภาพเคลื่อนไหว (จับภาพ 144 เฟรมใน 0.9 วินาที)
  - **เจอแต่ยังไม่แก้:** เปิด app ตอนเมาส์อยู่บนไอคอนแล้วไม่ขยับเมาส์ ไอคอนยังเป็นสภาพพัก (t = 0) อย่างน้อย 6 วินาที จนกว่าจะขยับ (ที่แก้ไว้ใน M8 ยังไม่ครอบกรณีนี้) · คลิกขวาที่ไอคอนภายใน ~2.5 วินาทีหลังไอคอนขึ้นบางครั้งเมนูไม่ขึ้น หลังจากนั้นขึ้นทุกครั้ง
- **ยังต้องตรวจ:** กล่องแจ้งเตือน แผงกลุ่ม หน้าสแกนใน HUD/Dot Matrix หลังเปลี่ยนไอคอน · High contrast · จอ scale 125–150%

### M9 · ภาษา
- [x] ไล่ตรวจว่าไม่มีข้อความเขียนตรงในโค้ดหรือ XAML
- [x] test: key ใน `Strings.resx` กับ `Strings.th.resx` ตรงกัน
- [x] ตรวจทุกหน้าจอทั้งสองภาษา: ภาษาไทยไม่โดนตัดสระ · ภาษาอังกฤษไม่ล้นปุ่ม
- **ตรวจ:** ภาพหน้าจอทุกหน้าในทั้งสองภาษาและทุกสไตล์ ไม่มีปัญหาข้างบน
- **ผลตรวจ (2026-09-15):**
  - **ไล่โค้ด:** string literal ทุกตัวในโปรเจกต์ app เป็นชื่อ resource, ชื่อไฟล์/path, ข้อความ log หรือข้อมูลแจ้งปัญหา (ภาษาอังกฤษโดยตั้งใจ) · XAML มีแค่สัญลักษณ์ ✕ ↑ ↓ – ▸ และปุ่มเหล่านั้นมีชื่อสำหรับ screen reader จาก resource
  - **test ใหม่** (`StringResourcesTests`): ตัวแทน `{0}` ตรงกันทั้งสองภาษา · ทุก key ที่โค้ดและ XAML เรียก (146 จุดในโค้ด, 220 จุดใน XAML) มีอยู่จริง · ทุก `LinkKind` มี `Kind_*` และ `Meta_*` · ทุกหน้าตั้งค่ามี `Page_*` · XAML ไม่มีข้อความเขียนตรง
  - **ภาพหน้าจอ:** หน้าตั้งค่าทั้ง 9 หมวด × 3 สไตล์ × 2 ภาษา (54 ภาพ) และชื่อไอคอน/ป้ายด้านข้าง, กล่องแจ้งเตือน, เมนูคลิกขวา, แถบเครื่องมือ, หน้าสแกน, แผงกลุ่ม × 3 สไตล์ × 2 ภาษา · เปิดดูภาพ Glass ครบทุกภาพ และ HUD/Dot Matrix ครบทุกชนิดของหน้าจอ (หน้าตั้งค่า 6 หมวดที่ตัวควบคุมต่างกันมากที่สุด + แผ่นลอยทุกแบบ) · ภาษาไทยไม่โดนตัดสระในภาพที่ดู
  - test ทั้งหมด 226 ผ่าน (ข้าม 2 ที่ต้องใช้อินเทอร์เน็ต)
- **แก้ระหว่างตรวจ:**
  - หน้าแก้ link ภาษาอังกฤษ: ปุ่ม "Choose an image…" ล้นขอบขวา (แถวนี้ขึ้นบรรทัดใหม่ได้แล้ว)
  - หัว "ขั้นสูง"/"Advanced" เป็นตัวสีดำบนพื้นเข้มจนแทบมองไม่เห็น (ปุ่มในหัวไม่รับสีตัวอักษรจาก Expander)
  - HUD และ Dot Matrix: ข้อความไทยที่ใช้ฟอนต์ mono (หัวกลุ่มค่าตั้ง หัวเมนู ชื่อใต้ไอคอน บรรทัดข้อมูล แถบเครื่องมือ) เล็กจนอ่านยาก เพราะ IBM Plex Sans Thai ตัวเล็กกว่า JetBrains Mono ที่ขนาดเดียวกัน · ทำฟอนต์ผสม `Mink Mono` (หัวข้อ 12)
  - หน้าสแกนภาษาอังกฤษ: ข้อความในช่อง URL ถูกตัด ("Paste a link and press Enter") · ภาษาไทย: ชื่อ browser หลักถูกตัด ("browser หลัก")
  - หน้าแก้ link: browser ที่ถอนการติดตั้งไปแล้วทำให้ช่อง "เปิดด้วย" ว่าง (แสดงเป็น browser หลักซึ่งเป็นตัวที่เปิดจริง)
  - Dot Matrix: icon ในรายการของหน้าสแกนและหน้าตั้งค่าเป็นวงกลมตาม SPEC 5.9 (เดิมยังเป็นสี่เหลี่ยม)
- **ยังต้องตรวจ:** เมนูที่ tray (ต้องคลิกที่ taskbar ซึ่งเป็นหน้าต่างของ Explorer) · กล่องแจ้งเตือนเปิดครั้งแรกและกู้ไฟล์ค่าตั้ง · คู่มือ (M10)

### M10 · คู่มือ
- [x] `ManualBuilder`: อ่าน `manual/<lang>/*.md` + `keywords.json` → HTML ไฟล์เดียวต่อภาษา (ฝัง CSS, JS, ฟอนต์, รูปแบบ base64, ดัชนีค้นหา)
- [x] ตรวจตอน build: id หัวข้อไม่ซ้ำ, หัวข้อมีครบทั้งสองภาษา (ภาษาอังกฤษขาดให้เตือนใน F1), id ที่หน้าตั้งค่าอ้างถึงมีจริง
- [x] template: สไตล์ Glass, ธีมสว่าง/มืด, สารบัญ, ปุ่มสลับภาษา, พิมพ์ได้
- [x] `search.js`: substring + `Intl.Segmenter('th')` + คำค้นเพิ่ม, `/` และ `Ctrl+K`, ↑↓ Enter, ไฮไลต์
- [x] `ManualWindow` (WebView2) + เปิดใน browser เมื่อไม่มี runtime
- [x] เนื้อหาภาษาไทยของฟีเจอร์ F1 ทุกบทใน SPEC 4.9
- [x] ผูกการ build คู่มือเข้ากับ build ของ app (MSBuild target) แล้วคัดลอกผลลัพธ์ไปไว้ที่ `Manual/`
- **ตรวจ:** ปิดเน็ตแล้วเปิดคู่มือได้ · ค้น "คีย์ลัด", "hotkey", "ซ่อน" เจอหัวข้อที่ถูก · คำไทยกลางประโยคก็เจอ
- **ผลตรวจ (2026-09-15):**
  - **เนื้อหา:** 10 บทตาม SPEC 4.9 (เริ่มต้นใช้งาน, เพิ่ม link, ไอคอนและกลุ่ม, จัดวาง, ซ่อน/แสดง, หน้าตั้งค่าทุกข้อ, แก้ปัญหา, ข้อมูล, ความเป็นส่วนตัว, คำถามที่พบบ่อย) เขียนจากพฤติกรรมของโค้ดจริง (ค่าเริ่มต้น ช่วงค่า เมนู ปุ่ม path) · ฟีเจอร์ F2 เขียนแค่ว่าจะมาในรุ่นถัดไป · `keywords.json` มีคำค้นไทย/อังกฤษทุกหัวข้อหลัก
  - **build:** `dotnet build` สร้าง `Manual\th.html` และ `Manual\en.html` (ไฟล์ละ ~640KB รวมฟอนต์) ข้าง exe · ภาษาอังกฤษแสดงบทภาษาไทยพร้อมป้าย "ยังไม่ได้แปล" และพิมพ์ note ทีละบทโดยไม่ทำให้ build ล้ม · ไม่มี error ของ id/ลิงก์/คำค้น/หัวข้อที่ app เปิด
  - **ไม่ใช้เน็ต:** ในไฟล์ไม่มีการอ้างทรัพยากรภายนอก (มีแค่ข้อความ `https://` ในเนื้อหา) และ CSP ของหน้าเป็น `default-src 'none'`
  - **หน้าคู่มือ** (Edge headless): ธีมสว่าง/มืด, สารบัญไฮไลต์หัวข้อ, หน้าต่างแคบ 480px สารบัญพับเป็นปุ่มเมนู · ค้น "คีย์ลัด" → หัวข้อคีย์ลัด · "hotkey" → หัวข้อเดียวกันจากคำค้นเพิ่ม · "ซ่อน" → 7 หัวข้อ รวมคำกลางประโยค ("ให้ซ่อน", "ตอนซ่อนอยู่") พร้อมไฮไลต์ · "ไอคอนหาย" → "ไอคอนหายไปทั้งหมด" เป็นอันดับแรก
  - **ในแอป:** หน้าตั้งค่า → เกี่ยวกับ → "เปิดคู่มือ" เปิดหน้าต่างคู่มือ (WebView2) ที่หัวข้อ `settings-about` ธีมมืดตาม app ปุ่มพิมพ์ในหน้าซ่อนเพราะมีปุ่มของหน้าต่างแทน
  - test ทั้งหมด 241 ผ่าน (ข้าม 2 ที่ต้องใช้อินเทอร์เน็ต) · ManualBuilder.Tests 15 ข้อ
- **ยังต้องตรวจ:** เปิดจากเมนูที่ tray และ F1 ในหน้าตั้งค่า (โค้ดผูกแล้ว แต่การทดสอบต้องคลิก tray หรือส่งปุ่มให้หน้าต่าง) · ปุ่ม "เปิดใน browser" และ "พิมพ์" (จะเปิด browser/หน้าพิมพ์บนเครื่องผู้ใช้) · เครื่องที่ไม่มี WebView2 Runtime · ปิดเน็ตจริง · เนื้อหาภาษาอังกฤษ (F2)

### M11 · ติดตั้ง อัปเดต และออกรุ่นแรก
- [x] `UpdateService` + แจ้งเตือนที่ tray + "รีสตาร์ทตอนนี้" + ช่องทาง beta/stable + ปิดการตรวจได้
- [x] `build/publish.ps1` และ `build/pack.ps1` (self-contained win-x64, shortcut Start Menu, icon, ชื่อ app)
- [x] `release.yml`: push tag `v*` → build → test → pack → อัปโหลด GitHub Release (tag มี `-beta` ให้ออกเป็น prerelease ช่องทาง beta)
- [ ] ทดสอบตามหัวข้อ 8.2 (ไม่มี Windows Sandbox ทั้งสองเครื่อง · ตรวจบนเครื่องจริงผ่านแล้ว 8 จาก 19 ข้อ ที่เหลือต้องใช้มือหรือฮาร์ดแวร์จริง ดูหัวข้อ 8.2)
- [x] ออก `v0.1.0-beta.1` (2026-09-15) → `v0.1.0-beta.2` → `v0.1.0` (2026-09-15) · ผู้ใช้เลือกออก `v0.1.0` ทันทีหลังอัปเดต beta.2 ผ่าน โดยข้ามช่วงใช้เอง 1 สัปดาห์
- **ตรวจ:** เครื่องใหม่ติดตั้งจากหน้า Release ได้ · อัปเดต beta.1 → beta.2 เองได้ · ถอนแล้วไม่เหลือรายการใน Run
- **ผลตรวจ (2026-09-15):**
  - `build/publish.ps1 -Version 0.1.0-beta.1` ได้ตัว self-contained + ReadyToRun 298 ไฟล์ พร้อม `Manual\th.html` และ `en.html` (21 วินาที) · `build/pack.ps1` ได้ `MinkQuickLax-beta-Setup.exe` 73MB, `-full.nupkg`, `Portable.zip`, `releases.beta.json` และหน้า release notes ไทย/อังกฤษ (ขั้นติดตั้ง, SmartScreen, หัวข้อของเวอร์ชันใน `CHANGELOG.md`)
  - เปิดตัวที่ publish (ยังไม่ติดตั้ง) ด้วยข้อมูลทดสอบ: ไอคอนขึ้นใน 0.7–0.8 วินาทีเมื่อเปิดซ้ำ (เป้า 2 วินาที) · ครั้งแรกหลัง publish 4.5 วินาที (ไฟล์ใหม่ยังไม่อยู่ใน cache ของดิสก์และถูกสแกน) · private working set 70MB
  - test ใหม่: ชื่อช่องทางตรงกับ pack.ps1 · ตัวที่ไม่ได้ติดตั้งตอบว่า NotInstalled โดยไม่ออกเน็ต
  - **ติดตั้งจริงบนเครื่องนี้** (ผู้ใช้อนุญาตให้ติดตั้งแล้วถอน · ตั้ง `MINKQUICKLAX_DATA_DIR` ให้ตัวติดตั้งจึงไม่แตะ `%AppData%\MinkQuickLax`): ติดตั้งเสร็จใน 2 วินาที ไม่มี UAC process ไม่ได้ elevated · app เปิดเองและขึ้นหน้าสแกน (เปิดครั้งแรก) · shortcut มีแค่ใน Start Menu · ค่าใน Run เป็น `"…\MinkQuickLax\current\MinkQuickLax.exe" --startup` · ค่าตั้งได้ช่องทาง `beta` ตามตัวติดตั้ง · 30 วินาทีหลังเปิดตรวจอัปเดตกับ GitHub ได้ ("0.1.0-beta.1 is the newest version") · `Update.exe uninstall --silent` แล้ว app ปิด ค่าใน Run และ StartupApproved, shortcut, รายการใน Apps และโฟลเดอร์ติดตั้งหายหมด
  - **ออกรุ่น:** ผู้ใช้ยืนยันให้ออก `v0.1.0-beta.1` · รอบแรก workflow ล้มที่ขั้น upload (ไม่ได้สร้างหน้า Release) จากบั๊ก 3 จุดใน `pack.ps1` แก้แล้วย้าย tag ไป commit ที่แก้ · รอบสองผ่าน: [หน้า Release](https://github.com/maxsrisupan/MinkQuickLax/releases/tag/v0.1.0-beta.1) เป็น pre-release มี `MinkQuickLax-beta-Setup.exe`, `-full.nupkg`, `Portable.zip`, `releases.beta.json` และคำอธิบายติดตั้ง/SmartScreen/สิ่งที่เปลี่ยนทั้งไทยและอังกฤษ
- **บั๊กที่เจอและแก้ระหว่างตรวจ:** คนที่ติดตั้งจากตัว beta ได้ค่าตั้งช่องทาง "ปกติ" จึงจะไม่ได้ beta ตัวถัดไป (ตอนเปิดครั้งแรกใช้ช่องทางของตัวติดตั้ง) · `pack.ps1`: ตัวแปร `$upload` ทับ switch `-Upload` (ชื่อตัวแปรใน PowerShell ไม่สนตัวพิมพ์), vpk 1.2 ไม่รับ `--publish true` ต้องใส่แค่ `--publish`, token ว่างทำให้ argument เลื่อน
- **ข้อจำกัดที่รู้:** ถ้ามีโฟลเดอร์ `%LocalAppData%\MinkQuickLax` อยู่ก่อนติดตั้ง (เคยรันตัว build หรือ Portable.zip ซึ่งเขียน log/cache ไว้ที่นั่น) ตัวติดตั้งจะขึ้น "MinkQuickLax is already installed" ให้เลือก Repair · เครื่องผู้ใช้ทั่วไปที่ติดตั้งครั้งแรกไม่เจอ
- **ผลตรวจรอบสอง (2026-09-15, เครื่องที่สอง: i7-12700KF, 2 จอ 2560×1440 scale 100%, Windows 11 build 26200, ธีมมืด, ปิด Transparency และ Animation effects):**
  - **เครื่องไม่มี .NET SDK:** ลง SDK 10.0.401 แบบรายผู้ใช้ด้วย `dotnet-install.ps1 -NoPath` ที่ `%LocalAppData%\Microsoft\dotnet` · build 0 warning · test 264 (ผ่าน 262 ข้าม 2 ที่ต้องใช้อินเทอร์เน็ต)
  - **ตรวจตามหัวข้อ 8.2 ด้วย dev build + ข้อมูลทดสอบ** (ผลอยู่ท้ายแต่ละข้อในหัวข้อ 8.2)
  - **icon ในกรอบ:** app ที่มีแต่ icon ความละเอียดต่ำ (51 จาก 253 ตัวบนเครื่องนี้ เช่น Java, PostgreSQL, Inno Setup) แสดงเป็น icon 48px กลางกรอบ 256px · build นี้วาดกรอบจาง (ขอบ 5px alpha 38, 38, 77, 51, 26) แต่ตัวตรวจต้องการ alpha > 200 · แก้ `IconExtractor.LooksFramed` ให้นับ alpha ใดก็ได้ในขอบ 4px ครบ 4 ด้านและข้างในว่าง · ไล่ทั้ง 253 icon แล้ว ตัวที่จับได้ 51 ตัวมีกรอบจริงทุกตัว และ 202 ตัวที่เหลือไม่มีตัวไหนมีกรอบ · ใส่เวอร์ชันใน key ของ icon cache สำหรับ icon จาก shell ให้ link เดิมได้ภาพใหม่ (หัวข้อ 12)
  - **ออก `v0.1.0-beta.2`** (ผู้ใช้ยืนยัน): pack ในเครื่องก่อน (release notes ถูก) · workflow 3 นาที ผ่านรอบแรก · หน้า Release เป็น pre-release มี delta package 556KB จาก beta.1
  - **อัปเดต beta.1 → beta.2 จาก GitHub:** ติดตั้ง `MinkQuickLax-beta-Setup.exe` ของ beta.1 จากหน้า Release ด้วยข้อมูลทดสอบ (ติดตั้ง 2 วินาที ไม่มี UAC ได้ช่องทาง `beta`) · เปิด app ใหม่ 36 วินาทีต่อมาดาวน์โหลด beta.2 และขึ้นกล่อง "Version 0.1.0-beta.2 is ready…" พร้อม Later/Restart now · กด Restart now แล้วเปิดกลับเป็น beta.2 ใน 3 วินาที · ค่าใน Run ยังเป็น path `current\` เดิม · ค่าตั้งและไอคอนอยู่ครบ
  - **ออก `v0.1.0`** (ผู้ใช้เลือกออกทันทีหลัง beta.2 ผ่าน): pack ช่องทาง `win` ในเครื่องก่อน · หน้า Release เป็น Latest มี `MinkQuickLax-win-Setup.exe`, `-full.nupkg`, `win-Portable.zip`, `releases.win.json`
  - **สลับจากทดลองเป็นปกติ:** ในตัวที่ติดตั้ง (beta.2) เปิดหน้าตั้งค่าด้วยการเปิด exe ซ้ำ → ทั่วไป → ช่องทางอัปเดต → Stable · เปิด app ใหม่แล้วได้ 0.1.0 จากช่องทาง `win` · `sq.version` เป็น 0.1.0 ช่องทาง `win`
  - **ถอน:** `Update.exe uninstall --silent` แล้ว app ปิด ค่าใน Run, shortcut ใน Start Menu, รายการใน Apps และโฟลเดอร์ติดตั้งหายหมด · `%AppData%\MinkQuickLax` ของผู้ใช้ไม่ถูกสร้าง
  - **ข้อจำกัดเดิมเจอจริง:** dev build ที่รันทดสอบสร้าง `%LocalAppData%\MinkQuickLax` (log/cache) ต้องย้ายโฟลเดอร์ออกก่อนติดตั้ง
- **ยังต้องทำ/ตรวจ:** รายการในหัวข้อ 8.2 ที่ยังไม่ติ๊ก · คนที่อยู่ช่องทางทดลองไม่ได้รุ่นปกติเองจนกว่าจะสลับในหน้าตั้งค่า (ตามหัวข้อ 12 และเขียนไว้ใน CHANGELOG ของ 0.1.0)
- **แก้หลังออก 0.1.0 (ยังไม่ออกรุ่น · ใส่ใน CHANGELOG ตอนออก 0.1.1):**
  - [x] **หน้าต่าง "เพิ่มเว็บ" แทนช่อง URL ในหน้าสแกน (2026-09-16):**
    - **ที่มา:** ผู้ใช้ติ๊ก Google Chrome ในรายการ วาง URL เลือก browser แล้วกด "เพิ่มลงจอ" โดยไม่กด Enter ได้แค่ link ของ app Chrome ส่วน URL หายเงียบ ๆ (ดูจาก `config.json` และ log ว่า "Added 1 links") · แก้ครั้งแรกให้ "เพิ่มลงจอ" เพิ่ม URL ที่ค้างด้วย (ยังไม่ commit) แต่ผู้ใช้บอกว่าวิธีเพิ่มยังเข้าใจยาก จึงเลือกทำหน้าต่างแยก (SPEC 4.1, 4.7, 4.8, หัวข้อ 8)
    - **ทำแล้ว:** `AddWeb/` (`AddWebService` เปิดทีละหน้าต่าง ถ้าหน้าต่างของ app ที่ active อยู่เป็นหน้าสแกนหรือหน้าตั้งค่าก็ใช้เป็น owner · `AddWebViewModel` ชื่อตามที่อยู่จนผู้ใช้พิมพ์เอง ดึง favicon ผ่าน `IconCache` หลังหยุดพิมพ์ 0.6 วินาที ใช้ key เดียวกับ link ที่จะเพิ่ม icon บนจอเลยขึ้นทันที) · `Core/Launch/WebAddress` เติม `https://` (หรือ `http://` สำหรับ localhost/IP) พร้อม test · หน้าสแกนเหลือปุ่ม ไฟล์…/โฟลเดอร์…/เว็บ… (เอา `Url`, `Browser`, `AddUrl` และ `ScanItem.Browser` ออก) · เมนู tray "เพิ่มเว็บ…" · ปุ่ม "เพิ่มเว็บ…" ในหน้าตั้งค่าหมวด Link · ข้อความ `AddWeb_*`, `Scanner_PickWeb`, `Tray_AddWeb` (ลบ `Scanner_UrlHint`, `Scanner_UrlInvalid`) · คู่มือบท 2
    - **ตรวจแล้ว:** dev build + ข้อมูลทดสอบ ผ่านโปรแกรมเล็กที่เปิดหน้าต่างจริงของ app โดยไม่ผ่าน single instance (app ที่ติดตั้งยังรันอยู่) และ UI Automation · ช่องว่างกดเพิ่มไม่ได้ · `hello` ขึ้นข้อความเตือน ไม่ปิด ไม่เพิ่ม · `github.com/` ขึ้นว่ามีอยู่แล้ว ชื่อ `github.com` · `youtube.com` ได้ชื่อและ favicon · พิมพ์ชื่อเอง "YouTube" แล้วเปลี่ยนที่อยู่ ชื่อไม่ถูกทับ · เลือก Google Chrome แล้วกด Enter หน้าต่างปิด ได้ link `https://www.youtube.com/` browser `Google Chrome` icon `favicon` · ปุ่ม "เว็บ…" ในหน้าสแกนเปิดหน้าต่างกลางหน้าสแกน กดยกเลิกแล้วหน้าสแกนยังอยู่ · ปุ่มในหน้าตั้งค่าเปิดได้ · ภาพ Glass ไทย, HUD อังกฤษ
    - **ยังต้องตรวจ:** ผู้ใช้ลองเพิ่มเว็บจริงจากเมนู tray · Dot Matrix และธีมสว่าง

---

## 7. เฟส 2 และ 3 (ลำดับคร่าว ๆ)

**เฟส 2**
1. คีย์ลัด + หน้าค้นหาด่วน + คีย์ซ่อน/แสดง + หน้าตั้งคีย์พร้อมเตือนคีย์ชน
2. ซ่อนตอนเปิด app เต็มจอ/present (`SHQueryUserNotificationState` + ตรวจหน้าต่างที่ active ว่าเต็มจอ)
3. กลุ่มแบบแถบ · เปิดกลุ่มด้วยการชี้
4. ลากไฟล์/URL มาวาง · ลาก link จากหน้าตั้งค่ามาวางบนจอ
5. เลือกหลายตัวแล้วลากพร้อมกัน
6. หน้าต้อนรับครั้งแรก
7. ปุ่ม ⓘ ในหน้าตั้งค่า · เนื้อหาคู่มือภาษาอังกฤษ
8. link ประเภท `command`, `msSettings` · icon แบบ SVG · แสงสะท้อนวิ่งตามเมาส์

**เฟส 3:** SPEC หัวข้อ 4.14

---

## 8. การทดสอบ

### 8.1 อัตโนมัติ
- **Core:** unit test ทุก service ในหัวข้อ 3.3 ที่อยู่ใน Core
- **Platform:** `LinkKindDetector` กับ path จริง, `StartupRegistration` กับ registry key ทดสอบ (`HKCU\Software\MinkQuickLax.Tests`)
- **ภาษา:** key ครบทั้งสองภาษา (`Core.Tests/RepositoryRules/StringResourcesTests` อ่านไฟล์ resx ตรง ไม่ต้องอ้าง WPF)
- **กฎของ repo:** Core ไม่อ้าง WPF/Win32 · manifest เป็น `asInvoker` และ PerMonitorV2 (`Core.Tests/RepositoryRules/`)
- **ManualBuilder:** id ไม่ซ้ำ, ครบสองภาษา, id จากหน้าตั้งค่ามีจริง

### 8.2 ทดสอบด้วยมือก่อนออกรุ่น (Windows Sandbox + เครื่องจริง)
ผลที่ต่อท้ายแต่ละข้อมาจากการตรวจวันที่ 2026-09-15 บนเครื่องที่สอง (ดู M11) · สคริปต์คลิกเฉพาะหน้าต่างของ app จึงบางข้อตรวจด้วยสถานะของหน้าต่างแทนการใช้ Notepad หรือ Task Manager จริง

- [ ] ติดตั้งบนเครื่องใหม่ ไม่มี UAC · ผ่านหน้า SmartScreen ตามคู่มือได้ (ไม่มี UAC ตรวจแล้วบนทั้งสองเครื่อง · SmartScreen ยังไม่ได้ตรวจ ต้องดาวน์โหลดด้วย browser ให้ไฟล์มีเครื่องหมายว่ามาจากเน็ต แล้วกดผ่านหน้าเตือนเอง)
- [x] เปิดครั้งแรกขึ้นหน้าสแกน · เพิ่ม app 5 ตัว (หน้าสแกนขึ้นเองพร้อม 253 app · ติ๊ก 5 ตัวแล้วไอคอนเรียงที่มุมขวาบนของจอหลัก)
- [x] กดไอคอนขณะพิมพ์ใน Notepad แล้วตัวอักษรยังพิมพ์ต่อได้ (หน้าต่างที่ active ไม่เปลี่ยนหลังคลิกขวา กางกลุ่ม และกดเปิด link · ตรวจด้วย `GetForegroundWindow`)
- [x] Alt+Tab และ taskbar ไม่มีไอคอนของเรา (หน้าต่างไอคอน/เมนู/แผงเป็น `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` มี owner ที่ซ่อนอยู่ ไม่มี `WS_EX_APPWINDOW` · ตรวจจาก style ไม่ได้เปิด Alt+Tab)
- [x] ลาก ชิดกริด ปล่อยทับ ย้อนกลับ ยกเลิก (ป้ายพิกัด `X 1800 Y 0696` ตรงกริด 24px · ปล่อยทับแล้วหลบขึ้น 1 ช่อง · Ctrl+Z กลับที่เดิม · Cancel แล้วกลุ่มที่สร้างหายและทุกตัวกลับที่เดิม)
- [x] สร้างกลุ่มด้วยการลากค้าง · กางกลุ่มใกล้ขอบจอทั้ง 4 ด้าน (ค้าง 1 วินาทีได้กลุ่ม · ชิดซ้าย/บน แผงอยู่ขวาของโฟลเดอร์ ชิดขวา แผงอยู่ซ้าย ชิดล่าง แผงอยู่เหนือ taskbar · ไม่ล้นไปจอข้าง ๆ)
- [ ] สลับธีม Windows สว่าง/มืด · ปิด Transparency effects · ปิด Animation effects (ปิด Transparency: เมนูเป็นพื้นทึบ · ปิด Animation: กดเปิดไม่มีภาพเคลื่อนไหว · สลับธีมยังไม่ได้ตรวจ ผู้ใช้จะทดสอบเอง)
- [ ] เปลี่ยน scale 100% → 150% · ถอดจอที่สองแล้วเสียบกลับ
- [ ] ลากไอคอนข้ามจอที่ scale ต่างกันแล้วขนาดไม่เพี้ยน (S2)
- [ ] id ของจอเดิมหลังรีบูต, ถอดแล้วเสียบ, สลับพอร์ต (S9) · ดูได้จาก log ของ app
- [ ] รีสตาร์ท Explorer (Task Manager) แล้ว icon ที่ tray กลับมา
- [ ] เปิด Transparency effects แล้วเมนู ชื่อไอคอน และกล่องแจ้งเตือนเบลอจริง · ปิดแล้วเป็นพื้นทึบ (ครึ่งหลังผ่าน · ครึ่งแรกผู้ใช้จะทดสอบเอง)
- [ ] "เปิดในฐานะผู้ดูแลระบบ" ขึ้น UAC แล้วเปิดได้ · กด No แล้วไม่มีข้อความ error
- [x] เปิด Task Manager แบบ Always on top แล้วคลิก app อื่น ไอคอนกลับขึ้นมาอยู่บน (ใช้หน้าต่าง TopMost ที่สร้างเองแทน Task Manager · ตอนหน้าต่างนั้น active มันบังไอคอน · พอสลับไปหน้าต่างอื่น ไอคอนกลับขึ้นอยู่เหนือทันที)
- [ ] รีบูตแล้ว app เปิดเองแบบเงียบ · ปิดใน Task Manager แล้วไม่เปิดเอง
- [x] Task Manager → ปิดโปรเซสระหว่างบันทึก แล้วเปิดใหม่ ค่าตั้งไม่เสีย (ฆ่าโปรเซส 12 รอบที่ 380–655ms หลังปล่อยไอคอน: 6 รอบก่อนบันทึก 6 รอบหลังบันทึก · ไฟล์อ่านได้ทุกรอบ ไม่เหลือ `config.json.tmp` เปิดใหม่ไม่ต้องกู้จากไฟล์สำรอง)
- [ ] สลับภาษาไทย/อังกฤษทุกหน้าจอ (ตรวจแล้วใน M9 ยกเว้นเมนูที่ tray)
- [ ] คู่มือเปิดได้ตอนไม่มีเน็ต และค้นหาได้ (ใน M10 ตรวจว่าไม่อ้างทรัพยากรภายนอกและ CSP เป็น `default-src 'none'` แต่ยังไม่ได้ปิดเน็ตจริง)
- [x] อัปเดตจากรุ่นก่อนหน้า · ถอนการติดตั้ง (beta.1 → beta.2 จาก GitHub · สลับช่องทางแล้ว beta.2 → 0.1.0 · ถอนแล้วไม่เหลืออะไร)

---

## 9. การออกรุ่น

- **เวอร์ชัน:** SemVer กำหนดใน `Directory.Build.props` · รุ่นทดลองเป็น `-beta.N`
- **ขั้นตอน**
  1. แก้เวอร์ชัน
  2. เขียน `CHANGELOG.md` (ไทย/อังกฤษ)
  3. commit
  4. `git tag vX.Y.Z[-beta.N]` แล้ว push
  5. รอ `release.yml`
  6. ตรวจหน้า Release
- **หน้า Release:** วิธีติดตั้ง, วิธีผ่าน SmartScreen, สิ่งที่เปลี่ยน
- **เซ็นโปรแกรม:** ยังไม่ทำ (SPEC หัวข้อ 8) · ถ้าเริ่มเซ็น ให้เพิ่มขั้นเซ็นใน `release.yml` ก่อน `vpk pack`

---

## 10. ความเสี่ยง

| ความเสี่ยง | ผลกระทบ | ทางออกถ้าเกิด |
|---|---|---|
| หน้าต่าง layered 30 บานกิน RAM/CPU เกินเป้า (S1, S3) | ต้องเปลี่ยนสถาปัตยกรรมไอคอน | รวมไอคอนที่อยู่ใกล้กันไว้ในหน้าต่างเดียว · ลดความถี่อัปเดต · ปิดการขยายตามเมาส์เป็นค่าเริ่มต้น |
| Acrylic บนหน้าต่าง noactivate/บนสุดทำงานไม่ถูก (S4) | เมนูและแผงไม่เบลอ | **เกิดจริงกับ Acrylic ของระบบ** จึงใช้ accent acrylic แทน · ถ้า Windows รุ่นหน้าเลิกรองรับ ใช้พื้นทึบ `a = 0.92` ทุกที่ (หน้าตายังเป็น Glass แต่ไม่เบลอ) |
| หา path จริงของ exe จาก `shell:AppsFolder` ไม่ได้ (S5) | "เปิดตำแหน่งที่เก็บ" และ run as admin ใช้ไม่ได้กับบาง app | ซ่อนเมนูเหล่านั้นสำหรับ `shellApp` ที่ไม่มี path |
| Velopack ทำตามข้อกำหนดบางข้อไม่ได้ (S6) | ตัวติดตั้งไม่ตรง SPEC | ปรับ SPEC 4.11 ตามที่ทำได้ · ทางสุดท้ายคือ Inno Setup + ทำระบบอัปเดตเอง |
| id ของจอไม่คงที่ (S9) | ไอคอนไปผิดจอหลังรีบูต | ใช้ชื่อรุ่นจอ + ความละเอียด + ตำแหน่งเทียบจอหลักเป็น id สำรอง |
| SmartScreen ทำให้คนไม่กล้าติดตั้ง | มีผู้ใช้น้อย | คู่มือและหน้า Release อธิบายชัด · พิจารณาซื้อใบรับรอง OV |

---

## 11. บันทึกผล spike (M1)

| # | วันที่ | ผล | ตัวเลขที่วัดได้ | ตัดสินใจ |
|---|---|---|---|---|
| S1 | 2026-09-15 | ผ่าน | private working set 38MB (ใช้ GPU วาด 80MB) · CPU ตอนนิ่ง 20 วินาที 0.000% · ทั้ง 30 บานเป็น TOOLWINDOW+NOACTIVATE ไม่มี APPWINDOW · กดไอคอนขณะพิมพ์ในหน้าต่างอื่นแล้วยังเป็น foreground ข้อความ `abcdef` ครบ deactivated=0 | วาดหน้าต่างไอคอนด้วย CPU รายหน้าต่าง (หัวข้อ 3.4) |
| S2 | 2026-09-15 | ผ่านบางส่วน | ลาก 131 ครั้ง ใช้เวลาต่อครั้งเฉลี่ย 0.88ms สูงสุด 6ms · ชิดกริด 24px ถูก · ขนาด 72×72 ไม่เปลี่ยน · **ข้ามจอที่ scale ต่างกันยังไม่ได้ทดสอบ** (เครื่องมีจอเดียว) | ใช้ drag loop แบบนี้ · ทดสอบข้ามจอด้วยมือในหัวข้อ 8.2 |
| S3 | 2026-09-15 | ผ่าน | ขยับเมาส์ผ่านไอคอน 20 วินาที (WM_INPUT 2,501 ครั้ง): CPU 1.59% ของทั้งเครื่อง (35% ของ 1 core, Core Ultra 7 165H) · handler ของ WM_INPUT เฉลี่ย 0.0008ms · หยุดแล้ว CPU กลับเป็น 0% · เทียบ: ใช้ animation ของ WPF 2.25%, ไม่มีเงา 1.67%, 30fps ไม่ช่วย | Raw Input ≤30 ครั้ง/วินาที + ไล่ค่าเองด้วย timer ที่ทำงานเฉพาะตอนมีไอคอนกำลังเปลี่ยน · ไม่ใช้ Storyboard กับการเข้าใกล้ |
| S4 | 2026-09-15 | ผ่านด้วยวิธีสำรอง | `DWMWA_SYSTEMBACKDROP_TYPE` บนหน้าต่างที่ไม่เคย active ได้พื้นทึบเสมอ (ลองส่ง WM_NCACTIVATE หลอกแล้วก็ไม่ช่วย) · `SetWindowCompositionAttribute` + `ACCENT_ENABLE_ACRYLICBLURBEHIND` เบลอจริง เห็นสีด้านหลังซึมผ่าน และเปลี่ยนเป็นพื้นทึบเองเมื่อปิด Transparency effects · มุมโค้งจาก `DWMWA_WINDOW_CORNER_PREFERENCE` ใช้ได้ · คลิกรายการในเมนูแล้ว focus ไม่หลุด · คลิกนอกเมนูแล้วปิด (ดักด้วย Raw Input) | ใช้ accent policy กับเมนู, ชื่อไอคอน, แผงกลุ่ม, แถบเครื่องมือ · เป็น API ที่ไม่มีเอกสาร จึงต้องมีพื้นทึบ `a = 0.92` เป็นทางสำรองเสมอ (หัวข้อ 12) |
| S5 | 2026-09-15 | ผ่าน | AppsFolder 212 รายการ เท่ากับ `Get-StartApps` ใช้เวลา 1.1 วินาที · Store 49, Win32 163 (ทุกตัวมี `System.Link.TargetParsingPath`, เป็น .exe 112) · icon 256px ตัวละ ~63ms พื้นโปร่งใสถูก · เปิดผ่าน `shell:AppsFolder\<parsing name>` ได้ทั้ง Win32 และ Calculator · app ที่มีแต่ icon เล็กจะได้ภาพเล็กในกรอบสี่เหลี่ยม | ดึง icon นอก UI thread แล้วเก็บลง cache · M5 ต้องตรวจจับ icon ที่มีกรอบแล้วขอขนาดเล็กลงแทน · Store app ไม่มี path จึงซ่อน "เปิดตำแหน่งที่เก็บ" และ run as admin |
| S6 | 2026-09-15 | ผ่าน (แหล่งอัปเดตในเครื่อง) | ติดตั้งแบบ `--silent` 3.8 วินาที ไม่ขอสิทธิ์ admin · มี shortcut แค่ Start Menu (`--shortcuts StartMenuRoot`) · ติดตั้งแบบปกติแล้วเปิด app ให้เอง · อัปเดต 1.0.1→1.0.3 (stable) และสลับไป 1.0.4-beta.1 (beta) แล้วรีสตาร์ทเอง · path ใน Run `…\current\<app>.exe --startup` ไม่เปลี่ยน · ถอนแล้ว hook ลบค่าใน Run, shortcut และรายการใน Apps หาย, โฟลเดอร์ถูกลบหลัง process จบ · **ยังไม่ได้ทดสอบกับ GitHub Release และ Windows Sandbox** (เครื่องไม่มี Sandbox) | ใช้ Velopack ต่อ · ใส่ `Environment.ProcessPath` ลง Run ได้เลย · ทดสอบ GitHub source ใน M11 |
| S7 | 2026-09-15 | ผ่าน | คลิกขวาที่ icon (ผ่านถาดไอคอนที่ซ่อน) แล้วเมนูของเราขึ้นชิดเคอร์เซอร์ · คลิกนอกเมนูแล้วปิด · ลบ icon ด้วย NIM_DELETE แล้วส่ง `TaskbarCreated` ให้หน้าต่างของเรา icon กลับมา · GUID ของ icon ที่ H.NotifyIcon สร้างเองต่างกันตาม path ของ exe จึงรันจากหลาย path ได้ · **ยังไม่ได้รีสตาร์ท Explorer จริง** (จะปิดหน้าต่าง File Explorer ที่ผู้ใช้เปิดอยู่) | ใช้ Id ค่าเริ่มต้นของ H.NotifyIcon · รีสตาร์ท Explorer จริงอยู่ในหัวข้อ 8.2 |
| S8 | 2026-09-15 | ผ่าน | WebView2 runtime 152 · พร้อมใน 486ms · เปิด `file:///…/manual th.html#section-30` แล้วหัวข้ออยู่บนสุดของหน้าต่าง · เปลี่ยน hash ด้วย script ก็เลื่อนไปถูก · ชี้ runtime ไปโฟลเดอร์ที่ไม่มีได้ `WebView2RuntimeNotFoundException` | ตรวจ runtime ด้วย `GetAvailableBrowserVersionString` · เก็บ user data ไว้ใน `%LocalAppData%\MinkQuickLax\WebView2` |
| S9 | 2026-09-15 | ผ่านบางส่วน | อ่าน `monitorDevicePath` (`\\?\DISPLAY#BOE0C6B#4&…&UID8388688#{…}`), EDID ผู้ผลิต/รุ่น และจับคู่กับ `HMONITOR` + พื้นที่ทำงาน + DPI ได้ · จอ laptop ไม่มี serial ใน EDID · **รีบูต ถอด/เสียบ และสลับพอร์ตยังไม่ได้ทดสอบ** (จอเดียวในตัวเครื่อง) | เก็บ `monitorDevicePath` เป็น id หลัก · ถ้าไม่เจอให้หาจอที่ EDID (ผู้ผลิต+รุ่น+serial) ตรงกัน · ถ้ายังไม่เจอใช้จอหลัก · เขียน id ของจอลง log ทุกครั้งที่จอเปลี่ยน เพื่อตรวจในการใช้งานจริง |

**สรุป M1:** ไม่มีข้อไหนที่ต้องเปลี่ยนสถาปัตยกรรมหลัก จึงเริ่ม M2 ได้ · สิ่งที่ยังต้องทดสอบด้วยมือ (ย้ายไปไว้ในหัวข้อ 8.2 แล้ว): ลากข้ามจอที่ scale ต่างกัน, รีสตาร์ท Explorer จริง, id ของจอหลังรีบูต/ถอดเสียบ/สลับพอร์ต, อัปเดตจาก GitHub Release · โค้ดทดลองอยู่ใน branch `spike/m1`

---

## 12. บันทึกการตัดสินใจทางเทคนิค

เรื่องที่ผู้ใช้ไม่เห็น Claude ตัดสินใจเองได้ แต่ต้องบันทึกไว้ที่นี่ · เรื่องที่ผู้ใช้เห็นให้ไปบันทึกใน SPEC หัวข้อ 8

| วันที่ | เรื่อง | ตัดสินใจ | เหตุผล |
|---|---|---|---|
| 2026-09-15 | เวอร์ชัน SDK | ล็อกใน `global.json` เป็น 10.0.401 + `rollForward: latestFeature` · CI อ่านจากไฟล์นี้ | เครื่องนักพัฒนาและ CI ใช้ SDK ตรงกัน |
| 2026-09-15 | test runner | `xunit.v3` 4.x บน Microsoft.Testing.Platform (ตั้งใน `global.json`) · ไม่ใช้ `Microsoft.NET.Test.Sdk` และ `xunit.runner.visualstudio` | `xunit.v3` 4.x ใช้ MTP v2 ซึ่ง .NET 10 ไม่ให้รันผ่าน VSTest แล้ว |
| 2026-09-15 | โปรเจกต์ test ที่ยังว่าง | สร้างโปรเจกต์ test พร้อม test แรกเท่านั้น (Platform.Tests, ManualBuilder.Tests ยังไม่สร้าง) | MTP ถือว่า 0 test เป็นความล้มเหลว (exit code 8) · ตั้งยกเว้นได้แค่ทั้งคำสั่ง ซึ่งจะซ่อนกรณี test หายจริง |
| 2026-09-15 | OS ขั้นต่ำใน csproj | ไม่ตั้ง `SupportedOSPlatformVersion` · TFM เป็น `net10.0-windows` เฉย ๆ | ตั้งเกิน 7.0 ต้องใช้ TFM `net10.0-windows10.0.x` ซึ่งดึง WinRT projection มาอีกประมาณ 25MB โดยไม่ได้ใช้ · API ที่ต้องใช้ Windows ใหม่ (เช่น Acrylic 22H2) ต้องเช็กด้วย `OperatingSystem.IsWindowsVersionAtLeast` ก่อนเรียก |
| 2026-09-15 | ชื่อโฟลเดอร์ใน Platform | `Windows/` → `Windowing/` · `System/` → `SystemIntegration/` | namespace `MinkQuickLax.Platform.Windows` บังชื่อ `Windows.Win32` ที่ CsWin32 สร้าง และ `...Platform.System` บังชื่อ `System` ทำให้โค้ดข้างในต้องเขียน `global::` ทุกที่ |
| 2026-09-15 | ชื่อ model ค่าตั้ง | `Settings` → `AppSettings` | namespace `MinkQuickLax.Settings` (โฟลเดอร์หน้าตั้งค่า) จะบังชื่อ class `Settings` ในโค้ดของ app |
| 2026-09-15 | ใครถือ tray | `Program.Main` สร้าง `TrayController` ด้วย `using` แล้วค่อย `app.Run()` | ให้ Program คุมลำดับตอนเปิด/ปิดตามหัวข้อ 3.1 · analyzer CA1001 ไม่ยอมให้ `App` ถือ object ที่ต้อง dispose |
| 2026-09-15 | เมนูที่ tray ใน M0 | ใช้ `ContextMenu` ของ WPF ชั่วคราว · เปลี่ยนเป็น `GlassMenuWindow` ใน M3 หลังผ่าน S7 | M0 ต้องการแค่เมนู "ออก" |
| 2026-09-15 | Efficiency mode ของ H.NotifyIcon | ปิด (`ForceCreate(enablesEfficiencyMode: false)`) | โหมดนี้ลดลำดับความสำคัญของ process ซึ่งจะทำให้ไอคอนตอบสนองเมาส์ช้า |
| 2026-09-15 | icon ของ app | `Assets/AppIcon.ico` ตอนนี้เป็นตัวชั่วคราว (ลูกแก้ว 3 สีวาดด้วยโค้ด ขนาด 16–256px) | ตัวจริงออกแบบใน M3 ตามรายการในหัวข้อ 6 |
| 2026-09-15 | เบลอบนเมนู/แผง/ชื่อไอคอน | accent acrylic (`SetWindowCompositionAttribute`) แทน `DWMWA_SYSTEMBACKDROP_TYPE` | Acrylic ของระบบเป็นพื้นทึบบนหน้าต่างที่ไม่เคย active (S4) · API นี้ไม่มีเอกสาร จึงห่อไว้ใน `DwmBackdrop` ที่เดียวและมีพื้นทึบเป็นทางสำรอง |
| 2026-09-15 | การวาดหน้าต่างไอคอน | layered + วาดด้วย CPU รายหน้าต่าง · การเข้าใกล้ไล่ค่าเองที่ 30Hz | RAM 38MB แทน 80MB · CPU ตอนขยับเมาส์ 1.59% แทน 2.25% (S1, S3) |
| 2026-09-15 | id ของจอ | `monitorDevicePath` → EDID (ผู้ผลิต+รุ่น+serial) → จอหลัก | ทดสอบรีบูต/ถอดเสียบไม่ได้บนเครื่องจอเดียว · path มีเลขพอร์ตอยู่ด้วย จึงต้องมี EDID เป็นตัวสำรองตอนสลับพอร์ต (S9) |
| 2026-09-15 | ผลลัพธ์ spike | ไม่ commit `spikes/results/` | มีภาพหน้าจอและรายชื่อโปรแกรมในเครื่องผู้ใช้ ซึ่งไม่ควรอยู่ใน repo สาธารณะ · ตัวเลขสรุปอยู่ในหัวข้อ 11 |
| 2026-09-15 | model เป็น record ที่มี setter | ใช้แบบ immutable ด้วย `with` แต่ property เป็น `set` | source generator ของ System.Text.Json เขียนทับค่าเริ่มต้นด้วย null/0 เมื่อ property แบบ `init` ไม่มีในไฟล์ (test จับได้) · `[JsonExtensionData]` ใช้กับ `init` ไม่ได้ |
| 2026-09-15 | เวลาและไฟล์ใน Core | ใช้ `TimeProvider` ของ .NET แทน `IClock` · ไม่ทำ `IFileSystem` แต่ test กับโฟลเดอร์ชั่วคราวจริง | `TimeProvider` + `FakeTimeProvider` ทดสอบการหน่วงบันทึกได้ตรง · การแทนที่ไฟล์แบบ atomic ควรทดสอบกับระบบไฟล์จริง |
| 2026-09-15 | เวลาที่สำรองค่าตั้ง | ตอนโหลดถ้าไฟล์ต่างจากชุดสำรองล่าสุด · ตอนบันทึกถ้าชุดล่าสุดเก่ากว่า 1 วัน · ก่อนกู้คืนและก่อนคืนค่าทั้งหมด · กด "สำรองตอนนี้" | สำรองทุกครั้งที่บันทึกจะทำให้ 10 ชุดหมดในการลากไม่กี่ครั้ง · ไฟล์ที่อ่านไม่ได้ย้ายไปเป็น `config.unreadable-*.json` ไม่ลบทิ้ง |
| 2026-09-15 | การชิดแนว | จับคู่กลาง↔กลาง, ขอบเดียวกัน, และขอบชนขอบ ไม่จับกลาง↔ขอบ · ระยะเท่ากันให้แบบที่มาก่อนในลำดับนี้ชนะ ไม่ว่าจะเป็นของไอคอนตัวไหน · ระยะชนกันวัดจากขนาดหน้าต่าง (ไอคอน + 12px รอบ) | กลาง↔ขอบทำให้ดูดแรงเกินไป · วัดจากหน้าต่างทำให้ไอคอนห่างกันอย่างน้อย 24px และไม่ทับกันตอนขยาย |
| 2026-09-15 | โปรเจกต์ Windows เป็น x64 | Platform, App, Platform.Tests ตั้ง `PlatformTarget=x64` + `RuntimeIdentifier=win-x64` | CsWin32 สร้าง API ที่มี struct ขนาดตาม CPU (`ShellExecuteEx`, `GetWindowLongPtr`) ให้ AnyCPU ไม่ได้ · app ออกเฉพาะ win-x64 อยู่แล้ว (arm64 เฟส 3) |
| 2026-09-15 | Platform ไม่ใช้ WPF | หน้าต่างรับข้อความของระบบเขียนเองด้วย Win32 (`MessageWindow`) · icon ส่งเป็น BGRA (`IconBitmap`) ให้ App แปลงเอง | Platform test ได้โดยไม่ต้องมี Dispatcher · message-only window รับ Raw Input ส่วนหน้าต่าง top-level ที่ซ่อนไว้รับข่าวจอ/ค่าตั้ง/พลังงาน |
| 2026-09-15 | หน้าต่างกระจกสร้างด้วยโค้ด | Tooltip, เมนู, กล่องแจ้งเตือนสร้าง visual tree ในโค้ด ใช้ style และ brush จาก `Glass.xaml` ผ่าน resource key | ต่อยอดจาก base class เดียว (`GlassSurfaceWindow`) ง่ายกว่า XAML ที่สืบทอดจาก base window · ค่าดีไซน์ยังอยู่ใน XAML/`GlassDesign.cs` |
| 2026-09-15 | animation ตอนเปิดหน้าต่างกระจก | จาง + ขยายเฉพาะเนื้อหาข้างใน · ชื่อไอคอนแบบ Glass ขึ้นทันทีไม่เลื่อน · ป้ายด้านข้างของ HUD/Dot Matrix จางและเลื่อน 6px (เผื่อที่ในหน้าต่างไว้ฝั่งไอคอน) | หน้าต่างที่ไม่ใช่ layered ใช้ `Window.Opacity` ไม่ได้ และถ้าเลื่อนเนื้อหาจะเห็นขอบเบลอว่าง · HUD/Dot Matrix ไม่มีเบลอจึงเลื่อนได้ตาม mockup |
| 2026-09-15 | ลำดับตอนกดไอคอน | สั่งเปิด link ก่อนแล้วค่อยเล่นเด้ง | เจอบั๊ก easing ที่ทำให้ animation โยน exception ก่อนเปิด app · งานหลักต้องไม่ขึ้นกับ animation |
| 2026-09-15 | โฟลเดอร์ข้อมูลทดสอบ | ตัวแปร `MINKQUICKLAX_DATA_DIR` ชี้โฟลเดอร์ค่าตั้งไปที่อื่นได้ | ทดสอบ app จริงโดยไม่แตะ `%AppData%` ของผู้ใช้ |
| 2026-09-15 | การลาก | ใช้ mouse capture ของ WPF + `GetCursorPos` + `SetWindowPos` (ตาม S2) · เริ่มลากเมื่อขยับเกิน 4px · ตัดสินคลิก/ลาก/เลือกที่ `ArrangeController` ที่เดียว | หน้าต่างไอคอนเป็นแค่ตัวส่งเหตุการณ์ ทำให้โหมดใช้งานกับโหมดแก้ไขใช้ logic เดียวกัน |
| 2026-09-15 | เส้นช่วยจัดแนว | หน้าต่าง layered บาง 1px แนวละบาน ยาวตลอดพื้นที่ทำงานเหมือน mockup (เดิมยาวเท่าช่วงของสองไอคอน) | หน้าต่างโปร่งใสเต็มจอต้องวาดใหม่ทั้งจอทุกครั้งที่เมาส์ขยับ · เส้นยาวบาง 1–3px ยังวาดแค่ไม่กี่พันพิกเซล |
| 2026-09-15 | ย้อนกลับ | เก็บ `AppConfig` ทั้งก้อนเป็น snapshot (`UndoHistory<T>`, สูงสุด 200 ขั้น) | model เป็นค่าคงที่อยู่แล้ว snapshot จึงถูกและถูกต้องกว่าการเก็บคำสั่งย้อน |
| 2026-09-15 | log แบบละเอียด | ตัวแปร `MINKQUICKLAX_DEBUG=1` เปิดระดับ Debug | ใช้หาปัญหาในเครื่องผู้ใช้โดยไม่ต้อง build ใหม่ |
| 2026-09-15 | ทดสอบ UI บนเครื่องผู้ใช้ | ขยับเมาส์/คลิกเฉพาะบนหน้าต่างของ app · ไม่ส่งปุ่มลัดไปหน้าต่างอื่น | สคริปต์ที่ส่ง Ctrl+W/Esc และคลิกลงหน้าต่างอื่นรบกวนงานของผู้ใช้ (ผู้ใช้กดยกเลิก) |
| 2026-09-15 | หน้าต่างโฟลเดอร์ | ใช้ `IconWindow` แบบโฟลเดอร์ (`SetFolder`) ไม่แยก `FolderWindow` | ขนาด เงา การเข้าใกล้ การลาก การสั่น และการเลือกเหมือนไอคอนเดี่ยวทุกอย่าง ต่างกันแค่สิ่งที่วาดข้างใน |
| 2026-09-15 | Esc ปิดแผงและเมนู | ลงทะเบียน Esc เป็น hotkey (`RegisterHotKey`) เฉพาะตอนมีแผง เมนู หรือกล่องแจ้งเตือนเปิดอยู่ แล้วคืนทันทีเมื่อปิด (`EscapeKeyWatcher`) | หน้าต่างของ app ไม่รับ focus จึงไม่ได้รับปุ่มเอง · ไม่ใช้ keyboard hook หรือ Raw Input ของคีย์บอร์ดที่เห็นทุกปุ่มที่ผู้ใช้พิมพ์ · Esc ที่กดตอนแผงเปิดจึงไม่หลุดไปถึง app ที่ผู้ใช้กำลังใช้ |
| 2026-09-15 | คลิกโฟลเดอร์ในโหมดแก้ไข | ยังกางแผง | ต้องกางแผงเพื่อจัดลำดับหรือลาก link ออกจากกลุ่ม |
| 2026-09-15 | สร้างกลุ่มใหม่จาก "เพิ่มเข้ากลุ่ม…" | วางโฟลเดอร์ในช่องว่างข้างไอคอนนั้น และไอคอนเดี่ยวยังอยู่ | เมนูนี้เป็นการเพิ่ม link เข้ากลุ่ม ไม่ใช่ย้าย (ต่างจากการลากไปวางบนกลุ่มที่ SPEC ให้ไอคอนเดี่ยวหาย) |
| 2026-09-15 | ไม่ใช้ WPF-UI | หน้าตั้งค่าและหน้าสแกนใช้ control ของ app เอง (`Styles/Controls.xaml`, `SettingRow`, `StyledWindow`) | ผู้ใช้ให้หน้าตั้งค่าเป็นแผ่นตามสไตล์ (SPEC 4.8) · ปรับ template ของ WPF-UI ให้เป็น 3 สไตล์ยากกว่าเขียนเอง · ไม่ต้องโหลด dictionary ใหญ่ของ WPF-UI |
| 2026-09-15 | ระบบสไตล์ | `ThemeService` เปลี่ยน brush, ความทึบ, มุมโค้ง และฟอนต์ตามสไตล์และธีม · `SurfaceFrame` วาดรูปทรงแผ่น (Glass มุมโค้ง+ไฮไลต์, HUD ตัดมุม+เส้นมุม+เส้นสแกน, Dot Matrix มุม 18px) · attached property `Skin.Kind` ที่สืบทอดลงไปให้ template สลับรายละเอียดตามสไตล์ · resource key ใช้ชื่อกลาง `Skin.*` แทน `Glass.*` | ทุกแผ่นเปลี่ยนสไตล์ได้ทันทีโดยไม่ต้องสร้างหน้าต่างใหม่ · ค่าที่ต่างกันอยู่ที่เดียว |
| 2026-09-15 | หน้าต่างของ HUD และ Dot Matrix | ยังเป็นหน้าต่างไม่ layered แบบเดิม แต่ปิด blur, ปิดมุมโค้งและเส้นขอบของระบบ (`DWMWCP_DONOTROUND` + `DWMWA_BORDER_COLOR = none`) แล้ววาดรูปทรงเองบนพื้นโปร่งใส | ตรวจแล้วส่วนนอกรูปทรงโปร่งใสจริง · ไม่ต้องเปลี่ยนเป็น layered ซึ่งต้องสร้างหน้าต่างใหม่ทุกครั้งที่สลับสไตล์ |
| 2026-09-15 | เก็บ browser ของ link | เก็บชื่อ key ใต้ `StartMenuInternet` แล้วค้นหา exe ตอนเปิด · ส่ง URL เป็น argument เดียวในเครื่องหมายคำพูดเฉพาะ http/https · ซ่อน Internet Explorer | path ของ browser เปลี่ยนได้เมื่ออัปเดต แต่ชื่อ key คงที่ · กัน link แอบใส่ switch ของ browser · IE ถูกปลดและเปิดต่อไปที่ Edge |
| 2026-09-15 | ลงทะเบียนเปิดพร้อม Windows ครั้งแรก | ทำเฉพาะตัวที่ติดตั้งแล้ว (`…\current\` ข้าง `Update.exe`) และเฉพาะตอนยังไม่เคยมีค่า | ตัว build ทดสอบจะได้ไม่ใส่ตัวเองลง Run ของเครื่องนักพัฒนา · ไม่เปิดกลับถ้าผู้ใช้ปิดใน Task Manager |
| 2026-09-15 | ลบข้อมูลทั้งหมด | ลบโฟลเดอร์ค่าตั้ง (รวมไฟล์สำรอง) และ icon cache แล้วหยุดการบันทึกก่อนออก · log ยังอยู่ | log ถูกเปิดค้างอยู่ระหว่างที่ app ทำงาน และหมุนทิ้งเองใน 7 วัน |
| 2026-09-15 | เปิด app ซ้ำ | ตัวแรกฟังคำสั่งตั้งแต่ต้นลำดับการเปิด · ตัวที่เปิดซ้ำรอได้ 10 วินาทีและเรียก `AllowSetForegroundWindow` ก่อนส่ง | หน้าตั้งค่าต้องขึ้นมาหน้าสุด (SPEC 7) และไม่หายเมื่อเปิดซ้ำตอนตัวแรกยังเปิดไม่เสร็จ |
| 2026-09-15 | ข้อมูลแจ้งปัญหา | ชื่อ field ในข้อความที่คัดลอกเป็นภาษาอังกฤษเสมอ | เป็นข้อมูลเทคนิคสำหรับผู้ดูแล repo ไม่ใช่ข้อความ UI |
| 2026-09-15 | ทดสอบ UI บนเครื่องผู้ใช้ (ต่อ) | สคริปต์ตรวจก่อนทุกคลิกว่าจุดนั้นเป็นหน้าต่างของ app (`WindowFromPoint`) · หน้าต่างปกติที่อาจอยู่หลังหน้าต่างอื่นใช้ UI Automation สั่งและ `PrintWindow` จับภาพแทนการคลิก | เคยคลิกโดนหน้าต่าง VS Code ตอนไอคอนไม่ขึ้น |
| 2026-09-15 | "คืนตำแหน่งทั้งหมด" (SPEC 4.8) | คืนค่าตั้งเรื่องตำแหน่งเป็นค่าเริ่มต้นแล้วจัดเรียงทุกชิ้นใหม่ (ถามยืนยันก่อน) · "จัดเรียงใหม่" จัดเรียงอย่างเดียว | SPEC ไม่ได้ระบุว่าต่างจากจัดเรียงใหม่อย่างไร |
| 2026-09-15 | ภาพโฮโลแกรม (HUD) และขาวดำ (Dot Matrix) | สร้างจากภาพ icon ครั้งเดียวเป็น bitmap 128px เก็บในหน่วยความจำคู่กับภาพต้นฉบับ (`ConditionalWeakTable`) แล้ววางซ้อนภาพจริงโดยไล่ความทึบตาม t · สีคำนวณด้วย matrix ของ CSS filter ชุดเดียวกับ mockup (`Core/Imaging/CssFilters`) · ไม่เขียนลง icon cache บนดิสก์ · ไม่ใช้ `ShaderEffect` | สร้างเร็วพอ (30 ภาพ) และหายไปเองเมื่อภาพถูกทิ้ง · ไฟล์ใน cache จะต้องตามลบเมื่อสูตรสีเปลี่ยน · หน้าต่างไอคอนวาดด้วย CPU ซึ่ง `ShaderEffect` ไม่ทำงาน · สูตรสีอยู่ใน Core จึง test เทียบกับค่าที่ Edge เรนเดอร์ได้ |
| 2026-09-15 | หน้ากากเม็ดจุด (Dot Matrix) | `DrawingBrush` แบบ tile 4px เป็น `OpacityMask` ของภาพ แล้วเปลี่ยนรัศมีของ `EllipseGeometry` ในนั้นตาม t | เปลี่ยนรัศมีได้โดยไม่ต้องสร้างภาพใหม่ทุกเฟรม |
| 2026-09-15 | ความใกล้ t | `ProximityAnimator` ไล่ค่า t ไปพร้อมความทึบและขนาด แล้วส่งให้ `IconWindow.SetProximity` · วัดระยะจากขอบไอคอน · โหมดแก้ไขใช้ t = 1 · โหมดแก้ไขและตอนลากไม่ขยายตามเมาส์ | ใช้ timer 33ms ตัวเดิม ไม่เพิ่ม animation ต่อไอคอน · ขยายซ้อนกับตอนลากแล้ววงเล็บ/วงจุดล้นหน้าต่างไอคอน และ mockup ก็ไม่ขยาย |
| 2026-09-15 | มุมแคปซูล | `RoundedBorder` จำกัดมุมโค้งไม่เกินครึ่งความกว้างหรือสูงแบบ CSS · ใช้กับปุ่ม ปุ่มแบ่งช่อง ป้ายพิกัด และชื่อใต้ไอคอน | `Border` ของ WPF ปรับรัศมีแนวนอนกับแนวตั้งแยกกัน มุม 99 บนแผ่นกว้างจึงกลายเป็นวงรี |
| 2026-09-15 | พื้นที่รับคลิกของไอคอน | ทั้งช่องไอคอนมีพื้นดำทึบ 1/255 ที่อยู่นอกชั้นที่จางตามความใกล้ | หน้าต่าง layered ปล่อยคลิกทะลุพิกเซลที่โปร่งใส 100% · ถ้าพื้นอยู่ในชั้นที่จาง 0.7 × 0.4 ค่าจะปัดเป็น 0 |
| 2026-09-15 | บรรทัดข้อมูลของป้ายด้านข้าง | ชนิดแบบย่อจาก resource (`Meta_App` = APP / แอป) · ชื่อไฟล์ ชื่อโฟลเดอร์ โดเมน หรือหน้า ms-settings จาก `LinkSummary` ใน Core · ต่อท้าย "ไม่พบปลายทาง" เมื่อเป้าหมายหาย · กลุ่มแสดงจำนวน link · ลำดับคือตำแหน่งใน `placements` | SPEC 5.8 ให้ตัวอย่าง `APP · CHROME.EXE` แต่ไม่ได้กำหนดชนิดอื่น |
| 2026-09-15 | animation ที่วนไม่หยุด | วงจุดหมุน 20fps · กะพริบ (วงเล็บ HUD, จุดในแถบเครื่องมือ) 10fps · การสั่นของ Glass ยังเป็น 60fps | ไอคอนละหน้าต่างวาดด้วย CPU · ลด fps การสั่นแล้ววัด CPU ไม่ลด จึงไม่เปลี่ยน |
| 2026-09-15 | ภาษาไทยในฟอนต์ mono | ฟอนต์ผสม `Assets/Fonts/MinkMono.CompositeFont`: ช่วง U+0E00–0E7F ใช้ IBM Plex Sans Thai ขยาย 115% ที่เหลือใช้ JetBrains Mono · baseline 1.15 และระยะบรรทัด 1.5 เผื่อสระบน/ล่าง · `ThemeService` แทน `Font.Mono` ด้วย `CompositeFonts.Mono` ตอนเริ่ม · Doto ไม่ทำเพราะใช้กับตัวเลข | ไม่ต้องไล่ปรับขนาดตัวอักษรทีละจุด · ต้องสร้าง `FontFamily` ด้วย base URI (`new FontFamily(folderUri, "./#Mink Mono")`) ถ้าใช้ string pack URI ตรง ๆ หรือ `FamilyMaps` ในโค้ด ฟอนต์ปลายทางจะหาไม่เจอ (วัดความกว้างข้อความไทยใน app แล้ว: 49.1 → 61.0) |
| 2026-09-15 | ตรวจ key ของข้อความ | test อ่านไฟล์ .cs และ .xaml ของ app หา `{s:Text Key}` และ string ที่มีรูปแบบ `Prefix_Name` แล้วเทียบกับ `Strings.resx` · key ที่ประกอบจากชื่อ enum ตรวจแยก | test อยู่ใน Core.Tests ซึ่งอ้าง WPF ไม่ได้ · key พิมพ์ผิดจะแสดงชื่อ key บนจอแทนข้อความ |
| 2026-09-15 | ไฟล์คู่มือ | `Manual\th.html` และ `Manual\en.html` ข้าง exe · id หัวข้อเขียนเองทุกหัวข้อด้วย `{#id}` (ไม่สร้างจากชื่อ) · 1 ไฟล์ Markdown = 1 บท เรียงตามชื่อไฟล์ · ภาษาอังกฤษจับคู่บทด้วยชื่อไฟล์ ถ้าไม่มีใช้บทภาษาไทยพร้อมป้าย | id ต้องเหมือนกันทุกภาษาเพื่อสลับภาษาแล้วอยู่หัวข้อเดิม (SPEC 4.9) · ถ้าสร้างจากชื่อ id จะเปลี่ยนเมื่อแก้ชื่อหัวข้อ |
| 2026-09-15 | ข้อความของหน้าคู่มือ | ช่องค้นหา ปุ่มพิมพ์ ปุ่มสลับภาษา และป้ายยังไม่แปล อยู่ใน `manual/template/strings.json` ทั้งสองภาษา ไม่ใช่ `Strings.resx` | หน้าคู่มือเป็น HTML ที่สร้างตอน build โดย ManualBuilder ซึ่งไม่อ่าน resx ของ app |
| 2026-09-15 | ผูกคู่มือกับ build | `MinkQuickLax.csproj` อ้าง ManualBuilder แบบ `ReferenceOutputAssembly="false"` แล้ว target `BuildManual` (incremental ตาม `manual/**` และ `ManualTopics.cs`) รัน builder ลง `obj\...\manual-html\` แล้วเพิ่มเป็น `Content` ที่ `Manual\` · ข้ามในโปรเจกต์ `*_wpftmp` | build ใน CI ตรวจคู่มือไปด้วย · ไม่ตั้ง `SetTargetFramework` เพราะจะได้ builder อีกชุดที่ build ขนานกับของ solution · โฟลเดอร์ชื่อ `manual` ชนกับที่ WPF compile `Manual\*.xaml` |
| 2026-09-15 | id ที่ app เปิดตรง | `Manual/ManualTopics.cs` เก็บเป็น `const string` · ManualBuilder อ่านค่าเหล่านี้ด้วย regex แล้ว error ถ้าไม่มีในบทภาษาไทย · F1 ในหน้าตั้งค่าใช้ `ManualTopics.ForPage` | ตรวจได้ตอน build โดยไม่ต้องให้ builder อ้าง assembly ของ app |
| 2026-09-15 | หน้าต่างคู่มือ | WebView2 ปิด DevTools, เมนูคลิกขวา, host object และ web message · เปิดได้เฉพาะไฟล์ในโฟลเดอร์ `Manual` ลิงก์ http/https เปิดใน browser หลัก · ส่ง `?theme=dark|light&embedded=1#id` · เปลี่ยนธีมระหว่างเปิดอยู่ใช้ script ตั้ง `data-theme` · สร้าง WebView2 ไม่ได้ให้ปิดหน้าต่างแล้วเปิดไฟล์ใน browser แทน | คู่มือเป็นไฟล์ในเครื่อง ไม่มีเหตุให้ไปหน้าเว็บอื่นในหน้าต่างของ app |
| 2026-09-15 | ค้นหาในคู่มือ | substring บนข้อความที่ NFKC + ตัวพิมพ์เล็ก · คะแนน: ชื่อหัวข้อ > คำค้นเพิ่ม > เนื้อหา · `Intl.Segmenter` ใช้หาจุดเริ่มคำเพื่อเพิ่มคะแนนและตัดข้อความตัวอย่าง · หลายคำต้องเจอครบทุกคำ · `?q=` เปิดหน้าพร้อมผลค้นหา | ภาษาไทยไม่มีช่องว่างระหว่างคำ (SPEC 4.9) · `?q=` ใช้ทดสอบการค้นด้วย Edge headless ได้โดยไม่ต้องพิมพ์ |
| 2026-09-15 | ช่องทางอัปเดต | ปกติ = ช่องทาง `win` ซึ่งเป็นค่าเริ่มต้นของ Velopack (ตัวติดตั้งชื่อ `MinkQuickLax-win-Setup.exe` ตาม SPEC 4.11) · ทดลอง = ช่องทาง `beta` ออกเป็น pre-release ของ GitHub · tag ที่มี `-` ในเวอร์ชันเป็นรุ่นทดลอง · ไม่อนุญาตให้ถอยเวอร์ชันเมื่อสลับจากทดลองไปปกติ | ชื่อตัวติดตั้งตรง SPEC โดยไม่ต้องตั้งชื่อเอง · ผู้ใช้ช่องทางปกติไม่ได้รุ่นทดลอง · อยู่รุ่นทดลองต่อจนรุ่นปกติตามทัน |
| 2026-09-15 | จังหวะตรวจอัปเดต | ตรวจครั้งแรกหลังเปิด app 30 วินาที แล้วทุก 24 ชั่วโมง · ดาวน์โหลดเงียบ ๆ แล้วแจ้งด้วยกล่องแจ้งเตือนของ app ที่มุมขวาล่างของจอหลัก (ข้าง tray) พร้อม "รีสตาร์ทตอนนี้" · เมนูที่ tray เปลี่ยนเป็น "รีสตาร์ทเพื่อติดตั้ง {เวอร์ชัน}" · ปิด app แล้ว `WaitExitThenApplyUpdates` ติดตั้งแบบเงียบโดยไม่เปิด app ใหม่ · ถ้าปิดไม่ปกติ (ออกจากระบบ) Velopack ติดตั้งให้ตอนเปิดครั้งถัดไป | ไม่ให้การตรวจถ่วงการเปิด app (SPEC 6: ไอคอนขึ้นใน 2 วินาที) · balloon ของ Windows ใส่ปุ่มไม่ได้ |
| 2026-09-15 | สร้างตัวติดตั้ง | `vpk` 1.2.0 เป็น local tool ใน `dotnet-tools.json` (ตรงกับ Velopack NuGet) · publish แบบ self-contained + ReadyToRun · `pack.ps1 -Upload` ดาวน์โหลดรุ่นก่อนของช่องทางเดียวกันเพื่อทำ delta แล้ว `vpk upload github --publish --merge` · release notes สร้างจากหัวข้อของเวอร์ชันใน `CHANGELOG.md` ถ้าไม่มีหัวข้อนั้น pack ล้ม | CI กับเครื่องนักพัฒนาใช้ vpk รุ่นเดียวกัน · ลืมเขียน CHANGELOG แล้วออกรุ่นไม่ได้ |
| 2026-09-15 | ช่องทางของคนที่ติดตั้งรุ่นทดลอง | ตอนเปิดครั้งแรกของตัวที่ติดตั้ง ตั้งช่องทางอัปเดตตามช่องทางของตัวติดตั้ง (`VelopackLocator.Current.Channel`) · หลังจากนั้นค่าในหน้าตั้งค่ามีผลเหนือกว่า | ค่าเริ่มต้นของค่าตั้งเป็น "ปกติ" ซึ่งทำให้คนที่ลง beta ไม่ได้ beta ตัวถัดไป · ไม่ต้องให้ผู้ใช้ไปเปลี่ยนเอง |
| 2026-09-15 | ตรวจ icon ที่อยู่ในกรอบ | นับว่ามีกรอบเมื่อขอบ 4px ด้านนอกมี alpha ≥ 16 อย่างน้อย 90% ของจุดที่สุ่มครบ 4 ด้าน และแถวที่ลึก 1/8 ของภาพว่าง (alpha < 40) · ไม่ดูว่าขอบทึบแค่ไหน | build 26200 วาดกรอบจาง alpha 26–77 ส่วนรุ่นก่อนทึบ · icon จริงที่เต็มภาพ (เช่นสี่เหลี่ยมทึบ) ข้างในไม่ว่างจึงไม่ถูกจับ · ถ้าจับผิดก็แค่ได้ภาพ 48px ที่ยังถูกต้อง |
| 2026-09-15 | เวอร์ชันของ icon cache | key ของ icon จาก shell ขึ้นต้นด้วยเลขเวอร์ชัน (`2\|`) เพิ่มเลขเมื่อวิธีดึง icon เปลี่ยน · favicon ไม่ใส่ · ไม่ลบไฟล์เก่า | link ที่เพิ่มไว้ก่อนแก้ได้ภาพใหม่เองโดยไม่ต้องให้ผู้ใช้ทำอะไร · ไม่ต้องดาวน์โหลด favicon ซ้ำ · ไฟล์เก่าเล็ก (ไม่กี่ KB) และโฟลเดอร์ cache ลบได้อยู่แล้ว |
| 2026-09-15 | ส่งปุ่มลัดตอนทดสอบ UI | ส่ง Ctrl+Z ด้วย `SendInput` ครั้งเดียวทั้งชุด หลังตรวจว่าหน้าต่างที่ active เป็นของ app · กดปุ่มในหน้าต่างของ app แทนปุ่มลัดเมื่อมี · link ทดสอบที่กดเปิดจริงชี้ `rundll32.exe` ที่ไม่เปิดหน้าต่าง | ปุ่มลัดไปถึงหน้าต่างที่ active ไม่ใช่หน้าต่างที่เล็งไว้ ถ้าหลุดไป VS Code จะย้อนงานของผู้ใช้ · รายการในหน้าสแกนมี shortcut ที่ทำงานจริง (เช่นสร้าง network adapter) จึงห้ามกดเปิด link ที่ได้จากการสแกน |
| 2026-09-15 | tag ของรุ่นที่ออกไม่สำเร็จ | ถ้า workflow ล้มก่อนสร้างหน้า Release ให้ลบ tag แล้ว tag ใหม่ที่ commit ที่แก้ ใช้เลขเวอร์ชันเดิม · ถ้าหน้า Release ถูกสร้างแล้วให้ออกเลขใหม่ | ยังไม่มีใครดาวน์โหลดได้ จึงไม่มีเวอร์ชันซ้ำสองแบบ · Velopack เทียบเวอร์ชันจาก releases.json จึงห้ามแทนไฟล์ของเลขที่ออกไปแล้ว |
| 2026-09-16 | วาดแสงบนกระจก Glass (SPEC 5.2) | `GlassLight.Draw` ที่เดียว ใช้ทั้ง `SurfaceFrame` และ `GlassLightLayer` (แผ่นโฟลเดอร์ ซึ่งย้าย padding ไปเป็น margin ของตารางไอคอนย่อ ให้แสงเต็มแผ่นแต่อยู่ใต้ไอคอนย่อ) · สีมาจาก `Skin.RimColor`/`Skin.SheenColor`/`Skin.HairlineColor` ในไฟล์ธีม · HUD/Dot Matrix ตั้ง `Skin.Rim` เป็นสีขอบเดิมและที่เหลือใส · `DwmBackdrop.SetRoundedCorners` ปิดเส้นขอบของระบบทุกสไตล์ · ปุ่ม Accent ย้าย padding เข้าไปใน template เพื่อให้แสงเต็มปุ่ม | สไตล์อื่นไม่เปลี่ยน: เทียบภาพ render ก่อน/หลังของ HUD และ Dot Matrix ต่างกันแค่ไอคอนตัวอักษร · ตรวจหน้าตาด้วยเครื่องมือชั่วคราวใน scratchpad ที่ render หน้าต่างจริงของ app (ไม่ commit) |
| 2026-09-16 | กระจกฝ้าเมื่อ Windows ไม่เบลอ (SPEC 5.5) | Platform `DesktopBackgrounds` อ่านภาพรายจอผ่าน `IDesktopWallpaper` (CsWin32) · Core `WallpaperLayout` คำนวณตำแหน่งภาพตามแบบของ Windows (มี test) · App `GlassFrost` ทำภาพเบลอกว้าง 192px ต่อจอด้วย `RenderTargetBitmap` + `BlurEffect` แล้วส่ง `ImageBrush` ที่ตัดเฉพาะส่วนหลังหน้าต่างให้ `SurfaceFrame.Backdrop` · `ThemeService` ถือ `GlassFrost` และมี `FrostChanged` เมื่อภาพหรือจอเปลี่ยนแต่ look เท่าเดิม · `SurfaceWindow`/`StyledWindow` คำนวณใหม่ตอนย้าย ขยาย เปลี่ยน look | ภาพเล็กต่อจอทำครั้งเดียว วาดซ้ำแค่ยืดภาพ จึงไม่กินเครื่องตอนลากหน้าต่าง · ตั้ง `EnableTransparency` ใน registry ตรง ๆ แล้ว DWM ยังไม่รู้ (acrylic ออกมาดำ) จึงไม่ใช้วิธีนี้แทนผู้ใช้ ต้องสลับจาก Settings ของ Windows · ทดสอบกับภาพพื้นหลังตัวอย่างในเครื่องมือชั่วคราว ไม่เปลี่ยนภาพพื้นหลังจริงของผู้ใช้ |
