# Audio / SFX

Prototype clips live in `Assets/Resources/SFX/` so `SfxPlayer` and `MainMenuController` can `Resources.Load` them without Inspector wiring:

- door_open.wav
- stamp.wav
- ui_click.wav — title Start/Quit + desk UI clicks
- office_ambience.wav — trimmed ~18s loop; **fallback** main menu bed and desk ambience
- phone_ring.wav (optional)

Source drops were in Downloads (ES_Doors / Rubber Stamp / Keyboard Mouse Clicks / Directors Office ambience / Telephone Ring).

## Main menu music

Funky title theme (preferred):

1. Drop Magdy track at **`Assets/Audio/Music/menu_funky.*`** (see `Assets/Audio/Music/README.md`).
2. Assign on **MainMenu → Main Menu Controller → Menu Music Clip**.
3. Optional auto-load: also place at `Assets/Resources/Music/menu_funky`.

If unset, `MainMenuController` loops `office_ambience` and fades it out before loading `SampleScene`.
