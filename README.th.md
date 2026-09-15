# MinkQuickLax

[![CI](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml/badge.svg)](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml)

[English](README.md) · **ภาษาไทย**

Quick launcher สำหรับ Windows · link แต่ละตัวเป็นไอคอนเล็ก ๆ ลอยอยู่บนสุดของจอ วางไว้ตรงไหนก็ได้ กดแล้วเปิดทันที

- วางไอคอนเดี่ยวและกลุ่ม (โฟลเดอร์) ได้ทุกจอ ใช้ปนกันได้
- ไม่แย่ง focus จาก app ที่กำลังพิมพ์อยู่ ไม่โผล่ใน taskbar และ Alt+Tab
- สแกนหา app ในเครื่อง หรือเพิ่มไฟล์ โฟลเดอร์ และเว็บเอง · link เว็บเลือก browser ที่ใช้เปิดได้
- 3 สไตล์: Glass, HUD และ Dot Matrix · ธีมสว่าง/มืด
- ภาษาไทยและอังกฤษ พร้อมคู่มือในตัวที่ใช้ได้โดยไม่ต้องมีเน็ต
- เปิดพร้อม Windows และอัปเดตเอง
- ฟรีและ open source (MIT)

## สถานะ

ฟีเจอร์ของรุ่นทดลองแรกครบแล้ว · ตัวติดตั้งจะอยู่ที่หน้า [Releases](https://github.com/maxsrisupan/MinkQuickLax/releases) ระหว่างที่รุ่นแรกยังไม่ออก ยังไม่มีไฟล์ให้ดาวน์โหลด

## ติดตั้ง

1. ดาวน์โหลด `MinkQuickLax-win-Setup.exe` (รุ่นทดลองใช้ `MinkQuickLax-beta-Setup.exe`) จากหน้า [Releases](https://github.com/maxsrisupan/MinkQuickLax/releases) แล้วดับเบิลคลิก ติดตั้งแบบรายผู้ใช้ ไม่ต้องใช้สิทธิ์ผู้ดูแลระบบ
2. โปรแกรมยังไม่ได้เซ็นดิจิทัล Windows อาจขึ้น "Windows protected your PC" ให้กด **More info** แล้วกด **Run anyway**

ถอนการติดตั้งได้ที่ Settings ของ Windows → Apps → Installed apps · link และค่าตั้งยังอยู่ใน `%AppData%\MinkQuickLax` ถ้าต้องการลบด้วย ให้ใช้หน้าตั้งค่า → ข้อมูล → "ลบข้อมูลทั้งหมดและออก" ก่อน

## ใช้กับ

- Windows 10 22H2 ขึ้นไป แบบ x64 (เอฟเฟกต์เบลอต้องใช้ Windows 11 22H2 ขึ้นไป)

## build เอง

ติดตั้ง [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) แล้วรัน

```powershell
dotnet build MinkQuickLax.slnx
dotnet test --solution MinkQuickLax.slnx
dotnet run --project src/MinkQuickLax
```

build แล้วจะได้คู่มือ (`Manual\th.html`, `Manual\en.html`) ข้างตัว app ด้วย · app แสดง icon ที่ถาดแจ้งเตือน บน Windows 11 อาจอยู่ใต้ปุ่ม "แสดงไอคอนที่ซ่อน"

สร้างตัวติดตั้งในเครื่อง: `build/publish.ps1 -Version 0.1.0` แล้ว `build/pack.ps1 -Version 0.1.0` (ได้ไฟล์ใน `artifacts/releases`) · push tag เช่น `v0.1.0` หรือ `v0.1.0-beta.1` แล้ว GitHub Actions จะทำขั้นตอนเดียวกันและออกรุ่นให้

## เอกสาร

- [docs/SPEC.md](docs/SPEC.md): ข้อกำหนด
- [docs/PLAN.md](docs/PLAN.md): สถาปัตยกรรม ลำดับงาน และความคืบหน้า
- [manual/th](manual/th): คู่มือผู้ใช้
- [CHANGELOG.md](CHANGELOG.md): สิ่งที่เปลี่ยนในแต่ละเวอร์ชัน

## สัญญาอนุญาต

[MIT](LICENSE)
