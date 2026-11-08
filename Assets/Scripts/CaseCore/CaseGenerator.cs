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
                "I finished my shift early and headed for the west bus loop.",
                "At the west bus loop after leaving campus buildings.",
                "A hardware-store receipt shows Jamie bought packing tape and zip ties that afternoon.",
                "Jamie used them for club table setup supplies.",
                "Transit tap receipt",
                "A transit-card tap places Jamie at the west bus loop shortly after they said they left.",
                "Volunteer duty roster",
                "Jamie is listed on the evening volunteer crew for a campus event."),
            new SuspectProfile(
                "morgan",
                "Morgan Lee",
                "Student events assistant",
                "I ended my desk shift and went straight back to housing.",
                "At their residence hall after clock-out.",
                "A staff key sign-out shows Morgan borrowed a generic utility key earlier that day.",
                "Morgan was inventorying supply closets and returned the key before evening.",
                "Hall exterior door log",
                "Morgan's hall exterior door registered an entry after their stated departure from work.",
                "Events desk schedule",
                "Morgan's posted events desk shift ended on time according to the roster."),
            new SuspectProfile(
                "avery",
                "Avery Patel",
                "Campus facilities worker",
                "I left the first building on my route and continued facilities checks elsewhere.",
                "On a facilities maintenance corridor during the later part of their shift.",
                "A work order shows Avery checked out a rolling utility cart.",
                "Avery used it to move linens and cleaning stock between buildings.",
                "Work radio check-in",
                "Avery radioed a wing check-in during the period they claim as their alibi.",
                "Facilities assignment board",
                "Avery was assigned evening porter duties spanning multiple campus buildings."),
            new SuspectProfile(
                "riley",
                "Riley Brooks",
                "Club treasurer",
                "I wrapped club paperwork and went to a reserved study room.",
                "Science library study room B during the later evening.",
                "A club receipt shows Riley bought a roll of heavy-duty tape.",
                "Riley used it to repair a display board for the club fair.",
                "Room reservation log",
                "Study room B was reserved under Riley's name for a multi-hour evening block.",
                "Club chat notice",
                "Riley posted that the treasurer report was finished before leaving the meeting space."),
            new SuspectProfile(
                "casey",
                "Casey Nguyen",
                "Spirit squad member",
                "I left practice early and went to the costume workshop annex.",
                "Costume workshop annex after practice.",
                "A locker checkout shows Casey removed a spare costume shell that afternoon.",
                "Casey was fitting a replacement shell for a scheduled pep event.",
                "Annex badge swipe",
                "Casey's badge opened the costume workshop annex after their stated departure from practice.",
                "Squad practice note",
                "Casey was marked present at afternoon drill and listed for evening shell maintenance."),
            new SuspectProfile(
                "drew",
                "Drew Okonkwo",
                "Campus security student aide",
                "I left the desk area and walked my assigned patrol loop.",
                "South quad patrol checkpoints during aide hours.",
                "A key cabinet log shows Drew signed out a hallway master key earlier.",
                "Drew was locking perimeter classrooms after an event and returned the key on time.",
                "Patrol checkpoint ping",
                "Drew's aide fob pinged the south quad checkpoint during their claimed patrol window.",
                "Aide duty roster",
                "Drew was scheduled for the evening student-aide patrol block."),
            new SuspectProfile(
                "quinn",
                "Quinn Alvarez",
                "Student media photographer",
                "I finished covering the event and uploaded files from the media lab.",
                "Media lab edit bay after the shoot.",
                "An equipment loan shows Quinn checked out a large rolling camera case.",
                "Quinn used the case to move lights and stands after covering the event.",
                "Lab login record",
                "Quinn logged into a media-lab workstation during their claimed upload window.",
                "Press pass list",
                "Quinn appears on the approved photographer list for the evening event."),
            new SuspectProfile(
                "sam",
                "Sam Torvald",
                "Dining services closer",
                "I left the upper floors and finished closing the cafe downstairs.",
                "Union cafe closing checklist through end of shift.",
                "A supply request shows Sam ordered industrial zip ties that morning.",
                "Sam used them to reseal bulk snack bags during inventory.",
                "POS closeout stamp",
                "The cafe register closeout was stamped complete under Sam's employee number.",
                "Closing checklist",
                "Sam is the named closer on the cafe evening checklist.")
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

        /// <summary>Preview easy / medium / hard for this seed.</summary>
        public static string PeekDifficulty(int seed)
        {
            CaseTemplateCatalog.CaseTemplate template = CaseTemplateCatalog.PickForSeed(seed);
            return template != null && !string.IsNullOrWhiteSpace(template.difficulty)
                ? template.difficulty
                : "medium";
        }

        /// <summary>
        /// Generates a seeded case with two to four suspects.
        /// The culprit still carries one EvidenceKind.Contradiction on the _access id.
        /// Hard templates write that card so it stays ambiguous until _sign_in and _alibi_support or _roster are read with it.
        /// </summary>
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

            bool rosterOverride = template != null
                && !string.IsNullOrWhiteSpace(template.rosterTitle)
                && !string.IsNullOrWhiteSpace(template.rosterDescription);
            bool alibiOverride = template != null
                && !string.IsNullOrWhiteSpace(template.alibiTitle)
                && !string.IsNullOrWhiteSpace(template.alibiDescription);
            string rosterTitle = rosterOverride ? template.rosterTitle.Trim() : profile.ContextTitleA;
            string rosterBody = rosterOverride
                ? CaseTemplateCatalog.FormatName(template.rosterDescription, profile.DisplayName)
                : profile.ContextDescriptionA;
            string alibiTitle = alibiOverride ? template.alibiTitle.Trim() : profile.AlibiSupportTitle;
            string alibiBody = alibiOverride
                ? CaseTemplateCatalog.FormatName(template.alibiDescription, profile.DisplayName)
                : profile.AlibiSupportDescription;

            var records = new List<EvidenceRecord>(4)
            {
                new EvidenceRecord(
                    caseId + "_" + profile.Id + "_roster",
                    rosterTitle,
                    rosterBody,
                    EvidenceKind.Context,
                    string.Empty),
                new EvidenceRecord(
                    caseId + "_" + profile.Id + "_alibi_support",
                    alibiTitle,
                    alibiBody,
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
                // Prefer case-template herring body so each shift has unique clue text and
                // title/description stay aligned (no shared mascot/storage affiliate copy).
                string herringBody = !string.IsNullOrWhiteSpace(template.redHerringDescription)
                    ? CaseTemplateCatalog.FormatName(template.redHerringDescription, profile.DisplayName)
                    : profile.RedHerringRecord;
                string herringExplain = !string.IsNullOrWhiteSpace(template.redHerringExplanation)
                    ? CaseTemplateCatalog.FormatName(template.redHerringExplanation, profile.DisplayName)
                    : profile.RedHerringExplanation;
                string herringTitle = !string.IsNullOrWhiteSpace(template.redHerringDescription)
                    ? (string.IsNullOrWhiteSpace(template.redHerringTitle)
                        ? InferRedHerringTitle(herringBody, "Filed campus record")
                        : template.redHerringTitle.Trim())
                    : InferRedHerringTitle(herringBody, template.redHerringTitle);
                records.Insert(0, new EvidenceRecord(
                    caseId + "_" + profile.Id + "_red_herring",
                    herringTitle,
                    herringBody,
                    EvidenceKind.RedHerring,
                    herringExplain));
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
