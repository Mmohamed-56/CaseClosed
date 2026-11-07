using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>Describes the current high-level phase of a case shift.</summary>
    public enum CaseShiftPhase
    {
        Inactive,
        Briefing,
        Investigating,
        Charging,
        /// <summary>Morning headline is on screen after the final charge.</summary>
        Headline,
        Resolved
    }

    /// <summary>Public summary of the resolved case, suitable for UI and local persistence.</summary>
    [Serializable]
    public sealed class CaseResolution
    {
        public string CaseId;
        public string Title;
        public string ChargedSuspectId;
        public string ChargedSuspectName;
        public CaseOutcome Outcome;
        public int Score;
        public int InnocentSuspectsHeld;
        public int QuestionsRemaining;
        public int SecondsRemaining;
        /// <summary>Morning newspaper banner. Filled by <see cref="MorningHeadlineBuilder"/>.</summary>
        public string Headline;
        /// <summary>One-line lede under the banner.</summary>
        public string Subheadline;
    }

    /// <summary>
    /// Coordinates suspect decisions, interrogation, scoring, and local result saving for one shift.
    /// Flow: briefing → investigate → HOLD/RELEASE stamp → next suspect (repeat) → final charge → morning headline.
    /// Bezi: bind Inspector UnityEvents under "Bezi / UI Hooks", or subscribe to the matching C# events.
    /// </summary>
    public sealed class CaseShiftController : MonoBehaviour
    {
        [Header("Shift Clock")]
        [Tooltip("When enabled, the shift clock counts down and undecided suspects are released when time expires.")]
        [SerializeField] private bool shiftClockEnabled;

        [Tooltip("Full-shift duration in seconds. The default ten minutes is 600 seconds.")]
        [SerializeField] private int shiftDurationSeconds = 600;

        /// <summary>Whether the shift clock is counting down.</summary>
        public bool ShiftClockEnabled
        {
            get { return shiftClockEnabled; }
            set { shiftClockEnabled = value; }
        }

        /// <summary>Configured full-shift duration in seconds.</summary>
        public int ShiftDurationSeconds
        {
            get { return shiftDurationSeconds; }
            set { shiftDurationSeconds = Mathf.Max(0, value); }
        }

        [Header("Local History")]
        [Tooltip("Display name saved to the local leaderboard.")]
        [SerializeField] private string playerName = "Detective";

        [Tooltip("Interrogation controller on this object. One is added automatically when missing.")]
        [SerializeField] private InterrogationController interrogationController;

        [Header("Bezi / UI Hooks")]
        [Tooltip(CaseShiftFlowEventNames.PhaseChanged)]
        [SerializeField] private CaseShiftPhaseUnityEvent onPhaseChanged = new CaseShiftPhaseUnityEvent();

        [Tooltip(CaseShiftFlowEventNames.SuspectStamped + " — stamp thud / clipboard leave")]
        [SerializeField] private SuspectStampUnityEvent onSuspectStamped = new SuspectStampUnityEvent();

        [Tooltip(CaseShiftFlowEventNames.NextSuspectReady + " — show next clipboard / enter next suspect")]
        [SerializeField] private NextSuspectUnityEvent onNextSuspectReady = new NextSuspectUnityEvent();

        [Tooltip(CaseShiftFlowEventNames.ChargeReady + " — open final charge UI")]
        [SerializeField] private ChargeReadyUnityEvent onChargeReady = new ChargeReadyUnityEvent();

        [Tooltip(CaseShiftFlowEventNames.MorningHeadline + " — newspaper screen")]
        [SerializeField] private MorningHeadlineUnityEvent onMorningHeadline = new MorningHeadlineUnityEvent();

        private CaseSession activeSession;
        private int currentSuspectIndex;
        private float secondsRemaining;
        private bool isResolving;
        private CaseResolution lastResolution;
        private MorningHeadlineEvent lastHeadline;

        /// <summary>Raised whenever the shift enters a new phase.</summary>
        public event Action<CaseShiftPhase> PhaseChanged;

        /// <summary>Raised after a HOLD or RELEASE stamp is recorded.</summary>
        public event Action<SuspectStampEvent> SuspectStamped;

        /// <summary>Raised when the next suspect becomes the current briefing target.</summary>
        public event Action<NextSuspectEvent> NextSuspectReady;

        /// <summary>Raised when every suspect is decided and the charge UI should open.</summary>
        public event Action<ChargeReadyEvent> ChargeReady;

        /// <summary>Raised when the morning headline is ready to show.</summary>
        public event Action<MorningHeadlineEvent> MorningHeadlineReady;

        /// <summary>Raised after the final charge, headline, and local save are complete.</summary>
        public event Action<CaseResolution> ShiftResolved;

        /// <summary>The active case session, or null before a shift starts.</summary>
        public CaseSession ActiveSession { get { return activeSession; } }

        /// <summary>The current high-level phase.</summary>
        public CaseShiftPhase Phase { get; private set; }

        /// <summary>The suspect whose turn is active, or null while charging / headline / resolved.</summary>
        public SuspectRecord CurrentSuspect
        {
            get
            {
                if (activeSession == null || currentSuspectIndex < 0 || currentSuspectIndex >= activeSession.Case.Suspects.Count)
                    return null;
                return activeSession.Case.Suspects[currentSuspectIndex];
            }
        }

        /// <summary>Zero-based index of <see cref="CurrentSuspect"/>, or -1 when none.</summary>
        public int CurrentSuspectIndex { get { return CurrentSuspect == null ? -1 : currentSuspectIndex; } }

        /// <summary>Remaining whole seconds on the shift clock.</summary>
        public int SecondsRemaining { get { return Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining)); } }

        /// <summary>Last resolution after a completed shift, or null.</summary>
        public CaseResolution LastResolution { get { return lastResolution; } }

        /// <summary>Last morning headline after a completed shift, or null.</summary>
        public MorningHeadlineEvent LastHeadline { get { return lastHeadline; } }

        private void Update()
        {
            if (!shiftClockEnabled || Phase == CaseShiftPhase.Inactive || Phase == CaseShiftPhase.Resolved ||
                Phase == CaseShiftPhase.Charging || Phase == CaseShiftPhase.Headline)
                return;

            secondsRemaining = Mathf.Max(0f, secondsRemaining - Time.deltaTime);
            if (secondsRemaining <= 0f) ResolveExpiredShift();
        }

        /// <summary>Generates and starts a reproducible campus case from the supplied seed.</summary>
        public void BeginGeneratedShift(int seed, int suspectCount = 4)
        {
            BeginShift(CaseGenerator.Generate(seed, suspectCount));
        }

        /// <summary>Starts a shift with a supplied deterministic case definition.</summary>
        public void BeginShift(CaseFile caseDefinition)
        {
            if (caseDefinition == null) throw new ArgumentNullException("caseDefinition");

            if (interrogationController == null)
            {
                interrogationController = GetComponent<InterrogationController>();
                if (interrogationController == null) interrogationController = gameObject.AddComponent<InterrogationController>();
            }

            activeSession = new CaseSession(caseDefinition);
            interrogationController.BeginCase(activeSession);
            currentSuspectIndex = 0;
            secondsRemaining = Mathf.Max(0, shiftDurationSeconds);
            isResolving = false;
            lastResolution = null;
            lastHeadline = null;
            SetPhase(CaseShiftPhase.Briefing);
            RaiseNextSuspectReady();
        }

        /// <summary>Begins investigating the current suspect after the briefing or previous decision.</summary>
        public void BeginCurrentInterview()
        {
            EnsureActive();
            if (Phase != CaseShiftPhase.Briefing && Phase != CaseShiftPhase.Investigating)
                throw new InvalidOperationException("The current suspect cannot be interviewed in this shift phase.");
            SetPhase(CaseShiftPhase.Investigating);
        }

        /// <summary>Adds a known case record to the collected evidence board.</summary>
        public bool CollectEvidence(string evidenceId)
        {
            EnsureActive();
            if (Phase != CaseShiftPhase.Briefing && Phase != CaseShiftPhase.Investigating)
                throw new InvalidOperationException("Evidence can only be collected before the charge phase.");
            return activeSession.CollectEvidence(evidenceId);
        }

        /// <summary>Asks the active suspect a question and applies code-owned stress rules.</summary>
        public InterrogationResult AskCurrentSuspect(string question, string evidenceId = null, bool aggressive = false)
        {
            EnsureActive();
            if (Phase != CaseShiftPhase.Investigating)
                throw new InvalidOperationException("Begin the current interview before asking questions.");
            return interrogationController.AskQuestion(CurrentSuspect.Id, question, evidenceId, aggressive);
        }

        /// <summary>Bezi / stamp UI: stamps HOLD on the current suspect.</summary>
        public void StampHold()
        {
            DecideCurrentSuspect(SuspectDecision.Hold);
        }

        /// <summary>Bezi / stamp UI: stamps RELEASE on the current suspect.</summary>
        public void StampRelease()
        {
            DecideCurrentSuspect(SuspectDecision.Release);
        }

        /// <summary>Records the current suspect's HOLD or RELEASE decision and advances the shift.</summary>
        public void DecideCurrentSuspect(SuspectDecision decision)
        {
            EnsureActive();
            if (Phase != CaseShiftPhase.Investigating)
                throw new InvalidOperationException("A suspect decision can only be made after investigation begins.");
            if (decision != SuspectDecision.Hold && decision != SuspectDecision.Release)
                throw new ArgumentException("Stamp HOLD or RELEASE.", "decision");

            SuspectRecord stamped = CurrentSuspect;
            int stampedIndex = currentSuspectIndex;
            int suspectCount = activeSession.Case.Suspects.Count;

            activeSession.SetDecision(stamped.Id, decision);
            currentSuspectIndex++;

            bool hasNext = currentSuspectIndex < suspectCount;
            bool chargeStarted = !hasNext;

            var stampEvent = new SuspectStampEvent
            {
                SuspectId = stamped.Id,
                SuspectName = stamped.DisplayName,
                Decision = decision,
                SuspectIndex = stampedIndex,
                SuspectCount = suspectCount,
                HasNextSuspect = hasNext,
                ChargePhaseStarted = chargeStarted
            };
            RaiseSuspectStamped(stampEvent);

            if (chargeStarted)
            {
                SetPhase(CaseShiftPhase.Charging);
                RaiseChargeReady();
            }
            else
            {
                SetPhase(CaseShiftPhase.Briefing);
                RaiseNextSuspectReady();
            }
        }

        /// <summary>Charges a held suspect, or passes null to charge nobody, then shows the morning headline and saves.</summary>
        public CaseResolution ResolveCharge(string chargedSuspectId)
        {
            EnsureActive();
            if (Phase != CaseShiftPhase.Charging)
                throw new InvalidOperationException("Decide Hold or Release for every suspect before charging.");
            return CompleteResolution(chargedSuspectId);
        }

        /// <summary>Bezi / charge UI: charge nobody and show the morning headline.</summary>
        public void ChargeNobody()
        {
            ResolveCharge(null);
        }

        /// <summary>Returns the suspect's current decision during an active shift.</summary>
        public SuspectDecision GetDecision(string suspectId)
        {
            EnsureActive();
            return activeSession.GetDecision(suspectId);
        }

        /// <summary>Returns held suspects available for the final charge UI.</summary>
        public IReadOnlyList<SuspectRecord> GetHeldSuspects()
        {
            EnsureActive();
            var held = new List<SuspectRecord>();
            IReadOnlyList<SuspectRecord> suspects = activeSession.Case.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                SuspectRecord suspect = suspects[i];
                if (activeSession.GetDecision(suspect.Id) == SuspectDecision.Hold)
                    held.Add(suspect);
            }
            return held;
        }

        /// <summary>Returns the interrogation controller's remaining question count for a suspect.</summary>
        public int GetQuestionsLeft(string suspectId)
        {
            return interrogationController == null ? 0 : interrogationController.GetQuestionsLeft(suspectId);
        }

        private void ResolveExpiredShift()
        {
            if (activeSession == null || isResolving || Phase == CaseShiftPhase.Resolved || Phase == CaseShiftPhase.Headline)
                return;

            IReadOnlyList<SuspectRecord> suspects = activeSession.Case.Suspects;
            for (int i = currentSuspectIndex; i < suspects.Count; i++)
            {
                if (activeSession.GetDecision(suspects[i].Id) == SuspectDecision.Undecided)
                    activeSession.SetDecision(suspects[i].Id, SuspectDecision.Release);
            }

            currentSuspectIndex = suspects.Count;
            SetPhase(CaseShiftPhase.Charging);
            RaiseChargeReady();
            CompleteResolution(null);
        }

        private CaseResolution CompleteResolution(string chargedSuspectId)
        {
            if (isResolving) throw new InvalidOperationException("This shift is already resolving.");
            CaseOutcome outcome = activeSession.ResolveCharge(chargedSuspectId);
            isResolving = true;
            int innocentHeld = 0;
            int questionsLeft = 0;
            string chargedName = string.Empty;

            IReadOnlyList<SuspectRecord> suspects = activeSession.Case.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                SuspectRecord suspect = suspects[i];
                if (activeSession.GetDecision(suspect.Id) == SuspectDecision.Hold && suspect.Id != activeSession.Case.CulpritSuspectId)
                    innocentHeld++;
                questionsLeft += interrogationController.GetQuestionsLeft(suspect.Id);
                if (suspect.Id == chargedSuspectId) chargedName = suspect.DisplayName;
            }

            // Score stubs — GDD weights; fine to polish later.
            int score = 0;
            if (outcome == CaseOutcome.CulpritCharged) score += 1000;
            score += questionsLeft * 100;
            if (shiftClockEnabled) score += SecondsRemaining * 5;
            score -= innocentHeld * 300;
            if (outcome == CaseOutcome.InnocentCharged) score -= 500;

            string headline = MorningHeadlineBuilder.BuildHeadline(outcome, activeSession.Case.Title, chargedName);
            string subheadline = MorningHeadlineBuilder.BuildSubheadline(outcome, chargedName, score, innocentHeld);

            string date = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var resolution = new CaseResolution
            {
                CaseId = activeSession.Case.CaseId,
                Title = activeSession.Case.Title,
                ChargedSuspectId = chargedSuspectId ?? string.Empty,
                ChargedSuspectName = chargedName,
                Outcome = outcome,
                Score = score,
                InnocentSuspectsHeld = innocentHeld,
                QuestionsRemaining = questionsLeft,
                SecondsRemaining = SecondsRemaining,
                Headline = headline,
                Subheadline = subheadline
            };

            CaseHistoryStore.AppendClosedCaseSummary(
                resolution.CaseId,
                resolution.Title,
                date,
                outcome.ToString(),
                chargedName,
                score);
            CaseHistoryStore.RecordScore(playerName, score, date, resolution.CaseId);

            lastResolution = resolution;
            lastHeadline = MorningHeadlineBuilder.FromResolution(resolution);

            SetPhase(CaseShiftPhase.Headline);
            RaiseMorningHeadline(lastHeadline);

            SetPhase(CaseShiftPhase.Resolved);
            ShiftResolved?.Invoke(resolution);
            return resolution;
        }

        private void RaiseSuspectStamped(SuspectStampEvent stampEvent)
        {
            SuspectStamped?.Invoke(stampEvent);
            if (onSuspectStamped != null) onSuspectStamped.Invoke(stampEvent);
        }

        private void RaiseNextSuspectReady()
        {
            SuspectRecord suspect = CurrentSuspect;
            if (suspect == null || activeSession == null) return;

            var nextEvent = new NextSuspectEvent
            {
                SuspectId = suspect.Id,
                SuspectName = suspect.DisplayName,
                Statement = suspect.Statement,
                CampusRole = suspect.CampusRole,
                SuspectIndex = currentSuspectIndex,
                SuspectCount = activeSession.Case.Suspects.Count
            };
            NextSuspectReady?.Invoke(nextEvent);
            if (onNextSuspectReady != null) onNextSuspectReady.Invoke(nextEvent);
        }

        private void RaiseChargeReady()
        {
            if (activeSession == null) return;

            var chargeEvent = new ChargeReadyEvent
            {
                CaseId = activeSession.Case.CaseId,
                CaseTitle = activeSession.Case.Title
            };

            IReadOnlyList<SuspectRecord> held = GetHeldSuspects();
            for (int i = 0; i < held.Count; i++)
            {
                chargeEvent.HeldSuspectIds.Add(held[i].Id);
                chargeEvent.HeldSuspectNames.Add(held[i].DisplayName);
            }

            ChargeReady?.Invoke(chargeEvent);
            if (onChargeReady != null) onChargeReady.Invoke(chargeEvent);
        }

        private void RaiseMorningHeadline(MorningHeadlineEvent headlineEvent)
        {
            MorningHeadlineReady?.Invoke(headlineEvent);
            if (onMorningHeadline != null) onMorningHeadline.Invoke(headlineEvent);
        }

        private void EnsureActive()
        {
            if (activeSession == null) throw new InvalidOperationException("Begin a shift before using the case controls.");
            if (activeSession.IsResolved) throw new InvalidOperationException("This shift has already been resolved.");
        }

        private void SetPhase(CaseShiftPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
            if (onPhaseChanged != null) onPhaseChanged.Invoke(phase);
        }
    }
}
