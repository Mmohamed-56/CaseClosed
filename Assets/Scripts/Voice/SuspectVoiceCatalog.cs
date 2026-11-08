using System.Collections.Generic;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Per-interviewee ElevenLabs voice profiles (gender / pitch / voiceId).
    /// Used by Speak now; <see cref="TryGetProfile"/> / <see cref="GetGender"/> are for character generation later.
    /// Premade voice ids are placeholders until Magdy assigns case voices.
    /// </summary>
    public static class SuspectVoiceCatalog
    {
        // Public ElevenLabs premade voices — gender pools for male / female / neutral.
        private const string VoiceRachel = "21m00Tcm4TlvDq8ikWAM"; // female
        private const string VoiceBella = "EXAVITQu4vr4xnSDxMaL";  // female
        private const string VoiceElli = "MF3mGyEYCl7XYWbV9V6O";   // female
        private const string VoiceAdam = "pNInz6obpgDQGcFmaJgB";   // male
        private const string VoiceJosh = "TxGEqnHWrfWFTfGW9XjX";   // male
        private const string VoiceAntoni = "ErXwobaYiN019PkySvjV"; // neutral / mid

        /// <summary>Shared male pool voice (default for unmapped Male).</summary>
        public static string MaleVoiceId { get { return VoiceJosh; } }

        /// <summary>Shared female pool voice (default for unmapped Female).</summary>
        public static string FemaleVoiceId { get { return VoiceElli; } }

        /// <summary>Shared neutral pool voice (default for unmapped Neutral).</summary>
        public static string NeutralVoiceId { get { return VoiceAntoni; } }

        /// <summary>Default profile when a suspect has no mapping.</summary>
        public static SuspectVoiceProfile DefaultProfile
        {
            get { return new SuspectVoiceProfile(SuspectVoiceGender.Male, 1f, MaleVoiceId); }
        }

        /// <summary>Default voice used when a suspect has no mapping.</summary>
        public static string DefaultVoiceId { get { return MaleVoiceId; } }

        /// <summary>Fallback female-leaning voice for unmapped characters.</summary>
        public static string FallbackFemaleVoiceId { get { return FemaleVoiceId; } }

        private static readonly Dictionary<string, SuspectVoiceProfile> ProfileBySuspectKey =
            new Dictionary<string, SuspectVoiceProfile>
            {
                // Core demo pool
                { "jamie", new SuspectVoiceProfile(SuspectVoiceGender.Female, 1.05f, VoiceRachel) },
                { "morgan", new SuspectVoiceProfile(SuspectVoiceGender.Male, 0.97f, VoiceAdam) },
                { "avery", new SuspectVoiceProfile(SuspectVoiceGender.Neutral, 1.00f, VoiceAntoni) },
                { "riley", new SuspectVoiceProfile(SuspectVoiceGender.Female, 1.04f, VoiceBella) },
                { "casey", new SuspectVoiceProfile(SuspectVoiceGender.Female, 1.07f, VoiceElli) },
                { "drew", new SuspectVoiceProfile(SuspectVoiceGender.Male, 0.95f, VoiceJosh) },
                { "quinn", new SuspectVoiceProfile(SuspectVoiceGender.Neutral, 0.98f, VoiceAntoni) },
                { "sam", new SuspectVoiceProfile(SuspectVoiceGender.Male, 1.00f, VoiceAdam) },
                // Expanded interviewee pool (affiliates.json)
                { "taylor", new SuspectVoiceProfile(SuspectVoiceGender.Female, 1.02f, VoiceRachel) },
                { "jordan", new SuspectVoiceProfile(SuspectVoiceGender.Male, 0.99f, VoiceJosh) },
                { "harper", new SuspectVoiceProfile(SuspectVoiceGender.Female, 1.06f, VoiceBella) },
                { "reese", new SuspectVoiceProfile(SuspectVoiceGender.Neutral, 1.02f, VoiceAntoni) }
            };

        /// <summary>
        /// Resolves the full voice profile for a suspect id (<c>jamie</c> or <c>suspect_jamie</c>).
        /// Character generation should prefer this over raw voice ids.
        /// <see cref="SuspectVoiceProfile.Look"/> is attached from suspect data when present.
        /// </summary>
        public static SuspectVoiceProfile GetProfile(string suspectId)
        {
            SuspectVoiceProfile profile;
            if (TryGetProfile(suspectId, out profile))
                return profile;
            return AttachLook(suspectId, DefaultProfile);
        }

        /// <summary>True when the catalog has an authored profile for this suspect key.</summary>
        public static bool TryGetProfile(string suspectId, out SuspectVoiceProfile profile)
        {
            profile = default(SuspectVoiceProfile);
            string key = NormalizeKey(suspectId);
            if (string.IsNullOrEmpty(key))
                return false;

            if (!ProfileBySuspectKey.TryGetValue(key, out profile))
                return false;

            profile = AttachLook(key, profile);
            return true;
        }

        /// <summary>
        /// Copies the affiliate look onto the voice profile so gender and slots travel together.
        /// </summary>
        private static SuspectVoiceProfile AttachLook(string suspectId, SuspectVoiceProfile profile)
        {
            if (profile.Look != null && !string.IsNullOrWhiteSpace(profile.Look.body))
                return profile;

            SuspectLook look = CaseClosed.CaseCore.CampusAffiliateCatalog.TryGetLook(suspectId);
            if (look != null)
                profile.Look = look;
            return profile;
        }

        /// <summary>Gender for character generation. Defaults to Male when unmapped.</summary>
        public static SuspectVoiceGender GetGender(string suspectId)
        {
            return GetProfile(suspectId).Gender;
        }

        /// <summary>Picks the shared male / female / neutral pool voice id.</summary>
        public static string VoiceIdForGender(SuspectVoiceGender gender)
        {
            switch (gender)
            {
                case SuspectVoiceGender.Female:
                    return FemaleVoiceId;
                case SuspectVoiceGender.Neutral:
                    return NeutralVoiceId;
                default:
                    return MaleVoiceId;
            }
        }

        /// <summary>Resolves a voice id from an optional override, then catalog profile, then default.</summary>
        public static string Resolve(string suspectId, string explicitVoiceId = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitVoiceId))
                return explicitVoiceId.Trim();

            SuspectVoiceProfile profile;
            if (TryGetProfile(suspectId, out profile) && profile.IsValid)
                return profile.VoiceId;

            return DefaultVoiceId;
        }

        /// <summary>
        /// Resolves voice id + playback pitch for Speak. Explicit voice id overrides the catalog
        /// voice but still uses catalog pitch when the suspect is mapped.
        /// </summary>
        public static SuspectVoiceProfile ResolveProfile(string suspectId, string explicitVoiceId = null)
        {
            SuspectVoiceProfile profile = GetProfile(suspectId);
            if (!string.IsNullOrWhiteSpace(explicitVoiceId))
            {
                profile.VoiceId = explicitVoiceId.Trim();
            }
            else if (!profile.IsValid)
            {
                profile.VoiceId = DefaultVoiceId;
                profile.Pitch = 1f;
            }

            if (profile.Pitch <= 0.01f)
                profile.Pitch = 1f;

            return profile;
        }

        private static string NormalizeKey(string suspectId)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
                return string.Empty;

            string key = suspectId.Trim().ToLowerInvariant();
            const string prefix = "suspect_";
            if (key.StartsWith(prefix))
                key = key.Substring(prefix.Length);
            return key;
        }
    }
}
