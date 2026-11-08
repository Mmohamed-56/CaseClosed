using System;
using System.Collections.Generic;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>
    /// Spawns one character prefab matching a suspect's broad gender presentation, choosing a stable variant per suspect id.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuspectCharacterPool : MonoBehaviour
    {
        [Header("Prefab Pool")]
        [Tooltip("Floreswa character prefabs. The pool chooses female or male variants from their prefab names; androgynous or unspecified looks may use either.")]
        [SerializeField] private GameObject[] characterPrefabs;

        [Tooltip("Optional palette materials. Leave empty to preserve each prefab's authored skin, hair, and outfit materials.")]
        [SerializeField] private Material[] paletteMaterials;

        [Header("Spawn Parent")]
        [Tooltip("Parent for the active suspect instance. Defaults to this transform.")]
        [SerializeField] private Transform characterParent;

        private GameObject activeCharacter;
        private CharacterAnimationDriver activeDriver;
        private readonly Dictionary<string, int> suspectPrefabIndexById = new Dictionary<string, int>();

        /// <summary>The currently spawned suspect GameObject, or null.</summary>
        public GameObject ActiveCharacter { get { return activeCharacter; } }

        /// <summary>Animation driver bound to the active character, or null.</summary>
        public CharacterAnimationDriver ActiveDriver { get { return activeDriver; } }

        /// <summary>
        /// Destroys any previous character and spawns a stable variant for this suspect id at <paramref name="spawnPoint"/>.
        /// </summary>
        public CharacterAnimationDriver SpawnForSuspect(string suspectId, Transform spawnPoint)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
                throw new ArgumentException("Suspect id is required.", "suspectId");
            if (spawnPoint == null)
                throw new ArgumentNullException("spawnPoint");
            if (characterPrefabs == null || characterPrefabs.Length == 0)
                throw new InvalidOperationException("Assign at least one character prefab to SuspectCharacterPool.");

            ClearActive();

            int prefabIndex = ResolvePrefabIndex(suspectId);
            GameObject prefab = characterPrefabs[prefabIndex];
            if (prefab == null)
                throw new InvalidOperationException("SuspectCharacterPool prefab slot " + prefabIndex + " is empty.");

            Transform parent = characterParent != null ? characterParent : transform;
            activeCharacter = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);
            activeCharacter.name = prefab.name + " (" + suspectId + ")";

            ApplyStablePalette(activeCharacter, suspectId);

            activeDriver = activeCharacter.GetComponent<CharacterAnimationDriver>();
            if (activeDriver == null)
                activeDriver = activeCharacter.AddComponent<CharacterAnimationDriver>();

            Animator animator = activeCharacter.GetComponentInChildren<Animator>(true);
            activeDriver.Bind(animator);
            return activeDriver;
        }

        /// <summary>Destroys the active suspect instance.</summary>
        public void ClearActive()
        {
            if (activeCharacter != null)
            {
                Destroy(activeCharacter);
                activeCharacter = null;
            }
            activeDriver = null;
        }

        private enum CharacterPresentation
        {
            Any,
            Female,
            Male
        }

        private int ResolvePrefabIndex(string suspectId)
        {
            int index;
            if (suspectPrefabIndexById.TryGetValue(suspectId, out index))
                return index;

            CharacterPresentation requestedPresentation = ResolvePresentation(suspectId);
            var candidateIndices = new List<int>();
            for (int i = 0; i < characterPrefabs.Length; i++)
            {
                GameObject prefab = characterPrefabs[i];
                if (!IsCharacterPrefab(prefab))
                    continue;

                CharacterPresentation prefabPresentation = GetPrefabPresentation(prefab);
                if (requestedPresentation == CharacterPresentation.Any || prefabPresentation == requestedPresentation)
                    candidateIndices.Add(i);
            }

            // If a requested gender family is not assigned, use the available character models
            // instead of failing the case. Selection remains stable for this suspect id.
            if (candidateIndices.Count == 0)
            {
                for (int i = 0; i < characterPrefabs.Length; i++)
                {
                    if (IsCharacterPrefab(characterPrefabs[i]))
                        candidateIndices.Add(i);
                }
            }

            if (candidateIndices.Count == 0)
                throw new InvalidOperationException("No character prefabs with a SkinnedMeshRenderer are assigned.");

            index = candidateIndices[StableHash(suspectId) % candidateIndices.Count];
            suspectPrefabIndexById[suspectId] = index;
            return index;
        }

        private static CharacterPresentation ResolvePresentation(string suspectId)
        {
            SuspectVoiceProfile profile = SuspectVoiceCatalog.GetProfile(suspectId);
            SuspectLook look = profile.Look;
            if (look != null && !string.IsNullOrWhiteSpace(look.genderPresentation))
            {
                string presentation = look.genderPresentation.Trim();
                if (presentation.IndexOf("woman", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    presentation.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterPresentation.Female;
                if (presentation.IndexOf("man", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    presentation.IndexOf("male", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterPresentation.Male;
                if (presentation.IndexOf("androgynous", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    presentation.IndexOf("neutral", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterPresentation.Any;
            }

            switch (profile.Gender)
            {
                case SuspectVoiceGender.Female:
                    return CharacterPresentation.Female;
                case SuspectVoiceGender.Neutral:
                    return CharacterPresentation.Any;
                default:
                    return CharacterPresentation.Male;
            }
        }

        private static CharacterPresentation GetPrefabPresentation(GameObject prefab)
        {
            if (prefab == null)
                return CharacterPresentation.Any;

            string prefabName = prefab.name;
            if (prefabName.StartsWith("female", StringComparison.OrdinalIgnoreCase) ||
                prefabName.IndexOf("woman", StringComparison.OrdinalIgnoreCase) >= 0)
                return CharacterPresentation.Female;
            if (prefabName.StartsWith("male", StringComparison.OrdinalIgnoreCase) ||
                prefabName.IndexOf(" man", StringComparison.OrdinalIgnoreCase) >= 0)
                return CharacterPresentation.Male;
            return CharacterPresentation.Any;
        }

        private static bool IsCharacterPrefab(GameObject prefab)
        {
            return prefab != null && prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        private void ApplyStablePalette(GameObject character, string suspectId)
        {
            if (paletteMaterials == null || paletteMaterials.Length == 0) return;

            Material palette = paletteMaterials[StableHash(suspectId) % paletteMaterials.Length];
            if (palette == null) return;

            SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sharedMaterial = palette;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                if (hash == int.MinValue) hash = 0;
                return Mathf.Abs(hash);
            }
        }
    }
}
