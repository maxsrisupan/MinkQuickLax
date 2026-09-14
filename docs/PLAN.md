# MinkQuickLax — แผนลงมือ (Plan)

> **ความคืบหน้า:** M0, M1 เสร็จ · งานถัดไปคือ **M2 Core** (หัวข้อ 6)
> อัปเดตล่าสุด: 2026-09-15
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
| หน้าตั้งค่า | WPF-UI (`WPF-UI`) | หน้าตา Windows 11 พร้อม Mica/Acrylic และ NavigationView |
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
│  │  ├─ Launch/                  LinkKindDetector, LaunchRequest
│  │  └─ Abstractions/            IMonitorProvider, IRegistry, IClock, IFileSystem
│  ├─ MinkQuickLax.Platform/      net10.0-windows · Win32 ทั้งหมดอยู่ที่นี่
│  │  ├─ NativeMethods.txt        รายชื่อ API ให้ CsWin32 สร้าง
│  │  ├─ Windowing/               WindowStyles, TopmostKeeper, DwmBackdrop
│  │  ├─ Input/                   MouseProximityTracker (Raw Input), HotkeyService (F2)
│  │  ├─ Displays/                MonitorProvider, DisplayChangeWatcher
│  │  ├─ Shell/                   AppScanner, IconExtractor, Launcher, FaviconFetcher
│  │  ├─ SystemIntegration/       StartupRegistration, SingleInstance, SystemSettingsWatcher, FullscreenDetector (F2)
│  │  └─ Update/                  UpdateService (ห่อ Velopack)
│  └─ MinkQuickLax/               net10.0-windows · WPF · ได้ไฟล์ MinkQuickLax.exe
│     ├─ Program.cs               Main เอง: Velopack → single instance → host
│     ├─ App.xaml(.cs)
│     ├─ Styles/Glass/            ResourceDictionary ของ Glass (สี, brush, template)
│     ├─ Surfaces/                IconWindow, FolderWindow, GroupPanelWindow, TooltipWindow, GlassMenuWindow, EditToolbarWindow, DragReadoutWindow
│     ├─ Settings/                SettingsWindow + Pages + ViewModels
│     ├─ Scanner/                 ScannerWindow + ViewModel
│     ├─ Manual/                  ManualWindow (WebView2)
│     ├─ Tray/                    TrayController
│     ├─ Services/                PlacementController, EditModeController, UndoStack, IconCache, Localizer
│     ├─ Resources/Strings.resx   อังกฤษ (ค่าหลัก)
│     ├─ Resources/Strings.th.resx
│     ├─ Assets/Fonts/            IBM Plex Sans Thai, Chakra Petch, JetBrains Mono + ไฟล์ OFL
│     └─ Assets/AppIcon.ico
├─ manual/
│  ├─ th/*.md · en/*.md           เนื้อหาคู่มือ 1 ไฟล์ต่อ 1 บท
│  ├─ keywords.json               คำค้นเพิ่มของแต่ละหัวข้อ
│  ├─ template/                   index.html, manual.css, search.js
│  └─ assets/                     รูปภาพ
├─ tools/
│  └─ ManualBuilder/              แปลง manual/ → HTML ภาษาละไฟล์ + ตรวจ id
├─ tests/                         สร้างโปรเจกต์ test พร้อม test แรกเท่านั้น (หัวข้อ 12)
│  ├─ Directory.Build.props       ค่าร่วมของโปรเจกต์ test (xunit.v3)
│  ├─ MinkQuickLax.Core.Tests/    รวม RepositoryRules/ ที่ตรวจกฎของ repo (resx ครบสองภาษา, Core ไม่อ้าง WPF/Win32, manifest)
│  ├─ MinkQuickLax.Platform.Tests/  test ที่ต้องรันบน Windows (สร้างเมื่อมี test แรก)
│  └─ ManualBuilder.Tests/        (สร้างใน M9)
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
| FolderWindow | ✔ (วาดด้วย CPU) | | ✔ | ✔ | ✔ | 1 ชิ้นต่อ 1 กลุ่ม |
| GroupPanelWindow | | accent acrylic (Win11 22H2+) | ✔ | ✔ | ✔ | สร้างตอนกาง ทำลายตอนหุบ |
| TooltipWindow | | accent acrylic | ✔ | ✔ | ✔ | มีตัวเดียวใช้ร่วมกัน · คลิกทะลุ (`WS_EX_TRANSPARENT`) |
| GlassMenuWindow | | accent acrylic | ✔ | ✔ | ✔ | เมนูคลิกขวาทั้งที่ไอคอนและที่ tray · ปิดเมื่อคลิกนอกเมนู (ดักด้วย Raw Input) |
| EditToolbarWindow | | accent acrylic | ✔ | ✔ | | รับแป้นพิมพ์ในโหมดแก้ไข |
| DragReadoutWindow | ✔ (วาดด้วย CPU) | | ✔ | ✔ | ✔ | คลิกทะลุ |
| ScannerWindow | | Acrylic ของระบบ | | | | หน้าต่างปกติ ไม่มีกรอบ · active ได้ จึงใช้ `DWMWA_SYSTEMBACKDROP_TYPE` ได้ |
| SettingsWindow | | Mica | | | | `FluentWindow` ของ WPF-UI |
| ManualWindow | | Mica | | | | WebView2 |

- **layered ใช้กับเบลอไม่ได้:** หน้าต่างที่เบลอต้องตั้ง `AllowsTransparency=False` + `WindowChrome` (`GlassFrameThickness=-1`) + พื้นหลังของ `CompositionTarget` โปร่งใส
- **accent acrylic:** `SetWindowCompositionAttribute(WCA_ACCENT_POLICY, ACCENT_ENABLE_ACRYLICBLURBEHIND)` เพราะ Acrylic ของระบบ (`DWMWA_SYSTEMBACKDROP_TYPE`) เป็นพื้นทึบเสมอบนหน้าต่างที่ไม่เคย active (S4) · มุมโค้งใช้ `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND` · ถ้าเรียกไม่สำเร็จให้ใช้พื้นทึบ `a = 0.92`
- **กลไกกลาง:** style ของหน้าต่างตั้งผ่าน `WindowStyles.Apply(hwnd, …)` ที่เดียว

### 3.3 service หลัก

| service | อยู่ที่ | หน้าที่ |
|---|---|---|
| `ConfigStore` | Core | โหลด/บันทึก JSON · เขียนไฟล์ชั่วคราวแล้วแทนที่ · หน่วงบันทึก 500ms · สำรอง 10 ชุด |
| `ConfigMigrator` | Core | แปลงไฟล์ตาม `schemaVersion` ทีละขั้น |
| `PositionMapper` | Core | สัดส่วน ↔ pixel ตามพื้นที่ทำงานของจอ · เลือกจอสำรองเมื่อจอหาย |
| `SnapEngine` | Core | ชิดกริด 24px / ชิดแนว 8px · คืนเส้นช่วยจัดแนว |
| `CollisionResolver` | Core | หาช่องว่างที่ใกล้ที่สุด (ค้นเป็นวงรอบตามกริด) |
| `TidyLayout` | Core | คำนวณตำแหน่งตอนจัดเรียงใหม่ |
| `UndoStack` | App | เก็บการเปลี่ยนแปลงในโหมดแก้ไข (Ctrl+Z / Ctrl+Y / ยกเลิก) |
| `PlacementController` | App | สร้าง/ทำลาย/อัปเดตหน้าต่างให้ตรงกับข้อมูล · ซ่อน/แสดงทั้งชุด |
| `EditModeController` | App | เข้า/ออกโหมดแก้ไข · ลาก · สร้างกลุ่มเมื่อลากค้าง |
| `MouseProximityTracker` | Platform | ใช้ Raw Input (`RIDEV_INPUTSINK`) บนหน้าต่าง message-only อ่านตำแหน่งเมาส์ ส่งไม่เกิน 30 ครั้ง/วินาที (ส่งตำแหน่งสุดท้ายตามหลังเสมอ) · แจ้งการกดปุ่มเมาส์ด้วย (ใช้ปิดเมนู/แผงเมื่อคลิกข้างนอก) · ไม่ใช้ timer ตอนเมาส์นิ่ง |
| `ProximityAnimator` | App | รับตำแหน่งจาก tracker แล้วคำนวณความทึบ/ขนาดเป้าหมาย · ไล่ค่าเองด้วย timer 33ms ที่ทำงานเฉพาะตอนมีชิ้นกำลังเปลี่ยน · ไม่ใช้ Storyboard ของ WPF เพราะกิน CPU มากกว่า (S3) |
| `TopmostKeeper` | Platform | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` แล้วสั่ง `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE…)` ให้ทุกชิ้น |
| `DisplayChangeWatcher` | Platform | ดัก `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `WM_SETTINGCHANGE` (พื้นที่ทำงาน), resume จาก sleep แล้วสั่งจัดตำแหน่งใหม่ |
| `MonitorProvider` | Platform | รายการจอ + id ที่คงที่ (`QueryDisplayConfig` → `monitorDevicePath`) + EDID (ผู้ผลิต, รุ่น, serial) ไว้จับคู่สำรอง + พื้นที่ทำงาน + DPI |
| `AppScanner` | Platform | ไล่รายการ `shell:AppsFolder` และ shortcut บน Desktop |
| `IconExtractor` | Platform | `IShellItemImageFactory.GetImage` 256px (`SIIGBF_ICONONLY`) → PNG ลง `IconCache` · ตรวจภาพที่เป็น icon เล็กในกรอบแล้วขอขนาดเล็กลง (S5) |
| `Launcher` | Platform | `ShellExecuteEx` (verb `runas` สำหรับ admin) · `shellApp` เปิดด้วย `shell:AppsFolder\<id>` |
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
      "icon": { "source": "favicon" }
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
- **อ่านไฟล์:** field ที่ไม่รู้จักให้เก็บไว้ ไม่ทิ้ง · ค่าที่ขาดใช้ค่าเริ่มต้น
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
- **ค่าตัวเลขของดีไซน์:** (สี, ขนาด, เวลา) อยู่ใน `Styles/Glass/*.xaml` หรือค่าคงที่ที่มีชื่อ ห้ามเขียนตัวเลขลอย ๆ ใน code-behind
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
- [ ] Model + JSON source generation ตามหัวข้อ 4.2
- [ ] `ConfigStore`: เขียนไฟล์ชั่วคราวแล้วแทนที่, หน่วงบันทึก, สำรอง 10 ชุด, กู้จากไฟล์สำรอง, ตรวจและซ่อมข้อมูล
- [ ] `ConfigMigrator` (มีขั้น 0→1 ไว้เป็นตัวอย่าง)
- [ ] `PositionMapper`: สัดส่วน ↔ pixel, จอหายแล้วใช้จอหลัก, ดึงกลับเข้าจอ, ไม่ทับ taskbar
- [ ] `SnapEngine`, `CollisionResolver`, `TidyLayout`
- [ ] `LinkKindDetector`: path/URL → `kind`
- **ตรวจ:** unit test ครอบคลุมทุกข้อข้างบน รวมกรณีไฟล์เสีย จอหาย DPI 100/150/200%

### M3 · ไอคอนบนจอและ tray
- [ ] Glass ResourceDictionary: สี ธีมสว่าง/มืด, brush, ค่าเวลา (SPEC 5.1–5.4) · สลับธีมตาม Windows ทันที
- [ ] `IconWindow`: วาด icon + เงา, ตัวอักษรเมื่อไม่มี icon, ความทึบตอนพัก, ใกล้แล้วชัด/ขยาย, เด้งตอนกด, ไอคอนจางและมี ! เมื่อเป้าหมายหาย
- [ ] `TooltipWindow` (ชื่อไอคอน) และแบบแสดงชื่อใต้ไอคอน
- [ ] `Launcher`: เปิดทุก `kind` ของ F1, run as admin, เปิดตำแหน่งที่เก็บ, แจ้ง error
- [ ] `GlassMenuWindow` + เมนูคลิกขวาที่ไอคอน (SPEC 4.2)
- [ ] `PlacementController`: สร้างหน้าต่างตามข้อมูล, ซ่อน/แสดงทั้งชุด
- [ ] `TopmostKeeper`, `DisplayChangeWatcher`, `SingleInstance`
- [ ] tray: คลิกซ้ายซ่อน/แสดง, เมนูคลิกขวา (SPEC 4.7), icon ของ app (.ico 16–256px ทำจากลูกแก้วสี `#2FD9BE/#FFC15A/#FF7250` ใน mockup)
- [ ] ค่าตั้งจาก `SystemSettingsWatcher`: Transparency effects, Animation effects, High contrast, ประหยัดแบต
- **ตรวจ:** ใส่ link ใน config.json เอง แล้ว app แสดงไอคอน กดเปิดได้ · focus ไม่หลุด · ไม่อยู่ใน Alt+Tab · เปลี่ยนความละเอียด/ถอดจอแล้วตำแหน่งถูก

### M4 · โหมดแก้ไขและการลาก
- [ ] `EditModeController`: เข้า/ออก, ไอคอนสั่น, `EditToolbarWindow` (เพิ่ม, จัดเรียงใหม่, ยกเลิก, เสร็จ)
- [ ] ลากพร้อมชิดกริด/แนว + เส้นช่วยจัดแนว + `DragReadoutWindow`
- [ ] ปล่อยทับแล้วหาช่องว่าง · ไม่ทับ taskbar
- [ ] แป้นพิมพ์: ลูกศร, Shift+ลูกศร, Delete, Ctrl+Z, Ctrl+Y
- [ ] `UndoStack` + ยกเลิกทั้งหมด
- [ ] Ctrl+ลากในโหมดใช้งาน
- **ตรวจ:** ทำตาม SPEC 4.4 ครบทุกข้อที่เป็น F1 (ยกเว้นเรื่องกลุ่ม ซึ่งอยู่ใน M6)

### M5 · เพิ่มและแก้ link
- [ ] `AppScanner` + `ScannerWindow` (ค้นหา, กรอง, ติ๊กหลายตัว, แก้ชื่อ, เพิ่ม)
- [ ] เพิ่มเอง: ไฟล์, โฟลเดอร์, URL
- [ ] `IconExtractor` + `IconCache` · icon จากไฟล์ PNG/ICO · `FaviconFetcher` (อ่าน `<link rel="icon">` แล้วค่อยลอง `/favicon.ico` · timeout 5 วินาที)
- [ ] วางเพิ่มบนจอ, เอาออกจากจอ, ลบ link (ยืนยันพร้อมบอกจำนวนที่วาง)
- [ ] เปิดครั้งแรกแล้วยังไม่มี link → เปิดหน้าสแกน แล้ววางที่เลือกไว้มุมขวาบน
- **ตรวจ:** สแกนเจอ app ทั้ง Win32 และ Store · เพิ่มครบทุก `kind` ของ F1 · link เดียววาง 2 ที่ แก้ชื่อแล้วเปลี่ยนทั้งคู่

### M6 · กลุ่ม
- [ ] `FolderWindow` (แผ่นกระจกไม่เบลอ + ไอคอนย่อ 2×2)
- [ ] `GroupPanelWindow`: กางตามทิศที่มีที่ว่าง, หุบเมื่อกด link/Esc/คลิกที่อื่น, ไม่แย่ง focus
- [ ] ลากไอคอนเดี่ยวเข้ากลุ่ม, ลาก link ออกจากแผง, จัดลำดับในแผง, ย้ายทั้งกลุ่ม
- [ ] ลากค้าง 0.6 วินาทีบนไอคอนอื่นเพื่อสร้างกลุ่ม
- [ ] เมนู "เพิ่มเข้ากลุ่ม…" และเมนูคลิกขวาที่กลุ่ม
- **ตรวจ:** SPEC 4.3 และแถวเรื่องกลุ่มในตารางการลากของ SPEC 4.4 ครบ

### M7 · หน้าตั้งค่า
- [ ] `SettingsWindow` (WPF-UI, Mica) + NavigationView หมวดที่เป็น F1 ใน SPEC 4.8
- [ ] ปรับแล้วมีผลทันที · คืนค่าเริ่มต้นรายหมวด · ค่าขั้นสูงพับไว้
- [ ] ช่องค้นหาค่าตั้ง (ดัชนีชื่อค่าตั้งไทย/อังกฤษ + คำค้น)
- [ ] หมวด Link: รายการ, ค้นหา/กรอง, แก้ไขทุก field, "วางอยู่ที่ไหน"
- [ ] หมวดทั่วไป: `StartupRegistration` ที่ตรงกับ Task Manager · สลับภาษาทันที
- [ ] หมวดข้อมูล: สำรองตอนนี้, กู้คืน, เปิดโฟลเดอร์, คืนค่าทั้งหมด, ลบข้อมูลทั้งหมดและออก
- [ ] หมวดเกี่ยวกับ: เวอร์ชัน, GitHub, สัญญาอนุญาต, คัดลอกข้อมูลแจ้งปัญหา
- **ตรวจ:** ทุกค่าตั้ง F1 มีผลทันทีกับไอคอนจริง · ปิดใน Task Manager แล้วหน้าตั้งค่าแสดงว่าปิด · สลับภาษาแล้วไม่มีข้อความตกค้าง

### M8 · ภาษา
- [ ] ไล่ตรวจว่าไม่มีข้อความเขียนตรงในโค้ดหรือ XAML
- [ ] test: key ใน `Strings.resx` กับ `Strings.th.resx` ตรงกัน
- [ ] ตรวจทุกหน้าจอทั้งสองภาษา: ภาษาไทยไม่โดนตัดสระ · ภาษาอังกฤษไม่ล้นปุ่ม
- **ตรวจ:** ภาพหน้าจอทุกหน้าในทั้งสองภาษา ไม่มีปัญหาข้างบน

### M9 · คู่มือ
- [ ] `ManualBuilder`: อ่าน `manual/<lang>/*.md` + `keywords.json` → HTML ไฟล์เดียวต่อภาษา (ฝัง CSS, JS, ฟอนต์, รูปแบบ base64, ดัชนีค้นหา)
- [ ] ตรวจตอน build: id หัวข้อไม่ซ้ำ, หัวข้อมีครบทั้งสองภาษา (ภาษาอังกฤษขาดให้เตือนใน F1), id ที่หน้าตั้งค่าอ้างถึงมีจริง
- [ ] template: สไตล์ Glass, ธีมสว่าง/มืด, สารบัญ, ปุ่มสลับภาษา, พิมพ์ได้
- [ ] `search.js`: substring + `Intl.Segmenter('th')` + คำค้นเพิ่ม, `/` และ `Ctrl+K`, ↑↓ Enter, ไฮไลต์
- [ ] `ManualWindow` (WebView2) + เปิดใน browser เมื่อไม่มี runtime
- [ ] เนื้อหาภาษาไทยของฟีเจอร์ F1 ทุกบทใน SPEC 4.9
- [ ] ผูกการ build คู่มือเข้ากับ build ของ app (MSBuild target) แล้วคัดลอกผลลัพธ์ไปไว้ที่ `Manual/`
- **ตรวจ:** ปิดเน็ตแล้วเปิดคู่มือได้ · ค้น "คีย์ลัด", "hotkey", "ซ่อน" เจอหัวข้อที่ถูก · คำไทยกลางประโยคก็เจอ

### M10 · ติดตั้ง อัปเดต และออกรุ่นแรก
- [ ] `UpdateService` + แจ้งเตือนที่ tray + "รีสตาร์ทตอนนี้" + ช่องทาง beta/stable + ปิดการตรวจได้
- [ ] `build/publish.ps1` และ `build/pack.ps1` (self-contained win-x64, shortcut Start Menu, icon, ชื่อ app)
- [ ] `release.yml`: push tag `v*` → build → test → pack → อัปโหลด GitHub Release (tag มี `-beta` ให้ออกเป็น prerelease ช่องทาง beta)
- [ ] ทดสอบใน Windows Sandbox ตามหัวข้อ 8.2
- [ ] ออก `v0.1.0-beta.1` → ใช้เองอย่างน้อย 1 สัปดาห์ → `v0.1.0`
- **ตรวจ:** เครื่องใหม่ติดตั้งจากหน้า Release ได้ · อัปเดต beta.1 → beta.2 เองได้ · ถอนแล้วไม่เหลือรายการใน Run

---

## 7. เฟส 2 และ 3 (ลำดับคร่าว ๆ)

**เฟส 2**
1. คีย์ลัด + หน้าค้นหาด่วน + คีย์ซ่อน/แสดง + หน้าตั้งคีย์พร้อมเตือนคีย์ชน
2. ซ่อนตอนเปิด app เต็มจอ/present (`SHQueryUserNotificationState` + ตรวจหน้าต่างที่ active ว่าเต็มจอ)
3. สไตล์ HUD และ Dot Matrix + ตัวเลือกสไตล์ในหน้าตั้งค่า
4. กลุ่มแบบแถบ · เปิดกลุ่มด้วยการชี้
5. ลากไฟล์/URL มาวาง · ลาก link จากหน้าตั้งค่ามาวางบนจอ
6. เลือกหลายตัวแล้วลากพร้อมกัน
7. หน้าต้อนรับครั้งแรก
8. ปุ่ม ⓘ ในหน้าตั้งค่า · เนื้อหาคู่มือภาษาอังกฤษ
9. link ประเภท `command`, `msSettings` · icon แบบ SVG · แสงสะท้อนวิ่งตามเมาส์

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
- [ ] ติดตั้งบนเครื่องใหม่ ไม่มี UAC · ผ่านหน้า SmartScreen ตามคู่มือได้
- [ ] เปิดครั้งแรกขึ้นหน้าสแกน · เพิ่ม app 5 ตัว
- [ ] กดไอคอนขณะพิมพ์ใน Notepad แล้วตัวอักษรยังพิมพ์ต่อได้
- [ ] Alt+Tab และ taskbar ไม่มีไอคอนของเรา
- [ ] ลาก ชิดกริด ปล่อยทับ ย้อนกลับ ยกเลิก
- [ ] สร้างกลุ่มด้วยการลากค้าง · กางกลุ่มใกล้ขอบจอทั้ง 4 ด้าน
- [ ] สลับธีม Windows สว่าง/มืด · ปิด Transparency effects · ปิด Animation effects
- [ ] เปลี่ยน scale 100% → 150% · ถอดจอที่สองแล้วเสียบกลับ
- [ ] ลากไอคอนข้ามจอที่ scale ต่างกันแล้วขนาดไม่เพี้ยน (S2)
- [ ] id ของจอเดิมหลังรีบูต, ถอดแล้วเสียบ, สลับพอร์ต (S9) · ดูได้จาก log ของ app
- [ ] รีสตาร์ท Explorer (Task Manager) แล้ว icon ที่ tray กลับมา
- [ ] รีบูตแล้ว app เปิดเองแบบเงียบ · ปิดใน Task Manager แล้วไม่เปิดเอง
- [ ] Task Manager → ปิดโปรเซสระหว่างบันทึก แล้วเปิดใหม่ ค่าตั้งไม่เสีย
- [ ] สลับภาษาไทย/อังกฤษทุกหน้าจอ
- [ ] คู่มือเปิดได้ตอนไม่มีเน็ต และค้นหาได้
- [ ] อัปเดตจากรุ่นก่อนหน้า · ถอนการติดตั้ง

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
| S6 | 2026-09-15 | ผ่าน (แหล่งอัปเดตในเครื่อง) | ติดตั้งแบบ `--silent` 3.8 วินาที ไม่ขอสิทธิ์ admin · มี shortcut แค่ Start Menu (`--shortcuts StartMenuRoot`) · ติดตั้งแบบปกติแล้วเปิด app ให้เอง · อัปเดต 1.0.1→1.0.3 (stable) และสลับไป 1.0.4-beta.1 (beta) แล้วรีสตาร์ทเอง · path ใน Run `…\current\<app>.exe --startup` ไม่เปลี่ยน · ถอนแล้ว hook ลบค่าใน Run, shortcut และรายการใน Apps หาย, โฟลเดอร์ถูกลบหลัง process จบ · **ยังไม่ได้ทดสอบกับ GitHub Release และ Windows Sandbox** (เครื่องไม่มี Sandbox) | ใช้ Velopack ต่อ · ใส่ `Environment.ProcessPath` ลง Run ได้เลย · ทดสอบ GitHub source ใน M10 |
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
