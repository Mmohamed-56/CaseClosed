using CaseClosed.Networking;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>
    /// Interface for ElevenLabs TTS consumers. Prefer attaching
    /// <see cref="ElevenLabsVoiceSink"/> which forwards to <see cref="ElevenLabsVoiceClient"/>.
    /// </summary>
    public interface ISuspectVoiceSink
    {
        void HandleSuspectLine(SuspectVoiceLine line);
    }

    /// <summary>
    /// Bridges dialogue events to the ElevenLabs TTS client.
    /// Listens to <c>SuspectLineReady</c> and <c>QuestionStarted</c> on
    /// <see cref="InterrogationDialogueService"/>, cancels in-flight audio on a new question,
    /// and republishes on <see cref="SuspectDialogueEvents.ReplyReady"/> for
    /// <see cref="ElevenLabsVoiceClient"/>.
    /// Choice-tree / Asuka answers can call <see cref="SpeakAnswer"/> directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevenLabsVoiceSink : MonoBehaviour, ISuspectVoiceSink
    {
        [SerializeField] private InterrogationDialogueService dialogueService;
        [SerializeField] private ElevenLabsVoiceClient voiceClient;
        [SerializeField] private ApiProxySettings proxySettings;
        [Tooltip("When enabled, logs each spoken line (useful without an API key).")]
        [SerializeField] private bool logLines;

        private void Awake()
        {
            EnsureWired();
        }

        private void OnEnable()
        {
            EnsureWired();
            if (dialogueService != null)
            {
                dialogueService.SuspectLineReady -= HandleSuspectLine;
                dialogueService.QuestionStarted -= HandleQuestionStarted;
                dialogueService.SuspectLineReady += HandleSuspectLine;
                dialogueService.QuestionStarted += HandleQuestionStarted;
            }
        }

        private void OnDisable()
        {
            if (dialogueService != null)
            {
                dialogueService.SuspectLineReady -= HandleSuspectLine;
                dialogueService.QuestionStarted -= HandleQuestionStarted;
            }
        }

        /// <summary>Runtime wiring used by <see cref="CaseClosed.Gameplay.CaseGameplayPrototype"/>.</summary>
        public void Configure(InterrogationDialogueService service, ApiProxySettings proxy = null)
        {
            if (dialogueService != null && dialogueService != service)
            {
                dialogueService.SuspectLineReady -= HandleSuspectLine;
                dialogueService.QuestionStarted -= HandleQuestionStarted;
            }

            dialogueService = service;
            if (proxy != null)
                proxySettings = proxy;

            EnsureWired();

            if (isActiveAndEnabled && dialogueService != null)
            {
                dialogueService.SuspectLineReady -= HandleSuspectLine;
                dialogueService.QuestionStarted -= HandleQuestionStarted;
                dialogueService.SuspectLineReady += HandleSuspectLine;
                dialogueService.QuestionStarted += HandleQuestionStarted;
            }
        }

        private void HandleQuestionStarted()
        {
            if (voiceClient != null)
                voiceClient.CancelInFlight();
        }

        /// <summary>
        /// Speak an authored reply from any path: scenario bank, scripted mock, or choice-tree (Asuka).
        /// Always publishes <see cref="SuspectDialogueEvents.ReplyReady"/> so the voice client Speaks.
        /// When <paramref name="voiceId"/> is empty, Speak resolves male/female/neutral from
        /// <see cref="SuspectVoiceCatalog"/> for the current suspect id.
        /// </summary>
        public void SpeakAnswer(string suspectId, string text, string emotion = null, string voiceId = null)
        {
            string resolvedVoice = voiceId;
            if (string.IsNullOrWhiteSpace(resolvedVoice))
                resolvedVoice = SuspectVoiceCatalog.Resolve(suspectId);

            HandleSuspectLine(new SuspectVoiceLine
            {
                SuspectId = suspectId ?? string.Empty,
                SuspectName = string.Empty,
                Text = text,
                Emotion = emotion,
                VoiceId = resolvedVoice,
                UsedMock = false,
                Slip = false
            });
        }

        /// <summary>Consumes Gemini / scenario / scripted spoken text and starts ElevenLabs playback.</summary>
        public void HandleSuspectLine(SuspectVoiceLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Text))
            {
                if (logLines)
                    Debug.LogWarning("[ElevenLabsVoiceSink] Ignored a null or blank spoken line.");
                return;
            }

            EnsureWired();

            if (logLines)
                Debug.Log("[ElevenLabsVoiceSink] Forwarding spoken line for suspect " +
                          (line.SuspectId ?? string.Empty) + " (" + line.Text.Length + " characters) to the voice event bus.");

            // ElevenLabsVoiceClient subscribes to ReplyReady in OnEnable and Speaks.
            // Choice-tree (Asuka) answers should call SpeakAnswer / HandleSuspectLine so they hit this same bus.
            SuspectDialogueEvents.PublishReply(line.SuspectId, line.Text, line.Emotion, line.VoiceId);
        }

        private void EnsureWired()
        {
            if (dialogueService == null)
                dialogueService = GetComponent<InterrogationDialogueService>();
            if (dialogueService == null)
                dialogueService = FindFirstObjectByType<InterrogationDialogueService>();

            if (voiceClient == null)
                voiceClient = GetComponent<ElevenLabsVoiceClient>();
            if (voiceClient == null)
                voiceClient = gameObject.AddComponent<ElevenLabsVoiceClient>();

            if (proxySettings != null)
                voiceClient.Configure(proxySettings);
        }
    }
}
