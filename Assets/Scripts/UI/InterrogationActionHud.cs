using System.Collections.Generic;
using CaseClosed.CameraControl;
using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Player desk HUD with top-level tags: ACTIONS (interview) | CASE FILE (records + stamps) | DECISION.
    /// Builds a Screen Space Overlay canvas at runtime when Bezi has not assigned scene refs.
    /// Ask turns go through <see cref="InterrogationDialogueService"/> → ScenarioDialogueClient (offline bank).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationActionHud : MonoBehaviour
    {
        private const int MaxQuestionChars = 180;
        private const int MaxTranscriptLines = 40;
        private const int MinEvidenceToCallIn = 2;

        private enum HudTag
        {
            Actions = 0,
            CaseFile = 1,
            Decision = 2
        }

        [Header("Case Connection")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private InterrogationDialogueService dialogueService;
        [SerializeField] private EvidenceBoardController evidenceBoard;
        [SerializeField] private StampPadController stampPad;

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
        private HudTag activeTag = HudTag.CaseFile;
        private string selectedEvidenceId = string.Empty;
        private string statusMessage = string.Empty;
        private bool awaitingReply;
        private CaseResolution resolution;
        private bool runtimeBuilt;
        private Font uiFont;

        private Text runtimeContentLabel;
        private readonly List<Button> runtimeActionButtons = new List<Button>();
        private readonly List<Text> runtimeTranscriptLabels = new List<Text>();

        private Button tagActions;
        private Button tagCaseFile;
        private Button tagDecision;
        private GameObject askRowRoot;
        private GameObject stampFooterRoot;
        private Button holdFooterButton;
        private Button releaseFooterButton;

        private SuspectEnterController enterController;
        private SuspectEnterShiftBinder enterBinder;
        private MonitorFocusController monitorFocus;
        private bool waitingForSeat;

        /// <summary>True while the desk file panel is visible.</summary>
        public bool IsOpen { get { return panelOpen; } }

        /// <summary>True when this HUD owns Case File + stamp chrome (Aqua overlay should stay off).</summary>
        public bool OwnsCaseFileChrome { get { return true; } }

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
            if (stampPad == null)
                stampPad = FindAnyObjectByType<StampPadController>();

            enterController = FindAnyObjectByType<SuspectEnterController>();
            enterBinder = FindAnyObjectByType<SuspectEnterShiftBinder>();
            monitorFocus = FindAnyObjectByType<MonitorFocusController>();

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
            if (enterController != null)
                enterController.Seated += OnSuspectSeated;
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
            if (enterController != null)
                enterController.Seated -= OnSuspectSeated;
            waitingForSeat = false;
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
            waitingForSeat = false;
            selectedEvidenceId = string.Empty;
            collectedIds.Clear();
            transcript.Clear();
            statusMessage = string.IsNullOrWhiteSpace(readyMessage)
                ? "Open CASE FILE → collect ≥2 records → ACTIONS to call the suspect in."
                : readyMessage;
            activeTag = HudTag.CaseFile;
            SyncCollectedFromSession();
            RefreshAll();
        }

        /// <summary>Opens or closes the desk file panel.</summary>
        public void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (panelRoot != null)
                panelRoot.SetActive(open);
            if (open)
                RefreshAll();
            else
                RefreshTags();
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
                // Legacy single open button — hide when split tags exist.
                if (tagActions != null)
                    openButton.gameObject.SetActive(false);
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
            if (holdFooterButton != null)
            {
                holdFooterButton.onClick.RemoveAllListeners();
                holdFooterButton.onClick.AddListener(StampHold);
            }
            if (releaseFooterButton != null)
            {
                releaseFooterButton.onClick.RemoveAllListeners();
                releaseFooterButton.onClick.AddListener(StampRelease);
            }

            WireTag(tagActions, HudTag.Actions);
            WireTag(tagCaseFile, HudTag.CaseFile);
            WireTag(tagDecision, HudTag.Decision);

            if (tabButtons != null && tabButtons.Length >= 3)
            {
                WireTag(tabButtons[0], HudTag.Actions);
                WireTag(tabButtons[1], HudTag.CaseFile);
                WireTag(tabButtons[2], HudTag.Decision);
            }
        }

        private void WireTag(Button button, HudTag tag)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectTag(tag));
        }

        private void ClosePanel()
        {
            SetPanelOpen(false);
        }

        private void SelectTag(HudTag tag)
        {
            if (panelOpen && activeTag == tag)
            {
                SetPanelOpen(false);
                return;
            }

            activeTag = tag;
            SetPanelOpen(true);
        }

        private void OnPhaseChanged(CaseShiftPhase phase)
        {
            if (phase == CaseShiftPhase.Headline || phase == CaseShiftPhase.Resolved)
                resolution = shiftController != null ? shiftController.LastResolution : resolution;
            if (phase == CaseShiftPhase.Charging)
            {
                activeTag = HudTag.Decision;
                SetPanelOpen(true);
            }
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
            string source = string.IsNullOrWhiteSpace(turn.ReplySource)
                ? (turn.UsedMock ? "scripted" : "suspect")
                : turn.ReplySource.Trim();
            statusMessage = "[" + source + "] " + name + " — stress " + result.Stress + "/100 · " + result.QuestionsLeft + " left";
            if (!string.IsNullOrEmpty(turn.Error))
                statusMessage += " · " + turn.Error;
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

        private void StampHold()
        {
            if (stampPad != null && stampPad.CanStamp)
                stampPad.StampHold();
            else if (shiftController != null)
                shiftController.StampHold();

            waitingForSeat = false;
            statusMessage = "HOLD stamped.";
            selectedEvidenceId = string.Empty;
            if (evidenceBoard != null) evidenceBoard.ClearSelection();
            RefreshAll();
        }

        private void StampRelease()
        {
            if (stampPad != null && stampPad.CanStamp)
                stampPad.StampRelease();
            else if (shiftController != null)
                shiftController.StampRelease();

            waitingForSeat = false;
            statusMessage = "RELEASE stamped.";
            selectedEvidenceId = string.Empty;
            if (evidenceBoard != null) evidenceBoard.ClearSelection();
            RefreshAll();
        }

        /// <summary>Zooms the player camera onto the desk monitor / CEF records browser. Never opens an OS browser tab.</summary>
        private void OpenPublicRecords()
        {
            activeTag = HudTag.CaseFile;
            if (monitorFocus == null)
                monitorFocus = FindAnyObjectByType<MonitorFocusController>();

            if (monitorFocus == null || !monitorFocus.TryEnterFocus())
            {
                statusMessage = "Desk computer unavailable — zoom to the monitor yourself (click screen / F).";
                RefreshAll();
                return;
            }

            SetPanelOpen(false);
            statusMessage = "Zoomed to desk computer. Collect ≥" + MinEvidenceToCallIn + " on the CASE FILE clipboard.";
            RefreshStatus();
        }

        /// <summary>True when SampleScene can zoom the camera onto the desk monitor. No path ⇒ hide the HUD button.</summary>
        private bool HasInGamePublicRecordsPath()
        {
            if (monitorFocus == null)
                monitorFocus = FindAnyObjectByType<MonitorFocusController>();
            return monitorFocus != null && monitorFocus.CanEnterFocus;
        }

        private void CallSuspectIn()
        {
            if (shiftController == null || shiftController.CurrentSuspect == null)
                return;
            if (collectedIds.Count < MinEvidenceToCallIn)
            {
                statusMessage = "Collect ≥" + MinEvidenceToCallIn + " records first.";
                RefreshStatus();
                return;
            }

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (enterController == null)
                enterController = FindAnyObjectByType<SuspectEnterController>();
            if (enterBinder == null)
                enterBinder = FindAnyObjectByType<SuspectEnterShiftBinder>();

            bool needsEnter = enterController != null &&
                              enterController.Stage != SuspectEnterStage.Seated;

            if (needsEnter)
            {
                waitingForSeat = true;
                statusMessage = "Calling " + suspect.DisplayName + " in…";
                if (enterBinder != null)
                    enterBinder.PlayEnterCurrentSuspect();
                else
                    enterController.PlayEnter(suspect.Id);

                // Anchors missing / enter no-op → don't softlock the prototype loop.
                if (enterController.Stage == SuspectEnterStage.Idle)
                {
                    waitingForSeat = false;
                    BeginInterviewNow();
                    return;
                }

                RefreshAll();
                return;
            }

            BeginInterviewNow();
        }

        private void OnSuspectSeated()
        {
            if (!waitingForSeat)
                return;
            waitingForSeat = false;
            BeginInterviewNow();
        }

        private void BeginInterviewNow()
        {
            if (shiftController == null)
                return;
            if (shiftController.Phase != CaseShiftPhase.Briefing &&
                shiftController.Phase != CaseShiftPhase.Investigating)
                return;

            try
            {
                if (shiftController.Phase == CaseShiftPhase.Briefing)
                    shiftController.BeginCurrentInterview();
            }
            catch (System.Exception ex)
            {
                statusMessage = "Could not start interview: " + ex.Message;
                RefreshAll();
                return;
            }

            statusMessage = "Interview open. Type a question and press ASK.";
            activeTag = HudTag.Actions;
            RefreshAll();
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
            RefreshTags();
            RefreshContent();
            RefreshTranscript();
            RefreshStatus();
            RefreshAskRow();
            RefreshStampFooter();
        }

        private void RefreshChrome()
        {
            CaseFile caseFile = shiftController != null && shiftController.ActiveSession != null
                ? shiftController.ActiveSession.Case
                : null;

            if (headerTitleText != null)
            {
                switch (activeTag)
                {
                    case HudTag.Actions:
                        headerTitleText.text = "ACTIONS";
                        break;
                    case HudTag.Decision:
                        headerTitleText.text = "DECISION";
                        break;
                    default:
                        headerTitleText.text = "CASE FILE";
                        break;
                }
            }

            if (headerCaseText != null)
            {
                if (caseFile == null || string.IsNullOrWhiteSpace(caseFile.Title))
                    headerCaseText.text = "No active case";
                else
                    headerCaseText.text = caseFile.Title + (string.IsNullOrWhiteSpace(caseFile.CaseId) ? string.Empty : "  ·  " + caseFile.CaseId);
            }
        }

        private void RefreshTags()
        {
            SetTagVisual(tagActions, panelOpen && activeTag == HudTag.Actions);
            SetTagVisual(tagCaseFile, panelOpen && activeTag == HudTag.CaseFile);
            SetTagVisual(tagDecision, panelOpen && activeTag == HudTag.Decision);
        }

        private static void SetTagVisual(Button button, bool active)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? DeskUiPalette.TabActive : DeskUiPalette.Navy;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = active ? DeskUiPalette.Ink : DeskUiPalette.White;
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
                                activeTag == HudTag.Actions &&
                                panelOpen &&
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

        private void RefreshStampFooter()
        {
            bool show = panelOpen &&
                        activeTag == HudTag.CaseFile &&
                        shiftController != null &&
                        shiftController.Phase == CaseShiftPhase.Investigating &&
                        shiftController.CurrentSuspect != null &&
                        resolution == null;

            if (stampFooterRoot != null)
                stampFooterRoot.SetActive(show);

            bool can = show && (stampPad == null || stampPad.CanStamp);
            if (holdFooterButton != null)
                holdFooterButton.interactable = can;
            if (releaseFooterButton != null)
                releaseFooterButton.interactable = can;
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

            bool show = panelOpen && activeTag == HudTag.Actions;
            if (!show || transcript.Count == 0)
            {
                transcriptRoot.gameObject.SetActive(show && transcript.Count > 0);
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

            if (!panelOpen)
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
                if (activeTag == HudTag.Decision)
                    DrawResolution();
                else
                    SetContentBody("Morning edition is ready. Open DECISION for the headline.");
                return;
            }

            switch (activeTag)
            {
                case HudTag.Actions:
                    DrawInterview();
                    break;
                case HudTag.Decision:
                    DrawDecision();
                    break;
                default:
                    DrawCaseFile();
                    break;
            }
        }

        private void DrawCaseFile()
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

                sb.AppendLine();
                sb.AppendLine("RECORDS");
                sb.AppendLine("Clipboard: " + collectedIds.Count + " filed (need ≥" + MinEvidenceToCallIn + " before call-in).");
                sb.AppendLine(shiftController.Phase == CaseShiftPhase.Briefing
                    ? "Collect case records, then open ACTIONS to call the suspect in."
                    : "Collected records stay on the clipboard.");
            }

            SetContentBody(sb.ToString().TrimEnd());

            if ((shiftController.Phase == CaseShiftPhase.Briefing || shiftController.Phase == CaseShiftPhase.Investigating) &&
                HasInGamePublicRecordsPath())
            {
                AddAction("Open public records", OpenPublicRecords);
            }

            if (suspect == null || suspect.Evidence == null || suspect.Evidence.Count == 0)
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
                    ? "On clipboard · " + evidence.Title
                    : "Collect · " + evidence.Title;

                if ((shiftController.Phase == CaseShiftPhase.Briefing ||
                     shiftController.Phase == CaseShiftPhase.Investigating) && !collected)
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
                int filed = collectedIds.Count;
                if (filed < MinEvidenceToCallIn)
                {
                    SetContentBody(
                        "Need ≥" + MinEvidenceToCallIn + " records before calling " + suspect.DisplayName +
                        " in.\nFiled so far: " + filed + ".\nOpen CASE FILE to collect, or zoom to the desk computer.");
                    AddAction("Open CASE FILE", () =>
                    {
                        activeTag = HudTag.CaseFile;
                        RefreshAll();
                    });
                    if (HasInGamePublicRecordsPath())
                        AddAction("Open public records", OpenPublicRecords);
                    return;
                }

                if (waitingForSeat)
                {
                    SetContentBody(suspect.DisplayName + " is walking in… interview unlocks when seated.");
                    return;
                }

                SetContentBody(
                    "Clipboard ready (" + filed + " records).\nCall " + suspect.DisplayName +
                    " in to sit and talk — then ask via the offline dialogue bank.");
                AddAction("Call in — " + suspect.DisplayName, CallSuspectIn);
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.AppendLine("INTERVIEW — " + suspect.DisplayName);
            sb.AppendLine("Questions remaining: " + shiftController.GetQuestionsLeft(suspect.Id));
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                sb.AppendLine("Suspect is answering…");
            else
                sb.AppendLine("Ask a question. Tap a record below to present it once.");
            SetContentBody(sb.ToString().TrimEnd());

            string evidenceId = ResolveSelectedEvidenceId();
            IReadOnlyList<EvidenceRecord> collected = shiftController.GetCollectedEvidence();
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord evidence = collected[i];
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.Title))
                    continue;
                string id = evidence.Id;
                bool selected = id == evidenceId;
                // Single present state on the button — no separate "Selected:" line.
                AddAction((selected ? "Presenting · " : "Present · ") + evidence.Title, () =>
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

                    statusMessage = string.IsNullOrEmpty(selectedEvidenceId)
                        ? "Nothing presented."
                        : "Presenting · " + FindEvidenceTitle(selectedEvidenceId);
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

            if (shiftController.Phase == CaseShiftPhase.Briefing)
            {
                SetContentBody("Call the suspect in from ACTIONS, finish the interview, then stamp HOLD / RELEASE on the CASE FILE footer.");
                return;
            }

            if (shiftController.Phase == CaseShiftPhase.Investigating)
            {
                SuspectRecord suspect = shiftController.CurrentSuspect;
                SetContentBody(suspect == null
                    ? "Stamp HOLD or RELEASE from CASE FILE, then return here for the final charge."
                    : "Stamp HOLD or RELEASE on CASE FILE for " + suspect.DisplayName + ".\nFinal charge unlocks after the roster is decided.");
                return;
            }

            SetContentBody("No decision pending.");
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
                    activeTag = HudTag.Decision;
                    RefreshAll();
                }, true, DeskUiPalette.Charge, DeskUiPalette.White);
            }

            AddAction("Charge nobody", () =>
            {
                shiftController.ChargeNobody();
                resolution = shiftController.LastResolution;
                statusMessage = "No charge filed.";
                activeTag = HudTag.Decision;
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
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.GetComponent<Text>();
            DeskUiPalette.StyleLabel(text, 14, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(labelGo.GetComponent<RectTransform>(), 10f, 4f);

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
            hudCanvas.sortingOrder = 55;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Top-level tags sit side-by-side (not buried inside the panel).
            GameObject tags = new GameObject("TopTags", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tags.transform.SetParent(canvasGo.transform, false);
            RectTransform tagsRect = tags.GetComponent<RectTransform>();
            tagsRect.anchorMin = new Vector2(0f, 1f);
            tagsRect.anchorMax = new Vector2(0f, 1f);
            tagsRect.pivot = new Vector2(0f, 1f);
            tagsRect.anchoredPosition = new Vector2(12f, -12f);
            tagsRect.sizeDelta = new Vector2(430f, 40f);
            HorizontalLayoutGroup tagLayout = tags.GetComponent<HorizontalLayoutGroup>();
            tagLayout.spacing = 6f;
            tagLayout.childForceExpandWidth = true;
            tagLayout.childForceExpandHeight = true;
            tagLayout.childControlWidth = true;
            tagLayout.childControlHeight = true;

            tagActions = CreateTopTag(tags.transform, "ACTIONS");
            tagCaseFile = CreateTopTag(tags.transform, "CASE FILE");
            tagDecision = CreateTopTag(tags.transform, "DECISION");
            tabButtons = new[] { tagActions, tagCaseFile, tagDecision };

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
            panelRect.offsetMax = new Vector2(460f, -60f);

            GameObject header = CreateBox(panel.transform, "Header", DeskUiPalette.Navy);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 52f);
            headerRect.anchoredPosition = Vector2.zero;

            headerTitleText = CreateLabel(header.transform, "Title", "CASE FILE", 17, DeskUiPalette.White, FontStyle.Bold);
            AnchorTopLeft(headerTitleText.rectTransform, 14f, -8f, 260f, 22f);
            headerCaseText = CreateLabel(header.transform, "Case", string.Empty, 12, DeskUiPalette.ManilaLight, FontStyle.Normal);
            AnchorTopLeft(headerCaseText.rectTransform, 14f, -28f, 340f, 18f);

            closeButton = CreateChromeButton(header.transform, "Close", "CLOSE", new Vector2(-10f, -10f), new Vector2(72f, 28f), DeskUiPalette.NavyDark, DeskUiPalette.White, TextAnchor.UpperRight);

            // Content scroll — room for ask row / stamp footer depending on tag.
            GameObject scrollGo = new GameObject("ContentScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = DeskUiPalette.ManilaLight;
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10f, 200f);
            scrollRect.offsetMax = new Vector2(-10f, -60f);
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
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 10f;
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

            // Ask row (ACTIONS only)
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

            // Stamp footer (CASE FILE only)
            GameObject stampFooter = new GameObject("StampFooter", typeof(RectTransform), typeof(Image));
            stampFooter.transform.SetParent(panel.transform, false);
            stampFooterRoot = stampFooter;
            stampFooter.GetComponent<Image>().color = DeskUiPalette.Blotter;
            RectTransform stampRect = stampFooter.GetComponent<RectTransform>();
            stampRect.anchorMin = new Vector2(0f, 0f);
            stampRect.anchorMax = new Vector2(1f, 0f);
            stampRect.pivot = new Vector2(0.5f, 0f);
            stampRect.offsetMin = new Vector2(0f, 42f);
            stampRect.offsetMax = new Vector2(0f, 100f);

            holdFooterButton = CreateChromeButton(stampFooter.transform, "Hold", "HOLD", new Vector2(18f, 0f), new Vector2(180f, 40f), DeskUiPalette.StampHold, DeskUiPalette.White, TextAnchor.MiddleLeft);
            releaseFooterButton = CreateChromeButton(stampFooter.transform, "Release", "RELEASE", new Vector2(-18f, 0f), new Vector2(180f, 40f), DeskUiPalette.StampRelease, DeskUiPalette.White, TextAnchor.MiddleRight);

            // Transcript (ACTIONS)
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
            tlg.padding = new RectOffset(8, 8, 6, 6);
            tlg.spacing = 3f;
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

        private Button CreateTopTag(Transform parent, string label)
        {
            GameObject go = new GameObject("Tag_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = DeskUiPalette.Navy;
            Text text = CreateLabel(go.transform, "Label", label, 13, DeskUiPalette.White, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleButton(button, image, text, DeskUiPalette.Navy, DeskUiPalette.White);
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
                case TextAnchor.MiddleLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
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
