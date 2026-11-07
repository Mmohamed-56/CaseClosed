using System;
using System.IO;
using UnityEngine;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Resolves the ElevenLabs API key without hardcoding.
    /// Order: process env → user env → machine env → optional local key file (gitignored).
    /// Missing key is silent: TTS is skipped, gameplay continues.
    /// </summary>
    public static class ElevenLabsApiKey
    {
        public const string EnvVarName = "ELEVENLABS_API_KEY";

        /// <summary>Optional local fallback path relative to the project (Editor) or persistentDataPath (player).</summary>
        public const string LocalKeyFileName = "elevenlabs.key";

        /// <summary>Attempts to read a non-empty API key. Returns false when none is configured.</summary>
        public static bool TryGet(out string apiKey)
        {
            apiKey = ReadEnvironment(EnvVarName);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = apiKey.Trim();
                return true;
            }

            apiKey = ReadLocalKeyFile();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = apiKey.Trim();
                return true;
            }

            apiKey = null;
            return false;
        }

        private static string ReadEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
        }

        private static string ReadLocalKeyFile()
        {
            try
            {
#if UNITY_EDITOR
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string projectKeyPath = Path.Combine(projectRoot, LocalKeyFileName);
                if (File.Exists(projectKeyPath))
                    return File.ReadAllText(projectKeyPath);
#endif
                string persistentPath = Path.Combine(Application.persistentDataPath, LocalKeyFileName);
                if (File.Exists(persistentPath))
                    return File.ReadAllText(persistentPath);
            }
            catch (Exception)
            {
                // Silent: missing or unreadable key must never break interrogation.
            }

            return null;
        }
    }
}
