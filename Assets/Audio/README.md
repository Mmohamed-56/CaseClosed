# Audio / SFX

Prototype clips live in `Assets/Resources/SFX/` so `SfxPlayer` and `MainMenuController` can `Resources.Load` them without Inspector wiring:

- door_open.wav
- stamp.wav
- ui_click.wav — title Start/Quit + desk UI clicks
- office_ambience.wav — trimmed ~18s loop; **main menu music bed** and desk ambience
- phone_ring.wav (optional)

Source drops were in Downloads (ES_Doors / Rubber Stamp / Keyboard Mouse Clicks / Directors Office ambience / Telephone Ring).

## Main menu

`MainMenuController` loops `office_ambience` on the title screen and fades it out before loading `SampleScene`. Swap in a dedicated theme later by assigning `menuMusicClip` on the MainMenu object (or drop a clip named for a future Resources path).
