using System.Collections.Generic;
using System.Text;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>Builds a tight Gemini system/user prompt from code-owned case truth and stress.</summary>
    public static class InterrogationPromptBuilder
    {
        public static string BuildSystemPrompt(
            SuspectRecord suspect,
            CaseFile caseFile,
            InterrogationResult stressResult,
            EvidenceRecord presentedEvidence,
            bool isCulprit)
        {
            var sb = new StringBuilder(512);
            sb.Append("You are ").Append(suspect.DisplayName);
            sb.Append(", a ").Append(string.IsNullOrWhiteSpace(suspect.CampusRole) ? "campus affiliate" : suspect.CampusRole);
            sb.Append(" under police interrogation. Never mention AI.\n");
            sb.Append("Answer in 1-2 short sentences, under 30 words. Be snappy.\n");
            sb.Append("Case: ").Append(caseFile.Title).Append('\n');
            sb.Append("Your cover story / alibi: ").Append(string.IsNullOrWhiteSpace(suspect.Alibi) ? suspect.Statement : suspect.Alibi).Append('\n');
            sb.Append("Your opening statement: ").Append(suspect.Statement).Append('\n');
            sb.Append("Stress tier: ").Append(stressResult.Tier).Append(" (").Append(stressResult.Stress).Append("/100). Act it out.\n");

            if (isCulprit)
            {
                string weakPoint = FindContradictionExplanation(suspect);
                if (stressResult.Tier == InterrogationStressTier.Cracking ||
                    stressResult.Tier == InterrogationStressTier.Broken)
                {
                    sb.Append("You are the culprit. You may slip one true detail. Truth: ");
                    sb.Append(string.IsNullOrWhiteSpace(weakPoint) ? "You returned during the theft window." : weakPoint);
                    sb.Append('\n');
                }
                else
                {
                    sb.Append("You are hiding guilt. Stick to the cover story. Do not confess yet.\n");
                }
            }
            else
            {
                sb.Append("You are innocent. Defend yourself. If stressed high, get angry or shut down.\n");
                string herring = FindRedHerringExplanation(suspect);
                if (!string.IsNullOrWhiteSpace(herring))
                    sb.Append("Innocent explanation for suspicious records: ").Append(herring).Append('\n');
            }

            if (stressResult.IsShutdown)
                sb.Append("You have shut down. Say you want a lawyer or refuse further answers.\n");

            if (presentedEvidence != null)
            {
                sb.Append("Detective confronts you with evidence titled \"");
                sb.Append(presentedEvidence.Title).Append("\": ");
                sb.Append(presentedEvidence.Description).Append('\n');
                if (!string.IsNullOrWhiteSpace(presentedEvidence.Explanation) &&
                    presentedEvidence.Kind == EvidenceKind.RedHerring &&
                    !isCulprit)
                {
                    sb.Append("Give the innocent explanation briefly: ");
                    sb.Append(presentedEvidence.Explanation).Append('\n');
                }
            }

            sb.Append("Never invent new physical evidence, names, or times not listed above.\n");
            sb.Append("Reply ONLY with JSON: {\"line\":\"...\",\"emotion\":\"calm|defensive|nervous|angry|scared|broken\",\"slip\":false}");
            return sb.ToString();
        }

        public static string BuildUserPrompt(
            string question,
            IReadOnlyList<string> recentTurns)
        {
            var sb = new StringBuilder(256);
            if (recentTurns != null && recentTurns.Count > 0)
            {
                sb.Append("Recent exchange:\n");
                int start = recentTurns.Count > 4 ? recentTurns.Count - 4 : 0;
                for (int i = start; i < recentTurns.Count; i++)
                    sb.Append(recentTurns[i]).Append('\n');
            }

            sb.Append("Detective: ").Append(question.Trim());
            return sb.ToString();
        }

        private static string FindContradictionExplanation(SuspectRecord suspect)
        {
            for (int i = 0; i < suspect.Evidence.Count; i++)
            {
                EvidenceRecord evidence = suspect.Evidence[i];
                if (evidence != null && evidence.Kind == EvidenceKind.Contradiction &&
                    !string.IsNullOrWhiteSpace(evidence.Explanation))
                    return evidence.Explanation;
            }

            return string.Empty;
        }

        private static string FindRedHerringExplanation(SuspectRecord suspect)
        {
            for (int i = 0; i < suspect.Evidence.Count; i++)
            {
                EvidenceRecord evidence = suspect.Evidence[i];
                if (evidence != null && evidence.Kind == EvidenceKind.RedHerring &&
                    !string.IsNullOrWhiteSpace(evidence.Explanation))
                    return evidence.Explanation;
            }

            return string.Empty;
        }
    }
}
