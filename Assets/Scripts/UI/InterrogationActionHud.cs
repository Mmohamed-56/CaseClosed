using System.Collections.Generic;
using CaseClosed.CameraControl;
using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Dialogue;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Player desk HUD with top-level tags: ACTIONS (interview) | CASE FILE (briefing) | DECISION (HOLD/RELEASE).
    /// CASE FILE does not pick evidence — collect via the computer / public records → right EVIDENCE board.
    /// Builds a Screen Space Overlay canvas at runtime when Bezi has not assigned scene refs.
    /// Interview: present a collected record → pick a tree question → reply via
    /// <see cref="InterrogationDialogueService"/> (SuspectLineReady → ElevenLabs).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationActionHud : MonoBehaviour
    {
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
        [SerializeField] private ScenarioDialogueClient scenarioClient;
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
        /// <summary>One Text for the whole pad — never stack per-line Body children (VLH+CSF overlapped for Magdy).</summary>
        private Text transcriptBodyText;

        private Button tagActions;
        private Button tagCaseFile;
        private Button tagDecision;
        private GameObject askRowRoot;
        private Text askHintText;
        private GameObject stampFooterRoot;
        private Button holdFooterButton;
        private Button releaseFooterButton;

        private SuspectEnterController enterController;
        private SuspectEnterShiftBinder enterBinder;
        private MonitorFocusController monitorFocus;
        private bool waitingForSeat;

        /// <summary>True while the desk file panel is visible.</summary>
        public bool IsOpen { get { return panelOpen; } }

        /// <summary>True when this HUD owns HOLD/RELEASE on DECISION (skip duplicate stamp overlay; evidence board still boots).</summary>
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

            if (scenarioClient == null)
                scenarioClient = GetComponent<ScenarioDialogueClient>();
            if (scenarioClient == null)
                scenarioClient = FindAnyObjectByType<ScenarioDialogueClient>();
            if (scenarioClient == null && dialogueService != null)
                scenarioClient = dialogueService.GetComponent<ScenarioDialogueClient>();

            if (evidenceBoard == null)
                evidenceBoard = FindAnyObjectByType<EvidenceBoardController>();
            if (stampPad == null)
                stampPad = FindAnyObjectByType<StampPadController>();

            enterController = FindAnyObjectByType<SuspectEnterController>();
            enterBinder = FindAnyObjectByType<SuspectEnterShiftBinder>();
            monitorFocus = FindAnyObjectByType<MonitorFocusController>();

            // Crisp Overlay HUD even when Bezi assigned a scene canvas without pixelPerfect.
            if (hudCanvas != null)
            {
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.pixelPerfect = true;
            }

            EnsureEventSystem();
            if ((hudCanvas == null || panelRoot == null) && buildRuntimeUiIfMissing)
                BuildRuntimeUi();

            // Bed1dd1 hotfix still left VLH Body stacks; normalize as soon as the pad exists.
            if (transcriptRoot != null)
                EnsureSingleTranscriptBody();

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
            {
                evidenceBoard.EvidenceSelectionChanged += OnBoardSelectionChanged;
                evidenceBoard.ActiveTabChanged += OnEvidenceTabChanged;
            }
            if (shiftController != null)
                shiftController.NextSuspectReady += OnNextSuspectReady;
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
                shiftController.NextSuspectReady -= OnNextSuspectReady;
            }
            if (evidenceBoard != null)
            {
                evidenceBoard.EvidenceSelectionChanged -= OnBoardSelectionChanged;
                evidenceBoard.ActiveTabChanged -= OnEvidenceTabChanged;
            }
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
                ? "Open public records (computer) → collect ≥2 → ACTIONS to call the suspect in."
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

        private void OnEvidenceTabChanged(string suspectId)
        {
            // Board already cleared present selection; keep HUD present id in sync.
            selectedEvidenceId = string.Empty;
            RefreshAskRow();
            RefreshStatus();
        }

        private void OnNextSuspectReady(NextSuspectEvent next)
        {
            selectedEvidenceId = string.Empty;
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

        private void AskTreeQuestion(DialogueQuestionChoice choice)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.Prompt))
                return;
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                return;

            string evidenceId = ResolveSelectedEvidenceId();
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                statusMessage = "Present a collected record first.";
                RefreshStatus();
                return;
            }

            string ask = choice.Prompt.Trim();
            AppendTranscript("You: " + ask);

            awaitingReply = true;
            statusMessage = "Waiting on the suspect…";
            RefreshAll();

            // Emits SuspectLineReady → ElevenLabsVoiceSink → Speak (same path as before).
            bool started = dialogueService != null &&
                           dialogueService.AskCurrentSuspect(ask, evidenceId, choice.Aggressive, choice.Id);
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
            statusMessage = "Zoomed to desk computer. Collect ≥" + MinEvidenceToCallIn + " — filed cards land on the EVIDENCE board.";
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

            statusMessage = "Interview open. Present a record, then pick a question.";
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
                // ACTIONS header shows the seated suspect so it matches the transcript log name.
                SuspectRecord current = shiftController != null ? shiftController.CurrentSuspect : null;
                switch (activeTag)
                {
                    case HudTag.Actions:
                        headerTitleText.text = current != null && !string.IsNullOrWhiteSpace(current.DisplayName)
                            ? current.DisplayName
                            : "ACTIONS";
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
            DeskUiPalette.StyleFolderTag(button, active);
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

            if (askHintText == null) return;

            if (!interviewing)
            {
                askHintText.text = string.Empty;
                return;
            }

            string evidenceId = ResolveSelectedEvidenceId();
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                askHintText.text = "Suspect is answering…";
            else if (string.IsNullOrWhiteSpace(evidenceId))
                askHintText.text = "Present a collected record, then pick a question below.";
            else
                askHintText.text = "Presenting · " + FindEvidenceTitle(evidenceId) + " — pick a question.";
        }

        private void RefreshStampFooter()
        {
            bool show = panelOpen &&
                        activeTag == HudTag.Decision &&
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

            EnsureSingleTranscriptBody();

            bool show = panelOpen && activeTag == HudTag.Actions;
            if (!show || transcript.Count == 0)
            {
                if (transcriptBodyText != null)
                    transcriptBodyText.text = string.Empty;
                transcriptRoot.gameObject.SetActive(show && transcript.Count > 0);
                return;
            }

            transcriptRoot.gameObject.SetActive(true);
            // Rewrite one Text — no per-line child spawn (hotfix DestroyImmediate still left VLH overlap).
            transcriptBodyText.text = string.Join("\n", transcript);
        }

        /// <summary>
        /// Transcript pad must be a single wrapped Text. Older builds spawned one Body Text per line under a
        /// VerticalLayoutGroup + ContentSizeFitter, which stacked/overlapped even after DestroyImmediate clears.
        /// </summary>
        private void EnsureSingleTranscriptBody()
        {
            if (transcriptRoot == null) return;

            // Drop VLH so AccentStripe stretch + one body Text are not layout-fought.
            VerticalLayoutGroup vlg = transcriptRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
                DestroyImmediate(vlg);
            ContentSizeFitter rootFit = transcriptRoot.GetComponent<ContentSizeFitter>();
            if (rootFit != null)
                DestroyImmediate(rootFit);

            if (transcriptBodyText == null)
            {
                Transform existing = transcriptRoot.Find("TranscriptBody");
                if (existing != null)
                    transcriptBodyText = existing.GetComponent<Text>();
            }

            // Nuclear purge: every Text under the pad except one TranscriptBody (bed1dd1 left Body stacks).
            Text[] texts = transcriptRoot.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text t = texts[i];
                if (t == null) continue;
                if (transcriptBodyText != null && t == transcriptBodyText) continue;
                DestroyImmediate(t.gameObject);
            }

            for (int i = transcriptRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = transcriptRoot.GetChild(i);
                if (child == null) continue;

                if (child.name == "AccentStripe" || child.name == "FolderFrame")
                {
                    LayoutElement chromeLayout = child.GetComponent<LayoutElement>();
                    if (chromeLayout == null)
                        chromeLayout = child.gameObject.AddComponent<LayoutElement>();
                    chromeLayout.ignoreLayout = true;
                    continue;
                }

                if (transcriptBodyText != null && child == transcriptBodyText.transform)
                    continue;

                DestroyImmediate(child.gameObject);
            }

            if (transcriptBodyText == null)
            {
                transcriptBodyText = CreateLabel(
                    transcriptRoot,
                    "TranscriptBody",
                    string.Empty,
                    15,
                    DeskUiPalette.InkMuted,
                    FontStyle.Normal);
                DeskUiPalette.StyleLabel(
                    transcriptBodyText,
                    15,
                    DeskUiPalette.InkMuted,
                    DeskUiPalette.TypeRole.Body,
                    FontStyle.Normal);
            }

            // Fill the pad; no CSF/LayoutElement — those fought VLH and collapsed line heights.
            ContentSizeFitter fit = transcriptBodyText.GetComponent<ContentSizeFitter>();
            if (fit != null)
                DestroyImmediate(fit);
            LayoutElement bodyLayout = transcriptBodyText.GetComponent<LayoutElement>();
            if (bodyLayout != null)
                DestroyImmediate(bodyLayout);

            Stretch(transcriptBodyText.rectTransform, 14f, 6f);
            transcriptBodyText.alignment = TextAnchor.UpperLeft;
            transcriptBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            transcriptBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            transcriptBodyText.raycastTarget = false;
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
            }

            sb.AppendLine();
            sb.AppendLine("EVIDENCE");
            sb.AppendLine("Filed on the board: " + collectedIds.Count + " (need ≥" + MinEvidenceToCallIn + " before call-in).");
            sb.AppendLine("Use the desk computer to collect — cards appear on the right EVIDENCE clipboard (per-suspect tabs).");

            SetContentBody(sb.ToString().TrimEnd());

            if ((shiftController.Phase == CaseShiftPhase.Briefing || shiftController.Phase == CaseShiftPhase.Investigating) &&
                HasInGamePublicRecordsPath())
            {
                AddAction("Open public records (computer)", OpenPublicRecords);
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
                        " in.\nFiled so far: " + filed + ".\nCollect from the desk computer — watch the right EVIDENCE board.");
                    AddAction("Open CASE FILE", () =>
                    {
                        activeTag = HudTag.CaseFile;
                        RefreshAll();
                    });
                    if (HasInGamePublicRecordsPath())
                        AddAction("Open public records (computer)", OpenPublicRecords);
                    return;
                }

                if (waitingForSeat)
                {
                    SetContentBody(suspect.DisplayName + " is walking in… interview unlocks when seated.");
                    return;
                }

                SetContentBody(
                    "Clipboard ready (" + filed + " records).\nCall " + suspect.DisplayName +
                    " in to sit and talk — then present a record and pick a question.");
                AddAction("Call in — " + suspect.DisplayName, CallSuspectIn);
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.AppendLine("INTERVIEW — " + suspect.DisplayName);
            sb.AppendLine("Questions remaining: " + shiftController.GetQuestionsLeft(suspect.Id));
            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                sb.AppendLine("Suspect is answering…");
            else
                sb.AppendLine("1) Present a record  2) Pick a question choice");
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

            if (string.IsNullOrWhiteSpace(evidenceId))
                return;

            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
                return;

            EnsureScenarioClient();
            List<DialogueQuestionChoice> choices = scenarioClient != null
                ? scenarioClient.GetQuestionChoices(suspect.Id, evidenceId)
                : null;

            if (choices == null || choices.Count == 0)
            {
                AddAction("(No tree questions for this record)", null, false);
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                DialogueQuestionChoice choice = choices[i];
                if (choice == null || string.IsNullOrWhiteSpace(choice.Prompt)) continue;
                DialogueQuestionChoice captured = choice;
                string label = (choice.Aggressive ? "Press · " : "Ask · ") + choice.Prompt;
                AddAction(label, () => AskTreeQuestion(captured), true, DeskUiPalette.Navy, DeskUiPalette.White);
            }
        }

        private void EnsureScenarioClient()
        {
            if (scenarioClient != null) return;
            scenarioClient = GetComponent<ScenarioDialogueClient>();
            if (scenarioClient == null)
                scenarioClient = FindAnyObjectByType<ScenarioDialogueClient>();
            if (scenarioClient == null && dialogueService != null)
                scenarioClient = dialogueService.GetComponent<ScenarioDialogueClient>();
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
                SetContentBody("Call the suspect in from ACTIONS, finish the interview, then stamp HOLD / RELEASE here on DECISION.");
                return;
            }

            if (shiftController.Phase == CaseShiftPhase.Investigating)
            {
                SuspectRecord suspect = shiftController.CurrentSuspect;
                SetContentBody(suspect == null
                    ? "Stamp HOLD or RELEASE below, then return for the final charge when the roster is done."
                    : "Stamp HOLD or RELEASE for " + suspect.DisplayName + ".\nFinal charge unlocks after the roster is decided.");
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
            bool tall = label.Length > 42;
            LayoutElement layout = go.GetComponent<LayoutElement>();
            layout.minHeight = tall ? 52f : 34f;
            layout.preferredHeight = tall ? 52f : 34f;

            bool isStampAction = fill == DeskUiPalette.StampHold || fill == DeskUiPalette.StampRelease || fill == DeskUiPalette.Charge;
            DeskUiPalette.AddAccentStripe(go.transform, isStampAction ? fill : DeskUiPalette.Hairline, isStampAction ? 5f : 3f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(labelGo.GetComponent<RectTransform>(), 14f, 4f);

            Button button = go.GetComponent<Button>();
            if (isStampAction)
                DeskUiPalette.StyleStampButton(button, image, text, fill, true);
            else
                DeskUiPalette.StyleFormRow(button, image, text, fill, textColor, interactable && onClick != null);
            if (tall)
                text.fontSize = 12;
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
            // Stay Overlay — Camera / World Space downscales and softens the manila HUD.
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.pixelPerfect = true;
            hudCanvas.sortingOrder = 55;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Prefer height so the left desk file is not soft-scaled on widescreen.
            scaler.matchWidthOrHeight = 1f;

            // Manila folder tabs — kraft stock + stamped type (not navy SaaS pills).
            GameObject tags = new GameObject("TopTags", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tags.transform.SetParent(canvasGo.transform, false);
            RectTransform tagsRect = tags.GetComponent<RectTransform>();
            tagsRect.anchorMin = new Vector2(0f, 1f);
            tagsRect.anchorMax = new Vector2(0f, 1f);
            tagsRect.pivot = new Vector2(0f, 1f);
            tagsRect.anchoredPosition = new Vector2(12f, -10f);
            tagsRect.sizeDelta = new Vector2(448f, 34f);
            HorizontalLayoutGroup tagLayout = tags.GetComponent<HorizontalLayoutGroup>();
            tagLayout.spacing = 3f;
            tagLayout.padding = new RectOffset(4, 4, 0, 0);
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
            DeskUiPalette.ApplyFill(panelImage, DeskUiPalette.Manila);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.offsetMin = new Vector2(12f, 12f);
            panelRect.offsetMax = new Vector2(460f, -48f);
            DeskUiPalette.AddFolderFrame(panel.transform, 2f, 2f);

            GameObject header = CreateBox(panel.transform, "Header", DeskUiPalette.NavyDark);
            DeskUiPalette.ApplyFill(header.GetComponent<Image>(), DeskUiPalette.NavyDark);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(-8f, 58f);
            headerRect.anchoredPosition = new Vector2(0f, -4f);

            Text agency = CreateLabel(header.transform, "Agency", "UB CAMPUS POLICE  ·  DESK FILE", 10, DeskUiPalette.AccentOlive, FontStyle.Bold);
            DeskUiPalette.StyleLabel(agency, 10, DeskUiPalette.AccentOlive, DeskUiPalette.TypeRole.Stamp, FontStyle.Bold, TextAnchor.UpperLeft);
            AnchorTopLeft(agency.rectTransform, 12f, -5f, 320f, 14f);

            headerTitleText = CreateLabel(header.transform, "Title", "CASE FILE", 20, DeskUiPalette.White, FontStyle.Bold);
            DeskUiPalette.StyleLabel(headerTitleText, 20, DeskUiPalette.White, DeskUiPalette.TypeRole.Stamp, FontStyle.Bold, TextAnchor.UpperLeft);
            AnchorTopLeft(headerTitleText.rectTransform, 12f, -18f, 280f, 24f);
            headerCaseText = CreateLabel(header.transform, "Case", string.Empty, 13, DeskUiPalette.ManilaLight, FontStyle.Normal);
            DeskUiPalette.StyleLabel(headerCaseText, 13, DeskUiPalette.ManilaLight, DeskUiPalette.TypeRole.Body, FontStyle.Italic, TextAnchor.UpperLeft);
            AnchorTopLeft(headerCaseText.rectTransform, 12f, -38f, 340f, 16f);
            DeskUiPalette.AddHairline(header.transform, "HeaderRule", 56f, 2f, false);

            closeButton = CreateChromeButton(header.transform, "Close", "FILE", new Vector2(-12f, -14f), new Vector2(64f, 28f), DeskUiPalette.Navy, DeskUiPalette.White, TextAnchor.UpperRight, stampOutline: true);

            // Content scroll — room for ask row / stamp footer depending on tag.
            GameObject scrollGo = new GameObject("ContentScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            DeskUiPalette.ApplyFill(scrollBg, DeskUiPalette.ManilaLight);
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10f, 200f);
            scrollRect.offsetMax = new Vector2(-10f, -68f);
            DeskUiPalette.AddHairline(scrollGo.transform, "ContentTopRule", 0f, 1.5f, true);
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

            // Hint row (ACTIONS only) — choices live in the content list; no type-in InputField.
            GameObject askRow = new GameObject("AskRow", typeof(RectTransform), typeof(Image));
            askRow.transform.SetParent(panel.transform, false);
            askRowRoot = askRow;
            DeskUiPalette.ApplyFill(askRow.GetComponent<Image>(), DeskUiPalette.Blotter);
            RectTransform askRect = askRow.GetComponent<RectTransform>();
            askRect.anchorMin = new Vector2(0f, 0f);
            askRect.anchorMax = new Vector2(1f, 0f);
            askRect.pivot = new Vector2(0.5f, 0f);
            askRect.offsetMin = new Vector2(4f, 148f);
            askRect.offsetMax = new Vector2(-4f, 200f);
            DeskUiPalette.AddHairline(askRow.transform, "AskTopRule", 0f, 1.5f, false);

            askHintText = CreateLabel(askRow.transform, "AskHint", "Present a record, then pick a question.", 13, DeskUiPalette.ManilaLight, FontStyle.Normal);
            DeskUiPalette.StyleLabel(askHintText, 13, DeskUiPalette.ManilaLight, DeskUiPalette.TypeRole.Body, FontStyle.Italic, TextAnchor.MiddleLeft);
            Stretch(askHintText.rectTransform, 12f, 8f);
            askHintText.alignment = TextAnchor.MiddleLeft;
            askHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Stamp footer (visibility owned by RefreshStampFooter — Futaba may use DECISION).
            GameObject stampFooter = new GameObject("StampFooter", typeof(RectTransform), typeof(Image));
            stampFooter.transform.SetParent(panel.transform, false);
            stampFooterRoot = stampFooter;
            DeskUiPalette.ApplyFill(stampFooter.GetComponent<Image>(), DeskUiPalette.Blotter);
            RectTransform stampRect = stampFooter.GetComponent<RectTransform>();
            stampRect.anchorMin = new Vector2(0f, 0f);
            stampRect.anchorMax = new Vector2(1f, 0f);
            stampRect.pivot = new Vector2(0.5f, 0f);
            stampRect.offsetMin = new Vector2(4f, 42f);
            stampRect.offsetMax = new Vector2(-4f, 100f);
            DeskUiPalette.AddHairline(stampFooter.transform, "StampTopRule", 0f, 1.5f, false);

            holdFooterButton = CreateChromeButton(stampFooter.transform, "Hold", "HOLD", new Vector2(18f, -4f), new Vector2(180f, 38f), DeskUiPalette.StampHold, DeskUiPalette.White, TextAnchor.MiddleLeft, stampOutline: true);
            releaseFooterButton = CreateChromeButton(stampFooter.transform, "Release", "RELEASE", new Vector2(-18f, -4f), new Vector2(180f, 38f), DeskUiPalette.StampRelease, DeskUiPalette.White, TextAnchor.MiddleRight, stampOutline: true);

            // Transcript (ACTIONS) — single Text pad (no VLH / per-line Body children).
            GameObject transcriptGo = new GameObject("Transcript", typeof(RectTransform), typeof(Image));
            transcriptGo.transform.SetParent(panel.transform, false);
            DeskUiPalette.ApplyFill(transcriptGo.GetComponent<Image>(), DeskUiPalette.TranscriptBg);
            DeskUiPalette.AddAccentStripe(transcriptGo.transform, DeskUiPalette.StampHold, 3f);
            RectTransform tr = transcriptGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(10f, 42f);
            tr.offsetMax = new Vector2(-10f, 142f);
            Transform stripe = transcriptGo.transform.Find("AccentStripe");
            if (stripe != null)
            {
                LayoutElement stripeLayout = stripe.GetComponent<LayoutElement>();
                if (stripeLayout == null)
                    stripeLayout = stripe.gameObject.AddComponent<LayoutElement>();
                stripeLayout.ignoreLayout = true;
            }
            transcriptRoot = transcriptGo.transform;
            EnsureSingleTranscriptBody();

            // Status — carbon strip
            GameObject statusGo = new GameObject("Status", typeof(RectTransform), typeof(Image));
            statusGo.transform.SetParent(panel.transform, false);
            DeskUiPalette.ApplyFill(statusGo.GetComponent<Image>(), DeskUiPalette.NavyDark);
            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-8f, 34f);
            statusRect.anchoredPosition = new Vector2(0f, 4f);
            DeskUiPalette.AddHairline(statusGo.transform, "StatusTopRule", 0f, 2f, false);
            statusText = CreateLabel(statusGo.transform, "StatusText", string.Empty, 11, DeskUiPalette.ManilaLight, FontStyle.Normal);
            DeskUiPalette.StyleLabel(statusText, 11, DeskUiPalette.ManilaLight, DeskUiPalette.TypeRole.Stamp, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(statusText.rectTransform, 10f, 4f);
            statusText.alignment = TextAnchor.MiddleLeft;
        }

        private Button CreateTopTag(Transform parent, string label)
        {
            GameObject go = new GameObject("Tag_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minWidth = 120f;
            Text text = CreateLabel(go.transform, "Label", label, 12, DeskUiPalette.Ink, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, 4f, 2f);
            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleFolderTag(button, false);
            return button;
        }

        private Button CreateChromeButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color fill, Color textColor, TextAnchor anchor, bool stampOutline = false)
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
            Text text = CreateLabel(go.transform, "Label", label, 14, textColor, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, 4f, 2f);
            Button button = go.GetComponent<Button>();
            if (stampOutline)
            {
                bool filled = name == "Close";
                DeskUiPalette.StyleStampButton(button, image, text, fill, filled);
                if (name == "Hold" || name == "Release")
                    DeskUiPalette.AddFolderFrame(go.transform, 2f, 2f);
            }
            else
            {
                DeskUiPalette.StyleButton(button, image, text, fill, textColor);
            }
            return button;
        }

        private static GameObject CreateBox(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            DeskUiPalette.ApplyFill(go.GetComponent<Image>(), color);
            return go;
        }

        private Text CreateLabel(Transform parent, string name, string value, int size, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            DeskUiPalette.StyleLabel(text, size, color, DeskUiPalette.TypeRole.Ui, style);
            text.text = value ?? string.Empty;
            return text;
        }

        private Text CreateBodyText(Transform parent, string value, int size, Color color)
        {
            Text text = CreateLabel(parent, "Body", value, size, color, FontStyle.Normal);
            DeskUiPalette.StyleLabel(text, size, color, DeskUiPalette.TypeRole.Body, FontStyle.Normal);
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
