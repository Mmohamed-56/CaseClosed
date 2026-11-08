using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>Exposes sanitized case data and an evidence callback for a browser integration adapter.</summary>
    public sealed class CaseWebsiteBridge : MonoBehaviour
    {
        [Header("Case Connection")]
        [Tooltip("The active shift controller that owns case state and collected evidence.")]
        [SerializeField] private CaseShiftController shiftController;

        /// <summary>Returns JSON for the active case, with culprit identity and hidden clue metadata omitted.</summary>
        public string GetCurrentCaseJson()
        {
            if (shiftController == null || shiftController.ActiveSession == null) return string.Empty;
            return CaseWebsitePayloadBuilder.BuildJson(shiftController.ActiveSession.Case);
        }

        /// <summary>Returns the URL hash fragment for loading the active case in the records website.</summary>
        public string GetCurrentCaseHashFragment()
        {
            if (shiftController == null || shiftController.ActiveSession == null) return string.Empty;
            return CaseWebsitePayloadBuilder.BuildHashFragment(shiftController.ActiveSession.Case);
        }

        /// <summary>Browser callback target for the records site's UBPDBridge evidence message.</summary>
        /// <param name="recordId">The public ID of the record added by the player.</param>
        /// <param name="message">Optional serialized protocol message from the website.</param>
        public void UBPD_onAddEvidence(string recordId, string message)
        {
            ReceiveEvidenceRecordId(recordId);
        }

        /// <summary>Accepts a record ID from the browser and adds it to the active evidence board state.</summary>
        /// <param name="recordId">The public ID of a record visible in the active case.</param>
        /// <returns>True when that record is newly collected; false for unknown, duplicate, or unavailable records.</returns>
        public bool ReceiveEvidenceRecordId(string recordId)
        {
            if (shiftController == null || shiftController.ActiveSession == null) return false;
            CaseShiftPhase phase = shiftController.Phase;
            if (phase != CaseShiftPhase.Briefing && phase != CaseShiftPhase.Investigating) return false;
            return shiftController.CollectEvidence(recordId);
        }

        /// <summary>Assigns the shift controller used by this bridge.</summary>
        public void SetShiftController(CaseShiftController controller)
        {
            shiftController = controller;
        }

        private void Awake()
        {
            if (shiftController == null) shiftController = GetComponent<CaseShiftController>();
        }
    }
}
