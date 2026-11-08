using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Builds a pinned-paper menu over the corkboard scene, follows the mouse with its warm spotlight,
    /// and fades into the selected gameplay mode.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "SampleScene";
        public const string SfxResourcesFolder = "SFX";
        public const string MenuFunkyResourcesPath = "Music/menu_funky";

        /// <summary>Mode selected from the menu for the gameplay scene that is about to load.</summary>
        public static bool StartDemoMode { get; private set; }

        [Header("Flow")]
        [SerializeField] private string gameplaySceneName = DefaultGameplayScene;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.85f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.35f;

        [Header("Audio")]
        [Tooltip("Funky menu theme. Drop Magdy track at Assets/Audio/Music/menu_funky.* and assign here (or Resources/Music/menu_funky). Falls back to office_ambience.")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.38f;
        [SerializeField, Range(0f, 1f)] private float uiClickVolume = 0.85f;

        private const float BoardClutterIntervalMin = 1.4f;
        private const float BoardClutterIntervalMax = 3.6f;
        private const float BoardNoteLifeMin = 4.2f;
        private const float BoardNoteLifeMax = 9.5f;
        private const float BoardNoteAnimationSeconds = 0.24f;
        private const int MaximumBoardNotes = 4;

        private static readonly Vector2[] BoardNoteSlots =
        {
            new Vector2(0.08f, 0.68f),
            new Vector2(0.92f, 0.68f),
            new Vector2(0.08f, 0.12f),
            new Vector2(0.92f, 0.12f)
        };

        private static readonly string[] BoardNoteMessages =
        {
            "CALL BACK\nTHE WITNESS",
            "CHECK THE\nNORTH GATE",
            "WHO HAD\nTHE KEYS?",
            "COFFEE\nIS EVIDENCE",
            "ASK ABOUT\nTHE FOOTPRINTS",
            "DON'T FORGET\nTHE NIGHT LOG",
            "FOLLOW UP\nAFTER SHIFT",
            "THIS CASE\nFEELS WEIRD",
            "HUNT'S SECURITY CHECK:\nWHO HAD ACCESS?",
            "BLANTON'S C QUESTION:\nWHO OWNS THIS POINTER?",
            "CSE 220 RULE:\nDON'T TRUST A NULL",
            "HERTZ REVIEW:\nWHERE'S THE TEST PLAN?",
            "UB BULLS CASE FILE:\nSTILL OPEN",
            "TEST THE BUILD\nBEFORE THE DEADLINE"
        };

        private static readonly Color[] BoardNoteColors =
        {
            new Color(0.91f, 0.78f, 0.48f, 0.98f),
            new Color(0.74f, 0.84f, 0.72f, 0.98f),
            new Color(0.86f, 0.69f, 0.62f, 0.98f),
            new Color(0.79f, 0.79f, 0.88f, 0.98f)
        };

        [Header("Corkboard Presentation")]
        [SerializeField] private Camera menuCamera;
        [SerializeField] private Transform corkBoardSurface;
        [SerializeField] private Light cursorSpotlight;
        [SerializeField] private Sprite titleLogoSprite;

        private Image fadeImage;
        private AudioSource musicSource;
        private AudioSource oneShotSource;
        private bool starting;
        private bool built;
        private float boardClutterTimer;
        private Rect boardViewportRect;
        private Transform menuCanvasRoot;
        private static Sprite pinSprite;
        private static Font cachedReadableMenuFont;
        private readonly List<MenuCardVisual> menuCardVisuals = new List<MenuCardVisual>();
        private readonly List<TransientBoardNote> transientBoardNotes = new List<TransientBoardNote>();

        private sealed class MenuCardVisual
        {
            public RectTransform Rect;
            public Image Background;
            public Image Pin;
            public Text Eyebrow;
            public Text Title;
            public Text Description;
            public bool IsLit;
        }

        private sealed class TransientBoardNote
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Shadow;
            public Image Paper;
            public Image Pin;
            public Text Message;
            public float Age;
            public float LifeSeconds;
            public float UnpinAge;
            public float Rotation;
            public Color PaperColor;
            public Color PinColor;
            public int SlotIndex;
            public string NoteText;
            public bool IsLit;
            public bool IsUnpinning;
        }

        private static readonly Color UnlitCardColor = new Color(0.10f, 0.075f, 0.05f, 0.94f);
        private static readonly Color LitCardColor = new Color(0.94f, 0.89f, 0.76f, 0.99f);
        private static readonly Color UnlitEyebrowColor = new Color(0.72f, 0.60f, 0.40f, 1f);
        private static readonly Color LitEyebrowColor = DeskUiPalette.Navy;
        private static readonly Color UnlitTitleColor = new Color(0.82f, 0.73f, 0.57f, 1f);
        private static readonly Color LitTitleColor = DeskUiPalette.Ink;
        private static readonly Color UnlitDescriptionColor = new Color(0.68f, 0.59f, 0.45f, 1f);
        private static readonly Color LitDescriptionColor = DeskUiPalette.InkMuted;
        private static readonly Color UnlitPinColor = new Color(0.42f, 0.14f, 0.10f, 1f);
        private static readonly Color LitPinColor = new Color(0.75f, 0.18f, 0.14f, 1f);
        private static readonly Color UnlitPostItColor = new Color(0.12f, 0.09f, 0.06f, 0.96f);
        private static readonly Color UnlitPostItTextColor = new Color(0.78f, 0.68f, 0.52f, 1f);
        private static readonly Color UnlitPostItPinMultiplier = new Color(0.40f, 0.40f, 0.40f, 1f);

        private void Awake()
        {
            ResolvePresentationReferences();
            EnsureAudio();
            EnsureEventSystem();
            if (!built)
                BuildUi();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PlayMenuMusic();
            if (fadeImage != null)
                StartCoroutine(FadeRoutine(1f, 0f, fadeInSeconds));
        }

        private void Update()
        {
            if (starting)
                return;

            UpdateBoardClutter();
            ResolvePresentationReferences();
            if (Mouse.current == null)
                return;

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            UpdateCardLighting(pointerPosition);

            if (menuCamera == null || corkBoardSurface == null || cursorSpotlight == null)
                return;

            Ray cursorRay = menuCamera.ScreenPointToRay(pointerPosition);
            Plane boardPlane = new Plane(corkBoardSurface.forward, corkBoardSurface.position);
            float distance;
            if (!boardPlane.Raycast(cursorRay, out distance) || distance < 0f)
                return;

            Vector3 target = cursorRay.GetPoint(distance);
            Vector3 direction = target - cursorSpotlight.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                cursorSpotlight.transform.rotation = Quaternion.LookRotation(direction.normalized, corkBoardSurface.up);
        }

        private void UpdateCardLighting(Vector2 pointerPosition)
        {
            for (int i = 0; i < menuCardVisuals.Count; i++)
            {
                MenuCardVisual visual = menuCardVisuals[i];
                if (visual == null || visual.Rect == null)
                    continue;

                bool isLit = !starting && RectTransformUtility.RectangleContainsScreenPoint(visual.Rect, pointerPosition, null);
                if (isLit == visual.IsLit)
                    continue;

                visual.IsLit = isLit;
                if (visual.Background != null)
                    visual.Background.color = isLit ? LitCardColor : UnlitCardColor;
                if (visual.Eyebrow != null)
                    visual.Eyebrow.color = isLit ? LitEyebrowColor : UnlitEyebrowColor;
                if (visual.Title != null)
                    visual.Title.color = isLit ? LitTitleColor : UnlitTitleColor;
                if (visual.Description != null)
                    visual.Description.color = isLit ? LitDescriptionColor : UnlitDescriptionColor;
                if (visual.Pin != null)
                    visual.Pin.color = isLit ? LitPinColor : UnlitPinColor;
                if (isLit)
                    PlayHoverSound();
            }
        }
        private void UpdateBoardClutter()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = transientBoardNotes.Count - 1; i >= 0; i--)
            {
                TransientBoardNote note = transientBoardNotes[i];
                if (note == null || note.Root == null)
                {
                    transientBoardNotes.RemoveAt(i);
                    continue;
                }

                if (!note.IsUnpinning)
                {
                    note.Age += deltaTime;
                    if (note.Age >= note.LifeSeconds)
                    {
                        note.IsUnpinning = true;
                        note.UnpinAge = 0f;
                        PlayPostItSound();
                    }
                }

                float progress;
                float visibility;
                if (note.IsUnpinning)
                {
                    note.UnpinAge += deltaTime;
                    progress = Mathf.Clamp01(note.UnpinAge / BoardNoteAnimationSeconds);
                    visibility = 1f - progress;
                    note.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, progress);
                    note.Rect.localRotation = Quaternion.Euler(0f, 0f, note.Rotation + 11f * progress);
                    if (progress >= 1f)
                    {
                        Destroy(note.Root);
                        transientBoardNotes.RemoveAt(i);
                        continue;
                    }
                }
                else
                {
                    progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(note.Age / BoardNoteAnimationSeconds));
                    visibility = progress;
                    note.Rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, progress);
                    note.Rect.localRotation = Quaternion.Euler(0f, 0f, note.Rotation - 12f * (1f - progress));
                }

                Vector2 pointerPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
                note.IsLit = !starting && RectTransformUtility.RectangleContainsScreenPoint(note.Rect, pointerPosition, null);
                SetGraphicAlpha(note.Shadow, 0.30f * visibility);

                Color paperColor = note.IsLit ? note.PaperColor : UnlitPostItColor;
                paperColor.a *= visibility;
                note.Paper.color = paperColor;

                Color pinColor = note.IsLit
                    ? note.PinColor
                    : new Color(
                        note.PinColor.r * UnlitPostItPinMultiplier.r,
                        note.PinColor.g * UnlitPostItPinMultiplier.g,
                        note.PinColor.b * UnlitPostItPinMultiplier.b,
                        note.PinColor.a);
                pinColor.a *= visibility;
                note.Pin.color = pinColor;

                Color textColor = note.IsLit ? DeskUiPalette.Ink : UnlitPostItTextColor;
                textColor.a *= visibility;
                note.Message.color = textColor;
            }

            boardClutterTimer -= deltaTime;
            if (boardClutterTimer > 0f)
                return;

            bool shouldPinNote = transientBoardNotes.Count == 0 ||
                                 (transientBoardNotes.Count < MaximumBoardNotes && Random.value < 0.64f);
            if (shouldPinNote)
            {
                List<int> availableSlots = GetAvailableBoardNoteSlots();
                List<string> availableMessages = GetAvailableBoardNoteMessages();
                if (availableSlots.Count > 0 && availableMessages.Count > 0)
                {
                    int slotIndex = availableSlots[Random.Range(0, availableSlots.Count)];
                    string message = availableMessages[Random.Range(0, availableMessages.Count)];
                    SpawnBoardNote(slotIndex, message);
                }
                else
                {
                    UnpinRandomBoardNote();
                }
            }
            else
            {
                UnpinRandomBoardNote();
            }

            boardClutterTimer = Random.Range(BoardClutterIntervalMin, BoardClutterIntervalMax);
        }

        private List<int> GetAvailableBoardNoteSlots()
        {
            var availableSlots = new List<int>();
            for (int slotIndex = 0; slotIndex < BoardNoteSlots.Length; slotIndex++)
            {
                bool occupied = false;
                for (int noteIndex = 0; noteIndex < transientBoardNotes.Count; noteIndex++)
                {
                    TransientBoardNote note = transientBoardNotes[noteIndex];
                    if (note != null && note.SlotIndex == slotIndex)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                    availableSlots.Add(slotIndex);
            }
            return availableSlots;
        }

        private List<string> GetAvailableBoardNoteMessages()
        {
            var availableMessages = new List<string>();
            for (int messageIndex = 0; messageIndex < BoardNoteMessages.Length; messageIndex++)
            {
                string candidate = BoardNoteMessages[messageIndex];
                bool alreadyPinned = false;
                for (int noteIndex = 0; noteIndex < transientBoardNotes.Count; noteIndex++)
                {
                    TransientBoardNote note = transientBoardNotes[noteIndex];
                    if (note != null && string.Equals(note.NoteText, candidate, System.StringComparison.Ordinal))
                    {
                        alreadyPinned = true;
                        break;
                    }
                }

                if (!alreadyPinned)
                    availableMessages.Add(candidate);
            }
            return availableMessages;
        }

        private void PinRandomAvailableBoardNote()
        {
            List<int> availableSlots = GetAvailableBoardNoteSlots();
            List<string> availableMessages = GetAvailableBoardNoteMessages();
            if (availableSlots.Count == 0 || availableMessages.Count == 0)
                return;

            int slotIndex = availableSlots[Random.Range(0, availableSlots.Count)];
            string noteText = availableMessages[Random.Range(0, availableMessages.Count)];
            SpawnBoardNote(slotIndex, noteText);
        }


        private void SpawnBoardNote(int slotIndex, string noteText)
        {
            if (menuCanvasRoot == null || boardViewportRect.width <= 0f || boardViewportRect.height <= 0f ||
                slotIndex < 0 || slotIndex >= BoardNoteSlots.Length || string.IsNullOrWhiteSpace(noteText))
                return;

            Vector2 slot = BoardNoteSlots[slotIndex];
            Vector2 anchor = new Vector2(
                Mathf.Lerp(boardViewportRect.xMin, boardViewportRect.xMax, slot.x),
                Mathf.Lerp(boardViewportRect.yMin, boardViewportRect.yMax, slot.y));
            Vector2 noteSize = new Vector2(180f, 108f);
            float rotation = Random.Range(-5.5f, 5.5f);
            string noteName = "TransientBoardNote_Slot" + slotIndex + "_" + Time.frameCount;

            GameObject root = new GameObject(noteName, typeof(RectTransform));
            root.transform.SetParent(menuCanvasRoot, false);
            root.transform.SetAsFirstSibling();
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation - 12f);
            rect.localScale = Vector3.one * 0.72f;

            Image shadow = CreateSized(root.transform, "DropShadow", new Color(0f, 0f, 0f, 0.38f),
                new Vector2(0.5f, 0.5f), noteSize + new Vector2(5f, 4f));
            DeskUiPalette.ApplyFill(shadow, new Color(0f, 0f, 0f, 0.38f));
            shadow.rectTransform.anchoredPosition = new Vector2(5f, -5f);
            shadow.raycastTarget = false;

            Color paperColor = BoardNoteColors[Random.Range(0, BoardNoteColors.Length)];
            Image paper = CreateSized(root.transform, "Paper", paperColor, new Vector2(0.5f, 0.5f), noteSize);
            DeskUiPalette.ApplyFill(paper, paperColor);
            paper.raycastTarget = false;

            Text message = CreateText(root.transform, "Note", noteText, 14, FontStyle.Bold,
                new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), Vector2.zero,
                new Vector2(noteSize.x * 0.84f, noteSize.y * 0.64f));
            DeskUiPalette.StyleLabel(message, 14, DeskUiPalette.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            UseReadableMenuFont(message);

            Color pinColor = Random.value < 0.5f
                ? new Color(0.12f, 0.28f, 0.70f, 1f)
                : new Color(0.72f, 0.16f, 0.12f, 1f);
            Image pin = CreateSized(root.transform, "Pushpin", pinColor,
                new Vector2(0.5f, 1f), new Vector2(15f, 15f));
            pin.sprite = GetPinSprite();
            pin.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            pin.raycastTarget = false;

            var note = new TransientBoardNote
            {
                Root = root,
                Rect = rect,
                Shadow = shadow,
                Paper = paper,
                Pin = pin,
                Message = message,
                LifeSeconds = Random.Range(BoardNoteLifeMin, BoardNoteLifeMax),
                Rotation = rotation,
                PaperColor = paperColor,
                PinColor = pinColor,
                SlotIndex = slotIndex,
                NoteText = noteText
            };
            transientBoardNotes.Add(note);
            SetGraphicAlpha(shadow, 0f);
            SetGraphicAlpha(paper, 0f);
            SetGraphicAlpha(pin, 0f);
            SetGraphicAlpha(message, 0f);
            PlayPostItSound();
        }

        private static void UseReadableMenuFont(Text text)
        {
            if (text == null)
                return;

            if (cachedReadableMenuFont == null)
                cachedReadableMenuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (cachedReadableMenuFont != null)
                text.font = cachedReadableMenuFont;
        }


        private void UnpinRandomBoardNote()
        {
            List<TransientBoardNote> availableNotes = new List<TransientBoardNote>();
            for (int i = 0; i < transientBoardNotes.Count; i++)
            {
                TransientBoardNote note = transientBoardNotes[i];
                if (note != null && !note.IsUnpinning)
                    availableNotes.Add(note);
            }

            if (availableNotes.Count == 0)
                return;

            TransientBoardNote selectedNote = availableNotes[Random.Range(0, availableNotes.Count)];
            selectedNote.IsUnpinning = true;
            selectedNote.UnpinAge = 0f;
            PlayPostItSound();
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }



        private void OnDestroy()
        {
            StopMenuMusic();
        }

        /// <summary>Bezi / UI button — begin a Standard shift, fade into gameplay.</summary>
        public void OnStartClicked()
        {
            BeginSelectedMode(false);
        }

        /// <summary>Bezi / UI button — begin the deterministic replayable Demo case.</summary>
        public void OnDemoClicked()
        {
            BeginSelectedMode(true);
        }

        private void BeginSelectedMode(bool demoMode)
        {
            if (starting) return;
            StartDemoMode = demoMode;
            starting = true;
            PlayUiClick();
            StartCoroutine(StartGameRoutine());
        }

        /// <summary>Bezi / UI button — quit player build (no-op in Editor beyond log).</summary>
        public void OnQuitClicked()
        {
            PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator StartGameRoutine()
        {
            StopMenuMusicFade(fadeOutSeconds * 0.9f);
            yield return FadeRoutine(fadeImage != null ? fadeImage.color.a : 0f, 1f, fadeOutSeconds);

            string scene = string.IsNullOrWhiteSpace(gameplaySceneName)
                ? DefaultGameplayScene
                : gameplaySceneName.Trim();

            AsyncOperation load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("MainMenuController: failed to load scene '" + scene + "'. Check Build Settings.", this);
                starting = false;
                yield break;
            }

            while (!load.isDone)
                yield return null;
        }

        private IEnumerator FadeRoutine(float from, float to, float seconds)
        {
            if (fadeImage == null) yield break;
            fadeImage.raycastTarget = true;
            float duration = Mathf.Max(0.01f, seconds);
            float t = 0f;
            Color c = fadeImage.color;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                c.a = a;
                fadeImage.color = c;
                yield return null;
            }

            c.a = to;
            fadeImage.color = c;
            fadeImage.raycastTarget = to > 0.01f;
        }

        private void ResolvePresentationReferences()
        {
            if (menuCamera == null)
                menuCamera = Camera.main;

            if (corkBoardSurface == null)
            {
                GameObject boardObject = GameObject.Find("CorkBoard_Desk/Cork Board");
                if (boardObject != null)
                    corkBoardSurface = boardObject.transform;
            }

            if (cursorSpotlight == null)
            {
                GameObject spotlightObject = GameObject.Find("Spot Light");
                if (spotlightObject != null)
                    cursorSpotlight = spotlightObject.GetComponent<Light>();
            }
        }

        private void BuildUi()
        {
            built = true;

            GameObject root = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            menuCanvasRoot = root.transform;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Rect boardBounds = GetBoardViewportRect();
            boardViewportRect = boardBounds;
            boardClutterTimer = Random.Range(BoardClutterIntervalMin * 0.35f, BoardClutterIntervalMax * 0.45f);
            float boardWidth = boardBounds.width * 1920f;
            float boardHeight = boardBounds.height * 1080f;
            Vector2 boardCenter = boardBounds.center;

            float logoWidth = Mathf.Clamp(boardWidth * 0.46f, 250f, 520f);
            RectTransform logoRect = CreateAnchoredRect(root.transform, "TitleLogo",
                new Vector2(boardCenter.x, boardCenter.y + boardBounds.height * 0.27f),
                new Vector2(logoWidth, logoWidth * 0.5f));
            Image logo = logoRect.gameObject.AddComponent<Image>();
            logo.sprite = titleLogoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            if (titleLogoSprite == null)
            {
                Text fallbackTitle = logoRect.gameObject.AddComponent<Text>();
                fallbackTitle.text = "CASE CLOSED";
                DeskUiPalette.StyleLabel(fallbackTitle, 46, DeskUiPalette.ManilaLight, FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            Text modePrompt = CreateText(root.transform, "ModePrompt", "SELECT YOUR SHIFT", 16, FontStyle.Bold,
                new Vector2(boardCenter.x, boardCenter.y + boardBounds.height * 0.02f),
                new Vector2(boardCenter.x, boardCenter.y + boardBounds.height * 0.02f),
                Vector2.zero, new Vector2(Mathf.Min(boardWidth * 0.6f, 600f), 32f));
            DeskUiPalette.StyleLabel(modePrompt, 16, new Color(0.95f, 0.88f, 0.70f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter);

            float cardWidth = Mathf.Min(boardWidth * 0.42f, 390f);
            float cardHeight = Mathf.Min(boardHeight * 0.48f, 245f);
            cardWidth = Mathf.Max(180f, cardWidth);
            cardHeight = Mathf.Max(130f, cardHeight);
            float cardOffset = boardBounds.width * 0.215f;
            Vector2 cardsCenter = new Vector2(boardCenter.x, boardCenter.y - boardBounds.height * 0.12f);

            CreatePinnedCard(root.transform, "StandardCaseCard",
                new Vector2(cardsCenter.x - cardOffset, cardsCenter.y), new Vector2(cardWidth, cardHeight), -1.3f,
                "CASE FILE  ·  STANDARD", "START SHIFT", "Begin a new case from the board.", OnStartClicked);
            CreatePinnedCard(root.transform, "DemoCaseCard",
                new Vector2(cardsCenter.x + cardOffset, cardsCenter.y), new Vector2(cardWidth, cardHeight), 1.1f,
                "PRESENTATION MODE", "PLAY DEMO", "A short, fixed two-suspect case.", OnDemoClicked);

            GameObject quitGo = CreateButton(root.transform, "QuitButton", "QUIT",
                new Color(0.13f, 0.20f, 0.28f, 0.94f), DeskUiPalette.ManilaLight,
                new Vector2(boardCenter.x, boardCenter.y - boardBounds.height * 0.46f), new Vector2(160f, 42f));
            Image quitImage = quitGo.GetComponent<Image>();
            Text quitLabel = quitGo.GetComponentInChildren<Text>();
            quitImage.color = UnlitCardColor;
            if (quitLabel != null)
                quitLabel.color = UnlitTitleColor;
            menuCardVisuals.Add(new MenuCardVisual
            {
                Rect = quitGo.GetComponent<RectTransform>(),
                Background = quitImage,
                Title = quitLabel,
                IsLit = false
            });
            quitGo.GetComponent<Button>().onClick.AddListener(OnQuitClicked);
            quitGo.transform.localRotation = Quaternion.Euler(0f, 0f, -0.5f);

            // Start with one board slip so the animated pinning is visible immediately.
            PinRandomAvailableBoardNote();

            fadeImage = CreateFill(root.transform, "Fade", Color.black, Vector2.zero, Vector2.one);
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
            fadeImage.raycastTarget = true;
            fadeImage.transform.SetAsLastSibling();
        }

        private Rect GetBoardViewportRect()
        {
            Rect fallback = new Rect(0.25f, 0.22f, 0.5f, 0.56f);
            if (menuCamera == null || corkBoardSurface == null)
                return fallback;

            Renderer[] renderers = corkBoardSurface.GetComponentsInChildren<Renderer>();
            bool foundRenderer = false;
            Bounds bounds = new Bounds(corkBoardSurface.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!foundRenderer)
                {
                    bounds = renderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!foundRenderer)
                return fallback;

            Vector3 extents = bounds.extents;
            Vector3 center = bounds.center;
            float minX = 1f;
            float maxX = 0f;
            float minY = 1f;
            float maxY = 0f;
            int visibleCorners = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 viewport = menuCamera.WorldToViewportPoint(corner);
                        if (viewport.z <= menuCamera.nearClipPlane)
                            continue;
                        minX = Mathf.Min(minX, viewport.x);
                        maxX = Mathf.Max(maxX, viewport.x);
                        minY = Mathf.Min(minY, viewport.y);
                        maxY = Mathf.Max(maxY, viewport.y);
                        visibleCorners++;
                    }
                }
            }

            if (visibleCorners == 0)
                return fallback;

            minX = Mathf.Clamp(minX, 0.03f, 0.97f);
            maxX = Mathf.Clamp(maxX, 0.03f, 0.97f);
            minY = Mathf.Clamp(minY, 0.03f, 0.97f);
            maxY = Mathf.Clamp(maxY, 0.03f, 0.97f);
            if (maxX - minX < 0.05f || maxY - minY < 0.05f)
                return fallback;

            float insetX = (maxX - minX) * 0.04f;
            float insetY = (maxY - minY) * 0.05f;
            return new Rect(minX + insetX, minY + insetY,
                (maxX - minX) - insetX * 2f, (maxY - minY) - insetY * 2f);
        }

        private void CreatePinnedCard(Transform parent, string name, Vector2 anchor, Vector2 size, float angle,
            string eyebrow, string title, string description, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform shadowRect = CreateAnchoredRect(parent, name + "Shadow",
                anchor + new Vector2(4f / 1920f, -7f / 1080f), size);
            shadowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image shadow = shadowRect.gameObject.AddComponent<Image>();
            DeskUiPalette.ApplyFill(shadow, new Color(0f, 0f, 0f, 0.48f));
            shadow.raycastTarget = false;

            RectTransform cardRect = CreateAnchoredRect(parent, name, anchor, size);
            cardRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image cardImage = cardRect.gameObject.AddComponent<Image>();
            Button cardButton = cardRect.gameObject.AddComponent<Button>();
            Color paper = UnlitCardColor;
            DeskUiPalette.StyleButton(cardButton, cardImage, null, paper, DeskUiPalette.Ink);
            cardImage.raycastTarget = true;
            cardButton.onClick.AddListener(onClick);

            Text eyebrowText = CreateText(cardRect, name + "Eyebrow", eyebrow, 15, FontStyle.Bold,
                new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(size.x * 0.84f, 30f));
            DeskUiPalette.StyleLabel(eyebrowText, 15, UnlitEyebrowColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            UseReadableMenuFont(eyebrowText);

            Text titleText = CreateText(cardRect, name + "Title", title, 30, FontStyle.Bold,
                new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(size.x * 0.88f, 56f));
            DeskUiPalette.StyleLabel(titleText, 30, UnlitTitleColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            UseReadableMenuFont(titleText);

            CreateSized(cardRect, name + "Rule", new Color(0.35f, 0.28f, 0.16f, 0.48f),
                new Vector2(0.5f, 0.31f), new Vector2(size.x * 0.62f, 2f));

            Text descriptionText = CreateText(cardRect, name + "Description", description, 17, FontStyle.Normal,
                new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(size.x * 0.84f, 50f));
            DeskUiPalette.StyleLabel(descriptionText, 17, UnlitDescriptionColor, FontStyle.Normal, TextAnchor.MiddleCenter);
            UseReadableMenuFont(descriptionText);

            Image pin = CreateSized(cardRect, name + "Pin", UnlitPinColor,
                new Vector2(0.5f, 1f), new Vector2(20f, 20f));
            pin.sprite = GetPinSprite();
            pin.raycastTarget = false;
            pin.rectTransform.anchoredPosition = new Vector2(0f, -11f);

            menuCardVisuals.Add(new MenuCardVisual
            {
                Rect = cardRect,
                Background = cardImage,
                Pin = pin,
                Eyebrow = eyebrowText,
                Title = titleText,
                Description = descriptionText,
                IsLit = false
            });
        }

        private static RectTransform CreateAnchoredRect(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private static Sprite GetPinSprite()
        {
            if (pinSprite != null)
                return pinSprite;

            const int textureSize = 48;
            const float radius = 22f;
            var pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - (textureSize - 1) * 0.5f;
                    float dy = y - (textureSize - 1) * 0.5f;
                    pixels[y * textureSize + x] = dx * dx + dy * dy <= radius * radius
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0f);
                }
            }

            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "MainMenuPinTexture";
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            pinSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            pinSprite.name = "MainMenuPinSprite";
            return pinSprite;
        }


        private void BuildLegacyUi()
        {
            built = true;

            GameObject root = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            menuCanvasRoot = root.transform;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Translucent veil — 3D night station must read behind (not a solid desk card).
            CreateFill(root.transform, "Backdrop",
                new Color(DeskUiPalette.NavyDark.r, DeskUiPalette.NavyDark.g, DeskUiPalette.NavyDark.b, 0.28f),
                Vector2.zero, Vector2.one);

            CreateFill(root.transform, "TopWash",
                new Color(0.08f, 0.14f, 0.18f, 0.22f),
                new Vector2(0f, 0.62f), Vector2.one);

            CreateFill(root.transform, "BottomShade",
                new Color(0.04f, 0.05f, 0.07f, 0.45f),
                new Vector2(0f, 0f), new Vector2(1f, 0.28f));

            // Brand block — CASE CLOSED dominates; translucent so facade stays visible.
            GameObject brand = CreatePanel(root.transform, "BrandBlock",
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
                Vector2.zero, new Vector2(980f, 260f));
            brand.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.32f);

            Text titleShadow = CreateText(brand.transform, "TitleShadow", "CASE CLOSED", 96, FontStyle.Bold,
                new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), new Vector2(3f, -3f), new Vector2(940f, 120f));
            DeskUiPalette.StyleLabel(titleShadow, 96, new Color(0f, 0f, 0f, 0.55f), FontStyle.Bold, TextAnchor.MiddleCenter);

            Text title = CreateText(brand.transform, "Title", "CASE CLOSED", 96, FontStyle.Bold,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(940f, 120f));
            DeskUiPalette.StyleLabel(title, 96, DeskUiPalette.ManilaLight, FontStyle.Bold, TextAnchor.MiddleCenter);

            Text atUb = CreateText(brand.transform, "AtUb", "AT  UB", 36, FontStyle.Bold,
                new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(900f, 44f));
            DeskUiPalette.StyleLabel(atUb, 36, new Color(0.72f, 0.86f, 0.82f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter);

            Text tag = CreateText(brand.transform, "Tag", "University Police · Night Shift", 18, FontStyle.Normal,
                new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(900f, 32f));
            DeskUiPalette.StyleLabel(tag, 18, new Color(DeskUiPalette.Manila.r, DeskUiPalette.Manila.g, DeskUiPalette.Manila.b, 0.9f),
                FontStyle.Normal, TextAnchor.MiddleCenter);

            CreateSized(brand.transform, "Rule",
                new Color(DeskUiPalette.Manila.r, DeskUiPalette.Manila.g, DeskUiPalette.Manila.b, 0.45f),
                new Vector2(0.5f, 0.26f), new Vector2(420f, 2f));

            Color startFill = new Color(DeskUiPalette.ManilaLight.r, DeskUiPalette.ManilaLight.g, DeskUiPalette.ManilaLight.b, 0.88f);
            Color quitFill = new Color(DeskUiPalette.Navy.r, DeskUiPalette.Navy.g, DeskUiPalette.Navy.b, 0.72f);

            GameObject standardGo = CreateButton(root.transform, "StandardButton", "STANDARD",
                startFill, DeskUiPalette.Ink,
                new Vector2(0.5f, 0.30f), new Vector2(270f, 56f));
            standardGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145f, 0f);
            standardGo.GetComponent<Button>().onClick.AddListener(OnStartClicked);

            GameObject demoGo = CreateButton(root.transform, "DemoButton", "PLAY DEMO",
                quitFill, DeskUiPalette.ManilaLight,
                new Vector2(0.5f, 0.30f), new Vector2(270f, 56f));
            demoGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, 0f);
            demoGo.GetComponent<Button>().onClick.AddListener(OnDemoClicked);

            GameObject quitGo = CreateButton(root.transform, "QuitButton", "QUIT",
                quitFill, DeskUiPalette.ManilaLight,
                new Vector2(0.5f, 0.22f), new Vector2(280f, 48f));
            quitGo.GetComponent<Button>().onClick.AddListener(OnQuitClicked);

            Text foot = CreateText(root.transform, "Footer",
                "Outside University Police · Buffalo campus", 15, FontStyle.Italic,
                new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.07f), Vector2.zero, new Vector2(900f, 28f));
            DeskUiPalette.StyleLabel(foot, 15, new Color(0.82f, 0.86f, 0.90f, 0.75f), FontStyle.Italic, TextAnchor.MiddleCenter);

            // Fade veil (starts opaque, fades in).
            fadeImage = CreateFill(root.transform, "Fade", Color.black, Vector2.zero, Vector2.one);
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
            fadeImage.raycastTarget = true;
            fadeImage.transform.SetAsLastSibling();
        }

        private void EnsureAudio()
        {
            if (menuMusicClip == null)
            {
                // Prefer Magdy funky drop (Resources/Music/menu_funky or Inspector assign from Assets/Audio/Music/).
                menuMusicClip = Resources.Load<AudioClip>(MenuFunkyResourcesPath);
                if (menuMusicClip == null)
                    menuMusicClip = Resources.Load<AudioClip>(SfxResourcesFolder + "/office_ambience");
            }

            if (uiClickClip == null)
                uiClickClip = Resources.Load<AudioClip>(SfxResourcesFolder + "/ui_click");

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f;
                musicSource.volume = menuMusicVolume;
            }

            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                oneShotSource.playOnAwake = false;
                oneShotSource.loop = false;
                oneShotSource.spatialBlend = 0f;
            }
        }

        private void PlayMenuMusic()
        {
            EnsureAudio();
            if (musicSource == null || menuMusicClip == null) return;
            musicSource.clip = menuMusicClip;
            musicSource.volume = menuMusicVolume;
            musicSource.loop = true;
            if (!musicSource.isPlaying)
                musicSource.Play();
        }

        private void StopMenuMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
                musicSource.Stop();
        }

        private void StopMenuMusicFade(float seconds)
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            StartCoroutine(FadeMusicOut(seconds));
        }

        private IEnumerator FadeMusicOut(float seconds)
        {
            if (musicSource == null) yield break;
            float start = musicSource.volume;
            float duration = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < duration && musicSource != null)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
                yield return null;
            }

            StopMenuMusic();
        }

        private void PlayUiClick()
        {
            PlayMenuInteractionSound();
        }

        private void PlayHoverSound()
        {
            PlayMenuInteractionSound();
        }

        private void PlayPostItSound()
        {
            PlayMenuInteractionSound();
        }

        private void PlayMenuInteractionSound()
        {
            EnsureAudio();
            AudioClip interactionClip = uiClickClip != null ? uiClickClip : hoverClip;
            if (oneShotSource == null || interactionClip == null)
                return;

            oneShotSource.PlayOneShot(interactionClip, uiClickVolume);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go;
        }

        private static Image CreateFill(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image CreateSized(Transform parent, string name, Color color, Vector2 anchor, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            Text text = go.GetComponent<Text>();
            text.text = value;
            DeskUiPalette.StyleLabel(text, size, DeskUiPalette.White, style, TextAnchor.MiddleCenter);
            return text;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Color fill, Color textColor, Vector2 anchor, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            Button button = go.GetComponent<Button>();

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            DeskUiPalette.StyleButton(button, image, text, fill, textColor);
            text.fontSize = 22;
            return go;
        }
    }
}
