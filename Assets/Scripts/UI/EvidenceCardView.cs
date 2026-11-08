using System;
using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// One evidence card on the desk board. Bezi can swap Text/Image refs; Cursor owns bind + select hooks.
    /// Cards are tagged with <see cref="SuspectId"/> so per-suspect clipboard tabs can filter the pile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvidenceCardView : MonoBehaviour
    {
        [Header("Card UI")]
        [Tooltip("Title line for the collected record.")]
        [SerializeField] private Text titleText;

        [Tooltip("Short description body.")]
        [SerializeField] private Text bodyText;

        [Tooltip("Optional owner / suspect label (Bezi). Hidden when empty.")]
        [SerializeField] private Text suspectLabelText;

        [Tooltip("Optional click target. Uses this GameObject's Button when empty.")]
        [SerializeField] private Button selectButton;

        [Tooltip("Optional highlight root enabled while this card is selected for confrontation.")]
        [SerializeField] private GameObject selectedHighlight;

        private EvidenceRecord boundRecord;
        private string suspectId = string.Empty;

        /// <summary>Raised when the player clicks this card.</summary>
        public event Action<EvidenceCardView> Clicked;

        /// <summary>Bound evidence id, or empty before Bind.</summary>
        public string EvidenceId
        {
            get { return boundRecord == null ? string.Empty : boundRecord.Id; }
        }

        /// <summary>Owner suspect / directory id tagged on this card, or empty.</summary>
        public string SuspectId { get { return suspectId ?? string.Empty; } }

        /// <summary>Currently bound record, or null.</summary>
        public EvidenceRecord BoundRecord { get { return boundRecord; } }

        private void Awake()
        {
            if (selectButton == null)
                selectButton = GetComponent<Button>();
            if (selectButton != null)
                selectButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleClick);
        }

        /// <summary>Bezi / Magdy can rewire text/button refs at runtime.</summary>
        public void SetUiRefs(Text title, Text body, Button button = null, GameObject highlight = null, Text suspectLabel = null)
        {
            titleText = title;
            bodyText = body;
            if (suspectLabel != null)
                suspectLabelText = suspectLabel;
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleClick);
            selectButton = button != null ? button : GetComponent<Button>();
            selectedHighlight = highlight;
            if (selectButton != null)
                selectButton.onClick.AddListener(HandleClick);
        }

        /// <summary>Fills the card from a collected CaseCore evidence record (no owner tag).</summary>
        public void Bind(EvidenceRecord record)
        {
            Bind(record, string.Empty);
        }

        /// <summary>Fills the card and tags it with the owning suspect / directory id.</summary>
        public void Bind(EvidenceRecord record, string ownerSuspectId)
        {
            boundRecord = record;
            suspectId = ownerSuspectId ?? string.Empty;

            if (titleText != null)
            {
                string title = record == null ? string.Empty : record.Title;
                titleText.text = title ?? string.Empty;
                titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
            }
            if (bodyText != null)
            {
                string body = record == null ? string.Empty : record.Description;
                bodyText.text = body ?? string.Empty;
                bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
            }
            if (suspectLabelText != null)
            {
                bool show = !string.IsNullOrWhiteSpace(suspectId);
                suspectLabelText.gameObject.SetActive(show);
                if (show)
                    suspectLabelText.text = suspectId;
            }

            SetSelected(false);
        }

        /// <summary>Toggles the selected highlight used when presenting evidence in interrogation.</summary>
        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.SetActive(selected);
        }

        private void HandleClick()
        {
            Clicked?.Invoke(this);
        }
    }
}
