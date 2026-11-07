using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using WebXR;

/// <summary>
/// One-shot project setup and WebGL build for the WebXR AR demo.
/// Menu: WebXR Demo/... or batch: -executeMethod ProjectSetup.SetupAll / ProjectSetup.BuildWebGL
/// </summary>
public static class ProjectSetup
{
  const string ScenePath = "Assets/Scenes/ARScene.unity";
  const string CubePrefabPath = "Assets/Prefabs/ARCube.prefab";
  const string CubeMaterialPath = "Assets/Materials/ARCube.mat";
  const string CameraSetPrefabPath = "Packages/com.de-panther.webxr-interactions/Runtime/Prefabs/WebXRCameraSet.prefab";
  const string XRSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
  const string WebXRSettingsPath = "Assets/XR/Settings/WebXRSettings.asset";
  const string BuildPath = "Builds/WebGL";

  [MenuItem("WebXR Demo/Setup All")]
  public static void SetupAll()
  {
    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
      EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
    SetupXRManagement();
    SetupPlayerSettings();
    SetupScene();
    AssetDatabase.SaveAssets();
    Debug.Log("[ProjectSetup] Setup complete");
  }

  [MenuItem("WebXR Demo/1. XR Plug-in Management")]
  public static void SetupXRManagement()
  {
    Directory.CreateDirectory("Assets/XR/Settings");

    // XR Plug-in Management: per-build-target settings asset
    var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XRSettingsPath);
    if (perTarget == null)
    {
      perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
      AssetDatabase.CreateAsset(perTarget, XRSettingsPath);
    }
    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);

    if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.WebGL))
      perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.WebGL);

    var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.WebGL);
    general.InitManagerOnStart = true;
    EditorUtility.SetDirty(general);

    bool assigned = XRPackageMetadataStore.AssignLoader(general.AssignedSettings, typeof(WebXRLoader).FullName, BuildTargetGroup.WebGL);
    EditorUtility.SetDirty(general.AssignedSettings);
    EditorUtility.SetDirty(perTarget);

    // WebXR Export settings: AR uses local reference space, hit-test optional
    var webxr = AssetDatabase.LoadAssetAtPath<WebXRSettings>(WebXRSettingsPath);
    if (webxr == null)
    {
      webxr = ScriptableObject.CreateInstance<WebXRSettings>();
      AssetDatabase.CreateAsset(webxr, WebXRSettingsPath);
    }
    webxr.ARRequiredReferenceSpace = WebXRSettings.ReferenceSpaceTypes.local;
    webxr.AROptionalFeatures = WebXRSettings.ExtraFeatureTypes.hit_test;
    webxr.VRRequiredReferenceSpace = WebXRSettings.ReferenceSpaceTypes.local_floor;
    webxr.AutoLoadWebXRManager = true;
    webxr.AutoLoadWebXRInputSystem = true;
    EditorUtility.SetDirty(webxr);
    EditorBuildSettings.AddConfigObject("WebXR.Settings", webxr, true);

    AssetDatabase.SaveAssets();

    var loaders = general.AssignedSettings.activeLoaders.Select(l => l.GetType().FullName);
    Debug.Log($"[ProjectSetup] WebXR loader assigned={assigned}; WebGL loaders: {string.Join(", ", loaders)}; InitOnStart={general.InitManagerOnStart}");
  }

  [MenuItem("WebXR Demo/2. Player Settings")]
  public static void SetupPlayerSettings()
  {
    var webgl = NamedBuildTarget.WebGL;
    PlayerSettings.companyName = "WebXRDemo";
    PlayerSettings.productName = "WebXR AR Viewer-Space Demo";
    PlayerSettings.colorSpace = ColorSpace.Linear;

    // WebXR works through WebGL 2 only (no WebGPU)
    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
    PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

    // WebXR template with the AR / VR buttons
    PlayerSettings.WebGL.template = "PROJECT:WebXRFullView2020";

    // Required: no compression fallback, no multithreading.
    // Compression disabled so the build runs on any static host (GitHub Pages can't set Content-Encoding).
    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
    PlayerSettings.WebGL.decompressionFallback = false;
    PlayerSettings.WebGL.threadsSupport = false;
    PlayerSettings.WebGL.nameFilesAsHashes = false;
    PlayerSettings.WebGL.dataCaching = true;
    PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
    PlayerSettings.WebGL.showDiagnostics = false;
    PlayerSettings.runInBackground = true;

    // Smaller build
    PlayerSettings.SetManagedStrippingLevel(webgl, ManagedStrippingLevel.High);
    PlayerSettings.stripEngineCode = true;
    PlayerSettings.SetIl2CppCodeGeneration(webgl, Il2CppCodeGeneration.OptimizeSize);
    EditorUserBuildSettings.development = false;

    // Mobile-friendly URP: no HDR / MSAA / post-processing on the asset used by WebGL
    foreach (var rp in new[] { GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline }.OfType<UniversalRenderPipelineAsset>().Distinct())
    {
      rp.supportsHDR = false;
      rp.msaaSampleCount = 1;
      EditorUtility.SetDirty(rp);
    }
    AssetDatabase.SaveAssets();
    Debug.Log("[ProjectSetup] Player settings applied");
  }

  [MenuItem("WebXR Demo/3. Scene")]
  public static void SetupScene()
  {
    Directory.CreateDirectory("Assets/Scenes");
    Directory.CreateDirectory("Assets/Prefabs");
    Directory.CreateDirectory("Assets/Materials");

    // Cube prefab
    var mat = AssetDatabase.LoadAssetAtPath<Material>(CubeMaterialPath);
    if (mat == null)
    {
      mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.SetColor("_BaseColor", new Color(0.1f, 0.65f, 1f));
      AssetDatabase.CreateAsset(mat, CubeMaterialPath);
    }
    var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cubeGo.name = "ARCube";
    cubeGo.transform.localScale = Vector3.one * 0.2f;
    cubeGo.GetComponent<MeshRenderer>().sharedMaterial = mat;
    Object.DestroyImmediate(cubeGo.GetComponent<BoxCollider>());
    cubeGo.AddComponent<Spin>();
    var cubePrefab = PrefabUtility.SaveAsPrefabAsset(cubeGo, CubePrefabPath);
    Object.DestroyImmediate(cubeGo);

    // Scene
    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    var light = new GameObject("Directional Light").AddComponent<Light>();
    light.type = LightType.Directional;
    light.intensity = 1.2f;
    light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

    // WebXR camera rig (camera + WebXRController for both hands) at the WebXRManager space origin
    var camSetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraSetPrefabPath);
    var camSet = (GameObject)PrefabUtility.InstantiatePrefab(camSetPrefab);
    camSet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    var cam = camSet.GetComponentInChildren<Camera>(true);
    var camData = cam.GetComponent<UniversalAdditionalCameraData>();
    if (camData != null)
    {
      camData.renderPostProcessing = false;
      camData.antialiasing = AntialiasingMode.None;
    }
    cam.allowHDR = false;
    cam.allowMSAA = false;
    cam.nearClipPlane = 0.05f;
    var camSettings = cam.GetComponent<WebXRCameraSettings>();

    // Cube instance, child of the WebXRManager space root
    var spaceRoot = new GameObject("XRSpaceRoot");
    var cube = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab);
    cube.transform.SetParent(spaceRoot.transform, false);
    cube.transform.localPosition = new Vector3(0f, 0f, 0.8f);

    var placer = spaceRoot.AddComponent<ViewerSpacePlacer>();
    var so = new SerializedObject(placer);
    so.FindProperty("target").objectReferenceValue = cube.transform;
    so.FindProperty("viewerCamera").objectReferenceValue = cam;
    so.FindProperty("distance").floatValue = 0.8f;
    so.ApplyModifiedPropertiesWithoutUndo();

    RenderSettings.ambientMode = AmbientMode.Trilight;
    RenderSettings.ambientSkyColor = new Color(0.8f, 0.85f, 0.9f);
    RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
    RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);
    RenderSettings.skybox = null;

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

    // Remove template sample scene
    if (File.Exists("Assets/Scenes/SampleScene.unity"))
      AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
    AssetDatabase.SaveAssets();
    Debug.Log($"[ProjectSetup] Scene saved: {ScenePath}; camera={cam.name}, controllers={camSet.GetComponentsInChildren<WebXRController>(true).Length}, cameraSettings={(camSettings != null)}");
  }

  [MenuItem("WebXR Demo/Build WebGL")]
  public static void BuildWebGL()
  {
    SetupPlayerSettings();
    var options = new BuildPlayerOptions
    {
      scenes = new[] { ScenePath },
      locationPathName = BuildPath,
      target = BuildTarget.WebGL,
      targetGroup = BuildTargetGroup.WebGL,
      options = BuildOptions.None
    };
    BuildReport report = BuildPipeline.BuildPlayer(options);
    var s = report.summary;
    Debug.Log($"[ProjectSetup] Build {s.result}: {s.totalSize / 1024f / 1024f:F2} MB, {s.totalErrors} errors, {s.totalTime}");
    if (Application.isBatchMode)
      EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
  }
}
