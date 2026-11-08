using System;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>Classifies how a piece of case evidence relates to the investigation.</summary>
    public enum EvidenceKind
    {
        Context,
        RedHerring,
        Contradiction
    }

    /// <summary>Describes whether a suspect is held or released.</summary>
    public enum SuspectDecision
    {
        Undecided,
        Hold,
        Release
    }

    /// <summary>Describes the result of the final charging decision.</summary>
    public enum CaseOutcome
    {
        CulpritCharged,
        CulpritReleased,
        InnocentCharged,
        NoCharge
    }

    /// <summary>A discoverable record or clue associated with a suspect.</summary>
    [Serializable]
    public sealed class EvidenceRecord
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public EvidenceKind Kind { get; private set; }
        public string Explanation { get; private set; }

        public EvidenceRecord(string id, string title, string description, EvidenceKind kind, string explanation)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Evidence needs an ID.", "id");
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Evidence needs a title.", "title");
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Evidence needs a description.", "description");

            Id = id;
            Title = title;
            Description = description;
            Kind = kind;
            Explanation = explanation ?? string.Empty;
        }
    }

    /// <summary>Fixed case information for one person brought in for questioning.</summary>
    [Serializable]
    public sealed class SuspectRecord
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string CampusRole { get; private set; }
        public string Statement { get; private set; }
        public string Alibi { get; private set; }
        /// <summary>Optional ElevenLabs voice id from case data. Empty falls back to the placeholder catalog.</summary>
        public string ElevenLabsVoiceId { get; private set; }
        public IReadOnlyList<EvidenceRecord> Evidence { get { return evidence; } }

        private readonly List<EvidenceRecord> evidence;

        public SuspectRecord(string id, string displayName, string campusRole, string statement, string alibi, IEnumerable<EvidenceRecord> evidenceRecords, string elevenLabsVoiceId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Suspect needs an ID.", "id");
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Suspect needs a display name.", "displayName");

            Id = id;
            DisplayName = displayName;
            CampusRole = campusRole ?? string.Empty;
            Statement = statement ?? string.Empty;
            Alibi = alibi ?? string.Empty;
            ElevenLabsVoiceId = elevenLabsVoiceId ?? string.Empty;
            evidence = evidenceRecords == null ? new List<EvidenceRecord>() : new List<EvidenceRecord>(evidenceRecords);
        }
    }

    /// <summary>
    /// Contains the briefing, suspects, and truth for a deterministic case.
    /// <see cref="CulpritSuspectId"/> is Unity-only — never copy it into <c>WebsiteCasePayload</c> / page source.
    /// </summary>
    [Serializable]
    public sealed class CaseFile
    {
        public string CaseId { get; private set; }
        public string Title { get; private set; }
        public string Briefing { get; private set; }
        public IReadOnlyList<SuspectRecord> Suspects { get { return suspects; } }

        /// <summary>
        /// Public campus directory rows (bios / witness record hooks). Not interrogated.
        /// Ids use <c>affiliate_*</c>; clue ids use <c>{caseId}_aff_{id}_{suffix}</c>.
        /// </summary>
        public IReadOnlyList<WebsiteSuspectRecord> CampusDirectory { get { return campusDirectory; } }

        /// <summary>Hidden truth id. Exposed to interrogation / scoring only; omitted from the public records JSON.</summary>
        internal string CulpritSuspectId { get; private set; }

        private readonly List<SuspectRecord> suspects;
        private readonly List<WebsiteSuspectRecord> campusDirectory;

        public CaseFile(string caseId, string title, string briefing, string culpritSuspectId, IEnumerable<SuspectRecord> caseSuspects)
            : this(caseId, title, briefing, culpritSuspectId, caseSuspects, null)
        {
        }

        public CaseFile(
            string caseId,
            string title,
            string briefing,
            string culpritSuspectId,
            IEnumerable<SuspectRecord> caseSuspects,
            IEnumerable<WebsiteSuspectRecord> directoryAffiliates)
        {
            if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("Case needs an ID.", "caseId");
            if (string.IsNullOrWhiteSpace(culpritSuspectId)) throw new ArgumentException("Case needs a culprit ID.", "culpritSuspectId");
            if (caseSuspects == null) throw new ArgumentNullException("caseSuspects");

            CaseId = caseId;
            Title = title ?? string.Empty;
            Briefing = briefing ?? string.Empty;
            CulpritSuspectId = culpritSuspectId;
            suspects = new List<SuspectRecord>(caseSuspects);
            campusDirectory = directoryAffiliates == null
                ? new List<WebsiteSuspectRecord>()
                : new List<WebsiteSuspectRecord>(directoryAffiliates);

            if (suspects.Count < 2) throw new ArgumentException("A case needs at least two suspects.", "caseSuspects");
            if (!suspects.Exists(suspect => suspect != null && suspect.Id == culpritSuspectId))
                throw new ArgumentException("The culprit ID must match one of the case suspects.", "culpritSuspectId");
        }
    }

    /// <summary>
    /// Fixed two-suspect demo case for judging. Prefer <see cref="CaseGenerator.Generate"/> for seeded full shifts.
    /// Evidence Kind/Explanation and voice ids remain Unity-side; public site only gets id/title/description via the payload builder.
    /// </summary>
    public static class DemoCaseFactory
    {
        public static CaseFile CreateMissingMascotHeadCase()
        {
            var innocent = new SuspectRecord(
                "suspect_jamie",
                "Jamie Rivera",
                "Student volunteer",
                "I locked up the student-union event room and went straight to the bus stop.",
                "At the west bus loop from 9:20 PM onward.",
                new[]
                {
                    new EvidenceRecord("jamie_purchase", "Hardware-store receipt", "Jamie bought a small utility knife that afternoon.", EvidenceKind.RedHerring, "Jamie used it to cut tape and cardboard for event decorations."),
                    new EvidenceRecord("jamie_roster", "Volunteer roster", "Jamie is listed on the evening setup crew for the student-union event.", EvidenceKind.Context, string.Empty),
                    new EvidenceRecord("jamie_bus", "Transit receipt", "A transit-card record places Jamie at the west bus loop at 9:24 PM.", EvidenceKind.Context, string.Empty),
                    new EvidenceRecord("jamie_sign_in", "Event sign-in sheet", "Jamie signed in to help with student-union event setup earlier that evening.", EvidenceKind.Context, string.Empty)
                },
                CaseClosed.Voice.SuspectVoiceCatalog.GetProfile("suspect_jamie").VoiceId);

            var culprit = new SuspectRecord(
                "suspect_morgan",
                "Morgan Lee",
                "Student events assistant",
                "I left the student union before nine and spent the rest of the night at home.",
                "At home from 9:00 PM onward.",
                new[]
                {
                    new EvidenceRecord("morgan_access", "Storage-room access log", "Morgan's campus card opened the event-storage room at 9:37 PM, inside the theft window and after their stated departure time.", EvidenceKind.Contradiction, "Morgan took the mascot head from storage and hid it in a laundry bag."),
                    new EvidenceRecord("morgan_schedule", "Staff schedule", "Morgan's events shift ended at 9:00 PM according to the posted roster.", EvidenceKind.Context, string.Empty),
                    new EvidenceRecord("morgan_hall", "Residence hall door log", "Morgan's hall exterior door registered an entry at 9:18 PM.", EvidenceKind.Context, string.Empty),
                    new EvidenceRecord("morgan_sign_in", "Event sign-in sheet", "Morgan signed in to help with student-union event setup earlier that evening.", EvidenceKind.Context, string.Empty)
                },
                CaseClosed.Voice.SuspectVoiceCatalog.GetProfile("suspect_morgan").VoiceId);

            return new CaseFile(
                "UB-001",
                "The Missing Mascot Head",
                "The university mascot head vanished from the student-union storage room between 9:15 PM and 9:45 PM. Two people had a reason to be near the event rooms. Find the contradiction before deciding who to hold.",
                culprit.Id,
                new[] { innocent, culprit });
        }
    }
}
