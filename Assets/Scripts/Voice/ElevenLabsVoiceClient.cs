using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CaseClosed.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Fast ElevenLabs TTS for suspect interrogation lines.
    /// Player builds call the cloud proxy; Editor may fall back to a direct ELEVENLABS_API_KEY.
    /// Uses Flash model + low-latency output format, cancels in-flight work
    /// when a new question arrives, and never blocks gameplay (coroutines + AudioSource).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevenLabsVoiceClient : MonoBehaviour, ISuspectSpeech
    {
        private const string StreamUrlFormat =
            "https://api.elevenlabs.io/v1/text-to-speech/{0}/stream?output_format={1}&optimize_streaming_latency={2}";

        [Header("Proxy")]
        [Tooltip("Cloud proxy settings (default https://ubcaseclosed.netlify.app). Required for player builds.")]
        [SerializeField] private ApiProxySettings proxySettings;

        [Header("Playback")]
        [Tooltip("AudioSource on the suspect / desk. Created on this object when empty.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Latency")]
        [Tooltip("ElevenLabs Flash model id for lowest generation latency.")]
        [SerializeField] private string modelId = "eleven_flash_v2_5";

        [Tooltip("Query output format. Lower bitrate starts sooner.")]
        [SerializeField] private string outputFormat = "mp3_22050_32";

        [Tooltip("ElevenLabs optimize_streaming_latency (0–4). Higher = faster, slightly lower quality.")]
        [SerializeField, Range(0, 4)] private int optimizeStreamingLatency = 4;

        [Tooltip("When the line is long, speak the first short sentence first so audio starts sooner.")]
        [SerializeField] private bool speakFirstSentenceFirst = true;

        [Tooltip("Character threshold before sentence chunking kicks in.")]
        [SerializeField] private int chunkThresholdChars = 72;

        [Header("Voice Settings")]
        [SerializeField, Range(0f, 1f)] private float stability = 0.35f;
        [SerializeField, Range(0f, 1f)] private float similarityBoost = 0.75f;

        [Header("Cache")]
        [Tooltip("Cache synthesized clips by voice id + text so repeated lines are instant.")]
        [SerializeField] private bool enableClipCache = true;

        [SerializeField] private int maxCachedClips = 48;

        [Header("Debug")]
        [SerializeField] private bool logSkippedMissingKey = true;
        [SerializeField] private bool logErrors = true;

        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private readonly LinkedList<string> cacheOrder = new LinkedList<string>();

        private Coroutine activeRoutine;
        private UnityWebRequest activeRequest;
        private int generation;
        private bool keyMissingLogged;
        private bool speechPlaybackActive;
        private string activeSpeechSuspectId;
        private ApiProxySettings runtimeProxyFallback;

        public void Configure(ApiProxySettings proxy)
        {
            if (proxy != null)
                proxySettings = proxy;
        }

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
                ApiProxySettings proxy = ResolveProxySettings();
                if (proxy != null && proxy.ShouldUseProxy)
                    return true;

                string unused;
                return proxy != null && proxy.ShouldAllowDirectKeys &&
                       ElevenLabsApiKey.TryGetDirect(out unused);
            }
        }

        /// <inheritdoc />
        public bool IsBusy
        {
            get { return activeRoutine != null || (audioSource != null && audioSource.isPlaying); }
        }

        private void Awake()
        {
            EnsureAudioSource();
        }

        private void OnEnable()
        {
            SuspectDialogueEvents.ReplyReady += HandleReplyReady;
        }

        private void OnDisable()
        {
            SuspectDialogueEvents.ReplyReady -= HandleReplyReady;
            CancelInFlight();
        }

        private void OnDestroy()
        {
            CancelInFlight();
            foreach (KeyValuePair<string, AudioClip> pair in clipCache)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }
            clipCache.Clear();
            cacheOrder.Clear();
        }

        /// <inheritdoc />
        public void Speak(SuspectSpeechRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Line))
                return;

            if (request.CancelPrevious)
                CancelInFlight();

            ApiProxySettings proxy = ResolveProxySettings();
            bool useProxy = proxy != null && proxy.ShouldUseProxy;
            string apiKey = null;
            if (!useProxy)
            {
                bool allowDirect = proxy == null || proxy.ShouldAllowDirectKeys;
                if (!allowDirect || !ElevenLabsApiKey.TryGetDirect(out apiKey))
                {
                    if (logSkippedMissingKey && !keyMissingLogged)
                    {
                        keyMissingLogged = true;
                        Debug.LogWarning(
                            "[ElevenLabs] Proxy/API key missing — TTS skipped. " +
                            "Set PlayerPrefs CASE_CLOSED_APP_TOKEN or ELEVENLABS_API_KEY.");
                    }
                    return;
                }
            }

            EnsureAudioSource();
            string voiceId = SuspectVoiceCatalog.Resolve(request.SuspectId, request.VoiceId);
            string cleaned = request.Line.Trim();
            string suspectId = request.SuspectId ?? string.Empty;
            generation++;
            int gen = generation;
            activeRoutine = StartCoroutine(SpeakRoutine(gen, useProxy, apiKey, voiceId, cleaned, suspectId, proxy));
        }

        /// <summary>Speak using resolved catalog / record voice id.</summary>
        public void Speak(string suspectId, string line, string voiceId = null)
        {
            Speak(new SuspectSpeechRequest
            {
                SuspectId = suspectId,
                Line = line,
                VoiceId = voiceId,
                CancelPrevious = true
            });
        }

        /// <inheritdoc />
        public void CancelInFlight()
        {
            generation++;

            if (activeRequest != null)
            {
                try
                {
                    activeRequest.Abort();
                }
                catch (Exception)
                {
                    // Abort can throw if already disposed; ignore.
                }

                try
                {
                    activeRequest.Dispose();
                }
                catch (Exception)
                {
                    // Already disposed during FinishDownload / SpeakRoutine finally.
                }

                activeRequest = null;
            }

            if (activeRoutine != null)
            {
                // StopCoroutine runs SpeakRoutine's finally; generation bump makes ClearInFlight a no-op.
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();

            // finally may already have published; idempotent via speechPlaybackActive.
            NotifySpeechEnded();
        }

        private void HandleReplyReady(SuspectSpeechRequest request)
        {
            Speak(request);
        }

        private IEnumerator SpeakRoutine(
            int gen,
            bool useProxy,
            string apiKey,
            string voiceId,
            string fullText,
            string suspectId,
            ApiProxySettings proxy)
        {
            // try/finally so errors (e.g. TTS 401), early yield break, and StopCoroutine
            // always release IsBusy — otherwise a failed request leaves activeRoutine stuck.
            try
            {
                List<string> chunks = BuildChunks(fullText);
                for (int i = 0; i < chunks.Count; i++)
                {
                    if (gen != generation)
                        yield break;

                    string chunk = chunks[i];
                    string cacheKey = voiceId + "|" + chunk;
                    AudioClip cached;
                    if (enableClipCache && clipCache.TryGetValue(cacheKey, out cached) && cached != null)
                    {
                        yield return PlayClip(gen, cached, suspectId);
                        continue;
                    }

                    byte[] audioBytes = null;
                    if (useProxy)
                        yield return DownloadSpeechViaProxy(gen, proxy, voiceId, chunk, bytes => audioBytes = bytes);
                    else
                        yield return DownloadSpeechDirect(gen, apiKey, voiceId, chunk, bytes => audioBytes = bytes);
                    if (gen != generation || audioBytes == null || audioBytes.Length == 0)
                        yield break;

                    AudioClip clip = null;
                    yield return LoadMp3Clip(gen, audioBytes, cacheKey, loaded => clip = loaded);
                    if (gen != generation || clip == null)
                        yield break;

                    if (enableClipCache)
                        StoreCache(cacheKey, clip);

                    yield return PlayClip(gen, clip, suspectId);
                }
            }
            finally
            {
                ClearInFlightIfCurrent(gen);
                NotifySpeechEnded();
            }
        }

        private IEnumerator PlayClip(int gen, AudioClip clip, string suspectId)
        {
            EnsureAudioSource();
            audioSource.mute = false;
            if (audioSource.volume <= 0.001f)
                audioSource.volume = 1f;
            audioSource.clip = clip;
            audioSource.Play();
            NotifySpeechStarted(suspectId);
            while (gen == generation && audioSource.isPlaying)
                yield return null;
        }

        private void NotifySpeechStarted(string suspectId)
        {
            if (speechPlaybackActive)
                return;

            speechPlaybackActive = true;
            activeSpeechSuspectId = suspectId ?? string.Empty;
            SuspectDialogueEvents.PublishSpeechStarted(activeSpeechSuspectId);
        }

        private void NotifySpeechEnded()
        {
            if (!speechPlaybackActive)
                return;

            speechPlaybackActive = false;
            string suspectId = activeSpeechSuspectId ?? string.Empty;
            activeSpeechSuspectId = null;
            SuspectDialogueEvents.PublishSpeechEnded(suspectId);
        }

        /// <summary>
        /// Drops busy/in-flight flags for this speak generation only.
        /// Skips when a newer Speak/Cancel already owns the slot.
        /// </summary>
        private void ClearInFlightIfCurrent(int gen)
        {
            if (gen != generation)
                return;

            if (activeRequest != null)
            {
                try
                {
                    activeRequest.Abort();
                }
                catch (Exception)
                {
                    // Abort can throw if already disposed; ignore.
                }

                try
                {
                    activeRequest.Dispose();
                }
                catch (Exception)
                {
                    // Already disposed during FinishDownload / CancelInFlight.
                }

                activeRequest = null;
            }

            activeRoutine = null;
        }

        private IEnumerator DownloadSpeechViaProxy(
            int gen,
            ApiProxySettings proxy,
            string voiceId,
            string text,
            Action<byte[]> onComplete)
        {
            string url = proxy.TtsProxyUrl;
            string body = BuildProxyRequestJson(voiceId, text);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/mpeg");
            string token = proxy.AppToken;
            if (!string.IsNullOrWhiteSpace(token))
                request.SetRequestHeader("X-Case-Closed-Token", token);
            else if (logErrors && !keyMissingLogged)
            {
                keyMissingLogged = true;
                Debug.LogWarning(
                    "[ElevenLabs] CASE_CLOSED_APP_TOKEN missing — proxy TTS will 401. " +
                    "Case Closed → API Proxy → Set PlayerPrefs (Editor menu).");
            }

            request.timeout = 12;

            activeRequest = request;
            yield return request.SendWebRequest();
            FinishDownload(gen, request, onComplete);
        }

        private IEnumerator DownloadSpeechDirect(int gen, string apiKey, string voiceId, string text, Action<byte[]> onComplete)
        {
            string url = string.Format(
                StreamUrlFormat,
                UnityWebRequest.EscapeURL(voiceId),
                UnityWebRequest.EscapeURL(outputFormat),
                optimizeStreamingLatency);

            string body = BuildDirectRequestJson(text);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", apiKey);
            request.SetRequestHeader("Accept", "audio/mpeg");
            request.timeout = 12;

            activeRequest = request;
            yield return request.SendWebRequest();
            FinishDownload(gen, request, onComplete);
        }

        private void FinishDownload(int gen, UnityWebRequest request, Action<byte[]> onComplete)
        {
            if (gen != generation)
            {
                onComplete(null);
                request.Dispose();
                if (activeRequest == request)
                    activeRequest = null;
                return;
            }

            bool failed = request.result != UnityWebRequest.Result.Success;
            if (failed)
            {
                if (logErrors)
                    Debug.LogWarning("[ElevenLabs] TTS failed: " + request.responseCode + " " + request.error);
                onComplete(null);
            }
            else
            {
                onComplete(request.downloadHandler.data);
            }

            request.Dispose();
            if (activeRequest == request)
                activeRequest = null;
        }

        private IEnumerator LoadMp3Clip(int gen, byte[] audioBytes, string cacheKey, Action<AudioClip> onComplete)
        {
            string fileName = "el_" + HashKey(cacheKey) + ".mp3";
            string path = Path.Combine(Application.temporaryCachePath, fileName);

            try
            {
                File.WriteAllBytes(path, audioBytes);
            }
            catch (Exception ex)
            {
                if (logErrors)
                    Debug.LogWarning("[ElevenLabs] Failed to write temp audio: " + ex.Message);
                onComplete(null);
                yield break;
            }

            string fileUrl = new Uri(path).AbsoluteUri;
            using (UnityWebRequest clipRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
            {
                DownloadHandlerAudioClip handler = (DownloadHandlerAudioClip)clipRequest.downloadHandler;
                handler.streamAudio = false;
                yield return clipRequest.SendWebRequest();

                if (gen != generation)
                {
                    onComplete(null);
                    yield break;
                }

                if (clipRequest.result != UnityWebRequest.Result.Success)
                {
                    if (logErrors)
                        Debug.LogWarning("[ElevenLabs] AudioClip load failed: " + clipRequest.error);
                    onComplete(null);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(clipRequest);
                if (clip != null)
                    clip.name = "ElevenLabs_" + HashKey(cacheKey);
                onComplete(clip);
            }

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // Temp cleanup is best-effort.
            }
        }

        private string BuildProxyRequestJson(string voiceId, string text)
        {
            return
                "{\"voiceId\":\"" + EscapeJson(voiceId) +
                "\",\"text\":\"" + EscapeJson(text) +
                "\",\"modelId\":\"" + EscapeJson(modelId) +
                "\",\"outputFormat\":\"" + EscapeJson(outputFormat) +
                "\",\"optimizeStreamingLatency\":" + optimizeStreamingLatency +
                ",\"stability\":" + stability.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"similarityBoost\":" + similarityBoost.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                "}";
        }

        private string BuildDirectRequestJson(string text)
        {
            // Manual JSON keeps us free of extra packages and avoids escaping surprises for short lines.
            string escapedText = EscapeJson(text);
            string escapedModel = EscapeJson(modelId);
            return
                "{\"text\":\"" + escapedText +
                "\",\"model_id\":\"" + escapedModel +
                "\",\"voice_settings\":{" +
                "\"stability\":" + stability.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"similarity_boost\":" + similarityBoost.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"use_speaker_boost\":false}}";
        }

        private List<string> BuildChunks(string text)
        {
            var chunks = new List<string>(2);
            if (!speakFirstSentenceFirst || text.Length < chunkThresholdChars)
            {
                chunks.Add(TrimToSpeakable(text));
                return chunks;
            }

            int split = FindFirstSentenceEnd(text);
            if (split <= 0 || split >= text.Length - 1)
            {
                chunks.Add(TrimToSpeakable(text));
                return chunks;
            }

            string first = TrimToSpeakable(text.Substring(0, split + 1));
            string rest = TrimToSpeakable(text.Substring(split + 1));
            if (!string.IsNullOrEmpty(first))
                chunks.Add(first);
            if (!string.IsNullOrEmpty(rest))
                chunks.Add(rest);
            if (chunks.Count == 0)
                chunks.Add(TrimToSpeakable(text));
            return chunks;
        }

        private static int FindFirstSentenceEnd(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '.' && c != '!' && c != '?')
                    continue;
                if (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]))
                    return i;
            }
            return -1;
        }

        private static string TrimToSpeakable(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            string trimmed = text.Trim();
            // Hard cap keeps Flash TTS snappy even if Gemini drifts long.
            const int maxChars = 220;
            if (trimmed.Length > maxChars)
                trimmed = trimmed.Substring(0, maxChars).TrimEnd() + "…";
            return trimmed;
        }

        private void StoreCache(string key, AudioClip clip)
        {
            if (clipCache.ContainsKey(key))
            {
                clipCache[key] = clip;
                cacheOrder.Remove(key);
                cacheOrder.AddLast(key);
                return;
            }

            while (clipCache.Count >= Mathf.Max(1, maxCachedClips) && cacheOrder.Count > 0)
            {
                string oldest = cacheOrder.First.Value;
                cacheOrder.RemoveFirst();
                AudioClip oldClip;
                if (clipCache.TryGetValue(oldest, out oldClip))
                {
                    clipCache.Remove(oldest);
                    if (oldClip != null && oldClip != audioSource.clip)
                        Destroy(oldClip);
                }
            }

            clipCache[key] = clip;
            cacheOrder.AddLast(key);
        }

        /// <summary>
        /// Prefers assigned proxy, else a runtime fallback that still hits
        /// https://ubcaseclosed.netlify.app/api/tts and reads X-Case-Closed-Token from PlayerPrefs.
        /// </summary>
        private ApiProxySettings ResolveProxySettings()
        {
            if (proxySettings != null)
                return proxySettings;

            if (runtimeProxyFallback == null)
                runtimeProxyFallback = ApiProxySettings.CreateRuntimeDefault();

            return runtimeProxyFallback;
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null)
            {
                audioSource.mute = false;
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                if (audioSource.volume <= 0.001f)
                    audioSource.volume = 1f;
                return;
            }

            // Dedicated child source so BarkPlayer / SfxPlayer Stop() cannot mute reply TTS.
            Transform existing = transform.Find("ElevenLabsAudio");
            GameObject host = existing != null ? existing.gameObject : new GameObject("ElevenLabsAudio");
            if (existing == null)
            {
                host.transform.SetParent(transform, false);
                host.transform.localPosition = Vector3.zero;
            }

            audioSource = host.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = host.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = false;
            audioSource.mute = false;
            audioSource.volume = 1f;
            audioSource.priority = 32;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            builder.Append(c);
                        break;
                }
            }
            return builder.ToString();
        }

        private static string HashKey(string value)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(16);
                for (int i = 0; i < 8 && i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
