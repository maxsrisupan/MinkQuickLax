# MinkQuickLax

[![CI](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml/badge.svg)](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml)

[English](README.md) · **ภาษาไทย**

Quick launcher สำหรับ Windows · link แต่ละตัวเป็นไอคอนเล็ก ๆ ลอยอยู่บนสุดของจอ วางไว้ตรงไหนก็ได้ กดแล้วเปิดทันที

- วางไอคอนเดี่ยวและกลุ่ม (โฟลเดอร์) ได้ทุกจอ ใช้ปนกันได้
- ไม่แย่ง focus จาก app ที่กำลังพิมพ์อยู่ ไม่โผล่ใน taskbar และ Alt+Tab
- สแกนหา app ในเครื่อง หรือเพิ่มไฟล์ โฟลเดอร์ และ URL เอง
- หน้าตาแบบกระจก เปลี่ยนตามธีมสว่าง/มืดของ Windows
- ภาษาไทยและอังกฤษ
- ฟรีและ open source (MIT)

## สถานะ

อยู่ระหว่างพัฒนาช่วงแรก ยังไม่มีตัวให้ติดตั้ง · ตอนนี้วางโครงโปรเจกต์เสร็จแล้ว ขั้นต่อไปคือทดลองส่วนที่เสี่ยงทางเทคนิคก่อนเริ่มทำฟีเจอร์

## ใช้กับ

- Windows 10 22H2 ขึ้นไป แบบ x64 (เอฟเฟกต์เบลอต้องใช้ Windows 11 22H2 ขึ้นไป)

## build เอง

ติดตั้ง [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) แล้วรัน

```powershell
dotnet build MinkQuickLax.slnx
dotnet test --solution MinkQuickLax.slnx
dotnet run --project src/MinkQuickLax
```

app จะแสดง icon ที่ถาดแจ้งเตือน · บน Windows 11 อาจอยู่ใต้ปุ่ม "แสดงไอคอนที่ซ่อน"

## เอกสาร

- [docs/SPEC.md](docs/SPEC.md): ข้อกำหนด
- [docs/PLAN.md](docs/PLAN.md): สถาปัตยกรรม ลำดับงาน และความคืบหน้า

## สัญญาอนุญาต

[MIT](LICENSE)
