using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Early-2000s UB campus PD desk diegesis for Case Closed HUD.
    /// Clean manila folder / blotter / rubber-stamp — stamped type, hairline rules.
    /// No purple SaaS, no Inter, no rounded-full pills, no busy paper noise.
    /// Fonts: Resources/Fonts/Boldonse-Regular (body/UI), HeadCapitalBoldGrunge (stamp titles).
    /// </summary>
    public static class DeskUiPalette
    {
        // Records-site aligned PD blue + manila folder stock.
        public static readonly Color Navy = new Color(0.165f, 0.290f, 0.431f, 1f);       // #2a4a6e
        public static readonly Color NavyDark = new Color(0.102f, 0.188f, 0.282f, 0.98f); // #1a3048
        public static readonly Color Manila = new Color(0.90f, 0.84f, 0.70f, 0.98f);      // folder face
        public static readonly Color ManilaLight = new Color(0.96f, 0.93f, 0.86f, 1f);    // inner leaf / off-white
        public static readonly Color Kraft = new Color(0.78f, 0.68f, 0.48f, 1f);          // tab stock
        public static readonly Color Ink = new Color(0.10f, 0.10f, 0.09f, 1f);
        public static readonly Color InkMuted = new Color(0.29f, 0.29f, 0.26f, 1f);
        public static readonly Color Blotter = new Color(0.18f, 0.32f, 0.24f, 1f);
        public static readonly Color StampHold = new Color(0.54f, 0.125f, 0.125f, 1f);
        public static readonly Color StampRelease = new Color(0.18f, 0.42f, 0.18f, 1f);
        public static readonly Color Charge = new Color(0.10f, 0.20f, 0.35f, 1f);
        public static readonly Color TranscriptBg = new Color(0.97f, 0.94f, 0.84f, 1f);
        public static readonly Color TabIdle = new Color(0.78f, 0.68f, 0.48f, 1f);
        public static readonly Color TabActive = new Color(0.96f, 0.93f, 0.86f, 1f);
        public static readonly Color TagIdle = new Color(0.78f, 0.68f, 0.48f, 1f);
        public static readonly Color TagActive = new Color(0.96f, 0.93f, 0.86f, 1f);
        public static readonly Color PanelBorder = new Color(0.28f, 0.20f, 0.10f, 0.95f);
        public static readonly Color Hairline = new Color(0.35f, 0.28f, 0.16f, 0.85f);
        public static readonly Color HairlineSoft = new Color(0.50f, 0.42f, 0.28f, 0.45f);
        public static readonly Color AccentOlive = new Color(0.54f, 0.60f, 0.29f, 1f);
        public static readonly Color White = new Color(0.97f, 0.96f, 0.94f, 1f);

        public enum TypeRole
        {
            Body,
            Ui,
            Stamp
        }

        private static Font cachedBodyFont;
        private static Font cachedStampFont;
        private static Font cachedUiFont;
        private static Sprite cachedWhiteSprite;
        private static Sprite cachedRuleSprite;
        private static bool fontsResolved;

        public static Font ResolveFont()
        {
            EnsureFonts();
            return cachedUiFont != null ? cachedUiFont : cachedBodyFont;
        }

        public static Font ResolveFont(TypeRole role)
        {
            EnsureFonts();
            switch (role)
            {
                case TypeRole.Stamp:
                    return cachedStampFont != null ? cachedStampFont : ResolveFont();
                case TypeRole.Body:
                    return cachedBodyFont != null ? cachedBodyFont : ResolveFont();
                default:
                    return ResolveFont();
            }
        }

        public static Sprite WhiteSprite()
        {
            if (cachedWhiteSprite != null) return cachedWhiteSprite;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply(false, true);
            cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            cachedWhiteSprite.name = "DeskUiWhite";
            return cachedWhiteSprite;
        }

        public static Sprite RuleSprite()
        {
            if (cachedRuleSprite != null) return cachedRuleSprite;
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            cachedRuleSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            cachedRuleSprite.name = "DeskUiRule";
            return cachedRuleSprite;
        }

        public static void StyleLabel(Text text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft)
        {
            StyleLabel(text, size, color, TypeRole.Ui, style, align);
        }

        public static void StyleLabel(Text text, int size, Color color, TypeRole role, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft)
        {
            if (text == null) return;
            text.font = ResolveFont(role);
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        public static void StyleButton(Button button, Image image, Text label, Color fill, Color textColor)
        {
            if (image != null)
            {
                image.sprite = WhiteSprite();
                image.type = Image.Type.Simple;
                image.color = fill;
            }
            if (label != null)
                StyleLabel(label, 14, textColor, TypeRole.Stamp, FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplySquareButtonColors(button);
        }

        /// <summary>Manila folder tab — kraft idle / light leaf active. Square, not pill.</summary>
        public static void StyleFolderTag(Button button, bool active)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = WhiteSprite();
                image.type = Image.Type.Simple;
                image.color = active ? TabActive : TagIdle;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                StyleLabel(label, 12, Ink, TypeRole.Stamp, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (!string.IsNullOrEmpty(label.text))
                    label.text = label.text.ToUpperInvariant();
            }

            ApplySquareButtonColors(button);
        }

        /// <summary>Rubber-stamp title button (HOLD / RELEASE / ASK / FILE).</summary>
        public static void StyleStampButton(Button button, Image image, Text label, Color ink, bool filled)
        {
            if (image != null)
            {
                image.sprite = WhiteSprite();
                image.type = Image.Type.Simple;
                image.color = filled ? ink : new Color(ink.r, ink.g, ink.b, 0.14f);
            }
            if (label != null)
            {
                StyleLabel(label, 15, filled ? White : ink, TypeRole.Stamp, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (!string.IsNullOrEmpty(label.text))
                    label.text = label.text.ToUpperInvariant();
            }
            ApplySquareButtonColors(button);
        }

        /// <summary>Form row / collect slip — clean manila leaf, body type, square corners.</summary>
        public static void StyleFormRow(Button button, Image image, Text label, Color fill, Color textColor, bool interactable)
        {
            if (image != null)
            {
                image.sprite = WhiteSprite();
                image.type = Image.Type.Simple;
                image.color = interactable ? fill : new Color(fill.r * 0.94f, fill.g * 0.94f, fill.b * 0.94f, 0.85f);
            }
            if (label != null)
                StyleLabel(label, 13, textColor, TypeRole.Body, FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplySquareButtonColors(button);
        }

        public static void ApplyFill(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;
        }

        public static Image AddHairline(Transform parent, string name, float yFromTop, float height = 1.5f, bool soft = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.sizeDelta = new Vector2(-16f, height);
            Image image = go.GetComponent<Image>();
            image.sprite = RuleSprite();
            image.color = soft ? HairlineSoft : Hairline;
            image.raycastTarget = false;
            return image;
        }

        public static void AddFolderFrame(Transform parent, float inset = 3f, float thickness = 1.5f)
        {
            CreateEdge(parent, "FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(inset, -inset), new Vector2(-inset, -inset - thickness));
            CreateEdge(parent, "FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(inset, inset + thickness), new Vector2(-inset, inset));
            CreateEdge(parent, "FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(inset, inset), new Vector2(inset + thickness, -inset));
            CreateEdge(parent, "FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-inset - thickness, inset), new Vector2(-inset, -inset));
        }

        public static void AddAccentStripe(Transform parent, Color color, float width = 3f)
        {
            GameObject go = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);
            Image image = go.GetComponent<Image>();
            image.sprite = RuleSprite();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = go.GetComponent<Image>();
            image.sprite = RuleSprite();
            image.color = PanelBorder;
            image.raycastTarget = false;
        }

        private static void ApplySquareButtonColors(Button button)
        {
            if (button == null) return;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.95f, 0.95f);
            colors.pressedColor = new Color(0.90f, 0.86f, 0.76f, 1f);
            colors.disabledColor = new Color(0.72f, 0.70f, 0.64f, 0.55f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
        }

        private static void EnsureFonts()
        {
            if (fontsResolved) return;
            fontsResolved = true;

            // Magdy drops + Resources copies (Unity Font importer — no TMP package in this project).
            cachedStampFont = Resources.Load<Font>("Fonts/HeadCapitalBoldGrunge")
                              ?? Resources.Load<Font>("Fonts/Stamp");
            cachedBodyFont = Resources.Load<Font>("Fonts/Boldonse-Regular")
                             ?? Resources.Load<Font>("Fonts/Boldonse")
                             ?? Resources.Load<Font>("Fonts/Body");
            cachedUiFont = cachedBodyFont
                           ?? Resources.Load<Font>("Fonts/UI");

            if (cachedStampFont == null)
                cachedStampFont = CreateOsFont(new[] { "Lucida Console", "Courier New", "Consolas" });
            if (cachedBodyFont == null)
                cachedBodyFont = CreateOsFont(new[] { "Georgia", "Times New Roman", "Cambria" });
            if (cachedUiFont == null)
                cachedUiFont = cachedBodyFont ?? CreateOsFont(new[] { "Tahoma", "Segoe UI", "Verdana" });

            if (cachedUiFont == null)
            {
                cachedUiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (cachedBodyFont == null) cachedBodyFont = cachedUiFont;
            if (cachedStampFont == null) cachedStampFont = cachedUiFont;
        }

        private static Font CreateOsFont(string[] names)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(names, 16);
                if (font != null && font.fontNames != null && font.fontNames.Length > 0)
                    return font;
            }
            catch
            {
                // OS font unavailable.
            }
            return null;
        }
    }
}
