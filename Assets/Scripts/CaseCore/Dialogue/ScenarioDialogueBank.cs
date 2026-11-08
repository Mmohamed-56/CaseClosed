using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>Loads and indexes offline dialogue JSON from Resources/Dialogue.</summary>
    public sealed class ScenarioDialogueBank
    {
        private readonly List<ScenarioLineEntry> entries = new List<ScenarioLineEntry>(512);
        private bool loaded;

        public int EntryCount { get { return entries.Count; } }
        public bool IsLoaded { get { return loaded; } }
        public IReadOnlyList<ScenarioLineEntry> Entries { get { return entries; } }

        /// <summary>Loads bank_index.json then each listed TextAsset. Safe to call more than once.</summary>
        public void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            entries.Clear();

            TextAsset indexAsset = Resources.Load<TextAsset>("Dialogue/bank_index");
            if (indexAsset == null)
            {
                Debug.LogWarning("ScenarioDialogueBank: missing Resources/Dialogue/bank_index.json");
                TryLoadBank("Dialogue/common");
                return;
            }

            ScenarioBankIndex index = null;
            try
            {
                index = JsonUtility.FromJson<ScenarioBankIndex>(indexAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ScenarioDialogueBank: bad bank_index.json — " + ex.Message);
            }

            if (index == null || index.banks == null || index.banks.Length == 0)
            {
                TryLoadBank("Dialogue/common");
                return;
            }

            for (int i = 0; i < index.banks.Length; i++)
            {
                string name = index.banks[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                string path = name.StartsWith("Dialogue/", StringComparison.Ordinal)
                    ? name
                    : "Dialogue/" + name.Trim();
                TryLoadBank(path);
            }
        }

        /// <summary>Force reload (Editor iteration / hot swap).</summary>
        public void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        private void TryLoadBank(string resourcesPath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcesPath);
            if (asset == null)
            {
                Debug.LogWarning("ScenarioDialogueBank: missing " + resourcesPath);
                return;
            }

            ScenarioBankFile file;
            try
            {
                file = JsonUtility.FromJson<ScenarioBankFile>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ScenarioDialogueBank: parse failed for " + resourcesPath + " — " + ex.Message);
                return;
            }

            if (file == null || file.entries == null) return;
            for (int i = 0; i < file.entries.Length; i++)
            {
                ScenarioLineEntry entry = file.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.line)) continue;
                if (string.IsNullOrWhiteSpace(entry.suspectId)) entry.suspectId = "*";
                if (string.IsNullOrWhiteSpace(entry.intent)) entry.intent = ScenarioIntents.Generic;
                if (entry.maxStress <= 0) entry.maxStress = 100;
                entries.Add(entry);
            }
        }
    }
}
