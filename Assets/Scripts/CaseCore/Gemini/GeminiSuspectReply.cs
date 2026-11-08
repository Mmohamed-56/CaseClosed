using System;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>Parsed Gemini JSON payload for one spoken suspect line.</summary>
    [Serializable]
    public sealed class GeminiSuspectReply
    {
        public string line;
        public string emotion;
        public bool slip;

        public static GeminiSuspectReply CreateFallback(string spokenLine, string emotionTag, bool didSlip)
        {
            return new GeminiSuspectReply
            {
                line = string.IsNullOrWhiteSpace(spokenLine) ? "..." : spokenLine.Trim(),
                emotion = NormalizeEmotion(emotionTag),
                slip = didSlip
            };
        }

        public static string NormalizeEmotion(string emotionTag)
        {
            if (string.IsNullOrWhiteSpace(emotionTag)) return "calm";
            switch (emotionTag.Trim().ToLowerInvariant())
            {
                case "calm":
                case "defensive":
                case "nervous":
                case "angry":
                case "scared":
                case "broken":
                    return emotionTag.Trim().ToLowerInvariant();
                default:
                    return "defensive";
            }
        }
    }

    /// <summary>UI-facing result after stress rules and scenario / scripted (optional Gemini) performance.</summary>
    [Serializable]
    public sealed class SuspectDialogueTurn
    {
        public bool IsValid;
        public string SuspectId;
        public string SuspectName;
        public string PlayerQuestion;
        public string EvidenceId;
        public InterrogationResult StressResult;
        public GeminiSuspectReply Reply;
        public bool UsedMock;
        /// <summary>UI tag: "suspect", "scripted", or legacy "gemini" when the opt-in flag is on.</summary>
        public string ReplySource;
        public string Error;
    }

    /// <summary>Text payload ElevenLabs (or any voice agent) should speak. No TTS here.</summary>
    [Serializable]
    public sealed class SuspectVoiceLine
    {
        public string SuspectId;
        public string SuspectName;
        public string Text;
        public string Emotion;
        public bool Slip;
        public bool UsedMock;
        /// <summary>Optional ElevenLabs voice id from <see cref="SuspectRecord.ElevenLabsVoiceId"/>.</summary>
        public string VoiceId;
    }
}
