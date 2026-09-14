# MinkQuickLax

Quick launcher สำหรับ Windows: link แต่ละตัวเป็นไอคอนลอยบนสุดของจอ วางตรงไหนก็ได้ รวมเป็นกลุ่มได้ · C# .NET 10 + WPF · ฟรี open source (MIT) · ภาษาไทยและอังกฤษ

## เอกสาร (อ่านตามลำดับ)
1. [docs/PLAN.md](docs/PLAN.md): **เริ่มที่นี่** บรรทัด "ความคืบหน้า" บอกงานถัดไป · มีโครงสร้าง repo, สถาปัตยกรรม, ข้อมูล, ข้อตกลง และลำดับงานพร้อม checkbox
2. [docs/SPEC.md](docs/SPEC.md): ข้อกำหนดทั้งหมด **ยึดไฟล์นี้เป็นหลัก** · เปิดหัวข้อที่เกี่ยวกับงานที่กำลังทำ
3. [DESIGN-DISCUSSION.md](DESIGN-DISCUSSION.md): บันทึกการคุยออกแบบ ใช้ดูเหตุผลย้อนหลังเท่านั้น ไม่ต้องแก้แล้ว
4. Mockup: https://claude.ai/code/artifact/0a7e4095-33f8-4eb2-8301-298bd6f00bd2 (สไตล์ Glass, HUD, Dot Matrix)

## วิธีทำงาน
- **ภาษา:** คุยกับผู้ใช้เป็นภาษาไทย · เอกสารใน `docs/` เป็นภาษาไทย · โค้ด comment และ commit message เป็นภาษาอังกฤษ
- **ทำตามลำดับ milestone ใน PLAN.md** · M1 (spike) ต้องผ่านก่อนเริ่ม M2 ถ้าไม่ผ่านให้แจ้งผู้ใช้พร้อมทางเลือก
- **ทำงานเสร็จแล้ว:** ติ๊ก checkbox และแก้บรรทัด "ความคืบหน้า" ใน PLAN.md · ผล spike เขียนลง PLAN.md หัวข้อ 11
- **ต้องเปลี่ยนการตัดสินใจ:** ถามผู้ใช้ก่อนถ้ากระทบสิ่งที่ผู้ใช้เห็น แล้วแก้ SPEC.md และเพิ่มแถวในหัวข้อ 8 ของ SPEC
- **ผู้ใช้ให้ Claude เลือกเครื่องมือและเขียนโค้ดทั้งหมด** · ตัดสินใจทางเทคนิคเองได้ แต่ต้องบันทึกเหตุผลไว้
- **ข้อห้ามสำคัญ**
  - ไม่เขียนข้อความที่ผู้ใช้เห็นลงในโค้ดตรง ๆ ต้องใช้ `Strings.resx` + `Strings.th.resx`
  - ตัว app ไม่รันเป็น admin
  - Core ห้ามอ้าง WPF หรือ Win32

## คำสั่ง
ต้องมี .NET SDK ตาม `global.json` (10.0.401 ขึ้นไป) · รันจาก root ของ repo

| งาน | คำสั่ง |
|---|---|
| build | `dotnet build MinkQuickLax.slnx` |
| test | `dotnet test --solution MinkQuickLax.slnx` |
| test เฉพาะโปรเจกต์ | `dotnet test --project tests/MinkQuickLax.Core.Tests` |
| รัน app | `dotnet run --project src/MinkQuickLax` (icon อยู่ที่ tray บน Windows 11 อาจอยู่ใต้ "แสดงไอคอนที่ซ่อน") |
| สร้างคู่มือ | _(เติมใน M9)_ |
| สร้างตัวติดตั้ง | _(เติมใน M10)_ |

- **test ใช้ Microsoft.Testing.Platform:** ต้องระบุ `--solution` หรือ `--project` · โปรเจกต์ test ที่ไม่มี test เลยจะล้ม (exit code 8) จึงสร้างโปรเจกต์ test พร้อม test แรกเท่านั้น
- **ปิด app ก่อน build ซ้ำ:** ถ้า app ยังรันอยู่ build จะล้มเพราะไฟล์ exe ถูกล็อก
