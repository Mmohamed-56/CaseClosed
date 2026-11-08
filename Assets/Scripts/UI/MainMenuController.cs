using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Lightweight title screen for Case Closed at UB — early-2000s campus police desk vibe.
    /// Builds runtime UI (no Bezi scene art required). Fades into SampleScene on Start.
    /// Soft-hooks Emilia Resources/SFX when present (ui_click + office_ambience as menu bed).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "SampleScene";
        public const string SfxResourcesFolder = "SFX";

        [Header("Flow")]
        [SerializeField] private string gameplaySceneName = DefaultGameplayScene;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.85f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.35f;

        [Header("Audio (Resources/SFX when empty)")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float uiClickVolume = 0.85f;

        private Image fadeImage;
        private AudioSource musicSource;
        private AudioSource oneShotSource;
        private bool starting;
        private bool built;

        private void Awake()
        {
            EnsureAudio();
            EnsureEventSystem();
            if (!built)
                BuildUi();
        }

        private void Start()
        {
            PlayMenuMusic();
            if (fadeImage != null)
                StartCoroutine(FadeRoutine(1f, 0f, fadeInSeconds));
        }

        private void OnDestroy()
        {
            StopMenuMusic();
        }

        /// <summary>Bezi / UI button — begin shift, fade into gameplay.</summary>
        public void OnStartClicked()
        {
            if (starting) return;
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

        private void BuildUi()
        {
            built = true;

            GameObject root = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-bleed blotter / night-desk plane (not purple).
            CreateFill(root.transform, "Backdrop", DeskUiPalette.NavyDark, Vector2.zero, Vector2.one);

            // Soft top wash — fluorescent office ceiling feel.
            CreateFill(root.transform, "TopWash",
                new Color(0.18f, 0.28f, 0.22f, 0.35f),
                new Vector2(0f, 0.55f), Vector2.one);

            // Bottom manila desk strip.
            CreateFill(root.transform, "DeskStrip", DeskUiPalette.Manila,
                new Vector2(0f, 0f), new Vector2(1f, 0.22f));

            // Left blotter edge accent.
            CreateFill(root.transform, "BlotterEdge", DeskUiPalette.Blotter,
                new Vector2(0f, 0.22f), new Vector2(0.015f, 1f));

            // Brand block — CASE CLOSED must dominate first viewport.
            GameObject brand = CreatePanel(root.transform, "BrandBlock",
                new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                Vector2.zero, new Vector2(920f, 280f));
            brand.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            Text title = CreateText(brand.transform, "Title", "CASE CLOSED", 92, FontStyle.Bold,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(900f, 110f));
            DeskUiPalette.StyleLabel(title, 92, DeskUiPalette.ManilaLight, FontStyle.Bold, TextAnchor.MiddleCenter);

            Text atUb = CreateText(brand.transform, "AtUb", "AT  UB", 34, FontStyle.Bold,
                new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(900f, 44f));
            DeskUiPalette.StyleLabel(atUb, 34, DeskUiPalette.Blotter, FontStyle.Bold, TextAnchor.MiddleCenter);

            Text tag = CreateText(brand.transform, "Tag", "University Police · Night Desk", 20, FontStyle.Normal,
                new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(900f, 36f));
            DeskUiPalette.StyleLabel(tag, 20, DeskUiPalette.Manila, FontStyle.Normal, TextAnchor.MiddleCenter);

            // Thin rule under brand.
            CreateSized(brand.transform, "Rule",
                new Color(DeskUiPalette.Manila.r, DeskUiPalette.Manila.g, DeskUiPalette.Manila.b, 0.55f),
                new Vector2(0.5f, 0.28f), new Vector2(420f, 2f));

            // Buttons.
            GameObject startGo = CreateButton(root.transform, "StartButton", "START SHIFT",
                DeskUiPalette.ManilaLight, DeskUiPalette.Ink,
                new Vector2(0.5f, 0.34f), new Vector2(280f, 56f));
            startGo.GetComponent<Button>().onClick.AddListener(OnStartClicked);

            GameObject quitGo = CreateButton(root.transform, "QuitButton", "QUIT",
                DeskUiPalette.Navy, DeskUiPalette.ManilaLight,
                new Vector2(0.5f, 0.26f), new Vector2(280f, 48f));
            quitGo.GetComponent<Button>().onClick.AddListener(OnQuitClicked);

            Text foot = CreateText(root.transform, "Footer",
                "Buffalo campus · one desk · one shift", 16, FontStyle.Italic,
                new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(800f, 28f));
            DeskUiPalette.StyleLabel(foot, 16, DeskUiPalette.InkMuted, FontStyle.Italic, TextAnchor.MiddleCenter);

            // Fade veil (starts opaque, fades in).
            fadeImage = CreateFill(root.transform, "Fade", Color.black, Vector2.zero, Vector2.one);
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
            fadeImage.raycastTarget = true;
            fadeImage.transform.SetAsLastSibling();
        }

        private void EnsureAudio()
        {
            if (menuMusicClip == null)
                menuMusicClip = Resources.Load<AudioClip>(SfxResourcesFolder + "/office_ambience");
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
            EnsureAudio();
            if (oneShotSource == null || uiClickClip == null) return;
            oneShotSource.PlayOneShot(uiClickClip, uiClickVolume);
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
