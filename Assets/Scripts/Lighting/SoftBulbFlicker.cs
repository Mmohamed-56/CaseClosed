using UnityEngine;

namespace CaseClosed.Lighting
{
    /// <summary>Applies a gentle, irregular old-bulb brightness drift without horror-style sharp flickers.</summary>
    public sealed class SoftBulbFlicker : MonoBehaviour
    {
        [Header("Bulb Light")]
        [Tooltip("Point light near the ceiling fan's bulb. Automatically finds a Light on this object if empty.")]
        [SerializeField] private Light bulbLight;

        [Header("Natural Flicker")]
        [Tooltip("Minimum brightness multiplier compared with the assigned light intensity.")]
        [SerializeField, Range(0.5f, 1f)] private float minimumIntensityMultiplier = 0.94f;

        [Tooltip("Maximum brightness multiplier compared with the assigned light intensity.")]
        [SerializeField, Range(1f, 1.1f)] private float maximumIntensityMultiplier = 1.02f;

        [Tooltip("How quickly the bulb drifts between brightness samples. Lower values feel slower and softer.")]
        [SerializeField, Min(0.05f)] private float driftSpeed = 0.7f;

        [Tooltip("Small maximum hue shift from the original bulb color, in degrees.")]
        [SerializeField, Range(0f, 12f)] private float colorDriftDegrees = 2f;

        private float baseIntensity;
        private Color baseColor;
        private float noiseOffset;

        private void Awake()
        {
            if (bulbLight == null) bulbLight = GetComponent<Light>();
            if (bulbLight == null) return;

            baseIntensity = bulbLight.intensity;
            baseColor = bulbLight.color;
            noiseOffset = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            if (bulbLight == null) return;

            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * driftSpeed);
            float brightness = Mathf.Lerp(minimumIntensityMultiplier, maximumIntensityMultiplier, noise);
            bulbLight.intensity = baseIntensity * brightness;

            float subtleWarmShift = (noise - 0.5f) * colorDriftDegrees;
            Color.RGBToHSV(baseColor, out float hue, out float saturation, out float value);
            Color shiftedColor = Color.HSVToRGB(Mathf.Repeat(hue + subtleWarmShift / 360f, 1f), saturation, value);
            bulbLight.color = new Color(
                Mathf.Lerp(baseColor.r, shiftedColor.r, 0.18f),
                Mathf.Lerp(baseColor.g, shiftedColor.g, 0.18f),
                Mathf.Lerp(baseColor.b, shiftedColor.b, 0.18f),
                baseColor.a);
        }
    }
}
