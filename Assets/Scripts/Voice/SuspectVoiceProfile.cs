using System;

namespace CaseClosed.Voice
{
    /// <summary>
    /// ElevenLabs + presentation profile for one interviewee.
    /// Character generation can read <see cref="Gender"/> without touching TTS.
    /// </summary>
    [Serializable]
    public struct SuspectVoiceProfile
    {
        public SuspectVoiceGender Gender;
        /// <summary>Unity <c>AudioSource.pitch</c> multiplier (1 = default).</summary>
        public float Pitch;
        /// <summary>ElevenLabs premade / case voice id.</summary>
        public string VoiceId;
        /// <summary>Mix-and-match look beside <see cref="Gender"/>. Null until affiliates load.</summary>
        public SuspectLook Look;

        public SuspectVoiceProfile(SuspectVoiceGender gender, float pitch, string voiceId, SuspectLook look = null)
        {
            Gender = gender;
            Pitch = pitch <= 0.01f ? 1f : pitch;
            VoiceId = voiceId ?? string.Empty;
            Look = look;
        }

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(VoiceId); }
        }
    }
}
