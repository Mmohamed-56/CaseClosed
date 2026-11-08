using CaseClosed.CameraControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>Builds a runtime pause overlay with volume, resume, return-to-menu, and quit controls.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string ReturnToMenuButtonName = "ReturnToMenuButton";
        private const string ReturnToMenuButtonLabel = "Return to Menu";
        private const float MenuTimeScale = 1f;
        private const float PanelWidth = 460f;
        private const float PanelHeight = 380f;
        private const float ButtonWidth = 300f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 60f;
        private const float QuitButtonOffsetY = 42f;

        private static readonly Vector2 ButtonAnchor = new Vector2(0.5f, 0f);

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.68f);
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.16f, 0.98f);
        private static readonly Color AccentColor = new Color(0.36f, 0.58f, 0.72f, 1f);
        private static readonly Color TextColor = new Color(0.96f, 0.95f, 0.90f, 1f);

        private GameObject canvasObject;
        private GameObject overlayObject;
        private GameObject ownedEventSystemObject;
        private Sprite runtimeWhiteSprite;
        private Texture2D runtimeWhiteTexture;
        private Slider volumeSlider;

        [Header("Behaviour")]
        [SerializeField] private bool enableEscapeToggle = true;
        [SerializeField] private CameraLookController cameraLookController;
        [SerializeField] private MonitorFocusController monitorFocusController;
        [SerializeField] private NotepadFocusController notepadFocusController;
        [SerializeField] private ClipboardFocusController clipboardFocusController;

        private bool isPaused;
        private float previousTimeScale = 1f;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockMode;

        private void OnEnable()
        {
            ResolveFocusControllers();
            BuildOverlay();
            EnsureEventSystem();
        }

        private void ResolveFocusControllers()
        {
            if (cameraLookController == null)
                cameraLookController = FindAnyObjectByType<CameraLookController>();
            if (monitorFocusController == null)
                monitorFocusController = FindAnyObjectByType<MonitorFocusController>();
            if (notepadFocusController == null)
                notepadFocusController = FindAnyObjectByType<NotepadFocusController>();
            if (clipboardFocusController == null)
                clipboardFocusController = FindAnyObjectByType<ClipboardFocusController>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (!enableEscapeToggle || keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (!isPaused && IsDeskInteractionOpen())
                return;

            SetPaused(!isPaused);
        }

        private bool IsDeskInteractionOpen()
        {
            return (monitorFocusController != null &&
                    (monitorFocusController.IsFocused || monitorFocusController.IsTransitioning)) ||
                   (notepadFocusController != null &&
                    (notepadFocusController.IsFocused || notepadFocusController.IsTransitioning)) ||
                   (clipboardFocusController != null &&
                    (clipboardFocusController.IsClipboardOpen || clipboardFocusController.HandledEscapeThisFrame));
        }

        private void OnDisable()
        {
            if (isPaused)
                RestoreGameState();

            isPaused = false;
            if (canvasObject != null)
                Destroy(canvasObject);
            if (ownedEventSystemObject != null)
                Destroy(ownedEventSystemObject);
            if (runtimeWhiteSprite != null)
                Destroy(runtimeWhiteSprite);
            if (runtimeWhiteTexture != null)
                Destroy(runtimeWhiteTexture);

            canvasObject = null;
            overlayObject = null;
            ownedEventSystemObject = null;
            runtimeWhiteSprite = null;
            runtimeWhiteTexture = null;
            volumeSlider = null;
        }

        /// <summary>Resumes gameplay and restores the time scale and cursor state from before pausing.</summary>
        public void OnResumeClicked()
        {
            SetPaused(false);
        }

        /// <summary>Restores gameplay input state, then loads the main menu with normal time and an unlocked cursor.</summary>
        public void OnReturnToMenuClicked()
        {
            if (!isPaused)
                return;

            // Reuse Resume cleanup before unloading gameplay; OnDisable must not restore its cursor over the menu.
            SetPaused(false);
            Time.timeScale = MenuTimeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }

        /// <summary>Quits the player build, or exits Play Mode when called in the Unity Editor.</summary>
        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildOverlay()
        {
            runtimeWhiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PauseMenuWhiteTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeWhiteTexture.SetPixel(0, 0, Color.white);
            runtimeWhiteTexture.Apply(false, true);
            runtimeWhiteSprite = Sprite.Create(runtimeWhiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            runtimeWhiteSprite.name = "PauseMenuWhiteSprite";
            runtimeWhiteSprite.hideFlags = HideFlags.HideAndDontSave;

            canvasObject = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayObject = new GameObject("PauseOverlay", typeof(RectTransform));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            StretchToParent(overlayObject.GetComponent<RectTransform>());
            CreateImage(overlayObject.transform, "Backdrop", BackdropColor, true, true);

            GameObject panel = CreateAnchoredImage(overlayObject.transform, "PausePanel", PanelColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight), true);

            CreateText(panel.transform, "Title", "PAUSED", 36, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(400f, 54f), TextAnchor.MiddleCenter);
            CreateText(panel.transform, "VolumeLabel", "Volume", 22, FontStyle.Normal,
                new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(340f, 34f), TextAnchor.MiddleLeft);

            volumeSlider = CreateSlider(panel.transform);
            RectTransform sliderRect = volumeSlider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 1f);
            sliderRect.anchorMax = new Vector2(0.5f, 1f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, -158f);
            sliderRect.sizeDelta = new Vector2(340f, 24f);
            volumeSlider.value = Mathf.Clamp01(AudioListener.volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);

            Vector2 buttonSize = new Vector2(ButtonWidth, ButtonHeight);
            CreateButton(panel.transform, "ResumeButton", "Resume / Continue", AccentColor,
                ButtonAnchor, new Vector2(0f, QuitButtonOffsetY + ButtonSpacing + ButtonSpacing), buttonSize, OnResumeClicked);
            CreateButton(panel.transform, ReturnToMenuButtonName, ReturnToMenuButtonLabel, AccentColor,
                ButtonAnchor, new Vector2(0f, QuitButtonOffsetY + ButtonSpacing), buttonSize, OnReturnToMenuClicked);
            CreateButton(panel.transform, "QuitButton", "Quit", new Color(0.43f, 0.19f, 0.17f, 1f),
                ButtonAnchor, new Vector2(0f, QuitButtonOffsetY), buttonSize, OnQuitClicked);

            overlayObject.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                ownedEventSystemObject = new GameObject("PauseMenuEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                return;
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private void SetPaused(bool paused)
        {
            if (paused == isPaused || !isActiveAndEnabled)
                return;

            isPaused = paused;
            if (overlayObject != null)
                overlayObject.SetActive(paused);

            if (paused)
            {
                previousTimeScale = Time.timeScale;
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                Time.timeScale = 0f;
                if (cameraLookController != null)
                    cameraLookController.SetLookEnabled(false);
                else
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                if (volumeSlider != null)
                    volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(AudioListener.volume));
            }
            else
            {
                RestoreGameState();
            }
        }

        private void RestoreGameState()
        {
            Time.timeScale = previousTimeScale;
            if (cameraLookController != null)
                cameraLookController.SetLookEnabled(true);
            else
            {
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLockMode;
            }
        }

        private static void SetVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        private Slider CreateSlider(Transform parent)
        {
            GameObject sliderObject = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            Image background = CreateImage(sliderObject.transform, "Background", new Color(0.16f, 0.20f, 0.23f, 1f), true, true);
            RectTransform backgroundRect = background.rectTransform;
            StretchToParent(backgroundRect);
            backgroundRect.offsetMin = new Vector2(0f, 5f);
            backgroundRect.offsetMax = new Vector2(0f, -5f);
            slider.targetGraphic = background;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            StretchToParent(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(5f, 5f);
            fillAreaRect.offsetMax = new Vector2(-5f, -5f);

            Image fill = CreateImage(fillArea.transform, "Fill", AccentColor, true, false);
            StretchToParent(fill.rectTransform);
            slider.fillRect = fill.rectTransform;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            StretchToParent(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            Image handle = CreateImage(handleArea.transform, "Handle", TextColor, true, true);
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(18f, 28f);
            slider.handleRect = handleRect;
            return slider;
        }

        private void CreateButton(Transform parent, string name, string label, Color fillColor,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction callback)
        {
            GameObject buttonObject = CreateAnchoredImage(parent, name, fillColor, anchor, anchoredPosition, size, true);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(callback);
            CreateText(buttonObject.transform, "Label", label, 20, FontStyle.Bold,
                Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
        }

        private GameObject CreateAnchoredImage(Transform parent, string name, Color color,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool raycastTarget)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.sprite = runtimeWhiteSprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return gameObject;
        }

        private Image CreateImage(Transform parent, string name, Color color, bool stretch, bool raycastTarget)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (stretch)
                StretchToParent(rect);
            Image image = gameObject.GetComponent<Image>();
            image.sprite = runtimeWhiteSprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle fontStyle,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, bool stretch = false)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (stretch)
            {
                StretchToParent(rect);
            }
            else
            {
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            Text text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = TextColor;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
