# Assets/UI

Drop desk HUD art here (optional). Runtime also loads from `Resources/`:

| Path | Use |
| --- | --- |
| `Resources/Fonts/Boldonse-Regular.ttf` | Body / UI (OFL) |
| `Resources/Fonts/HeadCapitalBoldGrunge.otf` | Stamp titles (HOLD / RELEASE / ACTIONS / CASE FILE) |
| `Resources/UI/manila-paper` (Sprite) | Optional paper texture — **not required**; clean manila fill is default |

No TextMeshPro package in this project yet — fonts are wired as Unity `Font` for UGUI `Text`. After Magdy opens the worktree, Unity imports the TTF/OTF. To convert to TMP later: add TMP, Font Asset Creator → assign in Inspector.
