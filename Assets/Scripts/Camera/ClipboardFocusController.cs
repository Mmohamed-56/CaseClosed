using CaseClosed.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaseClosed.CameraControl
{
    /// <summary>Opens the screen-space case clipboard when the player looks at the desk prop and presses F.</summary>
    [DisallowMultipleComponent]
    public sealed class ClipboardFocusController : MonoBehaviour
    {
        private const float ClipboardRayDistance = 5f;

        [Header("Clipboard Target")]
        [Tooltip("Root of the 3D clipboard prop. Child colliders are used for look targeting.")]
        [SerializeField] private Transform clipboardRoot;

        [Tooltip("Screen-space clipboard UI opened when the prop is targeted.")]
        [SerializeField] private ClipboardCaseUiController clipboardUi;

        [Header("Desk Focus")]
        [Tooltip("Monitor focus controller; monitor focus retains its own camera and browser input behavior.")]
        [SerializeField] private MonitorFocusController monitorFocus;

        private Camera playerCamera;
        private CameraLookController cameraLookController;
        private Collider[] clipboardColliders;
        private CursorLockMode previousCursorLockState;
        private bool previousCursorVisibility;
        private bool cursorStateCaptured;

        /// <summary>True while the case clipboard interface is open.</summary>
        public bool IsClipboardOpen { get { return clipboardUi != null && clipboardUi.IsOpen; } }

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (clipboardRoot == null)
            {
                GameObject clipboard = GameObject.Find("ClipBoard");
                if (clipboard != null)
                    clipboardRoot = clipboard.transform;
            }

            if (clipboardUi == null)
                clipboardUi = FindAnyObjectByType<ClipboardCaseUiController>();
            if (monitorFocus == null)
                monitorFocus = FindAnyObjectByType<MonitorFocusController>();
            cameraLookController = playerCamera != null ? playerCamera.GetComponent<CameraLookController>() : null;

            CacheClipboardColliders();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || clipboardUi == null)
                return;

            bool togglePressed = keyboard.fKey.wasPressedThisFrame;
            bool closePressed = keyboard.escapeKey.wasPressedThisFrame;

            if (clipboardUi.IsOpen)
            {
                if (togglePressed || closePressed)
                    CloseClipboard();
                return;
            }

            if (!togglePressed || Time.timeScale <= 0f || (monitorFocus != null && monitorFocus.IsFocused))
                return;

            if (!IsLookingAtClipboard())
                return;

            previousCursorLockState = Cursor.lockState;
            previousCursorVisibility = Cursor.visible;
            cursorStateCaptured = true;
            if (cameraLookController != null)
                cameraLookController.SetLookEnabled(false);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            clipboardUi.SetOpen(true);
        }

        private void LateUpdate()
        {
            if (clipboardUi != null && clipboardUi.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnDisable()
        {
            CloseClipboard();
        }

        /// <summary>Refreshes cached target colliders after clipboard scene wiring changes.</summary>
        public void RefreshClipboardTarget()
        {
            CacheClipboardColliders();
        }

        private void CacheClipboardColliders()
        {
            clipboardColliders = clipboardRoot != null
                ? clipboardRoot.GetComponentsInChildren<Collider>(true)
                : System.Array.Empty<Collider>();
        }

        private bool IsLookingAtClipboard()
        {
            if (playerCamera == null || clipboardRoot == null)
                return false;

            if (clipboardColliders == null || clipboardColliders.Length == 0)
                CacheClipboardColliders();

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, ClipboardRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            Transform hitTransform = hit.collider.transform;
            return hitTransform == clipboardRoot || hitTransform.IsChildOf(clipboardRoot);
        }

        private void CloseClipboard()
        {
            if (clipboardUi != null && clipboardUi.IsOpen)
                clipboardUi.SetOpen(false);

            if (!cursorStateCaptured)
                return;

            if (cameraLookController != null)
                cameraLookController.SetLookEnabled(true);
            else
            {
                Cursor.lockState = previousCursorLockState;
                Cursor.visible = previousCursorVisibility;
            }
            cursorStateCaptured = false;
        }
    }
}
