using System;
using System.Collections.Generic;
using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Dialogue;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Builds a standalone screen-space case clipboard at runtime. The panel starts closed and can be
    /// opened by a separate focus controller through <see cref="SetOpen"/> or <see cref="ToggleOpen"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClipboardCaseUiController : MonoBehaviour
    {
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;
        private const int MaxSortingOrder = 90;

        [Header("Case connections")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private InterrogationDialogueService dialogueService;
        [SerializeField] private ScenarioDialogueClient scenarioClient;

        private readonly Dictionary<string, string> pinnedEvidenceBySuspect = new Dictionary<string, string>();
        private readonly Dictionary<string, List<ChatTurn>> turnsBySuspect = new Dictionary<string, List<ChatTurn>>();

        private Canvas runtimeCanvas;
        private GameObject overlayRoot;
        private GameObject clipboardRoot;
        private GameObject subtitleRoot;
        private Text subtitleText;
        private Text headerTitleText;
        private Text headerCaseText;
        private Text footerStatusText;
        private ScrollRect profileScroll;
        private Transform profileContent;
        private Button beginInterviewButton;
        private Button holdButton;
        private Button releaseButton;
        private ScrollRect tabScroll;
        private Transform tabContentRoot;
        private ScrollRect caseCoverScroll;
        private Transform caseCoverContent;
        private GameObject caseCoverView;
        private GameObject suspectPageView;
        private ScrollRect evidenceScroll;
        private Transform evidenceContent;
        private ScrollRect chatScroll;
        private Transform chatContent;
        private ScrollRect questionScroll;
        private Transform questionContent;

        private CaseSession observedSession;
        private CaseShiftController subscribedShiftController;
        private InterrogationDialogueService subscribedDialogueService;
        private int selectedPageIndex;
        private bool isOpen;
        private bool awaitingReply;
        private string statusMessage = string.Empty;
        private CaseSession pendingRequestSession;
        private string pendingRequestSuspectId = string.Empty;
        private string pendingRequestQuestion = string.Empty;
        private string pendingRequestEvidenceId = string.Empty;
        private float connectionRetryTimer;

        private sealed class ChatTurn
        {
            public string Question;
            public string Reply;
        }

        /// <summary>Whether the clipboard panel is currently open.</summary>
        public bool IsOpen { get { return isOpen; } }

        private void Awake()
        {
            ResolveConnections();
            BuildRuntimeUi();
            EnsureEventSystem();
            CheckForNewSession();
            SetOpen(false);
        }

        private void OnEnable()
        {
            ResolveConnections();
            SubscribeToDependencies();
            CheckForNewSession();
        }

        private void OnDisable()
        {
            UnsubscribeFromDependencies();
        }

        private void Update()
        {
            if (shiftController == null || dialogueService == null || scenarioClient == null)
            {
                connectionRetryTimer -= Time.unscaledDeltaTime;
                if (connectionRetryTimer <= 0f)
                {
                    connectionRetryTimer = 1f;
                    ResolveConnections();
                    SubscribeToDependencies();
                }
            }

            if (CheckForNewSession())
                RefreshPage();
        }

        /// <summary>Opens or closes the clipboard without changing the case phase.</summary>
        /// <param name="open">True to show the clipboard; false to hide it.</param>
        public void SetOpen(bool open)
        {
            isOpen = open;
            if (overlayRoot != null)
                overlayRoot.SetActive(open);
            if (open)
            {
                ResolveConnections();
                SubscribeToDependencies();
                CheckForNewSession();
                RefreshPage();
            }
        }

        /// <summary>Toggles the clipboard's open state.</summary>
        public void ToggleOpen()
        {
            SetOpen(!isOpen);
        }

        private void ResolveConnections()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();

            if (dialogueService == null)
                dialogueService = GetComponent<InterrogationDialogueService>();
            if (dialogueService == null)
                dialogueService = FindAnyObjectByType<InterrogationDialogueService>();

            if (scenarioClient == null)
                scenarioClient = GetComponent<ScenarioDialogueClient>();
            if (scenarioClient == null && dialogueService != null)
                scenarioClient = dialogueService.GetComponent<ScenarioDialogueClient>();
            if (scenarioClient == null)
                scenarioClient = FindAnyObjectByType<ScenarioDialogueClient>();
        }

        private void SubscribeToDependencies()
        {
            if (subscribedShiftController != shiftController)
            {
                if (subscribedShiftController != null)
                {
                    subscribedShiftController.PhaseChanged -= HandlePhaseChanged;
                    subscribedShiftController.EvidenceCollected -= HandleEvidenceCollected;
                    subscribedShiftController.NextSuspectReady -= HandleNextSuspectReady;
                }

                subscribedShiftController = shiftController;
                if (subscribedShiftController != null)
                {
                    subscribedShiftController.PhaseChanged += HandlePhaseChanged;
                    subscribedShiftController.EvidenceCollected += HandleEvidenceCollected;
                    subscribedShiftController.NextSuspectReady += HandleNextSuspectReady;
                }
            }

            if (subscribedDialogueService != dialogueService)
            {
                if (subscribedDialogueService != null)
                {
                    subscribedDialogueService.SuspectLineReady -= HandleSuspectLineReady;
                    subscribedDialogueService.TurnCompleted -= HandleTurnCompleted;
                }

                subscribedDialogueService = dialogueService;
                if (subscribedDialogueService != null)
                {
                    subscribedDialogueService.SuspectLineReady += HandleSuspectLineReady;
                    subscribedDialogueService.TurnCompleted += HandleTurnCompleted;
                }
            }
        }

        private void UnsubscribeFromDependencies()
        {
            if (subscribedShiftController != null)
            {
                subscribedShiftController.PhaseChanged -= HandlePhaseChanged;
                subscribedShiftController.EvidenceCollected -= HandleEvidenceCollected;
                subscribedShiftController.NextSuspectReady -= HandleNextSuspectReady;
                subscribedShiftController = null;
            }

            if (subscribedDialogueService != null)
            {
                subscribedDialogueService.SuspectLineReady -= HandleSuspectLineReady;
                subscribedDialogueService.TurnCompleted -= HandleTurnCompleted;
                subscribedDialogueService = null;
            }
        }

        private bool CheckForNewSession()
        {
            CaseSession currentSession = shiftController != null ? shiftController.ActiveSession : null;
            if (observedSession == currentSession)
                return false;

            observedSession = currentSession;
            selectedPageIndex = 0;
            pinnedEvidenceBySuspect.Clear();
            turnsBySuspect.Clear();
            awaitingReply = false;
            ClearPendingRequest();
            statusMessage = string.Empty;
            if (subtitleRoot != null)
                subtitleRoot.SetActive(false);
            RebuildPageTabs();
            return true;
        }

        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            CheckForNewSession();
            RefreshPage();
        }

        private void HandleEvidenceCollected(EvidenceRecord record)
        {
            RefreshPage();
        }

        private void HandleNextSuspectReady(NextSuspectEvent nextSuspect)
        {
            RefreshPage();
        }

        private void HandleSuspectLineReady(SuspectVoiceLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Text) || !IsSuspectInCurrentCase(line.SuspectId))
                return;

            string name = string.IsNullOrWhiteSpace(line.SuspectName) ? "SUSPECT" : line.SuspectName.Trim().ToUpperInvariant();
            if (subtitleText != null)
            {
                subtitleText.text = name + "  —  “" + line.Text.Trim() + "”";
                subtitleRoot.SetActive(true);
            }
        }

        private void HandleTurnCompleted(SuspectDialogueTurn turn)
        {
            if (turn == null || observedSession == null || !IsSuspectInCurrentCase(turn.SuspectId))
                return;

            bool isPendingQuestion = pendingRequestSession == observedSession &&
                                     string.Equals(pendingRequestSuspectId, turn.SuspectId, StringComparison.Ordinal) &&
                                     string.Equals(pendingRequestQuestion, turn.PlayerQuestion, StringComparison.Ordinal) &&
                                     string.Equals(pendingRequestEvidenceId, turn.EvidenceId, StringComparison.Ordinal);
            bool isCurrentStamp = IsCurrentSessionStamp(turn);
            if (!isPendingQuestion && !isCurrentStamp)
                return;

            if (isPendingQuestion)
            {
                awaitingReply = false;
                ClearPendingRequest();
            }

            List<ChatTurn> turns;
            if (!turnsBySuspect.TryGetValue(turn.SuspectId, out turns))
            {
                turns = new List<ChatTurn>();
                turnsBySuspect.Add(turn.SuspectId, turns);
            }

            string question = string.IsNullOrWhiteSpace(turn.PlayerQuestion) ? "(No question recorded)" : turn.PlayerQuestion.Trim();
            string reply = turn.Reply != null && !string.IsNullOrWhiteSpace(turn.Reply.line)
                ? turn.Reply.line.Trim()
                : turn.StressResult != null && !string.IsNullOrWhiteSpace(turn.StressResult.FallbackLine)
                    ? turn.StressResult.FallbackLine.Trim()
                    : "No reply recorded.";
            turns.Add(new ChatTurn { Question = question, Reply = reply });

            statusMessage = turn.StressResult == null
                ? "Interview turn recorded."
                : "Reply received · stress " + turn.StressResult.Stress + "/100 · " + turn.StressResult.QuestionsLeft + " questions left.";
            RefreshPage();
        }

        private bool IsCurrentSessionStamp(SuspectDialogueTurn turn)
        {
            if (turn == null || observedSession == null || observedSession.IsResolved)
                return false;

            SuspectDecision expectedDecision;
            if (string.Equals(turn.PlayerQuestion, "HOLD", StringComparison.OrdinalIgnoreCase))
                expectedDecision = SuspectDecision.Hold;
            else if (string.Equals(turn.PlayerQuestion, "RELEASE", StringComparison.OrdinalIgnoreCase))
                expectedDecision = SuspectDecision.Release;
            else
                return false;

            try
            {
                return observedSession.GetDecision(turn.SuspectId) == expectedDecision;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool IsSuspectInCurrentCase(string suspectId)
        {
            if (observedSession == null || string.IsNullOrWhiteSpace(suspectId))
                return false;

            IReadOnlyList<SuspectRecord> suspects = observedSession.Case.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                if (suspects[i] != null && string.Equals(suspects[i].Id, suspectId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void ClearPendingRequest()
        {
            pendingRequestSession = null;
            pendingRequestSuspectId = string.Empty;
            pendingRequestQuestion = string.Empty;
            pendingRequestEvidenceId = string.Empty;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            new GameObject("ClipboardCaseUiEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void BuildRuntimeUi()
        {
            if (runtimeCanvas != null)
                return;

            GameObject canvasObject = new GameObject(
                "ClipboardCaseUiCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            runtimeCanvas = canvasObject.GetComponent<Canvas>();
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            runtimeCanvas.pixelPerfect = true;
            runtimeCanvas.sortingOrder = MaxSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            overlayRoot = CreateImageObject(canvasObject.transform, "ClipboardOverlay", new Color(0.025f, 0.035f, 0.04f, 0.58f));
            Stretch(overlayRoot.GetComponent<RectTransform>());

            BuildClipboard(overlayRoot.transform);
            BuildSubtitle(canvasObject.transform);
            RebuildPageTabs();
            RefreshPage();
            overlayRoot.SetActive(false);
        }

        private void BuildClipboard(Transform parent)
        {
            clipboardRoot = CreateImageObject(parent, "ManilaClipboard", DeskUiPalette.Manila);
            RectTransform clipboardRect = clipboardRoot.GetComponent<RectTransform>();
            clipboardRect.anchorMin = new Vector2(0.5f, 0.5f);
            clipboardRect.anchorMax = new Vector2(0.5f, 0.5f);
            clipboardRect.pivot = new Vector2(0.5f, 0.5f);
            clipboardRect.anchoredPosition = new Vector2(0f, 28f);
            clipboardRect.sizeDelta = new Vector2(1560f, 900f);
            DeskUiPalette.AddFolderFrame(clipboardRoot.transform, 3f, 2f);

            GameObject header = CreateImageObject(clipboardRoot.transform, "ClipboardHeader", DeskUiPalette.NavyDark);
            SetTopRect(header.GetComponent<RectTransform>(), 10f, 80f, 10f);

            Text agency = CreateLabel(header.transform, "AgencyLine", "UB CAMPUS POLICE  ·  CASE FILE", 12, DeskUiPalette.AccentOlive, FontStyle.Bold, TextAnchor.UpperLeft);
            SetTopLeftRect(agency.rectTransform, 14f, -8f, 430f, 18f);
            headerTitleText = CreateLabel(header.transform, "PageTitle", "CASE FILE", 23, DeskUiPalette.White, FontStyle.Bold, TextAnchor.UpperLeft);
            SetTopLeftRect(headerTitleText.rectTransform, 14f, -29f, 850f, 30f);
            headerCaseText = CreateLabel(header.transform, "CaseId", string.Empty, 13, DeskUiPalette.ManilaLight, FontStyle.Normal, TextAnchor.UpperRight);
            headerCaseText.rectTransform.anchorMin = new Vector2(1f, 1f);
            headerCaseText.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerCaseText.rectTransform.pivot = new Vector2(1f, 1f);
            headerCaseText.rectTransform.anchoredPosition = new Vector2(-100f, -47f);
            headerCaseText.rectTransform.sizeDelta = new Vector2(280f, 20f);

            Button closeButton = CreateActionButton(header.transform, "CloseClipboard", "CLOSE", DeskUiPalette.Navy, DeskUiPalette.White, CloseClipboard, 38f, 82f);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-12f, 0f);
            closeRect.sizeDelta = new Vector2(82f, 38f);
            DeskUiPalette.AddHairline(header.transform, "HeaderRule", 78f, 2f, false);

            BuildPageTabs(clipboardRoot.transform);
            BuildPageViews(clipboardRoot.transform);
            BuildFooter(clipboardRoot.transform);
        }

        private void BuildPageTabs(Transform parent)
        {
            GameObject tabHost = CreateImageObject(parent, "PageTabs", DeskUiPalette.Kraft);
            SetTopRect(tabHost.GetComponent<RectTransform>(), 96f, 48f, 12f);

            GameObject scrollObject = new GameObject("PageTabScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(tabHost.transform, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            Stretch(scrollRect, 4f, 3f);
            DeskUiPalette.ApplyFill(scrollObject.GetComponent<Image>(), DeskUiPalette.Kraft);
            tabScroll = scrollObject.GetComponent<ScrollRect>();
            tabScroll.horizontal = true;
            tabScroll.vertical = false;
            tabScroll.movementType = ScrollRect.MovementType.Clamped;
            tabScroll.scrollSensitivity = 35f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            tabContentRoot = content.transform;
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            tabScroll.viewport = viewport.GetComponent<RectTransform>();
            tabScroll.content = contentRect;
        }

        private void BuildPageViews(Transform parent)
        {
            GameObject contentHost = CreateImageObject(parent, "ClipboardPage", DeskUiPalette.ManilaLight);
            RectTransform hostRect = contentHost.GetComponent<RectTransform>();
            hostRect.anchorMin = new Vector2(0f, 0f);
            hostRect.anchorMax = new Vector2(1f, 1f);
            hostRect.offsetMin = new Vector2(14f, 96f);
            hostRect.offsetMax = new Vector2(-14f, -154f);

            caseCoverView = CreateImageObject(contentHost.transform, "CaseCoverView", DeskUiPalette.ManilaLight);
            Stretch(caseCoverView.GetComponent<RectTransform>());
            BuildCaseCover(caseCoverView.transform);

            suspectPageView = CreateImageObject(contentHost.transform, "SuspectPageView", DeskUiPalette.ManilaLight);
            Stretch(suspectPageView.GetComponent<RectTransform>());
            BuildSuspectPage(suspectPageView.transform);
        }

        private void BuildCaseCover(Transform parent)
        {
            GameObject scrollObject = CreateScrollArea(parent, "CaseBriefingScroll", DeskUiPalette.ManilaLight, out caseCoverScroll, out caseCoverContent);
            Stretch(scrollObject.GetComponent<RectTransform>(), 12f, 12f);
            VerticalLayoutGroup layout = caseCoverContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 28, 32);
            layout.spacing = 18f;
            caseCoverScroll.scrollSensitivity = 42f;
        }

        private void BuildSuspectPage(Transform parent)
        {
            GameObject profilePanel = CreateImageObject(parent, "SuspectStatement", DeskUiPalette.TranscriptBg);
            SetAnchors(profilePanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.37f, 1f),
                new Vector2(0f, -208f), new Vector2(-12f, 0f));
            CreateSectionCaption(profilePanel.transform, "STATEMENT / ALIBI");
            GameObject profileScrollObject = CreateScrollArea(profilePanel.transform, "ProfileScroll", DeskUiPalette.TranscriptBg, out profileScroll, out profileContent);
            Stretch(profileScrollObject.GetComponent<RectTransform>(), 10f, 10f, 8f, 38f);
            profileScroll.scrollSensitivity = 32f;

            GameObject evidencePanel = CreateImageObject(parent, "CollectedEvidencePanel", DeskUiPalette.TranscriptBg);
            SetAnchors(evidencePanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.37f, 1f),
                new Vector2(0f, 0f), new Vector2(-12f, -220f));
            CreateSectionCaption(evidencePanel.transform, "COLLECTED RECORDS  ·  PIN ONE TO PRESENT");
            GameObject evidenceScrollObject = CreateScrollArea(evidencePanel.transform, "EvidenceScroll", DeskUiPalette.TranscriptBg, out evidenceScroll, out evidenceContent);
            Stretch(evidenceScrollObject.GetComponent<RectTransform>(), 8f, 8f, 8f, 38f);
            evidenceScroll.scrollSensitivity = 38f;

            GameObject chatPanel = CreateImageObject(parent, "InterviewLogPanel", DeskUiPalette.TranscriptBg);
            SetAnchors(chatPanel.GetComponent<RectTransform>(), new Vector2(0.39f, 0.37f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            CreateSectionCaption(chatPanel.transform, "INTERVIEW TRANSCRIPT");
            GameObject chatScrollObject = CreateScrollArea(chatPanel.transform, "ChatScroll", DeskUiPalette.TranscriptBg, out chatScroll, out chatContent);
            Stretch(chatScrollObject.GetComponent<RectTransform>(), 8f, 8f, 8f, 38f);
            chatScroll.scrollSensitivity = 42f;

            GameObject questionPanel = CreateImageObject(parent, "QuestionChoicesPanel", DeskUiPalette.TranscriptBg);
            SetAnchors(questionPanel.GetComponent<RectTransform>(), new Vector2(0.39f, 0f), new Vector2(1f, 0.34f),
                new Vector2(0f, 0f), new Vector2(0f, -6f));
            CreateSectionCaption(questionPanel.transform, "EVIDENCE-DEPENDENT QUESTIONS");
            GameObject questionScrollObject = CreateScrollArea(questionPanel.transform, "QuestionScroll", DeskUiPalette.TranscriptBg, out questionScroll, out questionContent);
            Stretch(questionScrollObject.GetComponent<RectTransform>(), 8f, 8f, 8f, 38f);
            questionScroll.scrollSensitivity = 38f;
        }

        private void BuildFooter(Transform parent)
        {
            GameObject footer = CreateImageObject(parent, "ClipboardFooter", DeskUiPalette.Blotter);
            RectTransform footerRect = footer.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.offsetMin = new Vector2(10f, 10f);
            footerRect.offsetMax = new Vector2(-10f, 82f);
            DeskUiPalette.AddHairline(footer.transform, "FooterRule", 0f, 2f, false);

            footerStatusText = CreateLabel(footer.transform, "FooterStatus", "No active case.", 14, DeskUiPalette.ManilaLight, FontStyle.Normal, TextAnchor.MiddleLeft);
            footerStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            footerStatusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            footerStatusText.rectTransform.offsetMin = new Vector2(16f, 8f);
            footerStatusText.rectTransform.offsetMax = new Vector2(-470f, -8f);

            beginInterviewButton = CreateActionButton(footer.transform, "BeginInterview", "BEGIN INTERVIEW", DeskUiPalette.Navy, DeskUiPalette.White, BeginCurrentInterview, 52f, 194f);
            RectTransform beginRect = beginInterviewButton.GetComponent<RectTransform>();
            beginRect.anchorMin = new Vector2(1f, 0.5f);
            beginRect.anchorMax = new Vector2(1f, 0.5f);
            beginRect.pivot = new Vector2(1f, 0.5f);
            beginRect.anchoredPosition = new Vector2(-258f, 0f);
            beginRect.sizeDelta = new Vector2(194f, 52f);

            holdButton = CreateActionButton(footer.transform, "HoldSuspect", "HOLD", DeskUiPalette.StampHold, DeskUiPalette.White, StampHold, 52f, 112f);
            RectTransform holdRect = holdButton.GetComponent<RectTransform>();
            holdRect.anchorMin = new Vector2(1f, 0.5f);
            holdRect.anchorMax = new Vector2(1f, 0.5f);
            holdRect.pivot = new Vector2(1f, 0.5f);
            holdRect.anchoredPosition = new Vector2(-132f, 0f);
            holdRect.sizeDelta = new Vector2(112f, 52f);

            releaseButton = CreateActionButton(footer.transform, "ReleaseSuspect", "RELEASE", DeskUiPalette.StampRelease, DeskUiPalette.White, StampRelease, 52f, 112f);
            RectTransform releaseRect = releaseButton.GetComponent<RectTransform>();
            releaseRect.anchorMin = new Vector2(1f, 0.5f);
            releaseRect.anchorMax = new Vector2(1f, 0.5f);
            releaseRect.pivot = new Vector2(1f, 0.5f);
            releaseRect.anchoredPosition = new Vector2(-12f, 0f);
            releaseRect.sizeDelta = new Vector2(112f, 52f);
        }

        private void BuildSubtitle(Transform parent)
        {
            subtitleRoot = CreateImageObject(parent, "SuspectSubtitle", new Color(DeskUiPalette.NavyDark.r, DeskUiPalette.NavyDark.g, DeskUiPalette.NavyDark.b, 0.97f));
            RectTransform rect = subtitleRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, 76f);
            DeskUiPalette.AddHairline(subtitleRoot.transform, "SubtitleRule", 0f, 2f, false);
            subtitleText = CreateLabel(subtitleRoot.transform, "SubtitleText", string.Empty, 20, DeskUiPalette.ManilaLight, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(subtitleText.rectTransform, 42f, 18f, 24f, 10f);
            subtitleText.alignment = TextAnchor.MiddleLeft;
            subtitleText.verticalOverflow = VerticalWrapMode.Truncate;
            subtitleRoot.SetActive(false);
        }

        private void RebuildPageTabs()
        {
            if (tabContentRoot == null)
                return;

            ClearChildren(tabContentRoot);
            AddPageTab("CASE COVER", 0);

            if (observedSession != null && observedSession.Case != null)
            {
                IReadOnlyList<SuspectRecord> suspects = observedSession.Case.Suspects;
                for (int i = 0; i < suspects.Count; i++)
                {
                    SuspectRecord suspect = suspects[i];
                    if (suspect == null)
                        continue;
                    AddPageTab((i + 1).ToString("00") + "  ·  " + suspect.DisplayName.ToUpperInvariant(), i + 1);
                }
            }

            Canvas.ForceUpdateCanvases();
            if (tabScroll != null)
                tabScroll.horizontalNormalizedPosition = 0f;
        }

        private void AddPageTab(string label, int pageIndex)
        {
            GameObject tab = new GameObject("PageTab_" + pageIndex, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            tab.transform.SetParent(tabContentRoot, false);
            LayoutElement element = tab.GetComponent<LayoutElement>();
            element.minWidth = pageIndex == 0 ? 150f : 210f;
            element.preferredWidth = element.minWidth;
            Text text = CreateLabel(tab.transform, "Label", label, 13, DeskUiPalette.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f, 2f);
            Button button = tab.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                selectedPageIndex = pageIndex;
                RefreshPage();
            });
            DeskUiPalette.StyleFolderTag(button, pageIndex == selectedPageIndex);
        }

        private void RefreshPage()
        {
            if (runtimeCanvas == null)
                return;

            CheckForNewSession();
            RepaintPageTabs();
            SuspectRecord selectedSuspect = GetSelectedSuspect();
            bool isCover = selectedPageIndex == 0 || selectedSuspect == null;

            if (caseCoverView != null)
                caseCoverView.SetActive(isCover);
            if (suspectPageView != null)
                suspectPageView.SetActive(!isCover);

            if (headerTitleText != null)
                headerTitleText.text = isCover ? "CASE COVER" : selectedSuspect.DisplayName.ToUpperInvariant();
            if (headerCaseText != null)
            {
                CaseFile caseFile = observedSession != null ? observedSession.Case : null;
                headerCaseText.text = caseFile == null ? string.Empty : "FILE  " + caseFile.CaseId;
            }

            if (isCover)
                RefreshCaseCover();
            else
                RefreshSuspectPage(selectedSuspect);

            RefreshFooter(isCover, selectedSuspect);
        }

        private void RepaintPageTabs()
        {
            if (tabContentRoot == null)
                return;

            for (int i = 0; i < tabContentRoot.childCount; i++)
            {
                Transform child = tabContentRoot.GetChild(i);
                if (child == null)
                    continue;
                Button button = child.GetComponent<Button>();
                if (button != null)
                    DeskUiPalette.StyleFolderTag(button, i == selectedPageIndex);
            }
        }

        private void RefreshCaseCover()
        {
            if (caseCoverContent == null)
                return;

            ClearChildren(caseCoverContent);
            CaseFile caseFile = observedSession != null ? observedSession.Case : null;
            if (caseFile == null)
            {
                AddFitText(caseCoverContent, "No active case is assigned to this clipboard. Begin a shift through the existing case flow.", 20, DeskUiPalette.InkMuted, FontStyle.Italic);
                return;
            }

            AddFitText(caseCoverContent, caseFile.Title ?? "Untitled case", 28, DeskUiPalette.NavyDark, FontStyle.Bold);
            AddFitText(caseCoverContent, "CASE ID  ·  " + (caseFile.CaseId ?? string.Empty), 15, DeskUiPalette.InkMuted, FontStyle.Bold);
            AddFitText(caseCoverContent, "BRIEFING", 16, DeskUiPalette.Navy, FontStyle.Bold);
            AddFitText(caseCoverContent, string.IsNullOrWhiteSpace(caseFile.Briefing) ? "No briefing has been filed." : caseFile.Briefing.Trim(), 19, DeskUiPalette.Ink, FontStyle.Normal);

            AddFitText(caseCoverContent, "CURRENT ASSIGNMENT", 16, DeskUiPalette.Navy, FontStyle.Bold);
            SuspectRecord current = shiftController != null ? shiftController.CurrentSuspect : null;
            if (current == null)
            {
                AddFitText(caseCoverContent, "No suspect is currently assigned. Check the active case phase.", 17, DeskUiPalette.InkMuted, FontStyle.Italic);
            }
            else
            {
                string role = string.IsNullOrWhiteSpace(current.CampusRole) ? string.Empty : "  ·  " + current.CampusRole;
                AddFitText(caseCoverContent, current.DisplayName + role, 18, DeskUiPalette.Ink, FontStyle.Bold);
                if (!string.IsNullOrWhiteSpace(current.Statement))
                    AddFitText(caseCoverContent, "STATEMENT\n" + current.Statement.Trim(), 16, DeskUiPalette.Ink, FontStyle.Normal);
                if (!string.IsNullOrWhiteSpace(current.Alibi))
                    AddFitText(caseCoverContent, "ALIBI\n" + current.Alibi.Trim(), 16, DeskUiPalette.Ink, FontStyle.Normal);
            }

            IReadOnlyList<EvidenceRecord> collected = shiftController != null ? shiftController.GetCollectedEvidence() : Array.Empty<EvidenceRecord>();
            AddFitText(caseCoverContent, "EVIDENCE FILED  ·  " + collected.Count, 15, DeskUiPalette.InkMuted, FontStyle.Bold);
            Canvas.ForceUpdateCanvases();
            if (caseCoverScroll != null)
                caseCoverScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshSuspectPage(SuspectRecord suspect)
        {
            if (suspect == null)
                return;

            string profile = suspect.DisplayName;
            if (!string.IsNullOrWhiteSpace(suspect.CampusRole))
                profile += "\n" + suspect.CampusRole.Trim();
            profile += "\n\nSTATEMENT\n" + (string.IsNullOrWhiteSpace(suspect.Statement) ? "No statement filed." : suspect.Statement.Trim());
            profile += "\n\nALIBI\n" + (string.IsNullOrWhiteSpace(suspect.Alibi) ? "No alibi filed." : suspect.Alibi.Trim());
            if (profileContent != null)
            {
                ClearChildren(profileContent);
                AddFitText(profileContent, profile, 16, DeskUiPalette.Ink, FontStyle.Normal);
                RebuildScrollAtTop(profileScroll, profileContent);
            }

            RefreshEvidence(suspect);
            RefreshChat(suspect);
            RefreshQuestionChoices(suspect);
        }

        private void RefreshEvidence(SuspectRecord suspect)
        {
            if (evidenceContent == null)
                return;

            ClearChildren(evidenceContent);
            IReadOnlyList<EvidenceRecord> collected = shiftController != null ? shiftController.GetCollectedEvidence() : Array.Empty<EvidenceRecord>();
            int visible = 0;
            string pinnedId = GetPinnedEvidenceId(suspect.Id);
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord evidence = collected[i];
                if (evidence == null || !string.Equals(shiftController.FindEvidenceOwnerId(evidence.Id), suspect.Id, StringComparison.Ordinal))
                    continue;

                visible++;
                bool pinned = string.Equals(pinnedId, evidence.Id, StringComparison.Ordinal);
                string description = evidence.Description ?? string.Empty;
                string title = (pinned ? "PINNED  ·  " : "PIN  ·  ") + evidence.Title;
                string id = evidence.Id;
                CreateEvidenceButton(title, description, pinned, () => TogglePinnedEvidence(suspect.Id, id));
            }

            if (visible == 0)
                AddFitText(evidenceContent, "No records collected for this suspect yet. Collect case evidence through the existing investigation flow.", 15, DeskUiPalette.InkMuted, FontStyle.Italic);

            RebuildScrollAtTop(evidenceScroll, evidenceContent);
        }

        private void RefreshChat(SuspectRecord suspect)
        {
            if (chatContent == null)
                return;

            ClearChildren(chatContent);
            List<ChatTurn> turns;
            if (!turnsBySuspect.TryGetValue(suspect.Id, out turns) || turns.Count == 0)
            {
                AddFitText(chatContent, "No interview turns yet.", 15, DeskUiPalette.InkMuted, FontStyle.Italic);
            }
            else
            {
                for (int i = 0; i < turns.Count; i++)
                {
                    ChatTurn turn = turns[i];
                    CreateChatTurn(suspect.DisplayName, turn);
                }
            }

            Canvas.ForceUpdateCanvases();
            if (chatScroll != null)
                chatScroll.verticalNormalizedPosition = 0f;
        }

        private void RefreshQuestionChoices(SuspectRecord suspect)
        {
            if (questionContent == null)
                return;

            ClearChildren(questionContent);
            string evidenceId = GetPinnedEvidenceId(suspect.Id);
            bool canAsk = shiftController != null &&
                          shiftController.ActiveSession != null &&
                          shiftController.Phase == CaseShiftPhase.Investigating &&
                          shiftController.CurrentSuspect != null &&
                          string.Equals(shiftController.CurrentSuspect.Id, suspect.Id, StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                AddFitText(questionContent, "Pin one of this suspect's collected records to reveal its question tree.", 15, DeskUiPalette.InkMuted, FontStyle.Italic);
                return;
            }

            if (!canAsk)
            {
                AddFitText(questionContent, "Question choices are available during this suspect's active interview.", 15, DeskUiPalette.InkMuted, FontStyle.Italic);
                return;
            }

            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
            {
                AddFitText(questionContent, "Waiting for the suspect's reply…", 15, DeskUiPalette.InkMuted, FontStyle.Italic);
                return;
            }

            List<DialogueQuestionChoice> choices = scenarioClient != null
                ? scenarioClient.GetQuestionChoices(suspect.Id, evidenceId)
                : null;
            if (choices == null || choices.Count == 0)
            {
                AddFitText(questionContent, "No authored questions are available for this record.", 15, DeskUiPalette.InkMuted, FontStyle.Italic);
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                DialogueQuestionChoice choice = choices[i];
                if (choice == null || string.IsNullOrWhiteSpace(choice.Prompt))
                    continue;
                DialogueQuestionChoice capturedChoice = choice;
                string prefix = choice.Aggressive ? "PRESS  ·  " : "ASK  ·  ";
                CreateChoiceButton(prefix + choice.Prompt, choice.Aggressive, () => AskQuestion(suspect, capturedChoice));
            }

            RebuildScrollAtTop(questionScroll, questionContent);
        }

        private void RefreshFooter(bool isCover, SuspectRecord selectedSuspect)
        {
            if (shiftController == null || shiftController.ActiveSession == null)
            {
                SetButtonVisible(beginInterviewButton, false);
                SetButtonVisible(holdButton, false);
                SetButtonVisible(releaseButton, false);
                if (footerStatusText != null)
                    footerStatusText.text = "No active case. The clipboard is ready when a shift begins.";
                return;
            }

            CaseShiftPhase phase = shiftController.Phase;
            SuspectRecord current = shiftController.CurrentSuspect;
            bool pageIsCurrent = isCover || (current != null && selectedSuspect != null &&
                string.Equals(current.Id, selectedSuspect.Id, StringComparison.Ordinal));
            bool canBegin = pageIsCurrent && current != null && phase == CaseShiftPhase.Briefing;
            bool canDecide = pageIsCurrent && current != null && phase == CaseShiftPhase.Investigating;

            SetButtonVisible(beginInterviewButton, true);
            SetButtonVisible(holdButton, true);
            SetButtonVisible(releaseButton, true);
            if (beginInterviewButton != null)
                beginInterviewButton.interactable = canBegin;
            if (holdButton != null)
                holdButton.interactable = canDecide;
            if (releaseButton != null)
                releaseButton.interactable = canDecide;

            if (footerStatusText == null)
                return;

            if (!string.IsNullOrWhiteSpace(statusMessage))
                footerStatusText.text = statusMessage;
            else if (!pageIsCurrent && current != null)
                footerStatusText.text = "Viewing another suspect's page. Select the current suspect to begin or stamp.";
            else if (phase == CaseShiftPhase.Briefing && current != null)
                footerStatusText.text = "BRIEFING  ·  " + current.DisplayName + "  ·  Begin the interview to enter the Investigating phase.";
            else if (phase == CaseShiftPhase.Investigating && current != null)
                footerStatusText.text = "INTERVIEW  ·  " + current.DisplayName + "  ·  Questions left: " + shiftController.GetQuestionsLeft(current.Id);
            else
                footerStatusText.text = "SHIFT PHASE  ·  " + phase.ToString().ToUpperInvariant();
        }

        private void TogglePinnedEvidence(string suspectId, string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(suspectId) || string.IsNullOrWhiteSpace(evidenceId))
                return;

            if (string.Equals(GetPinnedEvidenceId(suspectId), evidenceId, StringComparison.Ordinal))
                pinnedEvidenceBySuspect.Remove(suspectId);
            else
                pinnedEvidenceBySuspect[suspectId] = evidenceId;

            statusMessage = string.IsNullOrEmpty(GetPinnedEvidenceId(suspectId))
                ? "Evidence unpinned."
                : "Evidence pinned for presentation.";
            RefreshPage();
        }

        private string GetPinnedEvidenceId(string suspectId)
        {
            string evidenceId;
            return !string.IsNullOrWhiteSpace(suspectId) && pinnedEvidenceBySuspect.TryGetValue(suspectId, out evidenceId)
                ? evidenceId
                : string.Empty;
        }

        private void AskQuestion(SuspectRecord suspect, DialogueQuestionChoice choice)
        {
            if (suspect == null || choice == null || string.IsNullOrWhiteSpace(choice.Prompt) ||
                shiftController == null || dialogueService == null || shiftController.ActiveSession == null ||
                shiftController.Phase != CaseShiftPhase.Investigating || shiftController.CurrentSuspect == null ||
                !string.Equals(shiftController.CurrentSuspect.Id, suspect.Id, StringComparison.Ordinal))
                return;

            if (awaitingReply || dialogueService.IsBusy)
                return;

            string evidenceId = GetPinnedEvidenceId(suspect.Id);
            if (string.IsNullOrWhiteSpace(evidenceId))
                return;

            string question = choice.Prompt.Trim();
            pendingRequestSession = shiftController.ActiveSession;
            pendingRequestSuspectId = suspect.Id;
            pendingRequestQuestion = question;
            pendingRequestEvidenceId = evidenceId;
            awaitingReply = true;
            statusMessage = "Question submitted · waiting for the suspect…";
            RefreshPage();

            bool accepted = dialogueService.AskCurrentSuspect(question, evidenceId, choice.Aggressive, choice.Id);
            if (!accepted)
            {
                awaitingReply = false;
                ClearPendingRequest();
                statusMessage = "The interview could not accept that question. Check the active phase and try again.";
                RefreshPage();
            }
        }

        private void BeginCurrentInterview()
        {
            if (shiftController == null || shiftController.ActiveSession == null || shiftController.CurrentSuspect == null)
                return;
            if (shiftController.Phase != CaseShiftPhase.Briefing)
                return;

            try
            {
                shiftController.BeginCurrentInterview();
                statusMessage = "Interview begun. Pin a collected record, then choose a tree question.";
            }
            catch (Exception exception)
            {
                statusMessage = "Could not begin interview: " + exception.Message;
                Debug.LogWarning(statusMessage, this);
            }
            RefreshPage();
        }

        private void StampHold()
        {
            StampCurrentSuspect(SuspectDecision.Hold);
        }

        private void StampRelease()
        {
            StampCurrentSuspect(SuspectDecision.Release);
        }

        private void StampCurrentSuspect(SuspectDecision decision)
        {
            if (shiftController == null || shiftController.ActiveSession == null ||
                shiftController.Phase != CaseShiftPhase.Investigating || shiftController.CurrentSuspect == null)
                return;

            try
            {
                if (decision == SuspectDecision.Hold)
                    shiftController.StampHold();
                else if (decision == SuspectDecision.Release)
                    shiftController.StampRelease();
                statusMessage = decision == SuspectDecision.Hold ? "HOLD stamped." : "RELEASE stamped.";
            }
            catch (Exception exception)
            {
                statusMessage = "Could not record the decision: " + exception.Message;
                Debug.LogWarning(statusMessage, this);
            }
            RefreshPage();
        }

        private void CloseClipboard()
        {
            SetOpen(false);
        }

        private SuspectRecord GetSelectedSuspect()
        {
            if (observedSession == null || selectedPageIndex <= 0)
                return null;
            int suspectIndex = selectedPageIndex - 1;
            IReadOnlyList<SuspectRecord> suspects = observedSession.Case.Suspects;
            return suspectIndex >= 0 && suspectIndex < suspects.Count ? suspects[suspectIndex] : null;
        }

        private void CreateEvidenceButton(string title, string description, bool selected, Action onClick)
        {
            GameObject row = new GameObject("EvidenceRecord", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(evidenceContent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 82f;
            row.GetComponent<LayoutElement>().minHeight = 68f;
            Text titleText = CreateLabel(row.transform, "Title", title, 14, selected ? DeskUiPalette.White : DeskUiPalette.NavyDark, FontStyle.Bold, TextAnchor.UpperLeft);
            SetTopLeftRect(titleText.rectTransform, 12f, -8f, 500f, 20f);
            Text descriptionText = CreateLabel(row.transform, "Description", description, 13, selected ? DeskUiPalette.White : DeskUiPalette.InkMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Stretch(descriptionText.rectTransform, 12f, 10f, 34f, 8f);
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;

            Image image = row.GetComponent<Image>();
            Button button = row.GetComponent<Button>();
            DeskUiPalette.StyleFormRow(button, image, titleText,
                selected ? DeskUiPalette.Navy : DeskUiPalette.ManilaLight,
                selected ? DeskUiPalette.White : DeskUiPalette.Ink,
                true);
            titleText.color = selected ? DeskUiPalette.White : DeskUiPalette.NavyDark;
            descriptionText.color = selected ? DeskUiPalette.ManilaLight : DeskUiPalette.InkMuted;
            button.onClick.AddListener(() => onClick());
        }

        private void CreateChoiceButton(string label, bool aggressive, Action onClick)
        {
            GameObject row = new GameObject("QuestionChoice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(questionContent, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            float height = label.Length > 90 ? 66f : 52f;
            layout.preferredHeight = height;
            layout.minHeight = height;
            Text text = CreateLabel(row.transform, "Label", label, 14, DeskUiPalette.White, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 14f, 5f);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Button button = row.GetComponent<Button>();
            DeskUiPalette.StyleFormRow(button, row.GetComponent<Image>(), text,
                aggressive ? DeskUiPalette.NavyDark : DeskUiPalette.Navy,
                DeskUiPalette.White,
                true);
            button.onClick.AddListener(() => onClick());
        }

        private void CreateChatTurn(string suspectName, ChatTurn turn)
        {
            GameObject row = CreateImageObject(chatContent, "DialogueTurn", new Color(0.92f, 0.88f, 0.77f, 1f));
            VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddChatLine(row.transform, "DETECTIVE", turn.Question, DeskUiPalette.Navy, FontStyle.Bold);
            AddChatLine(row.transform, suspectName.ToUpperInvariant(), turn.Reply, DeskUiPalette.Ink, FontStyle.Normal);
            row.AddComponent<LayoutElement>().minHeight = 70f;
        }

        private void AddChatLine(Transform parent, string speaker, string line, Color color, FontStyle style)
        {
            Text text = CreateLabel(parent, "ChatLine", speaker + "\n" + line, 15, color, style, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            AddPreferredHeightFitter(text);
        }

        private void CreateSectionCaption(Transform parent, string caption)
        {
            Text label = CreateLabel(parent, "SectionCaption", caption, 13, DeskUiPalette.Navy, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetTopLeftRect(label.rectTransform, 12f, -8f, 600f, 22f);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            DeskUiPalette.AddHairline(parent, "SectionRule", 34f, 1.5f, true);
        }

        private void AddFitText(Transform parent, string value, int fontSize, Color color, FontStyle style)
        {
            Text text = CreateLabel(parent, "BriefingText", value ?? string.Empty, fontSize, color, style, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            AddPreferredHeightFitter(text);
        }

        private void AddPreferredHeightFitter(Text text)
        {
            if (text == null)
                return;
            ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private GameObject CreateScrollArea(Transform parent, string name, Color background, out ScrollRect scroll, out Transform contentRootTransform)
        {
            GameObject scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            DeskUiPalette.ApplyFill(scrollObject.GetComponent<Image>(), background);
            scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            contentRootTransform = content.transform;
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            return scrollObject;
        }

        private Button CreateActionButton(Transform parent, string name, string label, Color fill, Color textColor, Action onClick, float height, float width)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.minWidth = width;
            element.preferredWidth = width;
            Text text = CreateLabel(buttonObject.transform, "Label", label, 14, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 4f);
            Button button = buttonObject.GetComponent<Button>();
            DeskUiPalette.StyleStampButton(button, buttonObject.GetComponent<Image>(), text, fill, true);
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            return button;
        }

        private GameObject CreateImageObject(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            DeskUiPalette.ApplyFill(go.GetComponent<Image>(), color);
            return go;
        }

        private Text CreateLabel(Transform parent, string name, string value, int fontSize, Color color, FontStyle style, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value ?? string.Empty;
            DeskUiPalette.StyleLabel(text, fontSize, color, DeskUiPalette.TypeRole.Body, style, alignment);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }

        private void RebuildScrollAtTop(ScrollRect scroll, Transform content)
        {
            if (content == null)
                return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopRect(RectTransform rect, float topOffset, float height, float sideInset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = new Vector2(-sideInset * 2f, height);
        }

        private static void SetTopLeftRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
