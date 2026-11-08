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
            public string briefing;
            public string treeHook;
            public string scene;
            public string windowStart;
            public string windowEnd;
            public string accessTime;
            public string accessTitle;
            public string accessDescription;
            public string contradictionExplanation;
            public string signInTitle;
            public string signInDescription;
            public string redHerringTitle;
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
            if (string.IsNullOrWhiteSpace(t.accessTitle)) t.accessTitle = "Restricted access log";
            if (string.IsNullOrWhiteSpace(t.accessDescription))
                t.accessDescription = "A campus credential assigned to {name} opened a restricted door inside the incident window.";
            if (string.IsNullOrWhiteSpace(t.contradictionExplanation))
                t.contradictionExplanation = "{name} returned during the incident window.";
            if (string.IsNullOrWhiteSpace(t.signInTitle)) t.signInTitle = "Activity sign-in sheet";
            if (string.IsNullOrWhiteSpace(t.signInDescription))
                t.signInDescription = "{name} signed in near the scene earlier that evening.";
            if (string.IsNullOrWhiteSpace(t.redHerringTitle)) t.redHerringTitle = "Suspicious purchase record";
            if (string.IsNullOrWhiteSpace(t.statementTemplate))
                t.statementTemplate = "I left before the incident window and never went back.";
            if (t.preferSuspectIds == null) t.preferSuspectIds = Array.Empty<string>();
            return t;
        }

        private static List<CaseTemplate> EmbeddedFallback()
        {
            return new List<CaseTemplate>
            {
                Normalize(new CaseTemplate
                {
                    id = "mascot_head",
                    title = "The Missing Mascot Head",
                    briefing =
                        "The university mascot head vanished from the student-union storage room between 9:15 PM and 9:45 PM. Check the records against each suspect's statement before deciding who to hold.",
                    treeHook = "mascot_theft",
                    scene = "the student-union storage wing",
                    windowStart = "9:15 PM",
                    windowEnd = "9:45 PM",
                    accessTime = "9:37 PM",
                    accessTitle = "Storage-room access log",
                    accessDescription =
                        "The campus card assigned to {name} opened the event-storage room at 9:37 PM, inside the theft window and after their stated departure time.",
                    contradictionExplanation = "{name} returned to the storage room and took the mascot head.",
                    signInTitle = "Event sign-in sheet",
                    signInDescription =
                        "{name} signed in to help with student-union event setup earlier that evening.",
                    redHerringTitle = "Suspicious purchase record",
                    statementTemplate =
                        "I left the student union before 9:15 PM and was already gone when the head went missing.",
                    preferSuspectIds = new[] { "jamie", "morgan", "casey", "drew", "quinn", "sam", "avery", "riley" }
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
