# Offline scenario dialogue (Case Closed)

Replaces Gemini for interrogation lines. `ScenarioDialogueClient` loads JSON from
`Assets/Resources/Dialogue/` and returns `{ line, emotion, slip }` for ElevenLabs.

## Play order

1. CaseCore stress / truth (`InterrogationController`)
2. **`ScenarioDialogueClient`** (preferred, offline)
3. Optional Gemini (off by default — do not delete Netlify yet)
4. Mock `FallbackLine` / slip string (last resort)

## Add lines

1. Edit or add a JSON bank under `Assets/Resources/Dialogue/` (see `drew.json`).
2. List the file stem in `bank_index.json` (`"drew"` → `Dialogue/drew.json`).
3. Regenerating everything: `python Tools/gen_dialogue_bank.py`

### Entry fields

| Field | Notes |
| --- | --- |
| `id` | Unique string |
| `suspectId` | `drew`, `suspect_drew`, or `*` |
| `intent` | `alibi` `evidence` `pressure` `small_talk` `accusation` `hold` `release` `slip` `shutdown` `generic` |
| `keywords` | Pipe-separated; optional |
| `minStress` / `maxStress` | 0–100 gates |
| `emotion` | `calm` `defensive` `nervous` `angry` `scared` `broken` |
| `slip` | Culprit tell |
| `line` | Spoken text (keep short) |
| `requiresEvidence` / `requiresAggressive` | Extra gates |
| `culpritOnly` / `innocentOnly` | Truth gates |

Emotions and slips still feed `SuspectLineReady` → ElevenLabs unchanged.
