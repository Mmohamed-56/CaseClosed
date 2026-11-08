using System;
using System.Reflection;
using CaseClosed.CaseCore;
using CaseClosed.Characters;
using CaseClosed.UI;
using UnityEngine;

namespace CaseClosed.Audio
{
    /// <summary>
    /// Tiny prototype SFX hub: door, stamp, UI click, office ambience loop.
    /// Auto-loads clips from Resources/SFX and wires existing gameplay hooks when present.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class SfxPlayer : MonoBehaviour
    {
        public const string ResourcesFolder = "SFX";

        [Header("Clips (auto-load from Resources/SFX when empty)")]
        [SerializeField] private AudioClip doorClip;
        [SerializeField] private AudioClip stampClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip phoneRingClip;

        [Header("Playback")]
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField, Range(0f, 1f)] private float oneShotVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.28f;
        [SerializeField] private bool playAmbienceOnEnable = true;

        [Header("Auto-wire")]
        [SerializeField] private bool autoWireHooks = true;
        [SerializeField] private SuspectEnterController enterController;
        [SerializeField] private StampPadController stampPad;
        [SerializeField] private EvidenceBoardController evidenceBoard;
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private InterrogationController interrogationController;

        private static SfxPlayer instance;
        private bool wired;
        private bool stampPadOwnsPlayback;

        /// <summary>Active scene player, if any.</summary>
        public static SfxPlayer Instance { get { return instance; } }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("SfxPlayer: duplicate instance — keeping the first.", this);
                enabled = false;
                return;
            }

            instance = this;
            EnsureSources();
            EnsureClips();
            SeedStampPadClips();
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
            EnsureSources();
            EnsureClips();
            if (autoWireHooks) WireHooks(true);
            if (playAmbienceOnEnable) PlayAmbience();
        }

        private void OnDisable()
        {
            WireHooks(false);
            StopAmbience();
            if (instance == this) instance = null;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Bezi / UI button hook — wood door open.</summary>
        public void PlayDoor()
        {
            PlayOneShot(doorClip);
        }

        /// <summary>Bezi / stamp pad hook — rubber stamp thud.</summary>
        public void PlayStamp()
        {
            PlayOneShot(stampClip);
        }

        /// <summary>Bezi / UI / keyboard click hook.</summary>
        public void PlayUiClick()
        {
            PlayOneShot(uiClickClip);
        }

        /// <summary>Optional phone ring (Epidemic drop Magdy included).</summary>
        public void PlayPhoneRing()
        {
            PlayOneShot(phoneRingClip);
        }

        /// <summary>Starts the short office ambience loop (menu / desk bed).</summary>
        public void PlayAmbience()
        {
            EnsureSources();
            EnsureClips();
            if (ambienceSource == null || ambienceClip == null) return;
            if (ambienceSource.clip != ambienceClip)
                ambienceSource.clip = ambienceClip;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.loop = true;
            if (!ambienceSource.isPlaying)
                ambienceSource.Play();
        }

        /// <summary>Stops the ambience loop.</summary>
        public void StopAmbience()
        {
            if (ambienceSource != null && ambienceSource.isPlaying)
                ambienceSource.Stop();
        }

        /// <summary>Runtime assignment helper for Bezi / Magdy.</summary>
        public void SetClips(AudioClip door, AudioClip stamp, AudioClip uiClick, AudioClip ambience, AudioClip phone = null)
        {
            if (door != null) doorClip = door;
            if (stamp != null) stampClip = stamp;
            if (uiClick != null) uiClickClip = uiClick;
            if (ambience != null) ambienceClip = ambience;
            if (phone != null) phoneRingClip = phone;
            SeedStampPadClips();
        }

        private void PlayOneShot(AudioClip clip)
        {
            EnsureSources();
            if (oneShotSource == null || clip == null) return;
            oneShotSource.PlayOneShot(clip, oneShotVolume);
        }

        private void EnsureClips()
        {
            if (doorClip == null) doorClip = LoadClip("door_open");
            if (stampClip == null) stampClip = LoadClip("stamp");
            if (uiClickClip == null) uiClickClip = LoadClip("ui_click");
            if (ambienceClip == null) ambienceClip = LoadClip("office_ambience");
            if (phoneRingClip == null) phoneRingClip = LoadClip("phone_ring");
        }

        private static AudioClip LoadClip(string resourceName)
        {
            return Resources.Load<AudioClip>(ResourcesFolder + "/" + resourceName);
        }

        private void EnsureSources()
        {
            if (oneShotSource == null)
            {
                oneShotSource = GetComponent<AudioSource>();
                if (oneShotSource == null)
                    oneShotSource = gameObject.AddComponent<AudioSource>();
                oneShotSource.playOnAwake = false;
                oneShotSource.spatialBlend = 0f;
                oneShotSource.loop = false;
            }

            if (ambienceSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null && sources[i] != oneShotSource)
                    {
                        ambienceSource = sources[i];
                        break;
                    }
                }
            }

            if (ambienceSource == null)
                ambienceSource = gameObject.AddComponent<AudioSource>();

            ambienceSource.playOnAwake = false;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.loop = true;
            ambienceSource.volume = ambienceVolume;
        }

        private void SeedStampPadClips()
        {
            if (stampClip == null) return;
            if (stampPad == null) stampPad = FindAnyObjectByType<StampPadController>();
            if (stampPad == null)
            {
                stampPadOwnsPlayback = false;
                return;
            }

            AssignPrivateField(stampPad, "holdClip", stampClip);
            AssignPrivateField(stampPad, "releaseClip", stampClip);

            AudioSource existing = GetPrivateField<AudioSource>(stampPad, "stampAudioSource");
            if (existing == null)
            {
                EnsureSources();
                AssignPrivateField(stampPad, "stampAudioSource", oneShotSource);
            }

            stampPadOwnsPlayback = GetPrivateField<AudioSource>(stampPad, "stampAudioSource") != null &&
                                   GetPrivateField<AudioClip>(stampPad, "holdClip") != null;
        }

        private static void AssignPrivateField(object target, string fieldName, object value)
        {
            if (target == null || value == null) return;
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return;
            if (field.GetValue(target) != null) return;
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            if (target == null) return null;
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) as T : null;
        }

        private void WireHooks(bool subscribe)
        {
            if (subscribe && wired) return;
            if (!subscribe && !wired) return;

            if (enterController == null) enterController = FindAnyObjectByType<SuspectEnterController>();
            if (stampPad == null) stampPad = FindAnyObjectByType<StampPadController>();
            if (evidenceBoard == null) evidenceBoard = FindAnyObjectByType<EvidenceBoardController>();
            if (shiftController == null) shiftController = FindAnyObjectByType<CaseShiftController>();
            if (interrogationController == null) interrogationController = FindAnyObjectByType<InterrogationController>();

            if (enterController != null)
            {
                enterController.StageChanged -= HandleEnterStageChanged;
                if (subscribe) enterController.StageChanged += HandleEnterStageChanged;
            }

            if (stampPad != null)
            {
                stampPad.StampRequested -= HandleStampRequested;
                if (subscribe) stampPad.StampRequested += HandleStampRequested;
            }

            if (evidenceBoard != null)
            {
                evidenceBoard.EvidenceSelectionChanged -= HandleEvidenceSelectionChanged;
                evidenceBoard.EvidenceAddedVisual -= HandleEvidenceAdded;
                if (subscribe)
                {
                    evidenceBoard.EvidenceSelectionChanged += HandleEvidenceSelectionChanged;
                    evidenceBoard.EvidenceAddedVisual += HandleEvidenceAdded;
                }
            }

            if (shiftController != null)
            {
                shiftController.SuspectStamped -= HandleSuspectStamped;
                shiftController.PhaseChanged -= HandlePhaseChanged;
                if (subscribe)
                {
                    shiftController.SuspectStamped += HandleSuspectStamped;
                    shiftController.PhaseChanged += HandlePhaseChanged;
                }
            }

            if (interrogationController != null)
            {
                interrogationController.ValidQuestionAsked -= HandleValidQuestionAsked;
                if (subscribe) interrogationController.ValidQuestionAsked += HandleValidQuestionAsked;
            }

            if (subscribe) SeedStampPadClips();
            wired = subscribe;
        }

        private void HandleEnterStageChanged(SuspectEnterStage stage)
        {
            if (stage == SuspectEnterStage.Entering)
                PlayDoor();
        }

        private void HandleStampRequested(SuspectDecision decision)
        {
            if (stampPadOwnsPlayback) return;
            PlayStamp();
        }

        private void HandleSuspectStamped(SuspectStampEvent stampEvent)
        {
            // Prototype GUI / CaseShift stamps when StampPadController is absent.
            if (stampPad != null) return;
            PlayStamp();
        }

        private void HandleEvidenceSelectionChanged(string evidenceId)
        {
            PlayUiClick();
        }

        private void HandleEvidenceAdded(EvidenceRecord record)
        {
            PlayUiClick();
        }

        private void HandleValidQuestionAsked(
            string question,
            InterrogationResult result,
            bool presentedEvidence,
            bool accused)
        {
            PlayUiClick();
        }

        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            if (phase == CaseShiftPhase.Briefing || phase == CaseShiftPhase.Investigating)
                PlayAmbience();
            else if (phase == CaseShiftPhase.Resolved || phase == CaseShiftPhase.Inactive)
                StopAmbience();
        }

        /// <summary>
        /// Spawns an SfxPlayer after scene load when a case loop exists and Magdy / Bezi
        /// never placed one. No-op for empty tool scenes.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapInstance()
        {
            if (instance != null) return;
            if (FindAnyObjectByType<SfxPlayer>() != null) return;

            bool hasCaseLoop =
                FindAnyObjectByType<CaseShiftController>() != null ||
                FindAnyObjectByType<SuspectEnterController>() != null ||
                FindAnyObjectByType<StampPadController>() != null ||
                FindAnyObjectByType<EvidenceBoardController>() != null;

            if (!hasCaseLoop)
            {
                Type proto = FindType("CaseClosed.Gameplay.CaseGameplayPrototype");
                if (proto == null || FindAnyObjectByType(proto) == null)
                    return;
            }

            GameObject go = new GameObject("SfxPlayer");
            go.AddComponent<SfxPlayer>();
        }

        private static Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName + ", Assembly-CSharp");
            if (direct != null) return direct;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
