using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>Describes a suspect's deterministic interrogation stress level.</summary>
    public enum InterrogationStressTier
    {
        Calm,
        Nervous,
        Cracking,
        Broken
    }

    /// <summary>Serializable outcome of one attempted interrogation question.</summary>
    [Serializable]
    public sealed class InterrogationResult
    {
        public bool IsValid;
        public int QuestionsLeft;
        public int Stress;
        public InterrogationStressTier Tier;
        public string Slip;
        public string FallbackLine;
        public bool IsShutdown;
    }

    /// <summary>Owns deterministic per-suspect questioning and stress rules for an active case.</summary>
    public sealed class InterrogationController : MonoBehaviour
    {
        [Header("Question Limits")]
        [Tooltip("Maximum number of valid questions each suspect can receive during this case.")]
        [SerializeField] private int maxQuestionsPerSuspect = 6;

        [Header("Stress")]
        [Tooltip("Stress added by each valid question, including aggressive questions.")]
        [SerializeField] private int normalQuestionStress = 3;
        [Tooltip("Additional stress from collected Contradiction evidence belonging to the questioned suspect.")]
        [SerializeField] private int contradictionEvidenceStress = 40;
        [Tooltip("Additional stress from collected RedHerring evidence belonging to the questioned suspect.")]
        [SerializeField] private int redHerringEvidenceStress = 10;

        private readonly Dictionary<string, SuspectState> suspectStates = new Dictionary<string, SuspectState>();
        private CaseSession session;

        private sealed class SuspectState
        {
            public int QuestionsAsked;
            public int Stress;
            public int AggressiveQuestions;
            public bool IsShutdown;
        }

        /// <summary>
        /// Raised immediately when a valid question is accepted (before the caller awaits Gemini/TTS).
        /// Args: suspectId, result, aggressive, confrontedWithEvidence.
        /// <see cref="CaseClosed.Voice.BarkPlayer"/> uses this for instant reaction audio.
        /// </summary>
        public event Action<string, InterrogationResult, bool, bool> ValidQuestionAsked;

        /// <summary>Starts or resets interrogation state for every suspect in the supplied case session.</summary>
        public void BeginCase(CaseSession caseSession)
        {
            if (caseSession == null) throw new ArgumentNullException("caseSession");

            session = caseSession;
            suspectStates.Clear();
            foreach (SuspectRecord suspect in session.Case.Suspects)
            {
                if (suspect != null && !string.IsNullOrWhiteSpace(suspect.Id))
                    suspectStates[suspect.Id] = new SuspectState();
            }
        }

        /// <summary>Applies a question and any already-collected evidence, returning its deterministic outcome.</summary>
        public InterrogationResult AskQuestion(string suspectId, string question, string evidenceId = null, bool aggressive = false)
        {
            SuspectState state;
            if (session == null || string.IsNullOrWhiteSpace(suspectId) ||
                !suspectStates.TryGetValue(suspectId, out state))
                return CreateResult(false, null, "No answer: this suspect is not part of the active case.");

            if (state.IsShutdown)
                return CreateResult(false, state, "I have nothing more to say.");

            if (string.IsNullOrWhiteSpace(question))
                return CreateResult(false, state, "Ask a question before continuing.");

            if (state.QuestionsAsked >= GetMaximumQuestions())
                return CreateResult(false, state, "There are no questions remaining.");

            EvidenceRecord presentedEvidence = null;
            if (!string.IsNullOrWhiteSpace(evidenceId))
            {
                IReadOnlyList<EvidenceRecord> collectedEvidence = session.GetCollectedEvidence();
                for (int i = 0; i < collectedEvidence.Count; i++)
                {
                    if (collectedEvidence[i] != null && collectedEvidence[i].Id == evidenceId)
                    {
                        presentedEvidence = collectedEvidence[i];
                        break;
                    }
                }

                if (presentedEvidence == null)
                    return CreateResult(false, state, "That evidence has not been collected.");
            }

            state.QuestionsAsked++;
            if (aggressive) state.AggressiveQuestions++;

            int stressIncrease = Mathf.Max(0, normalQuestionStress);
            string evidenceOwnerId = null;
            if (presentedEvidence != null)
            {
                foreach (SuspectRecord suspect in session.Case.Suspects)
                {
                    if (suspect == null) continue;
                    IReadOnlyList<EvidenceRecord> suspectEvidence = suspect.Evidence;
                    for (int i = 0; i < suspectEvidence.Count; i++)
                    {
                        if (suspectEvidence[i] != null && suspectEvidence[i].Id == presentedEvidence.Id)
                        {
                            evidenceOwnerId = suspect.Id;
                            break;
                        }
                    }
                    if (evidenceOwnerId != null) break;
                }

                if (evidenceOwnerId == suspectId)
                {
                    if (presentedEvidence.Kind == EvidenceKind.Contradiction)
                        stressIncrease += Mathf.Max(0, contradictionEvidenceStress);
                    else if (presentedEvidence.Kind == EvidenceKind.RedHerring)
                        stressIncrease += Mathf.Max(0, redHerringEvidenceStress);
                }
            }

            state.Stress = Mathf.Clamp(state.Stress + stressIncrease, 0, 100);

            bool isCulprit = suspectId == session.Case.CulpritSuspectId;
            if (!isCulprit && state.Stress >= 70 && state.AggressiveQuestions >= 2)
                state.IsShutdown = true;

            InterrogationStressTier tier = GetTier(state.Stress);
            string slip = isCulprit && (tier == InterrogationStressTier.Cracking || tier == InterrogationStressTier.Broken)
                ? "I was there that night."
                : null;
            string fallbackLine = state.IsShutdown
                ? "I have nothing more to say."
                : GetFallbackLine(tier);

            var result = new InterrogationResult
            {
                IsValid = true,
                QuestionsLeft = Mathf.Max(0, GetMaximumQuestions() - state.QuestionsAsked),
                Stress = state.Stress,
                Tier = tier,
                Slip = slip,
                FallbackLine = fallbackLine,
                IsShutdown = state.IsShutdown
            };

            // Fire before return so BarkPlayer can PlayOneShot while Gemini/ElevenLabs are still loading.
            bool confronted = presentedEvidence != null;
            Action<string, InterrogationResult, bool, bool> barkHandlers = ValidQuestionAsked;
            if (barkHandlers != null)
                barkHandlers.Invoke(suspectId, result, aggressive, confronted);

            return result;
        }

        /// <summary>Returns how many valid questions remain for the named suspect.</summary>
        public int GetQuestionsLeft(string suspectId)
        {
            SuspectState state;
            return !string.IsNullOrWhiteSpace(suspectId) && suspectStates.TryGetValue(suspectId, out state)
                ? Mathf.Max(0, GetMaximumQuestions() - state.QuestionsAsked)
                : 0;
        }

        /// <summary>Returns the current stress value for the named suspect, from zero to one hundred.</summary>
        public int GetStress(string suspectId)
        {
            SuspectState state;
            return !string.IsNullOrWhiteSpace(suspectId) && suspectStates.TryGetValue(suspectId, out state)
                ? state.Stress
                : 0;
        }

        private int GetMaximumQuestions()
        {
            return Mathf.Max(0, maxQuestionsPerSuspect);
        }

        private static InterrogationStressTier GetTier(int stress)
        {
            if (stress >= 85) return InterrogationStressTier.Broken;
            if (stress >= 60) return InterrogationStressTier.Cracking;
            if (stress >= 30) return InterrogationStressTier.Nervous;
            return InterrogationStressTier.Calm;
        }

        private static string GetFallbackLine(InterrogationStressTier tier)
        {
            switch (tier)
            {
                case InterrogationStressTier.Nervous:
                    return "I already told you what happened.";
                case InterrogationStressTier.Cracking:
                    return "Give me a second.";
                case InterrogationStressTier.Broken:
                    return "I can't keep this up.";
                default:
                    return "I answered you.";
            }
        }

        private InterrogationResult CreateResult(bool isValid, SuspectState state, string fallbackLine)
        {
            int stress = state == null ? 0 : state.Stress;
            return new InterrogationResult
            {
                IsValid = isValid,
                QuestionsLeft = state == null ? 0 : Mathf.Max(0, GetMaximumQuestions() - state.QuestionsAsked),
                Stress = stress,
                Tier = GetTier(stress),
                Slip = null,
                FallbackLine = fallbackLine,
                IsShutdown = state != null && state.IsShutdown
            };
        }
    }
}
