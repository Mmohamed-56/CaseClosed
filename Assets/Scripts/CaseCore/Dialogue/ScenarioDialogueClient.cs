using System;
using System.Collections.Generic;
using CaseClosed.CaseCore.Gemini;
using UnityEngine;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>
    /// Evidence-driven choice trees: present a record → pick a question → stress-gated reply.
    /// Same ask → {line, emotion, slip} shape for ElevenLabs via SuspectLineReady.
    /// Never networks. No free-text keyword matching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioDialogueClient : MonoBehaviour
    {
        [SerializeField] private bool logMatches;

        private readonly DialogueTreeBank treeBank = new DialogueTreeBank();
        private readonly System.Random rng = new System.Random();

        public int LoadedEntryCount
        {
            get
            {
                treeBank.EnsureLoaded();
                return treeBank.TreeCount;
            }
        }

        public bool IsReady
        {
            get
            {
                treeBank.EnsureLoaded();
                return treeBank.TreeCount > 0;
            }
        }

        private void Awake()
        {
            treeBank.EnsureLoaded();
        }

        /// <summary>Reload tree JSON from Resources (Editor polish / overnight content drops).</summary>
        public void ReloadBank()
        {
            treeBank.Reload();
        }

        /// <summary>
        /// Same callback shape as Gemini consumers expect:
        /// onComplete receives a JSON string <c>{"line","emotion","slip"}</c>.
        /// </summary>
        public void Ask(
            ScenarioAskContext context,
            Action<string> onComplete,
            Action<string> onError)
        {
            GeminiSuspectReply reply;
            if (!TryResolve(context, out reply))
            {
                onError?.Invoke("Dialogue tree returned no line.");
                return;
            }

            onComplete?.Invoke(ToJson(reply));
        }

        /// <summary>
        /// Questions the player may ask after presenting <paramref name="evidenceId"/>
        /// against the current suspect. Empty if no record selected or no tree branch.
        /// </summary>
        public List<DialogueQuestionChoice> GetQuestionChoices(string suspectId, string evidenceId)
        {
            var list = new List<DialogueQuestionChoice>(4);
            treeBank.EnsureLoaded();

            DialogueTreeFile tree;
            if (!treeBank.TryGetTree(suspectId, out tree) || tree == null)
                return list;

            string key = EvidenceKeyFromId(evidenceId);
            if (string.IsNullOrEmpty(key) || key == "hold" || key == "release")
                return list;

            DialogueTreeBranch branch = FindBranch(tree, key);
            if (branch == null || branch.questions == null) return list;

            for (int i = 0; i < branch.questions.Length; i++)
            {
                DialogueTreeQuestion q = branch.questions[i];
                if (q == null || string.IsNullOrWhiteSpace(q.prompt)) continue;
                if (string.Equals(q.prompt, "HOLD", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(q.prompt, "RELEASE", StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(new DialogueQuestionChoice
                {
                    Id = string.IsNullOrWhiteSpace(q.id) ? key + "_" + i : q.id,
                    Prompt = q.prompt.Trim(),
                    Aggressive = q.aggressive,
                    EvidenceKey = key
                });
            }

            return list;
        }

        /// <summary>Preferred sync path for interrogation — returns a spoken reply immediately.</summary>
        public bool TryResolve(ScenarioAskContext context, out GeminiSuspectReply reply)
        {
            reply = null;
            treeBank.EnsureLoaded();
            if (treeBank.TreeCount == 0) return false;

            DialogueTreeFile tree;
            if (!treeBank.TryGetTree(context != null ? context.SuspectId : null, out tree) || tree == null)
            {
                reply = FallbackReply(context);
                return true;
            }

            string evidenceKey = !string.IsNullOrWhiteSpace(context.AftermathDecision)
                ? context.AftermathDecision.Trim().ToLowerInvariant()
                : EvidenceKeyFromId(context.EvidenceId);

            if (string.IsNullOrEmpty(evidenceKey) && !string.IsNullOrWhiteSpace(context.Question))
            {
                string q = context.Question.Trim();
                if (string.Equals(q, "HOLD", StringComparison.OrdinalIgnoreCase)) evidenceKey = "hold";
                else if (string.Equals(q, "RELEASE", StringComparison.OrdinalIgnoreCase)) evidenceKey = "release";
            }

            DialogueTreeBranch branch = FindBranch(tree, evidenceKey);
            DialogueTreeQuestion question = FindQuestion(branch, context);
            if (question == null)
            {
                reply = FallbackReply(context);
                return true;
            }

            int stress = context != null && context.StressResult != null ? context.StressResult.Stress : 0;
            DialogueTreeReply pick = PickReply(question, stress);
            if (pick == null)
            {
                reply = FallbackReply(context);
                return true;
            }

            bool slip = pick.slip;
            if (context != null && context.IsCulprit && context.StressResult != null &&
                !string.IsNullOrWhiteSpace(context.StressResult.Slip) &&
                (context.StressResult.Tier == InterrogationStressTier.Cracking ||
                 context.StressResult.Tier == InterrogationStressTier.Broken))
            {
                slip = true;
            }

            string emotion = string.IsNullOrWhiteSpace(pick.emotion)
                ? EmotionForStress(context)
                : GeminiSuspectReply.NormalizeEmotion(pick.emotion);

            if (logMatches)
            {
                Debug.Log(
                    "ScenarioDialogueClient tree suspect=" + NormalizeSuspectKey(context.SuspectId) +
                    " evidence=" + evidenceKey +
                    " q=" + question.id +
                    " stress=" + stress +
                    " → " + pick.line,
                    this);
            }

            reply = GeminiSuspectReply.CreateFallback(
                GeminiApiClient.TrimToShortLine(pick.line),
                emotion,
                slip);
            return true;
        }

        /// <summary>HOLD / RELEASE aftermath line for the stamped suspect.</summary>
        public bool TryResolveAftermath(
            SuspectRecord suspect,
            SuspectDecision decision,
            InterrogationResult lastStress,
            bool isCulprit,
            out GeminiSuspectReply reply)
        {
            var ctx = new ScenarioAskContext
            {
                SuspectId = suspect != null ? suspect.Id : "*",
                SuspectName = suspect != null ? suspect.DisplayName : string.Empty,
                Question = decision == SuspectDecision.Hold ? "HOLD" : "RELEASE",
                AftermathDecision = decision == SuspectDecision.Hold ? "hold" : "release",
                IsCulprit = isCulprit,
                StressResult = lastStress,
                HasEvidence = false,
                Aggressive = false
            };
            return TryResolve(ctx, out reply);
        }

        /// <summary>Maps clue ids like UB-000_drew_access → access; alibi_support kept intact.</summary>
        public static string EvidenceKeyFromId(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId)) return string.Empty;
            string id = evidenceId.Trim().ToLowerInvariant();

            string[] keys =
            {
                "alibi_support",
                "red_herring",
                "sign_in",
                "access",
                "roster",
                "hold",
                "release"
            };

            for (int i = 0; i < keys.Length; i++)
            {
                if (id == keys[i] || id.EndsWith("_" + keys[i], StringComparison.Ordinal))
                    return keys[i];
            }

            int last = id.LastIndexOf('_');
            if (last >= 0 && last + 1 < id.Length)
                return id.Substring(last + 1);

            return id;
        }

        /// <summary>Maps suspect_drew / Drew / drew → drew.</summary>
        public static string NormalizeSuspectKey(string suspectIdOrName)
        {
            if (string.IsNullOrWhiteSpace(suspectIdOrName)) return "*";
            string s = suspectIdOrName.Trim().ToLowerInvariant();
            if (s.StartsWith("suspect_", StringComparison.Ordinal))
                s = s.Substring("suspect_".Length);
            int space = s.IndexOf(' ');
            if (space > 0) s = s.Substring(0, space);
            return s;
        }

        private static DialogueTreeBranch FindBranch(DialogueTreeFile tree, string evidenceKey)
        {
            if (tree == null || tree.branches == null || string.IsNullOrEmpty(evidenceKey))
                return null;

            for (int i = 0; i < tree.branches.Length; i++)
            {
                DialogueTreeBranch b = tree.branches[i];
                if (b == null) continue;
                if (string.Equals(b.evidenceKey, evidenceKey, StringComparison.OrdinalIgnoreCase))
                    return b;
            }

            return null;
        }

        private static DialogueTreeQuestion FindQuestion(DialogueTreeBranch branch, ScenarioAskContext context)
        {
            if (branch == null || branch.questions == null || branch.questions.Length == 0)
                return null;

            if (context != null && !string.IsNullOrWhiteSpace(context.QuestionId))
            {
                for (int i = 0; i < branch.questions.Length; i++)
                {
                    DialogueTreeQuestion q = branch.questions[i];
                    if (q != null && string.Equals(q.id, context.QuestionId, StringComparison.OrdinalIgnoreCase))
                        return q;
                }
            }

            if (context != null && !string.IsNullOrWhiteSpace(context.Question))
            {
                string prompt = context.Question.Trim();
                for (int i = 0; i < branch.questions.Length; i++)
                {
                    DialogueTreeQuestion q = branch.questions[i];
                    if (q != null && !string.IsNullOrWhiteSpace(q.prompt) &&
                        string.Equals(q.prompt.Trim(), prompt, StringComparison.OrdinalIgnoreCase))
                        return q;
                }
            }

            // Aftermath / single-node branches
            if (branch.questions.Length == 1)
                return branch.questions[0];

            return null;
        }

        private DialogueTreeReply PickReply(DialogueTreeQuestion question, int stress)
        {
            if (question == null || question.replies == null || question.replies.Length == 0)
                return null;

            var matches = new List<DialogueTreeReply>(4);
            for (int i = 0; i < question.replies.Length; i++)
            {
                DialogueTreeReply r = question.replies[i];
                if (r == null) continue;
                if (stress < r.minStress || stress > r.maxStress) continue;
                matches.Add(r);
            }

            if (matches.Count == 0)
            {
                // Soft: nearest by mid-band
                DialogueTreeReply best = null;
                int bestDist = int.MaxValue;
                for (int i = 0; i < question.replies.Length; i++)
                {
                    DialogueTreeReply r = question.replies[i];
                    if (r == null) continue;
                    int mid = (r.minStress + r.maxStress) / 2;
                    int dist = Math.Abs(mid - stress);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = r;
                    }
                }

                return best;
            }

            return matches[rng.Next(matches.Count)];
        }

        private static GeminiSuspectReply FallbackReply(ScenarioAskContext context)
        {
            string line = context != null && context.StressResult != null &&
                          !string.IsNullOrWhiteSpace(context.StressResult.FallbackLine)
                ? context.StressResult.FallbackLine
                : "I already answered that.";
            return GeminiSuspectReply.CreateFallback(
                GeminiApiClient.TrimToShortLine(line),
                EmotionForStress(context),
                false);
        }

        private static string EmotionForStress(ScenarioAskContext context)
        {
            if (context == null || context.StressResult == null) return "calm";
            switch (context.StressResult.Tier)
            {
                case InterrogationStressTier.Nervous: return "nervous";
                case InterrogationStressTier.Cracking: return "scared";
                case InterrogationStressTier.Broken: return "broken";
                default: return "calm";
            }
        }

        private static string ToJson(GeminiSuspectReply reply)
        {
            string line = reply != null ? reply.line : "...";
            string emotion = reply != null ? reply.emotion : "calm";
            bool slip = reply != null && reply.slip;
            return "{\"line\":\"" + Escape(line) + "\",\"emotion\":\"" + Escape(emotion) + "\",\"slip\":" +
                   (slip ? "true" : "false") + "}";
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
