using System.Collections;
using UnityEngine;

namespace CaseClosed.Characters
{
    /// <summary>Moves the placed female01 test character to the suspect chair, then sits.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class Female01WalkToChairTest : MonoBehaviour
    {
        private const string SitTrigger = "sit";

        [SerializeField] private Transform chairPoint;
        [SerializeField] private Transform facingTarget;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.35f;
        [SerializeField, Min(0f)] private float arrivalDistance = 0.025f;
        [SerializeField, Min(0f)] private float seatForwardOffset = 0.22f;

        private Animator characterAnimator;

        private void Awake()
        {
            characterAnimator = GetComponentInChildren<Animator>(true);
        }

        private IEnumerator Start()
        {
            if (chairPoint == null || characterAnimator == null)
            {
                Debug.LogWarning("Female01WalkToChairTest needs an Animator and chair point.", this);
                yield break;
            }

            Vector3 destination = chairPoint.position;
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            while (Vector3.Distance(transform.position, destination) > arrivalDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destination,
                    walkSpeed * Time.deltaTime);
                yield return null;
            }

            transform.SetPositionAndRotation(destination, chairPoint.rotation);
            if (facingTarget != null)
            {
                Vector3 cameraDirection = facingTarget.position - transform.position;
                cameraDirection.y = 0f;
                if (cameraDirection.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(cameraDirection.normalized, Vector3.up);
            }

            Vector3 seatForward = transform.forward;
            seatForward.y = 0f;
            if (seatForward.sqrMagnitude > 0.0001f)
                transform.position += seatForward.normalized * seatForwardOffset;

            characterAnimator.ResetTrigger(SitTrigger);
            characterAnimator.SetTrigger(SitTrigger);
        }
    }
}
