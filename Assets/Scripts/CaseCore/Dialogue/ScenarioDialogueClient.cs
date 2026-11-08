using CaseClosed.CaseCore.Gemini;
using UnityEngine;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>
    /// Offline / scripted suspect replies for ask turns (no Gemini HTTP).
    /// Chizuru's dialogue-bank can expand this into a richer scenario bank;
    /// keep <see cref="TryBuildReply"/> stable so InterrogationDialogueService stays compatible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioDialogueClient : MonoBehaviour
    {
        /// <summary>
        /// Builds a spoken line from CaseCore stress/truth. Returns false only when inputs are unusable.
        /// </summary>
        public bool TryBuildReply(
            SuspectRecord suspect,
            string question,
            InterrogationResult stressResult,
            EvidenceRecord presentedEvidence,
            bool isCulprit,
            out GeminiSuspectReply reply,
            out string sourceTag)
        {
            reply = null;
            sourceTag = "scripted";

            if (stressResult == null)
                return false;

            string emotion = EmotionForTier(stressResult.Tier);
            string line;

            if (!stressResult.IsValid)
            {
                line = FirstNonEmpty(stressResult.FallbackLine, "I already answered that.");
                sourceTag = "scripted";
            }
            else if (!string.IsNullOrWhiteSpace(stressResult.Slip) &&
                     (stressResult.Tier == InterrogationStressTier.Cracking ||
                      stressResult.Tier == InterrogationStressTier.Broken))
            {
                line = stressResult.Slip.Trim();
                sourceTag = "suspect";
            }
            else if (presentedEvidence != null && isCulprit &&
                     stressResult.Tier >= InterrogationStressTier.Nervous)
            {
                line = PickCulpritEvidenceLine(stressResult.Tier, presentedEvidence);
                sourceTag = "suspect";
            }
            else if (presentedEvidence != null)
            {
                line = PickInnocentEvidenceLine(stressResult.Tier, presentedEvidence);
                sourceTag = "suspect";
            }
            else
            {
                line = PickOpenQuestionLine(suspect, question, stressResult.Tier);
                sourceTag = "suspect";
            }

            bool slip = !string.IsNullOrWhiteSpace(stressResult.Slip) &&
                        (stressResult.Tier == InterrogationStressTier.Cracking ||
                         stressResult.Tier == InterrogationStressTier.Broken);

            reply = GeminiSuspectReply.CreateFallback(line, emotion, slip);
            return true;
        }

        private static string PickOpenQuestionLine(
            SuspectRecord suspect,
            string question,
            InterrogationStressTier tier)
        {
            string nameBit = suspect != null && !string.IsNullOrWhiteSpace(suspect.DisplayName)
                ? suspect.DisplayName.Trim()
                : "I";

            switch (tier)
            {
                case InterrogationStressTier.Nervous:
                    return "Look, " + ShortTopic(question) + " — I already told you what I know.";
                case InterrogationStressTier.Cracking:
                    return "Just… give me a second. " + ShortTopic(question) + " isn't simple.";
                case InterrogationStressTier.Broken:
                    return "I can't keep this up. Ask whatever you want.";
                default:
                    return nameBit + " answered that. " + ShortTopic(question) + " doesn't change my story.";
            }
        }

        private static string PickCulpritEvidenceLine(InterrogationStressTier tier, EvidenceRecord evidence)
        {
            string title = evidence != null ? evidence.Title : "that";
            switch (tier)
            {
                case InterrogationStressTier.Cracking:
                case InterrogationStressTier.Broken:
                    return "That " + title + "… fine. I was there that night.";
                case InterrogationStressTier.Nervous:
                    return "You're twisting " + title + ". It doesn't mean what you think.";
                default:
                    return "I've seen " + title + ". It doesn't prove anything about me.";
            }
        }

        private static string PickInnocentEvidenceLine(InterrogationStressTier tier, EvidenceRecord evidence)
        {
            string title = evidence != null ? evidence.Title : "that";
            switch (tier)
            {
                case InterrogationStressTier.Cracking:
                case InterrogationStressTier.Broken:
                    return "Stop waving " + title + " at me. I wasn't involved.";
                case InterrogationStressTier.Nervous:
                    return title + " has nothing to do with me. Check again.";
                default:
                    return "Presenting " + title + " doesn't scare me. Wrong person.";
            }
        }

        private static string EmotionForTier(InterrogationStressTier tier)
        {
            switch (tier)
            {
                case InterrogationStressTier.Nervous:
                    return "nervous";
                case InterrogationStressTier.Cracking:
                    return "scared";
                case InterrogationStressTier.Broken:
                    return "broken";
                default:
                    return "calm";
            }
        }

        private static string ShortTopic(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return "that";
            string q = question.Trim();
            if (q.Length > 42)
                q = q.Substring(0, 39).TrimEnd() + "...";
            return "\"" + q + "\"";
        }

        private static string FirstNonEmpty(string primary, string fallback)
        {
            return !string.IsNullOrWhiteSpace(primary) ? primary.Trim() : fallback;
        }
    }
}
