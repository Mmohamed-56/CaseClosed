using System;
using System.Collections.Generic;
using CaseClosed.CaseCore;
using UnityEngine;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Instant reaction barks on question submit — plays a short pre-baked (or placeholder) clip
    /// immediately while Gemini / ElevenLabs load. Non-blocking; never waits on network.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarkPlayer : MonoBehaviour
    {
        private const int PlaceholderSampleRate = 22050;
        private const float PlaceholderDurationSeconds = 0.22f;

        [Header("Pool")]
        [Tooltip("Optional ScriptableObject pool. When empty, runtime placeholders are used.")]
        [SerializeField] private SuspectBarkPool barkPool;

        [Header("Playback")]
        [Tooltip("Dedicated bark AudioSource (separate from ElevenLabs TTS). Created when missing.")]
        [SerializeField] private AudioSource audioSource;

        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

        [Tooltip("Generate tiny procedural clips when the pool has no AudioClips assigned.")]
        [SerializeField] private bool usePlaceholderClips = true;

        [Tooltip("Stop the bark when a full spoken line is ready (TTS / mock reply).")]
        [SerializeField] private bool stopWhenReplyReady = true;

        [Header("Wiring")]
        [Tooltip("InterrogationController that raises ValidQuestionAsked. Auto-found when empty.")]
        [SerializeField] private InterrogationController interrogationController;

        [SerializeField] private bool autoWireInterrogationController = true;

        private readonly Dictionary<string, AudioClip> placeholderByKey = new Dictionary<string, AudioClip>(8);
        private readonly System.Random rng = new System.Random();
        private bool wired;

        /// <summary>True when a bark AudioSource is ready to play.</summary>
        public bool IsReady
        {
            get
            {
                EnsureAudioSource();
                return audioSource != null;
            }
        }

        /// <summary>True while a bark clip is currently audible.</summary>
        public bool IsPlaying
        {
            get { return audioSource != null && audioSource.isPlaying; }
        }

        private void Awake()
        {
            EnsureAudioSource();
        }

        private void OnEnable()
        {
            TryWireInterrogation();
            if (stopWhenReplyReady)
                SuspectDialogueEvents.ReplyReady += HandleReplyReady;
        }

        private void OnDisable()
        {
            UnwireInterrogation();
            if (stopWhenReplyReady)
                SuspectDialogueEvents.ReplyReady -= HandleReplyReady;
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, AudioClip> pair in placeholderByKey)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }
            placeholderByKey.Clear();
        }

        /// <summary>Optional explicit wiring from prototype / dialogue bootstrap.</summary>
        public void Configure(InterrogationController controller, SuspectBarkPool pool = null)
        {
            UnwireInterrogation();
            interrogationController = controller;
            if (pool != null)
                barkPool = pool;
            TryWireInterrogation();
        }

        /// <summary>
        /// Plays an instant bark for the suspect. Safe to call from UI / AskQuestion paths; never blocks.
        /// </summary>
        public void PlayForSuspect(string suspectId, BarkKind kind = BarkKind.Neutral)
        {
            EnsureAudioSource();
            if (audioSource == null)
                return;

            AudioClip clip = null;
            if (barkPool != null)
                clip = barkPool.Pick(suspectId, kind, rng);

            if (clip == null && usePlaceholderClips)
                clip = GetOrCreatePlaceholder(suspectId, kind);

            if (clip == null)
                return;

            // PlayOneShot is fire-and-forget — does not block the frame or any Gemini/TTS work.
            audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>Stops any audible bark immediately (e.g. when real TTS starts).</summary>
        public void StopBark()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        /// <summary>Maps interrogation outcome hints into a bark flavor.</summary>
        public static BarkKind InferKind(InterrogationResult result, bool aggressive = false, bool confronted = false)
        {
            if (confronted)
                return BarkKind.Confronted;
            if (aggressive)
                return BarkKind.Aggressive;
            if (result == null)
                return BarkKind.Neutral;

            switch (result.Tier)
            {
                case InterrogationStressTier.Nervous:
                    return BarkKind.Nervous;
                case InterrogationStressTier.Cracking:
                case InterrogationStressTier.Broken:
                    return BarkKind.Defensive;
                default:
                    return BarkKind.Neutral;
            }
        }

        private void HandleValidQuestionAsked(
            string suspectId,
            InterrogationResult result,
            bool aggressive,
            bool confronted)
        {
            // Instant path: controller already accepted the question; Gemini/TTS may still be loading.
            BarkKind kind = InferKind(result, aggressive, confronted);
            PlayForSuspect(suspectId, kind);
        }

        private void HandleReplyReady(SuspectSpeechRequest request)
        {
            if (request == null)
                return;
            StopBark();
        }

        private void TryWireInterrogation()
        {
            if (!autoWireInterrogationController)
                return;

            if (interrogationController == null)
            {
                interrogationController = GetComponent<InterrogationController>();
                if (interrogationController == null)
                    interrogationController = GetComponentInParent<InterrogationController>();
                if (interrogationController == null)
                    interrogationController = FindAnyObjectByType<InterrogationController>();
            }

            if (interrogationController == null)
                return;

            if (wired)
                return;

            interrogationController.ValidQuestionAsked += HandleValidQuestionAsked;
            wired = true;
        }

        private void UnwireInterrogation()
        {
            if (!wired || interrogationController == null)
                return;

            interrogationController.ValidQuestionAsked -= HandleValidQuestionAsked;
            wired = false;
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null)
                return;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 64;
        }

        private AudioClip GetOrCreatePlaceholder(string suspectId, BarkKind kind)
        {
            string key = (suspectId ?? "default") + ":" + kind;
            AudioClip existing;
            if (placeholderByKey.TryGetValue(key, out existing) && existing != null)
                return existing;

            AudioClip generated = BuildPlaceholderClip(suspectId, kind);
            placeholderByKey[key] = generated;
            return generated;
        }

        /// <summary>
        /// Tiny procedural "vocal" blip so Play Mode has audible feedback before real bark assets exist.
        /// </summary>
        private static AudioClip BuildPlaceholderClip(string suspectId, BarkKind kind)
        {
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(PlaceholderSampleRate * PlaceholderDurationSeconds));
            float[] samples = new float[sampleCount];

            int hash = StableHash(suspectId ?? "suspect");
            float baseHz = 160f + (hash % 90);
            switch (kind)
            {
                case BarkKind.Nervous:
                    baseHz *= 1.12f;
                    break;
                case BarkKind.Defensive:
                    baseHz *= 0.92f;
                    break;
                case BarkKind.Aggressive:
                    baseHz *= 0.85f;
                    break;
                case BarkKind.Confronted:
                    baseHz *= 1.05f;
                    break;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)PlaceholderSampleRate;
                float envelope = Mathf.Sin(Mathf.PI * (i / (float)(sampleCount - 1)));
                envelope = Mathf.Clamp01(envelope);
                // Soft two-harmonic blip — reads as a short "hm" / breath, not a beep.
                float wave = Mathf.Sin(2f * Mathf.PI * baseHz * t) * 0.55f
                             + Mathf.Sin(2f * Mathf.PI * (baseHz * 1.5f) * t) * 0.2f;
                samples[i] = wave * envelope * 0.35f;
            }

            string clipName = "BarkPlaceholder_" + (suspectId ?? "default") + "_" + kind;
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, PlaceholderSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                if (hash == int.MinValue) hash = 0;
                return Math.Abs(hash);
            }
        }
    }
}
