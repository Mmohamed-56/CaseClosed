using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Public case data for rendering in the in-game records website (schemaVersion 1).
    /// Intentionally omits culprit id, alibi, EvidenceKind, and Explanation — do not add those fields.
    /// </summary>
    [Serializable]
    public sealed class WebsiteCasePayload
    {
        public int schemaVersion = 1;
        public string caseId;
        public string title;
        public string briefing;
        public List<WebsiteSuspectRecord> suspects = new List<WebsiteSuspectRecord>();
    }

    /// <summary>Public suspect details that may be shown to the player on the records website.</summary>
    [Serializable]
    public sealed class WebsiteSuspectRecord
    {
        public string id;
        public string name;
        public string role;
        public string statement;
        public List<WebsiteEvidenceRecord> records = new List<WebsiteEvidenceRecord>();
    }

    /// <summary>Public record content. Hidden classification and explanation are intentionally omitted.</summary>
    [Serializable]
    public sealed class WebsiteEvidenceRecord
    {
        public string id;
        public string title;
        public string description;
    }

    /// <summary>Builds a sanitized case payload and URL-safe Base64 hash fragment for the records site.</summary>
    public static class CaseWebsitePayloadBuilder
    {
        /// <summary>Creates JSON containing only case briefing, suspect statements, and public records.</summary>
        public static string BuildJson(CaseFile caseFile)
        {
            if (caseFile == null) throw new ArgumentNullException("caseFile");
            return JsonUtility.ToJson(CreatePayload(caseFile));
        }

        /// <summary>Creates a URL-safe Base64 value suitable for the records website's case hash.</summary>
        public static string BuildBase64Url(CaseFile caseFile)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(BuildJson(caseFile));
            return Convert.ToBase64String(jsonBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>Creates the complete case hash fragment expected by the static records site.</summary>
        public static string BuildHashFragment(CaseFile caseFile)
        {
            return "#case=" + BuildBase64Url(caseFile);
        }

        private static WebsiteCasePayload CreatePayload(CaseFile caseFile)
        {
            var payload = new WebsiteCasePayload
            {
                caseId = caseFile.CaseId,
                title = caseFile.Title,
                briefing = caseFile.Briefing
            };

            foreach (SuspectRecord suspect in caseFile.Suspects)
            {
                if (suspect == null) continue;
                var publicSuspect = new WebsiteSuspectRecord
                {
                    id = suspect.Id,
                    name = suspect.DisplayName,
                    role = suspect.CampusRole,
                    statement = suspect.Statement
                };

                foreach (EvidenceRecord evidence in suspect.Evidence)
                {
                    if (evidence == null) continue;
                    publicSuspect.records.Add(new WebsiteEvidenceRecord
                    {
                        id = evidence.Id,
                        title = evidence.Title,
                        description = evidence.Description
                    });
                }

                payload.suspects.Add(publicSuspect);
            }

            return payload;
        }
    }
}
