using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CaseClosed.CaseCore.Gemini
{
    /// <summary>Minimal Unity Gemini generateContent client tuned for short interrogation replies.</summary>
    public sealed class GeminiApiClient : MonoBehaviour
    {
        [SerializeField] private GeminiApiKeySettings settings;

        /// <summary>Optional runtime settings assignment when no asset is wired in the Inspector.</summary>
        public void Configure(GeminiApiKeySettings keySettings)
        {
            settings = keySettings;
        }

        public bool HasApiKey
        {
            get { return settings != null && settings.HasApiKey; }
        }

        /// <summary>Calls Gemini. Invokes onComplete with raw model text, or onError on failure.</summary>
        public Coroutine Generate(
            string systemPrompt,
            string userPrompt,
            Action<string> onComplete,
            Action<string> onError)
        {
            return StartCoroutine(GenerateRoutine(systemPrompt, userPrompt, onComplete, onError));
        }

        private IEnumerator GenerateRoutine(
            string systemPrompt,
            string userPrompt,
            Action<string> onComplete,
            Action<string> onError)
        {
            if (settings == null)
            {
                onError?.Invoke("GeminiApiKeySettings is not assigned.");
                yield break;
            }

            string apiKey = settings.ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError?.Invoke("GEMINI_API_KEY missing.");
                yield break;
            }

            string model = settings.ModelId;
            string url = "https://generativelanguage.googleapis.com/v1beta/models/" +
                         UnityWebRequest.EscapeURL(model) +
                         ":generateContent?key=" + UnityWebRequest.EscapeURL(apiKey);

            string body = BuildRequestJson(systemPrompt, userPrompt, settings.MaxOutputTokens, settings.Temperature);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke("Gemini HTTP " + request.responseCode + ": " + request.error);
                    yield break;
                }

                string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string modelText;
                if (!TryExtractModelText(responseText, out modelText))
                {
                    onError?.Invoke("Gemini returned an empty or unreadable payload.");
                    yield break;
                }

                onComplete?.Invoke(modelText);
            }
        }

        private static string BuildRequestJson(string systemPrompt, string userPrompt, int maxTokens, float temperature)
        {
            var sb = new StringBuilder(768);
            sb.Append('{');
            sb.Append("\"system_instruction\":{\"parts\":[{\"text\":").Append(JsonString(systemPrompt)).Append("}]},");
            sb.Append("\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":").Append(JsonString(userPrompt)).Append("}]}],");
            sb.Append("\"generationConfig\":{");
            sb.Append("\"temperature\":").Append(temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"maxOutputTokens\":").Append(maxTokens).Append(',');
            sb.Append("\"responseMimeType\":\"application/json\"");
            sb.Append("}}");
            return sb.ToString();
        }

        private static string JsonString(string value)
        {
            if (value == null) value = string.Empty;
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
        }

        /// <summary>Pulls candidates[0].content.parts[0].text without a full JSON library.</summary>
        public static bool TryExtractModelText(string responseJson, out string modelText)
        {
            modelText = null;
            if (string.IsNullOrWhiteSpace(responseJson)) return false;

            const string marker = "\"text\"";
            int index = responseJson.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return false;

            int colon = responseJson.IndexOf(':', index + marker.Length);
            if (colon < 0) return false;

            int startQuote = responseJson.IndexOf('"', colon + 1);
            if (startQuote < 0) return false;

            var sb = new StringBuilder();
            for (int i = startQuote + 1; i < responseJson.Length; i++)
            {
                char c = responseJson[i];
                if (c == '\\' && i + 1 < responseJson.Length)
                {
                    char next = responseJson[i + 1];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 5 < responseJson.Length)
                            {
                                string hex = responseJson.Substring(i + 2, 4);
                                int code;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                                    sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            sb.Append(next);
                            break;
                    }

                    i++;
                    continue;
                }

                if (c == '"') break;
                sb.Append(c);
            }

            modelText = sb.ToString().Trim();
            return !string.IsNullOrWhiteSpace(modelText);
        }

        /// <summary>Parses the compact {line,emotion,slip} schema Gemini is instructed to return.</summary>
        public static bool TryParseSuspectReply(string modelText, out GeminiSuspectReply reply)
        {
            reply = null;
            if (string.IsNullOrWhiteSpace(modelText)) return false;

            string json = ExtractJsonObject(modelText);
            if (string.IsNullOrWhiteSpace(json)) return false;

            string line = ExtractJsonStringField(json, "line");
            string emotion = ExtractJsonStringField(json, "emotion");
            bool slip = ExtractJsonBoolField(json, "slip");

            if (string.IsNullOrWhiteSpace(line)) return false;

            reply = GeminiSuspectReply.CreateFallback(TrimToShortLine(line), emotion, slip);
            return true;
        }

        public static string TrimToShortLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "...";
            string trimmed = line.Trim();
            if (trimmed.Length <= 160) return trimmed;
            return trimmed.Substring(0, 157).TrimEnd() + "...";
        }

        private static string ExtractJsonObject(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return text.Substring(start, end - start + 1);
        }

        private static string ExtractJsonStringField(string json, string fieldName)
        {
            string marker = "\"" + fieldName + "\"";
            int index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            int colon = json.IndexOf(':', index + marker.Length);
            if (colon < 0) return null;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;

            var sb = new StringBuilder();
            for (int p = i + 1; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length)
                {
                    sb.Append(json[p + 1] == 'n' ? '\n' : json[p + 1]);
                    p++;
                    continue;
                }

                if (c == '"') break;
                sb.Append(c);
            }

            return sb.ToString();
        }

        private static bool ExtractJsonBoolField(string json, string fieldName)
        {
            string marker = "\"" + fieldName + "\"";
            int index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;

            int colon = json.IndexOf(':', index + marker.Length);
            if (colon < 0) return false;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i + 4 <= json.Length && string.Compare(json, i, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
                return true;
            return false;
        }
    }
}
