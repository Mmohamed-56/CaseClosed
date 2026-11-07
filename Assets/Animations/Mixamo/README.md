# Mixamo + Low Poly People (David Jalbert)

Drop Mixamo FBX clips here. Magdy downloads from [mixamo.com](https://www.mixamo.com); this folder + the editor menu wire them for the door → chair path.

## Runtime contract (door-chair pathing)

`SuspectEnterController` / `CharacterAnimationDriver` use **Animator triggers** (not bools):

| Trigger | When pathing fires it | Package today |
| --- | --- | --- |
| `idle` | Fallback / seated placeholder | Yes (Low Poly People) |
| `walk` | Door → chair, chair → exit | Yes |
| `sit` | Arrive at `chairPoint` | **You add (Mixamo)** |
| `stand` | Exit before walk-out | **You add (Mixamo)** |
| `run` / `wave` | Optional | Yes (unused by enter path) |

If `sit` / `stand` are missing, pathing **still works**: snaps to the chair / walks out using `idle` + `walk` (`allowPlaceholderPoses`).

Shared scene anchors (Bezi): `doorPoint`, `chairPoint`, optional `exitPoint` on `SuspectEnterController`. One pool of Low Poly People prefabs on `SuspectCharacterPool`.

## Clips to download (minimum for enter/sit/leave)

Use **FBX for Unity**, **Without Skin**, in-place (no root motion) when possible:

1. **Walking** → rename / treat as `walk` (or keep package walk)
2. **Sitting** or **Sitting Idle** → `sit`
3. **Standing Up** / **Stand Up** → `stand`

Optional later (emotion tiers): fidget, lean forward, head in hands, table slam — same trigger style when gameplay asks for them.

Suggested filenames so the editor menu can auto-detect:

- `Mixamo_Walk.fbx`
- `Mixamo_Sit.fbx` / `Mixamo_Sitting.fbx`
- `Mixamo_Stand.fbx` / `Mixamo_StandingUp.fbx`

## Editor menu (after clips are in this folder)

Unity menu: **Case Closed → Characters → Mixamo**

1. **Create Drop Folder** — ensures `Assets/Animations/Mixamo` exists.
2. **Configure Selected FBX As Generic** — hackathon-fast; matches current Low Poly People controllers (Generic, empty Avatar on prefabs).
3. **Configure Selected FBX As Humanoid** — Option B; pick a Low Poly People avatar source when prompted if the FBX is animation-only.
4. **Build SuspectWalkSit Controller** — creates `SuspectWalkSit.controller` with triggers `idle`, `walk`, `sit`, `stand` and Any-State transitions. Assigns clips found in this folder by name.
5. **Add Sit/Stand To Package Controllers** — patches `normal` / `stout` / `strong` controllers under Low Poly People with `sit` + `stand` (keeps existing idle/walk/run/wave).

## Recommended path (hackathon)

**Option A (fast):** Generic Mixamo clips → **Add Sit/Stand To Package Controllers** → leave prefabs on package controllers → Play. Pathing already calls `PlayWalk` / `PlaySit` / `PlayStand`.

**Option B (cleaner retarget):** Humanoid Low Poly avatar + Humanoid Mixamo → **Build SuspectWalkSit Controller** → assign that controller on a wrapper prefab used by `SuspectCharacterPool`.

## Smoke test

1. Wire `doorPoint` + `chairPoint`.
2. Assign Low Poly prefabs on `SuspectCharacterPool`.
3. Briefing → one suspect walks in and sits (`sit` or idle fallback).
4. HOLD / RELEASE → stands, walks out, next suspect reuses the same door/chair.

No browser / monitor scripts involved.
