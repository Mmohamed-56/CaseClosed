# Magdy — Mixamo sit / stand (pointer)

**Canonical guide + drop folder:** [`Assets/Animations/Mixamo/README.md`](../../Animations/Mixamo/README.md)

**Editor menu:** `Case Closed → Characters → Mixamo`  
(`Assets/Editor/MixamoLowPolyPeopleSetup.cs`)

Pathing contract (shared with door→chair): Animator triggers `idle`, `walk`, `sit`, `stand`.  
`SuspectEnterController` fires `walk` on travel, `sit` at `chairPoint`, `stand` before exit. Missing `sit`/`stand` → idle placeholders; same door/chair anchors for every suspect.
