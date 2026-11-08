using System;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>Tracks evidence collection and decisions for one active case.</summary>
    public sealed class CaseSession
    {
        private readonly CaseFile caseFile;
        private readonly HashSet<string> collectedEvidenceIds = new HashSet<string>();
        private readonly Dictionary<string, SuspectDecision> decisions = new Dictionary<string, SuspectDecision>();
        private bool isResolved;

        public CaseFile Case { get { return caseFile; } }
        public bool IsResolved { get { return isResolved; } }

        public CaseSession(CaseFile caseDefinition)
        {
            if (caseDefinition == null) throw new ArgumentNullException("caseDefinition");

            caseFile = caseDefinition;
            foreach (SuspectRecord suspect in caseFile.Suspects)
                decisions.Add(suspect.Id, SuspectDecision.Undecided);
        }

        /// <summary>Marks a real case record as collected and returns whether it was newly added.</summary>
        public bool CollectEvidence(string evidenceId)
        {
            EnsureActive();
            if (string.IsNullOrWhiteSpace(evidenceId)) return false;
            if (FindEvidence(evidenceId) == null) return false;
            return collectedEvidenceIds.Add(evidenceId);
        }

        /// <summary>Returns the evidence records the player has collected so far.</summary>
        public IReadOnlyList<EvidenceRecord> GetCollectedEvidence()
        {
            var result = new List<EvidenceRecord>();
            foreach (SuspectRecord suspect in caseFile.Suspects)
            {
                foreach (EvidenceRecord evidence in suspect.Evidence)
                {
                    if (collectedEvidenceIds.Contains(evidence.Id)) result.Add(evidence);
                }
            }
            return result;
        }

        /// <summary>Sets a suspect's decision to hold or release.</summary>
        public void SetDecision(string suspectId, SuspectDecision decision)
        {
            EnsureActive();
            if (decision == SuspectDecision.Undecided) throw new ArgumentException("Choose Hold or Release.", "decision");
            if (!decisions.ContainsKey(suspectId)) throw new ArgumentException("Unknown suspect ID.", "suspectId");
            decisions[suspectId] = decision;
        }

        /// <summary>Gets the current hold-or-release decision for a suspect.</summary>
        public SuspectDecision GetDecision(string suspectId)
        {
            SuspectDecision decision;
            if (!decisions.TryGetValue(suspectId, out decision)) throw new ArgumentException("Unknown suspect ID.", "suspectId");
            return decision;
        }

        /// <summary>Resolves the case by charging a held suspect, or passing null to charge nobody.</summary>
        public CaseOutcome ResolveCharge(string chargedSuspectId)
        {
            EnsureActive();
            foreach (KeyValuePair<string, SuspectDecision> decision in decisions)
            {
                if (decision.Value == SuspectDecision.Undecided)
                    throw new InvalidOperationException("Decide Hold or Release for every suspect before charging.");
            }

            if (chargedSuspectId == null)
            {
                isResolved = true;
                return IsCulpritHeld() ? CaseOutcome.NoCharge : CaseOutcome.CulpritReleased;
            }

            SuspectDecision chargedDecision;
            if (!decisions.TryGetValue(chargedSuspectId, out chargedDecision))
                throw new ArgumentException("Unknown suspect ID.", "chargedSuspectId");
            if (chargedDecision != SuspectDecision.Hold)
                throw new InvalidOperationException("You can only charge a suspect you held.");

            isResolved = true;
            if (chargedSuspectId == caseFile.CulpritSuspectId) return CaseOutcome.CulpritCharged;
            return CaseOutcome.InnocentCharged;
        }

        private bool IsCulpritHeld()
        {
            return decisions[caseFile.CulpritSuspectId] == SuspectDecision.Hold;
        }

        private EvidenceRecord FindEvidence(string evidenceId)
        {
            foreach (SuspectRecord suspect in caseFile.Suspects)
            {
                foreach (EvidenceRecord evidence in suspect.Evidence)
                {
                    if (evidence.Id == evidenceId) return evidence;
                }
            }
            return null;
        }

        private void EnsureActive()
        {
            if (isResolved) throw new InvalidOperationException("This case has already been resolved.");
        }
    }
}
