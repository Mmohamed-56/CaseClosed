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
            DialogueTreeReply pick = PickReply(question, stress, context);
            if (pick == null)
            {
                reply = FallbackReply(context);
                return true;
            }

            // Slip flag follows the authored line — never auto-mark casual mid-stress answers as confessions.
            bool slip = pick.slip;
            if (context != null && context.IsCulprit && context.StressResult != null &&
                context.StressResult.Tier == InterrogationStressTier.Broken &&
                !string.IsNullOrWhiteSpace(context.StressResult.Slip) &&
                stress >= 85)
            {
                slip = true;
            }

            string emotion = string.IsNullOrWhiteSpace(pick.emotion)
                ? EmotionForStress(context)
                : GeminiSuspectReply.NormalizeEmotion(pick.emotion);

            string spoken = FillSpokenAct(
                pick.line,
                context != null ? context.SpokenAct : null,
                context != null ? context.SpokenAlibi : null);
            if (logMatches)
            {
                Debug.Log(
                    "ScenarioDialogueClient tree suspect=" + NormalizeSuspectKey(context.SuspectId) +
                    " evidence=" + evidenceKey +
                    " q=" + question.id +
                    " stress=" + stress +
                    " → " + spoken,
                    this);
            }

            reply = GeminiSuspectReply.CreateFallback(
                GeminiApiClient.TrimToShortLine(spoken),
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
            out GeminiSuspectReply reply,
            bool hasSlipped = false,
            string spokenAct = null,
            string spokenAlibi = null)
        {
            var ctx = new ScenarioAskContext
            {
                SuspectId = suspect != null ? suspect.Id : "*",
                SuspectName = suspect != null ? suspect.DisplayName : string.Empty,
                Question = decision == SuspectDecision.Hold ? "HOLD" : "RELEASE",
                AftermathDecision = decision == SuspectDecision.Hold ? "hold" : "release",
                IsCulprit = isCulprit,
                HasSlipped = hasSlipped,
                StressResult = lastStress,
                HasEvidence = false,
                Aggressive = false,
                SpokenAct = spokenAct,
                SpokenAlibi = spokenAlibi
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

        private DialogueTreeReply PickReply(DialogueTreeQuestion question, int stress, ScenarioAskContext context)
        {
            if (question == null || question.replies == null || question.replies.Length == 0)
                return null;

            var matches = new List<DialogueTreeReply>(4);
            for (int i = 0; i < question.replies.Length; i++)
            {
                DialogueTreeReply r = question.replies[i];
                if (!PassesRole(r, context)) continue;
                if (stress < r.minStress || stress > r.maxStress) continue;
                matches.Add(r);
            }

            if (matches.Count == 0)
                matches = NearestBandMatches(question, stress, context);

            var gated = new List<DialogueTreeReply>(matches);
            ApplySlipGates(gated, stress, context);
            if (gated.Count > 0)
            {
                matches = gated;
            }
            else
            {
                // Gating wiped the band (casual slip-only mid tiers) — fall back to nearest non-slip line.
                var nonSlip = new List<DialogueTreeReply>(4);
                for (int i = 0; i < question.replies.Length; i++)
                {
                    DialogueTreeReply r = question.replies[i];
                    if (PassesRole(r, context) && !r.slip)
                        nonSlip.Add(r);
                }

                if (nonSlip.Count > 0)
                    matches = NearestFromList(nonSlip, stress);
            }

            if (matches.Count == 0)
                return null;

            EnsureOwnedConfession(question, matches, stress, context);
            if (matches.Count == 0)
                return null;

            // Prefer the tightest authored band so wide 0–100 denial lines lose to 60–100 hold confessions.
            matches.Sort((a, b) =>
            {
                int spanA = a.maxStress - a.minStress;
                int spanB = b.maxStress - b.minStress;
                int cmp = spanA.CompareTo(spanB);
                if (cmp != 0) return cmp;
                return b.minStress.CompareTo(a.minStress);
            });

            int tightest = matches[0].maxStress - matches[0].minStress;
            var tight = new List<DialogueTreeReply>(matches.Count);
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].maxStress - matches[i].minStress == tightest)
                    tight.Add(matches[i]);
            }

            return tight[rng.Next(tight.Count)];
        }

        private static bool PassesRole(DialogueTreeReply reply, ScenarioAskContext context)
        {
            if (reply == null) return false;
            bool culprit = context != null && context.IsCulprit;
            if (reply.onlyIfCulprit && !culprit) return false;
            if (reply.onlyIfInnocent && culprit) return false;
            return true;
        }

        /// <summary>
        /// After a confession, HOLD/RELEASE must keep an owning line even if stress has dropped
        /// out of the slip band. Otherwise a culprit denies a crime they already admitted.
        /// </summary>
        private static void EnsureOwnedConfession(
            DialogueTreeQuestion question,
            List<DialogueTreeReply> matches,
            int stress,
            ScenarioAskContext context)
        {
            if (question == null || matches == null || context == null)
                return;
            if (string.IsNullOrWhiteSpace(context.AftermathDecision))
                return;

            bool mustOwn = context.HasSlipped || (context.IsCulprit && stress >= 85);
            if (!mustOwn)
                return;

            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i] != null && matches[i].slip)
                    return;
            }

            matches.Clear();
            if (question.replies == null) return;
            for (int i = 0; i < question.replies.Length; i++)
            {
                DialogueTreeReply reply = question.replies[i];
                if (reply != null && reply.slip && PassesRole(reply, context))
                    matches.Add(reply);
            }
        }

        private static List<DialogueTreeReply> NearestBandMatches(
            DialogueTreeQuestion question,
            int stress,
            ScenarioAskContext context)
        {
            var all = new List<DialogueTreeReply>(question.replies.Length);
            for (int i = 0; i < question.replies.Length; i++)
            {
                if (PassesRole(question.replies[i], context))
                    all.Add(question.replies[i]);
            }

            return NearestFromList(all, stress);
        }

        private static List<DialogueTreeReply> NearestFromList(List<DialogueTreeReply> replies, int stress)
        {
            var list = new List<DialogueTreeReply>(2);
            int bestDist = int.MaxValue;
            for (int i = 0; i < replies.Count; i++)
            {
                DialogueTreeReply r = replies[i];
                if (r == null) continue;
                int mid = (r.minStress + r.maxStress) / 2;
                int dist = Math.Abs(mid - stress);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    list.Clear();
                    list.Add(r);
                }
                else if (dist == bestDist)
                {
                    list.Add(r);
                }
            }

            return list;
        }

        /// <summary>
        /// Gate casual confessions during Q&amp;A; keep HOLD/RELEASE consistent with prior slips.
        /// </summary>
        private static void ApplySlipGates(List<DialogueTreeReply> matches, int stress, ScenarioAskContext context)
        {
            if (matches == null || matches.Count == 0) return;

            bool isAftermath = context != null && !string.IsNullOrWhiteSpace(context.AftermathDecision);
            bool hasSlipped = context != null && context.HasSlipped;
            bool isCulprit = context != null && context.IsCulprit;

            if (!isAftermath)
            {
                // Never auto-confess casually — slip-flagged lines only at Broken (85+).
                if (stress < 85)
                    RemoveWhere(matches, r => r.slip);
                return;
            }

            // HOLD/RELEASE: if they already confessed (or broken culprit), prefer slip lines — never deny after.
            bool mustOwnIt = hasSlipped || (isCulprit && stress >= 85);
            if (mustOwnIt && HasAny(matches, r => r.slip))
            {
                RemoveWhere(matches, r => !r.slip);
                return;
            }

            if (isCulprit && stress >= 60 && HasAny(matches, r => r.slip))
            {
                RemoveWhere(matches, r => !r.slip);
                return;
            }

            // Innocent / low-stress HOLD: keep denials, drop leftover slip tags.
            RemoveWhere(matches, r => r.slip);
        }

        private static bool HasAny(List<DialogueTreeReply> matches, Predicate<DialogueTreeReply> pred)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                if (pred(matches[i])) return true;
            }
            return false;
        }

        private static void RemoveWhere(List<DialogueTreeReply> matches, Predicate<DialogueTreeReply> pred)
        {
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (pred(matches[i]))
                    matches.RemoveAt(i);
            }
        }

        /// <summary>
        /// Shared trees store {act} and {alibi}. The chair's case supplies both, so the same line names a different crime and a different place on the next case. Braces are gone before ElevenLabs speaks.
        /// </summary>
        private static string FillSpokenAct(string line, string act, string spokenAlibi)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;
            if (line.IndexOf("{act}", StringComparison.Ordinal) >= 0)
            {
                string phrase = string.IsNullOrWhiteSpace(act) ? "did this" : act.Trim();
                line = line.Replace("{act}", phrase);
            }

            string alibi = string.IsNullOrWhiteSpace(spokenAlibi)
                ? "where I said I was"
                : spokenAlibi.Trim();
            line = line.Replace("{alibi}", alibi);
            return line;
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
