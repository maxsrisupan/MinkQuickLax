# สำรองข้อมูล ย้ายเครื่อง และถอนการติดตั้ง {#data}

## ข้อมูลเก็บไว้ที่ไหน {#data-locations}

| ข้อมูล | ที่เก็บ |
|---|---|
| ค่าตั้ง, link, กลุ่ม และตำแหน่งไอคอน | `%AppData%\MinkQuickLax\config.json` |
| ไฟล์สำรอง (10 ชุดล่าสุด) | `%AppData%\MinkQuickLax\backups\` |
| icon ที่ดึงมาเก็บไว้ | `%LocalAppData%\MinkQuickLax\cache\` |
| log (เก็บ 7 วัน) | `%LocalAppData%\MinkQuickLax\logs\` |
| ข้อมูลของหน้าต่างคู่มือ | `%LocalAppData%\MinkQuickLax\WebView2\` |

พิมพ์ที่อยู่ในตารางลงในช่องที่อยู่ของ File Explorer เพื่อเปิดโฟลเดอร์ได้ หรือใช้ปุ่ม[เปิดโฟลเดอร์ค่าตั้ง](#settings-open-folder)

## สำรองข้อมูล {#backup}

MinkQuickLax สำรองค่าตั้งให้เองตอนเปิด app และทุกวันที่มีการเปลี่ยนแปลง เก็บไว้ 10 ชุดล่าสุด ถ้าจะสำรองก่อนแก้อะไรเยอะ ๆ ให้กด[สำรองตอนนี้](#settings-backup)

ถ้าอยากเก็บสำรองไว้นอกเครื่อง ให้คัดลอกไฟล์ `config.json` จาก `%AppData%\MinkQuickLax\` ไปเก็บไว้

## ย้ายไปเครื่องใหม่ {#move-pc}

1. เครื่องเดิม: เปิด MinkQuickLax แล้ว[ออก](#tray)จากเมนูที่ tray เพื่อให้บันทึกครบ
2. คัดลอกไฟล์ `%AppData%\MinkQuickLax\config.json`
3. เครื่องใหม่: [ติดตั้ง](#install) MinkQuickLax เปิดครั้งหนึ่งแล้วออก
4. วาง `config.json` ทับไฟล์เดิมใน `%AppData%\MinkQuickLax\` ของเครื่องใหม่
5. เปิด MinkQuickLax อีกครั้ง

link ที่ชี้ไปยังโปรแกรมหรือไฟล์ที่ไม่มีในเครื่องใหม่จะแสดงเป็น[ไอคอนที่เป้าหมายหาย](#missing-target) ถ้าจอของเครื่องใหม่ไม่เหมือนเดิม ไอคอนจะไปอยู่ตำแหน่งเดียวกันตามสัดส่วนของจอหลัก

## ถอนการติดตั้ง {#uninstall}

Settings ของ Windows → Apps → Installed apps → MinkQuickLax → Uninstall

- รายการเปิดพร้อม Windows ถูกลบออก
- **ค่าตั้งยังอยู่** ใน `%AppData%\MinkQuickLax\` ถ้าติดตั้งใหม่จะได้ link และตำแหน่งเดิมกลับมา

## ลบข้อมูลที่เหลือ {#remove-leftovers}

ถ้าไม่ต้องการเก็บอะไรไว้ในเครื่อง เลือกวิธีใดวิธีหนึ่ง

- **ก่อนถอนการติดตั้ง:** หน้าตั้งค่า → ข้อมูล → [ลบข้อมูลทั้งหมดและออก](#settings-delete-all) แล้วค่อยถอนการติดตั้ง
- **หลังถอนการติดตั้ง:** ลบโฟลเดอร์ `%AppData%\MinkQuickLax` และ `%LocalAppData%\MinkQuickLax` เอง
