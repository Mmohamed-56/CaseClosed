using System.Collections.Generic;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Placeholder ElevenLabs premade voice ids keyed by suspect profile id (jamie, morgan, …)
    /// and by full suspect id (suspect_jamie). Replace with case-authored ids when available.
    /// </summary>
    public static class SuspectVoiceCatalog
    {
        // Public ElevenLabs premade voices — placeholders only until Magdy assigns case voices.
        private const string VoiceRachel = "21m00Tcm4TlvDq8ikWAM";
        private const string VoiceAdam = "pNInz6obpgDQGcFmaJgB";
        private const string VoiceAntoni = "ErXwobaYiN019PkySvjV";
        private const string VoiceJosh = "TxGEqnHWrfWFTfGW9XjX";
        private const string VoiceBella = "EXAVITQu4vr4xnSDxMaL";
        private const string VoiceElli = "MF3mGyEYCl7XYWbV9V6O";

        private static readonly Dictionary<string, string> VoiceBySuspectKey = new Dictionary<string, string>
        {
            { "jamie", VoiceRachel },
            { "suspect_jamie", VoiceRachel },
            { "morgan", VoiceAdam },
            { "suspect_morgan", VoiceAdam },
            { "avery", VoiceAntoni },
            { "suspect_avery", VoiceAntoni },
            { "riley", VoiceBella },
            { "suspect_riley", VoiceBella },
            { "casey", VoiceElli },
            { "suspect_casey", VoiceElli },
            { "drew", VoiceJosh },
            { "suspect_drew", VoiceJosh },
            { "quinn", VoiceAntoni },
            { "suspect_quinn", VoiceAntoni },
            { "sam", VoiceAdam },
            { "suspect_sam", VoiceAdam }
        };

        /// <summary>Default voice used when a suspect has no mapping.</summary>
        public static string DefaultVoiceId { get { return VoiceJosh; } }

        /// <summary>Fallback female-leaning voice for unmapped characters.</summary>
        public static string FallbackFemaleVoiceId { get { return VoiceElli; } }

        /// <summary>Resolves a voice id from an optional override, then catalog, then default.</summary>
        public static string Resolve(string suspectId, string explicitVoiceId = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitVoiceId))
                return explicitVoiceId.Trim();

            if (!string.IsNullOrWhiteSpace(suspectId))
            {
                string key = suspectId.Trim();
                string mapped;
                if (VoiceBySuspectKey.TryGetValue(key, out mapped))
                    return mapped;

                const string prefix = "suspect_";
                if (key.StartsWith(prefix) && VoiceBySuspectKey.TryGetValue(key.Substring(prefix.Length), out mapped))
                    return mapped;
            }

            return DefaultVoiceId;
        }
    }
}
