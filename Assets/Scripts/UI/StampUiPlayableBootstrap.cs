using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Prototype Play Mode wiring for evidence board + HOLD/RELEASE when Bezi desk props
    /// are not yet assigned. Skips the right-side overlay when <see cref="InterrogationActionHud"/>
    /// already owns Case File + stamp chrome (hud-split top tags).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class StampUiPlayableBootstrap : MonoBehaviour
    {
        [Header("Case Connection")]
        [Tooltip("Active shift controller. Auto-found when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Controllers")]
        [SerializeField] private EvidenceBoardController evidenceBoard;
        [SerializeField] private StampPadController stampPad;
        [SerializeField] private InterrogationActionHud actionHud;

        [Header("Prototype Overlay")]
        [Tooltip("When true, spawn a temporary Overlay canvas if board/stamp hooks are empty AND no InterrogationActionHud owns Case File.")]
        [SerializeField] private bool createOverlayWhenUnwired = true;

        [Tooltip("Anchor the prototype board on the right side of the screen.")]
        [SerializeField] private bool rightSideBoard = true;

        private GameObject overlayRoot;

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();

            if (evidenceBoard == null)
                evidenceBoard = GetComponent<EvidenceBoardController>();
            if (evidenceBoard == null)
                evidenceBoard = gameObject.AddComponent<EvidenceBoardController>();

            if (stampPad == null)
                stampPad = GetComponent<StampPadController>();
            if (stampPad == null)
                stampPad = gameObject.AddComponent<StampPadController>();

            if (actionHud == null)
                actionHud = GetComponent<InterrogationActionHud>();
            if (actionHud == null)
                actionHud = FindAnyObjectByType<InterrogationActionHud>();

            if (shiftController != null)
            {
                evidenceBoard.SetShiftController(shiftController);
                stampPad.SetShiftController(shiftController);
            }

            EnsureEventSystem();
        }

        private void Start()
        {
            if (actionHud == null)
                actionHud = GetComponent<InterrogationActionHud>() ?? FindAnyObjectByType<InterrogationActionHud>();

            // HUD Case File footer owns stamps — do not stack a second CASE FILE / EVIDENCE panel.
            if (actionHud != null && actionHud.OwnsCaseFileChrome)
                return;

            if (createOverlayWhenUnwired && NeedsPrototypeOverlay() && overlayRoot == null)
                BuildPrototypeOverlay();
        }

        private void OnDestroy()
        {
            if (overlayRoot != null)
                Destroy(overlayRoot);
        }

        private bool NeedsPrototypeOverlay()
        {
            bool boardReady = evidenceBoard != null && evidenceBoard.HasBoardHooks;
            bool stampReady = stampPad != null && stampPad.HasStampHooks;
            return !(boardReady && stampReady);
        }

        private void BuildPrototypeOverlay()
        {
            overlayRoot = new GameObject("StampUiPrototypeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayRoot.transform.SetParent(transform, false);

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject boardPanel = CreatePanel(overlayRoot.transform, "EvidenceBoardPanel",
                rightSideBoard ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                rightSideBoard ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                rightSideBoard ? new Vector2(-280f, 0f) : new Vector2(280f, 0f),
                new Vector2(520f, 640f));
            boardPanel.GetComponent<Image>().color = DeskUiPalette.NavyDark;

            GameObject titleGo = CreateText(boardPanel.transform, "BoardTitle", "CASE FILE", 18, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(480f, 36f));
            titleGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            titleGo.GetComponent<Text>().color = DeskUiPalette.ManilaLight;

            GameObject toastGo = CreateText(boardPanel.transform, "Toast", string.Empty, 14, FontStyle.Normal,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(480f, 28f));
            Text toast = toastGo.GetComponent<Text>();
            toast.alignment = TextAnchor.MiddleCenter;
            toast.color = DeskUiPalette.Manila;
            toastGo.SetActive(false);

            GameObject cardContainerGo = new GameObject("CardContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cardContainerGo.transform.SetParent(boardPanel.transform, false);
            RectTransform cardRect = cardContainerGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, -20f);
            cardRect.sizeDelta = new Vector2(480f, 520f);
            VerticalLayoutGroup layout = cardContainerGo.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = cardContainerGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            evidenceBoard.SetBoardSceneHooks(boardPanel, cardContainerGo.transform, null, toast);

            GameObject stampPanel = CreatePanel(overlayRoot.transform, "StampPadPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 120f));
            stampPanel.GetComponent<Image>().color = DeskUiPalette.NavyDark;

            GameObject stampLabel = CreateText(stampPanel.transform, "StampLabel", "STAMP", 16, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(380f, 28f));
            stampLabel.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            stampLabel.GetComponent<Text>().color = DeskUiPalette.ManilaLight;

            Button holdButton = CreateStampButton(stampPanel.transform, "HoldButton", "HOLD",
                new Vector2(-100f, -20f), DeskUiPalette.StampHold);
            Button releaseButton = CreateStampButton(stampPanel.transform, "ReleaseButton", "RELEASE",
                new Vector2(100f, -20f), DeskUiPalette.StampRelease);

            stampPad.SetStampSceneHooks(holdButton, releaseButton, null, stampPanel);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string value, int fontSize, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = DeskUiPalette.ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = DeskUiPalette.White;
            text.text = value;
            return go;
        }

        private static Button CreateStampButton(Transform parent, string name, string label, Vector2 anchoredPos, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(160f, 52f);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelGo = CreateText(go.transform, "Label", label, 18, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 44f));
            labelGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
