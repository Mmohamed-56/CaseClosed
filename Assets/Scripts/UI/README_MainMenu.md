# Main menu — outdoor night level (Case Closed at UB)

Title is a real **level**: night exterior outside University Police, translucent brand UI over the 3D set, Start still fades into `SampleScene`.

## Play

1. Open `Assets/Scenes/MainMenu.unity` (Build Settings index **0**).
2. Play → framed on the station facade / street at night, soft fluorescent flicker, cool URP grade.
3. **CASE CLOSED / AT UB** overlay stays brand-first and translucent so the set reads behind.
4. **START SHIFT** fades into `SampleScene` (index **1**). **QUIT** ends Play Mode / player.

## Scripts

| Asset | Role |
| --- | --- |
| `MainMenuController.cs` | Translucent title UI, fade, Start/Quit, menu music + click |
| `MainMenuExteriorBootstrap.cs` | Runtime night street + facade blockout, lamp flicker, camera frame, volume hook |
| `SoftBulbFlicker.cs` | Subtle fluorescent / lamp intensity drift (shared with SampleScene refs) |
| `DeskUiPalette.cs` | Navy + manila + blotter colors |

`MainMenuExteriorBootstrap` builds primitives so the scene works without an exterior mesh. Bezi can later parent a real facade / Brick `building.glb` (needs GLTF importer) under the set and hide the blockout cubes.

## Audio

1. **Preferred:** Magdy drops funky track at `Assets/Audio/Music/menu_funky.*` and assigns **MainMenu → menuMusicClip** (see `Assets/Audio/Music/README.md`).
2. **Also:** `Resources/Music/menu_funky` auto-loads if present.
3. **Fallback:** `Resources/SFX/office_ambience`.

## Post

`Assets/Settings/MainMenuNightProfile.asset` — cool white balance, light desat, vignette, film grain, restrained bloom. No purple grade.
