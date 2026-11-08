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
    /// Clipboard tabs: All + one per suspect so switching people does not look like the same pile.
    /// Bezi owns canvas / card prefab placement; Cursor owns CaseCore wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvidenceBoardController : MonoBehaviour
    {
        private const string AllTabId = "";

        [Header("Case Connection")]
        [Tooltip("Active shift controller. Auto-found when empty.")]
        [SerializeField] private CaseShiftController shiftController;

        [Header("Board Scene Hooks")]
        [Tooltip("Root object for the physical / world-space board on the desk. Shown when the board should be visible.")]
        [SerializeField] private GameObject boardRoot;

        [Tooltip("Parent transform that receives spawned evidence card instances.")]
        [SerializeField] private Transform cardContainer;

        [Tooltip("Optional row for All + per-suspect tab buttons. Created at runtime when empty.")]
        [SerializeField] private Transform tabContainer;

        [Tooltip("Optional prefab with EvidenceCardView. When empty, tries Assets/EvidenceCardView then a runtime fallback.")]
        [SerializeField] private EvidenceCardView cardPrefab;

        [Tooltip("Optional toast label shown when a new record is added from the browser.")]
        [SerializeField] private Text toastLabel;

        [Tooltip("Optional empty-state hint when the active tab has no filed cards.")]
        [SerializeField] private Text emptyHintLabel;

        [Tooltip("Seconds the toast stays visible.")]
        [SerializeField, Min(0.5f)] private float toastSeconds = 2.4f;

        [Tooltip("Keep the board root active even with zero cards (desk prop always visible).")]
        [SerializeField] private bool keepBoardVisibleWhenEmpty = true;

        [Tooltip("When the next suspect is ready, auto-select that suspect's tab.")]
        [SerializeField] private bool followCurrentSuspectTab = true;

        [Header("Stress Meter (optional desk readout)")]
        [Tooltip("Optional stress value text on the board side of the desk.")]
        [SerializeField] private Text stressValueLabel;

        [Tooltip("Interrogation controller used for the stress readout. Auto-found when empty.")]
        [SerializeField] private InterrogationController interrogationController;

        private readonly List<EvidenceCardView> activeCards = new List<EvidenceCardView>();
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<string> tabSuspectIds = new List<string>();
        private Coroutine toastRoutine;
        private string selectedEvidenceId = string.Empty;
        private string activeTabSuspectId = AllTabId;
        private Font uiFont;

        /// <summary>Raised when the player selects or clears a card for confrontation.</summary>
        public event Action<string> EvidenceSelectionChanged;

        /// <summary>Raised after the board card list is rebuilt.</summary>
        public event Action BoardRefreshed;

        /// <summary>Raised when a newly collected record is shown (toast / SFX hook).</summary>
        public event Action<EvidenceRecord> EvidenceAddedVisual;

        /// <summary>Raised when the All / per-suspect tab changes.</summary>
        public event Action<string> ActiveTabChanged;

        /// <summary>Currently selected evidence id for interrogation present, or empty.</summary>
        public string SelectedEvidenceId { get { return selectedEvidenceId; } }

        /// <summary>Active clipboard tab suspect id, or empty for All.</summary>
        public string ActiveTabSuspectId { get { return activeTabSuspectId ?? AllTabId; } }

        /// <summary>Live card views currently on the board (after tab filter).</summary>
        public IReadOnlyList<EvidenceCardView> ActiveCards { get { return activeCards; } }

        /// <summary>True when a card container is assigned (Inspector or <see cref="SetBoardSceneHooks"/>).</summary>
        public bool HasBoardHooks { get { return cardContainer != null; } }

        private void Awake()
        {
            uiFont = DeskUiPalette.ResolveFont();

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

            if (cardPrefab == null)
                cardPrefab = TryLoadBeziCardPrefab();

            if (toastLabel != null)
                toastLabel.gameObject.SetActive(false);
            if (emptyHintLabel != null)
                emptyHintLabel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected += HandleEvidenceCollected;
                shiftController.PhaseChanged += HandlePhaseChanged;
                shiftController.NextSuspectReady += HandleNextSuspectReady;
            }
            RefreshBoard();
        }

        private void OnDisable()
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected -= HandleEvidenceCollected;
                shiftController.PhaseChanged -= HandlePhaseChanged;
                shiftController.NextSuspectReady -= HandleNextSuspectReady;
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
        public void SetBoardSceneHooks(
            GameObject root,
            Transform container,
            EvidenceCardView prefab = null,
            Text toast = null,
            Transform tabs = null,
            Text emptyHint = null)
        {
            boardRoot = root;
            cardContainer = container != null ? container : (root != null ? root.transform : cardContainer);
            if (prefab != null) cardPrefab = prefab;
            if (toast != null) toastLabel = toast;
            if (tabs != null) tabContainer = tabs;
            if (emptyHint != null) emptyHintLabel = emptyHint;
            if (cardPrefab == null)
                cardPrefab = TryLoadBeziCardPrefab();
            RefreshBoard();
        }

        /// <summary>Assigns the shift controller used by this board.</summary>
        public void SetShiftController(CaseShiftController controller)
        {
            if (shiftController != null)
            {
                shiftController.EvidenceCollected -= HandleEvidenceCollected;
                shiftController.PhaseChanged -= HandlePhaseChanged;
                shiftController.NextSuspectReady -= HandleNextSuspectReady;
            }

            shiftController = controller;
            if (isActiveAndEnabled && shiftController != null)
            {
                shiftController.EvidenceCollected += HandleEvidenceCollected;
                shiftController.PhaseChanged += HandlePhaseChanged;
                shiftController.NextSuspectReady += HandleNextSuspectReady;
            }
            RefreshBoard();
        }

        /// <summary>Selects All (empty id) or a per-suspect clipboard tab.</summary>
        public void SetActiveSuspectTab(string suspectId)
        {
            string next = suspectId ?? AllTabId;
            if (next == activeTabSuspectId)
            {
                RefreshBoard();
                return;
            }

            // Clear present selection when leaving a suspect pile — A must not stay current for B.
            if (!string.IsNullOrEmpty(selectedEvidenceId))
                ClearSelection();

            activeTabSuspectId = next;
            ActiveTabChanged?.Invoke(activeTabSuspectId);
            RefreshBoard();
        }

        /// <summary>Rebuilds tab buttons + card views from CaseSession collected evidence.</summary>
        public void RefreshBoard()
        {
            RebuildTabs();
            ClearCards();

            IReadOnlyList<EvidenceRecord> collected = shiftController != null
                ? shiftController.GetCollectedEvidence()
                : Array.Empty<EvidenceRecord>();

            int visibleCount = 0;
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord record = collected[i];
                if (record == null) continue;

                string ownerId = shiftController != null
                    ? shiftController.FindEvidenceOwnerId(record.Id)
                    : string.Empty;

                if (!PassesActiveTab(ownerId))
                    continue;

                EvidenceCardView card = SpawnCard();
                if (card == null) continue;
                card.Bind(record, ownerId);
                card.Clicked += HandleCardClicked;
                card.SetSelected(record.Id == selectedEvidenceId);
                activeCards.Add(card);
                visibleCount++;
            }

            bool hasAnyCollected = collected.Count > 0;
            if (boardRoot != null)
                boardRoot.SetActive(keepBoardVisibleWhenEmpty || hasAnyCollected);

            if (emptyHintLabel != null)
            {
                if (visibleCount == 0)
                {
                    emptyHintLabel.gameObject.SetActive(true);
                    emptyHintLabel.text = hasAnyCollected
                        ? "No records on this tab. Switch to All or another suspect."
                        : "No records filed yet. Collect from the computer / public records site.";
                }
                else
                {
                    emptyHintLabel.gameObject.SetActive(false);
                }
            }

            if (!string.IsNullOrEmpty(selectedEvidenceId))
            {
                bool stillVisible = false;
                for (int i = 0; i < activeCards.Count; i++)
                {
                    if (activeCards[i] != null && activeCards[i].EvidenceId == selectedEvidenceId)
                    {
                        stillVisible = true;
                        break;
                    }
                }

                // Drop selection when filtered off the active tab, or removed from the case.
                if (!stillVisible || !ContainsEvidenceId(collected, selectedEvidenceId))
                    ClearSelection();
            }

            RefreshTabVisuals();
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
                // Card may live on another tab — jump to its owner tab then select.
                string ownerId = shiftController != null
                    ? shiftController.FindEvidenceOwnerId(evidenceId)
                    : string.Empty;
                if (!string.IsNullOrEmpty(ownerId) && ownerId != activeTabSuspectId)
                {
                    activeTabSuspectId = ownerId;
                    ActiveTabChanged?.Invoke(activeTabSuspectId);
                    selectedEvidenceId = evidenceId;
                    RefreshBoard();
                    EvidenceSelectionChanged?.Invoke(selectedEvidenceId);
                    return;
                }

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
            if (record != null && shiftController != null && followCurrentSuspectTab)
            {
                string ownerId = shiftController.FindEvidenceOwnerId(record.Id);
                if (!string.IsNullOrEmpty(ownerId) && ownerId != activeTabSuspectId)
                {
                    if (!string.IsNullOrEmpty(selectedEvidenceId))
                        ClearSelection();
                    activeTabSuspectId = ownerId;
                    ActiveTabChanged?.Invoke(activeTabSuspectId);
                }
            }

            RefreshBoard();
            if (record != null)
            {
                SelectEvidence(record.Id);
                ShowAddedToast(record);
            }
        }

        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            if (phase == CaseShiftPhase.Inactive || phase == CaseShiftPhase.Briefing)
                ClearSelection();

            if (followCurrentSuspectTab && shiftController != null && shiftController.CurrentSuspect != null)
            {
                string next = shiftController.CurrentSuspect.Id;
                if (next != activeTabSuspectId)
                {
                    if (!string.IsNullOrEmpty(selectedEvidenceId))
                        ClearSelection();
                    activeTabSuspectId = next;
                    ActiveTabChanged?.Invoke(activeTabSuspectId);
                }
            }

            RefreshBoard();
            RefreshStressMeter();
        }

        private void HandleNextSuspectReady(NextSuspectEvent next)
        {
            ClearSelection();
            if (!followCurrentSuspectTab || next == null || string.IsNullOrWhiteSpace(next.SuspectId))
            {
                RefreshBoard();
                return;
            }
            SetActiveSuspectTab(next.SuspectId);
        }

        private void HandleCardClicked(EvidenceCardView card)
        {
            if (card == null) return;
            if (card.EvidenceId == selectedEvidenceId)
                ClearSelection();
            else
                SelectEvidence(card.EvidenceId);
        }

        private bool PassesActiveTab(string ownerId)
        {
            if (string.IsNullOrEmpty(activeTabSuspectId))
                return true;
            return string.Equals(ownerId, activeTabSuspectId, StringComparison.Ordinal);
        }

        private void RebuildTabs()
        {
            EnsureTabContainer();
            ClearTabs();
            if (tabContainer == null) return;

            AddTabButton("All", AllTabId);

            if (shiftController == null || shiftController.ActiveSession == null)
                return;

            IReadOnlyList<SuspectRecord> suspects = shiftController.ActiveSession.Case.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                SuspectRecord suspect = suspects[i];
                if (suspect == null || string.IsNullOrWhiteSpace(suspect.Id))
                    continue;
                string label = string.IsNullOrWhiteSpace(suspect.DisplayName)
                    ? suspect.Id
                    : ShortName(suspect.DisplayName);
                AddTabButton(label, suspect.Id);
            }
        }

        private void EnsureTabContainer()
        {
            if (tabContainer != null || boardRoot == null)
                return;

            GameObject tabs = new GameObject("SuspectTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabs.transform.SetParent(boardRoot.transform, false);
            RectTransform rect = tabs.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -72f);
            rect.sizeDelta = new Vector2(-24f, 34f);
            HorizontalLayoutGroup layout = tabs.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            tabContainer = tabs.transform;
        }

        private void AddTabButton(string label, string suspectId)
        {
            GameObject go = new GameObject("Tab_" + (string.IsNullOrEmpty(suspectId) ? "All" : suspectId),
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(tabContainer, false);
            LayoutElement layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 28f;
            layout.preferredHeight = 28f;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            RectTransform textRect = labelGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Button button = go.GetComponent<Button>();
            DeskUiPalette.StyleFolderTag(button, false);
            string id = suspectId;
            button.onClick.AddListener(() => SetActiveSuspectTab(id));
            tabButtons.Add(button);
            tabSuspectIds.Add(id);
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                Button button = tabButtons[i];
                if (button == null) continue;
                string id = i < tabSuspectIds.Count ? tabSuspectIds[i] : AllTabId;
                bool active = string.Equals(id, activeTabSuspectId, StringComparison.Ordinal);
                DeskUiPalette.StyleFolderTag(button, active);
            }
        }

        private void ClearTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] != null)
                    Destroy(tabButtons[i].gameObject);
            }
            tabButtons.Clear();
            tabSuspectIds.Clear();
        }

        private EvidenceCardView SpawnCard()
        {
            if (cardContainer == null)
            {
                Debug.LogWarning(
                    "EvidenceBoardController: assign Card Container (desk board content root).",
                    this);
                return null;
            }

            if (cardPrefab == null)
                cardPrefab = TryLoadBeziCardPrefab();

            if (cardPrefab != null)
            {
                EvidenceCardView instance = Instantiate(cardPrefab, cardContainer);
                instance.gameObject.SetActive(true);
                return instance;
            }

            return CreateRuntimeFallbackCard(cardContainer);
        }

        private static EvidenceCardView TryLoadBeziCardPrefab()
        {
            EvidenceCardView fromResources = Resources.Load<EvidenceCardView>("EvidenceCardView");
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EvidenceCardView.prefab");
            if (asset != null)
                return asset.GetComponent<EvidenceCardView>();
#endif
            return null;
        }

        private EvidenceCardView CreateRuntimeFallbackCard(Transform parent)
        {
            GameObject root = new GameObject("EvidenceCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EvidenceCardView));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 96f);

            Image background = root.GetComponent<Image>();
            DeskUiPalette.ApplyFill(background, DeskUiPalette.ManilaLight);
            DeskUiPalette.AddFolderFrame(root.transform, 1f, 1.5f);
            DeskUiPalette.AddAccentStripe(root.transform, DeskUiPalette.Hairline, 3f);
            DeskUiPalette.AddHairline(root.transform, "CardRule", 42f, 1f, true);

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(root.transform, false);
            Text title = titleGo.GetComponent<Text>();
            DeskUiPalette.StyleLabel(title, 14, DeskUiPalette.Ink, DeskUiPalette.TypeRole.Stamp, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.55f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(12f, 0f);
            titleRect.offsetMax = new Vector2(-8f, -6f);

            GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(root.transform, false);
            Text body = bodyGo.GetComponent<Text>();
            DeskUiPalette.StyleLabel(body, 12, DeskUiPalette.InkMuted, DeskUiPalette.TypeRole.Body, FontStyle.Normal, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 0.55f);
            bodyRect.offsetMin = new Vector2(12f, 6f);
            bodyRect.offsetMax = new Vector2(-8f, 0f);

            GameObject highlight = new GameObject("SelectedHighlight", typeof(RectTransform), typeof(Image));
            highlight.transform.SetParent(root.transform, false);
            highlight.SetActive(false);
            Image highlightImage = highlight.GetComponent<Image>();
            DeskUiPalette.ApplyFill(highlightImage, new Color(DeskUiPalette.StampHold.r, DeskUiPalette.StampHold.g, DeskUiPalette.StampHold.b, 0.18f));
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
                stressValueLabel.gameObject.SetActive(false);
                return;
            }

            int stress = interrogationController.GetStress(suspect.Id);
            stressValueLabel.gameObject.SetActive(true);
            stressValueLabel.text = suspect.DisplayName + " · " + stress + "/100";
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

        private static string ShortName(string displayName)
        {
            string trimmed = displayName.Trim();
            int space = trimmed.IndexOf(' ');
            if (space > 0 && space < trimmed.Length - 1)
                return trimmed.Substring(0, space);
            if (trimmed.Length > 10)
                return trimmed.Substring(0, 10);
            return trimmed;
        }
    }
}
