# Interrogation dialogue (Case Closed)

**Preferred path:** offline `ScenarioDialogueClient` + JSON under `Assets/Resources/Dialogue/`.
See `Assets/Scripts/CaseCore/Dialogue/README.md`.

ElevenLabs still consumes `InterrogationDialogueService.SuspectLineReady`.

## Legacy Gemini / Netlify

`GeminiApiClient` and `netlify/functions/gemini.mjs` remain in tree. Player proxy is off by
default (`allowGeminiFallback = false`). Sibling branch strips Gemini; do not delete Netlify here.

Mock `FallbackLine` / CaseCore slip strings are the last resort when the scenario bank is missing.
