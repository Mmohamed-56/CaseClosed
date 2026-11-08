# Gemini interrogation (Case Closed)

Snappy Gemini dialogue on top of CaseCore. Code owns stress/truth; Gemini only performs short spoken lines.

## Quick start (shipped builds → cloud proxy)

1. Assign `ApiProxySettings` (default base URL `https://ubcaseclosed.netlify.app`) on `InterrogationDialogueService` / `GeminiApiClient`.
2. Set the same **App Token** as Netlify env `CASE_CLOSED_APP_TOKEN` (menu: **Case Closed → API Proxy**).
3. Typed questions go through `InterrogationDialogueService.AskCurrentSuspect`.
4. Subscribe to `SuspectLineReady` for ElevenLabs (see `ElevenLabsVoiceSink`).

Player builds call `POST {ProxyBaseUrl}/api/gemini`. Provider keys stay on Netlify only.

## Editor fallback (local only)

Without proxy, or when testing offline keys:

1. Set `GEMINI_API_KEY` via env, PlayerPrefs (**Case Closed → Gemini**), or a `GeminiApiKeySettings` asset.
2. Direct calls hit `generativelanguage.googleapis.com` with `?key=…` (Editor only when allowed).

Without proxy and without a key, mock/offline fallback lines are used automatically.

See Project store doc: `docs/api-proxy.md`.
