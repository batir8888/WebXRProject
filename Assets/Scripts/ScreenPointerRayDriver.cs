using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using WebXR;

/// <summary>
/// Turns screen input into an XR Interaction Toolkit ray: every frame the ray interactor on this
/// GameObject is posed from the active camera through the finger (or mouse cursor), and its
/// select input is fed manually from the touch / mouse button.
///
/// In a WebXR AR session on a phone there are no controllers: WebXR Export converts every
/// screen input source ("transient pointer") into regular canvas touch events, so they arrive
/// here through the Input System <see cref="Touchscreen"/>.
///
/// Gestures:
/// * one finger / left mouse button - drag the object (XRRayInteractor + XRGrabInteractable);
/// * two-finger pinch / mouse wheel while dragging - scale (read by <see cref="PinchScaleGrabTransformer"/>).
/// </summary>
[DefaultExecutionOrder(-200)] // before XRInteractionManager (-105), so XRIT raycasts this frame's ray
[RequireComponent(typeof(XRRayInteractor))]
public class ScreenPointerRayDriver : MonoBehaviour
{
  [Tooltip("Camera of the WebXR rig (rendered by the WebXR display subsystem in AR/VR). Defaults to Camera.main.")]
  [SerializeField] private Camera viewerCamera;
  [Tooltip("Scale step per mouse wheel notch.")]
  [SerializeField] private float mouseWheelStep = 1.1f;
  [Tooltip("Ignore the mouse for this long after a touch (browsers emulate mouse events after touches).")]
  [SerializeField] private float mouseSuppressAfterTouch = 0.6f;

  private XRRayInteractor interactor;
  private float lastPinchDistance;
  private float lastTouchTime = -100f;
  private bool hadPointerLastFrame;
  private Matrix4x4 xrProjection;
  private bool hasXRProjection;

  /// <summary>Multiplicative scale change requested this frame (1 = no change).</summary>
  public float FrameScaleFactor { get; private set; } = 1f;
  /// <summary>True while two fingers are on the screen.</summary>
  public bool IsPinching { get; private set; }
  public XRRayInteractor Interactor => interactor;

  private void Awake()
  {
    interactor = GetComponent<XRRayInteractor>();
    interactor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
  }

  private void OnEnable()
  {
    WebXRManager.OnHeadsetUpdate += OnHeadsetUpdate;
    WebXRManager.OnXRChange += OnXRChange;
  }

  private void OnDisable()
  {
    WebXRManager.OnHeadsetUpdate -= OnHeadsetUpdate;
    WebXRManager.OnXRChange -= OnXRChange;
  }

  // Projection of the first WebXR view: on a phone that is the single full-screen AR view.
  private void OnHeadsetUpdate(Matrix4x4 leftProjectionMatrix, Matrix4x4 rightProjectionMatrix)
  {
    xrProjection = leftProjectionMatrix;
    hasXRProjection = true;
  }

  private void OnXRChange(WebXRState state, int viewsCount, Rect leftRect, Rect rightRect)
  {
    if (state == WebXRState.NORMAL) hasXRProjection = false;
  }

  private void Update()
  {
    FrameScaleFactor = 1f;
    Camera cam = viewerCamera != null ? viewerCamera : Camera.main;

    bool hasPointer = false;
    bool pressed = false;
    bool freezePose = false;
    Vector2 screenPos = default;

    // Collect fingers that are down
    int count = 0;
    Vector2 p0 = default, p1 = default;
    var touchscreen = Touchscreen.current;
    if (touchscreen != null)
    {
      foreach (var touch in touchscreen.touches)
      {
        if (!touch.press.isPressed) continue;
        if (count == 0) p0 = touch.position.ReadValue();
        else if (count == 1) p1 = touch.position.ReadValue();
        count++;
      }
    }

    if (count > 0)
    {
      lastTouchTime = Time.unscaledTime;
      hasPointer = true;
      pressed = true;
      screenPos = p0;
      if (count >= 2)
      {
        // Pinch: keep the ray (and so the object position) still, change only the scale.
        float distance = Vector2.Distance(p0, p1);
        if (IsPinching && lastPinchDistance > 1f)
          FrameScaleFactor = distance / lastPinchDistance;
        lastPinchDistance = distance;
        IsPinching = true;
        freezePose = true;
      }
      else
      {
        IsPinching = false;
      }
    }
    else
    {
      IsPinching = false;
      var mouse = Mouse.current;
      bool immersive = WebXRManager.Instance != null && WebXRManager.Instance.XRState != WebXRState.NORMAL;
      if (mouse != null && !immersive && Time.unscaledTime - lastTouchTime > mouseSuppressAfterTouch)
      {
        hasPointer = true;
        screenPos = mouse.position.ReadValue();
        pressed = mouse.leftButton.isPressed;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
          FrameScaleFactor = scroll > 0f ? mouseWheelStep : 1f / mouseWheelStep;
      }
    }

    if (hasPointer && !freezePose && cam != null)
    {
      Ray ray = ScreenPointToRay(cam, screenPos);
      transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction, cam.transform.up));
    }

    // No finger / cursor: switch the interactor off so it doesn't hover from a stale pose.
    // Keep it on one extra frame so XRIT processes the release normally.
    bool keepEnabled = hasPointer || hadPointerLastFrame || interactor.hasSelection;
    if (interactor.enabled != keepEnabled) interactor.enabled = keepEnabled;
    hadPointerLastFrame = hasPointer;

    SetSelectState(pressed);
  }

  /// <summary>
  /// Writes the select state for the current frame. QueueManualState is not used on purpose: it
  /// applies the value only from the next frame and only when the reader is read. Re-queuing every
  /// frame before XRIT reads keeps pushing the target frame forward, so the state never lands.
  /// </summary>
  private void SetSelectState(bool pressed)
  {
    var input = interactor.selectInput;
    bool wasPressed = input.manualPerformed;
    if (pressed && !wasPressed) input.manualFramePerformed = Time.frameCount;
    if (!pressed && wasPressed) input.manualFrameCompleted = Time.frameCount;
    input.manualPerformed = pressed;
    input.manualValue = pressed ? 1f : 0f;
  }

  /// <summary>
  /// In an immersive session the camera is driven by the WebXR display subsystem, so its own
  /// fieldOfView/aspect don't match what is on screen. Unproject through the WebXR view projection
  /// and the viewer pose instead; in normal browser mode use the regular camera.
  /// </summary>
  private Ray ScreenPointToRay(Camera cam, Vector2 screenPos)
  {
    bool immersive = WebXRManager.Instance != null && WebXRManager.Instance.XRState != WebXRState.NORMAL;
    if (!immersive || !hasXRProjection || Screen.width <= 0 || Screen.height <= 0)
      return cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

    float ndcX = screenPos.x / Screen.width * 2f - 1f;
    float ndcY = screenPos.y / Screen.height * 2f - 1f;
    Matrix4x4 inverse = xrProjection.inverse;
    Vector4 nearClip = inverse * new Vector4(ndcX, ndcY, -1f, 1f);
    Vector4 midClip = inverse * new Vector4(ndcX, ndcY, 0f, 1f); // avoids w = 0 with an infinite far plane
    Vector3 nearView = (Vector3)nearClip / nearClip.w;
    Vector3 midView = (Vector3)midClip / midClip.w;

    // Unity view space looks down -Z
    Transform t = cam.transform;
    Matrix4x4 viewToWorld = Matrix4x4.TRS(t.position, t.rotation, new Vector3(1f, 1f, -1f));
    Vector3 origin = viewToWorld.MultiplyPoint(nearView);
    Vector3 direction = (viewToWorld.MultiplyPoint(midView) - origin).normalized;
    return new Ray(origin, direction);
  }
}
