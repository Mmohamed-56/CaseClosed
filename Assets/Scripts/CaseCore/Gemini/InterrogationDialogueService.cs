using System;
using System.Collections.Generic;
using CaseClosed.CaseCore.Dialogue;
using CaseClosed.Networking;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>
    /// Wires CaseCore stress/truth to spoken suspect lines and emits text for ElevenLabs.
    /// Prefers offline <see cref="ScenarioDialogueClient"/>; mock is last resort.
    /// Gemini/Netlify remain in tree but are not preferred (sibling strips Gemini).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationDialogueService : MonoBehaviour
    {
        [Header("Case Wiring")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private ScenarioDialogueClient scenarioClient;
        [SerializeField] private GeminiApiClient geminiClient;
        [SerializeField] private GeminiApiKeySettings apiKeySettings;
        [SerializeField] private ApiProxySettings proxySettings;

        [Header("Instant Reaction")]
        [Tooltip("Plays a short bark on ValidQuestionAsked while TTS loads. Added when missing.")]
        [SerializeField] private BarkPlayer barkPlayer;

        [Header("Behaviour")]
        [Tooltip("Prefer offline scenario bank (default). Disable only for Gemini experiments.")]
        [SerializeField] private bool preferScenarioBank = true;
        [Tooltip("Optional Gemini/Netlify path. Off by default — keep for sibling cleanup.")]
        [SerializeField] private bool allowGeminiFallback = false;
        [Tooltip("When scenario + Gemini unavailable, answer from CaseCore fallback lines.")]
        [SerializeField] private bool allowOfflineMock = true;

        private readonly Dictionary<string, List<string>> historyBySuspect = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, InterrogationResult> lastStressBySuspect = new Dictionary<string, InterrogationResult>();
        private bool isBusy;
        private bool subscribedToStamps;

        /// <summary>Raised when a spoken line is ready for ElevenLabs (or any voice consumer).</summary>
        public event Action<SuspectVoiceLine> SuspectLineReady;

        /// <summary>Raised after stress resolution and reply generation (live or mock).</summary>
        public event Action<SuspectDialogueTurn> TurnCompleted;

        /// <summary>
        /// Raised as soon as a new question is accepted (before a reply returns).
        /// Voice clients should cancel in-flight TTS here.
        /// </summary>
        public event Action QuestionStarted;

        /// <summary>True while a networked Gemini request is in flight (scenario path is sync).</summary>
        public bool IsBusy { get { return isBusy; } }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            EnsureScenarioClient();
            if (geminiClient == null)
                geminiClient = GetComponent<GeminiApiClient>();
            if (allowGeminiFallback && geminiClient == null)
                geminiClient = gameObject.AddComponent<GeminiApiClient>();
            if (geminiClient != null && apiKeySettings != null)
                geminiClient.Configure(apiKeySettings, proxySettings);
            EnsureBarkPlayer();
            SubscribeStampEvents();
        }

        private void OnEnable()
        {
            SubscribeStampEvents();
        }

        private void OnDisable()
        {
            UnsubscribeStampEvents();
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
            if (allowGeminiFallback)
            {
                if (geminiClient == null)
                    geminiClient = GetComponent<GeminiApiClient>() ?? gameObject.AddComponent<GeminiApiClient>();
                if (apiKeySettings != null)
                    geminiClient.Configure(apiKeySettings, proxySettings);
                else if (proxySettings != null)
                    geminiClient.Configure(null, proxySettings);
            }
            EnsureBarkPlayer();
            SubscribeStampEvents();
        }

        private void EnsureScenarioClient()
        {
            if (scenarioClient == null)
                scenarioClient = GetComponent<ScenarioDialogueClient>();
            if (scenarioClient == null)
                scenarioClient = gameObject.AddComponent<ScenarioDialogueClient>();
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

        private void SubscribeStampEvents()
        {
            if (subscribedToStamps || shiftController == null) return;
            shiftController.SuspectStamped += HandleSuspectStamped;
            subscribedToStamps = true;
        }

        private void UnsubscribeStampEvents()
        {
            if (!subscribedToStamps || shiftController == null) return;
            shiftController.SuspectStamped -= HandleSuspectStamped;
            subscribedToStamps = false;
        }

        /// <summary>Clears short per-suspect chat memory when a new shift begins.</summary>
        public void BeginShift()
        {
            historyBySuspect.Clear();
            lastStressBySuspect.Clear();
            isBusy = false;
        }

        /// <summary>
        /// Asks the current suspect: CaseCore applies stress/truth first, then the dialogue tree performs the line.
        /// Always emits <see cref="SuspectLineReady"/> so ElevenLabsVoiceSink can Speak.
        /// </summary>
        public bool AskCurrentSuspect(string question, string evidenceId = null, bool aggressive = false)
        {
            return AskCurrentSuspect(question, evidenceId, aggressive, null);
        }

        /// <param name="questionId">Tree question id from the ACTIONS choice list (preferred over free text).</param>
        public bool AskCurrentSuspect(string question, string evidenceId, bool aggressive, string questionId)
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

            lastStressBySuspect[suspect.Id] = stressResult;

            Action started = QuestionStarted;
            if (started != null)
                started.Invoke();

            EvidenceRecord presented = FindCollectedEvidence(evidenceId);
            bool isCulprit = suspect.Id == shiftController.ActiveSession.Case.CulpritSuspectId;

            if (!stressResult.IsValid)
            {
                EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, null);
                return true;
            }

            // 1) Offline evidence-driven dialogue trees (preferred)
            if (preferScenarioBank && scenarioClient != null && scenarioClient.IsReady)
            {
                var ctx = new ScenarioAskContext
                {
                    SuspectId = suspect.Id,
                    SuspectName = suspect.DisplayName,
                    Question = question,
                    QuestionId = questionId,
                    EvidenceId = evidenceId,
                    HasEvidence = presented != null,
                    Aggressive = aggressive,
                    IsCulprit = isCulprit,
                    IsShutdown = stressResult.IsShutdown,
                    StressResult = stressResult
                };

                GeminiSuspectReply scenarioReply;
                if (scenarioClient.TryResolve(ctx, out scenarioReply))
                {
                    if (!string.IsNullOrWhiteSpace(stressResult.Slip) &&
                        (stressResult.Tier == InterrogationStressTier.Cracking ||
                         stressResult.Tier == InterrogationStressTier.Broken))
                    {
                        scenarioReply.slip = true;
                    }

                    EmitImmediate(suspect, question, evidenceId, stressResult, scenarioReply, false, null);
                    return true;
                }
            }

            // 2) Optional Gemini (kept for sibling cleanup; off by default)
            bool canCallGemini = allowGeminiFallback && geminiClient != null && geminiClient.HasApiKey;
            if (canCallGemini)
            {
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
                            reply = BuildMockReply(stressResult);

                        if (!string.IsNullOrWhiteSpace(stressResult.Slip) &&
                            (stressResult.Tier == InterrogationStressTier.Cracking ||
                             stressResult.Tier == InterrogationStressTier.Broken))
                        {
                            reply.slip = true;
                        }

                        EmitTurn(suspect, capturedQuestion, capturedEvidenceId, stressResult, reply, false, null);
                    },
                    error =>
                    {
                        isBusy = false;
                        EmitTurn(suspect, capturedQuestion, capturedEvidenceId, stressResult, BuildMockReply(stressResult), true, error);
                    });

                return true;
            }

            // 3) Mock last resort
            if (!allowOfflineMock)
            {
                EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, "No scenario bank and Gemini disabled.");
                return true;
            }

            EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, null);
            return true;
        }

        private void HandleSuspectStamped(SuspectStampEvent stamp)
        {
            if (stamp == null || shiftController == null || shiftController.ActiveSession == null)
                return;

            EnsureScenarioClient();
            if (scenarioClient == null || !scenarioClient.IsReady)
                return;

            SuspectRecord stamped = null;
            IReadOnlyList<SuspectRecord> suspects = shiftController.ActiveSession.Case.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                if (suspects[i] != null && suspects[i].Id == stamp.SuspectId)
                {
                    stamped = suspects[i];
                    break;
                }
            }

            if (stamped == null) return;

            InterrogationResult lastStress;
            lastStressBySuspect.TryGetValue(stamped.Id, out lastStress);
            bool isCulprit = stamped.Id == shiftController.ActiveSession.Case.CulpritSuspectId;

            GeminiSuspectReply reply;
            if (!scenarioClient.TryResolveAftermath(stamped, stamp.Decision, lastStress, isCulprit, out reply))
                return;

            Action started = QuestionStarted;
            if (started != null)
                started.Invoke();

            string label = stamp.Decision == SuspectDecision.Hold ? "HOLD" : "RELEASE";
            EmitTurn(stamped, label, null, lastStress ?? new InterrogationResult { IsValid = true, FallbackLine = reply.line }, reply, false, null);
        }

        private void EmitImmediate(
            SuspectRecord suspect,
            string question,
            string evidenceId,
            InterrogationResult stressResult,
            GeminiSuspectReply reply,
            bool usedMock,
            string error)
        {
            EmitTurn(suspect, question, evidenceId, stressResult, reply, usedMock, error);
        }

        private void EmitTurn(
            SuspectRecord suspect,
            string question,
            string evidenceId,
            InterrogationResult stressResult,
            GeminiSuspectReply reply,
            bool usedMock,
            string error)
        {
            if (reply == null)
                reply = BuildMockReply(stressResult);

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
                ReplySource = usedMock ? "scripted" : "suspect",
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

        private static GeminiSuspectReply BuildMockReply(InterrogationResult stressResult)
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
