using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Networking;
using CaseClosed.UI;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.Gameplay
{
    /// <summary>
    /// Boots the first-run prototype loop and wires offline dialogue / voice / desk HUD.
    /// Ask path: CaseCore stress → <see cref="InterrogationDialogueService"/> → ScenarioDialogueClient bank.
    /// Voice: SuspectLineReady → <see cref="ElevenLabsVoiceSink"/> → ElevenLabs via Netlify /api/tts.
    /// Visual interrogation UI lives on <see cref="InterrogationActionHud"/> (UGUI).
    /// Runs before <see cref="StampUiPlayableBootstrap"/> so the HUD can claim Case File chrome.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class CaseGameplayPrototype : MonoBehaviour
    {
        [Header("Case Setup")]
        [Tooltip("Base seed. Each Start / Restart advances by one so case titles rotate through the template pack.")]
        [SerializeField] private int caseSeed = 42;
        [Tooltip("Number of suspects in this prototype case; the initial case uses two suspects.")]
        [SerializeField, Range(2, 4)] private int suspectCount = 2;

        [Header("Case Controller")]
        [Tooltip("Case state and scoring controller. Added automatically when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        /// <summary>How many times <see cref="StartCase"/> has run this session (0 = first shift).</summary>
        private int shiftSerial;

        [Header("Dialogue (offline bank preferred)")]
        [Tooltip("Optional legacy Gemini key settings. Unused while allowGeminiFallback is OFF.")]
        [SerializeField] private GeminiApiKeySettings geminiApiKeySettings;
        [Tooltip("Cloud proxy for ElevenLabs TTS (Netlify). Assign Assets/Settings/ApiProxySettings.")]
        [SerializeField] private ApiProxySettings apiProxySettings;
        [Tooltip("Dialogue bridge: stress from CaseCore, spoken lines from ScenarioDialogueClient.")]
        [SerializeField] private InterrogationDialogueService dialogueService;

        [Header("Voice")]
        [Tooltip("Bridges SuspectLineReady → ElevenLabs. Added automatically when empty.")]
        [SerializeField] private ElevenLabsVoiceSink voiceSink;
        [SerializeField] private ElevenLabsVoiceClient voiceClient;

        [Header("Desk HUD")]
        [Tooltip("UGUI desk file panel. Added automatically when empty.")]
        [SerializeField] private InterrogationActionHud actionHud;

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

            if (voiceClient == null)
                voiceClient = GetComponent<ElevenLabsVoiceClient>();
            if (voiceClient == null)
                voiceClient = gameObject.AddComponent<ElevenLabsVoiceClient>();

            if (voiceSink == null)
                voiceSink = GetComponent<ElevenLabsVoiceSink>();
            if (voiceSink == null)
                voiceSink = gameObject.AddComponent<ElevenLabsVoiceSink>();

            if (actionHud == null)
                actionHud = GetComponent<InterrogationActionHud>();
            if (actionHud == null)
                actionHud = gameObject.AddComponent<InterrogationActionHud>();

            dialogueService.Configure(shiftController, geminiApiKeySettings, apiProxySettings);
            // TTS still needs the Netlify proxy even though ask turns are offline.
            voiceClient.Configure(apiProxySettings);
            voiceSink.Configure(dialogueService, apiProxySettings);
            CaseGameplayBootstrap.StartCaseHandler = HandleBootstrapStartCase;
        }

        private void OnDestroy()
        {
            if (CaseGameplayBootstrap.StartCaseHandler == HandleBootstrapStartCase)
                CaseGameplayBootstrap.StartCaseHandler = null;
        }

        private void Start()
        {
            StartCase();
        }

        /// <summary>
        /// Starts or restarts a seeded case. Each call advances the seed so Restart rotates case titles
        /// through <see cref="CaseGenerator"/> / <c>Resources/Cases/case_templates.json</c>.
        /// </summary>
        public void StartCase()
        {
            int seed = caseSeed + shiftSerial;
            shiftSerial++;

            if (dialogueService != null)
                dialogueService.BeginShift();
            shiftController.BeginGeneratedShift(seed, suspectCount);

            string title = CaseGenerator.PeekTitle(seed);
            if (actionHud != null)
            {
                actionHud.ResetForNewCase(
                    "CASE: " + title +
                    "  ·  Computer collect ≥2 → EVIDENCE → ACTIONS Call in → ASK → DECISION HOLD/RELEASE.");
            }

            Debug.Log(
                "[CaseGameplay] shift seed=" + seed +
                " title=\"" + title + "\"" +
                " treeHook=" + CaseGenerator.PeekTreeHook(seed) +
                " templates=" + CaseGenerator.TemplateCount,
                this);
        }

        private void HandleBootstrapStartCase(CaseShiftController controller)
        {
            if (controller != null && controller != shiftController)
                shiftController = controller;
            StartCase();
        }
    }
}
