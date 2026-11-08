using CaseClosed.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaseClosed.CameraControl
{
    /// <summary>Owns cursor-safe opening and closing of the screen-space case clipboard.</summary>
    [DisallowMultipleComponent]
    public sealed class ClipboardFocusController : MonoBehaviour
    {
        [Tooltip("Screen-space clipboard UI opened when the prop is targeted.")]
        [SerializeField] private ClipboardCaseUiController clipboardUi;

        [Header("Desk Focus")]
        [Tooltip("Monitor focus controller; monitor focus retains its own camera and browser input behavior.")]
        [SerializeField] private MonitorFocusController monitorFocus;

        private Camera playerCamera;
        private CameraLookController cameraLookController;
        private CursorLockMode previousCursorLockState;
        private bool previousCursorVisibility;

        private int escapeHandledFrame = -1;

        /// <summary>True on the frame this controller handled Escape to close the clipboard.</summary>
        public bool HandledEscapeThisFrame { get { return escapeHandledFrame == Time.frameCount; } }

        private bool cursorStateCaptured;

        /// <summary>True while the case clipboard interface is open.</summary>
        public bool IsClipboardOpen { get { return clipboardUi != null && clipboardUi.IsOpen; } }

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (clipboardUi == null)
                clipboardUi = FindAnyObjectByType<ClipboardCaseUiController>();
            if (monitorFocus == null)
                monitorFocus = FindAnyObjectByType<MonitorFocusController>();
            cameraLookController = playerCamera != null ? playerCamera.GetComponent<CameraLookController>() : null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || clipboardUi == null || !clipboardUi.IsOpen)
                return;

            if (!keyboard.escapeKey.wasPressedThisFrame)
                return;

            escapeHandledFrame = Time.frameCount;
            CloseClipboard();
        }

        private void LateUpdate()
        {
            // The UI's Close button can hide the panel without calling this controller.
            if (cursorStateCaptured && !IsClipboardOpen)
                CloseClipboard();

            if (clipboardUi != null && clipboardUi.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Time.timeScale > 0f && cameraLookController != null && cameraLookController.LookEnabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            CloseClipboard();
        }

        /// <summary>Opens the screen-space clipboard and releases the cursor for UI interaction.</summary>
        public void OpenClipboard()
        {
            if (clipboardUi == null || clipboardUi.IsOpen || Time.timeScale <= 0f ||
                (monitorFocus != null && (monitorFocus.IsFocused || monitorFocus.IsTransitioning)))
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
