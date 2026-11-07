# Gemini interrogation (Case Closed)

Snappy Gemini dialogue on top of CaseCore. Code owns stress/truth; Gemini only performs short spoken lines.

## Quick start

1. Add `InterrogationDialogueService` (+ optional `GeminiApiClient`) next to `CaseShiftController`.
2. Set `GEMINI_API_KEY` via env, PlayerPrefs menu (**Case Closed → Gemini**), or a `GeminiApiKeySettings` asset.
3. Typed questions go through `InterrogationDialogueService.AskCurrentSuspect`.
4. Subscribe to `SuspectLineReady` for ElevenLabs (see `ElevenLabsVoiceSink`).

Without a key, mock/offline fallback lines are used automatically.
