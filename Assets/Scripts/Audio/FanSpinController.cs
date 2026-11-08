using UnityEngine;

namespace CaseClosed.Audio
{
    /// <summary>Subtly spins a ceiling fan around its local shaft axis.</summary>
    [DisallowMultipleComponent]
    public sealed class FanSpinController : MonoBehaviour
    {
        [Header("Fan Rotation")]
        [Tooltip("Rotation axis in the fan object's local space; local up follows the ceiling shaft.")]
        [SerializeField] private Vector3 localSpinAxis = Vector3.up;

        [Tooltip("Fan rotation speed in degrees per second.")]
        [SerializeField, Min(0f)] private float rotationDegreesPerSecond = 45f;

        private void Update()
        {
            if (localSpinAxis.sqrMagnitude <= Mathf.Epsilon || rotationDegreesPerSecond <= 0f)
                return;

            transform.Rotate(localSpinAxis.normalized, rotationDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
