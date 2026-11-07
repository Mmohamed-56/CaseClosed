using System;
using System.Globalization;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Creates reproducible, locally authored cases without using AI to decide the truth.
    /// Culprit identity and evidence Kind/Explanation stay Unity-only; the website payload
    /// builder must never receive those fields (see <see cref="CaseWebsitePayloadBuilder"/>).
    /// </summary>
    public static class CaseGenerator
    {
        private const int MinimumSuspectCount = 2;
        private const int MaximumSuspectCount = 4;
        private const string CaseTitle = "The Missing Mascot Head";
        private const string CaseBriefing =
            "The university mascot head vanished from the student-union storage room between 9:15 PM and 9:45 PM. Check the records against each suspect's statement before deciding who to hold.";

        /// <summary>Handwritten pool (GDD: 6–8). Each shift draws 2–4; exactly one selected suspect becomes the culprit.</summary>
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

        /// <summary>Generates a seeded case with two to four suspects and exactly one planted contradiction.</summary>
        /// <param name="seed">A stable seed used to reproduce suspect selection and culprit assignment.</param>
        /// <param name="suspectCount">The number of suspects, clamped to the supported range of two to four.</param>
        public static CaseFile Generate(int seed, int suspectCount = MaximumSuspectCount)
        {
            int clampedSuspectCount = Math.Max(MinimumSuspectCount, Math.Min(MaximumSuspectCount, suspectCount));
            var random = new Random(seed);
            var availableProfiles = new List<SuspectProfile>(SuspectPool);
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
                suspects.Add(new SuspectRecord(
                    suspectId,
                    profile.DisplayName,
                    profile.CampusRole,
                    profile.Statement,
                    profile.Alibi,
                    BuildEvidenceForSuspect(caseId, profile, isCulprit),
                    CaseClosed.Voice.SuspectVoiceCatalog.Resolve(suspectId)));
            }

            Shuffle(suspects, random);
            return new CaseFile(
                caseId,
                CaseTitle,
                CaseBriefing,
                "suspect_" + culprit.Id,
                suspects);
        }

        /// <summary>Size of the handwritten suspect pool available for seeded draws.</summary>
        public static int SuspectPoolCount { get { return SuspectPool.Length; } }

        private static List<EvidenceRecord> BuildEvidenceForSuspect(string caseId, SuspectProfile profile, bool isCulprit)
        {
            // Public website sees id/title/description only. Kind + Explanation stay Unity-side for Gemini / confrontation.
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
                    "Event sign-in sheet",
                    profile.DisplayName + " signed in to help with student-union event setup earlier that evening.",
                    EvidenceKind.Context,
                    string.Empty)
            };

            if (isCulprit)
            {
                records.Insert(0, new EvidenceRecord(
                    caseId + "_" + profile.Id + "_access",
                    "Storage-room access log",
                    "The campus card assigned to " + profile.DisplayName + " opened the event-storage room at 9:37 PM, inside the theft window and after their stated departure time.",
                    EvidenceKind.Contradiction,
                    profile.DisplayName + " returned to the storage room and took the mascot head."));
            }
            else
            {
                records.Insert(0, new EvidenceRecord(
                    caseId + "_" + profile.Id + "_red_herring",
                    "Suspicious purchase record",
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
