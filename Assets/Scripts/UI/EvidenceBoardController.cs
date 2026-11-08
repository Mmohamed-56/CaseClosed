using System;
using System.Collections;
using System.Collections.Generic;
using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Desk evidence board: rebuilds cards from <see cref="CaseShiftController"/> collected evidence
    /// (including records added via <see cref="CaseWebsiteBridge"/> → CollectEvidence).
    /// Bezi owns canvas / card prefab placement; Cursor owns CaseCore wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvidenceBoardController : MonoBehaviour
    {
        [Header("Case Connection")]
        [Tooltip("Active shift controller. Auto-found when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Board Scene Hooks (Bezi)")]
        [Tooltip("Root object for the physical / world-space board on the desk. Shown when the board should be visible.")]
        [SerializeField] private GameObject boardRoot;

        [Tooltip("Parent transform that receives spawned evidence card instances.")]
        [SerializeField] private Transform cardContainer;

        [Tooltip("Optional prefab with EvidenceCardView. When empty, a simple runtime card is created.")]
        [SerializeField] private EvidenceCardView cardPrefab;

        [Tooltip("Optional toast label shown when a new record is added from the browser.")]
        [SerializeField] private Text toastLabel;

        [Tooltip("Seconds the toast stays visible.")]
        [SerializeField, Min(0.5f)] private float toastSeconds = 2.4f;

        [Tooltip("Keep the board root active even with zero cards (desk prop always visible).")]
        [SerializeField] private bool keepBoardVisibleWhenEmpty = true;

        [Header("Stress Meter (optional desk readout)")]
        [Tooltip("Optional stress value text on the board side of the desk.")]
        [SerializeField] private Text stressValueLabel;

        [Tooltip("Interrogation controller used for the stress readout. Auto-found when empty.")]
        [SerializeField] private InterrogationController interrogationController;

        private readonly List<EvidenceCardView> activeCards = new List<EvidenceCardView>();
        private Coroutine toastRoutine;
        private string selectedEvidenceId = string.Empty;

        /// <summary>Raised when the player selects or clears a card for confrontation.</summary>
        public event Action<string> EvidenceSelectionChanged;

        /// <summary>Raised after the board card list is rebuilt.</summary>
        public event Action BoardRefreshed;

        /// <summary>Raised when a newly collected record is shown (toast / SFX hook).</summary>
        public event Action<EvidenceRecord> EvidenceAddedVisual;

        /// <summary>Currently selected evidence id for interrogation present, or empty.</summary>
        public string SelectedEvidenceId { get { return selectedEvidenceId; } }

        /// <summary>Live card views currently on the board.</summary>
        public IReadOnlyList<EvidenceCardView> ActiveCards { get { return activeCards; } }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();

            if (interrogationController == null && shiftController != null)
                interrogationController = shiftController.GetComponent<InterrogationController>();
            if (interrogationController == null)
                interrogationController = FindAnyObjectByType<InterrogationController>();

            if (cardContainer == null && boardRoot != null)
                cardContainer = boardRoot.transform;

            if (toastLabel != null)
                toastLabel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected += HandleEvidenceCollected;
                shiftController.PhaseChanged += HandlePhaseChanged;
            }
            RefreshBoard();
        }

        private void OnDisable()
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected -= HandleEvidenceCollected;
                shiftController.PhaseChanged -= HandlePhaseChanged;
            }
            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
                toastRoutine = null;
            }
        }

        private void Update()
        {
            RefreshStressMeter();
        }

        /// <summary>Bezi / Magdy can assign desk board anchors if not wired in the Inspector.</summary>
        public void SetBoardSceneHooks(GameObject root, Transform container, EvidenceCardView prefab = null, Text toast = null)
        {
            boardRoot = root;
            cardContainer = container != null ? container : (root != null ? root.transform : cardContainer);
            if (prefab != null) cardPrefab = prefab;
            if (toast != null) toastLabel = toast;
            RefreshBoard();
        }

        /// <summary>Assigns the shift controller used by this board.</summary>
        public void SetShiftController(CaseShiftController controller)
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected -= HandleEvidenceCollected;
                shiftController.PhaseChanged -= HandlePhaseChanged;
            }

            shiftController = controller;
            if (isActiveAndEnabled && shiftController != null)
            {
                shiftController.EvidenceCollected += HandleEvidenceCollected;
                shiftController.PhaseChanged += HandlePhaseChanged;
            }
            RefreshBoard();
        }

        /// <summary>Rebuilds card views from CaseSession collected evidence.</summary>
        public void RefreshBoard()
        {
            ClearCards();

            IReadOnlyList<EvidenceRecord> collected = shiftController != null
                ? shiftController.GetCollectedEvidence()
                : Array.Empty<EvidenceRecord>();

            bool hasCards = collected.Count > 0;
            if (boardRoot != null)
                boardRoot.SetActive(keepBoardVisibleWhenEmpty || hasCards);

            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord record = collected[i];
                if (record == null) continue;
                EvidenceCardView card = SpawnCard();
                if (card == null) continue;
                card.Bind(record);
                card.Clicked += HandleCardClicked;
                card.SetSelected(record.Id == selectedEvidenceId);
                activeCards.Add(card);
            }

            if (!string.IsNullOrEmpty(selectedEvidenceId) && !ContainsEvidenceId(collected, selectedEvidenceId))
                ClearSelection();

            BoardRefreshed?.Invoke();
        }

        /// <summary>Selects a collected card for confrontation, or clears when id is empty/unknown.</summary>
        public void SelectEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                ClearSelection();
                return;
            }

            bool found = false;
            for (int i = 0; i < activeCards.Count; i++)
            {
                bool match = activeCards[i].EvidenceId == evidenceId;
                activeCards[i].SetSelected(match);
                if (match) found = true;
            }

            if (!found)
            {
                ClearSelection();
                return;
            }

            selectedEvidenceId = evidenceId;
            EvidenceSelectionChanged?.Invoke(selectedEvidenceId);
        }

        /// <summary>Clears the confrontation selection.</summary>
        public void ClearSelection()
        {
            selectedEvidenceId = string.Empty;
            for (int i = 0; i < activeCards.Count; i++)
                activeCards[i].SetSelected(false);
            EvidenceSelectionChanged?.Invoke(selectedEvidenceId);
        }

        /// <summary>Shows the "Added to evidence board" toast (also used by browser collect path).</summary>
        public void ShowAddedToast(EvidenceRecord record)
        {
            string title = record == null || string.IsNullOrWhiteSpace(record.Title) ? "Evidence" : record.Title;
            ShowToast("Added to evidence board: " + title);
            EvidenceAddedVisual?.Invoke(record);
        }

        private void HandleEvidenceCollected(EvidenceRecord record)
        {
            RefreshBoard();
            if (record != null)
            {
                SelectEvidence(record.Id);
                ShowAddedToast(record);
            }
        }

        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            // New shift / next suspect: keep collected cards across the shift, clear confrontation pick.
            if (phase == CaseShiftPhase.Inactive || phase == CaseShiftPhase.Briefing)
                ClearSelection();
            RefreshBoard();
            RefreshStressMeter();
        }

        private void HandleCardClicked(EvidenceCardView card)
        {
            if (card == null) return;
            if (card.EvidenceId == selectedEvidenceId)
                ClearSelection();
            else
                SelectEvidence(card.EvidenceId);
        }

        private EvidenceCardView SpawnCard()
        {
            if (cardContainer == null)
            {
                Debug.LogWarning(
                    "EvidenceBoardController: assign Card Container (desk board content root). See docs/bezi-evidence-board-stamp.md.",
                    this);
                return null;
            }

            if (cardPrefab != null)
            {
                EvidenceCardView instance = Instantiate(cardPrefab, cardContainer);
                instance.gameObject.SetActive(true);
                return instance;
            }

            return CreateRuntimeFallbackCard(cardContainer);
        }

        private static EvidenceCardView CreateRuntimeFallbackCard(Transform parent)
        {
            GameObject root = new GameObject("EvidenceCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EvidenceCardView));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 96f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.92f, 0.88f, 0.78f, 0.96f);

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(root.transform, false);
            Text title = titleGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (title.font == null)
                title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.12f, 0.1f, 0.08f, 1f);
            title.alignment = TextAnchor.UpperLeft;
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.55f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(8f, 0f);
            titleRect.offsetMax = new Vector2(-8f, -6f);

            GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(root.transform, false);
            Text body = bodyGo.GetComponent<Text>();
            body.font = title.font;
            body.fontSize = 12;
            body.color = new Color(0.18f, 0.16f, 0.12f, 1f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 0.55f);
            bodyRect.offsetMin = new Vector2(8f, 6f);
            bodyRect.offsetMax = new Vector2(-8f, 0f);

            GameObject highlight = new GameObject("SelectedHighlight", typeof(RectTransform), typeof(Image));
            highlight.transform.SetParent(root.transform, false);
            highlight.SetActive(false);
            Image highlightImage = highlight.GetComponent<Image>();
            highlightImage.color = new Color(0.75f, 0.55f, 0.15f, 0.35f);
            RectTransform highlightRect = highlight.GetComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;

            EvidenceCardView view = root.GetComponent<EvidenceCardView>();
            view.SetUiRefs(title, body, root.GetComponent<Button>(), highlight);
            return view;
        }

        private void ClearCards()
        {
            for (int i = 0; i < activeCards.Count; i++)
            {
                EvidenceCardView card = activeCards[i];
                if (card == null) continue;
                card.Clicked -= HandleCardClicked;
                Destroy(card.gameObject);
            }
            activeCards.Clear();
        }

        private void ShowToast(string message)
        {
            if (toastLabel == null) return;
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastLabel.text = message;
            toastLabel.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(toastSeconds);
            toastLabel.gameObject.SetActive(false);
            toastRoutine = null;
        }

        private void RefreshStressMeter()
        {
            if (stressValueLabel == null || shiftController == null) return;
            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null || interrogationController == null)
            {
                stressValueLabel.text = string.Empty;
                return;
            }

            int stress = interrogationController.GetStress(suspect.Id);
            stressValueLabel.text = "Stress " + stress + "/100";
        }

        private static bool ContainsEvidenceId(IReadOnlyList<EvidenceRecord> records, string evidenceId)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && records[i].Id == evidenceId)
                    return true;
            }
            return false;
        }
    }
}
