using System;
using System.Collections;
using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>High-level stages of the one-at-a-time suspect enter / exit sequence.</summary>
    public enum SuspectEnterStage
    {
        Idle,
        Entering,
        Seated,
        Exiting
    }

    /// <summary>
    /// Moves ONE suspect at a time through the shared door → walk → sit path, then exposes a
    /// stand / exit hook for the next suspect. CaseShift wiring lives in
    /// <see cref="SuspectEnterShiftBinder"/> so this pathing module stays usable without CaseCore.
    /// Does not touch browser / monitor systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuspectEnterController : MonoBehaviour
    {
        [Header("Shared Scene Anchors (reuse for every suspect)")]
        [Tooltip("World pose where the suspect appears / starts walking in (typically just inside the door).")]
        [SerializeField] private Transform doorPoint;

        [Tooltip("World pose of the interrogation chair seat.")]
        [SerializeField] private Transform chairPoint;

        [Tooltip("Optional world pose used when leaving. Defaults to doorPoint when empty.")]
        [SerializeField] private Transform exitPoint;

        [Header("Character Pool")]
        [Tooltip("Spawns / swaps Low Poly People prefabs. Auto-found or added on this object when empty.")]
        [SerializeField] private SuspectCharacterPool characterPool;

        [Header("Motion")]
        [Tooltip("Walk speed in meters per second from door to chair (and chair to exit).")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.35f;

        [Tooltip("Seconds to wait after sitting before raising Seated (lets Mixamo sit clip settle).")]
        [SerializeField, Min(0f)] private float sitSettleSeconds = 0.35f;

        [Tooltip("Seconds to wait after standing before walking to the exit.")]
        [SerializeField, Min(0f)] private float standSettleSeconds = 0.25f;

        [Tooltip("If true and sit/stand clips are missing, the character still snaps to the chair / walks out using idle/walk.")]
        [SerializeField] private bool allowPlaceholderPoses = true;

        private Coroutine activeRoutine;
        private string activeSuspectId;

        /// <summary>Raised whenever the enter / exit stage changes.</summary>
        public event Action<SuspectEnterStage> StageChanged;

        /// <summary>Raised after the suspect finishes sitting and is ready for interview UI.</summary>
        public event Action Seated;

        /// <summary>Raised after the suspect finishes exiting and is cleared.</summary>
        public event Action Exited;

        /// <summary>Current enter / exit stage.</summary>
        public SuspectEnterStage Stage { get; private set; }

        /// <summary>True while a walk / sit / exit coroutine is running.</summary>
        public bool IsBusy { get { return Stage == SuspectEnterStage.Entering || Stage == SuspectEnterStage.Exiting; } }

        /// <summary>Suspect id currently on stage (entering, seated, or exiting).</summary>
        public string ActiveSuspectId { get { return activeSuspectId; } }

        private void Awake()
        {
            if (characterPool == null) characterPool = GetComponent<SuspectCharacterPool>();
            if (characterPool == null) characterPool = gameObject.AddComponent<SuspectCharacterPool>();
            // Voice → sit-talk bridge; no Bezi scene edit required beyond the existing enter object.
            if (GetComponent<SuspectTalkAnimationBridge>() == null)
                gameObject.AddComponent<SuspectTalkAnimationBridge>();
            SetStage(SuspectEnterStage.Idle);
        }

        private void OnDisable()
        {
            StopActiveRoutine();
        }

        /// <summary>Bezi / Magdy can assign anchors at runtime if not wired in the Inspector.</summary>
        public void SetSceneAnchors(Transform door, Transform chair, Transform exit = null)
        {
            doorPoint = door;
            chairPoint = chair;
            exitPoint = exit;
        }

        /// <summary>Spawns the suspect at the door, walks to the chair, and sits.</summary>
        public void PlayEnter(string suspectId)
        {
            if (!ValidateAnchors()) return;
            if (IsBusy) StopActiveRoutine();
            activeRoutine = StartCoroutine(EnterRoutine(suspectId));
        }

        /// <summary>
        /// Stand / exit hook: stands (placeholder OK), walks to the exit (or door), then clears the character.
        /// Call before swapping to the next suspect.
        /// </summary>
        public void PlayExit(Action onComplete = null)
        {
            if (characterPool == null || characterPool.ActiveCharacter == null)
            {
                SetStage(SuspectEnterStage.Idle);
                onComplete?.Invoke();
                Exited?.Invoke();
                return;
            }

            if (!ValidateAnchors())
            {
                characterPool.ClearActive();
                SetStage(SuspectEnterStage.Idle);
                onComplete?.Invoke();
                Exited?.Invoke();
                return;
            }

            StopActiveRoutine();
            activeRoutine = StartCoroutine(ExitRoutine(onComplete));
        }

        private IEnumerator EnterRoutine(string suspectId)
        {
            SetStage(SuspectEnterStage.Entering);
            activeSuspectId = suspectId;

            CharacterAnimationDriver driver = characterPool.SpawnForSuspect(suspectId, doorPoint);
            Transform mover = characterPool.ActiveCharacter.transform;
            mover.SetPositionAndRotation(doorPoint.position, doorPoint.rotation);

            if (driver != null) driver.PlayWalk();
            yield return MoveTo(mover, chairPoint);

            SnapTo(mover, chairPoint);
            if (driver != null)
            {
                if (allowPlaceholderPoses || HasSitTrigger(driver))
                    driver.PlaySit();
                else
                    driver.PlayIdle();
            }

            if (sitSettleSeconds > 0f)
                yield return new WaitForSeconds(sitSettleSeconds);

            SetStage(SuspectEnterStage.Seated);
            Seated?.Invoke();
            activeRoutine = null;
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            SetStage(SuspectEnterStage.Exiting);

            Transform mover = characterPool.ActiveCharacter != null ? characterPool.ActiveCharacter.transform : null;
            CharacterAnimationDriver driver = characterPool.ActiveDriver;
            Transform destination = exitPoint != null ? exitPoint : doorPoint;

            if (mover != null)
            {
                if (driver != null)
                {
                    if (allowPlaceholderPoses || HasStandTrigger(driver))
                        driver.PlayStand();
                    else
                        driver.PlayIdle();
                }

                if (standSettleSeconds > 0f)
                    yield return new WaitForSeconds(standSettleSeconds);

                if (driver != null) driver.PlayWalk();
                yield return MoveTo(mover, destination);
            }

            characterPool.ClearActive();
            activeSuspectId = null;
            SetStage(SuspectEnterStage.Idle);
            Exited?.Invoke();
            onComplete?.Invoke();
            activeRoutine = null;
        }

        private IEnumerator MoveTo(Transform mover, Transform destination)
        {
            if (mover == null || destination == null) yield break;

            Vector3 start = mover.position;
            Vector3 end = destination.position;
            float distance = Vector3.Distance(start, end);
            if (distance < 0.001f)
            {
                SnapTo(mover, destination);
                yield break;
            }

            float duration = distance / Mathf.Max(0.1f, walkSpeed);
            float elapsed = 0f;
            Quaternion travelRotation = mover.rotation;
            Vector3 flat = end - start;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                travelRotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            mover.rotation = travelRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                mover.position = Vector3.Lerp(start, end, t);
                // Keep the character aligned with the direction of travel. The destination's
                // seated-facing rotation is applied only once the character reaches the chair.
                mover.rotation = travelRotation;
                yield return null;
            }

            SnapTo(mover, destination);
        }

        private static void SnapTo(Transform mover, Transform target)
        {
            if (mover == null || target == null) return;
            mover.SetPositionAndRotation(target.position, target.rotation);
        }

        private bool ValidateAnchors()
        {
            if (doorPoint == null || chairPoint == null)
            {
                Debug.LogWarning(
                    "SuspectEnterController: assign Door Point and Chair Point (shared for every suspect).",
                    this);
                return false;
            }
            if (characterPool == null)
            {
                Debug.LogWarning("SuspectEnterController: SuspectCharacterPool is missing.", this);
                return false;
            }
            return true;
        }

        private void StopActiveRoutine()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        private void SetStage(SuspectEnterStage stage)
        {
            if (Stage == stage) return;
            Stage = stage;
            StageChanged?.Invoke(stage);
        }

        private static bool HasSitTrigger(CharacterAnimationDriver driver)
        {
            return DriverHasTrigger(driver, "sit");
        }

        private static bool HasStandTrigger(CharacterAnimationDriver driver)
        {
            return DriverHasTrigger(driver, "stand");
        }

        private static bool DriverHasTrigger(CharacterAnimationDriver driver, string triggerName)
        {
            if (driver == null) return false;
            Animator animator = driver.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                    return true;
            }
            return false;
        }
    }
}
