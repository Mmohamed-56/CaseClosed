using CaseClosed.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaseClosed.CameraControl
{
    /// <summary>Opens the notepad popup from its look interaction without moving the player camera.</summary>
    [DisallowMultipleComponent]
    public sealed class NotepadFocusController : MonoBehaviour
    {
        [Header("Legacy Scene Reference")]
        [Tooltip("Retained for scene compatibility; the screen-space popup does not move the camera toward the paper.")]
        [SerializeField] private Transform notepadSurface;

        [Header("Desk Views")]
        [SerializeField] private DeskClaimNotepad notepad;
        [SerializeField] private MonitorFocusController monitorFocusController;
        [SerializeField] private ClipboardFocusController clipboardFocusController;

        private Camera playerCamera;
        private CameraLookController cameraLookController;
        private CursorLockMode previousCursorLockState;
        private bool previousCursorVisibility;
        private bool cursorStateCaptured;
        private bool isFocused;
        private bool allowLookInteractionEntry;
        private int escapeHandledFrame = -1;

        /// <summary>Whether the notepad popup is open or it handled Escape this frame.</summary>
        public bool IsFocused { get { return isFocused || escapeHandledFrame == Time.frameCount; } }

        /// <summary>The popup opens immediately and no longer has a camera transition.</summary>
        public bool IsTransitioning { get { return false; } }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (isFocused && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                escapeHandledFrame = Time.frameCount;
                ExitFocus();
            }
        }

        private void LateUpdate()
        {
            // Restore look even if another UI path ends typing directly.
            if (isFocused && (notepad == null || !notepad.IsEditing))
                ExitFocus();

            if (isFocused)
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
            if (isFocused)
                ExitFocus();
        }

        /// <summary>Enters the notepad popup from its configured look-target interaction.</summary>
        public void EnterFocus()
        {
            allowLookInteractionEntry = true;
            try
            {
                TryEnterFocus();
            }
            finally
            {
                allowLookInteractionEntry = false;
            }
        }

        /// <summary>Opens the screen-space notepad only when invoked by the look-target interaction.</summary>
        public bool TryEnterFocus()
        {
            if (!allowLookInteractionEntry)
                return false;

            EnsureReferences();
            if (notepad == null || Time.timeScale <= 0f)
                return false;
            if ((monitorFocusController != null &&
                 (monitorFocusController.IsFocused || monitorFocusController.IsTransitioning)) ||
                (clipboardFocusController != null && clipboardFocusController.IsClipboardOpen))
                return false;
            if (isFocused)
                return true;

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

            isFocused = true;
            notepad.BeginTyping();
            return true;
        }

        /// <summary>Closes the popup, preserves typed notes, and restores normal camera look.</summary>
        public void ExitFocus()
        {
            if (!isFocused)
                return;

            isFocused = false;
            if (notepad != null)
                notepad.EndTyping();

            if (cameraLookController != null)
                cameraLookController.SetLookEnabled(true);
            else if (cursorStateCaptured)
            {
                Cursor.lockState = previousCursorLockState;
                Cursor.visible = previousCursorVisibility;
            }

            cursorStateCaptured = false;
        }

        private void EnsureReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponent<Camera>();
                if (playerCamera == null)
                    playerCamera = Camera.main;
            }

            if (cameraLookController == null && playerCamera != null)
                cameraLookController = playerCamera.GetComponent<CameraLookController>();
            if (monitorFocusController == null)
                monitorFocusController = FindAnyObjectByType<MonitorFocusController>();
            if (notepad == null)
                notepad = FindAnyObjectByType<DeskClaimNotepad>();
            if (clipboardFocusController == null)
                clipboardFocusController = FindAnyObjectByType<ClipboardFocusController>();
            if (notepadSurface == null && notepad != null)
            {
                Transform paper = notepad.transform.Find("Paper");
                notepadSurface = paper != null ? paper : notepad.transform;
            }
        }
    }
}
