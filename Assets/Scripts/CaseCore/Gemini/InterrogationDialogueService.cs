using System;
using System.Collections.Generic;
using CaseClosed.CaseCore.Dialogue;
using CaseClosed.Networking;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>
    /// Wires CaseCore stress/truth to spoken suspect lines and emits text events for an ElevenLabs agent.
    /// Default path is offline <see cref="ScenarioDialogueClient"/> (no Gemini HTTP / ask proxy).
    /// Gemini remains behind an opt-in feature flag for legacy debugging only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationDialogueService : MonoBehaviour
    {
        [Header("Case Wiring")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private ScenarioDialogueClient scenarioDialogue;
        [SerializeField] private GeminiApiClient geminiClient;
        [SerializeField] private GeminiApiKeySettings apiKeySettings;
        [SerializeField] private ApiProxySettings proxySettings;

        [Header("Instant Reaction")]
        [Tooltip("Plays a short bark on ValidQuestionAsked while voice loads. Added when missing.")]
        [SerializeField] private BarkPlayer barkPlayer;

        [Header("Feature Flags")]
        [Tooltip("Preferred path: local scenario / scripted bank. Default ON. No network for ask turns.")]
        [SerializeField] private bool useScenarioDialogue = true;
        [Tooltip("Legacy Gemini HTTP path (proxy or direct). Default OFF — Play Mode asks work offline.")]
        [SerializeField] private bool useGeminiApi = false;
        [Tooltip("When Gemini is enabled but unavailable, fall back to scripted lines.")]
        [SerializeField] private bool allowOfflineMock = true;

        private readonly Dictionary<string, List<string>> historyBySuspect = new Dictionary<string, List<string>>();
        private bool isBusy;

        /// <summary>Raised when a spoken line is ready for ElevenLabs (or any voice consumer).</summary>
        public event Action<SuspectVoiceLine> SuspectLineReady;

        /// <summary>Raised after stress resolution and reply generation (live or scripted).</summary>
        public event Action<SuspectDialogueTurn> TurnCompleted;

        /// <summary>
        /// Raised as soon as a new question is accepted (before a reply is ready).
        /// Voice clients should cancel in-flight TTS here.
        /// </summary>
        public event Action QuestionStarted;

        /// <summary>True while a Gemini request is in flight (scripted path never blocks).</summary>
        public bool IsBusy { get { return isBusy; } }

        /// <summary>True when ask turns may call Gemini HTTP (opt-in; default false).</summary>
        public bool UseGeminiApi { get { return useGeminiApi; } }

        /// <summary>True when ScenarioDialogueClient / scripted bank is preferred (default true).</summary>
        public bool UseScenarioDialogue { get { return useScenarioDialogue; } }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            EnsureScenarioClient();
            // Do not auto-add GeminiApiClient — ask turns must not depend on the proxy by default.
            if (geminiClient == null)
                geminiClient = GetComponent<GeminiApiClient>();
            if (useGeminiApi && geminiClient != null && apiKeySettings != null)
                geminiClient.Configure(apiKeySettings, proxySettings);
            EnsureBarkPlayer();
        }

        /// <summary>Optional runtime wiring used by prototype UI bootstrap.</summary>
        public void Configure(CaseShiftController controller, GeminiApiKeySettings settings)
        {
            Configure(controller, settings, proxySettings);
        }

        public void Configure(CaseShiftController controller, GeminiApiKeySettings settings, ApiProxySettings proxy)
        {
            shiftController = controller;
            apiKeySettings = settings;
            proxySettings = proxy;
            EnsureScenarioClient();
            if (geminiClient == null)
                geminiClient = GetComponent<GeminiApiClient>();
            if (useGeminiApi && geminiClient != null)
            {
                if (apiKeySettings != null)
                    geminiClient.Configure(apiKeySettings, proxySettings);
                else if (proxySettings != null)
                    geminiClient.Configure(null, proxySettings);
            }
            EnsureBarkPlayer();
        }

        private void EnsureScenarioClient()
        {
            if (scenarioDialogue == null)
                scenarioDialogue = GetComponent<ScenarioDialogueClient>();
            if (scenarioDialogue == null && useScenarioDialogue)
                scenarioDialogue = gameObject.AddComponent<ScenarioDialogueClient>();
        }

        private void EnsureBarkPlayer()
        {
            if (barkPlayer == null)
                barkPlayer = GetComponent<BarkPlayer>();
            if (barkPlayer == null)
                barkPlayer = gameObject.AddComponent<BarkPlayer>();

            InterrogationController interrogation = null;
            if (shiftController != null)
                interrogation = shiftController.GetComponent<InterrogationController>();
            if (interrogation == null)
                interrogation = GetComponent<InterrogationController>();
            if (interrogation != null)
                barkPlayer.Configure(interrogation);
        }

        /// <summary>Clears short per-suspect chat memory when a new shift begins.</summary>
        public void BeginShift()
        {
            historyBySuspect.Clear();
            isBusy = false;
        }

        /// <summary>
        /// Asks the current suspect: CaseCore applies stress/truth first, then scenario/scripted (or optional Gemini) performs the line.
        /// </summary>
        public bool AskCurrentSuspect(string question, string evidenceId = null, bool aggressive = false)
        {
            EnsureBarkPlayer();
            EnsureScenarioClient();

            if (isBusy)
            {
                Debug.LogWarning("InterrogationDialogueService is already waiting on a reply.");
                return false;
            }

            if (shiftController == null || shiftController.ActiveSession == null)
            {
                Debug.LogWarning("No active case shift.");
                return false;
            }

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null)
            {
                Debug.LogWarning("No current suspect to interrogate.");
                return false;
            }

            InterrogationResult stressResult;
            try
            {
                stressResult = shiftController.AskCurrentSuspect(question, evidenceId, aggressive);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AskCurrentSuspect failed: " + ex.Message);
                return false;
            }

            Action started = QuestionStarted;
            if (started != null)
                started.Invoke();

            EvidenceRecord presented = FindCollectedEvidence(evidenceId);
            bool isCulprit = suspect.Id == shiftController.ActiveSession.Case.CulpritSuspectId;

            // Default: scenario / scripted — offline, no ask proxy.
            if (useScenarioDialogue || !useGeminiApi)
            {
                EmitScenarioOrScripted(suspect, question, evidenceId, stressResult, presented, isCulprit);
                return true;
            }

            // Legacy Gemini path (opt-in only).
            bool canCallGemini = geminiClient != null && geminiClient.HasApiKey;
            if (!canCallGemini)
            {
                EmitScenarioOrScripted(
                    suspect,
                    question,
                    evidenceId,
                    stressResult,
                    presented,
                    isCulprit,
                    allowOfflineMock ? null : "GEMINI_API_KEY missing.");
                return true;
            }

            string systemPrompt = InterrogationPromptBuilder.BuildSystemPrompt(
                suspect,
                shiftController.ActiveSession.Case,
                stressResult,
                presented,
                isCulprit);
            string userPrompt = InterrogationPromptBuilder.BuildUserPrompt(question, GetHistory(suspect.Id));

            isBusy = true;
            string capturedQuestion = question;
            string capturedEvidenceId = evidenceId;
            geminiClient.Generate(
                systemPrompt,
                userPrompt,
                modelText =>
                {
                    isBusy = false;
                    GeminiSuspectReply reply;
                    if (!GeminiApiClient.TryParseSuspectReply(modelText, out reply))
                        reply = BuildLegacyMockReply(stressResult);

                    if (!string.IsNullOrWhiteSpace(stressResult.Slip) &&
                        (stressResult.Tier == InterrogationStressTier.Cracking ||
                         stressResult.Tier == InterrogationStressTier.Broken))
                    {
                        reply.slip = true;
                    }

                    EmitTurn(suspect, capturedQuestion, capturedEvidenceId, stressResult, reply, false, null, "gemini");
                },
                error =>
                {
                    isBusy = false;
                    EmitScenarioOrScripted(
                        suspect,
                        capturedQuestion,
                        capturedEvidenceId,
                        stressResult,
                        presented,
                        isCulprit,
                        error);
                });

            return true;
        }

        private void EmitScenarioOrScripted(
            SuspectRecord suspect,
            string question,
            string evidenceId,
            InterrogationResult stressResult,
            EvidenceRecord presented,
            bool isCulprit,
            string error = null)
        {
            GeminiSuspectReply reply;
            string sourceTag = "scripted";

            if (scenarioDialogue != null &&
                scenarioDialogue.TryBuildReply(suspect, question, stressResult, presented, isCulprit, out reply, out sourceTag))
            {
                EmitTurn(suspect, question, evidenceId, stressResult, reply, sourceTag != "suspect", error, sourceTag);
                return;
            }

            EmitTurn(suspect, question, evidenceId, stressResult, BuildLegacyMockReply(stressResult), true, error, "scripted");
        }

        private void EmitTurn(
            SuspectRecord suspect,
            string question,
            string evidenceId,
            InterrogationResult stressResult,
            GeminiSuspectReply reply,
            bool usedMock,
            string error,
            string replySource)
        {
            if (reply == null)
                reply = BuildLegacyMockReply(stressResult);

            if (string.IsNullOrWhiteSpace(replySource))
                replySource = usedMock ? "scripted" : "suspect";

            AppendHistory(suspect.Id, "Detective: " + question);
            AppendHistory(suspect.Id, suspect.DisplayName + ": " + reply.line);

            var turn = new SuspectDialogueTurn
            {
                IsValid = stressResult != null && stressResult.IsValid,
                SuspectId = suspect.Id,
                SuspectName = suspect.DisplayName,
                PlayerQuestion = question,
                EvidenceId = evidenceId ?? string.Empty,
                StressResult = stressResult,
                Reply = reply,
                UsedMock = usedMock,
                ReplySource = replySource,
                Error = error
            };

            var voice = new SuspectVoiceLine
            {
                SuspectId = suspect.Id,
                SuspectName = suspect.DisplayName,
                Text = reply.line,
                Emotion = reply.emotion,
                Slip = reply.slip,
                UsedMock = usedMock,
                VoiceId = suspect.ElevenLabsVoiceId
            };

            SuspectLineReady?.Invoke(voice);
            TurnCompleted?.Invoke(turn);
        }

        private static GeminiSuspectReply BuildLegacyMockReply(InterrogationResult stressResult)
        {
            string emotion = "calm";
            if (stressResult != null)
            {
                switch (stressResult.Tier)
                {
                    case InterrogationStressTier.Nervous:
                        emotion = "nervous";
                        break;
                    case InterrogationStressTier.Cracking:
                        emotion = "scared";
                        break;
                    case InterrogationStressTier.Broken:
                        emotion = "broken";
                        break;
                    default:
                        emotion = "calm";
                        break;
                }
            }

            string line = stressResult != null && !string.IsNullOrWhiteSpace(stressResult.FallbackLine)
                ? stressResult.FallbackLine
                : "I already answered that.";

            if (stressResult != null && !string.IsNullOrWhiteSpace(stressResult.Slip))
                line = stressResult.Slip;

            return GeminiSuspectReply.CreateFallback(line, emotion, stressResult != null && !string.IsNullOrWhiteSpace(stressResult.Slip));
        }

        private EvidenceRecord FindCollectedEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId) || shiftController == null || shiftController.ActiveSession == null)
                return null;

            IReadOnlyList<EvidenceRecord> collected = shiftController.ActiveSession.GetCollectedEvidence();
            for (int i = 0; i < collected.Count; i++)
            {
                if (collected[i] != null && collected[i].Id == evidenceId)
                    return collected[i];
            }

            return null;
        }

        private List<string> GetHistory(string suspectId)
        {
            List<string> history;
            if (!historyBySuspect.TryGetValue(suspectId, out history))
            {
                history = new List<string>(8);
                historyBySuspect[suspectId] = history;
            }

            return history;
        }

        private void AppendHistory(string suspectId, string line)
        {
            List<string> history = GetHistory(suspectId);
            history.Add(line);
            while (history.Count > 8)
                history.RemoveAt(0);
        }
    }
}
