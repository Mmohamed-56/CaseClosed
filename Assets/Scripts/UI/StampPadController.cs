using System;
using System.Collections;
using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Desk stamp pad UI hooks for HOLD / RELEASE. Calls <see cref="CaseShiftController.DecideCurrentSuspect"/>
    /// after optional Bezi animation / SFX delay. Does not own charge / headline flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StampPadController : MonoBehaviour
    {
        [Header("Case Connection")]
        [Tooltip("Active shift controller. Auto-found when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Stamp UI Hooks (Bezi)")]
        [Tooltip("HOLD stamp button (clipboard / pad).")]
        [SerializeField] private Button holdButton;

        [Tooltip("RELEASE stamp button (clipboard / pad).")]
        [SerializeField] private Button releaseButton;

        [Tooltip("Optional root enabled only while a stamp decision is legal.")]
        [SerializeField] private GameObject stampPadRoot;

        [Tooltip("Optional animator on the stamp prop. Triggers fire before the decision applies.")]
        [SerializeField] private Animator stampAnimator;

        [Tooltip("Animator trigger for HOLD.")]
        [SerializeField] private string holdTrigger = "StampHold";

        [Tooltip("Animator trigger for RELEASE.")]
        [SerializeField] private string releaseTrigger = "StampRelease";

        [Tooltip("Seconds to wait after the stamp animation before writing the CaseCore decision.")]
        [SerializeField, Min(0f)] private float applyDelaySeconds = 0.35f;

        [Header("Audio Hooks (Bezi)")]
        [SerializeField] private AudioSource stampAudioSource;
        [SerializeField] private AudioClip holdClip;
        [SerializeField] private AudioClip releaseClip;

        private Coroutine pendingStamp;
        private bool isStamping;

        /// <summary>Raised when the player requests HOLD or RELEASE (before animation delay).</summary>
        public event Action<SuspectDecision> StampRequested;

        /// <summary>Raised after CaseCore accepts the decision.</summary>
        public event Action<SuspectDecision> StampApplied;

        /// <summary>True while Investigating and no stamp animation is in flight.</summary>
        public bool CanStamp
        {
            get
            {
                return !isStamping &&
                       shiftController != null &&
                       shiftController.ActiveSession != null &&
                       shiftController.Phase == CaseShiftPhase.Investigating &&
                       shiftController.CurrentSuspect != null;
            }
        }

        /// <summary>True when HOLD and RELEASE buttons are assigned (Inspector or <see cref="SetStampSceneHooks"/>).</summary>
        public bool HasStampHooks { get { return holdButton != null && releaseButton != null; } }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();
        }

        private void OnEnable()
        {
            WireButtons(true);
            if (shiftController != null)
                shiftController.PhaseChanged += HandlePhaseChanged;
            RefreshInteractable();
        }

        private void OnDisable()
        {
            WireButtons(false);
            if (shiftController != null)
                shiftController.PhaseChanged -= HandlePhaseChanged;
            CancelPendingStamp();
        }

        /// <summary>Bezi / Magdy can assign stamp buttons and animator at runtime.</summary>
        public void SetStampSceneHooks(Button hold, Button release, Animator animator = null, GameObject padRoot = null)
        {
            WireButtons(false);
            holdButton = hold;
            releaseButton = release;
            if (animator != null) stampAnimator = animator;
            if (padRoot != null) stampPadRoot = padRoot;
            if (isActiveAndEnabled)
            {
                WireButtons(true);
                RefreshInteractable();
            }
        }

        /// <summary>Assigns the shift controller used by this stamp pad.</summary>
        public void SetShiftController(CaseShiftController controller)
        {
            if (shiftController != null)
                shiftController.PhaseChanged -= HandlePhaseChanged;
            shiftController = controller;
            if (isActiveAndEnabled && shiftController != null)
                shiftController.PhaseChanged += HandlePhaseChanged;
            RefreshInteractable();
        }

        /// <summary>UI / input hook: stamp HOLD on the current suspect.</summary>
        public void StampHold()
        {
            RequestStamp(SuspectDecision.Hold);
        }

        /// <summary>UI / input hook: stamp RELEASE on the current suspect.</summary>
        public void StampRelease()
        {
            RequestStamp(SuspectDecision.Release);
        }

        /// <summary>Shared entry for HOLD / RELEASE requests from buttons or other systems.</summary>
        public void RequestStamp(SuspectDecision decision)
        {
            if (decision != SuspectDecision.Hold && decision != SuspectDecision.Release)
            {
                Debug.LogWarning("StampPadController: decision must be Hold or Release.", this);
                return;
            }

            if (!CanStamp)
            {
                Debug.LogWarning("StampPadController: stamp ignored (need Investigating phase, current suspect, idle pad).", this);
                return;
            }

            CancelPendingStamp();
            pendingStamp = StartCoroutine(StampRoutine(decision));
        }

        private IEnumerator StampRoutine(SuspectDecision decision)
        {
            isStamping = true;
            RefreshInteractable();
            StampRequested?.Invoke(decision);

            PlayStampFeedback(decision);

            if (applyDelaySeconds > 0f)
                yield return new WaitForSeconds(applyDelaySeconds);

            try
            {
                shiftController.DecideCurrentSuspect(decision);
                StampApplied?.Invoke(decision);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("StampPadController: CaseCore rejected stamp — " + ex.Message, this);
            }

            isStamping = false;
            pendingStamp = null;
            RefreshInteractable();
        }

        private void PlayStampFeedback(SuspectDecision decision)
        {
            if (stampAnimator != null)
            {
                string trigger = decision == SuspectDecision.Hold ? holdTrigger : releaseTrigger;
                if (!string.IsNullOrWhiteSpace(trigger))
                    stampAnimator.SetTrigger(trigger);
            }

            if (stampAudioSource != null)
            {
                AudioClip clip = decision == SuspectDecision.Hold ? holdClip : releaseClip;
                if (clip != null)
                    stampAudioSource.PlayOneShot(clip);
            }
        }

        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            if (phase != CaseShiftPhase.Investigating)
                CancelPendingStamp();
            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            bool can = CanStamp;
            if (stampPadRoot != null)
                stampPadRoot.SetActive(shiftController != null &&
                                       shiftController.Phase == CaseShiftPhase.Investigating);

            if (holdButton != null)
                holdButton.interactable = can;
            if (releaseButton != null)
                releaseButton.interactable = can;
        }

        private void WireButtons(bool subscribe)
        {
            if (holdButton != null)
            {
                holdButton.onClick.RemoveListener(StampHold);
                if (subscribe) holdButton.onClick.AddListener(StampHold);
            }

            if (releaseButton != null)
            {
                releaseButton.onClick.RemoveListener(StampRelease);
                if (subscribe) releaseButton.onClick.AddListener(StampRelease);
            }
        }

        private void CancelPendingStamp()
        {
            if (pendingStamp != null)
            {
                StopCoroutine(pendingStamp);
                pendingStamp = null;
            }
            isStamping = false;
        }
    }
}
