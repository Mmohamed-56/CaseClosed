using System;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Starts a seeded case on Play and exposes Bezi-friendly stamp / charge / headline entry points.
    /// Wire clipboard HOLD/RELEASE buttons to <see cref="StampHold"/> / <see cref="StampRelease"/>,
    /// charge buttons to <see cref="ChargeSuspectById"/> / <see cref="ChargeNobody"/>,
    /// and subscribe UI to the controller's flow UnityEvents.
    /// </summary>
    public sealed class CaseShiftBootstrapper : MonoBehaviour
    {
        [Header("Prototype Case")]
        [Tooltip("A stable seed reproduces the same generated case, suspects, and culprit.")]
        [SerializeField] private int caseSeed = 42;

        [Tooltip("Number of suspects for the shift. The case generator supports two to four.")]
        [SerializeField, Range(2, 4)] private int suspectCount = 2;

        [Tooltip("Start the generated case automatically when this object becomes active.")]
        [SerializeField] private bool startAutomatically = true;

        [Tooltip("When enabled, disables the shift clock (demo / judging mode).")]
        [SerializeField] private bool demoMode = true;

        [Header("Shift Controller")]
        [Tooltip("Controller to start. Automatically found or added on this object when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Debug")]
        [Tooltip("Logs stamp / next / charge / headline events to the Console for Bezi wiring checks.")]
        [SerializeField] private bool logFlowEvents = true;

        /// <summary>The live shift controller used by this bootstrapper.</summary>
        public CaseShiftController ShiftController { get { return shiftController; } }

        /// <summary>Configured case seed.</summary>
        public int CaseSeed { get { return caseSeed; } }

        /// <summary>Configured suspect count.</summary>
        public int SuspectCount { get { return suspectCount; } }

        private void Awake()
        {
            if (shiftController == null) shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null) shiftController = gameObject.AddComponent<CaseShiftController>();
        }

        private void OnEnable()
        {
            if (shiftController == null) return;
            if (!logFlowEvents) return;

            shiftController.SuspectStamped += HandleSuspectStamped;
            shiftController.NextSuspectReady += HandleNextSuspect;
            shiftController.ChargeReady += HandleChargeReady;
            shiftController.MorningHeadlineReady += HandleMorningHeadline;
        }

        private void OnDisable()
        {
            if (shiftController == null) return;

            shiftController.SuspectStamped -= HandleSuspectStamped;
            shiftController.NextSuspectReady -= HandleNextSuspect;
            shiftController.ChargeReady -= HandleChargeReady;
            shiftController.MorningHeadlineReady -= HandleMorningHeadline;
        }

        private void Start()
        {
            if (startAutomatically) StartGeneratedShift();
        }

        /// <summary>Starts the configured seeded case using the configured suspect count.</summary>
        public void StartGeneratedShift()
        {
            if (shiftController == null)
                throw new InvalidOperationException("Assign a CaseShiftController or add one to the same GameObject.");

            if (demoMode)
                shiftController.ShiftClockEnabled = false;

            shiftController.BeginGeneratedShift(caseSeed, suspectCount);
        }

        /// <summary>Bezi / stamp pad: HOLD the current suspect, then advance.</summary>
        public void StampHold()
        {
            EnsureController().StampHold();
        }

        /// <summary>Bezi / stamp pad: RELEASE the current suspect, then advance.</summary>
        public void StampRelease()
        {
            EnsureController().StampRelease();
        }

        /// <summary>Bezi / interview UI: begin the current suspect interview.</summary>
        public void BeginCurrentInterview()
        {
            EnsureController().BeginCurrentInterview();
        }

        /// <summary>Bezi / charge UI: charge a held suspect by id, then show the morning headline.</summary>
        public void ChargeSuspectById(string suspectId)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
            {
                ChargeNobody();
                return;
            }

            EnsureController().ResolveCharge(suspectId);
        }

        /// <summary>Bezi / charge UI: charge nobody and show the morning headline.</summary>
        public void ChargeNobody()
        {
            EnsureController().ChargeNobody();
        }

        private CaseShiftController EnsureController()
        {
            if (shiftController == null)
                throw new InvalidOperationException("Assign a CaseShiftController or add one to the same GameObject.");
            return shiftController;
        }

        private void HandleSuspectStamped(SuspectStampEvent stamp)
        {
            Debug.Log(
                "[CaseShift] " + CaseShiftFlowEventNames.SuspectStamped + ": " +
                stamp.Decision + " " + stamp.SuspectName +
                (stamp.HasNextSuspect ? " → next suspect" : " → charge"),
                this);
        }

        private void HandleNextSuspect(NextSuspectEvent next)
        {
            Debug.Log(
                "[CaseShift] " + CaseShiftFlowEventNames.NextSuspectReady + ": " +
                (next.SuspectIndex + 1) + "/" + next.SuspectCount + " " + next.SuspectName,
                this);
        }

        private void HandleChargeReady(ChargeReadyEvent charge)
        {
            Debug.Log(
                "[CaseShift] " + CaseShiftFlowEventNames.ChargeReady + ": " +
                charge.HeldSuspectNames.Count + " held for " + charge.CaseTitle,
                this);
        }

        private void HandleMorningHeadline(MorningHeadlineEvent headline)
        {
            Debug.Log(
                "[CaseShift] " + CaseShiftFlowEventNames.MorningHeadline + ": " +
                headline.Headline + " | score " + headline.Score,
                this);
        }
    }
}
