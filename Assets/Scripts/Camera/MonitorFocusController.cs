using CaseClosed.Browser;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaseClosed.CameraControl
{
    /// <summary>Smoothly focuses the first-person camera on the monitor for readable, interactive browsing.</summary>
    public sealed class MonitorFocusController : MonoBehaviour
    {
        [Header("Monitor Target")]
        [Tooltip("The browser display surface the camera focuses on and the player clicks to zoom in.")]
        [SerializeField] private Transform displaySurface;

        [Tooltip("Collider on the display surface used to detect clicks while not focused.")]
        [SerializeField] private Collider displaySurfaceCollider;

        [Tooltip("Browser manager that receives pointer and keyboard input while the display is focused.")]
        [SerializeField] private RecordsWebsiteBrowserManager browserManager;

        [Header("Focus Framing")]
        [Tooltip("Distance from the display plane to the focused camera position.")]
        [SerializeField, Min(0.15f)] private float focusDistance = 0.72f;

        [Tooltip("Field of view while focused on the monitor.")]
        [SerializeField, Range(20f, 55f)] private float focusedFieldOfView = 32f;

        [Tooltip("Seconds used to smoothly move into or out of monitor focus.")]
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.42f;

        [Header("Controls")]
        [Tooltip("Key that toggles monitor focus. Escape exits focus mode.")]
        [SerializeField] private Key focusToggleKey = Key.F;

        private Camera playerCamera;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private float returnFieldOfView;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float targetFieldOfView;
        private Vector3 positionVelocity;
        private float transitionElapsed;
        private bool isFocused;
        private bool isTransitioning;

        /// <summary>Whether the player is focused on the monitor or transitioning into it.</summary>
        public bool IsFocused { get { return isFocused; } }

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;

            if (displaySurface == null)
            {
                GameObject surface = GameObject.Find("Browser Display Surface");
                if (surface != null) displaySurface = surface.transform;
            }

            if (displaySurfaceCollider == null && displaySurface != null)
                displaySurfaceCollider = displaySurface.GetComponent<Collider>();

            if (browserManager == null)
                browserManager = FindAnyObjectByType<RecordsWebsiteBrowserManager>();

            if (browserManager != null)
                browserManager.SetDisplayInputEnabled(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (!isFocused && !isTransitioning)
            {
                bool togglePressed = keyboard != null && keyboard[focusToggleKey].wasPressedThisFrame;
                bool surfaceClicked = mouse != null && mouse.leftButton.wasPressedThisFrame && IsPointerOverDisplay(mouse.position.ReadValue());
                if (togglePressed || surfaceClicked) EnterFocus();
            }
            else if (isFocused && !isTransitioning)
            {
                bool exitPressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                                   (keyboard != null && keyboard[focusToggleKey].wasPressedThisFrame);
                if (exitPressed) ExitFocus();
            }

            if (isTransitioning) UpdateTransition();
        }

        /// <summary>Focuses the camera on the browser surface and enables its web input.</summary>
        public void EnterFocus()
        {
            if (playerCamera == null || displaySurface == null || isFocused || isTransitioning) return;

            returnPosition = playerCamera.transform.position;
            returnRotation = playerCamera.transform.rotation;
            returnFieldOfView = playerCamera.fieldOfView;

            Vector3 viewerSide = (playerCamera.transform.position - displaySurface.position).normalized;
            Vector3 screenFacing = Vector3.Dot(displaySurface.forward, viewerSide) >= 0f
                ? displaySurface.forward
                : -displaySurface.forward;

            targetPosition = displaySurface.position + screenFacing * focusDistance;
            targetRotation = Quaternion.LookRotation(-screenFacing, displaySurface.up);
            targetFieldOfView = focusedFieldOfView;

            isFocused = true;
            BeginTransition();
        }

        /// <summary>Returns the camera to its previous position and disables web input.</summary>
        public void ExitFocus()
        {
            if (playerCamera == null || !isFocused || isTransitioning) return;

            if (browserManager != null)
                browserManager.SetDisplayInputEnabled(false);

            targetPosition = returnPosition;
            targetRotation = returnRotation;
            targetFieldOfView = returnFieldOfView;
            isFocused = false;
            BeginTransition();
        }

        private void BeginTransition()
        {
            transitionElapsed = 0f;
            positionVelocity = Vector3.zero;
            isTransitioning = true;
        }

        private void UpdateTransition()
        {
            transitionElapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.1f, transitionDuration));
            float smoothTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);

            playerCamera.transform.position = Vector3.SmoothDamp(
                playerCamera.transform.position,
                targetPosition,
                ref positionVelocity,
                Mathf.Max(0.05f, transitionDuration * 0.28f),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRotation, smoothTime);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFieldOfView, smoothTime);

            if (normalizedTime < 1f) return;

            playerCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
            playerCamera.fieldOfView = targetFieldOfView;
            isTransitioning = false;

            if (isFocused && browserManager != null)
                browserManager.SetDisplayInputEnabled(true);
        }

        private bool IsPointerOverDisplay(Vector2 screenPosition)
        {
            if (playerCamera == null || displaySurfaceCollider == null) return false;
            Ray ray = playerCamera.ScreenPointToRay(screenPosition);
            return displaySurfaceCollider.Raycast(ray, out _, playerCamera.farClipPlane);
        }
    }
}
