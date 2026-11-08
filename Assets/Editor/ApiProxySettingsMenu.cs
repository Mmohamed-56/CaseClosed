using CaseClosed.Networking;
using UnityEditor;
using UnityEngine;

namespace CaseClosed.EditorTools
{
    /// <summary>Local helpers so Magdy can point builds at the cloud proxy without editing scripts.</summary>
    public static class ApiProxySettingsMenu
    {
        private const string MenuRoot = "Case Closed/API Proxy/";

        [MenuItem(MenuRoot + "Set Proxy Base URL + App Token (PlayerPrefs)...")]
        private static void SetProxyPrefs()
        {
            ScriptableWizard.DisplayWizard<ProxyWizard>("API Proxy Settings", "Save");
        }

        [MenuItem(MenuRoot + "Clear Proxy PlayerPrefs")]
        private static void ClearProxyPrefs()
        {
            ApiProxySettings.SavePlayerPrefs(null, null);
            EditorUtility.DisplayDialog("API Proxy", "Cleared ProxyBaseUrl and AppToken PlayerPrefs.", "OK");
        }

        [MenuItem(MenuRoot + "Create ApiProxySettings Asset")]
        private static void CreateSettingsAsset()
        {
            const string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ApiProxySettings.asset");
            ApiProxySettings asset = ScriptableObject.CreateInstance<ApiProxySettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private sealed class ProxyWizard : ScriptableWizard
        {
            public string proxyBaseUrl = ApiProxySettings.DefaultProxyBaseUrl;
            [Tooltip("Must match Netlify env CASE_CLOSED_APP_TOKEN. Not a Gemini/ElevenLabs key.")]
            public string appToken = "";

            private void OnWizardCreate()
            {
                ApiProxySettings.SavePlayerPrefs(proxyBaseUrl, appToken);
                EditorUtility.DisplayDialog(
                    "API Proxy",
                    "Saved ProxyBaseUrl / AppToken to PlayerPrefs for this machine.",
                    "OK");
            }
        }
    }
}
