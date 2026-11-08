using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Runtime screen-space notepad popup with authored claim rows, case marks, and persistent player notes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeskClaimNotepad : MonoBehaviour
    {
        private const string CanvasObjectName = "Desk Claim Notepad Canvas";
        private const string PanelObjectName = "Paper Surface";
        private const string HeaderObjectName = "Notepad Header";
        private const string ViewportObjectName = "Claims Viewport";
        private const string ContentObjectName = "Claims Content";
        private const string BuiltInFontName = "LegacyRuntime.ttf";
        private const float PopupWidth = 1480f;
        private const float PopupHeight = 900f;
        private const int PopupSortingOrder = 95;
        private const float HeaderHeight = 84f;
        private const float ViewportTopInset = 104f;
        private const float ViewportSideInset = 24f;
        private const float ViewportBottomInset = 24f;
        private const float ClaimsViewportBottomInset = 300f;
        private const float NotesCaptionTopInset = 600f;
        private const float NotesTextTopInset = 660f;
        private const float NotesTextBottomInset = 32f;
        private const float RowTextLeftInset = 78f;
        private const float RowTextRightInset = 20f;
        private const float RowTextVerticalInset = 10f;
        private const float MarkSize = 32f;
        private const float MarkThickness = 3f;
        private const float PaperRuleSpacing = 38f;
        private const float PaperRuleTopInset = 132f;
        private const float PaperMarginX = 68f;
        private const float MinimumClaimRowHeight = 96f;
        private const float MaximumClaimRowHeight = 120f;
        private static readonly Color BackdropColor = new Color(0.025f, 0.035f, 0.04f, 0.62f);
        private static readonly Color HeaderColor = new Color(0.08f, 0.12f, 0.16f, 1f);
        private static readonly Color HeaderTextColor = new Color(0.96f, 0.95f, 0.90f, 1f);
        private const int MaximumPlayerNoteLength = 800;
        private const float BackspaceRepeatDelay = 0.4f;
        private const float BackspaceRepeatInterval = 0.06f;

        private static readonly Color PaperColor = new Color(0.98f, 0.96f, 0.88f, 1f);
        private static readonly Color InkColor = new Color(0.13f, 0.13f, 0.12f, 1f);
        private static readonly Color RuleColor = new Color(0.30f, 0.48f, 0.62f, 0.30f);
        private static readonly Color MarginColor = new Color(0.72f, 0.25f, 0.20f, 0.42f);
        private static readonly Color MatchColor = new Color(0.16f, 0.43f, 0.24f, 1f);
        private static readonly Color MismatchColor = new Color(0.67f, 0.16f, 0.13f, 1f);

        [Header("Typography")]
        [SerializeField, Min(1)] private int headerFontSize = 48;
        [SerializeField, Min(1)] private int claimFontSize = 48;
        [SerializeField, Min(40f)] private float rowHeight = 116f;

        private readonly List<ClaimRow> claimRows = new List<ClaimRow>();
        private GameObject canvasObject;
        private RectTransform canvasRect;
        private RectTransform panelRect;
        private RectTransform contentRect;
        private ScrollRect claimsScrollRect;
        private Text notesTextLabel;
        private Keyboard subscribedKeyboard;
        private Font runtimeFont;
        private bool uiCreated;
        private bool isEditing;
        private string playerNotes = string.Empty;
        private float backspaceHeldSeconds;
        private int lastTextInputFrame = -1;
        private bool capsLockActive;

        private enum ClaimMark
        {
            None,
            Matched,
            Mismatch
        }

        private sealed class ClaimRow
        {
            public string ClaimId;
            public GameObject Root;
            public ClaimMarkVisual Mark;
        }

        private sealed class ClaimMarkVisual
        {
            public GameObject Root;
            public Image FirstStroke;
            public Image SecondStroke;

            public void SetMark(ClaimMark mark)
            {
                bool visible = mark != ClaimMark.None;
                Root.SetActive(visible);
                if (!visible)
                    return;

                bool matched = mark == ClaimMark.Matched;
                Color color = matched ? MatchColor : MismatchColor;
                FirstStroke.color = color;
                SecondStroke.color = color;

                RectTransform firstRect = FirstStroke.rectTransform;
                RectTransform secondRect = SecondStroke.rectTransform;
                if (matched)
                {
                    firstRect.sizeDelta = new Vector2(13f, MarkThickness);
                    firstRect.anchoredPosition = new Vector2(-4f, -4f);
                    firstRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    secondRect.sizeDelta = new Vector2(23f, MarkThickness);
                    secondRect.anchoredPosition = new Vector2(5f, 2f);
                    secondRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                else
                {
                    firstRect.sizeDelta = new Vector2(23f, MarkThickness);
                    firstRect.anchoredPosition = Vector2.zero;
                    firstRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    secondRect.sizeDelta = new Vector2(23f, MarkThickness);
                    secondRect.anchoredPosition = Vector2.zero;
                    secondRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
                }
            }
        }

        private void Awake()
        {
            EnsureUiCreated();
        }

        /// <summary>
        /// Replaces the displayed claims using parallel id and plain-language text arrays.
        /// Each supplied text becomes a row; an absent id leaves that row unmarkable.
        /// </summary>
        /// <param name="claimIds">Claim identifiers corresponding by index to claimTexts.</param>
        /// <param name="claimTexts">Plain-language claim text to show on the notepad.</param>
        public void SetClaims(string[] claimIds, string[] claimTexts)
        {
            EnsureUiCreated();
            RemoveClaimRows();

            int claimCount = claimTexts == null ? 0 : claimTexts.Length;
            for (int claimIndex = 0; claimIndex < claimCount; claimIndex++)
            {
                string claimId = claimIds != null && claimIndex < claimIds.Length
                    ? claimIds[claimIndex]
                    : string.Empty;
                string claimText = claimTexts[claimIndex] ?? string.Empty;
                claimRows.Add(CreateClaimRow(claimIndex, claimId, claimText));
            }

            contentRect.sizeDelta = new Vector2(0f, claimRows.Count * GetClaimRowHeight());
            claimsScrollRect.verticalNormalizedPosition = 1f;
            SetVisible(isEditing);
        }

        /// <summary>Marks every displayed row whose claim id exactly matches as matched.</summary>
        /// <param name="claimId">Identifier of the claim that matched.</param>
        public void NoteMatched(string claimId)
        {
            SetClaimMark(claimId, ClaimMark.Matched);
        }

        /// <summary>Marks every displayed row whose claim id exactly matches as a mismatch.</summary>
        /// <param name="claimId">Identifier of the claim that mismatched.</param>
        public void NoteMismatch(string claimId)
        {
            SetClaimMark(claimId, ClaimMark.Mismatch);
        }

        /// <summary>Removes displayed claims while preserving notes the player has written.</summary>
        public void Clear()
        {
            EnsureUiCreated();
            RemoveClaimRows();
            contentRect.sizeDelta = Vector2.zero;
            claimsScrollRect.verticalNormalizedPosition = 1f;
            SetVisible(isEditing);
        }

        /// <summary>Starts capturing real keyboard text and shows the writable page.</summary>
        public void BeginTyping()
        {
            EnsureUiCreated();
            if (!isEditing)
            {
                isEditing = true;
                lastTextInputFrame = -1;
                backspaceHeldSeconds = 0f;
            }
            SetVisible(true);
            SubscribeToKeyboard(Keyboard.current);
            RefreshPlayerNotesText();
        }

        /// <summary>Stops capturing keyboard text while preserving the player's notes.</summary>
        public void EndTyping()
        {
            isEditing = false;
            SubscribeToKeyboard(null);
            backspaceHeldSeconds = 0f;
            RefreshPlayerNotesText();
            SetVisible(false);
        }

        private void Update()
        {
            if (!isEditing)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != subscribedKeyboard)
                SubscribeToKeyboard(keyboard);
            if (keyboard == null)
                return;

            if (keyboard.capsLockKey.wasPressedThisFrame)
                capsLockActive = !capsLockActive;

            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                RemoveLastNoteCharacter();
                backspaceHeldSeconds = 0f;
            }
            else if (keyboard.backspaceKey.isPressed)
            {
                backspaceHeldSeconds += Time.unscaledDeltaTime;
                if (backspaceHeldSeconds >= BackspaceRepeatDelay)
                {
                    RemoveLastNoteCharacter();
                    backspaceHeldSeconds -= BackspaceRepeatInterval;
                }
            }
            else
            {
                backspaceHeldSeconds = 0f;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                AppendPlayerNote("\n");

            if (lastTextInputFrame != Time.frameCount)
                AppendFallbackKeyboardText(keyboard);
        }

        private void OnDisable()
        {
            SubscribeToKeyboard(null);
            isEditing = false;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        private void HandleTextInput(char character)
        {
            if (!isEditing)
                return;

            lastTextInputFrame = Time.frameCount;
            if (char.IsControl(character))
                return;

            AppendPlayerNote(character.ToString());
        }

        private void AppendFallbackKeyboardText(Keyboard keyboard)
        {
            bool shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool uppercaseLetters = shiftPressed ^ capsLockActive;
            for (int keyValue = (int)Key.A; keyValue <= (int)Key.Z; keyValue++)
            {
                Key key = (Key)keyValue;
                if (!keyboard[key].wasPressedThisFrame)
                    continue;

                char letter = (char)('a' + keyValue - (int)Key.A);
                AppendPlayerNote(uppercaseLetters ? char.ToUpperInvariant(letter).ToString() : letter.ToString());
            }

            const string Digits = "1234567890";
            const string ShiftedDigits = "!@#$%^&*()";
            for (int digitIndex = 0; digitIndex < Digits.Length; digitIndex++)
            {
                Key key = digitIndex == 9 ? Key.Digit0 : (Key)((int)Key.Digit1 + digitIndex);
                if (keyboard[key].wasPressedThisFrame)
                    AppendPlayerNote((shiftPressed ? ShiftedDigits[digitIndex] : Digits[digitIndex]).ToString());
            }

            if (keyboard.spaceKey.wasPressedThisFrame) AppendPlayerNote(" ");
            if (keyboard.backquoteKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "~" : "`");
            if (keyboard.minusKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "_" : "-");
            if (keyboard.equalsKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "+" : "=");
            if (keyboard.leftBracketKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "{" : "[");
            if (keyboard.rightBracketKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "}" : "]");
            if (keyboard.backslashKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "|" : "\\");
            if (keyboard.semicolonKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? ":" : ";");
            if (keyboard.quoteKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "\"" : "'");
            if (keyboard.commaKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "<" : ",");
            if (keyboard.periodKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? ">" : ".");
            if (keyboard.slashKey.wasPressedThisFrame) AppendPlayerNote(shiftPressed ? "?" : "/");
        }

        private void AppendPlayerNote(string value)
        {
            if (string.IsNullOrEmpty(value) || playerNotes.Length >= MaximumPlayerNoteLength)
                return;

            int allowedLength = MaximumPlayerNoteLength - playerNotes.Length;
            playerNotes += value.Length > allowedLength ? value.Substring(0, allowedLength) : value;
            RefreshPlayerNotesText();
        }

        private void RemoveLastNoteCharacter()
        {
            if (playerNotes.Length == 0)
                return;

            playerNotes = playerNotes.Substring(0, playerNotes.Length - 1);
            RefreshPlayerNotesText();
        }

        private void RefreshPlayerNotesText()
        {
            if (notesTextLabel == null)
                return;

            string visibleNotes = string.IsNullOrEmpty(playerNotes) && !isEditing
                ? "Type notes here…"
                : playerNotes + (isEditing ? "|" : string.Empty);
            notesTextLabel.text = visibleNotes;
            notesTextLabel.color = string.IsNullOrEmpty(playerNotes) && !isEditing
                ? new Color(InkColor.r, InkColor.g, InkColor.b, 0.48f)
                : InkColor;
        }

        private void SubscribeToKeyboard(Keyboard keyboard)
        {
            if (subscribedKeyboard == keyboard)
                return;
            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput -= HandleTextInput;
            subscribedKeyboard = keyboard;
            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput += HandleTextInput;
        }


        private void EnsureUiCreated()
        {
            if (uiCreated)
                return;

            runtimeFont = Resources.GetBuiltinResource<Font>(BuiltInFontName);

            canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = PopupSortingOrder;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(PopupWidth, PopupHeight);

            GameObject backdropObject = CreateUiObject("Popup Backdrop", canvasRect, typeof(Image));
            RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
            SetFullRect(backdropRect);
            Image backdropImage = backdropObject.GetComponent<Image>();
            backdropImage.color = BackdropColor;
            backdropImage.raycastTarget = true;

            GameObject panelObject = CreateUiObject(PanelObjectName, canvasRect, typeof(Image));
            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PopupWidth, PopupHeight);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = PaperColor;
            panelImage.raycastTarget = true;

            CreatePaperGuides(panelRect);
            CreateHeader(panelRect);
            CreateClaimsScrollView(panelRect);
            CreateNotesEditor(panelRect);
            SetVisible(false);
            uiCreated = true;
        }

        private void CreatePaperGuides(Transform parent)
        {
            float pageHeight = PopupHeight;
            for (float y = PaperRuleTopInset; y < pageHeight - ViewportBottomInset; y += PaperRuleSpacing)
            {
                GameObject ruleObject = CreateUiObject("Notebook Rule", parent, typeof(Image));
                RectTransform ruleRect = ruleObject.GetComponent<RectTransform>();
                ruleRect.anchorMin = new Vector2(0f, 1f);
                ruleRect.anchorMax = new Vector2(1f, 1f);
                ruleRect.pivot = new Vector2(0.5f, 0.5f);
                ruleRect.anchoredPosition = new Vector2(0f, -y);
                ruleRect.sizeDelta = new Vector2(-ViewportSideInset * 2f, 1.5f);
                Image ruleImage = ruleObject.GetComponent<Image>();
                ruleImage.color = RuleColor;
                ruleImage.raycastTarget = false;
            }

            GameObject marginObject = CreateUiObject("Notebook Margin", parent, typeof(Image));
            RectTransform marginRect = marginObject.GetComponent<RectTransform>();
            marginRect.anchorMin = new Vector2(0f, 0f);
            marginRect.anchorMax = new Vector2(0f, 1f);
            marginRect.pivot = new Vector2(0.5f, 0.5f);
            marginRect.offsetMin = new Vector2(PaperMarginX, ViewportBottomInset);
            marginRect.offsetMax = new Vector2(PaperMarginX + 2f, -ViewportTopInset);
            Image marginImage = marginObject.GetComponent<Image>();
            marginImage.color = MarginColor;
            marginImage.raycastTarget = false;
        }

        private void CreateHeader(Transform parent)
        {
            GameObject headerBackground = CreateUiObject("Notepad Header Bar", parent, typeof(Image));
            RectTransform backgroundRect = headerBackground.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(-16f, HeaderHeight);
            Image backgroundImage = headerBackground.GetComponent<Image>();
            backgroundImage.color = HeaderColor;
            backgroundImage.raycastTarget = false;

            GameObject headerObject = CreateUiObject(HeaderObjectName, parent, typeof(Text));
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -4f);
            headerRect.sizeDelta = new Vector2(-48f, HeaderHeight - 8f);

            Text headerText = headerObject.GetComponent<Text>();
            StyleText(headerText, Mathf.Min(headerFontSize, 36), FontStyle.Bold, TextAnchor.MiddleLeft);
            headerText.color = HeaderTextColor;
            headerText.text = "INTERVIEW NOTES";

            GameObject ruleObject = CreateUiObject("Header Rule", parent, typeof(Image));
            RectTransform ruleRect = ruleObject.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0f, 1f);
            ruleRect.anchorMax = new Vector2(1f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.anchoredPosition = new Vector2(0f, -HeaderHeight);
            ruleRect.sizeDelta = new Vector2(-ViewportSideInset * 2f, 2f);
            Image ruleImage = ruleObject.GetComponent<Image>();
            ruleImage.color = RuleColor;
            ruleImage.raycastTarget = false;
        }

        private void CreateNotesEditor(Transform parent)
        {
            GameObject captionObject = CreateUiObject("Your Notes Caption", parent, typeof(Text));
            RectTransform captionRect = captionObject.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0f, 1f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.anchoredPosition = new Vector2(0f, -NotesCaptionTopInset);
            captionRect.sizeDelta = new Vector2(-ViewportSideInset * 2f, 44f);
            Text caption = captionObject.GetComponent<Text>();
            StyleText(caption, 34, FontStyle.Bold, TextAnchor.MiddleLeft);
            caption.text = "YOUR NOTES";

            GameObject textObject = CreateUiObject("Handwritten Notes Text", parent, typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(58f, NotesTextBottomInset);
            textRect.offsetMax = new Vector2(-32f, -NotesTextTopInset);
            notesTextLabel = textObject.GetComponent<Text>();
            StyleText(notesTextLabel, 34, FontStyle.Italic, TextAnchor.UpperLeft);
            notesTextLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            notesTextLabel.verticalOverflow = VerticalWrapMode.Truncate;
            RefreshPlayerNotesText();
        }


        private void CreateClaimsScrollView(Transform parent)
        {
            GameObject viewportObject = CreateUiObject(
                ViewportObjectName,
                parent,
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(ViewportSideInset, ClaimsViewportBottomInset);
            viewportRect.offsetMax = new Vector2(-ViewportSideInset, -ViewportTopInset);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = CreateUiObject(ContentObjectName, viewportRect);
            contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            claimsScrollRect = viewportObject.GetComponent<ScrollRect>();
            claimsScrollRect.content = contentRect;
            claimsScrollRect.viewport = viewportRect;
            claimsScrollRect.horizontal = false;
            claimsScrollRect.vertical = true;
            claimsScrollRect.inertia = false;
            claimsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            claimsScrollRect.scrollSensitivity = 40f;
        }

        private ClaimRow CreateClaimRow(int index, string claimId, string claimText)
        {
            GameObject rowObject = CreateUiObject("Claim Row " + index, contentRect);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * GetClaimRowHeight());
            rowRect.sizeDelta = new Vector2(0f, GetClaimRowHeight());

            GameObject markObject = CreateUiObject("Claim Mark", rowRect);
            RectTransform markRect = markObject.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0f, 0.5f);
            markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0.5f, 0.5f);
            markRect.anchoredPosition = new Vector2(42f, 0f);
            markRect.sizeDelta = new Vector2(MarkSize, MarkSize);

            ClaimMarkVisual mark = new ClaimMarkVisual
            {
                Root = markObject,
                FirstStroke = CreateMarkStroke(markRect, "Mark Stroke A"),
                SecondStroke = CreateMarkStroke(markRect, "Mark Stroke B")
            };
            mark.SetMark(ClaimMark.None);

            GameObject textObject = CreateUiObject("Claim Text", rowRect, typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(RowTextLeftInset, RowTextVerticalInset);
            textRect.offsetMax = new Vector2(-RowTextRightInset, -RowTextVerticalInset);

            Text claimLabel = textObject.GetComponent<Text>();
            StyleText(claimLabel, claimFontSize, FontStyle.Italic, TextAnchor.MiddleLeft);
            claimLabel.text = claimText;

            GameObject ruleObject = CreateUiObject("Row Rule", rowRect, typeof(Image));
            RectTransform ruleRect = ruleObject.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0f);
            ruleRect.anchoredPosition = Vector2.zero;
            ruleRect.sizeDelta = new Vector2(-ViewportSideInset, 1.5f);
            Image ruleImage = ruleObject.GetComponent<Image>();
            ruleImage.color = RuleColor;
            ruleImage.raycastTarget = false;

            return new ClaimRow { ClaimId = claimId ?? string.Empty, Root = rowObject, Mark = mark };
        }

        private Image CreateMarkStroke(Transform parent, string objectName)
        {
            GameObject strokeObject = CreateUiObject(objectName, parent, typeof(Image));
            RectTransform strokeRect = strokeObject.GetComponent<RectTransform>();
            strokeRect.anchorMin = new Vector2(0.5f, 0.5f);
            strokeRect.anchorMax = new Vector2(0.5f, 0.5f);
            strokeRect.pivot = new Vector2(0.5f, 0.5f);
            Image strokeImage = strokeObject.GetComponent<Image>();
            strokeImage.color = MatchColor;
            strokeImage.raycastTarget = false;
            return strokeImage;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent, params Type[] componentTypes)
        {
            Type[] types = new Type[componentTypes.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(componentTypes, 0, types, 1, componentTypes.Length);
            GameObject uiObject = new GameObject(objectName, types);
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private void StyleText(Text text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            text.font = runtimeFont;
            text.fontSize = Mathf.Max(1, fontSize);
            text.fontStyle = fontStyle;
            text.color = InkColor;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
        }

        private void SetClaimMark(string claimId, ClaimMark mark)
        {
            if (string.IsNullOrEmpty(claimId))
                return;

            for (int rowIndex = 0; rowIndex < claimRows.Count; rowIndex++)
            {
                ClaimRow row = claimRows[rowIndex];
                if (string.Equals(row.ClaimId, claimId, StringComparison.Ordinal))
                    row.Mark.SetMark(mark);
            }
        }

        private void RemoveClaimRows()
        {
            for (int rowIndex = 0; rowIndex < claimRows.Count; rowIndex++)
            {
                GameObject rowObject = claimRows[rowIndex].Root;
                if (rowObject != null)
                    Destroy(rowObject);
            }
            claimRows.Clear();
        }

        private float GetClaimRowHeight()
        {
            return Mathf.Clamp(rowHeight, MinimumClaimRowHeight, MaximumClaimRowHeight);
        }

        private void SetVisible(bool visible)
        {
            if (canvasObject != null && canvasObject.activeSelf != visible)
                canvasObject.SetActive(visible);
        }

        private static void SetFullRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
