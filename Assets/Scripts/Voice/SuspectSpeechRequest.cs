using System;

namespace CaseClosed.Voice
{
    /// <summary>
    /// One spoken suspect line ready for TTS. Gemini (or a fallback path) publishes these
    /// through <see cref="SuspectDialogueEvents"/>; <see cref="ElevenLabsVoiceClient"/> consumes them.
    /// </summary>
    [Serializable]
    public sealed class SuspectSpeechRequest
    {
        /// <summary>Stable case suspect id, e.g. suspect_jamie.</summary>
        public string SuspectId;

        /// <summary>Spoken text. Keep short (1–2 sentences) for low latency.</summary>
        public string Line;

        /// <summary>Optional emotion tag from Gemini (calm, defensive, nervous, angry, scared, broken).</summary>
        public string Emotion;

        /// <summary>
        /// Optional ElevenLabs voice id override. When empty, the voice client resolves from
        /// <see cref="CaseClosed.CaseCore.SuspectRecord.ElevenLabsVoiceId"/> or the placeholder catalog.
        /// </summary>
        public string VoiceId;

        /// <summary>When true, cancels any in-flight TTS before starting this line.</summary>
        public bool CancelPrevious = true;
    }
}
