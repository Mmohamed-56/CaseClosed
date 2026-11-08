using CaseClosed.CaseCore;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.Networking;
using CaseClosed.UI;
using CaseClosed.Voice;
using System;
using System.Collections.Generic;
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
        private bool demoMode;

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
            demoMode = MainMenuController.StartDemoMode;
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
        /// Starts the selected Demo again, or the next uncompleted Standard case.
        /// </summary>
        public void StartCase()
        {
            CaseFile caseDefinition;
            int seed = 0;

            if (demoMode)
            {
                caseDefinition = DemoCaseFactory.CreateBannerCutdownCase();
            }
            else
            {
                HashSet<string> completedCaseIds = CaseHistoryStore.GetCompletedCaseIds();
                caseDefinition = null;
                for (int attempt = 0; attempt <= completedCaseIds.Count; attempt++)
                {
                    seed = caseSeed + shiftSerial;
                    shiftSerial++;
                    CaseFile candidate = CaseGenerator.Generate(seed, suspectCount);
                    if (completedCaseIds.Contains(candidate.CaseId))
                        continue;

                    caseDefinition = candidate;
                    break;
                }

                if (caseDefinition == null)
                    throw new InvalidOperationException("Could not find an uncompleted Standard case seed.");
            }

            if (dialogueService != null)
                dialogueService.BeginShift();
            shiftController.BeginShift(caseDefinition, !demoMode);

            string title = caseDefinition.Title;
            string difficulty = demoMode ? "DEMO" : CaseGenerator.PeekDifficulty(seed).ToUpperInvariant();
            if (actionHud != null)
            {
                actionHud.ResetForNewCase(
                    "CASE: " + title +
                    "  ·  " + difficulty +
                    "  ·  Computer collect ≥2 → EVIDENCE → ACTIONS Call in → ASK → DECISION HOLD/RELEASE.");
            }

            Debug.Log(
                "[CaseGameplay] mode=" + (demoMode ? "Demo" : "Standard") +
                " seed=" + (demoMode ? "fixed" : seed.ToString()) +
                " title=\"" + title + "\"" +
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
