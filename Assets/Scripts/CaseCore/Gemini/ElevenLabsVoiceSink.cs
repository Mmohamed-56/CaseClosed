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
    /// Bridges Mikasa's Gemini dialogue events to the ElevenLabs TTS client.
    /// Listens to <c>SuspectLineReady</c> and <c>QuestionStarted</c> on
    /// <see cref="InterrogationDialogueService"/>, cancels in-flight audio on a new question,
    /// and republishes on <see cref="SuspectDialogueEvents.ReplyReady"/> for
    /// <see cref="ElevenLabsVoiceClient"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevenLabsVoiceSink : MonoBehaviour, ISuspectVoiceSink
    {
        [SerializeField] private InterrogationDialogueService dialogueService;
        [SerializeField] private ElevenLabsVoiceClient voiceClient;
        [Tooltip("When enabled, logs each spoken line (useful without an API key).")]
        [SerializeField] private bool logLines;

        private void Awake()
        {
            if (dialogueService == null)
                dialogueService = GetComponent<InterrogationDialogueService>();
            if (voiceClient == null)
                voiceClient = GetComponent<ElevenLabsVoiceClient>();
            if (voiceClient == null)
                voiceClient = gameObject.AddComponent<ElevenLabsVoiceClient>();
        }

        private void OnEnable()
        {
            if (dialogueService != null)
            {
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

        private void HandleQuestionStarted()
        {
            if (voiceClient != null)
                voiceClient.CancelInFlight();
        }

        /// <summary>Consumes Gemini (or mock) spoken text and starts ElevenLabs playback via the shared bus.</summary>
        public void HandleSuspectLine(SuspectVoiceLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Text))
                return;

            if (logLines)
                Debug.Log("[ElevenLabsVoiceSink] TTS: " + line.SuspectName + " / " + line.Emotion + " → " + line.Text);

            // ElevenLabsVoiceClient subscribes to SuspectDialogueEvents.ReplyReady.
            SuspectDialogueEvents.PublishReply(line.SuspectId, line.Text, line.Emotion, line.VoiceId);
        }
    }
}
