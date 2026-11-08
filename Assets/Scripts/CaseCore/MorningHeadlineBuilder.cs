using System;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Builds the morning-newspaper copy from a resolved outcome.
    /// Score / wording stubs are intentional — polish can swap templates later.
    /// </summary>
    public static class MorningHeadlineBuilder
    {
        /// <summary>Returns a primary headline string for the given outcome and case title.</summary>
        public static string BuildHeadline(CaseOutcome outcome, string caseTitle, string chargedSuspectName)
        {
            string title = string.IsNullOrWhiteSpace(caseTitle) ? "Campus Case" : caseTitle.Trim();
            string charged = string.IsNullOrWhiteSpace(chargedSuspectName) ? "a suspect" : chargedSuspectName.Trim();

            switch (outcome)
            {
                case CaseOutcome.CulpritCharged:
                    return title + ": Suspect Charged Before Morning";
                case CaseOutcome.CulpritReleased:
                    return "Second Incident Reported — Suspect Still At Large";
                case CaseOutcome.InnocentCharged:
                    return charged + " Wrongly Detained, Family Files Complaint";
                case CaseOutcome.NoCharge:
                    return title + " Goes Cold Overnight";
                default:
                    throw new ArgumentOutOfRangeException("outcome", outcome, "Unknown case outcome.");
            }
        }

        /// <summary>Returns a one-line subheadline / lede under the main banner.</summary>
        public static string BuildSubheadline(CaseOutcome outcome, string chargedSuspectName, int score, int innocentHeld)
        {
            string charged = string.IsNullOrWhiteSpace(chargedSuspectName) ? "nobody" : chargedSuspectName.Trim();

            switch (outcome)
            {
                case CaseOutcome.CulpritCharged:
                    return "Night-shift detective charged " + charged + ". Score: " + score + ".";
                case CaseOutcome.CulpritReleased:
                    return "The culprit walked free. UBPD asks the public for tips. Score: " + score + ".";
                case CaseOutcome.InnocentCharged:
                    return "An innocent party was charged. Captains reviewing hold policy. Score: " + score + ".";
                case CaseOutcome.NoCharge:
                    string holdNote = innocentHeld > 0
                        ? " " + innocentHeld + " held without a charge."
                        : " No holds filed.";
                    return "Case closed without a charge." + holdNote + " Score: " + score + ".";
                default:
                    throw new ArgumentOutOfRangeException("outcome", outcome, "Unknown case outcome.");
            }
        }

        /// <summary>Packs a full morning-headline event from a <see cref="CaseResolution"/>.</summary>
        public static MorningHeadlineEvent FromResolution(CaseResolution resolution)
        {
            if (resolution == null) throw new ArgumentNullException("resolution");

            string headline = string.IsNullOrWhiteSpace(resolution.Headline)
                ? BuildHeadline(resolution.Outcome, resolution.Title, resolution.ChargedSuspectName)
                : resolution.Headline;
            string subheadline = string.IsNullOrWhiteSpace(resolution.Subheadline)
                ? BuildSubheadline(resolution.Outcome, resolution.ChargedSuspectName, resolution.Score, resolution.InnocentSuspectsHeld)
                : resolution.Subheadline;

            return new MorningHeadlineEvent
            {
                CaseId = resolution.CaseId ?? string.Empty,
                CaseTitle = resolution.Title ?? string.Empty,
                Outcome = resolution.Outcome,
                Headline = headline,
                Subheadline = subheadline,
                ChargedSuspectId = resolution.ChargedSuspectId ?? string.Empty,
                ChargedSuspectName = resolution.ChargedSuspectName ?? string.Empty,
                Score = resolution.Score,
                InnocentSuspectsHeld = resolution.InnocentSuspectsHeld,
                QuestionsRemaining = resolution.QuestionsRemaining,
                SecondsRemaining = resolution.SecondsRemaining
            };
        }
    }
}
