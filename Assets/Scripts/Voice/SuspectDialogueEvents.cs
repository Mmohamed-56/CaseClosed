using System;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Cross-system dialogue bus for Case Closed interrogation.
    ///
    /// Primary Gemini path (Mikasa): <c>InterrogationDialogueService.SuspectLineReady</c>
    /// + <c>QuestionStarted</c>. <c>ElevenLabsVoiceSink</c> bridges those into this bus.
    ///
    /// Alternate / direct hook: call <see cref="PublishReply(SuspectSpeechRequest)"/> when a line is ready.
    /// Event name constant: <see cref="ReplyReadyEventName"/>.
    ///
    /// Voice: <see cref="ElevenLabsVoiceClient"/> subscribes to <see cref="ReplyReady"/> in OnEnable
    /// and publishes <see cref="SpeechStarted"/> / <see cref="SpeechEnded"/> around AudioSource playback.
    /// Animation: <c>SuspectTalkAnimationBridge</c> listens for those playback events.
    /// </summary>
    public static class SuspectDialogueEvents
    {
        /// <summary>Stable name for docs / logging / future message-bus adapters.</summary>
        public const string ReplyReadyEventName = "SuspectDialogue.ReplyReady";

        /// <summary>Stable name for seated-talk start (AudioSource actually began playing).</summary>
        public const string SpeechStartedEventName = "SuspectDialogue.SpeechStarted";

        /// <summary>Stable name for seated-talk end (playback finished, cancelled, or failed after start).</summary>
        public const string SpeechEndedEventName = "SuspectDialogue.SpeechEnded";

        /// <summary>Raised whenever a suspect line should be spoken (Gemini reply or local fallback).</summary>
        public static event Action<SuspectSpeechRequest> ReplyReady;

        /// <summary>Raised when reply TTS audio begins playing (not when the line is merely ready).</summary>
        public static event Action<string> SpeechStarted;

        /// <summary>Raised when reply TTS audio stops (natural end, cancel, or destroy).</summary>
        public static event Action<string> SpeechEnded;

        /// <summary>Publish a full speech request. No-ops on null/empty line.</summary>
        public static void PublishReply(SuspectSpeechRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Line))
                return;

            Action<SuspectSpeechRequest> handlers = ReplyReady;
            if (handlers != null)
                handlers.Invoke(request);
        }

        /// <summary>Convenience publisher matching the Gemini reply shape (line + optional emotion/voice).</summary>
        public static void PublishReply(string suspectId, string line, string emotion = null, string voiceId = null)
        {
            PublishReply(new SuspectSpeechRequest
            {
                SuspectId = suspectId,
                Line = line,
                Emotion = emotion,
                VoiceId = voiceId,
                CancelPrevious = true
            });
        }

        /// <summary>Notify listeners that suspect reply audio started. Empty suspect id is allowed.</summary>
        public static void PublishSpeechStarted(string suspectId)
        {
            Action<string> handlers = SpeechStarted;
            if (handlers != null)
                handlers.Invoke(suspectId ?? string.Empty);
        }

        /// <summary>Notify listeners that suspect reply audio ended. Empty suspect id is allowed.</summary>
        public static void PublishSpeechEnded(string suspectId)
        {
            Action<string> handlers = SpeechEnded;
            if (handlers != null)
                handlers.Invoke(suspectId ?? string.Empty);
        }
    }
}
