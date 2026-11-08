using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>Loads evidence-driven dialogue trees from Resources/Dialogue/Trees.</summary>
    public sealed class DialogueTreeBank
    {
        private readonly Dictionary<string, DialogueTreeFile> bySuspect =
            new Dictionary<string, DialogueTreeFile>(StringComparer.OrdinalIgnoreCase);
        private bool loaded;

        public int TreeCount { get { return bySuspect.Count; } }
        public bool IsLoaded { get { return loaded; } }

        public void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            bySuspect.Clear();

            TextAsset indexAsset = Resources.Load<TextAsset>("Dialogue/Trees/tree_index");
            if (indexAsset == null)
            {
                Debug.LogWarning("DialogueTreeBank: missing Resources/Dialogue/Trees/tree_index.json");
                return;
            }

            DialogueTreeIndex index = null;
            try
            {
                index = JsonUtility.FromJson<DialogueTreeIndex>(indexAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DialogueTreeBank: bad tree_index.json — " + ex.Message);
                return;
            }

            if (index == null || index.trees == null) return;

            for (int i = 0; i < index.trees.Length; i++)
            {
                string name = index.trees[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                TryLoadTree("Dialogue/Trees/" + name.Trim());
            }
        }

        public void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        public bool TryGetTree(string suspectIdOrName, out DialogueTreeFile tree)
        {
            EnsureLoaded();
            tree = null;
            string key = ScenarioDialogueClient.NormalizeSuspectKey(suspectIdOrName);
            if (key == "*" || string.IsNullOrEmpty(key)) return false;
            return bySuspect.TryGetValue(key, out tree) && tree != null;
        }

        private void TryLoadTree(string resourcesPath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcesPath);
            if (asset == null)
            {
                Debug.LogWarning("DialogueTreeBank: missing " + resourcesPath);
                return;
            }

            DialogueTreeFile file;
            try
            {
                file = JsonUtility.FromJson<DialogueTreeFile>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DialogueTreeBank: bad JSON " + resourcesPath + " — " + ex.Message);
                return;
            }

            if (file == null || string.IsNullOrWhiteSpace(file.suspectId)) return;
            string key = ScenarioDialogueClient.NormalizeSuspectKey(file.suspectId);
            bySuspect[key] = file;
        }
    }
}
