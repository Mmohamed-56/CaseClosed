# Interrogation dialogue (Case Closed)

CaseCore owns stress/truth. Spoken lines come from **`ScenarioDialogueClient`** by default (offline, no ask-proxy). Gemini is opt-in only.

## Default (Play Mode offline asks)

1. `InterrogationDialogueService` has `useScenarioDialogue = true`, `useGeminiApi = false`.
2. Ask turns never call `POST /api/gemini` or direct Gemini HTTP.
3. UI tags: `[suspect]` or `[scripted]` — never `[gemini]`.
4. TTS still uses the cloud proxy (`POST /api/tts`) via ElevenLabs.

## Legacy Gemini (opt-in)

Set `useGeminiApi = true` on `InterrogationDialogueService` only for debugging:

1. Assign `ApiProxySettings` and/or `GeminiApiKeySettings`.
2. Player builds would call `POST {ProxyBaseUrl}/api/gemini` (function may remain unused in deploy).

Magdy can drop `GEMINI_API_KEY` from Netlify once this path stays off.

See Project store doc: `docs/api-proxy.md` (TTS still required).
