using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// XR Interaction Toolkit grab transformer that scales the grabbed object uniformly with the
/// screen pinch gesture (or mouse wheel) reported by <see cref="ScreenPointerRayDriver"/>.
/// Runs after <see cref="XRGeneralGrabTransformer"/>, which handles position and the
/// two-controller scaling in VR / Quest passthrough.
/// </summary>
public class PinchScaleGrabTransformer : XRBaseGrabTransformer
{
  [SerializeField] private ScreenPointerRayDriver screenPointer;
  [Tooltip("Uniform scale limits (the cube is 1 m at scale 1).")]
  [SerializeField] private float minScale = 0.05f;
  [SerializeField] private float maxScale = 1.0f;

  private Vector3 targetScale;
  private int lastAppliedFrame = -1;

  protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

  public override void OnGrab(XRGrabInteractable grabInteractable)
  {
    base.OnGrab(grabInteractable);
    targetScale = grabInteractable.transform.localScale;
  }

  public override void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale)
  {
    base.OnGrabCountChanged(grabInteractable, targetPose, localScale);
    targetScale = localScale;
  }

  public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
  {
    if (screenPointer == null || !grabInteractable.IsSelected(screenPointer.Interactor))
    {
      targetScale = localScale; // controllers: leave scaling to XRGeneralGrabTransformer
      return;
    }

    if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && lastAppliedFrame != Time.frameCount)
    {
      lastAppliedFrame = Time.frameCount;
      float factor = screenPointer.FrameScaleFactor;
      if (!Mathf.Approximately(factor, 1f) && targetScale.x > 0f)
      {
        float uniform = Mathf.Clamp(targetScale.x * factor, minScale, maxScale);
        targetScale *= uniform / targetScale.x;
      }
    }
    localScale = targetScale;
  }
}
