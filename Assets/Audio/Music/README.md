# Menu music (Magdy drop)

Funky title theme for the outdoor MainMenu level.

## Drop path

Put the track here:

```
Assets/Audio/Music/menu_funky.wav
```

(or `.ogg` / `.mp3` — Unity will import any of those)

## Wire

1. Select the **MainMenu** object in `Assets/Scenes/MainMenu.unity`.
2. Drag the clip onto **Main Menu Controller → Menu Music Clip**.
3. Optional alternate: also copy/move a Resources-loadable copy to `Assets/Resources/Music/menu_funky` (no extension in the Resources path) if you want auto-load without Inspector assign.

## Fallback

If `menuMusicClip` is empty and no `Resources/Music/menu_funky` exists, the menu loops Emilia’s `Resources/SFX/office_ambience` bed so Play Mode is never silent.
