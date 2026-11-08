using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>
    /// Spawns one Low Poly People suspect at a time from a prefab pool, swapping the active instance
    /// while reusing the same door / chair transforms owned by <see cref="SuspectEnterController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuspectCharacterPool : MonoBehaviour
    {
        [Header("Prefab Pool")]
        [Tooltip("Low Poly People character prefabs (normal/stout/strong, man/woman). Assign all variants you want in rotation.")]
        [SerializeField] private GameObject[] characterPrefabs;

        [Tooltip("Optional palette1–palette9 materials from the Low Poly People package.")]
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
                throw new InvalidOperationException("Assign at least one Low Poly People prefab to SuspectCharacterPool.");

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

        private int ResolvePrefabIndex(string suspectId)
        {
            int index;
            if (suspectPrefabIndexById.TryGetValue(suspectId, out index))
                return index;

            // Stable hash so the same suspect keeps the same body across enter/exit cycles.
            int hash = StableHash(suspectId);
            index = hash % characterPrefabs.Length;
            // Skip null slots.
            for (int attempt = 0; attempt < characterPrefabs.Length; attempt++)
            {
                int candidate = (index + attempt) % characterPrefabs.Length;
                if (characterPrefabs[candidate] != null)
                {
                    suspectPrefabIndexById[suspectId] = candidate;
                    return candidate;
                }
            }

            throw new InvalidOperationException("No non-null character prefabs are assigned.");
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
