using System;
using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// One evidence card on the desk board. Bezi can swap Text/Image refs; Cursor owns bind + select hooks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvidenceCardView : MonoBehaviour
    {
        [Header("Card UI")]
        [Tooltip("Title line for the collected record.")]
        [SerializeField] private Text titleText;

        [Tooltip("Short description body.")]
        [SerializeField] private Text bodyText;

        [Tooltip("Optional click target. Uses this GameObject's Button when empty.")]
        [SerializeField] private Button selectButton;

        [Tooltip("Optional highlight root enabled while this card is selected for confrontation.")]
        [SerializeField] private GameObject selectedHighlight;

        private EvidenceRecord boundRecord;

        /// <summary>Raised when the player clicks this card.</summary>
        public event Action<EvidenceCardView> Clicked;

        /// <summary>Bound evidence id, or empty before Bind.</summary>
        public string EvidenceId
        {
            get { return boundRecord == null ? string.Empty : boundRecord.Id; }
        }

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
        public void SetUiRefs(Text title, Text body, Button button = null, GameObject highlight = null)
        {
            titleText = title;
            bodyText = body;
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleClick);
            selectButton = button != null ? button : GetComponent<Button>();
            selectedHighlight = highlight;
            if (selectButton != null)
                selectButton.onClick.AddListener(HandleClick);
        }

        /// <summary>Fills the card from a collected CaseCore evidence record.</summary>
        public void Bind(EvidenceRecord record)
        {
            boundRecord = record;
            if (titleText != null)
                titleText.text = record == null ? string.Empty : record.Title;
            if (bodyText != null)
                bodyText.text = record == null ? string.Empty : record.Description;
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
