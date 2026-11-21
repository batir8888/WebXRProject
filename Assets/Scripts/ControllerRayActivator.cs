using UnityEngine;
using UnityEngine.InputSystem.XR;
using WebXR;

/// <summary>
/// Shows a controller's ray interactor only while that controller is tracked in an immersive
/// WebXR session. Tracking comes from the webxr-xrit-bridge Input System devices
/// (<c>&lt;XRController&gt;{LeftHand|RightHand}</c>). On a phone there are no controllers, so the rays
/// stay hidden and only the screen-touch ray is used.
/// </summary>
public class ControllerRayActivator : MonoBehaviour
{
  public enum Hand { Left, Right }

  [SerializeField] private Hand hand = Hand.Right;
  [SerializeField] private GameObject rayObject;

  private void Update()
  {
    var device = hand == Hand.Left ? XRController.leftHand : XRController.rightHand;
    bool immersive = WebXRManager.Instance != null && WebXRManager.Instance.XRState != WebXRState.NORMAL;
    bool active = immersive && device != null && device.added && device.isTracked.isPressed;
    if (rayObject != null && rayObject.activeSelf != active) rayObject.SetActive(active);
  }
}
