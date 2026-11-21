using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.Scripting;
using UnityEngine.XR;
using WebXR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WebXR.XRITBridge
{
    /// <summary>
    /// Input System device that mirrors one WebXR controller (or a tracked hand's select/squeeze)
    /// coming from De-Panther's WebXR Export, so XR Interaction Toolkit can bind to it
    /// through the standard <c>&lt;XRController&gt;{LeftHand|RightHand}</c> paths.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(stateType = typeof(XRSimulatedControllerState), commonUsages = new[] { "LeftHand", "RightHand" }, isGenericTypeOfDevice = false, displayName = "WebXR Controller (XRIT Bridge)", updateBeforeRender = true)]
    [Preserve]
    public class WebXRInteractionController : XRController
    {
        static WebXRInteractionController()
        {
            InputSystem.RegisterLayout<WebXRInteractionController>(
                matches: new InputDeviceMatcher()
                    .WithInterface("WebXRController"));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterLayoutInPlayer()
        {
            // Static constructors don't run before AddDevice<T> in IL2CPP with high stripping,
            // register explicitly so the layout always exists in the WebGL player.
            InputSystem.RegisterLayout<WebXRInteractionController>();
        }

        internal WebXRControllerHand ident;

        const float k_PressThreshold = 0.5f;
        bool m_Subscribed;

        protected override void FinishSetup()
        {
            base.FinishSetup();
            if (!m_Subscribed)
            {
                WebXRManager.OnControllerUpdate += OnControllerUpdate;
                WebXRManager.OnXRChange += OnXRChange;
                m_Subscribed = true;
            }
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            if (m_Subscribed)
            {
                WebXRManager.OnControllerUpdate -= OnControllerUpdate;
                WebXRManager.OnXRChange -= OnXRChange;
                m_Subscribed = false;
            }
        }

        void OnXRChange(WebXRState state, int viewsCount, Rect leftRect, Rect rightRect)
        {
            // Leaving AR/VR: mark the controller as not tracked so interactors switch off.
            if (state == WebXRState.NORMAL && added)
                InputSystem.QueueStateEvent(this, new XRSimulatedControllerState { deviceRotation = Quaternion.identity });
        }

        void OnControllerUpdate(WebXRControllerData data)
        {
            if ((WebXRControllerHand)data.hand != ident || !added)
                return;

            ushort buttons = 0;

            if (data.squeeze >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.GripButton;
            if (data.trigger >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.TriggerButton;
            if (data.buttonA >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.PrimaryButton;
            if (data.buttonB >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.SecondaryButton;
            if (data.buttonATouched)
                buttons |= 1 << (int)ControllerButton.PrimaryTouch;
            if (data.buttonBTouched)
                buttons |= 1 << (int)ControllerButton.SecondaryTouch;
            if (data.thumbstickTouched)
                buttons |= 1 << (int)ControllerButton.Primary2DAxisTouch;
            if (data.thumbstick >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.Primary2DAxisClick;
            if (data.touchpadTouched)
                buttons |= 1 << (int)ControllerButton.Secondary2DAxisTouch;
            if (data.touchpad >= k_PressThreshold)
                buttons |= 1 << (int)ControllerButton.Secondary2DAxisClick;

            if (data.enabled)
                buttons |= 1 << (int)ControllerButton.UserPresence;

            // WebXR Export 0.25 already reports the target-ray pose in the reference space
            // the rig lives in, so no floor-height or pitch correction is applied here.
            var state = new XRSimulatedControllerState
            {
                primary2DAxis = new Vector2(data.thumbstickX, data.thumbstickY),
                trigger = data.trigger,
                grip = data.squeeze,
                secondary2DAxis = new Vector2(data.touchpadX, data.touchpadY),
                buttons = buttons,
                batteryLevel = 1f,
                trackingState = data.enabled ? (int)(InputTrackingState.Position | InputTrackingState.Rotation) : 0,
                isTracked = data.enabled,
                devicePosition = data.position,
                deviceRotation = data.rotation
            };

            InputSystem.QueueStateEvent(this, state);
        }
    }

    /// <summary>
    /// Creates the left and right <see cref="WebXRInteractionController"/> devices.
    /// Put one in the scene (the package prefab "WebXR XR Interaction Toolkit Bridge" has it).
    /// </summary>
    public class WebXRControllerAdapter : MonoBehaviour
    {
        static WebXRInteractionController s_Left;
        static WebXRInteractionController s_Right;

        public static WebXRInteractionController left => s_Left;
        public static WebXRInteractionController right => s_Right;

        void Awake()
        {
#if UNITY_WEBGL
            if (s_Left != null && s_Left.added)
                return;

            DontDestroyOnLoad(gameObject);

            s_Left = InputSystem.AddDevice<WebXRInteractionController>("WebXR Controller Left");
            s_Left.ident = WebXRControllerHand.LEFT;
            InputSystem.SetDeviceUsage(s_Left, UnityEngine.InputSystem.CommonUsages.LeftHand);

            s_Right = InputSystem.AddDevice<WebXRInteractionController>("WebXR Controller Right");
            s_Right.ident = WebXRControllerHand.RIGHT;
            InputSystem.SetDeviceUsage(s_Right, UnityEngine.InputSystem.CommonUsages.RightHand);
#endif
        }

        void OnDestroy()
        {
            if (s_Left != null && s_Left.added) InputSystem.RemoveDevice(s_Left);
            if (s_Right != null && s_Right.added) InputSystem.RemoveDevice(s_Right);
            s_Left = null;
            s_Right = null;
        }
    }
}
