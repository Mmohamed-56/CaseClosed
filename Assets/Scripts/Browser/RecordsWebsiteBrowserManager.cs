using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CaseClosed.CaseCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VoltstroStudios.UnityWebBrowser.Communication;
using VoltstroStudios.UnityWebBrowser.Core;
using VoltstroStudios.UnityWebBrowser.Core.Engines;
using VoltstroStudios.UnityWebBrowser.Input;
using VoltstroStudios.UnityWebBrowser.Shared;
using VoltstroStudios.UnityWebBrowser.Shared.Core;
using VoltstroStudios.UnityWebBrowser.Shared.Events;

namespace CaseClosed.Browser
{
    /// <summary>Displays the records website on the monitor and routes its evidence callbacks into the active case.</summary>
    [DisallowMultipleComponent]
    public sealed class RecordsWebsiteBrowserManager : BaseUwbClientManager
    {
        private const string RecordsWebsiteUrl = "https://ubcaseclosed.netlify.app/index.html";
        private const string EvidenceMethodName = "sendEvidenceRecordId";
        private const string NavigateMethodName = "navigateFromToolbar";
        private const string BackMethodName = "goBackFromToolbar";
        private const string ForwardMethodName = "goForwardFromToolbar";
        private const string ReloadMethodName = "reloadFromToolbar";
        private const string CefEngineAppName = "UnityWebBrowser.Engine.Cef";
        private const string WindowsCefEngineEditorPath = "Packages/dev.voltstro.unitywebbrowser.engine.cef.win.x64/Engine~/";
        private const int BrowserScrollScale = 120;
        private const float BrowserScrollSensitivity = 0.2f;
        private const int BrowserTcpInPort = 5575;
        private const int BrowserTcpOutPort = 5576;
        private const int CefStartupTimeoutMilliseconds = 15000;

        [Header("Case Website")]
        [SerializeField] private CaseWebsiteBridge caseWebsiteBridge;

        [Header("Browser Display")]
        [SerializeField] private MeshRenderer displaySurfaceRenderer;
        [SerializeField] private MeshCollider displaySurfaceCollider;
        [SerializeField] private Camera displayInputCamera;
        [SerializeField] private bool flipBrowserTextureVertically = true;

        private Material originalDisplayMaterial;
        private Material runtimeDisplayMaterial;
        private string lastLoadedCaseHashFragment;
        private int browserConnectedPending;
        private int browserLoadFinishedPending;
        private bool websiteToolbarInstalled;
        private float nextToolbarInstallTime;
        private readonly ConcurrentQueue<string> pendingEvidenceRecordIds = new ConcurrentQueue<string>();
        private readonly List<WindowsKey> keyboardKeysDown = new List<WindowsKey>();
        private readonly List<WindowsKey> keyboardKeysUp = new List<WindowsKey>();
        private string pendingKeyboardText = string.Empty;
        private Keyboard subscribedKeyboard;
        private bool displayInputEnabled;
        private bool browserKeyboardFocused;
        private bool leftMousePressedOnDisplay;
        private bool rightMousePressedOnDisplay;
        private bool middleMousePressedOnDisplay;
        private bool hasLastBrowserMousePosition;
        private Vector2 lastBrowserMousePosition;
        private int lastBrowserFrameProcessed = -1;

        private void Awake()
        {
            if (caseWebsiteBridge == null)
                caseWebsiteBridge = GetComponent<CaseWebsiteBridge>();

            if (displaySurfaceRenderer == null)
            {
                Transform surface = transform.Find("Browser Display Surface");
                if (surface != null)
                    displaySurfaceRenderer = surface.GetComponent<MeshRenderer>();
            }

            if (displaySurfaceCollider == null && displaySurfaceRenderer != null)
                displaySurfaceCollider = displaySurfaceRenderer.GetComponent<MeshCollider>();

            ConfigureBrowserClient();
        }

        /// <summary>Enables browser pointer input while the display is focused; disable it when zoom/focus ends.</summary>
        public void SetDisplayInputEnabled(bool enabled)
        {
            displayInputEnabled = enabled;
            if (!enabled)
            {
                if (browserClient != null && browserClient.IsConnected && browserClient.ReadySignalReceived && !browserClient.HasDisposed && hasLastBrowserMousePosition)
                {
                    if (leftMousePressedOnDisplay)
                        browserClient.SendMouseClick(lastBrowserMousePosition, 1, MouseClickType.Left, MouseEventType.Up);
                    if (rightMousePressedOnDisplay)
                        browserClient.SendMouseClick(lastBrowserMousePosition, 1, MouseClickType.Right, MouseEventType.Up);
                    if (middleMousePressedOnDisplay)
                        browserClient.SendMouseClick(lastBrowserMousePosition, 1, MouseClickType.Middle, MouseEventType.Up);
                }

                SetBrowserKeyboardFocus(false);
                leftMousePressedOnDisplay = false;
                rightMousePressedOnDisplay = false;
                middleMousePressedOnDisplay = false;
                hasLastBrowserMousePosition = false;
            }
        }

        /// <summary>Sets up the official CEF engine and TCP communication layer before the browser starts.</summary>
        private void ConfigureBrowserClient()
        {
            EngineConfiguration engineConfiguration = ScriptableObject.CreateInstance<EngineConfiguration>();
            engineConfiguration.engineAppName = CefEngineAppName;
#if UNITY_EDITOR
            engineConfiguration.engineFiles = new[]
            {
                new Engine.EnginePlatformFiles
                {
                    platform = Platform.Windows64,
                    engineBaseAppLocation = string.Empty,
                    engineEditorLocation = WindowsCefEngineEditorPath,
                    engineRuntimeLocation = "UWB/"
                }
            };
#endif

            browserClient.engine = engineConfiguration;
            browserClient.engineStartupTimeout = CefStartupTimeoutMilliseconds;
            TCPCommunicationLayer communicationLayer = ScriptableObject.CreateInstance<TCPCommunicationLayer>();
            communicationLayer.inPort = BrowserTcpInPort;
            communicationLayer.outPort = BrowserTcpOutPort;
            browserClient.communicationLayer = communicationLayer;
            browserClient.initialUrl = RecordsWebsiteUrl;
            browserClient.jsMethodManager.jsMethodsEnable = true;
        }

        /// <summary>Registers the page callback and loads the case website when CEF is connected.</summary>
        protected override void OnStart()
        {
            browserClient.RegisterJsMethod<string>(EvidenceMethodName, ReceiveEvidenceRecordIdFromWebsite);
            browserClient.RegisterJsMethod<string>(NavigateMethodName, NavigateFromToolbar);
            browserClient.RegisterJsMethod(BackMethodName, GoBackFromToolbar);
            browserClient.RegisterJsMethod(ForwardMethodName, GoForwardFromToolbar);
            browserClient.RegisterJsMethod(ReloadMethodName, ReloadFromToolbar);
            browserClient.OnClientConnected += HandleBrowserConnected;
            browserClient.OnLoadFinish += HandleBrowserLoadFinished;

            if (browserClient.IsConnected)
                Interlocked.Exchange(ref browserConnectedPending, 1);
        }

        /// <summary>Refreshes the display texture and follows case changes without changing the monitor's other materials.</summary>
        protected override void OnFixedUpdate()
        {
            lastBrowserFrameProcessed = Time.frameCount;
            if (Interlocked.Exchange(ref browserConnectedPending, 0) == 1)
                HandleBrowserConnectedOnUnityThread();

            if (Interlocked.Exchange(ref browserLoadFinishedPending, 0) == 1)
            {
                websiteToolbarInstalled = false;
                nextToolbarInstallTime = 0f;
            }

            if (browserClient != null && browserClient.IsConnected && !websiteToolbarInstalled && Time.unscaledTime >= nextToolbarInstallTime)
            {
                nextToolbarInstallTime = Time.unscaledTime + 1f;
                InstallWebsiteEvidenceBridge();
            }

            while (pendingEvidenceRecordIds.TryDequeue(out string recordId))
            {
                if (caseWebsiteBridge != null)
                    caseWebsiteBridge.ReceiveEvidenceRecordId(recordId);
            }

            BindBrowserTextureToDisplaySurface();
            LoadUpdatedCaseHashFragment();
            RouteDisplayInput();
        }

        private void RouteDisplayInput()
        {
            if (!displayInputEnabled || displaySurfaceCollider == null || !displaySurfaceCollider.enabled)
                return;

            if (browserClient == null || !browserClient.IsConnected || !browserClient.ReadySignalReceived || browserClient.HasDisposed)
                return;

            Camera inputCamera = displayInputCamera != null ? displayInputCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (inputCamera == null || mouse == null)
                return;

            bool pointerOverDisplay = TryGetBrowserMousePosition(inputCamera, mouse.position.ReadValue(), out Vector2 browserMousePosition);
            if (pointerOverDisplay)
            {
                if (!hasLastBrowserMousePosition || lastBrowserMousePosition != browserMousePosition)
                {
                    browserClient.SendMouseMove(browserMousePosition);
                    lastBrowserMousePosition = browserMousePosition;
                    hasLastBrowserMousePosition = true;
                }
            }
            else
            {
                hasLastBrowserMousePosition = false;
            }

            RouteMouseButtons(mouse, pointerOverDisplay, browserMousePosition);

            if (pointerOverDisplay)
                RouteMouseScroll(mouse, browserMousePosition);

            RouteKeyboardInput();
        }

        private void LateUpdate()
        {
            if (lastBrowserFrameProcessed == Time.frameCount || browserClient == null || browserClient.HasDisposed)
                return;

            browserClient.UpdateFps();
            browserClient.LoadTextureData();
            OnFixedUpdate();
        }


        /// <summary>Unsubscribes browser callbacks and restores the display's assigned material on shutdown.</summary>
        protected override void OnDestroyed()
        {
            SetDisplayInputEnabled(false);

            if (browserClient != null)
            {
                browserClient.OnClientConnected -= HandleBrowserConnected;
                browserClient.OnLoadFinish -= HandleBrowserLoadFinished;
            }

            if (runtimeDisplayMaterial != null)
            {
                if (displaySurfaceRenderer != null)
                {
                    Material[] currentMaterials = displaySurfaceRenderer.sharedMaterials;
                    if (currentMaterials.Length > 0 && currentMaterials[0] == runtimeDisplayMaterial)
                    {
                        currentMaterials[0] = originalDisplayMaterial;
                        displaySurfaceRenderer.sharedMaterials = currentMaterials;
                    }
                }

                Destroy(runtimeDisplayMaterial);
            }
        }

        private void HandleBrowserConnected()
        {
            Interlocked.Exchange(ref browserConnectedPending, 1);
        }

        private void HandleBrowserConnectedOnUnityThread()
        {
            lastLoadedCaseHashFragment = GetActiveCaseHashFragment();
            browserClient.LoadUrl(BuildRecordsWebsiteUrl(lastLoadedCaseHashFragment));
            BindBrowserTextureToDisplaySurface();
        }

        private void HandleBrowserLoadFinished(string url)
        {
            Interlocked.Exchange(ref browserLoadFinishedPending, 1);
        }

        private void ReceiveEvidenceRecordIdFromWebsite(string recordId)
        {
            if (!string.IsNullOrWhiteSpace(recordId))
                pendingEvidenceRecordIds.Enqueue(recordId);
        }

        private void InstallWebsiteEvidenceBridge()
        {
            // UWB JS methods are void: uwb.ExecuteJsMethod never returns a value to the page.
            // record.html still reads the synchronous { ok, channel, recordId, offline } object
            // from UBPDBridge.sendEvidenceRecordId (see site/js/unity-bridge.js), so this
            // replacement returns that acknowledgement after the call is dispatched.
            browserClient.ExecuteJs(
                "(function(){if(window.__ubpdBrowserToolbarInstalled)return;" +
                "function invoke(method,arg){if(arguments.length>1)uwb.ExecuteJsMethod(method,arg);else uwb.ExecuteJsMethod(method);}" +
                "function sendEvidenceRecordId(recordId){var id=recordId===undefined||recordId===null?'':String(recordId);if(!id)return {ok:false,channel:'none',recordId:'',offline:false};try{invoke('sendEvidenceRecordId',id);}catch(e){return {ok:false,channel:'uwb.ExecuteJsMethod',recordId:id,offline:false};}return {ok:true,channel:'uwb.ExecuteJsMethod',recordId:id,offline:false};}" +
                "window.UBPDBridge=window.UBPDBridge||{};" +
                "window.UBPDBridge.sendEvidenceRecordId=sendEvidenceRecordId;" +
                "window.UBPD_onAddEvidence=function(recordId,message){sendEvidenceRecordId(recordId);};" +
                "function readClueHash(){var match=window.location.hash.match(/^#clue=(.*)$/);" +
                "if(match)sendEvidenceRecordId(decodeURIComponent(match[1]));}" +
                "window.addEventListener('hashchange',readClueHash);readClueHash();" +
                "var d=document;" +
                "if(!d.getElementById('ubpd-browser-bar')){" +
                "var style=d.createElement('style');style.id='ubpd-browser-style';" +
                "style.textContent='body{zoom:1!important;padding-top:42px!important;box-sizing:border-box!important}#ubpd-browser-bar{position:fixed;top:0;left:0;width:100%;height:42px;box-sizing:border-box;display:flex;align-items:center;gap:5px;padding:5px 8px;background:linear-gradient(#e6e4de,#c7c5be);border-bottom:1px solid #777;box-shadow:0 1px 4px #0008;z-index:2147483647;font:13px Arial,sans-serif;color:#222}#ubpd-browser-bar button{height:29px;min-width:30px;border:1px solid #888;border-radius:3px;background:linear-gradient(#fff,#d8d6cf);color:#222;font:16px Arial,sans-serif;cursor:pointer}#ubpd-browser-bar button:hover{background:#fff}#ubpd-browser-address{height:27px;min-width:0;flex:1;box-sizing:border-box;padding:3px 8px;border:1px solid #888;border-radius:3px;background:#fff;color:#222;font:13px Arial,sans-serif}#ubpd-browser-go{min-width:44px!important;font-size:12px!important}' ;"+
                "d.documentElement.appendChild(style);"+
                "var bar=d.createElement('div');bar.id='ubpd-browser-bar';"+
                "bar.innerHTML='<button type=\"button\" id=\"ubpd-browser-back\" title=\"Back\">&#8592;</button><button type=\"button\" id=\"ubpd-browser-forward\" title=\"Forward\">&#8594;</button><button type=\"button\" id=\"ubpd-browser-reload\" title=\"Reload\">&#8635;</button><input id=\"ubpd-browser-address\" type=\"text\" spellcheck=\"false\" placeholder=\"Search or enter address\"><button type=\"button\" id=\"ubpd-browser-go\">Go</button>';"+
                "d.documentElement.appendChild(bar);"+
                "var address=d.getElementById('ubpd-browser-address');"+
                "function displayAddress(){address.value=window.location.href.split('#')[0];}"+
                "function navigate(){var value=address.value.trim();if(value)invoke('"+NavigateMethodName+"',value);}"+
                "d.getElementById('ubpd-browser-back').onclick=function(){invoke('"+BackMethodName+"');};"+
                "d.getElementById('ubpd-browser-forward').onclick=function(){invoke('"+ForwardMethodName+"');};"+
                "d.getElementById('ubpd-browser-reload').onclick=function(){invoke('"+ReloadMethodName+"');};"+
                "d.getElementById('ubpd-browser-go').onclick=navigate;"+
                "address.addEventListener('keydown',function(e){if(e.key==='Enter'){e.preventDefault();navigate();}});"+
                "displayAddress();window.addEventListener('hashchange',displayAddress);"+
                "window.addEventListener('popstate',displayAddress);"+
                "window.__ubpdBrowserToolbarInstalled=true;"+
                "}"+
                "})();");
        }

        private void NavigateFromToolbar(string address)
        {
            if (browserClient == null || !browserClient.IsConnected || string.IsNullOrWhiteSpace(address)) return;

            string target = address.Trim();
            if (!target.Contains("://"))
            {
                bool looksLikeHost = target.Contains(".") && !target.Contains(" ");
                target = looksLikeHost ? "https://" + target : "https://www.google.com/search?q=" + Uri.EscapeDataString(target);
            }

            if (!Uri.TryCreate(target, UriKind.Absolute, out Uri parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                return;

            lastLoadedCaseHashFragment = GetActiveCaseHashFragment();
            browserClient.LoadUrl(parsed.AbsoluteUri);
        }

        private void GoBackFromToolbar()
        {
            if (browserClient != null && browserClient.IsConnected) browserClient.GoBack();
        }

        private void MarkWebsiteToolbarInstalled()
        {
            websiteToolbarInstalled = true;
        }

        private void GoForwardFromToolbar()
        {
            if (browserClient != null && browserClient.IsConnected) browserClient.GoForward();
        }

        private void ReloadFromToolbar()
        {
            if (browserClient != null && browserClient.IsConnected) browserClient.Refresh();
        }

        private void LoadUpdatedCaseHashFragment()
        {
            if (browserClient == null || !browserClient.IsConnected || caseWebsiteBridge == null)
                return;

            string currentHashFragment = GetActiveCaseHashFragment();
            if (string.IsNullOrEmpty(currentHashFragment) || currentHashFragment == lastLoadedCaseHashFragment)
                return;

            lastLoadedCaseHashFragment = currentHashFragment;
            browserClient.LoadUrl(BuildRecordsWebsiteUrl(currentHashFragment));
        }

        private string GetActiveCaseHashFragment()
        {
            return caseWebsiteBridge != null ? caseWebsiteBridge.GetCurrentCaseHashFragment() : string.Empty;
        }

        private static string BuildRecordsWebsiteUrl(string hashFragment)
        {
            return string.IsNullOrEmpty(hashFragment) ? RecordsWebsiteUrl : RecordsWebsiteUrl + hashFragment;
        }

        private void BindBrowserTextureToDisplaySurface()
        {
            if (displaySurfaceRenderer == null)
                return;

            if (browserClient == null || !browserClient.IsConnected ||
                !browserClient.ReadySignalReceived || browserClient.HasDisposed ||
                browserClient.BrowserTexture == null)
            {
                RestoreOriginalDisplayMaterial();
                return;
            }

            if (runtimeDisplayMaterial == null)
            {
                Material[] sharedMaterials = displaySurfaceRenderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0 || sharedMaterials[0] == null)
                    return;

                originalDisplayMaterial = sharedMaterials[0];
                runtimeDisplayMaterial = new Material(originalDisplayMaterial);
                runtimeDisplayMaterial.name = originalDisplayMaterial.name + " (Browser Runtime)";
                sharedMaterials[0] = runtimeDisplayMaterial;
                displaySurfaceRenderer.sharedMaterials = sharedMaterials;
            }

            runtimeDisplayMaterial.mainTexture = browserClient.BrowserTexture;
            runtimeDisplayMaterial.mainTextureScale = new Vector2(1f, flipBrowserTextureVertically ? -1f : 1f);
            runtimeDisplayMaterial.mainTextureOffset = new Vector2(0f, flipBrowserTextureVertically ? 1f : 0f);
        }

        private void RestoreOriginalDisplayMaterial()
        {
            if (runtimeDisplayMaterial == null)
                return;

            if (displaySurfaceRenderer != null)
            {
                Material[] currentMaterials = displaySurfaceRenderer.sharedMaterials;
                if (currentMaterials.Length > 0 && currentMaterials[0] == runtimeDisplayMaterial)
                {
                    currentMaterials[0] = originalDisplayMaterial;
                    displaySurfaceRenderer.sharedMaterials = currentMaterials;
                }
            }

            Destroy(runtimeDisplayMaterial);
            runtimeDisplayMaterial = null;
            originalDisplayMaterial = null;
        }

        private bool TryGetBrowserMousePosition(Camera inputCamera, Vector2 screenPosition, out Vector2 browserPosition)
        {
            browserPosition = default;
            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!displaySurfaceCollider.Raycast(ray, out RaycastHit hit, float.MaxValue))
                return false;

            Texture2D browserTexture = browserClient.BrowserTexture;
            if (browserTexture == null)
                return false;

            Vector2 uv = hit.textureCoord;
            float browserX = Mathf.Clamp(uv.x, 0f, 1f) * (browserTexture.width - 1);
            float browserY = (1f - Mathf.Clamp(uv.y, 0f, 1f)) * (browserTexture.height - 1);
            browserPosition = new Vector2(browserX, browserY);
            return true;
        }

        private void RouteMouseButtons(Mouse mouse, bool pointerOverDisplay, Vector2 browserPosition)
        {
            if (mouse.leftButton.wasPressedThisFrame && pointerOverDisplay)
            {
                browserClient.SendMouseClick(browserPosition, 1, MouseClickType.Left, MouseEventType.Down);
                leftMousePressedOnDisplay = true;
                SetBrowserKeyboardFocus(true);
            }

            if (mouse.leftButton.wasReleasedThisFrame && leftMousePressedOnDisplay)
            {
                browserClient.SendMouseClick(pointerOverDisplay ? browserPosition : lastBrowserMousePosition, 1, MouseClickType.Left, MouseEventType.Up);
                leftMousePressedOnDisplay = false;
            }

            if (mouse.rightButton.wasPressedThisFrame && pointerOverDisplay)
            {
                browserClient.SendMouseClick(browserPosition, 1, MouseClickType.Right, MouseEventType.Down);
                rightMousePressedOnDisplay = true;
            }

            if (mouse.rightButton.wasReleasedThisFrame && rightMousePressedOnDisplay)
            {
                browserClient.SendMouseClick(pointerOverDisplay ? browserPosition : lastBrowserMousePosition, 1, MouseClickType.Right, MouseEventType.Up);
                rightMousePressedOnDisplay = false;
            }

            if (mouse.middleButton.wasPressedThisFrame && pointerOverDisplay)
            {
                browserClient.SendMouseClick(browserPosition, 1, MouseClickType.Middle, MouseEventType.Down);
                middleMousePressedOnDisplay = true;
            }

            if (mouse.middleButton.wasReleasedThisFrame && middleMousePressedOnDisplay)
            {
                browserClient.SendMouseClick(pointerOverDisplay ? browserPosition : lastBrowserMousePosition, 1, MouseClickType.Middle, MouseEventType.Up);
                middleMousePressedOnDisplay = false;
            }
        }

        private void RouteMouseScroll(Mouse mouse, Vector2 browserPosition)
        {
            float wheel = Mathf.Clamp(mouse.scroll.ReadValue().y / BrowserScrollScale, -1f, 1f);
            int scrollAmount = Mathf.RoundToInt(wheel * BrowserScrollSensitivity * browserClient.BrowserTexture.height);
            if (scrollAmount != 0)
                browserClient.SendMouseScroll(browserPosition, scrollAmount);
        }

        private void RouteKeyboardInput()
        {
            if (!browserKeyboardFocused)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != subscribedKeyboard)
                SubscribeToKeyboardTextInput(keyboard);

            if (keyboard == null)
                return;

            keyboardKeysDown.Clear();
            keyboardKeysUp.Clear();
            foreach (KeyControl key in keyboard.allKeys)
            {
                try
                {
                    if (key.wasPressedThisFrame)
                        keyboardKeysDown.Add(key.keyCode.UnityKeyToWindowKey());
                    if (key.wasReleasedThisFrame)
                        keyboardKeysUp.Add(key.keyCode.UnityKeyToWindowKey());
                }
                catch (ArgumentOutOfRangeException)
                {
                    // UWB's verified Unity-to-Windows key map does not include every Input System control.
                }
            }

            if (keyboardKeysDown.Count == 0 && keyboardKeysUp.Count == 0 && pendingKeyboardText.Length == 0)
                return;

            browserClient.SendKeyboardControls(keyboardKeysDown.ToArray(), keyboardKeysUp.ToArray(), pendingKeyboardText.ToCharArray());
            pendingKeyboardText = string.Empty;
        }

        private void SetBrowserKeyboardFocus(bool focused)
        {
            browserKeyboardFocused = focused;
            if (!focused)
            {
                SubscribeToKeyboardTextInput(null);
                pendingKeyboardText = string.Empty;
                return;
            }

            SubscribeToKeyboardTextInput(Keyboard.current);
        }

        private void SubscribeToKeyboardTextInput(Keyboard keyboard)
        {
            if (subscribedKeyboard == keyboard)
                return;

            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput -= HandleKeyboardTextInput;

            subscribedKeyboard = keyboard;
            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput += HandleKeyboardTextInput;
        }

        private void HandleKeyboardTextInput(char character)
        {
            pendingKeyboardText += character;
        }
    }
}
