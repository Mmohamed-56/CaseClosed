using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Title overlay for Case Closed at UB — brand-first UI over the night exterior menu level.
    /// Builds translucent runtime UI so the 3D station facade reads behind. Fades into SampleScene.
    /// Audio: assign funky theme to menuMusicClip (drop Assets/Audio/Music/menu_funky.*);
    /// falls back to Resources/SFX/office_ambience when empty.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "SampleScene";
        public const string SfxResourcesFolder = "SFX";
        public const string MenuFunkyResourcesPath = "Music/menu_funky";

        [Header("Flow")]
        [SerializeField] private string gameplaySceneName = DefaultGameplayScene;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.85f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.35f;

        [Header("Audio")]
        [Tooltip("Funky menu theme. Drop Magdy track at Assets/Audio/Music/menu_funky.* and assign here (or Resources/Music/menu_funky). Falls back to office_ambience.")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.38f;
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

            GameObject startGo = CreateButton(root.transform, "StartButton", "START SHIFT",
                startFill, DeskUiPalette.Ink,
                new Vector2(0.5f, 0.30f), new Vector2(280f, 56f));
            startGo.GetComponent<Button>().onClick.AddListener(OnStartClicked);

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
