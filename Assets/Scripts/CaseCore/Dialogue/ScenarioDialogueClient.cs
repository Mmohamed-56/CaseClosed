using System;
using System.Collections.Generic;
using CaseClosed.CaseCore.Gemini;
using UnityEngine;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>
    /// Offline dialogue client with the same ask → {line, emotion, slip} shape Gemini used.
    /// Keyword/tag matching + stress gates. Never networks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioDialogueClient : MonoBehaviour
    {
        [SerializeField] private bool logMatches;

        private readonly ScenarioDialogueBank bank = new ScenarioDialogueBank();
        private readonly System.Random rng = new System.Random();
        private int recentSalt;

        public int LoadedEntryCount
        {
            get
            {
                bank.EnsureLoaded();
                return bank.EntryCount;
            }
        }

        public bool IsReady
        {
            get
            {
                bank.EnsureLoaded();
                return bank.EntryCount > 0;
            }
        }

        private void Awake()
        {
            bank.EnsureLoaded();
        }

        /// <summary>Reload JSON from Resources (Editor polish / overnight content drops).</summary>
        public void ReloadBank()
        {
            bank.Reload();
        }

        /// <summary>
        /// Same callback shape as <see cref="GeminiApiClient.Generate"/> consumers expect:
        /// onComplete receives a JSON string <c>{"line","emotion","slip"}</c>.
        /// Always succeeds offline when the bank has any lines.
        /// </summary>
        public void Ask(
            ScenarioAskContext context,
            Action<string> onComplete,
            Action<string> onError)
        {
            GeminiSuspectReply reply;
            if (!TryResolve(context, out reply))
            {
                onError?.Invoke("Scenario bank returned no line.");
                return;
            }

            onComplete?.Invoke(ToJson(reply));
        }

        /// <summary>Preferred sync path for interrogation — returns a spoken reply immediately.</summary>
        public bool TryResolve(ScenarioAskContext context, out GeminiSuspectReply reply)
        {
            reply = null;
            bank.EnsureLoaded();
            if (bank.EntryCount == 0) return false;

            string intent = DetectIntent(context);
            int stress = context != null && context.StressResult != null ? context.StressResult.Stress : 0;

            List<ScenarioLineEntry> matches = CollectMatches(context, intent, stress, strict: true);
            if (matches.Count == 0)
                matches = CollectMatches(context, intent, stress, strict: false);
            if (matches.Count == 0)
                matches = CollectMatches(context, ScenarioIntents.Generic, stress, strict: false);

            if (matches.Count == 0)
            {
                reply = GeminiSuspectReply.CreateFallback(
                    context != null && context.StressResult != null && !string.IsNullOrWhiteSpace(context.StressResult.FallbackLine)
                        ? context.StressResult.FallbackLine
                        : "I already answered that.",
                    EmotionForStress(context),
                    false);
                return true;
            }

            ScenarioLineEntry pick = matches[rng.Next(matches.Count)];
            recentSalt++;

            bool slip = pick.slip;
            if (context != null && context.StressResult != null &&
                !string.IsNullOrWhiteSpace(context.StressResult.Slip) &&
                (intent == ScenarioIntents.Slip ||
                 context.StressResult.Tier == InterrogationStressTier.Cracking ||
                 context.StressResult.Tier == InterrogationStressTier.Broken) &&
                context.IsCulprit)
            {
                // Prefer authored slip lines; still mark slip when CaseCore says so.
                if (string.Equals(pick.intent, ScenarioIntents.Slip, StringComparison.OrdinalIgnoreCase))
                    slip = true;
            }

            string emotion = string.IsNullOrWhiteSpace(pick.emotion)
                ? EmotionForStress(context)
                : GeminiSuspectReply.NormalizeEmotion(pick.emotion);

            if (logMatches)
            {
                Debug.Log(
                    "ScenarioDialogueClient match intent=" + intent +
                    " stress=" + stress +
                    " candidates=" + matches.Count +
                    " id=" + pick.id +
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
                AftermathDecision = decision == SuspectDecision.Hold ? ScenarioIntents.Hold : ScenarioIntents.Release,
                IsCulprit = isCulprit,
                StressResult = lastStress,
                HasEvidence = false,
                Aggressive = false
            };
            return TryResolve(ctx, out reply);
        }

        private List<ScenarioLineEntry> CollectMatches(
            ScenarioAskContext context,
            string intent,
            int stress,
            bool strict)
        {
            var list = new List<ScenarioLineEntry>(32);
            string suspectKey = NormalizeSuspectKey(context != null ? context.SuspectId : null);
            string question = context != null && context.Question != null
                ? context.Question.Trim().ToLowerInvariant()
                : string.Empty;

            IReadOnlyList<ScenarioLineEntry> all = bank.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                ScenarioLineEntry e = all[i];
                if (e == null) continue;

                if (!SuspectMatches(e.suspectId, suspectKey)) continue;
                if (stress < e.minStress || stress > e.maxStress) continue;

                if (e.culpritOnly && (context == null || !context.IsCulprit)) continue;
                if (e.innocentOnly && (context == null || context.IsCulprit)) continue;

                if (e.requiresEvidence && (context == null || !context.HasEvidence)) continue;
                if (e.requiresAggressive && (context == null || !context.Aggressive)) continue;

                if (context != null && context.IsShutdown &&
                    !string.Equals(e.intent, ScenarioIntents.Shutdown, StringComparison.OrdinalIgnoreCase) &&
                    strict)
                    continue;

                if (!string.Equals(e.intent, intent, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(e.intent, ScenarioIntents.Generic, StringComparison.OrdinalIgnoreCase))
                {
                    // Aftermath intents must match exactly.
                    if (intent == ScenarioIntents.Hold || intent == ScenarioIntents.Release ||
                        intent == ScenarioIntents.Shutdown || intent == ScenarioIntents.Slip)
                        continue;
                    if (strict) continue;
                }

                if (strict && !string.Equals(e.intent, intent, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (strict && !string.IsNullOrWhiteSpace(e.keywords) && !string.IsNullOrEmpty(question))
                {
                    if (!KeywordsHit(e.keywords, question)) continue;
                }

                list.Add(e);
            }

            // Soft pass: same intent, ignore keyword miss.
            if (list.Count == 0 && strict)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    ScenarioLineEntry e = all[i];
                    if (e == null) continue;
                    if (!SuspectMatches(e.suspectId, suspectKey)) continue;
                    if (stress < e.minStress || stress > e.maxStress) continue;
                    if (e.culpritOnly && (context == null || !context.IsCulprit)) continue;
                    if (e.innocentOnly && (context == null || context.IsCulprit)) continue;
                    if (e.requiresEvidence && (context == null || !context.HasEvidence)) continue;
                    if (e.requiresAggressive && (context == null || !context.Aggressive)) continue;
                    if (!string.Equals(e.intent, intent, StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(e);
                }
            }

            return list;
        }

        public static string DetectIntent(ScenarioAskContext context)
        {
            if (context == null) return ScenarioIntents.Generic;

            if (!string.IsNullOrWhiteSpace(context.AftermathDecision))
                return context.AftermathDecision.Trim().ToLowerInvariant();

            if (context.IsShutdown)
                return ScenarioIntents.Shutdown;

            if (context.StressResult != null &&
                !string.IsNullOrWhiteSpace(context.StressResult.Slip) &&
                (context.StressResult.Tier == InterrogationStressTier.Cracking ||
                 context.StressResult.Tier == InterrogationStressTier.Broken) &&
                context.IsCulprit &&
                context.Aggressive)
                return ScenarioIntents.Slip;

            string q = context.Question != null ? context.Question.ToLowerInvariant() : string.Empty;

            if (context.HasEvidence || ContainsAny(q, "evidence", "this log", "this receipt", "this record", "look at this", "see this", "prove", "card opened", "access log"))
                return ScenarioIntents.Evidence;

            if (ContainsAny(q, "you took", "you stole", "you're lying", "you did it", "admit", "confess", "guilty", "thief", "mascot head"))
                return ScenarioIntents.Accusation;

            if (context.Aggressive || ContainsAny(q, "enough", "stop lying", "cut the", "pressure", "don't play", "we know", "last chance"))
                return ScenarioIntents.Pressure;

            if (ContainsAny(q, "where were", "where was", "alibi", "what time", "when did", "bus", "home", "patrol", "study", "lab", "cafe", "annex", "library"))
                return ScenarioIntents.Alibi;

            if (ContainsAny(q, "how are", "name", "who are", "tell me about", "what do you do", "role", "job", "major", "hello", "hi "))
                return ScenarioIntents.SmallTalk;

            return ScenarioIntents.Generic;
        }

        private static bool KeywordsHit(string keywords, string questionLower)
        {
            string[] parts = keywords.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string k = parts[i].Trim().ToLowerInvariant();
                if (k.Length > 0 && questionLower.IndexOf(k, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            for (int i = 0; i < needles.Length; i++)
            {
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool SuspectMatches(string entrySuspectId, string normalizedAskId)
        {
            if (string.IsNullOrWhiteSpace(entrySuspectId) || entrySuspectId == "*") return true;
            string entryKey = NormalizeSuspectKey(entrySuspectId);
            return entryKey == normalizedAskId || entryKey == "*";
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
