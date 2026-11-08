using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Prototype Play Mode wiring for the right-side evidence clipboard (CollectEvidence → cards)
    /// when Bezi desk props are not yet assigned. Skips the duplicate stamp pad when
    /// <see cref="InterrogationActionHud"/> already owns HOLD/RELEASE on DECISION.
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
        [Tooltip("Off by default — the scene owns the right CASE FILE / EVIDENCE clipboard. Do not respawn a prototype overlay.")]
        [SerializeField] private bool createOverlayWhenUnwired = false;

        [Tooltip("Anchor the prototype board on the right side of the screen.")]
        [SerializeField] private bool rightSideBoard = true;

        private GameObject overlayRoot;

        private void Awake()
        {
            // Bezi removes the right duplicate clipboard — never fight that by respawning, even if an
            // older scene still has createOverlayWhenUnwired serialized true.
            createOverlayWhenUnwired = false;

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

            DestroyLeftoverPrototypeOverlay();
            EnsureEventSystem();

            // Stay on the object for wiring, but do not run Start overlay spawn.
            enabled = false;
        }

        private void Start()
        {
            // Hidden by default — Awake disables this component so Bezi scene cleanup sticks.
            if (!createOverlayWhenUnwired || overlayRoot != null)
                return;

            if (actionHud == null)
                actionHud = GetComponent<InterrogationActionHud>() ?? FindAnyObjectByType<InterrogationActionHud>();

            bool boardReady = evidenceBoard != null && evidenceBoard.HasBoardHooks;
            // InterrogationActionHud DECISION footer owns HOLD/RELEASE — never spawn a second stamp pad.
            bool hudOwnsStamps = actionHud != null && actionHud.OwnsCaseFileChrome;
            bool stampReady = stampPad != null && stampPad.HasStampHooks;
            bool skipStampPad = hudOwnsStamps || stampReady;

            // Only reachable if createOverlayWhenUnwired is forced true at runtime for a one-off debug.
            if (!boardReady)
                BuildPrototypeOverlay(boardAlreadyReady: false, stampAlreadyReady: skipStampPad);
            else if (!skipStampPad)
                BuildPrototypeOverlay(boardAlreadyReady: true, stampAlreadyReady: false);
        }

        private void DestroyLeftoverPrototypeOverlay()
        {
            if (overlayRoot != null)
            {
                Destroy(overlayRoot);
                overlayRoot = null;
            }

            Transform existing = transform.Find("StampUiPrototypeOverlay");
            if (existing != null)
                Destroy(existing.gameObject);
        }

        private void OnDestroy()
        {
            if (overlayRoot != null)
                Destroy(overlayRoot);
        }

        private void BuildPrototypeOverlay(bool boardAlreadyReady, bool stampAlreadyReady)
        {
            overlayRoot = new GameObject("StampUiPrototypeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayRoot.transform.SetParent(transform, false);

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (!boardAlreadyReady)
                BuildEvidenceBoardPanel();

            if (!stampAlreadyReady)
                BuildStampPadPanel();
        }

        private void BuildEvidenceBoardPanel()
        {
            GameObject boardPanel = CreatePanel(overlayRoot.transform, "EvidenceBoardPanel",
                rightSideBoard ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                rightSideBoard ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                rightSideBoard ? new Vector2(-280f, 0f) : new Vector2(280f, 0f),
                new Vector2(520f, 640f));
            DeskUiPalette.ApplyFill(boardPanel.GetComponent<Image>(), DeskUiPalette.Manila);
            DeskUiPalette.AddFolderFrame(boardPanel.transform, 3f, 2f);

            GameObject titleGo = CreateText(boardPanel.transform, "BoardTitle", "EVIDENCE", 18, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(480f, 32f));
            Text boardTitle = titleGo.GetComponent<Text>();
            boardTitle.alignment = TextAnchor.MiddleCenter;
            DeskUiPalette.StyleLabel(boardTitle, 18, DeskUiPalette.Ink, DeskUiPalette.TypeRole.Stamp, FontStyle.Bold, TextAnchor.MiddleCenter);

            GameObject toastGo = CreateText(boardPanel.transform, "Toast", string.Empty, 14, FontStyle.Normal,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(480f, 24f));
            Text toast = toastGo.GetComponent<Text>();
            toast.alignment = TextAnchor.MiddleCenter;
            DeskUiPalette.StyleLabel(toast, 13, DeskUiPalette.InkMuted, DeskUiPalette.TypeRole.Body, FontStyle.Italic, TextAnchor.MiddleCenter);
            toastGo.SetActive(false);

            GameObject tabsGo = new GameObject("SuspectTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabsGo.transform.SetParent(boardPanel.transform, false);
            RectTransform tabsRect = tabsGo.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -78f);
            tabsRect.sizeDelta = new Vector2(-24f, 34f);
            HorizontalLayoutGroup tabLayout = tabsGo.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;

            GameObject emptyGo = CreateText(boardPanel.transform, "EmptyHint", string.Empty, 13, FontStyle.Italic,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(440f, 80f));
            Text emptyHint = emptyGo.GetComponent<Text>();
            emptyHint.alignment = TextAnchor.MiddleCenter;
            DeskUiPalette.StyleLabel(emptyHint, 13, DeskUiPalette.InkMuted, DeskUiPalette.TypeRole.Body, FontStyle.Italic, TextAnchor.MiddleCenter);
            emptyGo.SetActive(false);

            GameObject cardContainerGo = new GameObject("CardContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cardContainerGo.transform.SetParent(boardPanel.transform, false);
            RectTransform cardRect = cardContainerGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, -24f);
            cardRect.sizeDelta = new Vector2(480f, -140f);
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

            EvidenceCardView prefab = TryLoadBeziCardPrefab();
            evidenceBoard.SetBoardSceneHooks(
                boardPanel,
                cardContainerGo.transform,
                prefab,
                toast,
                tabsGo.transform,
                emptyHint);
        }

        private void BuildStampPadPanel()
        {
            GameObject stampPanel = CreatePanel(overlayRoot.transform, "StampPadPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 120f));
            DeskUiPalette.ApplyFill(stampPanel.GetComponent<Image>(), DeskUiPalette.Blotter);
            DeskUiPalette.AddFolderFrame(stampPanel.transform, 3f, 2f);

            GameObject stampLabel = CreateText(stampPanel.transform, "StampLabel", "RUBBER STAMP", 14, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(380f, 24f));
            Text stampTitle = stampLabel.GetComponent<Text>();
            stampTitle.alignment = TextAnchor.MiddleCenter;
            DeskUiPalette.StyleLabel(stampTitle, 14, DeskUiPalette.ManilaLight, DeskUiPalette.TypeRole.Stamp, FontStyle.Bold, TextAnchor.MiddleCenter);

            Button holdButton = CreateStampButton(stampPanel.transform, "HoldButton", "HOLD",
                new Vector2(-100f, -20f), DeskUiPalette.StampHold);
            Button releaseButton = CreateStampButton(stampPanel.transform, "ReleaseButton", "RELEASE",
                new Vector2(100f, -20f), DeskUiPalette.StampRelease);

            stampPad.SetStampSceneHooks(holdButton, releaseButton, null, stampPanel);
        }

        private static EvidenceCardView TryLoadBeziCardPrefab()
        {
            EvidenceCardView fromResources = Resources.Load<EvidenceCardView>("EvidenceCardView");
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EvidenceCardView.prefab");
            if (asset != null)
                return asset.GetComponent<EvidenceCardView>();
#endif
            return null;
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
            DeskUiPalette.StyleLabel(text, fontSize, DeskUiPalette.White, DeskUiPalette.TypeRole.Ui, style, TextAnchor.MiddleCenter);
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
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelGo = CreateText(go.transform, "Label", label, 18, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 44f));
            Text labelText = labelGo.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleCenter;
            DeskUiPalette.StyleStampButton(button, image, labelText, color, filled: false);
            DeskUiPalette.AddFolderFrame(go.transform, 2f, 2f);
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
