using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
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
  const string OutlineMaterialPath = "Assets/Materials/ARCubeOutline.mat";
  const string RayMaterialPath = "Assets/Materials/XRRay.mat";
  const string BridgePrefabPath = "Packages/com.someone-s.webxr-xrit-bridge/Runtime/Prefabs/WebXR XR Interaction Toolkit Bridge.prefab";
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
    // Controllers reach the Input System / XRIT through webxr-xrit-bridge (WebXRControllerAdapter).
    // WebXR Interactions' own WebXRInputSystem would add a second LeftHand/RightHand device pair.
    webxr.AutoLoadWebXRInputSystem = false;
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
    var outlineMat = GetOrCreateUnlit(OutlineMaterialPath, Color.white, cullFront: true);
    var rayMat = GetOrCreateUnlit(RayMaterialPath, Color.white, cullFront: false);

    var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its BoxCollider: XRIT raycasts need it
    cubeGo.name = "ARCube";
    cubeGo.transform.localScale = Vector3.one * 0.2f;
    var bodyRenderer = cubeGo.GetComponent<MeshRenderer>();
    bodyRenderer.sharedMaterial = mat;
    var rb = cubeGo.AddComponent<Rigidbody>();
    rb.isKinematic = true;
    rb.useGravity = false;

    // Inverted-hull outline shown on hover / grab
    var outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
    outline.name = "Outline";
    Object.DestroyImmediate(outline.GetComponent<BoxCollider>());
    outline.transform.SetParent(cubeGo.transform, false);
    outline.transform.localScale = Vector3.one * 1.06f;
    var outlineRenderer = outline.GetComponent<MeshRenderer>();
    outlineRenderer.sharedMaterial = outlineMat;
    outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
    outlineRenderer.receiveShadows = false;
    outlineRenderer.enabled = false;

    var spin = cubeGo.AddComponent<Spin>();

    // XR Interaction Toolkit: grab with a ray (touch or controller), move + scale
    var grab = cubeGo.AddComponent<XRGrabInteractable>();
    grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
    grab.selectMode = InteractableSelectMode.Multiple; // two controllers -> two-handed scaling
    grab.useDynamicAttach = true;                      // keep the grab point, no snapping to the ray origin
    grab.trackRotation = false;                        // keep the cube upright while dragging
    grab.trackScale = true;
    grab.throwOnDetach = false;
    grab.addDefaultGrabTransformers = false;

    var general = cubeGo.AddComponent<XRGeneralGrabTransformer>();
    general.allowTwoHandedScaling = true;
    general.clampScaling = true;
    general.minimumScaleRatio = 0.25f;
    general.maximumScaleRatio = 5f;
    var pinch = cubeGo.AddComponent<PinchScaleGrabTransformer>();
    grab.startingSingleGrabTransformers = new System.Collections.Generic.List<XRBaseGrabTransformer> { general, pinch };
    grab.startingMultipleGrabTransformers = new System.Collections.Generic.List<XRBaseGrabTransformer> { general, pinch };

    var feedback = cubeGo.AddComponent<InteractionFeedback>();
    var fso = new SerializedObject(feedback);
    fso.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
    fso.FindProperty("outlineRenderer").objectReferenceValue = outlineRenderer;
    fso.FindProperty("pauseWhileSelected").objectReferenceValue = spin;
    fso.ApplyModifiedPropertiesWithoutUndo();

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

    SetupInteraction(camSet, cube, rayMat);

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

  /// <summary>
  /// XR Interaction Toolkit part of the scene: interaction manager, webxr-xrit-bridge devices,
  /// a screen-touch ray (phones in WebXR AR, mouse on desktop) and controller rays (Quest etc.).
  /// </summary>
  static void SetupInteraction(GameObject camSet, GameObject cube, Material rayMat)
  {
    new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

    var bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePrefabPath);
    PrefabUtility.InstantiatePrefab(bridgePrefab);

    var rig = new GameObject("XR Interaction Rig"); // same space as WebXRCameraSet (origin)

    // Screen touch / mouse ray
    var screenRayGo = new GameObject("Screen Touch Ray Interactor");
    screenRayGo.transform.SetParent(rig.transform, false);
    var screenRay = screenRayGo.AddComponent<XRRayInteractor>();
    ConfigureRay(screenRay);
    screenRay.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
    screenRay.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
    var driver = screenRayGo.AddComponent<ScreenPointerRayDriver>();
    var dso = new SerializedObject(driver);
    dso.FindProperty("viewerCamera").objectReferenceValue = camSet.GetComponentInChildren<Camera>(true);
    dso.ApplyModifiedPropertiesWithoutUndo();

    // Controller rays fed by webxr-xrit-bridge devices
    CreateControllerRay(rig.transform, ControllerRayActivator.Hand.Left, rayMat);
    CreateControllerRay(rig.transform, ControllerRayActivator.Hand.Right, rayMat);

    var pinch = cube.GetComponent<PinchScaleGrabTransformer>();
    var pso = new SerializedObject(pinch);
    pso.FindProperty("screenPointer").objectReferenceValue = driver;
    pso.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.RecordPrefabInstancePropertyModifications(pinch);
  }

  static void ConfigureRay(XRRayInteractor ray)
  {
    ray.useForceGrab = false;              // drag at the hit distance instead of pulling to the hand
    ray.manipulateAttachTransform = false;
    ray.enableUIInteraction = false;
    ray.hoverToSelect = false;
    ray.maxRaycastDistance = 20f;
  }

  static void CreateControllerRay(Transform rig, ControllerRayActivator.Hand hand, Material rayMat)
  {
    string usage = hand == ControllerRayActivator.Hand.Left ? "LeftHand" : "RightHand";
    var controller = new GameObject($"{hand} Controller");
    controller.transform.SetParent(rig, false);
    var tpd = controller.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
    tpd.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, "<XRController>{" + usage + "}/devicePosition", expectedControlType: "Vector3"));
    tpd.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, "<XRController>{" + usage + "}/deviceRotation", expectedControlType: "Quaternion"));
    tpd.ignoreTrackingState = true;

    var rayGo = new GameObject("Ray Interactor");
    rayGo.transform.SetParent(controller.transform, false);
    var ray = rayGo.AddComponent<XRRayInteractor>();
    ConfigureRay(ray);
    var select = new InputAction("Select", InputActionType.Button);
    select.AddBinding("<XRController>{" + usage + "}/{TriggerButton}");
    select.AddBinding("<XRController>{" + usage + "}/{GripButton}");
    ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction;
    ray.selectInput.inputActionPerformed = select;
    ray.selectInput.inputActionValue = new InputAction("Select Value", InputActionType.Value, "<XRController>{" + usage + "}/{Trigger}", expectedControlType: "Axis");

    var line = rayGo.AddComponent<XRInteractorLineVisual>();
    line.lineWidth = 0.005f;
    line.validColorGradient = MakeGradient(new Color(1f, 0.9f, 0.2f));
    line.invalidColorGradient = MakeGradient(new Color(1f, 1f, 1f, 0.5f));
    var lr = rayGo.GetComponent<LineRenderer>();
    lr.sharedMaterial = rayMat;
    lr.shadowCastingMode = ShadowCastingMode.Off;

    var activator = controller.AddComponent<ControllerRayActivator>();
    var aso = new SerializedObject(activator);
    aso.FindProperty("hand").enumValueIndex = (int)hand;
    aso.FindProperty("rayObject").objectReferenceValue = rayGo;
    aso.ApplyModifiedPropertiesWithoutUndo();
    rayGo.SetActive(false);
  }

  static Gradient MakeGradient(Color c)
  {
    var g = new Gradient();
    g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
              new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(0f, 1f) });
    return g;
  }

  static Material GetOrCreateUnlit(string path, Color color, bool cullFront)
  {
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null)
    {
      m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
      AssetDatabase.CreateAsset(m, path);
    }
    m.SetColor("_BaseColor", color);
    m.SetFloat("_Cull", (float)(cullFront ? CullMode.Front : CullMode.Back));
    EditorUtility.SetDirty(m);
    return m;
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
