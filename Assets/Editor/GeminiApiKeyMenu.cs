using CaseClosed.CaseCore.Gemini;
using UnityEditor;
using UnityEngine;

namespace CaseClosed.EditorTools
{
    /// <summary>Local-only helpers so Magdy can store GEMINI_API_KEY without editing scripts.</summary>
    public static class GeminiApiKeyMenu
    {
        private const string MenuRoot = "Case Closed/Gemini/";

        [MenuItem(MenuRoot + "Set API Key (PlayerPrefs)...")]
        private static void SetApiKey()
        {
            ScriptableWizard.DisplayWizard<GeminiKeyWizard>("Set GEMINI_API_KEY", "Save");
        }

        [MenuItem(MenuRoot + "Clear API Key (PlayerPrefs)")]
        private static void ClearApiKey()
        {
            GeminiApiKeySettings.SavePlayerPrefsKey(null);
            EditorUtility.DisplayDialog("Gemini API Key", "Cleared PlayerPrefs key GEMINI_API_KEY.", "OK");
        }

        [MenuItem(MenuRoot + "Create Key Settings Asset")]
        private static void CreateSettingsAsset()
        {
            const string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/GeminiApiKeySettings.asset");
            GeminiApiKeySettings asset = ScriptableObject.CreateInstance<GeminiApiKeySettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private sealed class GeminiKeyWizard : ScriptableWizard
        {
            [Tooltip("Paste your Gemini API key. Stored in PlayerPrefs only. Never commit this value.")]
            public string geminiApiKey = "";

            private void OnWizardCreate()
            {
                GeminiApiKeySettings.SavePlayerPrefsKey(geminiApiKey);
                EditorUtility.DisplayDialog(
                    "Gemini API Key",
                    string.IsNullOrWhiteSpace(geminiApiKey)
                        ? "Cleared PlayerPrefs key GEMINI_API_KEY."
                        : "Saved GEMINI_API_KEY to PlayerPrefs for this machine.",
                    "OK");
            }
        }
    }
}
