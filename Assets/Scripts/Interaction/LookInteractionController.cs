using CaseClosed.CameraControl;
using CaseClosed.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CaseClosed.Interaction
{
    /// <summary>Raycasts from the center of the player camera and displays contextual look interaction UI.</summary>
    [DisallowMultipleComponent]
    public sealed class LookInteractionController : MonoBehaviour
    {
        private static readonly Color CrosshairColor = new Color(0.96f, 0.96f, 0.91f, 0.95f);
        private static readonly Color PromptPanelColor = new Color(0.035f, 0.045f, 0.055f, 0.84f);
        private static readonly Color PromptTextColor = new Color(0.98f, 0.97f, 0.92f, 1f);

        [Header("Raycast")]
        [Tooltip("Camera used for the center-screen look ray. Defaults to the main camera.")]
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;

        [Header("Optional interaction blockers")]
        [Tooltip("Interaction is suppressed while the monitor is focused or transitioning.")]
        [SerializeField] private MonitorFocusController monitorFocusController;
        [Tooltip("Interaction is suppressed while the clipboard is open.")]
        [SerializeField] private ClipboardCaseUiController clipboardCaseUiController;
        [Tooltip("Assign the pause controller; interaction is suppressed while the game is paused.")]
        [SerializeField] private PauseMenuController pauseMenuController;

        [Header("Runtime UI")]
        [SerializeField, Min(10)] private int promptFontSize = 20;

        private GameObject canvasObject;
        private GameObject promptPanelObject;
        private Text promptText;
        private Sprite runtimeWhiteSprite;
        private Texture2D runtimeWhiteTexture;
        private LookInteractable currentTarget;

        private void OnEnable()
        {
            ResolveReferences();
            if (canvasObject == null)
                BuildOverlay();
        }

        private void Update()
        {
            if (IsInteractionBlocked())
            {
                SetCurrentTarget(null);
                SetOverlayVisible(false);
                return;
            }

            SetOverlayVisible(true);
            LookInteractable target = FindLookTarget();
            SetCurrentTarget(target);
            UpdatePrompt(target);

            Keyboard keyboard = Keyboard.current;
            if (target != null && keyboard != null && keyboard.fKey.wasPressedThisFrame)
                target.InvokeInteraction();
        }

        private void OnDisable()
        {
            SetCurrentTarget(null);
            if (canvasObject != null)
                Destroy(canvasObject);
            if (runtimeWhiteSprite != null)
                Destroy(runtimeWhiteSprite);
            if (runtimeWhiteTexture != null)
                Destroy(runtimeWhiteTexture);

            canvasObject = null;
            promptPanelObject = null;
            promptText = null;
            runtimeWhiteSprite = null;
            runtimeWhiteTexture = null;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (monitorFocusController == null)
                monitorFocusController = FindAnyObjectByType<MonitorFocusController>();
            if (clipboardCaseUiController == null)
                clipboardCaseUiController = FindAnyObjectByType<ClipboardCaseUiController>();
            if (pauseMenuController == null)
                pauseMenuController = FindAnyObjectByType<PauseMenuController>();
        }

        private bool IsInteractionBlocked()
        {
            return !isActiveAndEnabled || playerCamera == null || Time.timeScale <= 0f ||
                   (monitorFocusController != null &&
                    (monitorFocusController.IsFocused || monitorFocusController.IsTransitioning)) ||
                   (clipboardCaseUiController != null && clipboardCaseUiController.IsOpen) ||
                   (pauseMenuController != null && IsPauseOverlayOpen());
        }

        private bool IsPauseOverlayOpen()
        {
            Transform pauseOverlay = pauseMenuController.transform.Find("PauseMenuCanvas/PauseOverlay");
            return pauseOverlay != null && pauseOverlay.gameObject.activeInHierarchy;
        }

        private LookInteractable FindLookTarget()
        {
            if (playerCamera == null)
                return null;

            Ray centerRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(centerRay, out RaycastHit hit, interactionDistance, raycastLayers, QueryTriggerInteraction.Ignore))
                return null;

            LookInteractable target = hit.collider.GetComponentInParent<LookInteractable>();
            return target != null && target.IsAvailable ? target : null;
        }

        private void SetCurrentTarget(LookInteractable target)
        {
            if (currentTarget == target)
                return;

            if (currentTarget != null)
                currentTarget.InvokeHoverExit();

            currentTarget = target;
            if (currentTarget != null)
                currentTarget.InvokeHoverEnter();
        }

        private void BuildOverlay()
        {
            runtimeWhiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "LookInteractionWhiteTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeWhiteTexture.SetPixel(0, 0, Color.white);
            runtimeWhiteTexture.Apply(false, true);
            runtimeWhiteSprite = Sprite.Create(runtimeWhiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            runtimeWhiteSprite.name = "LookInteractionWhiteSprite";
            runtimeWhiteSprite.hideFlags = HideFlags.HideAndDontSave;

            canvasObject = new GameObject("LookInteractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateImage(canvasObject.transform, "CrosshairHorizontal", CrosshairColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 2f));
            CreateImage(canvasObject.transform, "CrosshairVertical", CrosshairColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 14f));

            promptPanelObject = CreateImage(canvasObject.transform, "InteractionPromptPanel", PromptPanelColor,
                new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(520f, 48f));
            promptText = CreateText(promptPanelObject.transform, "InteractionPromptText", string.Empty);
            RectTransform textRect = promptText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);
            promptPanelObject.SetActive(false);
        }

        private void UpdatePrompt(LookInteractable target)
        {
            bool hasTarget = target != null;
            if (promptPanelObject != null && promptPanelObject.activeSelf != hasTarget)
                promptPanelObject.SetActive(hasTarget);
            if (hasTarget && promptText != null)
            {
                string label = string.IsNullOrWhiteSpace(target.PromptText) ? "Interact" : target.PromptText.Trim();
                promptText.text = "F   " + label;
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (canvasObject != null && canvasObject.activeSelf != visible)
                canvasObject.SetActive(visible);
        }

        private GameObject CreateImage(Transform parent, string objectName, Color color,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = runtimeWhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private Text CreateText(Transform parent, string objectName, string value)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = promptFontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = PromptTextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
