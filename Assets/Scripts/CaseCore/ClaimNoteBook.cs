using System;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>One line the student says, written on the desk notepad after the opening statement.</summary>
    [Serializable]
    public sealed class ClaimNoteLine
    {
        public string id;
        public string text;
    }

    /// <summary>
    /// Which existing evidence suffix checks or fights a claim.
    /// <see cref="onlyIf"/> is empty for every interviewee, <c>decoy</c> for the nervous innocent,
    /// or <c>contradiction</c> for a record already marked <see cref="EvidenceKind.Contradiction"/>.
    /// </summary>
    [Serializable]
    public sealed class ClaimNoteLink
    {
        public string claimId;
        public string suffix;
        public string effect;
        public string onlyIf;
    }

    /// <summary>Authored claims and record links for one case template id.</summary>
    [Serializable]
    public sealed class ClaimNoteCase
    {
        public string caseId;
        public ClaimNoteLine[] claims;
        public ClaimNoteLink[] links;
    }

    /// <summary>Notepad mark for one claim after the records seen so far.</summary>
    public enum ClaimNoteMark
    {
        None,
        Matched,
        Mismatch
    }

    /// <summary>A collected record reduced to the suffix and kind the claim links use.</summary>
    public struct ClaimNoteRecord
    {
        public string Suffix;
        public EvidenceKind Kind;
    }

    /// <summary>
    /// Loads <c>Resources/Cases/claim_notes.json</c> and decides check vs mismatch.
    /// A matching record wins over a mismatch on the same claim, so the nervous innocent's
    /// second record clears the first. A claim with only a contradiction link never clears.
    /// </summary>
    public static class ClaimNoteBook
    {
        private const string ResourcePath = "Cases/claim_notes";

        [Serializable]
        private sealed class NoteFile
        {
            public int schemaVersion = 1;
            public ClaimNoteCase[] cases;
        }

        private static Dictionary<string, ClaimNoteCase> byCaseId;
        private static bool loaded;

        public static void Reload()
        {
            loaded = false;
            byCaseId = null;
        }

        public static ClaimNoteCase Find(string caseId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(caseId) || byCaseId == null) return null;
            ClaimNoteCase notes;
            return byCaseId.TryGetValue(caseId.Trim(), out notes) ? notes : null;
        }

        /// <summary>
        /// Matched if any seen record links as a match. Mismatch only when a record fights the claim
        /// and no seen record matches it.
        /// </summary>
        public static ClaimNoteMark MarkFor(
            ClaimNoteCase notes,
            string claimId,
            bool isDecoy,
            IList<ClaimNoteRecord> seen)
        {
            if (notes == null || notes.links == null || string.IsNullOrWhiteSpace(claimId))
                return ClaimNoteMark.None;

            bool matched = false;
            bool mismatched = false;
            for (int linkIndex = 0; linkIndex < notes.links.Length; linkIndex++)
            {
                ClaimNoteLink link = notes.links[linkIndex];
                if (link == null || !string.Equals(link.claimId, claimId, StringComparison.Ordinal))
                    continue;
                if (seen == null) continue;

                for (int recordIndex = 0; recordIndex < seen.Count; recordIndex++)
                {
                    ClaimNoteRecord record = seen[recordIndex];
                    if (!SuffixEquals(link.suffix, record.Suffix)) continue;
                    if (!GateOpens(link.onlyIf, isDecoy, record.Kind)) continue;

                    if (IsMatch(link.effect)) matched = true;
                    else if (IsMismatch(link.effect)) mismatched = true;
                }
            }

            if (matched) return ClaimNoteMark.Matched;
            if (mismatched) return ClaimNoteMark.Mismatch;
            return ClaimNoteMark.None;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            byCaseId = new Dictionary<string, ClaimNoteCase>(16);

            UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

            NoteFile file;
            try
            {
                file = UnityEngine.JsonUtility.FromJson<NoteFile>(asset.text);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[ClaimNoteBook] Failed to parse claim_notes.json: " + ex.Message);
                return;
            }

            if (file == null || file.cases == null) return;
            for (int i = 0; i < file.cases.Length; i++)
            {
                ClaimNoteCase notes = file.cases[i];
                if (notes == null || string.IsNullOrWhiteSpace(notes.caseId)) continue;
                byCaseId[notes.caseId.Trim()] = notes;
            }
        }

        private static bool SuffixEquals(string linkSuffix, string recordSuffix)
        {
            return !string.IsNullOrWhiteSpace(linkSuffix) &&
                   !string.IsNullOrWhiteSpace(recordSuffix) &&
                   string.Equals(linkSuffix.Trim(), recordSuffix.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool GateOpens(string onlyIf, bool isDecoy, EvidenceKind kind)
        {
            if (string.IsNullOrWhiteSpace(onlyIf) ||
                string.Equals(onlyIf, "any", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(onlyIf, "decoy", StringComparison.OrdinalIgnoreCase))
                return isDecoy;
            if (string.Equals(onlyIf, "contradiction", StringComparison.OrdinalIgnoreCase))
                return kind == EvidenceKind.Contradiction;
            return false;
        }

        private static bool IsMatch(string effect)
        {
            return string.Equals(effect, "match", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMismatch(string effect)
        {
            return string.Equals(effect, "mismatch", StringComparison.OrdinalIgnoreCase);
        }
    }
}
