using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>
    /// Drives Low Poly People package triggers (idle/walk/run/wave) plus optional Mixamo sit/stand
    /// and seated talk triggers when those parameters exist on the Animator.
    /// </summary>
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        private const string IdleTrigger = "idle";
        private const string WalkTrigger = "walk";
        private const string RunTrigger = "run";
        private const string WaveTrigger = "wave";
        private const string SitTrigger = "sit";
        private const string StandTrigger = "stand";
        private const string TalkTrigger = "talk";
        private const string StopTalkingTrigger = "stopTalking";

        [Header("Character Animator")]
        [Tooltip("Animator on the selected character prefab. Automatically found in children when empty.")]
        [SerializeField] private Animator characterAnimator;

        private void Awake()
        {
            ResolveAnimator();
            PlayIdle();
        }

        /// <summary>Binds this driver to a newly spawned character's Animator.</summary>
        public void Bind(Animator animator)
        {
            characterAnimator = animator;
            if (characterAnimator == null) ResolveAnimator();
        }

        /// <summary>Plays the package's idle animation.</summary>
        public void PlayIdle()
        {
            PlayTrigger(IdleTrigger);
        }

        /// <summary>Plays the package's walk animation.</summary>
        public void PlayWalk()
        {
            PlayTrigger(WalkTrigger);
        }

        /// <summary>Plays the package's run animation.</summary>
        public void PlayRun()
        {
            PlayTrigger(RunTrigger);
        }

        /// <summary>Plays the package's wave animation.</summary>
        public void PlayWave()
        {
            PlayTrigger(WaveTrigger);
        }

        /// <summary>
        /// Plays a Mixamo sit clip when the Animator has a <c>sit</c> trigger; otherwise falls back to idle.
        /// </summary>
        public void PlaySit()
        {
            if (!PlayTriggerIfPresent(SitTrigger))
                PlayIdle();
        }

        /// <summary>
        /// Plays a Mixamo stand clip when the Animator has a <c>stand</c> trigger; otherwise falls back to idle.
        /// </summary>
        public void PlayStand()
        {
            if (!PlayTriggerIfPresent(StandTrigger))
                PlayIdle();
        }

        /// <summary>
        /// Plays seated talking when the Animator has a <c>talk</c> trigger (Bezi Mixamo sitting-talking state).
        /// No-ops when the parameter is missing so a seated character is not kicked back to standing idle.
        /// </summary>
        public void PlayTalk()
        {
            PlayTriggerIfPresent(TalkTrigger);
        }

        /// <summary>
        /// Returns from seated talking to sitting idle via <c>stopTalking</c> when present.
        /// </summary>
        public void PlayStopTalking()
        {
            PlayTriggerIfPresent(StopTalkingTrigger);
        }

        private void ResolveAnimator()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
        }

        private void PlayTrigger(string triggerName)
        {
            if (characterAnimator == null) return;
            if (!HasTrigger(triggerName)) return;
            characterAnimator.ResetTrigger(triggerName);
            characterAnimator.SetTrigger(triggerName);
        }

        private bool PlayTriggerIfPresent(string triggerName)
        {
            if (characterAnimator == null || !HasTrigger(triggerName)) return false;
            characterAnimator.ResetTrigger(triggerName);
            characterAnimator.SetTrigger(triggerName);
            return true;
        }

        private bool HasTrigger(string triggerName)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter parameter in characterAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                    return true;
            }
            return false;
        }
    }
}
