# FoodIsekaiZ — UWB, Dual Display และ Gameplay Architecture

## 1. ภาพรวมการไหลของข้อมูล

```text
NoopLoop Tag 1..N
    -> Serial COM / UDP binary datagram
    -> NoopLoopFrameParser
    -> UWBManager (axis conversion, reject jump, smoothing, calibration)
    -> UWBPlayerController (Tag ID binding, Rigidbody visual smoothing บน XZ)
    -> SphereCollider
    -> ArenaSlot2D 3D trigger
    -> FoodIsekaiZGameManager
    -> Side UI / score
```

ระบบไม่ hard-code จำนวนผู้เล่นไว้ที่ 4 คน Dictionary ใน `UWBManager` ใช้ Tag ID เป็น key จึงเพิ่มผู้เล่นได้ด้วยการเพิ่ม element ใน array ของ `UWBPlayerSpawner` และกำหนด ID ใหม่

## 2. ไฟล์จาก `ledfloorpaperarena` ที่ใช้

ย้ายเฉพาะกลุ่ม UWB ต่อไปนี้ (ใน `FoodIsekaiZ` มีสำเนาอยู่ใน `Assets/Scripts` แล้ว):

- `Assets/_Project/Scripts/UWB/NoopLoopFrameParser.cs` — ถอด TagFrame0/AnchorFrame0
- `Assets/_Project/Scripts/UWB/NoopLoopPose.cs` — data model ของ frame
- `Assets/_Project/Scripts/UWB/NoopLoopSerialPort.cs` — Win32 serial reader
- `Assets/_Project/Scripts/UWB/UWBManager.cs` — connection, filtering, prediction และ trilateration
- `Assets/_Project/Scripts/Utilities/GameConfigs/BaseConfigManager.cs`
- `Assets/_Project/Scripts/Utilities/GameConfigs/UWBConfigData.cs`
- `Assets/_Project/Scripts/Utilities/GameConfigs/UWBConfigManager.cs`

`UWBTracker.cs` ถูกเก็บไว้เพื่อ compatibility กับโปรเจกต์ต้นแบบ แต่ Player ใหม่ควรใช้ `UWBPlayerController.cs` แทน ไม่ต้องย้าย Character, Territory, Team หรือ PaperArena manager มาด้วย

## 3. สร้าง Scene

แนะนำ hierarchy:

```text
FoodIsekaiZScene
├── Systems
│   ├── UWBConfigManager
│   ├── UWBManager
│   ├── DisplayManager
│   └── FoodIsekaiZGameManager
├── Display1_Side
│   ├── SideCamera
│   └── SideDisplayLayout (FoodIsekaiZSideDisplayLayout)
│       └── _GeneratedSideDisplay (Canvas, Target Display 1)
└── Display2_Floor
    ├── FloorCamera (Orthographic, อยู่เหนือสนามและมองลงระนาบ XZ)
    ├── FloorCanvas (Target Display 2)
    ├── Map (FoodIsekaiZArenaLayout)
    │   └── _GeneratedArena
    │       ├── Background + Grid + Boundary
    │       ├── TopCustomerSlots (Customer01..04)
    │       └── BottomStationSlots (Food01..05, Deposit)
    └── Players (UWBPlayerSpawner; สร้าง Player01..04 ตอน runtime)
```

`FoodIsekaiZArenaLayout` ใช้แนวทางเดียวกับ `MapBackground`/`PaperArenaCamera` ของ PaperArena: สร้าง mesh จากขนาดสนามและ fit Orthographic Camera จาก bounds อัตโนมัติ แต่เพิ่ม Edit Mode preview ให้เห็นสนามและจุดทั้งหมดก่อนกด Play เลือก `Game View > Display 2` เพื่อดูภาพจาก Floor Camera

### Player array / prefab

วิธีแนะนำ: ใส่ `UWBPlayerSpawner` บน GameObject `Players` แล้วกำหนด array `Players` ใน Inspector แต่ละ element มี `Player ID`, `Tag ID`, สี และตำแหน่งเริ่มต้น ไม่ต้องวาง Player01..04 ใน Hierarchy ด้วยมือ หากไม่กำหนด `Player Prefab` ระบบจะสร้างวงกลมพร้อม component ให้เอง

หากต้องการรูป/ขนาดเฉพาะ ให้สร้าง Player prefab ที่มี component ด้านล่างแล้วลากเข้า `Player Prefab` ของ spawner:

เพิ่ม component ต่อไปนี้บน GameObject เดียวกัน:

- `UWBPlayerController`
- `FoodIsekaiZPlayerState`
- `Rigidbody` (script จะตั้งเป็น Kinematic และปิด Gravity)
- `SphereCollider`
- `SpriteRenderer` (ถ้าไม่ใส่ sprite script จะสร้างวงกลมให้ runtime)

กำหนด Player/Tag เริ่มต้น:

| GameObject | Player ID | UWB Tag ID | สีแนะนำ |
|---|---:|---:|---|
| Player01 | 1 | 1 | Cyan |
| Player02 | 2 | 2 | Magenta |
| Player03 | 3 | 3 | Yellow |
| Player04 | 4 | 4 | Green |

Tag ID ต้องตรงกับ byte ID ที่อ่านได้จาก NoopLoop frame ไม่จำเป็นต้องเรียง 1–4 เช่น hardware จริงเป็น 11, 14, 21, 25 ให้ใส่เลขนั้นใน `tagId` ได้ทันที

### Slot setup

วิธีแนะนำคือให้ `FoodIsekaiZArenaLayout` สร้าง Slot ทั้ง 10 ช่องและส่ง array เข้า GameManager อัตโนมัติ จึงไม่ต้องสร้าง/ลาก Slot ด้วยมือ ขั้นตอนด้านล่างใช้เมื่ออยากจัดตำแหน่งเองเท่านั้น

- ด้านบนสร้าง `ArenaSlot2D` 4 ตัว, `slotType = Customer`
- ด้านล่างสร้าง `ArenaSlot2D` 5 ตัว, `slotType = FoodStation`, ตั้ง `stationFood` เป็น Food1..Food5 ไม่ซ้ำกัน
- ด้านล่างอีก 1 ตัว, `slotType = MoneyDeposit`
- แต่ละ slot ต้องมี `BoxCollider`; script จะตั้ง `isTrigger = true`
- นำ Customer ทั้ง 4 และ Bottom ทั้ง 6 ใส่ array ของ `FoodIsekaiZGameManager`
- ใส่ child visual ของเหรียญลง `moneyVisual` ของ Customer แต่ละช่อง (ไม่บังคับ)

Gameplay loop คือหยิบอาหารเมื่อมือว่าง, ส่งอาหารที่ตรงกับ request, รอ `eatingDurationSeconds`, เก็บเงินเมื่อเดินเข้า Customer อีกครั้ง และเดินเข้าช่อง Deposit เพื่อเพิ่มคะแนนทีม

## 4. UWB connection และ calibration

1. เพิ่ม `UWBConfigManager` ก่อนเข้า Play Mode ครั้งแรก ระบบจะสร้าง `UWBConfig.json` ที่ `Application.persistentDataPath`
2. Serial: เลือก `transportMode = Serial`, ตั้ง COM และ baud (NoopLoop ปกติ 921600)
3. UDP: เลือก `transportMode = Udp`, ตั้ง listen address/port ตัวส่งต้องส่ง **NoopLoop binary frame** ทั้งก้อนใน datagram; JSON/CSV ต้องมี adapter parser เพิ่ม
4. ตั้ง `axisConversion` ตามการติดตั้งจริง ค่าเริ่มต้น raw X -> Unity X, raw Y -> Unity Z, raw Z ถูกทิ้ง
5. เดิน Tag ไปมุมล่างซ้ายและบันทึก `(X,Z)` เป็น `physicalMinMeters`
6. เดิน Tag ไปมุมบนขวาและบันทึก `(X,Z)` เป็น `physicalMaxMeters`
7. ตั้งขอบเขต Floor X/Z เป็น `arenaMin`/`arenaMax` เช่น `(-6,-4)` ถึง `(6,4)` โดย Vector2.y แทน world Z
8. เปิด `clampToArena` เพื่อไม่ให้ noise พาผู้เล่นออกนอกกรอบ

สูตร mapping ที่ใช้:

```text
normalized = (uwbXZ - physicalMin) / (physicalMax - physicalMin)
floorXZ    = Lerp(arenaMin, arenaMax, normalized)
```

ถ้าทิศกลับด้าน ให้สลับ min/max ของแกนนั้น หรือใช้ token ติดลบใน `axisConversion` พร้อม `UWBInputOffset` ไม่ควรหมุน Player prefab เพื่อแก้ calibration

## 5. Dual display

- ค่าจอที่พบจาก PaperArena Unity Game View profiles คือ Side `1536x435` และ Floor `2816x1280`
- `GameConfig.json` ของ PaperArena กำหนด logical grid ด้วย `MapWidth = 10`, `MapHeight = 10`, `gameTileUnit = 1`; ค่านี้ไม่ใช่อัตราส่วนกายภาพของจอพื้น
- Floor layout ใช้ `11x5` world units ซึ่งมี aspect `2.2` ตรงกับ `2816:1280` เพื่อให้ภาพเต็มจอโดยไม่มีแถบดำซ้าย–ขวา ส่วน grid ภายในยังเป็น `10x10`
- `UWBConfig.json` มี `UWBAnchorPositions` 4 จุด ค่าจะถูกอ่านตามลำดับเดียวกับ Anchor Device IDs; ไฟล์ runtime ที่ตรวจบนเครื่องปัจจุบันยังเป็น `(0,0,0)` ทั้ง 4 จุด จึงต้องใส่ค่าที่วัดจริงก่อนใช้ trilateration
- Side Wall ถูกวางตั้งตรงตลอดขอบหลังของ Floor เพื่อให้ Scene preview เป็นรูปตัว L ตามการติดตั้งจริง (`Wall Matches Floor Width`)
- CanvasScaler ใน Scene PaperArena เดิมใช้ reference resolution `1920x1080` ทั้งสอง Canvas แต่ FoodIsekaiZ Side preview ใช้ `1536x435` เพื่อจัด UI ตรงกับจอจริง
- ต่อจอ Side และ Floor ให้ Windows เห็นเป็น Extended Desktop ก่อนเปิดเกม
- `SideCamera.targetDisplay = 0` และ Side Canvas `targetDisplay = 0`
- `FloorCamera.targetDisplay = 1` และ Floor Canvas `targetDisplay = 1`
- `DisplayManager` ตั้งค่าให้และเรียก Display 2 ด้วย `2816x1280`; Standalone primary output ใช้ `1536x435`
- ใส่ `FoodIsekaiZSideDisplayLayout` ที่ `Display1_Side` เพื่อสร้างจอคะแนน/UWB/Player status ใน Edit Mode
- Camera ของ Floor ใช้ Orthographic อยู่ด้านบนแกน Y และมองลงพื้น XZ; แยก Culling Mask เช่น `SideView`/`FloorView` เพื่อไม่ให้ object ข้ามจอ
- Multi-display ทำงานถูกต้องใน Standalone Player มากกว่า Game View ปกติ ให้ทดสอบด้วย Windows build และเลือก resolution ของแต่ละจอให้ตรง hardware

## 6. Integration checklist

- [ ] Scene มี UWBConfigManager เพียงหนึ่งตัว
- [ ] Scene มี UWBManager เพียงหนึ่งตัว และ status เปลี่ยนเป็น frame OK
- [ ] Anchor transform/device ID ถูกต้อง ถ้าเลือก trilateration (`useDevicePositionFirst = false`)
- [ ] วัด physical min/max ด้วย Tag จริง ไม่เดาจากขนาดจอ
- [ ] Player 1–4 ใช้ Tag ID ไม่ซ้ำ และวงกลมเคลื่อนบนพื้น XZ
- [ ] Player มี Rigidbody + SphereCollider + FoodIsekaiZPlayerState
- [ ] Customer 4 ช่องเป็น trigger และอยู่ด้านบน
- [ ] Food Station 5 ช่องกำหนด Food1..Food5 และ Deposit 1 ช่องอยู่ด้านล่าง
- [ ] Camera/Canvas ของ Side ใช้ display 0, Floor ใช้ display 1
- [ ] Build Windows ทดสอบทั้ง serial permission, tag disconnect/reconnect และสองจอจริง
- [ ] ทดสอบ noise ตอนยืนนิ่ง แล้วปรับ manager dead zone ก่อนปรับ player `smoothTime`

## 7. การต่อ UI คะแนน

Side UI สามารถ subscribe `FoodIsekaiZGameManager.PlayerMoneyDeposited` แล้วอ่าน `TeamBankedMoney` หรือ `GetPlayerBankedMoney(playerId)` เพื่อแสดงคะแนนรวม/รายคน ส่วน request icon ใช้ event `CustomerRequestedFood` และสถานะเงินใช้ `CustomerMoneySpawned`
