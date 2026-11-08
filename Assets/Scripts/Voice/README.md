# ElevenLabs TTS (Case Closed)

Fast suspect speech for interrogation. Listens to Mikasa’s Gemini events via `ElevenLabsVoiceSink`.

## Shipped builds (cloud proxy)

1. Assign `ApiProxySettings` on `ElevenLabsVoiceClient` (default `https://ubcaseclosed.netlify.app`).
2. App token must match Netlify `CASE_CLOSED_APP_TOKEN`.
3. Client calls `POST {ProxyBaseUrl}/api/tts` and plays returned `audio/mpeg`.

No `ELEVENLABS_API_KEY` in the build.

## Editor fallback

1. **Preferred for local:** set user env var `ELEVENLABS_API_KEY`, then **restart Unity**.
2. **Local file:** one-line key in project root `elevenlabs.key` (gitignored).

No proxy / no key → TTS skips silently; text replies still work.

## Event hook

| Event | Action |
| --- | --- |
| `InterrogationDialogueService.QuestionStarted` | Cancel in-flight TTS |
| `InterrogationDialogueService.SuspectLineReady` | Speak line (`SuspectVoiceLine`) |
| `SuspectDialogueEvents.ReplyReady` | Shared bus (`ISuspectSpeech`) |
| `SuspectDialogueEvents.SpeechStarted` | AudioSource began playing → `CharacterAnimationDriver.PlayTalk()` (`talk`) |
| `SuspectDialogueEvents.SpeechEnded` | Playback finished / cancelled → `PlayStopTalking()` (`stopTalking`) |

Sit-talk bridge: `SuspectTalkAnimationBridge` (auto-added on `SuspectEnterController`) listens to SpeechStarted/Ended and fires Bezi’s Mixamo seated-talk triggers. No scene rewrite required.

Latency: Flash model `eleven_flash_v2_5`, `optimize_streaming_latency=4`, short clips, clip cache, non-blocking `AudioSource`.

See Project store doc: `docs/api-proxy.md`.
