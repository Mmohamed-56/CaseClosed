using System;
using System.Collections.Generic;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// One authored opening line for a template suspect. Guilt picks <see cref="culprit"/> or <see cref="innocent"/>.
    /// </summary>
    [Serializable]
    public sealed class OpeningStatementRow
    {
        public string caseId;
        public string suspectId;
        public string innocent;
        public string culprit;
    }

    /// <summary>
    /// Loads <c>Resources/Cases/opening_statements.json</c>.
    /// A row matches on template case id and suspect id. No row means the caller keeps
    /// <see cref="SuspectRecord.Statement"/>.
    /// </summary>
    public static class OpeningStatementBook
    {
        private const string ResourcePath = "Cases/opening_statements";

        [Serializable]
        private sealed class StatementFile
        {
            public int schemaVersion = 1;
            public int count;
            public OpeningStatementRow[] statements;
        }

        private static Dictionary<string, OpeningStatementRow> byKey;
        private static bool loaded;

        public static void Reload()
        {
            loaded = false;
            byKey = null;
        }

        /// <summary>
        /// Culprit or innocent text for this template suspect. Null when no row matches or that side is blank.
        /// Accepts <c>jamie</c> or <c>suspect_jamie</c>.
        /// </summary>
        public static string LineFor(string caseId, string suspectId, bool isCulprit)
        {
            OpeningStatementRow row = Find(caseId, suspectId);
            if (row == null) return null;

            string text = isCulprit ? row.culprit : row.innocent;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        public static OpeningStatementRow Find(string caseId, string suspectId)
        {
            EnsureLoaded();
            if (byKey == null) return null;

            string key = Key(caseId, suspectId);
            if (key.Length == 0) return null;

            OpeningStatementRow row;
            return byKey.TryGetValue(key, out row) ? row : null;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            byKey = new Dictionary<string, OpeningStatementRow>(64);

            UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

            StatementFile file;
            try
            {
                file = UnityEngine.JsonUtility.FromJson<StatementFile>(asset.text);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[OpeningStatementBook] Failed to parse opening_statements.json: " + ex.Message);
                return;
            }

            if (file == null || file.statements == null) return;
            for (int i = 0; i < file.statements.Length; i++)
            {
                OpeningStatementRow row = file.statements[i];
                if (row == null) continue;
                string key = Key(row.caseId, row.suspectId);
                if (key.Length == 0) continue;
                byKey[key] = row;
            }
        }

        private static string Key(string caseId, string suspectId)
        {
            if (string.IsNullOrWhiteSpace(caseId) || string.IsNullOrWhiteSpace(suspectId))
                return string.Empty;

            string person = suspectId.Trim().ToLowerInvariant();
            const string prefix = "suspect_";
            if (person.StartsWith(prefix, StringComparison.Ordinal))
                person = person.Substring(prefix.Length);
            if (person.Length == 0) return string.Empty;

            return caseId.Trim().ToLowerInvariant() + "|" + person;
        }
    }
}
