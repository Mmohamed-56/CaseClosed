using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Payload for a HOLD / RELEASE stamp on the current suspect.
    /// Bezi: bind UI / SFX to <see cref="CaseShiftController.SuspectStamped"/>
    /// or the Inspector UnityEvent of the same name.
    /// </summary>
    [Serializable]
    public sealed class SuspectStampEvent
    {
        public string SuspectId;
        public string SuspectName;
        public SuspectDecision Decision;
        public int SuspectIndex;
        public int SuspectCount;
        public bool HasNextSuspect;
        public bool ChargePhaseStarted;
    }

    /// <summary>
    /// Payload when the shift advances to the next suspect's briefing clipboard.
    /// Bezi: show the next clipboard / trigger enter animation.
    /// </summary>
    [Serializable]
    public sealed class NextSuspectEvent
    {
        public string SuspectId;
        public string SuspectName;
        public string Statement;
        public string CampusRole;
        public int SuspectIndex;
        public int SuspectCount;
    }

    /// <summary>
    /// Payload when every suspect has been stamped and the final charge UI should open.
    /// Bezi: populate charge buttons from <see cref="HeldSuspectIds"/> / names.
    /// </summary>
    [Serializable]
    public sealed class ChargeReadyEvent
    {
        public string CaseId;
        public string CaseTitle;
        public List<string> HeldSuspectIds = new List<string>();
        public List<string> HeldSuspectNames = new List<string>();
    }

    /// <summary>
    /// Morning newspaper payload after the final charge.
    /// Bezi: bind HeadlineScreen text to <see cref="Headline"/> / <see cref="Subheadline"/> / score fields.
    /// </summary>
    [Serializable]
    public sealed class MorningHeadlineEvent
    {
        public string CaseId;
        public string CaseTitle;
        public CaseOutcome Outcome;
        public string Headline;
        public string Subheadline;
        public string ChargedSuspectId;
        public string ChargedSuspectName;
        public int Score;
        public int InnocentSuspectsHeld;
        public int QuestionsRemaining;
        public int SecondsRemaining;
    }

    /// <summary>Inspector-bindable UnityEvent wrappers for Bezi UI wiring.</summary>
    [Serializable] public sealed class SuspectStampUnityEvent : UnityEvent<SuspectStampEvent> { }
    [Serializable] public sealed class NextSuspectUnityEvent : UnityEvent<NextSuspectEvent> { }
    [Serializable] public sealed class ChargeReadyUnityEvent : UnityEvent<ChargeReadyEvent> { }
    [Serializable] public sealed class MorningHeadlineUnityEvent : UnityEvent<MorningHeadlineEvent> { }
    [Serializable] public sealed class CaseShiftPhaseUnityEvent : UnityEvent<CaseShiftPhase> { }

    /// <summary>
    /// Stable event-name constants for docs / logging / future message-bus adapters.
    /// Prefer subscribing on <see cref="CaseShiftController"/>; these names match the UnityEvents.
    /// </summary>
    public static class CaseShiftFlowEventNames
    {
        public const string PhaseChanged = "CaseShift.PhaseChanged";
        public const string SuspectStamped = "CaseShift.SuspectStamped";
        public const string NextSuspectReady = "CaseShift.NextSuspectReady";
        public const string ChargeReady = "CaseShift.ChargeReady";
        public const string MorningHeadline = "CaseShift.MorningHeadline";
        public const string ShiftResolved = "CaseShift.ShiftResolved";
    }
}
