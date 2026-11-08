using UnityEngine;

namespace CaseClosed.Networking
{
    /// <summary>
    /// Cloud API proxy config for shipped builds.
    /// Keys stay on the host (Netlify); the game only knows ProxyBaseUrl + a shared app token.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ApiProxySettings",
        menuName = "Case Closed/API Proxy Settings")]
    public sealed class ApiProxySettings : ScriptableObject
    {
        public const string PlayerPrefsBaseUrlKey = "CASE_CLOSED_PROXY_BASE_URL";
        public const string PlayerPrefsAppTokenKey = "CASE_CLOSED_APP_TOKEN";
        public const string DefaultProxyBaseUrl = "https://ubcaseclosed.netlify.app";

        [Header("Proxy")]
        [Tooltip("Public HTTPS base URL of the Netlify site (no trailing slash).")]
        [SerializeField] private string proxyBaseUrl = DefaultProxyBaseUrl;

        [Tooltip("Shared app token matching Netlify env CASE_CLOSED_APP_TOKEN. Not a provider API key.")]
        [SerializeField] private string appToken = "";

        [Header("Behaviour")]
        [Tooltip("Player builds always prefer the proxy when a base URL is set.")]
        [SerializeField] private bool preferProxyInPlayer = true;

        [Tooltip("In the Editor, allow direct GEMINI_API_KEY / ELEVENLABS_API_KEY when proxy is down or unset.")]
        [SerializeField] private bool allowDirectKeysInEditor = true;

        public string ProxyBaseUrl
        {
            get
            {
                if (PlayerPrefs.HasKey(PlayerPrefsBaseUrlKey))
                {
                    string fromPrefs = PlayerPrefs.GetString(PlayerPrefsBaseUrlKey, string.Empty);
                    if (!string.IsNullOrWhiteSpace(fromPrefs))
                        return TrimBase(fromPrefs);
                }

                return TrimBase(proxyBaseUrl);
            }
        }

        public string AppToken
        {
            get
            {
                if (PlayerPrefs.HasKey(PlayerPrefsAppTokenKey))
                {
                    string fromPrefs = PlayerPrefs.GetString(PlayerPrefsAppTokenKey, string.Empty);
                    if (!string.IsNullOrWhiteSpace(fromPrefs))
                        return fromPrefs.Trim();
                }

                return string.IsNullOrWhiteSpace(appToken) ? string.Empty : appToken.Trim();
            }
        }

        public bool PreferProxyInPlayer { get { return preferProxyInPlayer; } }
        public bool AllowDirectKeysInEditor { get { return allowDirectKeysInEditor; } }

        /// <summary>True when a non-empty proxy base URL is configured.</summary>
        public bool HasProxyUrl
        {
            get { return !string.IsNullOrWhiteSpace(ProxyBaseUrl); }
        }

        /// <summary>
        /// Player: use proxy when URL is set.
        /// Editor: use proxy when URL is set, unless Magdy is relying on local keys only (URL empty).
        /// </summary>
        public bool ShouldUseProxy
        {
            get
            {
                if (!HasProxyUrl)
                    return false;

#if UNITY_EDITOR
                return true;
#else
                return preferProxyInPlayer;
#endif
            }
        }

        /// <summary>Editor-only escape hatch for direct provider keys.</summary>
        public bool ShouldAllowDirectKeys
        {
            get
            {
#if UNITY_EDITOR
                return allowDirectKeysInEditor;
#else
                return false;
#endif
            }
        }

        /// <summary>Legacy ask-proxy URL. Gameplay no longer calls this by default (ScenarioDialogueClient).</summary>
        public string GeminiProxyUrl
        {
            get { return ProxyBaseUrl + "/api/gemini"; }
        }

        /// <summary>ElevenLabs TTS proxy — still used by voice clients.</summary>
        public string TtsProxyUrl
        {
            get { return ProxyBaseUrl + "/api/tts"; }
        }

        public static void SavePlayerPrefs(string baseUrl, string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                PlayerPrefs.DeleteKey(PlayerPrefsBaseUrlKey);
            else
                PlayerPrefs.SetString(PlayerPrefsBaseUrlKey, TrimBase(baseUrl));

            if (string.IsNullOrWhiteSpace(token))
                PlayerPrefs.DeleteKey(PlayerPrefsAppTokenKey);
            else
                PlayerPrefs.SetString(PlayerPrefsAppTokenKey, token.Trim());

            PlayerPrefs.Save();
        }

        private static string TrimBase(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;
            return url.Trim().TrimEnd('/');
        }
    }
}
