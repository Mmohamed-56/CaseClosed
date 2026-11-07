# ElevenLabs TTS (Case Closed)

Fast suspect speech for interrogation. Listens to Mikasa’s Gemini events via `ElevenLabsVoiceSink`.

## Add the API key (Magdy)

1. **Preferred:** set user env var `ELEVENLABS_API_KEY`, then **restart Unity**.
2. **Local file:** one-line key in project root `elevenlabs.key` (gitignored).

No key → TTS skips silently; text replies still work.

## Event hook

| Event | Action |
| --- | --- |
| `InterrogationDialogueService.QuestionStarted` | Cancel in-flight TTS |
| `InterrogationDialogueService.SuspectLineReady` | Speak line (`SuspectVoiceLine`) |
| `SuspectDialogueEvents.ReplyReady` | Shared bus (`ISuspectSpeech`) |

Latency: Flash model `eleven_flash_v2_5`, stream endpoint, `optimize_streaming_latency=4`, short clips, clip cache, non-blocking `AudioSource`.
