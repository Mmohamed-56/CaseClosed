using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>
    /// Script hook: <see cref="SuspectDialogueEvents.SpeechStarted"/> /
    /// <see cref="SuspectDialogueEvents.SpeechEnded"/> → seated talk / stopTalking on the
    /// active <see cref="CharacterAnimationDriver"/>. No scene wiring beyond the enter pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuspectTalkAnimationBridge : MonoBehaviour
    {
        [Header("Character")]
        [Tooltip("Pool that owns the seated suspect. Auto-found on this object / in parents when empty.")]
        [SerializeField] private SuspectCharacterPool characterPool;

        [Tooltip("Optional direct driver override. When set, pool lookup is skipped.")]
        [SerializeField] private CharacterAnimationDriver overrideDriver;

        private void Awake()
        {
            ResolvePool();
        }

        private void OnEnable()
        {
            SuspectDialogueEvents.SpeechStarted += HandleSpeechStarted;
            SuspectDialogueEvents.SpeechEnded += HandleSpeechEnded;
        }

        private void OnDisable()
        {
            SuspectDialogueEvents.SpeechStarted -= HandleSpeechStarted;
            SuspectDialogueEvents.SpeechEnded -= HandleSpeechEnded;
            // Leave the seated idle pose if TTS is cancelled while this bridge is disabled.
            CharacterAnimationDriver driver = ResolveDriver();
            if (driver != null)
                driver.PlayStopTalking();
        }

        private void HandleSpeechStarted(string suspectId)
        {
            CharacterAnimationDriver driver = ResolveDriver();
            if (driver == null)
                return;

            driver.PlayTalk();
        }

        private void HandleSpeechEnded(string suspectId)
        {
            CharacterAnimationDriver driver = ResolveDriver();
            if (driver == null)
                return;

            driver.PlayStopTalking();
        }

        private CharacterAnimationDriver ResolveDriver()
        {
            if (overrideDriver != null)
                return overrideDriver;

            ResolvePool();
            return characterPool != null ? characterPool.ActiveDriver : null;
        }

        private void ResolvePool()
        {
            if (characterPool != null)
                return;

            characterPool = GetComponent<SuspectCharacterPool>();
            if (characterPool == null)
                characterPool = GetComponentInParent<SuspectCharacterPool>();
            if (characterPool == null)
                characterPool = FindFirstObjectByType<SuspectCharacterPool>();
        }
    }
}
