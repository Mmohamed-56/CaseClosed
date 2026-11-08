using System;
using System.Globalization;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Creates reproducible, locally authored cases without using AI to decide the truth.
    /// Culprit identity and evidence Kind/Explanation stay Unity-only; the website payload
    /// builder must never receive those fields (see <see cref="CaseWebsitePayloadBuilder"/>).
    /// Case title/briefing/evidence copy come from <see cref="CaseTemplateCatalog"/> (seeded pick).
    /// </summary>
    public static class CaseGenerator
    {
        private const int MinimumSuspectCount = 2;
        private const int MaximumSuspectCount = 4;

        /// <summary>
        /// Embedded fallback interviewee pool (original 8). Prefer
        /// <see cref="CampusAffiliateCatalog.Interviewees"/> when Resources JSON is present (~12 playable).
        /// Clue id suffixes stay: roster, alibi_support, sign_in, access, red_herring.
        /// </summary>
        private static readonly SuspectProfile[] SuspectPool =
        {
            new SuspectProfile(
                "jamie",
                "Jamie Rivera",
                "Student volunteer",
                "I left the student union before 9:15 PM and went straight to the bus stop.",
                "At the west bus loop from 9:20 PM onward.",
                "A hardware-store receipt shows Jamie bought a utility knife that afternoon.",
                "Jamie used it to cut tape and cardboard for event decorations.",
                "Transit receipt",
                "A transit-card tap places Jamie at the west bus loop at 9:24 PM.",
                "Volunteer roster",
                "Jamie is listed on the evening setup crew for the student-union event."),
            new SuspectProfile(
                "morgan",
                "Morgan Lee",
                "Student events assistant",
                "I left the student union before 9:15 PM and went home.",
                "At home from 9:10 PM onward.",
                "An event sign-out sheet shows Morgan borrowed the storage-room key earlier that day.",
                "Morgan was inventorying decorations and returned the key before the theft window.",
                "Residence hall door log",
                "Morgan's hall exterior door registered an entry at 9:18 PM.",
                "Staff schedule",
                "Morgan's events shift ended at 9:00 PM according to the posted roster."),
            new SuspectProfile(
                "avery",
                "Avery Patel",
                "Campus facilities worker",
                "I left the student union before 9:15 PM and continued my shift in the library wing.",
                "Library wing maintenance corridor from 9:20 PM onward.",
                "A work order shows Avery checked out a large laundry cart.",
                "Avery used it to move event linens to the laundry room.",
                "Work radio check-in",
                "Avery radioed a library-wing check-in at 9:28 PM.",
                "Facilities assignment board",
                "Avery was assigned to evening linen pickup in the student union before relocating to the library wing."),
            new SuspectProfile(
                "riley",
                "Riley Brooks",
                "Club treasurer",
                "I left the student union before 9:15 PM and went to a study group.",
                "Science library study room B from 9:25 PM onward.",
                "A club receipt shows Riley bought a roll of heavy-duty tape.",
                "Riley used it to repair a display board for the club fair.",
                "Room reservation log",
                "Study room B was reserved under Riley's name from 9:15 PM to 10:30 PM.",
                "Club chat notice",
                "Riley posted in the club chat that the treasurer report was finished before leaving the union."),
            new SuspectProfile(
                "casey",
                "Casey Nguyen",
                "Mascot squad member",
                "I left the student union before 9:15 PM and went to costume storage across campus.",
                "Costume workshop annex from 9:20 PM onward.",
                "A locker checkout shows Casey removed a spare mascot torso shell that afternoon.",
                "Casey was fitting a replacement shell for tomorrow's pep rally.",
                "Annex badge swipe",
                "Casey's badge opened the costume workshop annex at 9:22 PM.",
                "Squad practice note",
                "Casey was marked present at afternoon mascot drill and listed for evening shell maintenance."),
            new SuspectProfile(
                "drew",
                "Drew Okonkwo",
                "Campus security student aide",
                "I left the student union before 9:15 PM and walked the south quad patrol loop.",
                "South quad patrol from 9:15 PM onward.",
                "A key cabinet log shows Drew signed out a master hallway key for the union.",
                "Drew was locking perimeter classrooms after the event and returned the key at 9:05 PM.",
                "Patrol checkpoint ping",
                "Drew's aide fob pinged the south quad checkpoint at 9:31 PM.",
                "Aide duty roster",
                "Drew was scheduled for the 8:00 PM to 11:00 PM student-aide patrol."),
            new SuspectProfile(
                "quinn",
                "Quinn Alvarez",
                "Student media photographer",
                "I left the student union before 9:15 PM and uploaded photos from the media lab.",
                "Media lab edit bay from 9:20 PM onward.",
                "An equipment loan shows Quinn checked out a large rolling camera case.",
                "Quinn used the case to move lights and stands after covering the event.",
                "Lab login record",
                "Quinn logged into a media-lab workstation at 9:26 PM.",
                "Press pass list",
                "Quinn appears on the approved photographer list for the student-union event."),
            new SuspectProfile(
                "sam",
                "Sam Torvald",
                "Dining services closer",
                "I left the student union before 9:15 PM and finished closing the cafe downstairs.",
                "Union cafe closing checklist from 9:15 PM onward.",
                "A supply request shows Sam ordered industrial zip ties that morning.",
                "Sam used them to reseal bulk snack bags during inventory.",
                "POS closeout stamp",
                "The cafe register closeout was stamped complete under Sam's employee number at 9:33 PM.",
                "Closing checklist",
                "Sam is the named closer on the union cafe evening checklist.")
        };

        /// <summary>How many authored case templates are available for seeded rotation.</summary>
        public static int TemplateCount { get { return CaseTemplateCatalog.Count; } }

        /// <summary>Preview the title that <see cref="Generate"/> will use for this seed.</summary>
        public static string PeekTitle(int seed)
        {
            CaseTemplateCatalog.CaseTemplate template = CaseTemplateCatalog.PickForSeed(seed);
            return template != null ? template.title : string.Empty;
        }

        /// <summary>Preview the dialogue tree hook id for this seed (stub / bank routing).</summary>
        public static string PeekTreeHook(int seed)
        {
            CaseTemplateCatalog.CaseTemplate template = CaseTemplateCatalog.PickForSeed(seed);
            return template != null ? template.treeHook : string.Empty;
        }

        /// <summary>Generates a seeded case with two to four suspects and exactly one planted contradiction.</summary>
        /// <param name="seed">A stable seed used to reproduce template pick, suspect selection, and culprit assignment.</param>
        /// <param name="suspectCount">The number of suspects, clamped to the supported range of two to four.</param>
        public static CaseFile Generate(int seed, int suspectCount = MaximumSuspectCount)
        {
            CaseTemplateCatalog.CaseTemplate template = CaseTemplateCatalog.PickForSeed(seed);
            int clampedSuspectCount = Math.Max(MinimumSuspectCount, Math.Min(MaximumSuspectCount, suspectCount));
            var random = new Random(seed);
            var availableProfiles = new List<SuspectProfile>(GetIntervieweePool(template));
            if (availableProfiles.Count < clampedSuspectCount)
                clampedSuspectCount = Math.Max(MinimumSuspectCount, availableProfiles.Count);

            var selectedProfiles = new List<SuspectProfile>(clampedSuspectCount);

            for (int i = 0; i < clampedSuspectCount; i++)
            {
                int selectedIndex = random.Next(availableProfiles.Count);
                selectedProfiles.Add(availableProfiles[selectedIndex]);
                availableProfiles.RemoveAt(selectedIndex);
            }

            int culpritIndex = random.Next(selectedProfiles.Count);
            SuspectProfile culprit = selectedProfiles[culpritIndex];
            string caseId = seed < 0
                ? "UB-N" + Math.Abs((long)seed).ToString("D10", CultureInfo.InvariantCulture)
                : "UB-" + seed.ToString("D10", CultureInfo.InvariantCulture);
            var suspects = new List<SuspectRecord>(selectedProfiles.Count);

            for (int i = 0; i < selectedProfiles.Count; i++)
            {
                SuspectProfile profile = selectedProfiles[i];
                bool isCulprit = i == culpritIndex;
                string suspectId = "suspect_" + profile.Id;
                string statement = string.IsNullOrWhiteSpace(template.statementTemplate)
                    ? profile.Statement
                    : template.statementTemplate.Trim();
                suspects.Add(new SuspectRecord(
                    suspectId,
                    profile.DisplayName,
                    profile.CampusRole,
                    statement,
                    profile.Alibi,
                    BuildEvidenceForSuspect(caseId, profile, isCulprit, template),
                    CaseClosed.Voice.SuspectVoiceCatalog.GetProfile(suspectId).VoiceId));
            }

            Shuffle(suspects, random);

            // Seeded witness slice for records site (~half of witness pool tonight; grows with affiliates.json).
            int witnessCap = Math.Min(24, Math.Max(8, CampusAffiliateCatalog.Witnesses.Count));
            List<WebsiteSuspectRecord> directory =
                CampusAffiliateCatalog.BuildDirectoryPayload(caseId, seed, witnessCap);

            return new CaseFile(
                caseId,
                template.title,
                template.briefing,
                "suspect_" + culprit.Id,
                suspects,
                directory);
        }

        /// <summary>Playable interviewee pool size (catalog when loaded, else embedded 8).</summary>
        public static int SuspectPoolCount { get { return GetIntervieweePool(null).Count; } }

        /// <summary>Total campus affiliates in the records/dialogue pool (interviewees + witnesses).</summary>
        public static int CampusPoolCount { get { return CampusAffiliateCatalog.TotalCount; } }

        private static List<SuspectProfile> GetIntervieweePool(CaseTemplateCatalog.CaseTemplate template)
        {
            List<SuspectProfile> full = LoadFullIntervieweePool();
            if (template == null || template.preferSuspectIds == null || template.preferSuspectIds.Length == 0)
                return full;

            var preferred = new List<SuspectProfile>(template.preferSuspectIds.Length);
            for (int i = 0; i < template.preferSuspectIds.Length; i++)
            {
                string want = template.preferSuspectIds[i];
                if (string.IsNullOrWhiteSpace(want)) continue;
                want = want.Trim().ToLowerInvariant();
                for (int j = 0; j < full.Count; j++)
                {
                    if (full[j].Id == want)
                    {
                        preferred.Add(full[j]);
                        break;
                    }
                }
            }

            return preferred.Count >= MinimumSuspectCount ? preferred : full;
        }

        private static List<SuspectProfile> LoadFullIntervieweePool()
        {
            IReadOnlyList<CampusAffiliateCatalog.CampusAffiliate> catalog = CampusAffiliateCatalog.Interviewees;
            if (catalog != null && catalog.Count >= MinimumSuspectCount)
            {
                var fromCatalog = new List<SuspectProfile>(catalog.Count);
                for (int i = 0; i < catalog.Count; i++)
                {
                    CampusAffiliateCatalog.CampusAffiliate a = catalog[i];
                    if (a == null || string.IsNullOrWhiteSpace(a.id)) continue;
                    if (string.IsNullOrWhiteSpace(a.statement) || string.IsNullOrWhiteSpace(a.alibi)) continue;
                    fromCatalog.Add(new SuspectProfile(
                        a.id,
                        a.displayName,
                        a.campusRole,
                        a.statement,
                        a.alibi,
                        a.redHerringRecord ?? string.Empty,
                        a.redHerringExplanation ?? string.Empty,
                        a.alibiSupportTitle ?? "Alibi support",
                        a.alibiSupportDescription ?? string.Empty,
                        a.contextTitleA ?? "Directory listing",
                        a.contextDescriptionA ?? a.bio ?? string.Empty));
                }

                if (fromCatalog.Count >= MinimumSuspectCount)
                    return fromCatalog;
            }

            return new List<SuspectProfile>(SuspectPool);
        }

        private static List<EvidenceRecord> BuildEvidenceForSuspect(
            string caseId,
            SuspectProfile profile,
            bool isCulprit,
            CaseTemplateCatalog.CaseTemplate template)
        {
            // Public website sees id/title/description only. Kind + Explanation stay Unity-side.
            // Suffixes must stay stable for ScenarioDialogueClient EvidenceKeyFromId + CollectEvidence.
            string signInTitle = string.IsNullOrWhiteSpace(template.signInTitle)
                ? "Event sign-in sheet"
                : template.signInTitle;
            string signInDescription = CaseTemplateCatalog.FormatName(
                string.IsNullOrWhiteSpace(template.signInDescription)
                    ? "{name} signed in near the scene earlier that evening."
                    : template.signInDescription,
                profile.DisplayName);

            var records = new List<EvidenceRecord>(4)
            {
                new EvidenceRecord(
                    caseId + "_" + profile.Id + "_roster",
                    profile.ContextTitleA,
                    profile.ContextDescriptionA,
                    EvidenceKind.Context,
                    string.Empty),
                new EvidenceRecord(
                    caseId + "_" + profile.Id + "_alibi_support",
                    profile.AlibiSupportTitle,
                    profile.AlibiSupportDescription,
                    EvidenceKind.Context,
                    string.Empty),
                new EvidenceRecord(
                    caseId + "_" + profile.Id + "_sign_in",
                    signInTitle,
                    signInDescription,
                    EvidenceKind.Context,
                    string.Empty)
            };

            if (isCulprit)
            {
                string accessTitle = string.IsNullOrWhiteSpace(template.accessTitle)
                    ? "Restricted access log"
                    : template.accessTitle;
                string accessDescription = CaseTemplateCatalog.FormatName(
                    string.IsNullOrWhiteSpace(template.accessDescription)
                        ? "A campus credential assigned to {name} opened a restricted door inside the incident window."
                        : template.accessDescription,
                    profile.DisplayName);
                string explanation = CaseTemplateCatalog.FormatName(
                    string.IsNullOrWhiteSpace(template.contradictionExplanation)
                        ? "{name} returned during the incident window."
                        : template.contradictionExplanation,
                    profile.DisplayName);

                records.Insert(0, new EvidenceRecord(
                    caseId + "_" + profile.Id + "_access",
                    accessTitle,
                    accessDescription,
                    EvidenceKind.Contradiction,
                    explanation));
            }
            else
            {
                // Title must match the authored description (site + board). Template category
                // labels like "Suspicious purchase" were mismatching sign-out sheets / work orders.
                string herringTitle = InferRedHerringTitle(profile.RedHerringRecord, template.redHerringTitle);
                records.Insert(0, new EvidenceRecord(
                    caseId + "_" + profile.Id + "_red_herring",
                    herringTitle,
                    profile.RedHerringRecord,
                    EvidenceKind.RedHerring,
                    profile.RedHerringExplanation));
            }

            return records;
        }

        private static void Shuffle<T>(IList<T> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                T current = items[i];
                items[i] = items[swapIndex];
                items[swapIndex] = current;
            }
        }

        /// <summary>
        /// Derive a board/site title from the red-herring body so labels stay aligned
        /// ("An event sign-out sheet shows…" → "Event sign-out sheet").
        /// </summary>
        internal static string InferRedHerringTitle(string record, string templateFallback)
        {
            string fallback = string.IsNullOrWhiteSpace(templateFallback)
                ? "Filed campus record"
                : templateFallback.Trim();

            if (string.IsNullOrWhiteSpace(record))
                return fallback;

            string text = record.Trim();
            int cut = IndexOfFirst(text, " shows ", " lists ", " indicates ", " notes ", " records ");
            string head = cut > 0 ? text.Substring(0, cut).Trim() : text;
            if (head.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
                head = head.Substring(2).Trim();
            else if (head.StartsWith("An ", StringComparison.OrdinalIgnoreCase))
                head = head.Substring(3).Trim();
            else if (head.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                head = head.Substring(4).Trim();

            if (string.IsNullOrWhiteSpace(head) || head.Length > 64)
                return fallback;

            return char.ToUpper(head[0], CultureInfo.InvariantCulture) + head.Substring(1);
        }

        private static int IndexOfFirst(string text, params string[] needles)
        {
            int best = -1;
            for (int i = 0; i < needles.Length; i++)
            {
                int idx = text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && (best < 0 || idx < best))
                    best = idx;
            }

            return best;
        }

        private sealed class SuspectProfile
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string CampusRole;
            public readonly string Statement;
            public readonly string Alibi;
            public readonly string RedHerringRecord;
            public readonly string RedHerringExplanation;
            public readonly string AlibiSupportTitle;
            public readonly string AlibiSupportDescription;
            public readonly string ContextTitleA;
            public readonly string ContextDescriptionA;

            public SuspectProfile(
                string id,
                string displayName,
                string campusRole,
                string statement,
                string alibi,
                string redHerringRecord,
                string redHerringExplanation,
                string alibiSupportTitle,
                string alibiSupportDescription,
                string contextTitleA,
                string contextDescriptionA)
            {
                Id = id;
                DisplayName = displayName;
                CampusRole = campusRole;
                Statement = statement;
                Alibi = alibi;
                RedHerringRecord = redHerringRecord;
                RedHerringExplanation = redHerringExplanation;
                AlibiSupportTitle = alibiSupportTitle;
                AlibiSupportDescription = alibiSupportDescription;
                ContextTitleA = contextTitleA;
                ContextDescriptionA = contextDescriptionA;
            }
        }
    }
}
