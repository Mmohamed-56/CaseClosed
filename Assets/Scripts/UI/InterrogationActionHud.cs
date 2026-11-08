using System.Collections.Generic;
using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Player desk file HUD: case brief, records collect, interview ask, HOLD/RELEASE, final charge.
    /// Builds a Screen Space Overlay canvas at runtime when Bezi has not assigned scene refs.
    /// Does not own Gemini/TTS — only calls <see cref="InterrogationDialogueService"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationActionHud : MonoBehaviour
    {
        private const int MaxQuestionChars = 180;
        private const int MaxTranscriptLines = 40;

        [Header("Case Connection")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private InterrogationDialogueService dialogueService;
        [SerializeField] private EvidenceBoardController evidenceBoard;

        [Header("Optional Scene Hooks (Bezi)")]
        [Tooltip("When empty, a runtime Screen Space Overlay canvas is created.")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text headerTitleText;
        [SerializeField] private Text headerCaseText;
        [SerializeField] private Text statusText;
        [SerializeField] private InputField questionInput;
        [SerializeField] private Button askButton;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private Transform transcriptRoot;
        [SerializeField] private ScrollRect contentScroll;
        [SerializeField] private Button[] tabButtons;

        [Header("Behaviour")]
        [SerializeField] private bool startOpen = false;
        [SerializeField] private bool buildRuntimeUiIfMissing = true;

        private readonly List<string> transcript = new List<string>();
        private readonly HashSet<string> collectedIds = new HashSet<string>();

        private bool panelOpen;
        private int activeTab;
        private string selectedEvidenceId = string.Empty;
        private string statusMessage = string.Empty;
        private bool awaitingReply;
        private CaseResolution resolution;
        private bool runtimeBuilt;
        private Font uiFont;

        private Text runtimeContentLabel;
        private readonly List<Button> runtimeActionButtons = new List<Button>();
        private readonly List<Text> runtimeTranscriptLabels = new List<Text>();

        private Button tabCase;
        private Button tabRecords;
        private Button tabInterview;
        private Button tabDecision;
        private GameObject askRowRoot;

        /// <summary>True while the desk file panel is visible.</summary>
        public bool IsOpen { get { return panelOpen; } }

        private void Awake()
        {
            uiFont = DeskUiPalette.ResolveFont();

            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();

            if (dialogueService == null)
                dialogueService = GetComponent<InterrogationDialogueService>();
            if (dialogueService == null)
                dialogueService = FindAnyObjectByType<InterrogationDialogueService>();

            if (evidenceBoard == null)
                evidenceBoard = FindAnyObjectByType<EvidenceBoardController>();

            EnsureEventSystem();
            if ((hudCanvas == null || panelRoot == null) && buildRuntimeUiIfMissing)
                BuildRuntimeUi();

            WireChrome();
            SetPanelOpen(startOpen);
            RefreshChrome();
        }

        private void OnEnable()
        {
            if (dialogueService != null)
                dialogueService.TurnCompleted += OnDialogueTurnCompleted;
            if (shiftController != null)
            {
                shiftController.PhaseChanged += OnPhaseChanged;
                shiftController.EvidenceCollected += OnEvidenceCollected;
            }
            if (evidenceBoard != null)
                evidenceBoard.EvidenceSelectionChanged += OnBoardSelectionChanged;
        }

        private void OnDisable()
        {
            if (dialogueService != null)
                dialogueService.TurnCompleted -= OnDialogueTurnCompleted;
            if (shiftController != null)
            {
                shiftController.PhaseChanged -= OnPhaseChanged;
                shiftController.EvidenceCollected -= OnEvidenceCollected;
            }
            if (evidenceBoard != null)
                evidenceBoard.EvidenceSelectionChanged -= OnBoardSelectionChanged;
        }

        /// <summary>Bezi / Magdy: assign canvas + panel after scene placement.</summary>
        public void SetHudSceneHooks(Canvas canvas, GameObject panel, Button open, Button close)
        {
            hudCanvas = canvas;
            panelRoot = panel;
            openButton = open;
            closeButton = close;
            WireChrome();
            SetPanelOpen(panelOpen);
        }

        /// <summary>Resets transcript / selection when a new case starts.</summary>
        public void ResetForNewCase(string readyMessage = null)
        {
            resolution = null;
            awaitingReply = false;
            selectedEvidenceId = string.Empty;
            collectedIds.Clear();
            transcript.Clear();
            statusMessage = string.IsNullOrWhiteSpace(readyMessage)
                ? "Desk file open. Review records, then begin the interview."
                : readyMessage;
            activeTab = 0;
            SyncCollectedFromSession();
            RefreshAll();
        }

        /// <summary>Opens or closes the desk file panel.</summary>
        public void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (panelRoot != null)
                panelRoot.SetActive(open);
            if (openButton != null)
                openButton.gameObject.SetActive(!open);
            if (open)
                RefreshAll();
        }

        public void TogglePanel()
        {
            SetPanelOpen(!panelOpen);
        }

        private void WireChrome()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(TogglePanel);
                openButton.onClick.AddListener(TogglePanel);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
                closeButton.onClick.AddListener(ClosePanel);
            }
            if (askButton != null)
            {
                askButton.onClick.RemoveListener(AskQuestion);
                askButton.onClick.AddListener(AskQuestion);
            }
            WireTab(tabCase, 0);
            WireTab(tabRecords, 1);
            WireTab(tabInterview, 2);
            WireTab(tabDecision, 3);
            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int index = i;
                    WireTab(tabButtons[i], index);
                }
            }
        }

        private void WireTab(Button button, int index)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectTab(index));
        }

        private void ClosePanel()
        {
            SetPanelOpen(false);
        }

        private void SelectTab(int index)
        {
            activeTab = Mathf.Clamp(index, 0, 3);
            RefreshAll();
        }

        private void OnPhaseChanged(CaseShiftPhase phase)
        {
            if (phase == CaseShiftPhase.Headline || phase == CaseShiftPhase.Resolved)
                resolution = shiftController != null ? shiftController.LastResolution : resolution;
            if (phase == CaseShiftPhase.Charging)
                activeTab = 3;
            RefreshAll();
        }

        private void OnEvidenceCollected(EvidenceRecord record)
        {
            if (record != null)
                collectedIds.Add(record.Id);
            if (record != null && !string.IsNullOrWhiteSpace(record.Title))
                statusMessage = "Filed: " + record.Title;
            RefreshAll();
        }

        private void OnBoardSelectionChanged(string evidenceId)
        {
            selectedEvidenceId = evidenceId ?? string.Empty;
            RefreshAll();
        }

        private void OnDialogueTurnCompleted(SuspectDialogueTurn turn)
        {
            awaitingReply = false;
            if (turn == null || turn.StressResult == null)
            {
                statusMessage = "No reply.";
                RefreshAll();
                return;
            }

            InterrogationResult result = turn.StressResult;
            string line = turn.Reply != null ? turn.Reply.line : result.FallbackLine;
            string name = string.IsNullOrWhiteSpace(turn.SuspectName) ? "Suspect" : turn.SuspectName;

            AppendTranscript(name + ": " + (line ?? string.Empty).Trim());
            statusMessage = name + " — stress " + result.Stress + "/100 · " + result.QuestionsLeft + " questions left";
            if (!string.IsNullOrEmpty(turn.Error))
                statusMessage += " · connection note recorded";
            RefreshAll();
        }

        private void AskQuestion()
        {
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                return;

            string ask = questionInput != null ? questionInput.text : string.Empty;
            ask = ask == null ? string.Empty : ask.Trim();
            if (ask.Length == 0)
            {
                statusMessage = "Type a question first.";
                RefreshStatus();
                return;
            }

            if (ask.Length > MaxQuestionChars)
                ask = ask.Substring(0, MaxQuestionChars);

            string evidenceId = ResolveSelectedEvidenceId();
            AppendTranscript("You: " + ask);
            if (questionInput != null)
                questionInput.text = string.Empty;

            awaitingReply = true;
            statusMessage = "Waiting on the suspect…";
            RefreshAll();

            bool started = dialogueService != null && dialogueService.AskCurrentSuspect(ask, evidenceId);
            if (!started)
            {
                awaitingReply = false;
                statusMessage = "Could not ask that right now.";
                RefreshAll();
            }
        }

        private string ResolveSelectedEvidenceId()
        {
            if (evidenceBoard != null && !string.IsNullOrWhiteSpace(evidenceBoard.SelectedEvidenceId))
                return evidenceBoard.SelectedEvidenceId;
            return selectedEvidenceId ?? string.Empty;
        }

        private void SyncCollectedFromSession()
        {
            collectedIds.Clear();
            if (shiftController == null) return;
            IReadOnlyList<EvidenceRecord> list = shiftController.GetCollectedEvidence();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    collectedIds.Add(list[i].Id);
            }
        }

        private void AppendTranscript(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            transcript.Add(line.Trim());
            while (transcript.Count > MaxTranscriptLines)
                transcript.RemoveAt(0);
        }

        private void RefreshAll()
        {
            RefreshChrome();
            RefreshTabs();
            RefreshContent();
            RefreshTranscript();
            RefreshStatus();
            RefreshAskRow();
        }

        private void RefreshChrome()
        {
            CaseFile caseFile = shiftController != null && shiftController.ActiveSession != null
                ? shiftController.ActiveSession.Case
                : null;

            if (headerTitleText != null)
                headerTitleText.text = "CASE CLOSED";
            if (headerCaseText != null)
            {
                if (caseFile == null || string.IsNullOrWhiteSpace(caseFile.Title))
                    headerCaseText.text = "No active case";
                else
                    headerCaseText.text = caseFile.Title + (string.IsNullOrWhiteSpace(caseFile.CaseId) ? string.Empty : "  ·  " + caseFile.CaseId);
            }
        }

        private void RefreshTabs()
        {
            SetTabVisual(tabCase, activeTab == 0);
            SetTabVisual(tabRecords, activeTab == 1);
            SetTabVisual(tabInterview, activeTab == 2);
            SetTabVisual(tabDecision, activeTab == 3);
        }

        private static void SetTabVisual(Button button, bool active)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? DeskUiPalette.TabActive : DeskUiPalette.TabIdle;
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;
            statusText.text = statusMessage ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusMessage));
        }

        private void RefreshAskRow()
        {
            bool interviewing = shiftController != null &&
                                shiftController.Phase == CaseShiftPhase.Investigating &&
                                activeTab == 2 &&
                                resolution == null;

            if (askRowRoot != null)
                askRowRoot.SetActive(interviewing);
            if (questionInput != null)
                questionInput.gameObject.SetActive(interviewing);
            if (askButton != null)
            {
                askButton.gameObject.SetActive(interviewing);
                askButton.interactable = interviewing && !awaitingReply &&
                                         (dialogueService == null || !dialogueService.IsBusy);
                Text label = askButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = awaitingReply ? "…" : "ASK";
            }
        }

        private void RefreshTranscript()
        {
            if (transcriptRoot == null) return;

            for (int i = 0; i < runtimeTranscriptLabels.Count; i++)
            {
                if (runtimeTranscriptLabels[i] != null)
                    Destroy(runtimeTranscriptLabels[i].gameObject);
            }
            runtimeTranscriptLabels.Clear();

            if (transcript.Count == 0)
            {
                transcriptRoot.gameObject.SetActive(activeTab == 2);
                return;
            }

            transcriptRoot.gameObject.SetActive(true);
            for (int i = 0; i < transcript.Count; i++)
            {
                Text line = CreateBodyText(transcriptRoot, transcript[i], 13, DeskUiPalette.InkMuted);
                runtimeTranscriptLabels.Add(line);
            }
        }

        private void RefreshContent()
        {
            ClearRuntimeActions();
            if (runtimeContentLabel == null && contentRoot == null)
                return;

            if (shiftController == null || shiftController.ActiveSession == null)
            {
                SetContentBody("No active case on the desk.");
                AddAction("Start case", () =>
                {
                    CaseGameplayBootstrap.RequestStartCase(shiftController);
                    statusMessage = "Case ready.";
                    RefreshAll();
                });
                return;
            }

            CaseShiftPhase phase = shiftController.Phase;
            if (resolution != null || phase == CaseShiftPhase.Headline || phase == CaseShiftPhase.Resolved)
            {
                if (resolution == null)
                    resolution = shiftController.LastResolution;
                if (activeTab == 3)
                    DrawResolution();
                else
                    SetContentBody("Morning edition is ready. Open Decision for the headline.");
                return;
            }

            switch (activeTab)
            {
                case 0:
                    DrawCase();
                    break;
                case 1:
                    DrawEvidence();
                    break;
                case 2:
                    DrawInterview();
                    break;
                default:
                    DrawDecision();
                    break;
            }
        }

        private void DrawCase()
        {
            CaseFile caseFile = shiftController.ActiveSession.Case;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            if (!string.IsNullOrWhiteSpace(caseFile.Title))
                sb.AppendLine(caseFile.Title);
            if (!string.IsNullOrWhiteSpace(caseFile.CaseId))
                sb.AppendLine("File " + caseFile.CaseId);
            sb.AppendLine();
            sb.AppendLine("BRIEFING");
            if (!string.IsNullOrWhiteSpace(caseFile.Briefing))
                sb.AppendLine(caseFile.Briefing.Trim());

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect != null)
            {
                sb.AppendLine();
                sb.AppendLine("CURRENT SUSPECT");
                sb.AppendLine(suspect.DisplayName + (string.IsNullOrWhiteSpace(suspect.CampusRole) ? string.Empty : " — " + suspect.CampusRole));
                if (!string.IsNullOrWhiteSpace(suspect.Statement))
                    sb.AppendLine("Statement: " + suspect.Statement.Trim());
                if (!string.IsNullOrWhiteSpace(suspect.Alibi))
                    sb.AppendLine("Public record: " + suspect.Alibi.Trim());
            }

            SetContentBody(sb.ToString().TrimEnd());
        }

        private void DrawEvidence()
        {
            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null)
            {
                SetContentBody("No suspect on the clipboard.");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.AppendLine("PUBLIC RECORDS — " + suspect.DisplayName);
            sb.AppendLine("Collect what you need before the interview.");
            SetContentBody(sb.ToString().TrimEnd());

            if (suspect.Evidence == null || suspect.Evidence.Count == 0)
            {
                AddAction("No records on file", null, false);
                return;
            }

            for (int i = 0; i < suspect.Evidence.Count; i++)
            {
                EvidenceRecord evidence = suspect.Evidence[i];
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.Title))
                    continue;

                bool collected = collectedIds.Contains(evidence.Id);
                string label = collected
                    ? "Collected · " + evidence.Title
                    : "Collect · " + evidence.Title;

                if (shiftController.Phase == CaseShiftPhase.Briefing && !collected)
                {
                    string id = evidence.Id;
                    string title = evidence.Title;
                    AddAction(label, () =>
                    {
                        bool added = shiftController.CollectEvidence(id);
                        if (added) collectedIds.Add(id);
                        statusMessage = added ? "Filed: " + title : "Already on the board.";
                        RefreshAll();
                    });
                }
                else
                {
                    AddAction(label, null, false);
                }
            }
        }

        private void DrawInterview()
        {
            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null)
            {
                SetContentBody("No suspect seated.");
                return;
            }

            if (shiftController.Phase == CaseShiftPhase.Briefing)
            {
                SetContentBody("Collect records first, then begin the interview with " + suspect.DisplayName + ".");
                AddAction("Begin interview — " + suspect.DisplayName, () =>
                {
                    shiftController.BeginCurrentInterview();
                    statusMessage = "Interview open. Type a question and press ASK.";
                    activeTab = 2;
                    RefreshAll();
                });
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.AppendLine("INTERVIEW — " + suspect.DisplayName);
            sb.AppendLine("Questions remaining: " + shiftController.GetQuestionsLeft(suspect.Id));
            string evidenceId = ResolveSelectedEvidenceId();
            if (!string.IsNullOrWhiteSpace(evidenceId))
                sb.AppendLine("Presenting: " + FindEvidenceTitle(evidenceId));
            else
                sb.AppendLine("Presenting: none");
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                sb.AppendLine("Suspect is answering…");
            SetContentBody(sb.ToString().TrimEnd());

            IReadOnlyList<EvidenceRecord> collected = shiftController.GetCollectedEvidence();
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord evidence = collected[i];
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.Title))
                    continue;
                string id = evidence.Id;
                bool selected = id == evidenceId;
                AddAction((selected ? "Selected · " : "Present · ") + evidence.Title, () =>
                {
                    if (selectedEvidenceId == id)
                        selectedEvidenceId = string.Empty;
                    else
                        selectedEvidenceId = id;

                    if (evidenceBoard != null)
                    {
                        if (string.IsNullOrEmpty(selectedEvidenceId))
                            evidenceBoard.ClearSelection();
                        else
                            evidenceBoard.SelectEvidence(selectedEvidenceId);
                    }
                    RefreshAll();
                });
            }
        }

        private void DrawDecision()
        {
            if (shiftController.Phase == CaseShiftPhase.Charging)
            {
                DrawCharging();
                return;
            }

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (shiftController.Phase == CaseShiftPhase.Briefing)
            {
                SetContentBody("Begin the interview before stamping HOLD or RELEASE.");
                return;
            }

            if (suspect == null)
            {
                SetContentBody("No suspect decision pending.");
                return;
            }

            SetContentBody("Stamp the clipboard for " + suspect.DisplayName + ".");
            AddAction("HOLD", () =>
            {
                shiftController.StampHold();
                statusMessage = "HOLD stamped.";
                selectedEvidenceId = string.Empty;
                if (evidenceBoard != null) evidenceBoard.ClearSelection();
                RefreshAll();
            }, true, DeskUiPalette.StampHold, DeskUiPalette.White);
            AddAction("RELEASE", () =>
            {
                shiftController.StampRelease();
                statusMessage = "RELEASE stamped.";
                selectedEvidenceId = string.Empty;
                if (evidenceBoard != null) evidenceBoard.ClearSelection();
                RefreshAll();
            }, true, DeskUiPalette.StampRelease, DeskUiPalette.White);
        }

        private void DrawCharging()
        {
            CaseSession session = shiftController.ActiveSession;
            SetContentBody((session.Case.Title ?? "Case") + "\nFINAL CHARGE\nChoose a held suspect, or charge nobody.");

            IReadOnlyList<SuspectRecord> held = shiftController.GetHeldSuspects();
            for (int i = 0; i < held.Count; i++)
            {
                SuspectRecord suspect = held[i];
                if (suspect == null || string.IsNullOrWhiteSpace(suspect.DisplayName))
                    continue;
                string id = suspect.Id;
                string name = suspect.DisplayName;
                AddAction("Charge " + name, () =>
                {
                    resolution = shiftController.ResolveCharge(id);
                    statusMessage = "Charge filed.";
                    activeTab = 3;
                    RefreshAll();
                }, true, DeskUiPalette.Charge, DeskUiPalette.White);
            }

            AddAction("Charge nobody", () =>
            {
                shiftController.ChargeNobody();
                resolution = shiftController.LastResolution;
                statusMessage = "No charge filed.";
                activeTab = 3;
                RefreshAll();
            });
        }

        private void DrawResolution()
        {
            if (resolution == null)
            {
                SetContentBody("No morning headline yet.");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            sb.AppendLine("MORNING HEADLINE");
            if (!string.IsNullOrWhiteSpace(resolution.Headline))
                sb.AppendLine(resolution.Headline.Trim());
            if (!string.IsNullOrWhiteSpace(resolution.Subheadline))
                sb.AppendLine(resolution.Subheadline.Trim());
            sb.AppendLine();
            sb.AppendLine("Outcome: " + resolution.Outcome);
            sb.AppendLine("Charged: " + (string.IsNullOrWhiteSpace(resolution.ChargedSuspectName) ? "Nobody" : resolution.ChargedSuspectName));
            sb.AppendLine("Score: " + resolution.Score);
            SetContentBody(sb.ToString().TrimEnd());

            AddAction("Restart case", () =>
            {
                CaseGameplayBootstrap.RequestStartCase(shiftController);
            });
        }

        private string FindEvidenceTitle(string evidenceId)
        {
            IReadOnlyList<EvidenceRecord> evidence = shiftController.GetCollectedEvidence();
            for (int i = 0; i < evidence.Count; i++)
            {
                if (evidence[i] != null && evidence[i].Id == evidenceId)
                    return evidence[i].Title;
            }
            return "record";
        }

        private void SetContentBody(string body)
        {
            if (runtimeContentLabel != null)
            {
                runtimeContentLabel.text = body ?? string.Empty;
                runtimeContentLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
            }
        }

        private void ClearRuntimeActions()
        {
            for (int i = 0; i < runtimeActionButtons.Count; i++)
            {
                if (runtimeActionButtons[i] != null)
                    Destroy(runtimeActionButtons[i].gameObject);
            }
            runtimeActionButtons.Clear();
        }

        private void AddAction(string label, System.Action onClick, bool interactable = true)
        {
            AddAction(label, onClick, interactable, DeskUiPalette.ManilaLight, DeskUiPalette.Ink);
        }

        private void AddAction(string label, System.Action onClick, bool interactable, Color fill, Color textColor)
        {
            if (contentRoot == null || string.IsNullOrWhiteSpace(label))
                return;

            GameObject go = new GameObject("Action_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(contentRoot, false);
            Image image = go.GetComponent<Image>();
            image.color = fill;
            LayoutElement layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 34f;
            layout.preferredHeight = 34f;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.GetComponent<Text>();
            DeskUiPalette.StyleLabel(text, 14, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(labelGo.GetComponent<RectTransform>(), 8f, 4f);

            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleButton(button, image, text, fill, textColor);
            button.interactable = interactable && onClick != null;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            runtimeActionButtons.Add(button);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void BuildRuntimeUi()
        {
            if (runtimeBuilt) return;
            runtimeBuilt = true;

            GameObject canvasGo = new GameObject("InterrogationActionHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            hudCanvas = canvasGo.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 50;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            openButton = CreateChromeButton(canvasGo.transform, "DeskFileOpen", "DESK FILE", new Vector2(12f, -12f), new Vector2(132f, 40f), DeskUiPalette.Navy, DeskUiPalette.White, TextAnchor.UpperLeft);

            GameObject panel = new GameObject("DeskFilePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            panelRoot = panel;
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = DeskUiPalette.Manila;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.offsetMin = new Vector2(12f, 12f);
            panelRect.offsetMax = new Vector2(472f, -58f);

            // Header
            GameObject header = CreateBox(panel.transform, "Header", DeskUiPalette.Navy);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 56f);
            headerRect.anchoredPosition = Vector2.zero;

            headerTitleText = CreateLabel(header.transform, "Title", "CASE CLOSED", 18, DeskUiPalette.White, FontStyle.Bold);
            AnchorTopLeft(headerTitleText.rectTransform, 14f, -8f, 220f, 24f);
            headerCaseText = CreateLabel(header.transform, "Case", string.Empty, 13, DeskUiPalette.ManilaLight, FontStyle.Normal);
            AnchorTopLeft(headerCaseText.rectTransform, 14f, -30f, 360f, 20f);

            closeButton = CreateChromeButton(header.transform, "Close", "CLOSE", new Vector2(-10f, -10f), new Vector2(72f, 28f), DeskUiPalette.NavyDark, DeskUiPalette.White, TextAnchor.UpperRight);

            // Tabs
            GameObject tabs = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabs.transform.SetParent(panel.transform, false);
            RectTransform tabsRect = tabs.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -56f);
            tabsRect.sizeDelta = new Vector2(0f, 34f);
            HorizontalLayoutGroup tabLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.padding = new RectOffset(8, 8, 2, 2);
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            tabCase = CreateTabButton(tabs.transform, "Case");
            tabRecords = CreateTabButton(tabs.transform, "Records");
            tabInterview = CreateTabButton(tabs.transform, "Interview");
            tabDecision = CreateTabButton(tabs.transform, "Decision");
            tabButtons = new[] { tabCase, tabRecords, tabInterview, tabDecision };

            // Content scroll
            GameObject scrollGo = new GameObject("ContentScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = DeskUiPalette.ManilaLight;
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10f, 210f);
            scrollRect.offsetMax = new Vector2(-10f, -96f);
            contentScroll = scrollGo.GetComponent<ScrollRect>();
            contentScroll.horizontal = false;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            contentRoot = content.transform;
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            contentScroll.viewport = viewport.GetComponent<RectTransform>();
            contentScroll.content = contentRect;

            runtimeContentLabel = CreateBodyText(contentRoot, string.Empty, 14, DeskUiPalette.Ink);
            LayoutElement bodyLayout = runtimeContentLabel.gameObject.GetComponent<LayoutElement>();
            if (bodyLayout == null)
                bodyLayout = runtimeContentLabel.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 40f;
            ContentSizeFitter bodyFit = runtimeContentLabel.gameObject.GetComponent<ContentSizeFitter>();
            if (bodyFit == null)
                bodyFit = runtimeContentLabel.gameObject.AddComponent<ContentSizeFitter>();
            bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Ask row
            GameObject askRow = new GameObject("AskRow", typeof(RectTransform), typeof(Image));
            askRow.transform.SetParent(panel.transform, false);
            askRowRoot = askRow;
            askRow.GetComponent<Image>().color = DeskUiPalette.Blotter;
            RectTransform askRect = askRow.GetComponent<RectTransform>();
            askRect.anchorMin = new Vector2(0f, 0f);
            askRect.anchorMax = new Vector2(1f, 0f);
            askRect.pivot = new Vector2(0.5f, 0f);
            askRect.offsetMin = new Vector2(0f, 148f);
            askRect.offsetMax = new Vector2(0f, 200f);

            GameObject inputGo = new GameObject("QuestionInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(askRow.transform, false);
            inputGo.GetComponent<Image>().color = DeskUiPalette.TranscriptBg;
            RectTransform inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0.5f);
            inputRect.anchorMax = new Vector2(1f, 0.5f);
            inputRect.pivot = new Vector2(0f, 0.5f);
            inputRect.anchoredPosition = new Vector2(10f, 0f);
            inputRect.sizeDelta = new Vector2(-100f, 34f);
            inputRect.offsetMax = new Vector2(-92f, 17f);
            inputRect.offsetMin = new Vector2(10f, -17f);

            Text placeholder = CreateLabel(inputGo.transform, "Placeholder", "Type your question…", 14, DeskUiPalette.InkMuted, FontStyle.Italic);
            Stretch(placeholder.rectTransform, 8f, 4f);
            Text inputText = CreateLabel(inputGo.transform, "Text", string.Empty, 14, DeskUiPalette.Ink, FontStyle.Normal);
            Stretch(inputText.rectTransform, 8f, 4f);
            questionInput = inputGo.GetComponent<InputField>();
            questionInput.textComponent = inputText;
            questionInput.placeholder = placeholder;
            questionInput.characterLimit = MaxQuestionChars;
            questionInput.lineType = InputField.LineType.SingleLine;

            askButton = CreateChromeButton(askRow.transform, "Ask", "ASK", new Vector2(-10f, 0f), new Vector2(74f, 34f), DeskUiPalette.Navy, DeskUiPalette.White, TextAnchor.MiddleRight);

            // Transcript
            GameObject transcriptGo = new GameObject("Transcript", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            transcriptGo.transform.SetParent(panel.transform, false);
            transcriptGo.GetComponent<Image>().color = DeskUiPalette.TranscriptBg;
            RectTransform tr = transcriptGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(10f, 42f);
            tr.offsetMax = new Vector2(-10f, 142f);
            VerticalLayoutGroup tlg = transcriptGo.GetComponent<VerticalLayoutGroup>();
            tlg.padding = new RectOffset(8, 8, 4, 4);
            tlg.spacing = 2f;
            tlg.childForceExpandWidth = true;
            tlg.childForceExpandHeight = false;
            transcriptRoot = transcriptGo.transform;

            // Status
            GameObject statusGo = new GameObject("Status", typeof(RectTransform), typeof(Image));
            statusGo.transform.SetParent(panel.transform, false);
            statusGo.GetComponent<Image>().color = DeskUiPalette.NavyDark;
            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(0f, 36f);
            statusRect.anchoredPosition = Vector2.zero;
            statusText = CreateLabel(statusGo.transform, "StatusText", string.Empty, 12, DeskUiPalette.ManilaLight, FontStyle.Normal);
            Stretch(statusText.rectTransform, 10f, 6f);
            statusText.alignment = TextAnchor.MiddleLeft;
        }

        private Button CreateTabButton(Transform parent, string label)
        {
            GameObject go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = DeskUiPalette.TabIdle;
            Text text = CreateLabel(go.transform, "Label", label.ToUpperInvariant(), 13, DeskUiPalette.Ink, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleButton(button, image, text, DeskUiPalette.TabIdle, DeskUiPalette.Ink);
            return button;
        }

        private Button CreateChromeButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color fill, Color textColor, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case TextAnchor.UpperRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.MiddleRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    break;
                default:
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = fill;
            Text text = CreateLabel(go.transform, "Label", label, 14, textColor, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleButton(button, image, text, fill, textColor);
            return button;
        }

        private static GameObject CreateBox(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text CreateLabel(Transform parent, string name, string value, int size, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            DeskUiPalette.StyleLabel(text, size, color, style);
            text.font = uiFont != null ? uiFont : DeskUiPalette.ResolveFont();
            text.text = value ?? string.Empty;
            return text;
        }

        private Text CreateBodyText(Transform parent, string value, int size, Color color)
        {
            Text text = CreateLabel(parent, "Body", value, size, color, FontStyle.Normal);
            LayoutElement layout = text.gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 18f;
            ContentSizeFitter fit = text.gameObject.GetComponent<ContentSizeFitter>();
            if (fit == null)
                fit = text.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }

        private static void Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        private static void AnchorTopLeft(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }

    /// <summary>Tiny bridge so the HUD can restart a case without owning seed/config.</summary>
    public static class CaseGameplayBootstrap
    {
        public static System.Action<CaseShiftController> StartCaseHandler;

        public static void RequestStartCase(CaseShiftController controller)
        {
            if (StartCaseHandler != null)
                StartCaseHandler(controller);
        }
    }
}
