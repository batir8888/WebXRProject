# Changelog

## 0.1.3-webxr0.25 (embedded fork, 2026-11-21)

Fork of [someone-s/webxr-xri-toolkit-bridge](https://github.com/someone-s/webxr-xri-toolkit-bridge)
at commit `d556fd64afbd75718f15f54038175616b775fb23` (0.1.2), Apache-2.0. Changes:

- **Fixed build** against WebXR Export 0.25: `WebXRManager.OnHeadsetUpdate` now only passes
  projection matrices, so the controller no longer subscribes to it.
- Removed the floor-height subtraction and the 30° pitch offset: WebXR Export 0.25 reports the
  target-ray pose in the rig's reference space (`local` / `local-floor`) already.
- Tracking state and `UserPresence` follow `WebXRControllerData.enabled`; buttons A/B,
  thumbstick and touchpad clicks are mapped too. On leaving AR/VR the controllers are reset to untracked.
- Unsubscribes from WebXR events when the device is removed; layout is registered in the player too.
- **Removed `WebXRHeadsetAdapter`**. On entering AR it disabled `Camera.main` and spawned two bare cameras,
  which breaks WebXR Export's own camera rig (`WebXRCameraSet` / `WebXRCamera`). Head tracking and
  rendering are handled by WebXR Export.
- Prefab "WebXR XR Interaction Toolkit Bridge" now contains only `WebXRControllerAdapter`
  (the second `WebXRManager` is removed, WebXR Export auto-loads one).
