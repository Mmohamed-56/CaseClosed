using UnityEngine;
using UnityEngine.InputSystem;

namespace CaseClosed.CameraControl
{
    /// <summary>Rotates the active camera from mouse movement while the player is in desk exploration mode.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraLookController : MonoBehaviour
    {
        private const float DefaultSensitivity = 0.08f;
        private const float MinimumPitch = -80f;
        private const float MaximumPitch = 80f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.001f)] private float sensitivity = DefaultSensitivity;
        [SerializeField] private bool invertVertical;

        private float yaw;
        private float pitch;
        private bool lookEnabled = true;

        /// <summary>Whether desk exploration mouse-look is enabled.</summary>
        public bool LookEnabled { get { return lookEnabled; } }

        private void Awake()
        {
            SyncAnglesFromTransform();
        }

        private void OnEnable()
        {
            SetLookEnabled(true);
        }

        private void OnDisable()
        {
            UnlockCursor();
        }

        private void Update()
        {
            if (!lookEnabled || Time.timeScale <= 0f)
                return;

            if (Cursor.lockState != CursorLockMode.Locked)
                LockCursor();

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            float verticalDirection = invertVertical ? 1f : -1f;
            pitch = Mathf.Clamp(pitch + delta.y * sensitivity * verticalDirection, MinimumPitch, MaximumPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>Enables or disables mouse look and applies the corresponding cursor mode.</summary>
        /// <param name="enabled">True for locked-cursor camera look; false for UI or browser interaction.</param>
        public void SetLookEnabled(bool enabled)
        {
            lookEnabled = enabled;
            if (enabled)
            {
                SyncAnglesFromTransform();
                LockCursor();
            }
            else
            {
                UnlockCursor();
            }
        }

        private void SyncAnglesFromTransform()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f)
                pitch -= 360f;
            pitch = Mathf.Clamp(pitch, MinimumPitch, MaximumPitch);
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
