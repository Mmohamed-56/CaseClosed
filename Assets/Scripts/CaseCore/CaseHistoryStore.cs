using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>A public, resolved-case record containing no hidden culprit data.</summary>
    [Serializable]
    public sealed class ClosedCaseSummary
    {
        public string caseId;
        public string title;
        public string date;
        public string outcome;
        public string chargedSuspect;
        public int score;
    }

    /// <summary>A score earned by a player for a resolved case.</summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        public string playerName;
        public int score;
        public string date;
        public string caseId;
    }

    /// <summary>Stores resolved-case summaries and a local top-ten leaderboard as JSON.</summary>
    public static class CaseHistoryStore
    {
        private const int CurrentDataVersion = 1;
        private const int MaximumLeaderboardEntries = 10;
        private const string FileName = "case-history.json";

        [Serializable]
        private sealed class HistoryData
        {
            public int version = CurrentDataVersion;
            public List<ClosedCaseSummary> summaries = new List<ClosedCaseSummary>();
            public List<LeaderboardEntry> scores = new List<LeaderboardEntry>();
        }

        /// <summary>Appends a resolved-case summary and persists the updated history.</summary>
        /// <param name="caseId">The public identifier of the resolved case.</param>
        /// <param name="title">The case title.</param>
        /// <param name="date">The resolution date, preferably in ISO-8601 format.</param>
        /// <param name="outcome">The public outcome label.</param>
        /// <param name="chargedSuspect">The charged suspect's public name or an empty value when nobody was charged.</param>
        /// <param name="score">The score earned for this case.</param>
        public static void AppendClosedCaseSummary(string caseId, string title, string date, string outcome, string chargedSuspect, int score)
        {
            HistoryData data = ReadData();
            data.summaries.Add(new ClosedCaseSummary
            {
                caseId = caseId ?? string.Empty,
                title = title ?? string.Empty,
                date = date ?? string.Empty,
                outcome = outcome ?? string.Empty,
                chargedSuspect = chargedSuspect ?? string.Empty,
                score = score
            });
            WriteData(data);
        }

        /// <summary>Returns a snapshot of all stored resolved-case summaries.</summary>
        public static List<ClosedCaseSummary> GetClosedCaseSummaries()
        {
            return new List<ClosedCaseSummary>(ReadData().summaries);
        }

        /// <summary>Records a score, sorts the leaderboard descending, and retains only the top ten entries.</summary>
        /// <param name="playerName">The player's public display name.</param>
        /// <param name="score">The score to record.</param>
        /// <param name="date">The score date, preferably in ISO-8601 format.</param>
        /// <param name="caseId">The public identifier of the case associated with the score.</param>
        public static void RecordScore(string playerName, int score, string date, string caseId)
        {
            HistoryData data = ReadData();
            data.scores.Add(new LeaderboardEntry
            {
                playerName = playerName ?? string.Empty,
                score = score,
                date = date ?? string.Empty,
                caseId = caseId ?? string.Empty
            });
            data.scores.Sort(CompareScores);
            if (data.scores.Count > MaximumLeaderboardEntries)
                data.scores.RemoveRange(MaximumLeaderboardEntries, data.scores.Count - MaximumLeaderboardEntries);
            WriteData(data);
        }

        /// <summary>Returns a snapshot of the stored leaderboard, ordered by descending score.</summary>
        public static List<LeaderboardEntry> GetTopScores()
        {
            return new List<LeaderboardEntry>(ReadData().scores);
        }

        private static int CompareScores(LeaderboardEntry left, LeaderboardEntry right)
        {
            int scoreComparison = right.score.CompareTo(left.score);
            if (scoreComparison != 0) return scoreComparison;
            return string.Compare(left.date, right.date, StringComparison.Ordinal);
        }

        private static HistoryData ReadData()
        {
            string filePath = GetFilePath();
            if (!File.Exists(filePath)) return new HistoryData();

            try
            {
                HistoryData data = JsonUtility.FromJson<HistoryData>(File.ReadAllText(filePath));
                if (data == null || data.version != CurrentDataVersion)
                    return new HistoryData();

                if (data.summaries == null) data.summaries = new List<ClosedCaseSummary>();
                if (data.scores == null) data.scores = new List<LeaderboardEntry>();
                data.summaries.RemoveAll(summary => summary == null);
                data.scores.RemoveAll(entry => entry == null);
                data.scores.Sort(CompareScores);
                if (data.scores.Count > MaximumLeaderboardEntries)
                    data.scores.RemoveRange(MaximumLeaderboardEntries, data.scores.Count - MaximumLeaderboardEntries);
                return data;
            }
            catch (Exception)
            {
                return new HistoryData();
            }
        }

        private static void WriteData(HistoryData data)
        {
            string directoryPath = Application.persistentDataPath;
            Directory.CreateDirectory(directoryPath);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetFilePath(), json);
        }

        private static string GetFilePath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }
}
