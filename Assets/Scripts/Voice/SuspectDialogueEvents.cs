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
    /// Voice: <see cref="ElevenLabsVoiceClient"/> subscribes to <see cref="ReplyReady"/> in OnEnable.
    /// </summary>
    public static class SuspectDialogueEvents
    {
        /// <summary>Stable name for docs / logging / future message-bus adapters.</summary>
        public const string ReplyReadyEventName = "SuspectDialogue.ReplyReady";

        /// <summary>Raised whenever a suspect line should be spoken (Gemini reply or local fallback).</summary>
        public static event Action<SuspectSpeechRequest> ReplyReady;

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
    }
}
