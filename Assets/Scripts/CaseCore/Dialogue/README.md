# Evidence-driven dialogue trees (Case Closed)

Interview flow: **present a collected record → pick a question choice → hear a stress/slip reply**.

`ScenarioDialogueClient` loads JSON from `Assets/Resources/Dialogue/Trees/` and returns
`{ line, emotion, slip }`. Replies still emit `InterrogationDialogueService.SuspectLineReady`
→ `ElevenLabsVoiceSink` → Speak. No free-text Ask. No Gemini.

## Play order

1. CaseCore stress / truth (`InterrogationController`)
2. **`ScenarioDialogueClient` tree resolve** (preferred, offline)
3. Mock `FallbackLine` / slip string (last resort)

## Tree layout

```
Resources/Dialogue/Trees/
  tree_index.json     # { "trees": ["jamie", "drew", ...] }
  jamie.json
  drew.json
  …
```

### File shape

| Field | Notes |
| --- | --- |
| `suspectId` | `drew` (normalized; `suspect_drew` also works at runtime) |
| `displayName` | Full name (HUD / log) |
| `branches[].evidenceKey` | `access` `red_herring` `alibi_support` `roster` `sign_in` `hold` `release` |
| `questions[].id` | Stable id for the HUD choice |
| `questions[].prompt` | Player-facing question text |
| `questions[].aggressive` | Passed into CaseCore stress |
| `replies[]` | 2–4 variants gated by `minStress`/`maxStress`, with `emotion` + `slip` + `line` |

Evidence ids from CaseGenerator (`{caseId}_{id}_access`, …) map to `evidenceKey` by suffix.

## Regenerate

```bash
python Tools/gen_dialogue_trees.py
```

Legacy keyword banks under `Resources/Dialogue/*.json` are unused by the client (kept for reference / rollback).
