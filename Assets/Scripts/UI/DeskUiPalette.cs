using UnityEngine;
using UnityEngine.UI;

namespace CaseClosed.UI
{
    /// <summary>
    /// Early-2000s UB campus police desk colors for Case Closed HUD.
    /// Navy + manila + blotter green — no purple / glow defaults.
    /// </summary>
    public static class DeskUiPalette
    {
        public static readonly Color Navy = new Color(0.105f, 0.20f, 0.325f, 1f);
        public static readonly Color NavyDark = new Color(0.07f, 0.13f, 0.22f, 0.96f);
        public static readonly Color Manila = new Color(0.86f, 0.80f, 0.68f, 0.97f);
        public static readonly Color ManilaLight = new Color(0.93f, 0.88f, 0.78f, 1f);
        public static readonly Color Ink = new Color(0.12f, 0.10f, 0.08f, 1f);
        public static readonly Color InkMuted = new Color(0.28f, 0.24f, 0.18f, 1f);
        public static readonly Color Blotter = new Color(0.22f, 0.36f, 0.26f, 1f);
        public static readonly Color StampHold = new Color(0.55f, 0.14f, 0.12f, 1f);
        public static readonly Color StampRelease = new Color(0.28f, 0.40f, 0.26f, 1f);
        public static readonly Color Charge = new Color(0.14f, 0.22f, 0.36f, 1f);
        public static readonly Color TranscriptBg = new Color(0.96f, 0.93f, 0.86f, 1f);
        public static readonly Color TabIdle = new Color(0.78f, 0.72f, 0.60f, 1f);
        public static readonly Color TabActive = new Color(0.92f, 0.87f, 0.76f, 1f);
        public static readonly Color PanelBorder = new Color(0.35f, 0.28f, 0.18f, 0.85f);
        public static readonly Color White = new Color(0.97f, 0.95f, 0.90f, 1f);

        public static Font ResolveFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        public static void StyleLabel(Text text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft)
        {
            if (text == null) return;
            text.font = ResolveFont();
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
                image.color = fill;
            if (label != null)
                StyleLabel(label, 15, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (button == null) return;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
    }
}
