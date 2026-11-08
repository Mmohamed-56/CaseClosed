using System;
using UnityEngine;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Per-suspect pool of short pre-baked reaction clips ("Hm.", sigh, scoff).
    /// Assign ElevenLabs-generated clips later; empty slots fall back to runtime placeholders.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SuspectBarkPool",
        menuName = "Case Closed/Suspect Bark Pool")]
    public sealed class SuspectBarkPool : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Profile key (jamie) or full id (suspect_jamie).")]
            public string SuspectKey;

            [Tooltip("Neutral / filler reactions while Gemini loads.")]
            public AudioClip[] Neutral;

            [Tooltip("Nervous / hesitant reactions.")]
            public AudioClip[] Nervous;

            [Tooltip("Defensive pushback reactions.")]
            public AudioClip[] Defensive;

            [Tooltip("Aggressive / irritated reactions.")]
            public AudioClip[] Aggressive;

            [Tooltip("Reactions when confronted with evidence.")]
            public AudioClip[] Confronted;
        }

        [Tooltip("One entry per authored suspect voice.")]
        [SerializeField] private Entry[] suspects = CreateDefaultStubEntries();

        [Tooltip("Used when the suspect has no entry or empty clip arrays.")]
        [SerializeField] private AudioClip[] sharedFallback;

        /// <summary>Readonly view of configured suspect entries (stub pools for jamie/morgan/avery/riley).</summary>
        public Entry[] Suspects { get { return suspects; } }

        /// <summary>Shared fallback clips when a per-suspect slot is empty.</summary>
        public AudioClip[] SharedFallback { get { return sharedFallback; } }

        /// <summary>Picks a clip for the suspect and kind. Returns null when nothing is assigned.</summary>
        public AudioClip Pick(string suspectId, BarkKind kind, System.Random rng = null)
        {
            Entry entry = FindEntry(suspectId);
            AudioClip[] primary = entry != null ? GetClips(entry, kind) : null;
            AudioClip clip = PickFrom(primary, rng);
            if (clip != null) return clip;

            // Soft fallback: try neutral on the same entry, then shared.
            if (kind != BarkKind.Neutral && entry != null)
            {
                clip = PickFrom(entry.Neutral, rng);
                if (clip != null) return clip;
            }

            return PickFrom(sharedFallback, rng);
        }

        public Entry FindEntry(string suspectId)
        {
            if (suspects == null || string.IsNullOrWhiteSpace(suspectId))
                return null;

            string key = suspectId.Trim();
            string bare = StripSuspectPrefix(key);

            for (int i = 0; i < suspects.Length; i++)
            {
                Entry entry = suspects[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SuspectKey))
                    continue;

                string entryKey = entry.SuspectKey.Trim();
                if (string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entryKey, bare, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(StripSuspectPrefix(entryKey), bare, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private static AudioClip[] GetClips(Entry entry, BarkKind kind)
        {
            switch (kind)
            {
                case BarkKind.Nervous:
                    return entry.Nervous;
                case BarkKind.Defensive:
                    return entry.Defensive;
                case BarkKind.Aggressive:
                    return entry.Aggressive;
                case BarkKind.Confronted:
                    return entry.Confronted;
                default:
                    return entry.Neutral;
            }
        }

        private static AudioClip PickFrom(AudioClip[] clips, System.Random rng)
        {
            if (clips == null || clips.Length == 0)
                return null;

            int start = rng != null ? rng.Next(clips.Length) : UnityEngine.Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(start + i) % clips.Length];
                if (clip != null)
                    return clip;
            }

            return null;
        }

        private static string StripSuspectPrefix(string key)
        {
            const string prefix = "suspect_";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return key.Substring(prefix.Length);
            return key;
        }

        private static Entry[] CreateDefaultStubEntries()
        {
            // Empty clip arrays on purpose — drop pre-baked ElevenLabs barks in later.
            return new[]
            {
                new Entry { SuspectKey = "jamie" },
                new Entry { SuspectKey = "morgan" },
                new Entry { SuspectKey = "avery" },
                new Entry { SuspectKey = "riley" }
            };
        }
    }
}
