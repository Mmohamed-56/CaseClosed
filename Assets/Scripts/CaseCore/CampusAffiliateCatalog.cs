using System;
using System.Collections.Generic;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Loads the campus affiliate pool from Resources/Campus/affiliates.json.
    /// Interviewee tier ≈ playable interrogation pool; witness tier = records/bios + light lines.
    /// Clue id contract for interviewees stays <c>{caseId}_{id}_{suffix}</c> via CaseGenerator.
    /// </summary>
    public static class CampusAffiliateCatalog
    {
        private const string ResourcePath = "Campus/affiliates";
        private static List<CampusAffiliate> all;
        private static List<CampusAffiliate> interviewees;
        private static List<CampusAffiliate> witnesses;
        private static bool loaded;

        [Serializable]
        private sealed class AffiliateFile
        {
            public int schemaVersion = 1;
            public int count;
            public CampusAffiliate[] affiliates;
        }

        [Serializable]
        public sealed class CampusAffiliate
        {
            public string id;
            public string displayName;
            public string campusRole;
            public string tier = "witness";
            public string bio;
            public string statement;
            public string alibi;
            public string redHerringRecord;
            public string redHerringExplanation;
            public string alibiSupportTitle;
            public string alibiSupportDescription;
            public string contextTitleA;
            public string contextDescriptionA;
            /// <summary>Mix slots for the low-poly pack. Beside voice gender via SuspectVoiceCatalog.</summary>
            public SuspectLook look;
            public RecordHook[] recordHooks;
        }

        [Serializable]
        public sealed class RecordHook
        {
            public string suffix;
            public string title;
            public string description;
        }

        public static int TotalCount
        {
            get
            {
                EnsureLoaded();
                return all.Count;
            }
        }

        public static IReadOnlyList<CampusAffiliate> All
        {
            get
            {
                EnsureLoaded();
                return all;
            }
        }

        public static IReadOnlyList<CampusAffiliate> Interviewees
        {
            get
            {
                EnsureLoaded();
                return interviewees;
            }
        }

        public static IReadOnlyList<CampusAffiliate> Witnesses
        {
            get
            {
                EnsureLoaded();
                return witnesses;
            }
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        /// <summary>
        /// Look slots authored on the affiliate (body, hair, top, bottoms, and the rest).
        /// Accepts <c>jamie</c> or <c>suspect_jamie</c>. Null when this person has no look yet.
        /// </summary>
        public static SuspectLook TryGetLook(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(id) || all == null)
                return null;

            string key = id.Trim().ToLowerInvariant();
            const string prefix = "suspect_";
            if (key.StartsWith(prefix))
                key = key.Substring(prefix.Length);

            for (int i = 0; i < all.Count; i++)
            {
                CampusAffiliate affiliate = all[i];
                if (affiliate == null || affiliate.id != key || affiliate.look == null)
                    continue;
                if (string.IsNullOrWhiteSpace(affiliate.look.body))
                    continue;
                return affiliate.look;
            }

            return null;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            all = new List<CampusAffiliate>(128);
            interviewees = new List<CampusAffiliate>(16);
            witnesses = new List<CampusAffiliate>(128);

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("CampusAffiliateCatalog: missing Resources/Campus/affiliates.json");
                return;
            }

            AffiliateFile file;
            try
            {
                file = JsonUtility.FromJson<AffiliateFile>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CampusAffiliateCatalog: parse failed — " + ex.Message);
                return;
            }

            if (file == null || file.affiliates == null) return;
            for (int i = 0; i < file.affiliates.Length; i++)
            {
                CampusAffiliate a = file.affiliates[i];
                if (a == null || string.IsNullOrWhiteSpace(a.id)) continue;
                a.id = a.id.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(a.tier)) a.tier = "witness";
                all.Add(a);
                if (string.Equals(a.tier, "interviewee", StringComparison.OrdinalIgnoreCase))
                    interviewees.Add(a);
                else
                    witnesses.Add(a);
            }
        }

        /// <summary>
        /// Builds public directory rows for the records site. Ids use <c>affiliate_{id}</c>
        /// so they never collide with interrogated <c>suspect_{id}</c> clue owners.
        /// Record ids: <c>{caseId}_aff_{id}_{suffix}</c>.
        /// </summary>
        public static List<WebsiteSuspectRecord> BuildDirectoryPayload(string caseId, int seed, int maxWitnesses)
        {
            EnsureLoaded();
            var list = new List<WebsiteSuspectRecord>();
            if (string.IsNullOrWhiteSpace(caseId)) return list;

            // Always include interviewee directory bios (public), even if not drawn this shift.
            for (int i = 0; i < interviewees.Count; i++)
                list.Add(ToWebsiteRecord(caseId, interviewees[i], includeJobHooks: false));

            if (witnesses.Count == 0 || maxWitnesses <= 0) return list;

            var rng = new System.Random(seed ^ unchecked((int)0xC4FEA771));
            var bag = new List<CampusAffiliate>(witnesses);
            int take = Mathf.Min(maxWitnesses, bag.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = rng.Next(bag.Count);
                CampusAffiliate w = bag[pick];
                bag.RemoveAt(pick);
                list.Add(ToWebsiteRecord(caseId, w, includeJobHooks: true));
            }

            return list;
        }

        private static WebsiteSuspectRecord ToWebsiteRecord(string caseId, CampusAffiliate a, bool includeJobHooks)
        {
            var row = new WebsiteSuspectRecord
            {
                id = "affiliate_" + a.id,
                name = a.displayName ?? a.id,
                role = a.campusRole ?? string.Empty,
                statement = string.IsNullOrWhiteSpace(a.bio) ? (a.statement ?? string.Empty) : a.bio
            };

            if (a.recordHooks != null)
            {
                for (int i = 0; i < a.recordHooks.Length; i++)
                {
                    RecordHook hook = a.recordHooks[i];
                    if (hook == null || string.IsNullOrWhiteSpace(hook.suffix)) continue;
                    if (!includeJobHooks && hook.suffix != "directory") continue;
                    row.records.Add(new WebsiteEvidenceRecord
                    {
                        id = caseId + "_aff_" + a.id + "_" + hook.suffix,
                        title = hook.title ?? hook.suffix,
                        description = hook.description ?? string.Empty
                    });
                }
            }

            return row;
        }
    }
}
