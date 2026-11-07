using System;
using System.Collections.Generic;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>
    /// Wires CaseCore stress/truth to Gemini performance and emits text events for an ElevenLabs agent.
    /// Does not implement TTS. Ensures a <see cref="BarkPlayer"/> is present so submit feels instant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterrogationDialogueService : MonoBehaviour
    {
        [Header("Case Wiring")]
        [SerializeField] private CaseShiftController shiftController;
        [SerializeField] private GeminiApiClient geminiClient;
        [SerializeField] private GeminiApiKeySettings apiKeySettings;

        [Header("Instant Reaction")]
        [Tooltip("Plays a short bark on ValidQuestionAsked while Gemini/ElevenLabs load. Added when missing.")]
        [SerializeField] private BarkPlayer barkPlayer;

        [Header("Behaviour")]
        [Tooltip("When no GEMINI_API_KEY is available, answer from code-owned fallback lines.")]
        [SerializeField] private bool allowOfflineMock = true;

        private readonly Dictionary<string, List<string>> historyBySuspect = new Dictionary<string, List<string>>();
        private bool isBusy;

        /// <summary>Raised when a spoken line is ready for ElevenLabs (or any voice consumer).</summary>
        public event Action<SuspectVoiceLine> SuspectLineReady;

        /// <summary>Raised after stress resolution and reply generation (live or mock).</summary>
        public event Action<SuspectDialogueTurn> TurnCompleted;

        /// <summary>
        /// Raised as soon as a new question is accepted (before Gemini returns).
        /// Voice clients should cancel in-flight TTS here.
        /// </summary>
        public event Action QuestionStarted;

        /// <summary>True while a Gemini request is in flight.</summary>
        public bool IsBusy { get { return isBusy; } }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (geminiClient == null)
                geminiClient = GetComponent<GeminiApiClient>();
            if (geminiClient == null)
                geminiClient = gameObject.AddComponent<GeminiApiClient>();
            if (apiKeySettings != null)
                geminiClient.Configure(apiKeySettings);
            EnsureBarkPlayer();
        }

        /// <summary>Optional runtime wiring used by prototype UI bootstrap.</summary>
        public void Configure(CaseShiftController controller, GeminiApiKeySettings settings)
        {
            shiftController = controller;
            apiKeySettings = settings;
            if (geminiClient == null)
                geminiClient = GetComponent<GeminiApiClient>() ?? gameObject.AddComponent<GeminiApiClient>();
            if (apiKeySettings != null)
                geminiClient.Configure(apiKeySettings);
            EnsureBarkPlayer();
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
        /// Asks the current suspect: CaseCore applies stress/truth first, then Gemini performs the line.
        /// </summary>
        public bool AskCurrentSuspect(string question, string evidenceId = null, bool aggressive = false)
        {
            EnsureBarkPlayer();

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

            if (!stressResult.IsValid)
            {
                EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, null);
                return true;
            }

            bool canCallGemini = geminiClient != null && geminiClient.HasApiKey;
            if (!canCallGemini)
            {
                if (!allowOfflineMock)
                {
                    EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, "GEMINI_API_KEY missing.");
                    return true;
                }

                EmitImmediate(suspect, question, evidenceId, stressResult, BuildMockReply(stressResult), true, null);
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
                    if (allowOfflineMock)
                    {
                        EmitTurn(suspect, capturedQuestion, capturedEvidenceId, stressResult, BuildMockReply(stressResult), true, error);
                    }
                    else
                    {
                        EmitTurn(suspect, capturedQuestion, capturedEvidenceId, stressResult, BuildMockReply(stressResult), true, error);
                    }
                });

            return true;
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
