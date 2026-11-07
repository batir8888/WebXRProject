using UnityEngine;
using WebXR;

/// <summary>
/// Places the target object in the center of the user's field of view (viewer space):
/// once at startup (normal browser mode) and again every time an AR/VR session starts,
/// after the first head poses have arrived from WebXR.
/// </summary>
public class ViewerSpacePlacer : MonoBehaviour
{
  [SerializeField] private Transform target;
  [SerializeField] private Camera viewerCamera;
  [SerializeField] private float distance = 0.8f;
  [Tooltip("Frames to wait after an XR session starts, so the head pose is valid.")]
  [SerializeField] private int framesToWaitAfterXRStart = 3;

  private int framesUntilPlacement = -1;

  private void OnEnable()
  {
    WebXRManager.OnXRChange += OnXRChange;
  }

  private void OnDisable()
  {
    WebXRManager.OnXRChange -= OnXRChange;
  }

  private void Start()
  {
    if (viewerCamera == null) viewerCamera = Camera.main;
    PlaceInFrontOfViewer();
  }

  private void OnXRChange(WebXRState state, int viewsCount, Rect leftRect, Rect rightRect)
  {
    // Re-place on every mode change (entering AR/VR and returning to NORMAL)
    framesUntilPlacement = framesToWaitAfterXRStart;
  }

  private void LateUpdate()
  {
    if (framesUntilPlacement < 0) return;
    if (framesUntilPlacement-- == 0) PlaceInFrontOfViewer();
  }

  public void PlaceInFrontOfViewer()
  {
    if (target == null || viewerCamera == null) return;
    Transform cam = viewerCamera.transform;
    Vector3 forward = cam.forward;
    target.position = cam.position + forward * distance;
    // Face the viewer, keep the object upright
    Vector3 flat = Vector3.ProjectOnPlane(-forward, Vector3.up);
    if (flat.sqrMagnitude > 0.0001f) target.rotation = Quaternion.LookRotation(-flat, Vector3.up);
    Debug.Log($"[ViewerSpacePlacer] {target.name} placed at {target.position} (mode: {(WebXRManager.Instance != null ? WebXRManager.Instance.XRState.ToString() : "n/a")})");
  }
}
