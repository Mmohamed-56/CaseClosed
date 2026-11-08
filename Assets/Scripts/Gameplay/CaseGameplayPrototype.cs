using System.Collections.Generic;
using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using UnityEngine;

namespace CaseClosed.Gameplay
{
    /// <summary>Runs a minimal first-run case loop through a compact immediate-mode prototype interface.</summary>
    [DisallowMultipleComponent]
    public sealed class CaseGameplayPrototype : MonoBehaviour
    {
        [Header("Case Setup")]
        [Tooltip("Stable seed for reproducing the initial two-suspect generated case.")]
        [SerializeField] private int caseSeed = 42;
        [Tooltip("Number of suspects in this prototype case; the initial case uses two suspects.")]
        [SerializeField, Range(2, 4)] private int suspectCount = 2;

        [Header("Case Controller")]
        [Tooltip("Case state and scoring controller. Added automatically when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Gemini Interrogation")]
        [Tooltip("Optional key settings asset. Env/PlayerPrefs still win when set.")]
        [SerializeField] private GeminiApiKeySettings geminiApiKeySettings;
        [Tooltip("Dialogue bridge: stress from CaseCore, lines from Gemini (or mock).")]
        [SerializeField] private InterrogationDialogueService dialogueService;

        [Header("Voice")]
        [Tooltip("Bridges SuspectLineReady → ElevenLabs. Added automatically when empty.")]
        [SerializeField] private ElevenLabsVoiceSink voiceSink;

        private string questionText = "";
        private string feedbackText = "";
        private string selectedEvidenceId = "";
        private Vector2 scrollPosition;
        private CaseResolution resolution;
        private readonly HashSet<string> collectedIds = new HashSet<string>();
        private GUIStyle wrappedLabelStyle;
        private bool actionsOpen;
        private int activeTab;
        private bool awaitingReply;

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = gameObject.AddComponent<CaseShiftController>();

            if (dialogueService == null)
                dialogueService = GetComponent<InterrogationDialogueService>();
            if (dialogueService == null)
                dialogueService = gameObject.AddComponent<InterrogationDialogueService>();

            if (voiceSink == null)
                voiceSink = GetComponent<ElevenLabsVoiceSink>();
            if (voiceSink == null)
                voiceSink = gameObject.AddComponent<ElevenLabsVoiceSink>();

            dialogueService.Configure(shiftController, geminiApiKeySettings);
            dialogueService.TurnCompleted += OnDialogueTurnCompleted;
        }

        private void OnDestroy()
        {
            if (dialogueService == null) return;
            dialogueService.TurnCompleted -= OnDialogueTurnCompleted;
        }

        private void Start()
        {
            StartCase();
        }

        /// <summary>Starts or restarts the deterministic prototype case.</summary>
        public void StartCase()
        {
            resolution = null;
            feedbackText = "Case ready. Review public records before confronting either suspect.";
            questionText = "";
            selectedEvidenceId = "";
            collectedIds.Clear();
            awaitingReply = false;
            if (dialogueService != null) dialogueService.BeginShift();
            shiftController.BeginGeneratedShift(caseSeed, suspectCount);
        }

        private void OnGUI()
        {
            if (wrappedLabelStyle == null)
                wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

            if (GUI.Button(new Rect(12f, 12f, 112f, 34f), "ACTIONS"))
                actionsOpen = !actionsOpen;

            if (!actionsOpen)
                return;

            float width = Mathf.Min(Screen.width - 24f, 460f);
            float height = Mathf.Min(Screen.height - 64f, 560f);
            Rect panelRect = new Rect(12f, 54f, width, height);
            GUI.Box(panelRect, "");
            GUILayout.BeginArea(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, panelRect.height - 20f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("CASE CLOSED", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.Width(64f)))
                actionsOpen = false;
            GUILayout.EndHorizontal();

            string[] tabs = { "Case", "Evidence", "Interview", "Decision" };
            activeTab = GUILayout.Toolbar(activeTab, tabs);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (shiftController == null || shiftController.ActiveSession == null)
            {
                GUILayout.Label("No active case.");
                if (GUILayout.Button("Start case")) StartCase();
            }
            else
            {
                DrawActiveTab();
                GUILayout.Space(10f);
                if (!string.IsNullOrEmpty(feedbackText))
                    GUILayout.Label(feedbackText, wrappedLabelStyle, GUILayout.ExpandWidth(true));
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawActiveTab()
        {
            CaseShiftPhase phase = shiftController.Phase;
            if (resolution != null || phase == CaseShiftPhase.Headline || phase == CaseShiftPhase.Resolved)
            {
                if (resolution == null)
                    resolution = shiftController.LastResolution;
                if (activeTab == 3)
                    DrawResolution();
                else
                    GUILayout.Label("Morning headline ready. Open Decision to read it.", wrappedLabelStyle);
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
                    DrawInterviewTab();
                    break;
                case 3:
                    DrawDecision();
                    break;
            }
        }

        private void DrawCase()
        {
            CaseFile caseFile = shiftController.ActiveSession.Case;
            GUILayout.Label(caseFile.Title);
            GUILayout.Label("Case " + caseFile.CaseId + " | " + caseFile.Suspects.Count + " suspects | fixed seed " + caseSeed);
            GUILayout.Space(6f);
            GUILayout.Label("BRIEFING", GUI.skin.box);
            GUILayout.Label(caseFile.Briefing, wrappedLabelStyle);

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null) return;
            GUILayout.Space(6f);
            GUILayout.Label("CURRENT SUSPECT", GUI.skin.box);
            GUILayout.Label(suspect.DisplayName + " — " + suspect.CampusRole);
            GUILayout.Label("Statement: " + suspect.Statement, wrappedLabelStyle);
            GUILayout.Label("Public record / alibi: " + suspect.Alibi, wrappedLabelStyle);
        }

        private void DrawEvidence()
        {
            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null)
            {
                GUILayout.Label("No current suspect.");
                return;
            }

            GUILayout.Label("PUBLIC RECORDS", GUI.skin.box);
            GUILayout.Label("Review and collect records before beginning the interview.", wrappedLabelStyle);
            for (int i = 0; i < suspect.Evidence.Count; i++)
            {
                EvidenceRecord evidence = suspect.Evidence[i];
                GUILayout.Label(evidence.Title, GUI.skin.label);
                GUILayout.Label(evidence.Description, wrappedLabelStyle);
                bool collected = collectedIds.Contains(evidence.Id);
                if (shiftController.Phase == CaseShiftPhase.Briefing)
                {
                    if (GUILayout.Button(collected ? "Collected: " + evidence.Title : "Collect: " + evidence.Title))
                    {
                        bool added = shiftController.CollectEvidence(evidence.Id);
                        if (added) collectedIds.Add(evidence.Id);
                        feedbackText = added ? "Collected " + evidence.Title + "." : "That record was already collected.";
                    }
                }
                else
                {
                    GUILayout.Label(collected ? "Collected before interview" : "Not collected before interview");
                }
                GUILayout.Space(4f);
            }
        }

        private void DrawInterviewTab()
        {
            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null)
            {
                GUILayout.Label("No current suspect.");
                return;
            }

            if (shiftController.Phase == CaseShiftPhase.Briefing)
            {
                GUILayout.Label("INTERVIEW", GUI.skin.box);
                GUILayout.Label("Collect any public records you need, then begin the interview.", wrappedLabelStyle);
                if (GUILayout.Button("Begin interview with " + suspect.DisplayName))
                {
                    shiftController.BeginCurrentInterview();
                    feedbackText = "Interview started. Ask a typed question; question budget and stress are handled by the case controller.";
                }
                return;
            }

            DrawInterview(suspect);
        }

        private void DrawInterview(SuspectRecord suspect)
        {
            GUILayout.Space(6f);
            GUILayout.Label("INTERVIEW", GUI.skin.box);
            GUILayout.Label("Questions remaining: " + shiftController.GetQuestionsLeft(suspect.Id));
            GUILayout.Label("Type a question (up to 180 characters):");
            questionText = GUILayout.TextField(questionText, 180);
            GUILayout.Label("Optional evidence to present:");
            if (GUILayout.Button(string.IsNullOrEmpty(selectedEvidenceId) ? "No evidence selected" : "Selected: " + FindEvidenceTitle(selectedEvidenceId)))
                selectedEvidenceId = "";

            IReadOnlyList<EvidenceRecord> collected = shiftController.ActiveSession.GetCollectedEvidence();
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord evidence = collected[i];
                if (GUILayout.Button((selectedEvidenceId == evidence.Id ? "Selected: " : "Present: ") + evidence.Title))
                    selectedEvidenceId = evidence.Id;
            }

            if (awaitingReply || (dialogueService != null && dialogueService.IsBusy))
            {
                GUILayout.Label("Suspect is answering...");
            }
            else if (GUILayout.Button("Ask question"))
            {
                string ask = questionText;
                string evidence = selectedEvidenceId;
                questionText = "";
                awaitingReply = true;
                feedbackText = "Sending question to Gemini (or mock if no key)...";
                bool started = dialogueService != null && dialogueService.AskCurrentSuspect(ask, evidence);
                if (!started)
                {
                    awaitingReply = false;
                    feedbackText = "Could not ask that question right now.";
                }
            }
        }

        private void OnDialogueTurnCompleted(SuspectDialogueTurn turn)
        {
            awaitingReply = false;
            if (turn == null || turn.StressResult == null)
            {
                feedbackText = "No reply.";
                return;
            }

            InterrogationResult result = turn.StressResult;
            string line = turn.Reply != null ? turn.Reply.line : result.FallbackLine;
            string emotion = turn.Reply != null ? turn.Reply.emotion : "calm";
            string source = turn.UsedMock ? "mock" : "gemini";
            feedbackText = "[" + source + "] " + turn.SuspectName + " (" + emotion + ") | stress " +
                           result.Stress + "/100 (" + result.Tier + ") | questions left " + result.QuestionsLeft +
                           "\n" + line;
            if (!string.IsNullOrEmpty(turn.Error))
                feedbackText += "\n(note: " + turn.Error + ")";
        }

        private void DrawDecision()
        {
            if (shiftController.Phase == CaseShiftPhase.Charging)
            {
                DrawCharging();
                return;
            }

            SuspectRecord suspect = shiftController.CurrentSuspect;
            GUILayout.Label("SUSPECT DECISION", GUI.skin.box);
            if (shiftController.Phase == CaseShiftPhase.Briefing)
            {
                GUILayout.Label("Begin the interview before deciding whether to hold or release this suspect.", wrappedLabelStyle);
                return;
            }

            if (suspect == null)
            {
                GUILayout.Label("No current suspect decision is available.");
                return;
            }

            GUILayout.Label("Review your interview, then choose whether to hold or release " + suspect.DisplayName + ".", wrappedLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HOLD"))
            {
                shiftController.StampHold();
                feedbackText = "HOLD stamped. Next suspect or charge.";
                selectedEvidenceId = "";
            }
            if (GUILayout.Button("RELEASE"))
            {
                shiftController.StampRelease();
                feedbackText = "RELEASE stamped. Next suspect or charge.";
                selectedEvidenceId = "";
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCharging()
        {
            CaseSession session = shiftController.ActiveSession;
            GUILayout.Label(session.Case.Title);
            GUILayout.Label("FINAL CHARGE", GUI.skin.box);
            GUILayout.Label("Choose a held suspect, or charge nobody. The culprit is not displayed before resolution.", wrappedLabelStyle);
            System.Collections.Generic.IReadOnlyList<SuspectRecord> held = shiftController.GetHeldSuspects();
            for (int i = 0; i < held.Count; i++)
            {
                SuspectRecord suspect = held[i];
                if (GUILayout.Button("Charge " + suspect.DisplayName))
                    Resolve(suspect.Id);
            }
            if (GUILayout.Button("Charge nobody"))
            {
                shiftController.ChargeNobody();
                resolution = shiftController.LastResolution;
            }
        }

        private void DrawResolution()
        {
            GUILayout.Label("MORNING HEADLINE", GUI.skin.box);
            if (!string.IsNullOrEmpty(resolution.Headline))
                GUILayout.Label(resolution.Headline, wrappedLabelStyle);
            if (!string.IsNullOrEmpty(resolution.Subheadline))
                GUILayout.Label(resolution.Subheadline, wrappedLabelStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Outcome: " + resolution.Outcome);
            GUILayout.Label("Charged: " + (string.IsNullOrEmpty(resolution.ChargedSuspectName) ? "Nobody" : resolution.ChargedSuspectName));
            GUILayout.Label("Score: " + resolution.Score + " | innocent suspects held: " + resolution.InnocentSuspectsHeld);
            GUILayout.Label("Questions remaining: " + resolution.QuestionsRemaining);
            if (GUILayout.Button("Restart deterministic case")) StartCase();
        }

        private string FindEvidenceTitle(string evidenceId)
        {
            IReadOnlyList<EvidenceRecord> evidence = shiftController.ActiveSession.GetCollectedEvidence();
            for (int i = 0; i < evidence.Count; i++)
                if (evidence[i].Id == evidenceId) return evidence[i].Title;
            return "none";
        }

        private void Resolve(string suspectId)
        {
            resolution = shiftController.ResolveCharge(suspectId);
        }
    }
}
