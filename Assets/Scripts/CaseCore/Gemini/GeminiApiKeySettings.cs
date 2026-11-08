using UnityEngine;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>
    /// Resolves GEMINI_API_KEY for Editor / local fallback only.
    /// Shipped builds should use <see cref="CaseClosed.Networking.ApiProxySettings"/> instead.
    /// Order: environment variable, PlayerPrefs, then this asset's placeholder field.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GeminiApiKeySettings",
        menuName = "Case Closed/Gemini API Key Settings")]
    public sealed class GeminiApiKeySettings : ScriptableObject
    {
        public const string PlayerPrefsKey = "GEMINI_API_KEY";
        public const string EnvironmentVariableName = "GEMINI_API_KEY";

        [Header("Placeholder (Editor / local only)")]
        [Tooltip("Optional local fallback. Leave empty in shared assets. Prefer env or PlayerPrefs. Never used in player builds when proxy is configured.")]
        [SerializeField] private string geminiApiKeyPlaceholder = "";

        [Header("Model")]
        [Tooltip("Fast Gemini model id for short interrogation replies.")]
        [SerializeField] private string modelId = "gemini-2.0-flash";

        [Tooltip("Hard cap on generated tokens. Keep low for snappy replies.")]
        [SerializeField, Range(40, 160)] private int maxOutputTokens = 90;

        [SerializeField, Range(0f, 1.5f)] private float temperature = 0.6f;

        public string ModelId { get { return string.IsNullOrWhiteSpace(modelId) ? "gemini-2.0-flash" : modelId.Trim(); } }
        public int MaxOutputTokens { get { return Mathf.Clamp(maxOutputTokens, 40, 160); } }
        public float Temperature { get { return Mathf.Clamp(temperature, 0f, 1.5f); } }

        /// <summary>True when a direct (non-proxy) key is available. Prefer proxy in player builds.</summary>
        public bool HasDirectApiKey { get { return !string.IsNullOrWhiteSpace(ResolveDirectApiKey()); } }

        /// <summary>Back-compat alias for HasDirectApiKey.</summary>
        public bool HasApiKey { get { return HasDirectApiKey; } }

        /// <summary>Resolves a direct API key. Never logs the value. Editor / local only.</summary>
        public string ResolveDirectApiKey()
        {
            string fromEnv = System.Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv.Trim();

            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                string fromPrefs = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(fromPrefs))
                    return fromPrefs.Trim();
            }

            if (!string.IsNullOrWhiteSpace(geminiApiKeyPlaceholder))
                return geminiApiKeyPlaceholder.Trim();

            return string.Empty;
        }

        /// <summary>Back-compat alias for ResolveDirectApiKey.</summary>
        public string ResolveApiKey()
        {
            return ResolveDirectApiKey();
        }

        /// <summary>Stores a key in PlayerPrefs for local Play Mode tests. Do not commit the value.</summary>
        public static void SavePlayerPrefsKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            }
            else
            {
                PlayerPrefs.SetString(PlayerPrefsKey, apiKey.Trim());
            }

            PlayerPrefs.Save();
        }
    }
}
