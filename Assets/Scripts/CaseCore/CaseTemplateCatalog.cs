using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Loads distinct case templates from Resources/Cases/case_templates.json.
    /// Each template supplies title, briefing, evidence copy, tree hook, and optional suspect prefs.
    /// CollectEvidence id suffixes stay fixed: roster, alibi_support, sign_in, access, red_herring.
    /// </summary>
    public static class CaseTemplateCatalog
    {
        private const string ResourcePath = "Cases/case_templates";
        private static List<CaseTemplate> templates;
        private static bool loaded;

        [Serializable]
        private sealed class TemplateFile
        {
            public int schemaVersion = 1;
            public int count;
            public CaseTemplate[] templates;
        }

        [Serializable]
        public sealed class CaseTemplate
        {
            public string id;
            public string title;
            /// <summary>easy, medium, or hard. Hard briefings need the access card read with sign-in and alibi or roster.</summary>
            public string difficulty;
            /// <summary>Public motive. No culprit name.</summary>
            public string motive;
            /// <summary>Unity-side solve note. Not copied into the public briefing.</summary>
            public string solvePath;
            public string briefing;
            public string treeHook;
            public string scene;
            /// <summary>What they did, as a verb phrase after "I ". Slip lines fill {act} from this at speak time.</summary>
            public string act;
            public string windowStart;
            public string windowEnd;
            public string accessTime;
            public string accessTitle;
            public string accessDescription;
            public string contradictionExplanation;
            public string signInTitle;
            public string signInDescription;
            public string rosterTitle;
            public string rosterDescription;
            public string alibiTitle;
            public string alibiDescription;
            public string redHerringTitle;
            public string redHerringDescription;
            public string redHerringExplanation;
            public string statementTemplate;
            public string[] preferSuspectIds;
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return templates.Count;
            }
        }

        public static IReadOnlyList<CaseTemplate> All
        {
            get
            {
                EnsureLoaded();
                return templates;
            }
        }

        /// <summary>Stable pick for a seed. Consecutive seeds rotate through titles.</summary>
        public static CaseTemplate PickForSeed(int seed)
        {
            EnsureLoaded();
            if (templates.Count == 0)
                return EmbeddedFallback()[0];

            int index = seed % templates.Count;
            if (index < 0) index += templates.Count;
            return templates[index];
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            templates = new List<CaseTemplate>();

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    TemplateFile file = JsonUtility.FromJson<TemplateFile>(asset.text);
                    if (file != null && file.templates != null)
                    {
                        for (int i = 0; i < file.templates.Length; i++)
                        {
                            CaseTemplate t = file.templates[i];
                            if (t == null || string.IsNullOrWhiteSpace(t.id) || string.IsNullOrWhiteSpace(t.title))
                                continue;
                            templates.Add(Normalize(t));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CaseTemplateCatalog] Failed to parse case_templates.json: " + ex.Message);
                }
            }

            if (templates.Count == 0)
                templates.AddRange(EmbeddedFallback());
        }

        private static CaseTemplate Normalize(CaseTemplate t)
        {
            if (string.IsNullOrWhiteSpace(t.treeHook)) t.treeHook = t.id;
            if (string.IsNullOrWhiteSpace(t.difficulty)) t.difficulty = "medium";
            else t.difficulty = t.difficulty.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(t.accessTitle)) t.accessTitle = "Restricted access log";
            if (string.IsNullOrWhiteSpace(t.accessDescription))
                t.accessDescription = "A campus credential for {name} opened a door during the hours in the briefing.";
            if (string.IsNullOrWhiteSpace(t.contradictionExplanation))
                t.contradictionExplanation = "{name} went back during those hours.";
            if (string.IsNullOrWhiteSpace(t.signInTitle)) t.signInTitle = "Activity sign-in sheet";
            if (string.IsNullOrWhiteSpace(t.signInDescription))
                t.signInDescription = "{name} signed a sheet near the building before it happened.";
            if (string.IsNullOrWhiteSpace(t.redHerringTitle)) t.redHerringTitle = "Suspicious purchase record";
            if (string.IsNullOrWhiteSpace(t.redHerringDescription))
                t.redHerringDescription = "A purchase or checkout under {name}'s name looks related to the incident at first glance.";
            if (string.IsNullOrWhiteSpace(t.redHerringExplanation))
                t.redHerringExplanation = "The record has an innocent explanation unrelated to the incident.";
            if (string.IsNullOrWhiteSpace(t.statementTemplate))
                t.statementTemplate = "I left before it happened, and I didn't go back.";
            if (t.preferSuspectIds == null) t.preferSuspectIds = Array.Empty<string>();
            return t;
        }

        private static List<CaseTemplate> EmbeddedFallback()
        {
            return new List<CaseTemplate>
            {
                Normalize(new CaseTemplate
                {
                    id = "banner_cutdown",
                    title = "Field House Spirit Banner Cutdown",
                    briefing =
                        "WHERE: Field House, mezzanine over the court.\nWHEN: 6:05 PM to 6:35 PM.\nWHAT: Somebody cut down the Founders Weekend spirit banner.\nMOTIVE: Embarrass the weekend before anyone could hang it back up.\n\nMezzanine camera still.\nAthletics volunteer sheet.\nA hardware receipt for a folding knife.",
                    treeHook = "banner_cutdown",
                    scene = "Field House mezzanine",
                    act = "cut down the spirit banner",
                    windowStart = "6:05 PM",
                    windowEnd = "6:35 PM",
                    accessTime = "6:21 PM",
                    accessTitle = "Mezzanine catwalk camera still",
                    accessDescription =
                        "A 6:21 PM still from catwalk cam FH-M3 shows a figure matching {name}'s clothing profile on the banner walkway — after {name} claimed to be at dinner across campus.",
                    contradictionExplanation = "{name} climbed the Field House mezzanine and cut down the spirit banner.",
                    signInTitle = "Athletics volunteer day sheet",
                    signInDescription =
                        "{name} signed the afternoon Field House athletics volunteer sheet before the cutdown window.",
                    redHerringTitle = "Utility-knife purchase",
                    redHerringDescription =
                        "A campus-adjacent hardware debit under {name}'s card bought a folding utility knife the same afternoon.",
                    redHerringExplanation =
                        "{name} used the knife to open taped spirit posters for a club table — not the banner ropes.",
                    statementTemplate =
                        "I never went up to the Field House mezzanine. I was at dinner when the banner came down.",
                    preferSuspectIds = new[] { "jamie", "casey", "quinn", "sam", "drew", "morgan", "reese", "jordan" }
                })
            };
        }

        public static string FormatName(string template, string displayName)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            string name = string.IsNullOrWhiteSpace(displayName) ? "the suspect" : displayName.Trim();
            return template.Replace("{name}", name);
        }
    }
}
