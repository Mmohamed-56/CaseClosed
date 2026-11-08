using UnityEngine;
using UnityEngine.Rendering;
using CaseClosed.Lighting;

namespace CaseClosed.UI
{
    /// <summary>
    /// Runtime night exterior blockout for the MainMenu level: UB police station facade,
    /// street, door, signage, lamp posts, flickering fluorescents, and cool URP volume.
    /// Primitive stand-in until Bezi swaps in a real facade mesh / Brick building import.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class MainMenuExteriorBootstrap : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera menuCamera;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 2.35f, -11.5f);
        [SerializeField] private Vector3 cameraLookAt = new Vector3(0f, 2.1f, 0f);
        [SerializeField, Range(35f, 75f)] private float cameraFov = 42f;
        [SerializeField] private bool softCameraSway = true;
        [SerializeField, Range(0f, 0.08f)] private float swayAmount = 0.035f;
        [SerializeField, Min(0.01f)] private float swaySpeed = 0.22f;

        [Header("Volume")]
        [SerializeField] private VolumeProfile nightProfile;
        [SerializeField, Range(0f, 1f)] private float volumeWeight = 1f;

        [Header("Materials (optional)")]
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material facadeMaterial;
        [SerializeField] private Material asphaltMaterial;

        private Transform setRoot;
        private Vector3 cameraBasePos;
        private Quaternion cameraBaseRot;
        private float swaySeed;

        private void Awake()
        {
            if (setRoot != null) return;
            BuildExterior();
            FrameCamera();
            EnsureNightVolume();
            swaySeed = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (!softCameraSway || menuCamera == null) return;
            float t = Time.unscaledTime * swaySpeed;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(swaySeed, t) - 0.5f) * 2f * swayAmount,
                (Mathf.PerlinNoise(swaySeed + 17f, t) - 0.5f) * 2f * swayAmount * 0.55f,
                0f);
            menuCamera.transform.position = cameraBasePos + offset;
            menuCamera.transform.rotation = cameraBaseRot;
        }

        private void BuildExterior()
        {
            setRoot = new GameObject("MenuExteriorSet").transform;
            setRoot.SetParent(transform, false);

            Material brick = facadeMaterial != null ? facadeMaterial : MakeLit(new Color(0.42f, 0.28f, 0.22f), 0.12f);
            Material asphalt = asphaltMaterial != null ? asphaltMaterial : MakeLit(new Color(0.12f, 0.12f, 0.13f), 0.35f);
            Material ground = groundMaterial != null ? groundMaterial : MakeLit(new Color(0.28f, 0.32f, 0.22f), 0.2f);
            Material concrete = MakeLit(new Color(0.35f, 0.36f, 0.34f), 0.25f);
            Material doorMat = MakeLit(new Color(0.18f, 0.22f, 0.28f), 0.4f);
            Material glass = MakeLit(new Color(0.55f, 0.72f, 0.78f), 0.05f);
            Material signNavy = MakeLit(new Color(0.08f, 0.14f, 0.24f), 0.3f);
            Material metal = MakeLit(new Color(0.22f, 0.22f, 0.24f), 0.55f);
            Material warmBulb = MakeUnlit(new Color(1f, 0.86f, 0.62f));
            Material neonCool = MakeUnlit(new Color(0.72f, 0.92f, 0.88f));

            // Street + curb + lawn strip.
            CreateBox(setRoot, "Street", new Vector3(0f, -0.05f, -6f), new Vector3(28f, 0.1f, 14f), asphalt);
            CreateBox(setRoot, "Sidewalk", new Vector3(0f, 0.02f, -1.6f), new Vector3(18f, 0.08f, 3.2f), concrete);
            CreateBox(setRoot, "Lawn", new Vector3(0f, -0.02f, 4.5f), new Vector3(30f, 0.08f, 10f), ground);
            CreateBox(setRoot, "Curb", new Vector3(0f, 0.08f, -3.15f), new Vector3(18f, 0.16f, 0.28f), concrete);

            // Station massing — clearly “outside the building.”
            CreateBox(setRoot, "Facade", new Vector3(0f, 3.4f, 1.2f), new Vector3(14f, 6.8f, 2.2f), brick);
            CreateBox(setRoot, "WingL", new Vector3(-9.2f, 2.6f, 1.6f), new Vector3(4.2f, 5.2f, 3.2f), brick);
            CreateBox(setRoot, "WingR", new Vector3(9.2f, 2.6f, 1.6f), new Vector3(4.2f, 5.2f, 3.2f), brick);
            CreateBox(setRoot, "RoofCap", new Vector3(0f, 6.95f, 1.1f), new Vector3(14.6f, 0.35f, 2.6f), concrete);
            CreateBox(setRoot, "EntryRecess", new Vector3(0f, 1.55f, 0.15f), new Vector3(3.4f, 3.1f, 0.6f), concrete);

            // Door + frame.
            CreateBox(setRoot, "Door", new Vector3(0f, 1.35f, -0.05f), new Vector3(1.55f, 2.55f, 0.12f), doorMat);
            CreateBox(setRoot, "DoorGlass", new Vector3(0f, 1.85f, -0.12f), new Vector3(1.15f, 1.1f, 0.04f), glass);
            CreateBox(setRoot, "DoorHandle", new Vector3(0.55f, 1.25f, -0.16f), new Vector3(0.08f, 0.35f, 0.08f), metal);

            // Awning.
            CreateBox(setRoot, "Awning", new Vector3(0f, 2.95f, -0.85f), new Vector3(4.2f, 0.12f, 1.8f), signNavy);
            CreateBox(setRoot, "AwningPoleL", new Vector3(-1.85f, 1.5f, -1.5f), new Vector3(0.08f, 2.9f, 0.08f), metal);
            CreateBox(setRoot, "AwningPoleR", new Vector3(1.85f, 1.5f, -1.5f), new Vector3(0.08f, 2.9f, 0.08f), metal);

            // Signage — University Police.
            CreateBox(setRoot, "SignBoard", new Vector3(0f, 4.55f, 0.05f), new Vector3(5.6f, 0.85f, 0.12f), signNavy);
            CreateWorldLabel(setRoot, "SignText", "UNIVERSITY  POLICE", new Vector3(0f, 4.55f, -0.05f), 0.28f,
                new Color(0.93f, 0.88f, 0.78f, 1f));
            CreateWorldLabel(setRoot, "CampusTag", "AT  UB", new Vector3(0f, 3.85f, -0.05f), 0.16f,
                new Color(0.72f, 0.82f, 0.78f, 1f));

            // Lit windows (tired office glow).
            CreateWindow(setRoot, "WinL1", new Vector3(-3.6f, 4.4f, 0.05f), glass, neonCool);
            CreateWindow(setRoot, "WinR1", new Vector3(3.6f, 4.4f, 0.05f), glass, neonCool);
            CreateWindow(setRoot, "WinL2", new Vector3(-3.6f, 2.5f, 0.05f), glass, neonCool);
            CreateWindow(setRoot, "WinR2", new Vector3(3.6f, 2.5f, 0.05f), glass, neonCool);
            CreateWindow(setRoot, "WingWinL", new Vector3(-9.2f, 3.2f, -0.05f), glass, neonCool);
            CreateWindow(setRoot, "WingWinR", new Vector3(9.2f, 3.2f, -0.05f), glass, neonCool);

            // Fluorescent / neon strip over entrance.
            GameObject strip = CreateBox(setRoot, "EntryFluorescent", new Vector3(0f, 3.15f, -0.2f),
                new Vector3(3.2f, 0.08f, 0.18f), neonCool);
            AddPointLight(strip.transform, "StripLight", new Vector3(0f, -0.05f, -0.4f),
                new Color(0.78f, 0.92f, 0.88f), 2.4f, 6.5f, true);

            // Door spill.
            AddPointLight(setRoot, "DoorSpill", new Vector3(0f, 1.6f, -1.2f),
                new Color(1f, 0.86f, 0.65f), 1.6f, 5.5f, true);

            // Lamp posts.
            BuildLampPost(setRoot, "LampPostL", new Vector3(-4.6f, 0f, -3.4f), warmBulb, metal);
            BuildLampPost(setRoot, "LampPostR", new Vector3(4.6f, 0f, -3.4f), warmBulb, metal);

            // Moon / night fill.
            GameObject moon = new GameObject("MoonDirectional");
            moon.transform.SetParent(setRoot, false);
            moon.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
            Light moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.55f, 0.64f, 0.82f, 1f);
            moonLight.intensity = 0.38f;
            moonLight.shadows = LightShadows.Soft;

            // Ambient night push for this scene.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.13f, 0.20f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.08f, 0.10f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.035f, 1f);
            RenderSettings.ambientIntensity = 0.75f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            RenderSettings.fogDensity = 0.018f;
        }

        private void FrameCamera()
        {
            if (menuCamera == null)
                menuCamera = Camera.main;
            if (menuCamera == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                menuCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.tag = "MainCamera";
            }

            menuCamera.orthographic = false;
            menuCamera.fieldOfView = cameraFov;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.035f, 0.05f, 0.08f, 1f);
            menuCamera.transform.position = cameraPosition;
            menuCamera.transform.LookAt(cameraLookAt);
            cameraBasePos = menuCamera.transform.position;
            cameraBaseRot = menuCamera.transform.rotation;

            // Ensure URP post-processing is on when the component exists.
            var urpCam = menuCamera.GetComponent("UniversalAdditionalCameraData");
            if (urpCam == null)
            {
                var type = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (type != null)
                {
                    urpCam = menuCamera.gameObject.AddComponent(type);
                    var prop = type.GetProperty("renderPostProcessing");
                    if (prop != null) prop.SetValue(urpCam, true);
                }
            }
            else
            {
                var prop = urpCam.GetType().GetProperty("renderPostProcessing");
                if (prop != null) prop.SetValue(urpCam, true);
            }
        }

        private void EnsureNightVolume()
        {
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                GameObject go = new GameObject("MenuNightVolume");
                go.transform.SetParent(transform, false);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }

            volume.weight = volumeWeight;
            if (nightProfile != null)
                volume.sharedProfile = nightProfile;
        }

        private void BuildLampPost(Transform parent, string name, Vector3 pos, Material bulbMat, Material poleMat)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            CreateBox(root, "Pole", new Vector3(0f, 1.7f, 0f), new Vector3(0.12f, 3.4f, 0.12f), poleMat);
            CreateBox(root, "Arm", new Vector3(0f, 3.35f, -0.35f), new Vector3(0.08f, 0.08f, 0.7f), poleMat);
            GameObject bulb = CreateBox(root, "Bulb", new Vector3(0f, 3.25f, -0.7f), new Vector3(0.28f, 0.22f, 0.28f), bulbMat);
            AddPointLight(bulb.transform, "LampLight", Vector3.zero,
                new Color(1f, 0.82f, 0.55f), 2.8f, 8f, true);
        }

        private void CreateWindow(Transform parent, string name, Vector3 pos, Material glass, Material glow)
        {
            CreateBox(parent, name, pos, new Vector3(1.5f, 1.1f, 0.08f), glass);
            CreateBox(parent, name + "Glow", pos + new Vector3(0f, 0f, -0.06f), new Vector3(1.35f, 0.95f, 0.02f), glow);
            AddPointLight(parent, name + "Light", pos + new Vector3(0f, 0f, -0.5f),
                new Color(0.7f, 0.88f, 0.86f), 0.85f, 4.5f, true);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static void CreateWorldLabel(Transform parent, string name, string text, Vector3 pos, float charSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // Face the street camera (looking toward +Z from negative Z).
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = charSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.fontStyle = FontStyle.Bold;
        }

        private static void AddPointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range, bool flicker)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (flicker)
            {
                SoftBulbFlicker soft = go.AddComponent<SoftBulbFlicker>();
                // Defaults on SoftBulbFlicker are already subtle / non-seizure.
                _ = soft;
            }
        }

        private static Material MakeLit(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static Material MakeUnlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
