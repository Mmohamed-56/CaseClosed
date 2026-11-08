using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Networking;
using CaseClosed.UI;
using UnityEngine;

namespace CaseClosed.Gameplay
{
    /// <summary>
    /// Boots the first-run prototype loop and wires offline dialogue / voice / desk HUD.
    /// Ask path: CaseCore stress → <see cref="InterrogationDialogueService"/> → ScenarioDialogueClient bank.
    /// Visual interrogation UI lives on <see cref="InterrogationActionHud"/> (UGUI).
    /// Runs before <see cref="StampUiPlayableBootstrap"/> so the HUD can claim Case File chrome.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
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

        [Header("Dialogue (offline bank preferred)")]
        [Tooltip("Optional legacy Gemini key settings. Unused while allowGeminiFallback is OFF.")]
        [SerializeField] private GeminiApiKeySettings geminiApiKeySettings;
        [Tooltip("Optional cloud proxy settings (Netlify). Keep assigned; ask path does not need it.")]
        [SerializeField] private ApiProxySettings apiProxySettings;
        [Tooltip("Dialogue bridge: stress from CaseCore, spoken lines from ScenarioDialogueClient.")]
        [SerializeField] private InterrogationDialogueService dialogueService;

        [Header("Voice")]
        [Tooltip("Bridges SuspectLineReady → ElevenLabs. Added automatically when empty.")]
        [SerializeField] private ElevenLabsVoiceSink voiceSink;

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

            if (voiceSink == null)
                voiceSink = GetComponent<ElevenLabsVoiceSink>();
            if (voiceSink == null)
                voiceSink = gameObject.AddComponent<ElevenLabsVoiceSink>();

            if (actionHud == null)
                actionHud = GetComponent<InterrogationActionHud>();
            if (actionHud == null)
                actionHud = gameObject.AddComponent<InterrogationActionHud>();

            dialogueService.Configure(shiftController, geminiApiKeySettings, apiProxySettings);
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

        /// <summary>Starts or restarts the deterministic prototype case (seed 42 / two suspects by default).</summary>
        public void StartCase()
        {
            if (dialogueService != null)
                dialogueService.BeginShift();
            shiftController.BeginGeneratedShift(caseSeed, suspectCount);
            if (actionHud != null)
            {
                actionHud.ResetForNewCase(
                    "Case ready. Computer collect ≥2 → EVIDENCE board → ACTIONS Call in → ASK → DECISION HOLD/RELEASE.");
            }
        }

        private void HandleBootstrapStartCase(CaseShiftController controller)
        {
            if (controller != null && controller != shiftController)
                shiftController = controller;
            StartCase();
        }
    }
}
