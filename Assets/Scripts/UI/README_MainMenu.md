# Main menu (Case Closed at UB)

Lightweight title screen — navy / manila / blotter desk vibe, no purple.

## Play

1. Open `Assets/Scenes/MainMenu.unity` (also Build Settings index 0).
2. Play → **CASE CLOSED** / **AT UB** → **START SHIFT** fades into `SampleScene`.
3. **QUIT** ends Play Mode in Editor / quits the player build.

## Scripts

| Asset | Role |
| --- | --- |
| `MainMenuController.cs` | Runtime title UI, fade, Start/Quit, menu music + click |
| `DeskUiPalette.cs` | Shared early-2000s campus PD colors |

UI builds itself — Bezi does **not** need to wire buttons for the prototype. Optional: assign `menuMusicClip` / `uiClickClip` on the `MainMenu` object; otherwise it loads `Resources/SFX/office_ambience` and `ui_click`.

## Audio

Menu bed = Emilia’s trimmed `office_ambience` loop. Gameplay SFX after the fade uses `SfxPlayer` (same Resources folder) when the case loop is present.
