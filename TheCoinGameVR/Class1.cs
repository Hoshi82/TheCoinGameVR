using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(TheCoinGameVR.TheCoinGameVRMod), "The Coin Game VR", "0.0.89", "Hoshi82")]
[assembly: MelonGame("devotid", "TheCoinGame")]

namespace TheCoinGameVR
{
    public class TheCoinGameVRMod : MelonMod
    {
        private const string NativeDll = "TCGVR_OpenXR.dll";
        private const int RenderEventCopyStereoTextures = 1001;
        private const int RenderEventSubmitSingleFrame = 1002;
        private const int BurstFrameTarget = 30;
        private const float StereoRenderScale = 1.0f;
        private const float VrMenuDistance = 2.0f;
        private const float VrMenuWidth = 1.8f;
        private const float VrMenuAspect = 16.0f / 9.0f;

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LeftX, LeftY, RightX, RightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint Packet;
            public XInputGamepad Gamepad;
        }

        [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern uint XInputGetState(uint userIndex, out XInputState state);

        private bool xInputAvailable = true;
        private readonly bool[] viewButtonWasDown = new bool[4];
        private bool useMenuCamera;
        private float nextVrStartAttempt;
        private const float ObjectSearchInterval = 0.5f;
        private const int DefaultTrackingPredictionMs = 0;

        // ====================================================
        // Native OpenXR
        // ====================================================

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_InitializeOpenXR();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_FindHMD();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_CreateSession();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_StartTrackingThread();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetSessionState();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetLastTrackingResult();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_SetTrackingPredictionMs(int milliseconds);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetTrackingPredictionMs();


        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_CaptureRenderPose();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void TCGVR_ReleaseRenderPoseSnapshot();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetRenderPoseInfo(
            out ulong capturedGeneration,
            out ulong submittedGeneration,
            out int inFlight);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetRenderHeadPose(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetRenderLeftEye(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW,
            out float angleLeft,
            out float angleRight,
            out float angleUp,
            out float angleDown);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetRenderRightEye(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW,
            out float angleLeft,
            out float angleRight,
            out float angleUp,
            out float angleDown);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetHeadPose(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetStereoConfiguration(
            out int viewCount,
            out int recommendedWidth,
            out int recommendedHeight,
            out int recommendedSampleCount);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetLeftEye(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW,
            out float angleLeft,
            out float angleRight,
            out float angleUp,
            out float angleDown);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetRightEye(
            out float posX,
            out float posY,
            out float posZ,
            out float rotX,
            out float rotY,
            out float rotZ,
            out float rotW,
            out float angleLeft,
            out float angleRight,
            out float angleUp,
            out float angleDown);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_CreateStereoSwapchains(int width, int height);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetSwapchainInfo(
            out int width,
            out int height,
            out int format,
            out int leftImageCount,
            out int rightImageCount);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_TestSwapchainAcquireRelease();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_SetUnityStereoTextures(
            IntPtr leftTexture,
            IntPtr rightTexture,
            int width,
            int height);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void TCGVR_ClearUnityStereoTextures();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr TCGVR_GetRenderEventFunc();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TCGVR_GetFrameSubmitInfo(
            out int step,
            out int lastResult,
            out int waitResult,
            out int beginResult,
            out int endResult,
            out int shouldRender,
            out int leftImageIndex,
            out int rightImageIndex,
            out ulong successfulFrames);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void TCGVR_ShutdownOpenXR();

        // ====================================================
        // OpenXR / Renderstatus
        // ====================================================

        private IntPtr renderEventFunction = IntPtr.Zero;
        private bool openXRInitialized;
        private bool hmdFound;
        private bool sessionStarted;

        private bool trackingEnabled = true;
        private bool bodyFollowEnabled = true;
        private bool hideOwnHead = true;

        private bool stereoWanted;
        private bool continuousWanted;
        private bool stereoRenderingEnabled;
        private bool continuousVRRendering;
        private bool pendingSceneRebuild;

        private bool textureCopyRequested;
        private bool singleFrameRequested;
        private bool burstActive;
        private int burstFramesQueued;
        private ulong burstStartSuccessfulFrames;
        private ulong continuousStartSuccessfulFrames;

        private int trackingPredictionMs = DefaultTrackingPredictionMs;

        private bool renderPoseSnapshotReadyThisFrame;
        private bool projectionEventQueuedThisFrame;
        private int lastRenderPoseCaptureResult;
        private ulong capturedRenderPoseGeneration;
        private ulong submittedRenderPoseGeneration;
        private int renderPoseInFlight;

        // ====================================================
        // Player / Kamera
        // ====================================================

        private Camera playerCamera;
        private Transform playerRoot;
        private Rigidbody playerRigidbody;

        // ====================================================
        // 0.0.61 master VR / robust head aiming
        // ====================================================

        private bool vrMasterWanted;
        private bool vrActivationPending;
        private Quaternion neutralCameraLocalRotation = Quaternion.identity;
        private bool neutralCameraRotationStored;

        private Component firstPersonController;
        private object mouseLookObject;
        private FieldInfo mouseLookYSensitivityField;
        private FieldInfo mouseLookCameraTargetRotField;
        private PropertyInfo mouseLookYSensitivityProperty;
        private FieldInfo mouseLookSmoothField;
        private PropertyInfo mouseLookSmoothProperty;
        private float originalMouseLookYSensitivity;
        private bool originalMouseLookYSensitivityStored;
        private bool originalMouseLookSmooth;
        private bool originalMouseLookSmoothStored;
        private bool headAimControllerFound;
        private bool headAimActive;
        private bool mouseLookLookupWarningLogged;

        // 0.0.61 direct PlayMaker camera controller binding
        private MonoBehaviour playMakerCameraController;
        private object playMakerCameraFsm;
        private object pmCamLookY;
        private object pmLookY;
        private object pmEulerY;
        private object pmEulerX;
        private float pmNeutralEulerY;
        private bool pmNeutralEulerYStored;
        private bool pmCameraBindingReady;
        private bool pmCameraBindingLogged;
        private float pmLastInjectedHeadYaw;
        private bool pmHeadYawInjected;
        private readonly List<object> pmDisabledVerticalActions = new List<object>();
        private readonly List<bool> pmDisabledVerticalActionEveryFrame = new List<bool>();


        // ====================================================
        // 0.0.61 PlayMaker look diagnostics
        // ====================================================

        private void DumpPlayMakerLookActions()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("0.0.61 PLAYMAKER LOOK ACTION SCAN");
            MelonLogger.Msg("Looking for MouseLook / CamLook actions...");

            int fsmCount = 0;
            int lookActionCount = 0;

            try
            {
                MonoBehaviour[] behaviours =
                    UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    Type behaviourType = behaviour.GetType();

                    if (
                        behaviourType.Name != "PlayMakerFSM"
                        &&
                        behaviourType.FullName != "HutongGames.PlayMaker.PlayMakerFSM"
                        )
                    {
                        continue;
                    }

                    fsmCount++;

                    object fsm = GetMemberValue(behaviour, "Fsm");
                    if (fsm == null)
                        fsm = GetMemberValue(behaviour, "fsm");

                    if (fsm == null)
                        continue;

                    string fsmName = Convert.ToString(GetMemberValue(behaviour, "FsmName"));
                    if (string.IsNullOrEmpty(fsmName))
                        fsmName = Convert.ToString(GetMemberValue(fsm, "Name"));

                    object statesObject = GetMemberValue(fsm, "States");
                    Array states = statesObject as Array;

                    if (states == null)
                        continue;

                    foreach (object state in states)
                    {
                        if (state == null)
                            continue;

                        string stateName = Convert.ToString(GetMemberValue(state, "Name"));

                        object actionsObject = GetMemberValue(state, "Actions");
                        Array actions = actionsObject as Array;

                        if (actions == null)
                            continue;

                        foreach (object action in actions)
                        {
                            if (action == null)
                                continue;

                            Type actionType = action.GetType();
                            string actionName = actionType.Name;
                            string actionFullName = actionType.FullName ?? actionName;

                            bool interesting =
                                actionName.IndexOf("MouseLook", StringComparison.OrdinalIgnoreCase) >= 0
                                ||
                                actionName.IndexOf("CamLook", StringComparison.OrdinalIgnoreCase) >= 0
                                ||
                                actionFullName.IndexOf("MouseLook", StringComparison.OrdinalIgnoreCase) >= 0
                                ||
                                actionFullName.IndexOf("CamLook", StringComparison.OrdinalIgnoreCase) >= 0;

                            if (!interesting)
                                continue;

                            lookActionCount++;

                            MelonLogger.Msg(
                                "LOOK ACTION: Object="
                                + GetTransformPath(behaviour.transform)
                                + " | FSM="
                                + fsmName
                                + " | State="
                                + stateName
                                + " | Type="
                                + actionFullName);

                            DumpObjectFields(action, "    ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "PlayMaker look action scan failed: " + ex.Message);
            }

            MelonLogger.Msg(
                "PlayMaker FSMs scanned: "
                + fsmCount
                + ", look actions found: "
                + lookActionCount);

            MelonLogger.Msg("====================================");
        }


        private void DumpPlayMakerLookVariablesAndActions()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("0.0.61 PLAYMAKER AIM VARIABLE/ACTION SCAN");
            MelonLogger.Msg("Searching runtime FSM variables and action fields for look input...");

            int fsmCount = 0;
            int variableHits = 0;
            int actionHits = 0;

            try
            {
                MonoBehaviour[] behaviours =
                    UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    Type behaviourType = behaviour.GetType();

                    if (
                        behaviourType.Name != "PlayMakerFSM"
                        &&
                        behaviourType.FullName != "HutongGames.PlayMaker.PlayMakerFSM"
                        )
                    {
                        continue;
                    }

                    fsmCount++;

                    object fsm = GetMemberValue(behaviour, "Fsm");
                    if (fsm == null)
                        fsm = GetMemberValue(behaviour, "fsm");

                    if (fsm == null)
                        continue;

                    string fsmName = Convert.ToString(GetMemberValue(behaviour, "FsmName"));
                    if (string.IsNullOrEmpty(fsmName))
                        fsmName = Convert.ToString(GetMemberValue(fsm, "Name"));

                    object variables = GetMemberValue(fsm, "Variables");
                    if (variables == null)
                        variables = GetMemberValue(behaviour, "FsmVariables");

                    if (variables != null)
                    {
                        Type variablesType = variables.GetType();
                        PropertyInfo[] variableProperties =
                            variablesType.GetProperties(
                                BindingFlags.Instance
                                | BindingFlags.Public
                                | BindingFlags.NonPublic);

                        foreach (PropertyInfo property in variableProperties)
                        {
                            if (!property.CanRead)
                                continue;

                            object collection = null;

                            try
                            {
                                collection = property.GetValue(variables, null);
                            }
                            catch
                            {
                                continue;
                            }

                            Array array = collection as Array;
                            if (array == null)
                                continue;

                            foreach (object variable in array)
                            {
                                if (variable == null)
                                    continue;

                                string variableName =
                                    Convert.ToString(GetMemberValue(variable, "Name"));

                                if (!IsLookDiagnosticText(variableName))
                                    continue;

                                object variableValue =
                                    GetMemberValue(variable, "Value");

                                MelonLogger.Msg(
                                    "LOOK VARIABLE: Object="
                                    + GetTransformPath(behaviour.transform)
                                    + " | FSM="
                                    + fsmName
                                    + " | Collection="
                                    + property.Name
                                    + " | Name="
                                    + variableName
                                    + " | Value="
                                    + (variableValue == null ? "<null>" : variableValue.ToString()));

                                variableHits++;
                            }
                        }
                    }

                    object statesObject = GetMemberValue(fsm, "States");
                    Array states = statesObject as Array;

                    if (states == null)
                        continue;

                    foreach (object state in states)
                    {
                        if (state == null)
                            continue;

                        string stateName = Convert.ToString(GetMemberValue(state, "Name"));
                        object actionsObject = GetMemberValue(state, "Actions");
                        Array actions = actionsObject as Array;

                        if (actions == null)
                            continue;

                        foreach (object action in actions)
                        {
                            if (action == null)
                                continue;

                            Type actionType = action.GetType();
                            string actionFullName =
                                actionType.FullName ?? actionType.Name;

                            bool hit =
                                IsLookDiagnosticText(actionFullName)
                                || ObjectContainsLookDiagnosticField(action);

                            if (!hit)
                                continue;

                            MelonLogger.Msg(
                                "AIM ACTION: Object="
                                + GetTransformPath(behaviour.transform)
                                + " | FSM="
                                + fsmName
                                + " | State="
                                + stateName
                                + " | Type="
                                + actionFullName);

                            DumpObjectFields(action, "    ");
                            actionHits++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "0.0.61 aim diagnostic scan failed: " + ex.Message);
            }

            MelonLogger.Msg(
                "0.0.61 scan complete. FSMs="
                + fsmCount
                + ", variable hits="
                + variableHits
                + ", action hits="
                + actionHits);

            MelonLogger.Msg("====================================");
        }

        private bool ObjectContainsLookDiagnosticField(object target)
        {
            if (target == null)
                return false;

            try
            {
                FieldInfo[] fields =
                    target.GetType().GetFields(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    if (IsLookDiagnosticText(field.Name))
                        return true;

                    object value = null;

                    try
                    {
                        value = field.GetValue(target);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value == null)
                        continue;

                    object nameValue = GetMemberValue(value, "Name");
                    if (nameValue != null &&
                        IsLookDiagnosticText(Convert.ToString(nameValue)))
                    {
                        return true;
                    }

                    if (
                        value is string
                        &&
                        IsLookDiagnosticText((string)value)
                        )
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private bool IsLookDiagnosticText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string value = text.ToLowerInvariant();

            return
                value.Contains("camlook")
                || value.Contains("mouse look")
                || value.Contains("mouselook")
                || value.Contains("mouse x")
                || value.Contains("mouse y")
                || value.Contains("getmousex")
                || value.Contains("getmousey")
                || value.Contains("look_x")
                || value.Contains("look_y")
                || value.Contains("lookx")
                || value.Contains("looky")
                || value.Contains("rewired");
        }

        private object GetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = target.GetType();

            try
            {
                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                if (property != null && property.CanRead)
                    return property.GetValue(target, null);
            }
            catch
            {
            }

            try
            {
                FieldInfo field = type.GetField(
                    memberName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                if (field != null)
                    return field.GetValue(target);
            }
            catch
            {
            }

            return null;
        }

        private void DumpObjectFields(object target, string prefix)
        {
            if (target == null)
                return;

            Type type = target.GetType();

            try
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    object value = null;
                    string valueText = "<null>";

                    try
                    {
                        value = field.GetValue(target);

                        if (value != null)
                        {
                            valueText = value.ToString();

                            object fsmVariableName = GetMemberValue(value, "Name");
                            if (fsmVariableName != null)
                            {
                                string variableName = Convert.ToString(fsmVariableName);
                                if (!string.IsNullOrEmpty(variableName))
                                    valueText += " [Name=" + variableName + "]";
                            }

                            object fsmVariableValue = GetMemberValue(value, "Value");
                            if (fsmVariableValue != null)
                                valueText += " [Value=" + fsmVariableValue + "]";
                        }
                    }
                    catch (Exception ex)
                    {
                        valueText = "<read failed: " + ex.Message + ">";
                    }

                    if (valueText != null && valueText.Length > 300)
                        valueText = valueText.Substring(0, 300);

                    MelonLogger.Msg(
                        prefix
                        + "Field "
                        + field.Name
                        + " : "
                        + field.FieldType.FullName
                        + " = "
                        + valueText);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "Could not dump action fields for "
                    + type.FullName
                    + ": "
                    + ex.Message);
            }
        }

        // ====================================================
        // Hands-only body
        // ====================================================

        private SkinnedMeshRenderer bodyRenderer;
        private Mesh originalBodyMesh;
        private Mesh handsOnlyBodyMesh;
        private bool bodyMeshModified;
        private bool bodyFallbackHidden;
        private bool originalBodyRendererEnabled;

        // ====================================================
        // Headtracking
        // ====================================================

        private bool currentPoseValid;
        private Vector3 currentHeadPosition = Vector3.zero;
        private Quaternion currentHeadRotation = Quaternion.identity;

        private bool referencePoseValid;
        private Vector3 referenceHeadPosition = Vector3.zero;
        private Quaternion referenceHeadRotation = Quaternion.identity;
        private bool recenterRequested;

        private bool trackingContributionApplied;
        private Vector3 lastAppliedPosition = Vector3.zero;
        private Quaternion lastAppliedRotation = Quaternion.identity;
        private Vector3 lastFinalCameraPosition = Vector3.zero;
        private Quaternion lastFinalCameraRotation = Quaternion.identity;

        // ====================================================
        // Stereo
        // ====================================================

        private GameObject leftEyeObject;
        private GameObject rightEyeObject;
        private Camera leftEyeCamera;
        private Camera rightEyeCamera;
        private bool stereoCamerasCreated;

        private RenderTexture leftEyeRenderTexture;
        private RenderTexture rightEyeRenderTexture;
        private RenderTexture leftEyeSubmitTexture;
        private RenderTexture rightEyeSubmitTexture;
        private bool stereoRenderTexturesCreated;
        private bool nativeTexturePointersRegistered;

        private int stereoRenderWidth;
        private int stereoRenderHeight;
        private int recommendedWidth;
        private int recommendedHeight;
        private int recommendedSampleCount;
        private int stereoViewCount;

        private bool openXRSwapchainsCreated;
        private int openXRSwapchainWidth;
        private int openXRSwapchainHeight;
        private int openXRSwapchainFormat;
        private int openXRLeftImageCount;
        private int openXRRightImageCount;

        private bool stereoPoseValid;
        private Vector3 leftEyePosition = Vector3.zero;
        private Vector3 rightEyePosition = Vector3.zero;
        private Quaternion leftEyeRotation = Quaternion.identity;
        private Quaternion rightEyeRotation = Quaternion.identity;
        private Vector3 leftEyeRelativePosition = Vector3.zero;
        private Vector3 rightEyeRelativePosition = Vector3.zero;
        private Quaternion leftEyeRelativeRotation = Quaternion.identity;
        private Quaternion rightEyeRelativeRotation = Quaternion.identity;

        private float leftAngleLeft;
        private float leftAngleRight;
        private float leftAngleUp;
        private float leftAngleDown;
        private float rightAngleLeft;
        private float rightAngleRight;
        private float rightAngleUp;
        private float rightAngleDown;

        private long stereoRenderedFrameCount;
        private bool firstStereoFrameLogged;
        private int orientationStartupSnapshotsLogged;

        // ====================================================
        // HUD / UI
        // ====================================================

        // 0.0.71 stable HUD rollback after comfort test.
        private GameObject vrInteractionHudObject;
        private Canvas vrInteractionHudCanvas;
        private GameObject vrInteractionHudClone;
        private Transform interactionHudRoot;
        private Vector3 interactionHudSmoothedPosition;
        private bool interactionHudHasSmoothedPosition;

        // Separate VR copy of the persistent player corner HUD.
        // The original center cursor / interaction aiming is never moved.
        private GameObject vrPlayerHudObject;
        private Canvas vrPlayerHudCanvas;
        private Transform vrPlayerHudSource;
        private Transform vrPlayerHudClone;
        private bool vrPlayerHudSourceWasActive;

        private const float VrPlayerHudDistance = 1.50f;
        private const float VrPlayerHudHorizontalOffset = -0.42f;
        private const float VrPlayerHudVerticalOffset = -0.24f;
        private const float VrPlayerHudWorldScale = 0.00115f;

        private const float InteractionHudSurfaceOffset = 0.025f;
        private const float InteractionHudWorldScale = 0.00125f;
        private const float InteractionHudMaxDistance = 10.0f;
        private const float InteractionHudPositionSharpness = 24.0f;

        private class CanvasState
        {
            public Canvas Canvas;
            public RenderMode RenderMode;
            public Camera WorldCamera;
            public float PlaneDistance;
            public RectTransform Rect;
            public Vector2 AnchorMin, AnchorMax, Pivot, SizeDelta, AnchoredPosition;
            public Vector3 LocalPosition, LocalScale;
            public Quaternion LocalRotation;
        }

        private readonly List<CanvasState> hudCanvasStates = new List<CanvasState>();
        private int lastHudCanvasCount;
        private Transform vrEnabledPromptRoot;
        private bool vrPromptRootOriginallyActive;
        private bool vrPromptActivationLogged;

        // ====================================================
        // Eigener Kopf
        // ====================================================

        private Transform headBone;
        private Renderer leftEyeRenderer;
        private Renderer rightEyeRenderer;
        private Vector3 originalHeadScale = Vector3.one;
        private bool originalHeadScaleStored;
        private bool originalLeftEyeEnabled = true;
        private bool originalRightEyeEnabled = true;
        private bool originalEyeStateStored;

        private static readonly Vector3 HiddenHeadScale =
            new Vector3(0.001f, 0.001f, 0.001f);

        // ====================================================
        // Diagnose
        // ====================================================

        private int lastSessionState = -999;
        private int lastTrackingResult = -999;
        private int lastFrameSubmitStep;
        private int lastFrameSubmitResult;
        private int lastWaitFrameResult;
        private int lastBeginFrameResult;
        private int lastEndFrameResult;
        private int lastShouldRender;
        private int lastSubmitLeftIndex = -1;
        private int lastSubmitRightIndex = -1;
        private ulong successfulSubmittedFrames;

        private float objectSearchTimer;

        // ====================================================
        // Init
        // ====================================================

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("The Coin Game VR");
            MelonLogger.Msg("Version 0.0.89 separate VR player HUD test (0.0.45 codebase)");
            MelonLogger.Msg("0.0.89 pose-test A: consistent FSM game yaw; detailed cursor dumps only after F11.");
            MelonLogger.Msg("0.0.89 menu-test F: head-following 16:9 menus at 2m; live action hints retained.");
            MelonLogger.Msg("ONE-BUTTON VR + ROBUST HEAD AIM + YAW TURN + HANDS-ONLY BODY + MATCHED RENDER POSE");
            MelonLogger.Msg("====================================");

            try
            {
                int initResult = TCGVR_InitializeOpenXR();
                openXRInitialized = initResult == 1;
                MelonLogger.Msg("OpenXR Init: " + initResult);

                if (openXRInitialized)
                {
                    int hmdResult = TCGVR_FindHMD();
                    hmdFound = hmdResult == 1;
                    MelonLogger.Msg("HMD search: " + hmdResult);
                }

                renderEventFunction = TCGVR_GetRenderEventFunc();
                MelonLogger.Msg("Native Render Event Pointer: " + (renderEventFunction != IntPtr.Zero));
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex.ToString());
            }

            MelonLogger.Msg("F5  = VR ON/OFF");
            MelonLogger.Msg("F9 / Xbox View (Back) = Recenter (Yaw only)");
            MelonLogger.Msg("F12 = Diagnostics");
            vrMasterWanted = true;
            vrActivationPending = true;
            recenterRequested = true;
        }

        // ====================================================
        // Scene Loading
        // ====================================================

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonLogger.Msg("Scene loaded: " + sceneName);

            bool shouldResumeStereo = sessionStarted && stereoWanted;
            bool shouldResumeContinuous = sessionStarted && continuousWanted;

            RestoreBodyMesh();
            RestoreMouseLookPitch();
            RestorePlayMakerVerticalLookActions();

            // 0.0.89: Scene-safe interaction HUD reset.
            // Restore the old scene's original prompt before discarding its
            // references. The new scene will be discovered from scratch.
            RestoreOriginalInteractionHud();
            RestoreVrPromptRootActivation();
            DestroyWorldSpaceInteractionHud();
            interactionHudRoot = null;
            interactionHudHasSmoothedPosition = false;

            DestroyStereoResources();

            playerCamera = null;
            useMenuCamera = false;
            playerRoot = null;
            playerRigidbody = null;

            interactionHudRoot = null;
            interactionHudHasSmoothedPosition = false;
            headBone = null;
            leftEyeRenderer = null;
            rightEyeRenderer = null;
            originalHeadScaleStored = false;
            originalEyeStateStored = false;
            neutralCameraRotationStored = false;
            firstPersonController = null;
            mouseLookObject = null;
            mouseLookYSensitivityField = null;
            mouseLookCameraTargetRotField = null;
            mouseLookYSensitivityProperty = null;
            mouseLookSmoothField = null;
            mouseLookSmoothProperty = null;
            originalMouseLookYSensitivityStored = false;
            originalMouseLookSmoothStored = false;
            headAimControllerFound = false;
            headAimActive = false;
            ResetPlayMakerCameraBinding();
            bodyRenderer = null;
            originalBodyMesh = null;
            handsOnlyBodyMesh = null;
            bodyMeshModified = false;
            bodyFallbackHidden = false;

            currentPoseValid = false;
            referencePoseValid = false;
            recenterRequested = true;
            trackingContributionApplied = false;
            stereoPoseValid = false;

            stereoWanted = shouldResumeStereo;
            continuousWanted = shouldResumeContinuous;
            pendingSceneRebuild = shouldResumeStereo || shouldResumeContinuous;

            FindPlayerObjects();

            if (pendingSceneRebuild)
            {
                MelonLogger.Msg("VR will be restored automatically after the scene change...");
                MelonLogger.Msg("0.0.89 scene HUD/cursor sources will be rebound from scratch.");
            }
        }

        // ====================================================
        // Update
        // ====================================================

        public override void OnUpdate()
        {
            objectSearchTimer += Time.unscaledDeltaTime;

            if (objectSearchTimer >= ObjectSearchInterval)
            {
                objectSearchTimer = 0.0f;

                if (playerCamera == null || playerRoot == null || playerRigidbody == null)
                {
                    FindPlayerObjects();
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
                ToggleMasterVR();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F9) || ReadGamepadRecenterPressed())
            {
                recenterRequested = true;
                MelonLogger.Msg("HMD recenter requested (yaw only).");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F11))
            {
                vrDetailedDiagnosticUntil = Time.unscaledTime + 8.0f;
                vrGrabDiagnosticStates.Clear();
                vrGrabProbeUntil = vrDetailedDiagnosticUntil;
                vrNextGrabProbeTime = Time.unscaledTime;
                MelonLogger.Msg("0.0.89 pose-test A: detailed grab diagnostics enabled for 8 seconds.");
                DumpHudHierarchy();
                DumpInteractionRaycast();
                DumpPlayerHudHierarchy();
                DumpVisibleCenterUi();
                DumpVrAimComparison();
                DumpUiVisibility();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F12))
            {
                PrintDiagnostics();
                DumpPlayMakerLookActions();
                DumpPlayMakerLookVariablesAndActions();
            }

            if (vrActivationPending)
            {
                if (!sessionStarted && playerCamera != null &&
                    Time.unscaledTime >= nextVrStartAttempt)
                {
                    nextVrStartAttempt = Time.unscaledTime + 2.0f;
                    if (!openXRInitialized)
                        openXRInitialized = TCGVR_InitializeOpenXR() == 1;
                    if (openXRInitialized && !hmdFound)
                        hmdFound = TCGVR_FindHMD() == 1;
                    if (openXRInitialized && hmdFound)
                        StartOpenXR();
                }
                TryActivateMasterVR();
            }

            if (!sessionStarted)
                return;

            LogOpenXRStatus();
            ReadHeadPose();
            ReadStereoData();

            if (vrMasterWanted)
            {
                ApplyPlayMakerHeadAim();
                EnforceMouseLookPitchDisabled();
                ApplyHeadAimEarly();
                ApplyVRBodyVisibility();
            }

            if (pendingSceneRebuild)
                TryResumeVRAfterSceneLoad();

            if (vrActivationPending)
                TryActivateMasterVR();
        }

        // ====================================================
        // LateUpdate / Camera pose
        // ====================================================

        public override void OnLateUpdate()
        {
            if (vrMasterWanted && hideOwnHead)
                ApplyHeadVisibility();

            if (vrMasterWanted)
            {
                ApplyPlayMakerHeadAim();
                EnforceMouseLookPitchDisabled();
                ApplyVRBodyVisibility();
            }

            if (playerCamera == null)
                return;

            renderPoseSnapshotReadyThisFrame = false;
            projectionEventQueuedThisFrame = false;

            // ----------------------------------------------------
            // 0.0.38:
            // Fuer Frames, die wirklich an OpenXR gehen, frieren wir
            // EINEN Tracking-Snapshot ein. Unity liest genau diesen
            // Snapshot und der native Render-Thread submitted spaeter
            // dieselben Eye-Posen/FOVs.
            //
            // KEIN xrWaitFrame auf dem Unity-Main-Thread.
            // ----------------------------------------------------
            bool projectionFrameWanted =
                stereoRenderingEnabled
                &&
                (
                    continuousVRRendering
                    ||
                    burstActive
                    ||
                    singleFrameRequested
                );

            if (projectionFrameWanted)
            {
                try
                {
                    lastRenderPoseCaptureResult =
                        TCGVR_CaptureRenderPose();

                    if (lastRenderPoseCaptureResult == 1)
                    {
                        if (ReadRenderPoseData())
                        {
                            renderPoseSnapshotReadyThisFrame = true;
                        }
                        else
                        {
                            TCGVR_ReleaseRenderPoseSnapshot();
                            lastRenderPoseCaptureResult = -1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastRenderPoseCaptureResult = -2;
                    MelonLogger.Error("Render-pose snapshot error: " + ex.Message);
                }
            }

            Vector3 cameraPositionNow = playerCamera.transform.localPosition;
            Quaternion cameraRotationNow = playerCamera.transform.localRotation;

            Vector3 basePosition = cameraPositionNow;
            Quaternion baseRotation = cameraRotationNow;

            // Remove our previous positional contribution when the game has
            // not replaced the camera transform.
            if (trackingContributionApplied)
            {
                float posDiff = Vector3.Distance(cameraPositionNow, lastFinalCameraPosition);

                if (posDiff < 0.001f)
                    basePosition = cameraPositionNow - lastAppliedPosition;
            }

            // Keep horizontal turning from the original game, but remove the
            // VR rotation that this mod applied on the previous frame. This
            // lets mouse/gamepad yaw rotate the VR view while HMD pitch/yaw
            // remains the actual head-aim direction.
            if (vrMasterWanted && neutralCameraRotationStored)
            {
                if (trackingContributionApplied)
                {
                    float rotationDifference =
                        Quaternion.Angle(cameraRotationNow, lastFinalCameraRotation);

                    if (rotationDifference < 0.25f)
                    {
                        baseRotation =
                            cameraRotationNow * Quaternion.Inverse(lastAppliedRotation);
                    }
                }

                baseRotation = KeepOnlyGameYaw(baseRotation);

                // 0.0.61: PlayMaker EulerX contains the HMD yaw used for
                // interaction aiming. Remove exactly the yaw value that this mod
                // injected into PlayMaker on the current pass. The complete HMD
                // rotation is applied below from the native tracking snapshot, so
                // visible horizontal head motion does not depend on PlayMaker's
                // camera update timing.
                if (pmCameraBindingReady && pmEulerX != null && pmHeadYawInjected)
                {
                    // The game can overwrite the camera between Update and
                    // LateUpdate. Its FSM yaw is authoritative; deciding from a
                    // transform-angle threshold can subtract HMD yaw twice.
                    baseRotation = GetGameYawRotation();
                }
            }

            trackingContributionApplied = false;
            lastAppliedPosition = Vector3.zero;
            lastAppliedRotation = Quaternion.identity;

            if (!vrMasterWanted || !sessionStarted || !trackingEnabled || !currentPoseValid)
            {
                RestoreOriginalInteractionHud();
                SetWorldSpaceInteractionHudVisible(false);
                RestoreBasePose(basePosition, baseRotation);

                if (renderPoseSnapshotReadyThisFrame)
                {
                    try { TCGVR_ReleaseRenderPoseSnapshot(); }
                    catch { }

                    renderPoseSnapshotReadyThisFrame = false;
                }

                return;
            }

            // ----------------------------------------------------
            // YAW-ONLY RECENTER
            //
            // Referenzrotation enthaelt absichtlich nur die Drehung
            // um die vertikale Y-Achse. Reale Kopfneigung (Pitch/Roll)
            // wird dadurch niemals als "gerade" gespeichert.
            // ----------------------------------------------------
            if (recenterRequested || !referencePoseValid)
            {
                referenceHeadPosition = currentHeadPosition;
                referenceHeadRotation = ExtractYawOnly(currentHeadRotation);
                referencePoseValid = true;
                recenterRequested = false;

                MelonLogger.Msg(
                    "HMD reference set. Yaw recentered; physical pitch and roll preserved.");
            }

            Quaternion relativeRotation =
                Quaternion.Inverse(referenceHeadRotation) * currentHeadRotation;

            // 0.0.61: Apply the complete native HMD rotation here. The HMD yaw
            // injected into PlayMaker for interaction aiming was removed from
            // baseRotation above.

            Vector3 relativePosition =
                Quaternion.Inverse(referenceHeadRotation) *
                (currentHeadPosition - referenceHeadPosition);

            if (bodyFollowEnabled && playerRoot != null && playerRigidbody != null)
                ApplyImmediateBodyFollow(ref relativePosition);

            Vector3 finalPosition = basePosition + relativePosition;
            Quaternion finalRotation = baseRotation * relativeRotation;

            playerCamera.transform.localPosition = finalPosition;
            playerCamera.transform.localRotation = finalRotation;

            UpdateWorldSpaceInteractionHud();
            UpdateVrPcCursorScale();
            UpdateVrPlayerHud();

            UpdateStereoCameraTransforms();

            // 0.0.89 diagnostic: report if the eye pair ever stops looking like
            // a single stereo pair after applying the frozen snapshot.
            if (stereoRenderingEnabled &&
                leftEyeObject != null &&
                rightEyeObject != null &&
                (stereoRenderedFrameCount % 300) == 0)
            {
                float eyeSeparation =
                    Vector3.Distance(
                        leftEyeObject.transform.position,
                        rightEyeObject.transform.position);

                float eyeRotationDifference =
                    Quaternion.Angle(
                        leftEyeObject.transform.rotation,
                        rightEyeObject.transform.rotation);

                MelonLogger.Msg(
                    "0.0.89 STEREO_PAIR: eyeSeparation=" +
                    eyeSeparation.ToString("F5") +
                    "m eyeRotationDelta=" +
                    eyeRotationDifference.ToString("F5") +
                    "deg snapshotReady=" +
                    renderPoseSnapshotReadyThisFrame);
            }

            if (stereoRenderingEnabled)
            {
                // Wenn ein Projection Frame gewuenscht ist, aber der vorige
                // Snapshot auf dem Render-Thread noch "in flight" ist,
                // rendern/submittieren wir diesen Unity-Frame nicht erneut.
                // Dadurch kann kein spaeterer Snapshot die Pose der bereits
                // gerenderten Pixel ueberschreiben.
                if (!projectionFrameWanted || renderPoseSnapshotReadyThisFrame)
                    RenderStereoEyes();
            }

            if (
                renderPoseSnapshotReadyThisFrame
                &&
                !projectionEventQueuedThisFrame
                )
            {
                try { TCGVR_ReleaseRenderPoseSnapshot(); }
                catch { }

                renderPoseSnapshotReadyThisFrame = false;
            }

            lastAppliedPosition = relativePosition;
            lastAppliedRotation = relativeRotation;
            lastFinalCameraPosition = finalPosition;
            lastFinalCameraRotation = finalRotation;
            trackingContributionApplied = true;
        }


        private Quaternion KeepOnlyGameYaw(Quaternion cameraLocalRotation)
        {
            if (!neutralCameraRotationStored)
                return cameraLocalRotation;

            Quaternion delta =
                Quaternion.Inverse(neutralCameraLocalRotation) * cameraLocalRotation;

            Vector3 forward = delta * Vector3.forward;
            forward.y = 0.0f;

            if (forward.sqrMagnitude < 0.000001f)
                return neutralCameraLocalRotation;

            forward.Normalize();

            Quaternion yawOnly = Quaternion.LookRotation(forward, Vector3.up);
            return neutralCameraLocalRotation * yawOnly;
        }

        private Quaternion GetGameYawRotation()
        {
            if (pmCameraBindingReady && pmEulerX != null)
            {
                float yaw = ReadFsmFloat(pmEulerX, 0.0f);
                if (pmHeadYawInjected)
                    yaw -= pmLastInjectedHeadYaw;
                return Quaternion.AngleAxis(yaw, Vector3.up);
            }

            return neutralCameraLocalRotation;
        }

        private Quaternion ExtractYawOnly(Quaternion rotation)
        {
            Vector3 forward =
                rotation * Vector3.forward;

            forward.y = 0.0f;

            if (forward.sqrMagnitude < 0.000001f)
                return Quaternion.identity;

            forward.Normalize();

            return Quaternion.LookRotation(
                forward,
                Vector3.up);
        }


        // ====================================================
        // Master VR toggle
        // ====================================================

        private void ToggleMasterVR()
        {
            if (vrMasterWanted)
            {
                vrMasterWanted = false;
                vrActivationPending = false;
                continuousWanted = false;
                continuousVRRendering = false;
                stereoWanted = false;
                stereoRenderingEnabled = false;
                headAimActive = false;

                RestoreMouseLookPitch();
                RestorePlayMakerHorizontalHeadAim();
                RestorePlayMakerVerticalLookActions();
                RestoreVrPromptRootActivation();
                RestoreBodyMesh();
                RestoreHeadVisibility();

                if (playerCamera != null && neutralCameraRotationStored)
                    playerCamera.transform.localRotation = neutralCameraLocalRotation;

                trackingContributionApplied = false;
                referencePoseValid = false;

                MelonLogger.Msg("====================================");
                MelonLogger.Msg("VR OFF");
                MelonLogger.Msg("====================================");
                return;
            }

            vrMasterWanted = true;
            vrActivationPending = true;
            recenterRequested = true;

            if (!sessionStarted)
                StartOpenXR();

            FindPlayerObjects();
            FindFirstPersonController();
            FindBodyRenderer();

            MelonLogger.Msg("====================================");
            MelonLogger.Msg("VR START REQUESTED");
            MelonLogger.Msg("Waiting for stereo tracking if needed...");
            MelonLogger.Msg("====================================");
        }

        private bool ReadGamepadRecenterPressed()
        {
            if (!xInputAvailable)
                return false;

            bool pressed = false;
            try
            {
                for (uint index = 0; index < 4; index++)
                {
                    XInputState state;
                    bool down = XInputGetState(index, out state) == 0 &&
                        (state.Gamepad.Buttons & 0x0020) != 0; // XINPUT_GAMEPAD_BACK
                    if (down && !viewButtonWasDown[index])
                        pressed = true;
                    viewButtonWasDown[index] = down;
                }
            }
            catch (DllNotFoundException) { xInputAvailable = false; }
            catch (EntryPointNotFoundException) { xInputAvailable = false; }

            return pressed;
        }

        private void TryActivateMasterVR()
        {
            if (!vrMasterWanted || !vrActivationPending)
                return;

            if (!sessionStarted)
                return;

            if (playerCamera == null || (!useMenuCamera && (playerRoot == null || playerRigidbody == null)))
            {
                FindPlayerObjects();
                return;
            }

            if (!stereoPoseValid)
                return;

            if (!stereoCamerasCreated)
                CreateStereoCameras();

            if (!stereoRenderTexturesCreated)
                CreateStereoRenderTextures();

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            if (!nativeTexturePointersRegistered)
                return;

            stereoWanted = true;
            stereoRenderingEnabled = true;
            continuousWanted = true;
            continuousVRRendering = true;
            trackingEnabled = true;
            hideOwnHead = true;
            bodyFollowEnabled = true;

            FindFirstPersonController();
            FindBodyRenderer();
            BindPlayMakerCameraController();
            ApplyPlayMakerHeadAim();
            EnforceMouseLookPitchDisabled();
            ApplyVRBodyVisibility();
            ApplyHeadVisibility();

            RefreshFrameSubmitInfo();
            continuousStartSuccessfulFrames = successfulSubmittedFrames;

            vrActivationPending = false;
            pendingSceneRebuild = false;

            MelonLogger.Msg("====================================");
            MelonLogger.Msg("VR ON");
            MelonLogger.Msg("Head aiming requested: ON");
            MelonLogger.Msg("0.0.89 vertical interaction aim test: physical HMD ray pitch -> Camera Cntroller EulerY.");
            MelonLogger.Msg("Mouse/gamepad vertical look: DISABLED");
            MelonLogger.Msg("Mouse/gamepad horizontal turn: ENABLED");
            MelonLogger.Msg("Hands-only body: " + (bodyMeshModified ? "ACTIVE" : (bodyFallbackHidden ? "BODY HIDDEN" : "NOT ACTIVE")));
            MelonLogger.Msg("F9 = recenter yaw");
            MelonLogger.Msg("====================================");
        }

        // ====================================================
        // 0.0.61 direct PlayMaker camera controller
        // ====================================================

        private void ResetPlayMakerCameraBinding()
        {
            playMakerCameraController = null;
            playMakerCameraFsm = null;
            pmCamLookY = null;
            pmLookY = null;
            pmEulerY = null;
            pmEulerX = null;
            pmNeutralEulerY = 0.0f;
            pmNeutralEulerYStored = false;
            pmCameraBindingReady = false;
            pmCameraBindingLogged = false;
            pmLastInjectedHeadYaw = 0.0f;
            pmHeadYawInjected = false;
            pmDisabledVerticalActions.Clear();
            pmDisabledVerticalActionEveryFrame.Clear();
        }

        private bool SetMemberValue(object target, string memberName, object value)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return false;

            Type type = target.GetType();

            try
            {
                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, value, null);
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                FieldInfo field = type.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null)
                {
                    field.SetValue(target, value);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private object FindFsmFloatVariable(object fsm, string wantedName)
        {
            if (fsm == null || string.IsNullOrEmpty(wantedName))
                return null;

            object variables = GetMemberValue(fsm, "Variables");
            if (variables == null)
                return null;

            try
            {
                PropertyInfo[] properties = variables.GetType().GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (PropertyInfo property in properties)
                {
                    if (!property.CanRead)
                        continue;

                    Array array = null;
                    try { array = property.GetValue(variables, null) as Array; }
                    catch { }

                    if (array == null)
                        continue;

                    foreach (object variable in array)
                    {
                        if (variable == null)
                            continue;

                        string name = Convert.ToString(GetMemberValue(variable, "Name"));
                        if (string.Equals(name, wantedName, StringComparison.OrdinalIgnoreCase))
                            return variable;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private float ReadFsmFloat(object variable, float fallback)
        {
            if (variable == null)
                return fallback;

            try
            {
                object value = GetMemberValue(variable, "Value");
                if (value != null)
                    return Convert.ToSingle(value);
            }
            catch
            {
            }

            return fallback;
        }

        private void WriteFsmFloat(object variable, float value)
        {
            if (variable == null)
                return;

            SetMemberValue(variable, "Value", value);
        }

        private string GetFsmVariableName(object variable)
        {
            if (variable == null)
                return string.Empty;

            try
            {
                return Convert.ToString(GetMemberValue(variable, "Name")) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void BindPlayMakerCameraController()
        {
            if (pmCameraBindingReady)
                return;

            if (playerCamera == null)
                return;

            try
            {
                MonoBehaviour[] behaviours = playerCamera.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();
                    if (type.Name != "PlayMakerFSM" && type.FullName != "HutongGames.PlayMaker.PlayMakerFSM")
                        continue;

                    string fsmName = Convert.ToString(GetMemberValue(behaviour, "FsmName"));
                    object fsm = GetMemberValue(behaviour, "Fsm");
                    if (fsm == null)
                        fsm = GetMemberValue(behaviour, "fsm");

                    if (fsm == null)
                        continue;

                    if (string.IsNullOrEmpty(fsmName))
                        fsmName = Convert.ToString(GetMemberValue(fsm, "Name"));

                    if (!string.Equals(fsmName, "Camera Cntroller", StringComparison.OrdinalIgnoreCase))
                        continue;

                    playMakerCameraController = behaviour;
                    playMakerCameraFsm = fsm;
                    pmCamLookY = FindFsmFloatVariable(fsm, "CamLookY");
                    pmLookY = FindFsmFloatVariable(fsm, "LookY");
                    pmEulerY = FindFsmFloatVariable(fsm, "EulerY");
                    pmEulerX = FindFsmFloatVariable(fsm, "EulerX");

                    if (pmEulerY != null)
                    {
                        pmNeutralEulerY = ReadFsmFloat(pmEulerY, 0.0f);
                        pmNeutralEulerYStored = true;
                    }

                    DisablePlayMakerVerticalLookActions();

                    pmCameraBindingReady = pmEulerY != null;

                    if (!pmCameraBindingLogged)
                    {
                        MelonLogger.Msg("0.0.61 PlayMaker camera binding: " + (pmCameraBindingReady ? "READY" : "INCOMPLETE"));
                        MelonLogger.Msg("  FSM: Camera Cntroller");
                        MelonLogger.Msg("  CamLookY: " + (pmCamLookY != null));
                        MelonLogger.Msg("  LookY: " + (pmLookY != null));
                        MelonLogger.Msg("  EulerY: " + (pmEulerY != null));
                        MelonLogger.Msg("  EulerX: " + (pmEulerX != null));
                        MelonLogger.Msg("  Neutral EulerY: " + pmNeutralEulerY);
                        MelonLogger.Msg("  Disabled vertical Rewired actions: " + pmDisabledVerticalActions.Count);
                        pmCameraBindingLogged = true;
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("0.0.61 PlayMaker camera binding failed: " + ex.Message);
            }
        }

        private void DisablePlayMakerVerticalLookActions()
        {
            if (playMakerCameraFsm == null)
                return;

            if (pmDisabledVerticalActions.Count > 0)
                return;

            try
            {
                Array states = GetMemberValue(playMakerCameraFsm, "States") as Array;
                if (states == null)
                    return;

                foreach (object state in states)
                {
                    if (state == null)
                        continue;

                    Array actions = GetMemberValue(state, "Actions") as Array;
                    if (actions == null)
                        continue;

                    foreach (object action in actions)
                    {
                        if (action == null)
                            continue;

                        Type actionType = action.GetType();
                        string fullName = actionType.FullName ?? actionType.Name;
                        if (fullName.IndexOf("RewiredPlayerGetAxis", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        object storeValue = GetMemberValue(action, "storeValue");
                        string storeName = GetFsmVariableName(storeValue);

                        if (!string.Equals(storeName, "CamLookY", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(storeName, "LookY", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        bool oldEveryFrame = true;
                        object oldValue = GetMemberValue(action, "everyFrame");
                        if (oldValue != null)
                        {
                            try { oldEveryFrame = Convert.ToBoolean(oldValue); }
                            catch { oldEveryFrame = true; }
                        }

                        pmDisabledVerticalActions.Add(action);
                        pmDisabledVerticalActionEveryFrame.Add(oldEveryFrame);

                        SetMemberValue(action, "everyFrame", false);
                        SetMemberValue(action, "Enabled", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Could not disable PlayMaker vertical look actions: " + ex.Message);
            }
        }

        private void RestorePlayMakerVerticalLookActions()
        {
            for (int i = 0; i < pmDisabledVerticalActions.Count; i++)
            {
                object action = pmDisabledVerticalActions[i];
                if (action == null)
                    continue;

                bool everyFrame = true;
                if (i < pmDisabledVerticalActionEveryFrame.Count)
                    everyFrame = pmDisabledVerticalActionEveryFrame[i];

                SetMemberValue(action, "everyFrame", everyFrame);
                SetMemberValue(action, "Enabled", true);
            }

            pmDisabledVerticalActions.Clear();
            pmDisabledVerticalActionEveryFrame.Clear();
        }

        private float GetRelativeHeadPitchDegrees()
        {
            if (!currentPoseValid || !referencePoseValid)
                return 0.0f;

            Quaternion relativeRotation = Quaternion.Inverse(referenceHeadRotation) * currentHeadRotation;
            Vector3 forward = relativeRotation * Vector3.forward;
            float horizontal = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);

            if (horizontal < 0.000001f)
                horizontal = 0.000001f;

            return Mathf.Atan2(-forward.y, horizontal) * Mathf.Rad2Deg;
        }

        private float GetVrInteractionPitchDegrees()
        {
            if (playerCamera == null)
                return pmNeutralEulerY;

            Vector3 forward = playerCamera.transform.forward.normalized;

            // Unity forward.y is positive when looking upward.
            // The game's Camera Cntroller EulerY uses the opposite sign.
            float physicalPitch =
                Mathf.Asin(Mathf.Clamp(forward.y, -1.0f, 1.0f)) *
                Mathf.Rad2Deg;

            return -physicalPitch;
        }

        private float GetRelativeHeadYawDegrees()
        {
            if (!currentPoseValid || !referencePoseValid)
                return 0.0f;

            Quaternion relativeRotation = Quaternion.Inverse(referenceHeadRotation) * currentHeadRotation;
            Vector3 forward = relativeRotation * Vector3.forward;
            forward.y = 0.0f;

            if (forward.sqrMagnitude < 0.000001f)
                return 0.0f;

            forward.Normalize();
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private void RestorePlayMakerHorizontalHeadAim()
        {
            if (!pmHeadYawInjected || pmEulerX == null)
                return;

            float currentEulerX = ReadFsmFloat(pmEulerX, 0.0f);
            WriteFsmFloat(pmEulerX, currentEulerX - pmLastInjectedHeadYaw);
            pmLastInjectedHeadYaw = 0.0f;
            pmHeadYawInjected = false;
        }

        private void ApplyPlayMakerHeadAim()
        {
            if (!vrMasterWanted || !sessionStarted || useMenuCamera)
                return;

            if (!pmCameraBindingReady)
                BindPlayMakerCameraController();

            if (!pmCameraBindingReady || !pmNeutralEulerYStored)
                return;

            DisablePlayMakerVerticalLookActions();

            // Vertical mouse/gamepad look stays disabled. EulerY is driven directly
            // by physical HMD pitch.
            WriteFsmFloat(pmCamLookY, 0.0f);
            WriteFsmFloat(pmLookY, 0.0f);

            float hmdPitch = GetRelativeHeadPitchDegrees();
            // 0.0.89 controlled vertical interaction-aim test:
            // Drive the game's virtual vertical aim directly from the final
            // physical HMD/camera ray instead of adding HMD pitch to the
            // desktop camera's neutral EulerY.
            WriteFsmFloat(pmEulerY, GetVrInteractionPitchDegrees());

            // Keep the game's normal horizontal mouse/gamepad accumulation, but add
            // the current physical HMD yaw to EulerX. Before adding the new HMD yaw,
            // remove the value injected on the previous pass. This prevents drift and
            // lets mouse/gamepad X and HMD yaw share the same interaction aim.
            if (pmEulerX != null)
            {
                float currentEulerX = ReadFsmFloat(pmEulerX, 0.0f);
                float gameEulerX = pmHeadYawInjected
                    ? currentEulerX - pmLastInjectedHeadYaw
                    : currentEulerX;

                float hmdYaw = GetRelativeHeadYawDegrees();
                WriteFsmFloat(pmEulerX, gameEulerX + hmdYaw);
                pmLastInjectedHeadYaw = hmdYaw;
                pmHeadYawInjected = true;
            }

            headAimActive = true;
        }

        // ====================================================
        // Head aiming / mouse-look pitch suppression
        // ====================================================

        private void FindFirstPersonController()
        {
            firstPersonController = null;
            mouseLookObject = null;
            headAimControllerFound = false;

            // 0.0.61: first try Unity's string based GetComponent lookup directly
            // on PLAYER. This is more reliable for this game than enumerating
            // Component objects through reflection.
            if (playerRoot != null)
            {
                try
                {
                    firstPersonController =
                        playerRoot.gameObject.GetComponent("RigidbodyFirstPersonController");
                }
                catch
                {
                    firstPersonController = null;
                }
            }

            // Exact Assembly-CSharp type lookup.
            if (firstPersonController == null && playerRoot != null)
            {
                try
                {
                    Type exactType = null;
                    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    foreach (Assembly assembly in assemblies)
                    {
                        Type candidate = assembly.GetType(
                            "UnitySampleAssets.Characters.FirstPerson.RigidbodyFirstPersonController",
                            false,
                            false);

                        if (candidate != null)
                        {
                            exactType = candidate;
                            break;
                        }
                    }

                    if (exactType != null)
                    {
                        firstPersonController = playerRoot.GetComponent(exactType);

                        if (firstPersonController == null)
                        {
                            Component[] all =
                                playerRoot.GetComponentsInChildren(exactType, true);

                            if (all != null && all.Length > 0)
                                firstPersonController = all[0];
                        }
                    }
                }
                catch
                {
                    firstPersonController = null;
                }
            }

            // Generic hierarchy fallback.
            if (firstPersonController == null && playerRoot != null)
            {
                try
                {
                    Component[] candidates =
                        playerRoot.GetComponentsInChildren<Component>(true);

                    if (candidates != null)
                    {
                        foreach (Component component in candidates)
                        {
                            if (IsFirstPersonControllerComponent(component))
                            {
                                firstPersonController = component;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            // Scene-wide fallback.
            if (firstPersonController == null)
            {
                try
                {
                    MonoBehaviour[] behaviours =
                        UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

                    foreach (MonoBehaviour behaviour in behaviours)
                    {
                        if (IsFirstPersonControllerComponent(behaviour))
                        {
                            firstPersonController = behaviour;
                            break;
                        }
                    }
                }
                catch
                {
                }
            }

            if (firstPersonController == null)
            {
                LogPlayerBehaviourTypes();
                return;
            }

            Type controllerType = firstPersonController.GetType();
            object mouseLook = FindMouseLookObject(firstPersonController, controllerType);

            if (mouseLook == null)
            {
                headAimControllerFound = false;
                headAimActive = false;

                if (!mouseLookLookupWarningLogged)
                {
                    mouseLookLookupWarningLogged = true;
                    MelonLogger.Warning(
                        "First-person controller found, but no MouseLook object could be resolved: "
                        + controllerType.FullName);
                    LogControllerFields(controllerType, firstPersonController);
                }
                return;
            }

            mouseLookLookupWarningLogged = false;
            mouseLookObject = mouseLook;
            Type mouseLookType = mouseLookObject.GetType();

            mouseLookYSensitivityField = FindFieldByNames(
                mouseLookType,
                "YSensitivity", "ySensitivity", "m_YSensitivity", "m_ySensitivity");

            mouseLookYSensitivityProperty = FindPropertyByNames(
                mouseLookType,
                "YSensitivity", "ySensitivity");

            mouseLookCameraTargetRotField = FindFieldByNames(
                mouseLookType,
                "m_CameraTargetRot", "CameraTargetRot", "cameraTargetRot", "m_cameraTargetRot");

            mouseLookSmoothField = FindFieldByNames(
                mouseLookType,
                "smooth", "m_Smooth", "m_smooth");

            mouseLookSmoothProperty = FindPropertyByNames(
                mouseLookType,
                "smooth", "Smooth");

            try
            {
                if (!originalMouseLookYSensitivityStored)
                {
                    if (mouseLookYSensitivityField != null)
                    {
                        object value = mouseLookYSensitivityField.GetValue(mouseLookObject);
                        originalMouseLookYSensitivity = Convert.ToSingle(value);
                        originalMouseLookYSensitivityStored = true;
                    }
                    else if (
                        mouseLookYSensitivityProperty != null
                        &&
                        mouseLookYSensitivityProperty.CanRead
                        )
                    {
                        object value = mouseLookYSensitivityProperty.GetValue(mouseLookObject, null);
                        originalMouseLookYSensitivity = Convert.ToSingle(value);
                        originalMouseLookYSensitivityStored = true;
                    }

                    if (mouseLookSmoothField != null)
                    {
                        object smoothValue = mouseLookSmoothField.GetValue(mouseLookObject);
                        originalMouseLookSmooth = Convert.ToBoolean(smoothValue);
                        originalMouseLookSmoothStored = true;
                    }
                    else if (
                        mouseLookSmoothProperty != null
                        &&
                        mouseLookSmoothProperty.CanRead
                        )
                    {
                        object smoothValue = mouseLookSmoothProperty.GetValue(mouseLookObject, null);
                        originalMouseLookSmooth = Convert.ToBoolean(smoothValue);
                        originalMouseLookSmoothStored = true;
                    }
                }
            }
            catch
            {
            }

            headAimControllerFound = true;

            MelonLogger.Msg(
                "First-person controller found: "
                + controllerType.FullName
                + " on "
                + GetTransformPath(firstPersonController.transform));

            MelonLogger.Msg(
                "MouseLook found: "
                + mouseLookType.FullName
                + ", YSensitivity="
                + (mouseLookYSensitivityField != null || mouseLookYSensitivityProperty != null)
                + ", CameraTargetRot="
                + (mouseLookCameraTargetRotField != null));
        }

        private object FindMouseLookObject(Component controller, Type controllerType)
        {
            if (controller == null || controllerType == null)
                return null;

            string[] preferredNames = new string[]
            {
                "mouseLook",
                "m_MouseLook",
                "MouseLook",
                "m_mouseLook"
            };

            Type scanType = controllerType;
            while (scanType != null)
            {
                foreach (string name in preferredNames)
                {
                    try
                    {
                        FieldInfo field = scanType.GetField(
                            name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                        if (field != null)
                        {
                            object value = field.GetValue(controller);
                            if (value != null)
                            {
                                MelonLogger.Msg("MouseLook resolved from field: " + scanType.FullName + "." + field.Name);
                                return value;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                scanType = scanType.BaseType;
            }

            scanType = controllerType;
            while (scanType != null)
            {
                try
                {
                    FieldInfo[] fields = scanType.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    foreach (FieldInfo field in fields)
                    {
                        string fieldName = field.Name ?? string.Empty;
                        string fieldTypeName = field.FieldType != null ? (field.FieldType.FullName ?? field.FieldType.Name) : string.Empty;

                        if (
                            fieldName.IndexOf("mouselook", StringComparison.OrdinalIgnoreCase) < 0
                            &&
                            fieldTypeName.IndexOf("mouselook", StringComparison.OrdinalIgnoreCase) < 0
                            )
                        {
                            continue;
                        }

                        object value = field.GetValue(controller);
                        if (value != null)
                        {
                            MelonLogger.Msg("MouseLook resolved by type/name scan: " + scanType.FullName + "." + field.Name + " -> " + fieldTypeName);
                            return value;
                        }
                    }
                }
                catch
                {
                }

                scanType = scanType.BaseType;
            }

            scanType = controllerType;
            while (scanType != null)
            {
                try
                {
                    PropertyInfo[] properties = scanType.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    foreach (PropertyInfo property in properties)
                    {
                        if (!property.CanRead)
                            continue;

                        string propertyName = property.Name ?? string.Empty;
                        string propertyTypeName = property.PropertyType != null ? (property.PropertyType.FullName ?? property.PropertyType.Name) : string.Empty;

                        if (
                            propertyName.IndexOf("mouselook", StringComparison.OrdinalIgnoreCase) < 0
                            &&
                            propertyTypeName.IndexOf("mouselook", StringComparison.OrdinalIgnoreCase) < 0
                            )
                        {
                            continue;
                        }

                        object value = property.GetValue(controller, null);
                        if (value != null)
                        {
                            MelonLogger.Msg("MouseLook resolved from property: " + scanType.FullName + "." + property.Name + " -> " + propertyTypeName);
                            return value;
                        }
                    }
                }
                catch
                {
                }

                scanType = scanType.BaseType;
            }

            return null;
        }

        private FieldInfo FindFieldByNames(Type type, params string[] names)
        {
            Type scanType = type;
            while (scanType != null)
            {
                foreach (string name in names)
                {
                    try
                    {
                        FieldInfo field = scanType.GetField(
                            name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (field != null)
                            return field;
                    }
                    catch
                    {
                    }
                }
                scanType = scanType.BaseType;
            }
            return null;
        }

        private PropertyInfo FindPropertyByNames(Type type, params string[] names)
        {
            Type scanType = type;
            while (scanType != null)
            {
                foreach (string name in names)
                {
                    try
                    {
                        PropertyInfo property = scanType.GetProperty(
                            name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (property != null)
                            return property;
                    }
                    catch
                    {
                    }
                }
                scanType = scanType.BaseType;
            }
            return null;
        }

        private void LogControllerFields(Type controllerType, Component controller)
        {
            if (controllerType == null || controller == null)
                return;

            try
            {
                MelonLogger.Msg("Controller field scan:");
                Type scanType = controllerType;
                while (scanType != null)
                {
                    FieldInfo[] fields = scanType.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    foreach (FieldInfo field in fields)
                    {
                        string typeName = field.FieldType != null ? (field.FieldType.FullName ?? field.FieldType.Name) : "<unknown>";
                        MelonLogger.Msg("  Field: " + scanType.FullName + "." + field.Name + " : " + typeName);
                    }
                    scanType = scanType.BaseType;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Could not enumerate controller fields: " + ex.Message);
            }
        }

        private void LogPlayerBehaviourTypes()
        {
            if (playerRoot == null)
                return;

            try
            {
                MonoBehaviour[] behaviours =
                    playerRoot.GetComponentsInChildren<MonoBehaviour>(true);

                MelonLogger.Warning(
                    "RigidbodyFirstPersonController was not found. PLAYER MonoBehaviours:");

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();
                    MelonLogger.Msg(
                        "  Behaviour: "
                        + type.FullName
                        + " on "
                        + GetTransformPath(behaviour.transform));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "Could not enumerate PLAYER MonoBehaviours: " + ex.Message);
            }
        }

        private bool IsFirstPersonControllerComponent(Component component)
        {
            if (component == null)
                return false;

            try
            {
                Type type = component.GetType();
                string fullName = type.FullName ?? type.Name;

                return
                    fullName.IndexOf(
                        "RigidbodyFirstPersonController",
                        StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private void EnforceMouseLookPitchDisabled()
        {
            if (!vrMasterWanted || useMenuCamera)
                return;

            if (!headAimControllerFound || mouseLookObject == null)
                FindFirstPersonController();

            if (mouseLookObject == null)
                return;

            try
            {
                if (mouseLookYSensitivityField != null)
                    mouseLookYSensitivityField.SetValue(mouseLookObject, 0.0f);

                if (
                    mouseLookYSensitivityProperty != null
                    &&
                    mouseLookYSensitivityProperty.CanWrite
                    )
                {
                    mouseLookYSensitivityProperty.SetValue(mouseLookObject, 0.0f, null);
                }

                if (mouseLookSmoothField != null)
                    mouseLookSmoothField.SetValue(mouseLookObject, false);

                if (
                    mouseLookSmoothProperty != null
                    &&
                    mouseLookSmoothProperty.CanWrite
                    )
                {
                    mouseLookSmoothProperty.SetValue(mouseLookObject, false, null);
                }

                if (
                    mouseLookCameraTargetRotField != null
                    &&
                    neutralCameraRotationStored
                    )
                {
                    Quaternion aimRotation = neutralCameraLocalRotation;

                    if (currentPoseValid && referencePoseValid)
                    {
                        Quaternion relativeRotation =
                            Quaternion.Inverse(referenceHeadRotation) *
                            currentHeadRotation;

                        aimRotation = GetGameYawRotation() * relativeRotation;
                    }

                    mouseLookCameraTargetRotField.SetValue(
                        mouseLookObject,
                        aimRotation);
                }

                headAimActive = true;
            }
            catch (Exception ex)
            {
                headAimActive = false;
                MelonLogger.Warning("Could not suppress vertical mouse look: " + ex.Message);
            }
        }

        private void RestoreMouseLookPitch()
        {
            if (mouseLookObject == null)
                return;

            if (!originalMouseLookYSensitivityStored)
                return;

            try
            {
                if (mouseLookYSensitivityField != null)
                    mouseLookYSensitivityField.SetValue(mouseLookObject, originalMouseLookYSensitivity);

                if (
                    mouseLookYSensitivityProperty != null
                    &&
                    mouseLookYSensitivityProperty.CanWrite
                    )
                {
                    mouseLookYSensitivityProperty.SetValue(
                        mouseLookObject,
                        originalMouseLookYSensitivity,
                        null);
                }

                if (originalMouseLookSmoothStored)
                {
                    if (mouseLookSmoothField != null)
                        mouseLookSmoothField.SetValue(mouseLookObject, originalMouseLookSmooth);

                    if (
                        mouseLookSmoothProperty != null
                        &&
                        mouseLookSmoothProperty.CanWrite
                        )
                    {
                        mouseLookSmoothProperty.SetValue(
                            mouseLookObject,
                            originalMouseLookSmooth,
                            null);
                    }
                }
            }
            catch
            {
            }
        }

        private void ApplyHeadAimEarly()
        {
            if (
                !vrMasterWanted
                || useMenuCamera
                ||
                playerCamera == null
                ||
                !currentPoseValid
                ||
                !referencePoseValid
                ||
                !neutralCameraRotationStored
                )
            {
                return;
            }

            Quaternion relativeRotation =
                Quaternion.Inverse(referenceHeadRotation) *
                currentHeadRotation;

            playerCamera.transform.localRotation =
                GetGameYawRotation() *
                relativeRotation;
        }

        // ====================================================
        // Hands-only body
        // ====================================================

        private bool IsHandBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return false;

            string n = boneName.ToLowerInvariant();

            return
                n.Contains("hand")
                || n.Contains("thumb")
                || n.Contains("index")
                || n.Contains("middle")
                || n.Contains("ring")
                || n.Contains("pinky")
                || n.Contains("little");
        }

        private float GetVertexHandWeight(BoneWeight bw, bool[] handBones)
        {
            float weight = 0.0f;

            if (bw.boneIndex0 >= 0 && bw.boneIndex0 < handBones.Length && handBones[bw.boneIndex0])
                weight += bw.weight0;
            if (bw.boneIndex1 >= 0 && bw.boneIndex1 < handBones.Length && handBones[bw.boneIndex1])
                weight += bw.weight1;
            if (bw.boneIndex2 >= 0 && bw.boneIndex2 < handBones.Length && handBones[bw.boneIndex2])
                weight += bw.weight2;
            if (bw.boneIndex3 >= 0 && bw.boneIndex3 < handBones.Length && handBones[bw.boneIndex3])
                weight += bw.weight3;

            return weight;
        }

        private void FindBodyRenderer()
        {
            if (playerRoot == null)
                return;

            SkinnedMeshRenderer[] renderers =
                playerRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (renderer.name == "Body003")
                {
                    bodyRenderer = renderer;
                    break;
                }
            }

            if (bodyRenderer == null)
            {
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    string n = renderer.name.ToLowerInvariant();
                    if (n.Contains("body") || n.Contains("mandy"))
                    {
                        bodyRenderer = renderer;
                        break;
                    }
                }
            }

            if (bodyRenderer != null && originalBodyMesh == null)
            {
                originalBodyMesh = bodyRenderer.sharedMesh;
                originalBodyRendererEnabled = bodyRenderer.enabled;
            }
        }

        private void ApplyVRBodyVisibility()
        {
            if (!vrMasterWanted)
                return;

            if (bodyMeshModified || bodyFallbackHidden)
                return;

            if (bodyRenderer == null || originalBodyMesh == null)
                FindBodyRenderer();

            if (bodyRenderer == null || originalBodyMesh == null)
                return;

            try
            {
                Transform[] bones = bodyRenderer.bones;
                BoneWeight[] boneWeights = originalBodyMesh.boneWeights;

                if (
                    bones == null
                    ||
                    bones.Length == 0
                    ||
                    boneWeights == null
                    ||
                    boneWeights.Length != originalBodyMesh.vertexCount
                    )
                {
                    throw new InvalidOperationException("Body mesh has no usable skin weights.");
                }

                bool[] handBones = new bool[bones.Length];
                int handBoneCount = 0;

                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone = bones[i];
                    if (bone != null && IsHandBoneName(bone.name))
                    {
                        handBones[i] = true;
                        handBoneCount++;
                    }
                }

                if (handBoneCount == 0)
                    throw new InvalidOperationException("No hand/finger bones were found.");

                float[] handWeights = new float[boneWeights.Length];
                for (int i = 0; i < boneWeights.Length; i++)
                    handWeights[i] = GetVertexHandWeight(boneWeights[i], handBones);

                Mesh clone = UnityEngine.Object.Instantiate(originalBodyMesh);
                clone.name = originalBodyMesh.name + "_TCGVR_HandsOnly";

                int keptTriangles = 0;

                for (int subMesh = 0; subMesh < originalBodyMesh.subMeshCount; subMesh++)
                {
                    int[] triangles = originalBodyMesh.GetTriangles(subMesh);
                    List<int> kept = new List<int>(triangles.Length / 8);

                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        int a = triangles[i];
                        int b = triangles[i + 1];
                        int c = triangles[i + 2];

                        float wa = handWeights[a];
                        float wb = handWeights[b];
                        float wc = handWeights[c];

                        int strongVertices = 0;
                        if (wa >= 0.20f) strongVertices++;
                        if (wb >= 0.20f) strongVertices++;
                        if (wc >= 0.20f) strongVertices++;

                        float average = (wa + wb + wc) / 3.0f;

                        if (strongVertices >= 2 || average >= 0.30f)
                        {
                            kept.Add(a);
                            kept.Add(b);
                            kept.Add(c);
                            keptTriangles++;
                        }
                    }

                    clone.SetTriangles(kept.ToArray(), subMesh, false);
                }

                if (keptTriangles == 0)
                {
                    UnityEngine.Object.Destroy(clone);
                    throw new InvalidOperationException("No hand triangles passed the skin-weight filter.");
                }

                clone.RecalculateBounds();

                handsOnlyBodyMesh = clone;
                bodyRenderer.sharedMesh = handsOnlyBodyMesh;
                bodyRenderer.enabled = true;
                bodyMeshModified = true;

                MelonLogger.Msg(
                    "Hands-only body mesh active. Kept triangles: "
                    + keptTriangles
                    + ", hand bones: "
                    + handBoneCount);
            }
            catch (Exception ex)
            {
                bodyRenderer.enabled = false;
                bodyFallbackHidden = true;

                MelonLogger.Warning(
                    "Hands-only mesh could not be created. Full body hidden instead: "
                    + ex.Message);
            }
        }

        private void RestoreBodyMesh()
        {
            if (bodyRenderer != null)
            {
                if (bodyMeshModified && originalBodyMesh != null)
                    bodyRenderer.sharedMesh = originalBodyMesh;

                if (bodyFallbackHidden)
                    bodyRenderer.enabled = originalBodyRendererEnabled;
            }

            if (handsOnlyBodyMesh != null)
            {
                UnityEngine.Object.Destroy(handsOnlyBodyMesh);
                handsOnlyBodyMesh = null;
            }

            bodyMeshModified = false;
            bodyFallbackHidden = false;
        }


        // ====================================================
        // OpenXR Start
        // ====================================================

        private void StartOpenXR()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("Starting OpenXR session...");

            if (sessionStarted)
            {
                MelonLogger.Msg("Session already running.");
                return;
            }

            if (!openXRInitialized || !hmdFound)
            {
                MelonLogger.Error("OpenXR/HMD not ready.");
                return;
            }

            FindPlayerObjects();

            if (playerCamera == null || (!useMenuCamera && (playerRoot == null || playerRigidbody == null)))
            {
                MelonLogger.Error("PLAYER/camera not found.");
                return;
            }

            int sessionResult = TCGVR_CreateSession();
            MelonLogger.Msg("Session creation: " + sessionResult);
            if (sessionResult != 1)
                return;

            if (!ReadStereoConfiguration())
            {
                MelonLogger.Error("Stereo configuration failed.");
                return;
            }

            int swapchainResult = TCGVR_CreateStereoSwapchains(stereoRenderWidth, stereoRenderHeight);
            MelonLogger.Msg("Swapchain creation: " + swapchainResult);
            if (swapchainResult != 1)
                return;

            int infoResult = TCGVR_GetSwapchainInfo(
                out openXRSwapchainWidth,
                out openXRSwapchainHeight,
                out openXRSwapchainFormat,
                out openXRLeftImageCount,
                out openXRRightImageCount);

            if (infoResult != 1)
                return;

            openXRSwapchainsCreated = true;
            MelonLogger.Msg("OpenXR Swapchains: " + openXRSwapchainWidth + " x " + openXRSwapchainHeight);

            int threadResult = TCGVR_StartTrackingThread();
            MelonLogger.Msg("Tracking thread: " + threadResult);
            if (threadResult != 1)
                return;

            trackingPredictionMs = TCGVR_SetTrackingPredictionMs(DefaultTrackingPredictionMs);
            MelonLogger.Msg("Tracking Prediction: " + trackingPredictionMs + " ms (0.0.89 snapshot mode)");

            sessionStarted = true;
            recenterRequested = true;
            currentPoseValid = false;
            stereoPoseValid = false;
            hideOwnHead = true;
            ApplyHeadVisibility();

            MelonLogger.Msg("0.0.89 OpenXR session ready.");
            MelonLogger.Msg("====================================");
        }

        private bool ReadStereoConfiguration()
        {
            int result = TCGVR_GetStereoConfiguration(
                out stereoViewCount,
                out recommendedWidth,
                out recommendedHeight,
                out recommendedSampleCount);

            if (result != 1)
                return false;

            stereoRenderWidth = Mathf.Max(1, Mathf.RoundToInt(recommendedWidth * StereoRenderScale));
            stereoRenderHeight = Mathf.Max(1, Mathf.RoundToInt(recommendedHeight * StereoRenderScale));

            MelonLogger.Msg("OpenXR recommended resolution: " + recommendedWidth + " x " + recommendedHeight);
            MelonLogger.Msg("Render resolution: " + stereoRenderWidth + " x " + stereoRenderHeight);
            return true;
        }

        // ====================================================
        // F1 / F6
        // ====================================================

        private void ToggleContinuousVR()
        {
            if (continuousWanted || continuousVRRendering)
            {
                continuousWanted = false;
                continuousVRRendering = false;
                MelonLogger.Msg("CONTINUOUS VR: OFF");
                return;
            }

            if (!sessionStarted)
            {
                MelonLogger.Warning("Start OpenXR first.");
                return;
            }

            if (!stereoWanted && !stereoRenderingEnabled)
            {
                MelonLogger.Warning("Enable stereo first.");
                return;
            }

            if (!stereoRenderTexturesCreated)
                CreateStereoRenderTextures();

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            if (!nativeTexturePointersRegistered)
            {
                MelonLogger.Error("Submit texture pointers are missing.");
                return;
            }

            burstActive = false;
            RefreshFrameSubmitInfo();
            continuousStartSuccessfulFrames = successfulSubmittedFrames;
            continuousWanted = true;
            continuousVRRendering = true;

            MelonLogger.Msg("====================================");
            MelonLogger.Msg("CONTINUOUS VR: ON");
            MelonLogger.Msg("Vertical flip: ON");
            MelonLogger.Msg("HUD/UI camera override: ON");
            MelonLogger.Msg("Tracking Prediction: " + trackingPredictionMs + " ms (0.0.89 snapshot mode)");
            MelonLogger.Msg("====================================");
        }

        private void ToggleStereoRendering()
        {
            if (!sessionStarted || !stereoPoseValid)
            {
                MelonLogger.Warning("Stereo tracking is not ready yet.");
                return;
            }

            if (continuousVRRendering)
            {
                MelonLogger.Warning("Disable continuous VR first.");
                return;
            }

            stereoWanted = !stereoWanted;
            stereoRenderingEnabled = stereoWanted;

            if (stereoRenderingEnabled)
            {
                if (!stereoCamerasCreated)
                    CreateStereoCameras();
                if (!stereoRenderTexturesCreated)
                    CreateStereoRenderTextures();
            }

            MelonLogger.Msg("Stereo rendering: " + (stereoRenderingEnabled ? "ON" : "OFF"));
        }

        // ====================================================
        // Scene Resume
        // ====================================================

        private void TryResumeVRAfterSceneLoad()
        {
            if (!sessionStarted || playerCamera == null || !stereoPoseValid)
                return;

            if (stereoWanted)
            {
                if (!stereoCamerasCreated)
                    CreateStereoCameras();

                if (!stereoRenderTexturesCreated)
                    CreateStereoRenderTextures();

                stereoRenderingEnabled = true;
            }

            if (continuousWanted)
            {
                if (!nativeTexturePointersRegistered)
                    RegisterNativeSubmitTextures();

                if (!nativeTexturePointersRegistered)
                    return;

                continuousVRRendering = true;
            }

            recenterRequested = true;
            pendingSceneRebuild = false;

            if (vrMasterWanted)
            {
                FindFirstPersonController();
                FindBodyRenderer();
                EnforceMouseLookPitchDisabled();
                ApplyVRBodyVisibility();
                ApplyHeadVisibility();
            }

            MelonLogger.Msg("====================================");
            MelonLogger.Msg("VR RESTORED AFTER SCENE CHANGE");
            MelonLogger.Msg("Stereo active: " + stereoRenderingEnabled);
            MelonLogger.Msg("Continuous VR active: " + continuousVRRendering);
            MelonLogger.Msg("====================================");
        }

        // ====================================================
        // Tracking Data
        // ====================================================

        private void LogOpenXRStatus()
        {
            int state = TCGVR_GetSessionState();
            if (state != lastSessionState)
            {
                lastSessionState = state;
                MelonLogger.Msg("OpenXR Session State: " + state);
            }

            int result = TCGVR_GetLastTrackingResult();
            if (result != lastTrackingResult)
            {
                lastTrackingResult = result;
                MelonLogger.Msg("Tracking result: " + result);
            }
        }

        private void ReadHeadPose()
        {
            int result = TCGVR_GetHeadPose(
                out float px, out float py, out float pz,
                out float rx, out float ry, out float rz, out float rw);

            if (result != 1)
            {
                currentPoseValid = false;
                return;
            }

            currentHeadPosition = new Vector3(px, py, -pz);
            currentHeadRotation = new Quaternion(-rx, -ry, rz, rw);
            currentPoseValid = true;
        }

        private void ReadStereoData()
        {
            int leftResult = TCGVR_GetLeftEye(
                out float lpx, out float lpy, out float lpz,
                out float lrx, out float lry, out float lrz, out float lrw,
                out leftAngleLeft, out leftAngleRight, out leftAngleUp, out leftAngleDown);

            int rightResult = TCGVR_GetRightEye(
                out float rpx, out float rpy, out float rpz,
                out float rrx, out float rry, out float rrz, out float rrw,
                out rightAngleLeft, out rightAngleRight, out rightAngleUp, out rightAngleDown);

            if (leftResult != 1 || rightResult != 1 || !currentPoseValid)
            {
                stereoPoseValid = false;
                return;
            }

            leftEyePosition = new Vector3(lpx, lpy, -lpz);
            leftEyeRotation = new Quaternion(-lrx, -lry, lrz, lrw);
            rightEyePosition = new Vector3(rpx, rpy, -rpz);
            rightEyeRotation = new Quaternion(-rrx, -rry, rrz, rrw);

            Quaternion inverseHead = Quaternion.Inverse(currentHeadRotation);

            leftEyeRelativePosition = inverseHead * (leftEyePosition - currentHeadPosition);
            rightEyeRelativePosition = inverseHead * (rightEyePosition - currentHeadPosition);
            leftEyeRelativeRotation = inverseHead * leftEyeRotation;
            rightEyeRelativeRotation = inverseHead * rightEyeRotation;

            stereoPoseValid = true;

            if (!stereoCamerasCreated && (stereoWanted || pendingSceneRebuild))
                CreateStereoCameras();
        }

        // ====================================================
        // 0.0.38 render-pose snapshot
        // ====================================================

        private bool ReadRenderPoseData()
        {
            int headResult =
                TCGVR_GetRenderHeadPose(
                    out float hpx,
                    out float hpy,
                    out float hpz,
                    out float hrx,
                    out float hry,
                    out float hrz,
                    out float hrw);

            int leftResult =
                TCGVR_GetRenderLeftEye(
                    out float lpx,
                    out float lpy,
                    out float lpz,
                    out float lrx,
                    out float lry,
                    out float lrz,
                    out float lrw,
                    out float lAngleLeft,
                    out float lAngleRight,
                    out float lAngleUp,
                    out float lAngleDown);

            int rightResult =
                TCGVR_GetRenderRightEye(
                    out float rpx,
                    out float rpy,
                    out float rpz,
                    out float rrx,
                    out float rry,
                    out float rrz,
                    out float rrw,
                    out float rAngleLeft,
                    out float rAngleRight,
                    out float rAngleUp,
                    out float rAngleDown);

            if (
                headResult != 1
                ||
                leftResult != 1
                ||
                rightResult != 1
                )
            {
                return false;
            }

            currentHeadPosition =
                new Vector3(
                    hpx,
                    hpy,
                    -hpz);

            currentHeadRotation =
                new Quaternion(
                    -hrx,
                    -hry,
                    hrz,
                    hrw);

            currentPoseValid = true;

            leftEyePosition =
                new Vector3(
                    lpx,
                    lpy,
                    -lpz);

            leftEyeRotation =
                new Quaternion(
                    -lrx,
                    -lry,
                    lrz,
                    lrw);

            rightEyePosition =
                new Vector3(
                    rpx,
                    rpy,
                    -rpz);

            rightEyeRotation =
                new Quaternion(
                    -rrx,
                    -rry,
                    rrz,
                    rrw);

            leftAngleLeft = lAngleLeft;
            leftAngleRight = lAngleRight;
            leftAngleUp = lAngleUp;
            leftAngleDown = lAngleDown;

            rightAngleLeft = rAngleLeft;
            rightAngleRight = rAngleRight;
            rightAngleUp = rAngleUp;
            rightAngleDown = rAngleDown;

            Quaternion inverseHead =
                Quaternion.Inverse(
                    currentHeadRotation);

            leftEyeRelativePosition =
                inverseHead *
                (
                    leftEyePosition
                    -
                    currentHeadPosition
                );

            rightEyeRelativePosition =
                inverseHead *
                (
                    rightEyePosition
                    -
                    currentHeadPosition
                );

            leftEyeRelativeRotation =
                inverseHead *
                leftEyeRotation;

            rightEyeRelativeRotation =
                inverseHead *
                rightEyeRotation;

            stereoPoseValid = true;

            return true;
        }


        // ====================================================
        // Stereo Cameras / Projection
        // ====================================================

        private void CreateStereoCameras()
        {
            if (playerCamera == null || stereoCamerasCreated)
                return;

            leftEyeObject = new GameObject("TCGVR Left Eye");
            leftEyeObject.transform.SetParent(playerCamera.transform, false);
            leftEyeCamera = leftEyeObject.AddComponent<Camera>();
            CopyCameraSettings(playerCamera, leftEyeCamera);

            rightEyeObject = new GameObject("TCGVR Right Eye");
            rightEyeObject.transform.SetParent(playerCamera.transform, false);
            rightEyeCamera = rightEyeObject.AddComponent<Camera>();
            CopyCameraSettings(playerCamera, rightEyeCamera);

            stereoCamerasCreated = true;
            MelonLogger.Msg("Unity stereo cameras created.");
            PrintHudSummary();
        }

        private void CopyCameraSettings(Camera source, Camera destination)
        {
            destination.clearFlags = source.clearFlags;
            destination.backgroundColor = source.backgroundColor;
            destination.cullingMask = source.cullingMask;
            destination.nearClipPlane = source.nearClipPlane;
            destination.farClipPlane = source.farClipPlane;
            destination.depth = source.depth;
            destination.allowHDR = source.allowHDR;
            destination.allowMSAA = false;
            destination.useOcclusionCulling = source.useOcclusionCulling;
            destination.enabled = false;
        }

        private void UpdateStereoCameraTransforms()
        {
            if (!stereoCamerasCreated || !stereoPoseValid)
                return;

            leftEyeObject.transform.localPosition = leftEyeRelativePosition;
            leftEyeObject.transform.localRotation = leftEyeRelativeRotation;
            rightEyeObject.transform.localPosition = rightEyeRelativePosition;
            rightEyeObject.transform.localRotation = rightEyeRelativeRotation;

            leftEyeCamera.projectionMatrix = BuildProjection(
                leftAngleLeft, leftAngleRight, leftAngleUp, leftAngleDown,
                leftEyeCamera.nearClipPlane, leftEyeCamera.farClipPlane);

            rightEyeCamera.projectionMatrix = BuildProjection(
                rightAngleLeft, rightAngleRight, rightAngleUp, rightAngleDown,
                rightEyeCamera.nearClipPlane, rightEyeCamera.farClipPlane);
        }

        private Matrix4x4 BuildProjection(
            float angleLeft,
            float angleRight,
            float angleUp,
            float angleDown,
            float nearClip,
            float farClip)
        {
            return Matrix4x4.Frustum(
                Mathf.Tan(angleLeft) * nearClip,
                Mathf.Tan(angleRight) * nearClip,
                Mathf.Tan(angleDown) * nearClip,
                Mathf.Tan(angleUp) * nearClip,
                nearClip,
                farClip);
        }

        // ====================================================
        // RenderTextures
        // ====================================================

        private RenderTexture CreateEyeTexture(string name, int depth)
        {
            RenderTexture texture = new RenderTexture(
                stereoRenderWidth,
                stereoRenderHeight,
                depth,
                RenderTextureFormat.ARGB32);

            texture.name = name;
            texture.antiAliasing = 1;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.Create();
            return texture;
        }

        private void CreateStereoRenderTextures()
        {
            if (stereoRenderTexturesCreated || !stereoCamerasCreated)
                return;

            leftEyeRenderTexture = CreateEyeTexture("TCGVR Left Render", 24);
            rightEyeRenderTexture = CreateEyeTexture("TCGVR Right Render", 24);
            leftEyeSubmitTexture = CreateEyeTexture("TCGVR Left Submit FLIPPED", 0);
            rightEyeSubmitTexture = CreateEyeTexture("TCGVR Right Submit FLIPPED", 0);

            leftEyeCamera.targetTexture = leftEyeRenderTexture;
            rightEyeCamera.targetTexture = rightEyeRenderTexture;

            stereoRenderTexturesCreated = true;
            nativeTexturePointersRegistered = false;

            MelonLogger.Msg("Stereo RenderTextures created: " + stereoRenderWidth + " x " + stereoRenderHeight);
            MelonLogger.Msg("Flipped submit textures created.");
        }

        private void RegisterNativeSubmitTextures()
        {
            if (leftEyeSubmitTexture == null || rightEyeSubmitTexture == null)
                return;

            IntPtr left = leftEyeSubmitTexture.GetNativeTexturePtr();
            IntPtr right = rightEyeSubmitTexture.GetNativeTexturePtr();

            MelonLogger.Msg("Left FLIPPED Submit Texture Ptr: " + (left != IntPtr.Zero));
            MelonLogger.Msg("Right FLIPPED Submit Texture Ptr: " + (right != IntPtr.Zero));

            if (left == IntPtr.Zero || right == IntPtr.Zero)
                return;

            int result = TCGVR_SetUnityStereoTextures(
                left,
                right,
                stereoRenderWidth,
                stereoRenderHeight);

            nativeTexturePointersRegistered = result == 1;
            MelonLogger.Msg("TCGVR_SetUnityStereoTextures: " + result);
        }

        // ====================================================
        // HUD / UI fuer beide Augen
        // ====================================================

        private void PrepareHudForEye(Camera eyeCamera)
        {
            hudCanvasStates.Clear();

            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            int prepared = 0;

            foreach (Canvas canvas in canvases)
            {
                if (!IsSourceCanvasVisible(canvas))
                    continue;

                // Nested canvases inherit their root's projection.
                if (!canvas.isRootCanvas)
                    continue;

                bool shouldAttach = false;

                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    shouldAttach = true;
                }
                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    Camera wc = canvas.worldCamera;
                    shouldAttach =
                        wc == null ||
                        wc == playerCamera ||
                        wc == leftEyeCamera ||
                        wc == rightEyeCamera;
                }

                if (!shouldAttach)
                    continue;

                CanvasState state = new CanvasState();
                state.Canvas = canvas;
                state.RenderMode = canvas.renderMode;
                state.WorldCamera = canvas.worldCamera;
                state.PlaneDistance = canvas.planeDistance;
                RectTransform rect = canvas.transform as RectTransform;
                if (rect == null || playerCamera == null)
                    continue;
                Vector2 pixelSize = rect.rect.size;
                if (pixelSize.x < 1.0f || pixelSize.y < 1.0f)
                    continue;
                state.Rect = rect;
                state.AnchorMin = rect.anchorMin;
                state.AnchorMax = rect.anchorMax;
                state.Pivot = rect.pivot;
                state.SizeDelta = rect.sizeDelta;
                state.AnchoredPosition = rect.anchoredPosition;
                state.LocalPosition = rect.localPosition;
                state.LocalRotation = rect.localRotation;
                state.LocalScale = rect.localScale;
                hudCanvasStates.Add(state);

                // Both eyes see the SAME world plane, not two independent
                // screen-filling overlays. Keep original objects and UI logic.
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eyeCamera;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                // Give anchored UI a 16:9 layout area regardless of the
                // desktop monitor. Use uniform scaling to preserve text and
                // icon proportions rather than squeezing an ultrawide image.
                Vector2 menuSize = new Vector2(pixelSize.y * VrMenuAspect, pixelSize.y);
                rect.sizeDelta = menuSize;
                float scale = VrMenuWidth / menuSize.x;
                Vector3 parentScale = rect.parent != null ? rect.parent.lossyScale : Vector3.one;
                rect.localScale = new Vector3(
                    scale / Mathf.Max(0.00001f, Mathf.Abs(parentScale.x)),
                    scale / Mathf.Max(0.00001f, Mathf.Abs(parentScale.y)),
                    scale / Mathf.Max(0.00001f, Mathf.Abs(parentScale.z)));
                rect.position = playerCamera.transform.position +
                    playerCamera.transform.forward * Mathf.Max(VrMenuDistance, eyeCamera.nearClipPlane + 0.1f);
                rect.rotation = playerCamera.transform.rotation;
                prepared++;
            }

            lastHudCanvasCount = prepared;
            Canvas.ForceUpdateCanvases();
        }

        private bool IsSourceCanvasVisible(Canvas canvas)
        {
            if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                return false;
            Vector3 scale = canvas.transform.lossyScale;
            if (Mathf.Abs(scale.x) < 0.00001f || Mathf.Abs(scale.y) < 0.00001f)
                return false;
            return GetUiHierarchyAlpha(canvas.transform) > 0.001f;
        }

        private float GetUiHierarchyAlpha(Transform source)
        {
            float alpha = 1.0f;
            for (Transform current = source; current != null; current = current.parent)
            {
                bool ignoreParents = false;
                foreach (CanvasGroup group in current.GetComponents<CanvasGroup>())
                {
                    if (!group.enabled)
                        continue;
                    alpha *= group.alpha;
                    ignoreParents |= group.ignoreParentGroups;
                }
                if (ignoreParents)
                    break;
            }
            return alpha;
        }

        private void DumpUiVisibility()
        {
            int count = 0;
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || count++ >= 30)
                    continue;
                MelonLogger.Msg("UI_VISIBILITY: path=" + GetTransformPath(canvas.transform) +
                    " id=" + canvas.GetInstanceID() + " enabled=" + canvas.enabled +
                    " active=" + canvas.gameObject.activeInHierarchy +
                    " alpha=" + GetUiHierarchyAlpha(canvas.transform) +
                    " scale=" + FormatVector3(canvas.transform.lossyScale));
                for (int i = 0; i < canvas.transform.childCount && i < 16; i++)
                {
                    Transform panel = canvas.transform.GetChild(i);
                    MelonLogger.Msg("UI_PANEL: " + GetTransformPath(panel) +
                        " active=" + panel.gameObject.activeInHierarchy +
                        " alpha=" + GetUiHierarchyAlpha(panel) +
                        " scale=" + FormatVector3(panel.lossyScale));
                }
            }
        }

        private void RestoreHudCanvases()
        {
            for (int i = hudCanvasStates.Count - 1; i >= 0; i--)
            {
                CanvasState state = hudCanvasStates[i];
                if (state == null || state.Canvas == null)
                    continue;

                state.Canvas.renderMode = state.RenderMode;
                state.Canvas.worldCamera = state.WorldCamera;
                state.Canvas.planeDistance = state.PlaneDistance;
                if (state.Rect != null)
                {
                    state.Rect.anchorMin = state.AnchorMin;
                    state.Rect.anchorMax = state.AnchorMax;
                    state.Rect.pivot = state.Pivot;
                    state.Rect.sizeDelta = state.SizeDelta;
                    state.Rect.anchoredPosition = state.AnchoredPosition;
                    state.Rect.localPosition = state.LocalPosition;
                    state.Rect.localRotation = state.LocalRotation;
                    state.Rect.localScale = state.LocalScale;
                }
            }

            hudCanvasStates.Clear();
            Canvas.ForceUpdateCanvases();
        }

        private void RenderEyeWithHud(Camera eyeCamera)
        {
            try
            {
                PrepareHudForEye(eyeCamera);
                HideOriginalPlayerHudForVrEye();
                eyeCamera.Render();
            }
            finally
            {
                RestoreOriginalPlayerHudAfterVrEye();
                RestoreHudCanvases();
            }
        }

        private void PrintHudSummary()
        {
            try
            {
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                int overlay = 0;
                int camera = 0;
                int world = 0;
                int active = 0;

                foreach (Canvas c in canvases)
                {
                    if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
                        continue;

                    active++;
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay) overlay++;
                    else if (c.renderMode == RenderMode.ScreenSpaceCamera) camera++;
                    else if (c.renderMode == RenderMode.WorldSpace) world++;
                }

                MelonLogger.Msg("HUD canvas diagnostics: active=" + active +
                    " overlay=" + overlay +
                    " camera=" + camera +
                    " world=" + world);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("HUD diagnostics failed: " + ex.Message);
            }
        }


        // ====================================================
        // Separate world-space player HUD
        // ====================================================

        private Transform FindPersistentPlayerHudSource()
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];

                if (canvas == null || canvas.name != "Canvas_PLAYER HUD")
                    continue;

                Transform root = canvas.transform;

                for (int childIndex = 0; childIndex < root.childCount; childIndex++)
                {
                    Transform child = root.GetChild(childIndex);

                    if (child == null || !child.gameObject.activeInHierarchy)
                        continue;

                    if (child.name == "Panel_BDAY_CornerInfoHUD Strts OFF" ||
                        child.name == "Panel_SRV_CornerInfoHUD Strts OFF")
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private void UpdateVrPlayerHud()
        {
            if (!continuousVRRendering ||
                !stereoRenderingEnabled ||
                playerCamera == null)
            {
                SetVrPlayerHudVisible(false);
                return;
            }

            Transform source = FindPersistentPlayerHudSource();

            if (source == null)
            {
                SetVrPlayerHudVisible(false);
                return;
            }

            Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
            if ((sourceCanvas != null && !IsSourceCanvasVisible(sourceCanvas)) ||
                GetUiHierarchyAlpha(source) <= 0.001f ||
                Mathf.Abs(source.lossyScale.x) < 0.00001f ||
                Mathf.Abs(source.lossyScale.y) < 0.00001f)
            {
                SetVrPlayerHudVisible(false);
                return;
            }

            if (vrPlayerHudSource != source ||
                vrPlayerHudObject == null ||
                vrPlayerHudClone == null)
            {
                RebuildVrPlayerHud(source);
            }

            if (vrPlayerHudObject == null ||
                vrPlayerHudClone == null)
            {
                return;
            }

            SyncVrPlayerHudRecursive(source, vrPlayerHudClone);

            Transform cameraTransform = playerCamera.transform;

            Vector3 worldPosition =
                cameraTransform.position +
                cameraTransform.forward * VrPlayerHudDistance +
                cameraTransform.right * VrPlayerHudHorizontalOffset +
                cameraTransform.up * VrPlayerHudVerticalOffset;

            vrPlayerHudObject.transform.position = worldPosition;
            vrPlayerHudObject.transform.rotation = cameraTransform.rotation;
            vrPlayerHudObject.SetActive(true);
        }

        private void RebuildVrPlayerHud(Transform source)
        {
            DestroyVrPlayerHud();

            if (source == null)
                return;

            vrPlayerHudSource = source;

            vrPlayerHudObject = new GameObject("TCGVR Player HUD");
            vrPlayerHudCanvas = vrPlayerHudObject.AddComponent<Canvas>();
            vrPlayerHudCanvas.renderMode = RenderMode.WorldSpace;
            vrPlayerHudCanvas.worldCamera = playerCamera;
            vrPlayerHudCanvas.sortingOrder = 4900;

            RectTransform canvasRect =
                vrPlayerHudObject.GetComponent<RectTransform>();

            canvasRect.sizeDelta = new Vector2(420.0f, 320.0f);
            canvasRect.localScale =
                new Vector3(
                    VrPlayerHudWorldScale,
                    VrPlayerHudWorldScale,
                    VrPlayerHudWorldScale);

            GameObject cloneObject =
                UnityEngine.Object.Instantiate(
                    source.gameObject,
                    vrPlayerHudObject.transform);

            cloneObject.name = source.name + " VR";
            vrPlayerHudClone = cloneObject.transform;

            RectTransform cloneRect =
                vrPlayerHudClone as RectTransform;

            if (cloneRect != null)
            {
                cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
                cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
                cloneRect.pivot = new Vector2(0.5f, 0.5f);
                cloneRect.anchoredPosition = Vector2.zero;
                cloneRect.localScale = Vector3.one;
                cloneRect.localRotation = Quaternion.identity;
            }

            DisableGameplayBehavioursOnHudClone(vrPlayerHudClone);
            vrPlayerHudObject.SetActive(true);

            MelonLogger.Msg(
                "0.0.89 separate VR player HUD created from: " +
                source.name +
                " distance=" + VrPlayerHudDistance +
                "m scale=" + VrPlayerHudWorldScale);
        }

        private void DisableGameplayBehavioursOnHudClone(Transform root)
        {
            if (root == null)
                return;

            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];

                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                string fullName = type.FullName ?? type.Name;

                // Keep only Unity UI and TextMeshPro rendering behaviours.
                // Gameplay FSMs, localization scripts and other copied logic
                // must not execute a second time on the VR display clone.
                if (fullName.StartsWith("UnityEngine.UI.", StringComparison.Ordinal) ||
                    fullName.StartsWith("TMPro.", StringComparison.Ordinal))
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        private void SyncVrPlayerHudRecursive(
            Transform source,
            Transform clone)
        {
            if (source == null || clone == null)
                return;

            if (clone.gameObject.activeSelf != source.gameObject.activeSelf)
                clone.gameObject.SetActive(source.gameObject.activeSelf);

            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] cloneComponents = clone.GetComponents<Component>();

            int componentCount =
                Mathf.Min(sourceComponents.Length, cloneComponents.Length);

            for (int i = 0; i < componentCount; i++)
            {
                Component sourceComponent = sourceComponents[i];
                Component cloneComponent = cloneComponents[i];

                if (sourceComponent == null || cloneComponent == null)
                    continue;

                if (sourceComponent.GetType() != cloneComponent.GetType())
                    continue;

                SyncHudComponentProperties(
                    sourceComponent,
                    cloneComponent);
            }

            int childCount =
                Mathf.Min(source.childCount, clone.childCount);

            for (int i = 0; i < childCount; i++)
            {
                SyncVrPlayerHudRecursive(
                    source.GetChild(i),
                    clone.GetChild(i));
            }
        }

        private void SyncHudComponentProperties(
            Component source,
            Component clone)
        {
            Type type = source.GetType();
            string fullName = type.FullName ?? type.Name;

            CanvasGroup sourceGroup = source as CanvasGroup;
            CanvasGroup cloneGroup = clone as CanvasGroup;
            if (sourceGroup != null && cloneGroup != null)
            {
                cloneGroup.alpha = sourceGroup.alpha;
                cloneGroup.ignoreParentGroups = sourceGroup.ignoreParentGroups;
                return;
            }

            // Only copy visual UI values. This keeps the clone passive.
            if (!(fullName.StartsWith("UnityEngine.UI.", StringComparison.Ordinal) ||
                  fullName.StartsWith("TMPro.", StringComparison.Ordinal)))
            {
                return;
            }

            CopyHudProperty(type, source, clone, "text");
            CopyHudProperty(type, source, clone, "spriteAsset");
            CopyHudProperty(type, source, clone, "sprite");
            CopyHudProperty(type, source, clone, "overrideSprite");
            CopyHudProperty(type, source, clone, "fillAmount");
            CopyHudProperty(type, source, clone, "color");
            CopyHudProperty(type, source, clone, "enabled");
        }

        private void CopyHudProperty(
            Type type,
            Component source,
            Component clone,
            string propertyName)
        {
            try
            {
                PropertyInfo property =
                    type.GetProperty(
                        propertyName,
                        BindingFlags.Instance |
                        BindingFlags.Public);

                if (property == null ||
                    !property.CanRead ||
                    !property.CanWrite)
                {
                    return;
                }

                object value = property.GetValue(source, null);
                property.SetValue(clone, value, null);
            }
            catch
            {
                // A visual property that cannot be copied is non-fatal.
            }
        }

        private void HideOriginalPlayerHudForVrEye()
        {
            vrPlayerHudSourceWasActive = false;

            if (vrPlayerHudSource == null ||
                vrPlayerHudSource.gameObject == null)
            {
                return;
            }

            vrPlayerHudSourceWasActive =
                vrPlayerHudSource.gameObject.activeSelf;

            if (vrPlayerHudSourceWasActive)
                vrPlayerHudSource.gameObject.SetActive(false);
        }

        private void RestoreOriginalPlayerHudAfterVrEye()
        {
            if (vrPlayerHudSource == null ||
                vrPlayerHudSource.gameObject == null)
            {
                vrPlayerHudSourceWasActive = false;
                return;
            }

            if (vrPlayerHudSourceWasActive)
                vrPlayerHudSource.gameObject.SetActive(true);

            vrPlayerHudSourceWasActive = false;
        }

        private void SetVrPlayerHudVisible(bool visible)
        {
            if (vrPlayerHudObject != null)
                vrPlayerHudObject.SetActive(visible);
        }

        private void DestroyVrPlayerHud()
        {
            RestoreOriginalPlayerHudAfterVrEye();

            if (vrPlayerHudObject != null)
                UnityEngine.Object.Destroy(vrPlayerHudObject);

            vrPlayerHudObject = null;
            vrPlayerHudCanvas = null;
            vrPlayerHudClone = null;
            vrPlayerHudSource = null;
            vrPlayerHudSourceWasActive = false;
        }


        // ====================================================
        // HUD hierarchy diagnostic
        // ====================================================

        private void DumpHudHierarchy()
        {
            try
            {
                MelonLogger.Msg("====================================");
                MelonLogger.Msg("0.0.89 TARGETED INTERACTION HUD SCAN");
                MelonLogger.Msg("F11: scan Canvas_PLAYER HUD / Panel_Cursor Tips Always ON.");
                MelonLogger.Msg("Run once with no interactable targeted and once while the interaction prompt is visible.");

                Transform target = FindInteractionHudRoot();

                if (target == null)
                {
                    MelonLogger.Warning("Target HUD root was not found.");
                    MelonLogger.Msg("====================================");
                    return;
                }

                MelonLogger.Msg("TARGET ROOT: " + GetTransformPath(target));

                int objectCount = 0;
                DumpInteractionHudRecursive(target, 0, ref objectCount);

                MelonLogger.Msg("Targeted HUD scan complete. Objects logged=" + objectCount);
                MelonLogger.Msg("====================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Targeted HUD scan failed: " + ex.Message);
            }
        }

        private Transform FindInteractionHudRoot()
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                    continue;

                string path = GetTransformPath(canvas.transform);

                if (canvas.name == "Canvas_PLAYER HUD" ||
                    path.EndsWith("/Canvas_PLAYER HUD"))
                {
                    Transform[] all = canvas.GetComponentsInChildren<Transform>(true);

                    foreach (Transform child in all)
                    {
                        if (child != null &&
                            child.name == "Panel_Cursor Tips Always ON")
                        {
                            return child;
                        }
                    }
                }
            }

            // Fallback only to live scene UI, never an inactive/prefab copy.
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

            foreach (Transform transform in transforms)
            {
                if (transform == null)
                    continue;

                if (transform.name == "Panel_Cursor Tips Always ON" &&
                    transform.parent != null && transform.parent.gameObject.activeInHierarchy)
                    return transform;
            }

            return null;
        }

        private void DumpInteractionHudRecursive(
            Transform transform,
            int depth,
            ref int objectCount)
        {
            if (transform == null || transform.gameObject == null)
                return;

            GameObject go = transform.gameObject;
            RectTransform rect = transform as RectTransform;
            Component[] components = go.GetComponents<Component>();

            string componentNames = "";
            string values = "";

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                if (component == null)
                    continue;

                Type type = component.GetType();

                if (componentNames.Length > 0)
                    componentNames += ",";

                componentNames += type.Name;

                string typeName = type.Name;

                if (typeName == "Image" ||
                    typeName == "RawImage" ||
                    typeName == "Text" ||
                    typeName == "TextMeshProUGUI" ||
                    typeName == "TMP_Text")
                {
                    values += ReadUiComponentValue(component);
                }

                if (typeName == "PlayMakerFSM")
                    values += ReadPlayMakerFsmIdentity(component);
            }

            string rectInfo = "";

            if (rect != null)
            {
                rectInfo =
                    " anchored=" + FormatVector2(rect.anchoredPosition) +
                    " size=" + FormatVector2(rect.rect.size) +
                    " scale=" + FormatVector3(rect.localScale);
            }

            MelonLogger.Msg(
                "INTERACTION_UI: depth=" + depth +
                " path=" + GetTransformPath(transform) +
                " activeSelf=" + go.activeSelf +
                " activeHierarchy=" + go.activeInHierarchy +
                rectInfo +
                " components=[" + componentNames + "]" +
                values);

            objectCount++;

            // Deliberately include inactive children.
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                if (child != null)
                    DumpInteractionHudRecursive(child, depth + 1, ref objectCount);
            }
        }

        private string ReadPlayMakerFsmIdentity(Component component)
        {
            try
            {
                Type type = component.GetType();
                string result = "";

                PropertyInfo fsmNameProperty = type.GetProperty(
                    "FsmName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (fsmNameProperty != null && fsmNameProperty.CanRead)
                {
                    object value = fsmNameProperty.GetValue(component, null);

                    if (value != null)
                        result += " fsm=\"" + value.ToString() + "\"";
                }

                PropertyInfo activeStateNameProperty = type.GetProperty(
                    "ActiveStateName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (activeStateNameProperty != null && activeStateNameProperty.CanRead)
                {
                    object value = activeStateNameProperty.GetValue(component, null);

                    if (value != null)
                        result += " state=\"" + value.ToString() + "\"";
                }

                return result;
            }
            catch
            {
                return "";
            }
        }

        private string ReadUiComponentValue(Component component)
        {
            try
            {
                Type type = component.GetType();
                string result = "";

                PropertyInfo textProperty = type.GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public);

                if (textProperty != null && textProperty.CanRead)
                {
                    object value = textProperty.GetValue(component, null);

                    if (value != null)
                    {
                        string textValue = value.ToString();

                        if (textValue.Length > 120)
                            textValue = textValue.Substring(0, 120);

                        textValue = textValue.Replace("\r", " ").Replace("\n", " ");
                        result += " text=\"" + textValue + "\"";
                    }
                }

                PropertyInfo spriteProperty = type.GetProperty(
                    "sprite",
                    BindingFlags.Instance | BindingFlags.Public);

                if (spriteProperty != null && spriteProperty.CanRead)
                {
                    object spriteValue = spriteProperty.GetValue(component, null);
                    UnityEngine.Object spriteObject = spriteValue as UnityEngine.Object;

                    if (spriteObject != null)
                        result += " sprite=\"" + spriteObject.name + "\"";
                }

                PropertyInfo textureProperty = type.GetProperty(
                    "texture",
                    BindingFlags.Instance | BindingFlags.Public);

                if (textureProperty != null && textureProperty.CanRead)
                {
                    object textureValue = textureProperty.GetValue(component, null);
                    UnityEngine.Object textureObject = textureValue as UnityEngine.Object;

                    if (textureObject != null)
                        result += " texture=\"" + textureObject.name + "\"";
                }

                return result;
            }
            catch
            {
                return "";
            }
        }

        private string FormatVector2(Vector2 value)
        {
            return "(" +
                FormatFloat(value.x) + "," +
                FormatFloat(value.y) + ")";
        }

        // ====================================================
        // 0.0.71 world-space interaction HUD
        // ====================================================

        private void UpdateWorldSpaceInteractionHud()
        {
            Transform root = FindInteractionHudRoot();

            if (root == null)
            {
                SetWorldSpaceInteractionHudVisible(false);
                return;
            }

            if (vrEnabledPromptRoot != root)
            {
                RestoreVrPromptRootActivation();
                vrEnabledPromptRoot = root;
                vrPromptRootOriginallyActive = root.gameObject.activeSelf;
                vrPromptActivationLogged = false;
            }
            // The game still selects its original child hint (Grab/Interact),
            // but an inactive container also prevents the glyph helper from
            // resolving its input tokens. Enable only that live UI container.
            if (vrMasterWanted && !useMenuCamera && root.parent != null &&
                root.parent.gameObject.activeInHierarchy && !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
                if (!vrPromptActivationLogged)
                {
                    MelonLogger.Msg("VR action-hint container reactivated: " + GetTransformPath(root));
                    vrPromptActivationLogged = true;
                }
            }

            if (interactionHudRoot != root)
            {
                RestoreOriginalInteractionHud();
                DestroyWorldSpaceInteractionHud();
                interactionHudRoot = root;
                interactionHudHasSmoothedPosition = false;

                MelonLogger.Msg(
                    "0.0.89 interaction HUD source rebound: " +
                    GetTransformPath(root));
            }
            else
            {
                interactionHudRoot = root;
            }

            Transform activePrompt = FindActiveInteractionPrompt(root);

            if (activePrompt == null)
            {
                RestoreOriginalInteractionHud();
                SetWorldSpaceInteractionHudVisible(false);
                interactionHudHasSmoothedPosition = false;
                return;
            }

            RaycastHit hit;
            if (!TryGetInteractionSurfaceHit(out hit))
            {
                RestoreOriginalInteractionHud();
                SetWorldSpaceInteractionHudVisible(false);
                return;
            }

            EnsureWorldSpaceInteractionHud(activePrompt);

            if (vrInteractionHudObject == null || vrInteractionHudClone == null)
                return;

            string expectedCloneName = activePrompt.name + " VR";

            if (vrInteractionHudClone.name != expectedCloneName)
            {
                DestroyWorldSpaceInteractionHud();
                EnsureWorldSpaceInteractionHud(activePrompt);
            }

            if (vrInteractionHudObject == null)
                return;

            // Device changes can keep the same Grab/Interact panel selected.
            // Refresh its live text and glyph asset instead of leaving the
            // clone with the input device's original button hints.
            SyncVrPlayerHudRecursive(activePrompt, vrInteractionHudClone.transform);

            // Hide only the original prompt panel visually. The game can keep
            // controlling its child active states normally.
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
                group = root.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0.0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Vector3 hitPoint = hit.point;
            Vector3 towardHead = playerCamera.transform.position - hitPoint;

            if (towardHead.sqrMagnitude > 0.000001f)
                towardHead.Normalize();
            else
                towardHead = hit.normal.normalized;

            // Keep the interaction marker close to its real hit position,
            // but pull the complete panel toward the HMD so cabinet geometry
            // cannot easily occlude the text or icon.
            Vector3 targetPosition =
                hitPoint + towardHead * 0.080f;

            if (!interactionHudHasSmoothedPosition)
            {
                interactionHudSmoothedPosition = targetPosition;
                interactionHudHasSmoothedPosition = true;
            }
            else
            {
                float t = 1.0f - Mathf.Exp(
                    -InteractionHudPositionSharpness * Time.unscaledDeltaTime);

                interactionHudSmoothedPosition = Vector3.Lerp(
                    interactionHudSmoothedPosition,
                    targetPosition,
                    t);
            }

            vrInteractionHudObject.transform.position =
                interactionHudSmoothedPosition;

            Vector3 toHead =
                playerCamera.transform.position -
                vrInteractionHudObject.transform.position;

            if (toHead.sqrMagnitude > 0.000001f)
            {
                // Face the HMD but keep text upright relative to world up.
                vrInteractionHudObject.transform.rotation =
                    Quaternion.LookRotation(-toHead.normalized, Vector3.up);
            }

            vrInteractionHudObject.SetActive(true);
        }

        private Transform FindActiveInteractionPrompt(Transform root)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (child == null || child.gameObject == null)
                    continue;

                if (!child.name.EndsWith("_glyphSupported"))
                    continue;

                if (child.gameObject.activeSelf && child.gameObject.activeInHierarchy)
                    return child;
            }

            return null;
        }

        private bool TryGetInteractionSurfaceHit(out RaycastHit selectedHit)
        {
            selectedHit = new RaycastHit();

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward.normalized;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                InteractionHudMaxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(
                hits,
                delegate (RaycastHit a, RaycastHit b)
                {
                    return a.distance.CompareTo(b.distance);
                });

            // First choice: nearest solid world surface.
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsUsableInteractionSurface(hits[i], false))
                {
                    selectedHit = hits[i];
                    return true;
                }
            }

            // Fallback for interactions implemented only with triggers.
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsUsableInteractionSurface(hits[i], true))
                {
                    selectedHit = hits[i];
                    return true;
                }
            }

            return false;
        }

        private bool IsUsableInteractionSurface(RaycastHit hit, bool allowTrigger)
        {
            Collider collider = hit.collider;

            if (collider == null || collider.gameObject == null)
                return false;

            GameObject go = collider.gameObject;
            string path = GetTransformPath(go.transform);

            if (!allowTrigger && collider.isTrigger)
                return false;

            // Ignore the player's own camera helpers and inventory spawn trigger.
            if (path.StartsWith("PLAYER/") ||
                path.Contains("/PLAYER/") ||
                path.Contains("SPAWNSPOT_Inventory") ||
                path.Contains("TCGVR Interaction HUD"))
            {
                return false;
            }

            // Prefer visible geometry over the game's interference helper.
            if (!allowTrigger &&
                (go.name.IndexOf("INTERFERENCE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 LayerMask.LayerToName(go.layer) == "INTERFERENCE"))
            {
                return false;
            }

            return true;
        }

        private void EnsureWorldSpaceInteractionHud(Transform activePrompt)
        {
            if (activePrompt == null)
                return;

            if (vrInteractionHudObject != null && vrInteractionHudClone != null)
                return;

            vrInteractionHudObject = new GameObject("TCGVR Interaction HUD");

            vrInteractionHudCanvas =
                vrInteractionHudObject.AddComponent<Canvas>();

            vrInteractionHudCanvas.renderMode = RenderMode.WorldSpace;
            vrInteractionHudCanvas.worldCamera = playerCamera;
            vrInteractionHudCanvas.sortingOrder = 5000;

            RectTransform canvasRect =
                vrInteractionHudObject.GetComponent<RectTransform>();

            canvasRect.sizeDelta = new Vector2(300.0f, 300.0f);
            canvasRect.localScale =
                Vector3.one * InteractionHudWorldScale;

            vrInteractionHudClone =
                UnityEngine.Object.Instantiate(activePrompt.gameObject);

            vrInteractionHudClone.name = activePrompt.name + " VR";
            DisableGameplayBehavioursOnHudClone(vrInteractionHudClone.transform);
            vrInteractionHudClone.transform.SetParent(
                vrInteractionHudObject.transform,
                false);

            RectTransform cloneRect =
                vrInteractionHudClone.GetComponent<RectTransform>();

            if (cloneRect != null)
            {
                cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
                cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
                cloneRect.pivot = new Vector2(0.5f, 0.5f);
                cloneRect.anchoredPosition = Vector2.zero;
                cloneRect.localRotation = Quaternion.identity;
                cloneRect.localScale = Vector3.one;
                cloneRect.sizeDelta = new Vector2(300.0f, 300.0f);
            }

            vrInteractionHudClone.SetActive(true);
            vrInteractionHudObject.SetActive(false);

            MelonLogger.Msg(
                "0.0.89 world-space interaction HUD created for: " +
                activePrompt.name);
        }

        private void RestoreOriginalInteractionHud()
        {
            Transform root = interactionHudRoot;

            if (root == null)
                root = FindInteractionHudRoot();

            if (root == null)
                return;

            CanvasGroup group = root.GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha = 1.0f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
        }

        private void RestoreVrPromptRootActivation()
        {
            if (vrEnabledPromptRoot != null)
                vrEnabledPromptRoot.gameObject.SetActive(vrPromptRootOriginallyActive);
            vrEnabledPromptRoot = null;
        }

        private void SetWorldSpaceInteractionHudVisible(bool visible)
        {
            if (vrInteractionHudObject != null)
                vrInteractionHudObject.SetActive(visible);
        }

        private void DestroyWorldSpaceInteractionHud()
        {
            if (vrInteractionHudObject != null)
                UnityEngine.Object.Destroy(vrInteractionHudObject);

            vrInteractionHudObject = null;
            vrInteractionHudCanvas = null;
            vrInteractionHudClone = null;
            interactionHudHasSmoothedPosition = false;
        }

        // ====================================================
        // Interaction ray diagnostic
        // ====================================================

        private void DumpInteractionRaycast()
        {
            try
            {
                MelonLogger.Msg("====================================");
                MelonLogger.Msg("0.0.89 HMD INTERACTION RAY DIAGNOSTIC");

                Camera sourceCamera = playerCamera;

                if (sourceCamera == null)
                {
                    MelonLogger.Warning("Interaction ray: Main Player-Camera is not available.");
                    MelonLogger.Msg("====================================");
                    return;
                }

                Vector3 origin = sourceCamera.transform.position;
                Vector3 direction = sourceCamera.transform.forward.normalized;
                const float maxDistance = 10.0f;

                MelonLogger.Msg(
                    "Ray origin=" + FormatVector3(origin) +
                    " direction=" + FormatVector3(direction) +
                    " maxDistance=" + FormatFloat(maxDistance));

                RaycastHit[] hits = Physics.RaycastAll(
                    origin,
                    direction,
                    maxDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);

                if (hits == null || hits.Length == 0)
                {
                    MelonLogger.Msg("Interaction ray: no physics hits.");
                    MelonLogger.Msg("====================================");
                    return;
                }

                Array.Sort(
                    hits,
                    delegate (RaycastHit a, RaycastHit b)
                    {
                        return a.distance.CompareTo(b.distance);
                    });

                MelonLogger.Msg("Interaction ray hits: " + hits.Length);

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    Collider collider = hit.collider;
                    GameObject hitObject = collider != null ? collider.gameObject : null;

                    string objectName = hitObject != null ? hitObject.name : "<null>";
                    string objectPath =
                        hitObject != null ? GetTransformPath(hitObject.transform) : "<null>";
                    string colliderType =
                        collider != null ? collider.GetType().FullName : "<null>";
                    int layer = hitObject != null ? hitObject.layer : -1;
                    string layerName =
                        layer >= 0 ? LayerMask.LayerToName(layer) : "<none>";

                    MelonLogger.Msg(
                        "RAY_HIT[" + i + "]" +
                        " distance=" + FormatFloat(hit.distance) +
                        " point=" + FormatVector3(hit.point) +
                        " normal=" + FormatVector3(hit.normal) +
                        " object=\"" + objectName + "\"" +
                        " path=\"" + objectPath + "\"" +
                        " collider=" + colliderType +
                        " layer=" + layer +
                        " layerName=\"" + layerName + "\"" +
                        " trigger=" + (collider != null && collider.isTrigger));

                    if (hitObject != null)
                    {
                        Component[] components = hitObject.GetComponents<Component>();
                        string componentNames = "";

                        for (int c = 0; c < components.Length; c++)
                        {
                            Component component = components[c];

                            if (component == null)
                                continue;

                            if (componentNames.Length > 0)
                                componentNames += ",";

                            componentNames += component.GetType().FullName;
                        }

                        MelonLogger.Msg(
                            "RAY_HIT[" + i + "] components=[" + componentNames + "]");

                        Transform parent = hitObject.transform.parent;

                        for (int p = 0; p < 3 && parent != null; p++)
                        {
                            MelonLogger.Msg(
                                "RAY_HIT[" + i + "] parent" + (p + 1) +
                                "=\"" + GetTransformPath(parent) + "\"");
                            parent = parent.parent;
                        }
                    }
                }

                MelonLogger.Msg("====================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Interaction ray diagnostic failed: " + ex.Message);
            }
        }

        // ====================================================
        // 0.0.71 focused player HUD diagnostic
        // ====================================================

        private void DumpPlayerHudHierarchy()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("0.0.89 PLAYER HUD / CROSSHAIR SCAN");

            Canvas playerHudCanvas = null;
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];

                if (canvas == null || canvas.gameObject == null)
                    continue;

                if (canvas.name == "Canvas_PLAYER HUD")
                {
                    playerHudCanvas = canvas;
                    break;
                }
            }

            if (playerHudCanvas == null)
            {
                MelonLogger.Warning("Canvas_PLAYER HUD was not found.");
                MelonLogger.Msg("====================================");
                return;
            }

            RectTransform canvasRect =
                playerHudCanvas.GetComponent<RectTransform>();

            MelonLogger.Msg(
                "PLAYER_HUD_ROOT: path=" +
                GetTransformPath(playerHudCanvas.transform) +
                " renderMode=" + playerHudCanvas.renderMode +
                " sortingOrder=" + playerHudCanvas.sortingOrder +
                " planeDistance=" + playerHudCanvas.planeDistance +
                " worldCamera=" +
                (playerHudCanvas.worldCamera != null
                    ? playerHudCanvas.worldCamera.name
                    : "<null>") +
                " rect=" +
                (canvasRect != null
                    ? FormatVector2(canvasRect.rect.size)
                    : "<none>"));

            int logged = 0;
            DumpPlayerHudRecursive(
                playerHudCanvas.transform,
                playerHudCanvas.transform,
                0,
                ref logged);

            MelonLogger.Msg(
                "Player HUD scan complete. Objects logged=" + logged);
            MelonLogger.Msg("====================================");
        }

        private void DumpPlayerHudRecursive(
            Transform root,
            Transform current,
            int depth,
            ref int logged)
        {
            if (current == null)
                return;

            // The interaction panel is already diagnosed separately.
            if (current != root &&
                current.name == "Panel_Cursor Tips Always ON")
            {
                return;
            }

            RectTransform rect = current as RectTransform;
            Component[] components = current.GetComponents<Component>();

            string componentNames = "";
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    continue;

                if (componentNames.Length > 0)
                    componentNames += ",";

                componentNames += components[i].GetType().Name;
            }

            string uiValue = "";
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    continue;

                string value = ReadUiComponentValue(components[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    if (uiValue.Length > 0)
                        uiValue += " ";

                    uiValue += value;
                }
            }

            string rectInfo = "";
            if (rect != null)
            {
                rectInfo =
                    " anchored=" + FormatVector2(rect.anchoredPosition) +
                    " size=" + FormatVector2(rect.rect.size) +
                    " scale=" + FormatVector3(rect.localScale);
            }

            MelonLogger.Msg(
                "PLAYER_HUD: depth=" + depth +
                " path=" + GetTransformPath(current) +
                " activeSelf=" + current.gameObject.activeSelf +
                " activeHierarchy=" + current.gameObject.activeInHierarchy +
                rectInfo +
                " components=[" + componentNames + "]" +
                (string.IsNullOrEmpty(uiValue) ? "" : " " + uiValue));

            logged++;

            for (int i = 0; i < current.childCount; i++)
            {
                DumpPlayerHudRecursive(
                    root,
                    current.GetChild(i),
                    depth + 1,
                    ref logged);
            }
        }

        // ====================================================
        // 0.0.89 visible center UI / reticle diagnostic
        // Uses reflection so no additional Unity UI assembly reference is required.
        // ====================================================

        private void DumpVisibleCenterUi()
        {
            MelonLogger.Msg("====================================");
            MelonLogger.Msg("0.0.89 VISIBLE CENTER UI / RETICLE SCAN");
            MelonLogger.Msg("Press F11 once for each state: normal reticle, hand/grab, card/use, etc.");

            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            int logged = 0;

            for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
            {
                Canvas canvas = canvases[canvasIndex];

                if (canvas == null ||
                    canvas.gameObject == null ||
                    !canvas.gameObject.activeInHierarchy)
                    continue;

                Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);

                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform current = transforms[transformIndex];

                    if (current == null ||
                        current.gameObject == null ||
                        !current.gameObject.activeInHierarchy)
                        continue;

                    RectTransform rect = current as RectTransform;
                    if (rect == null)
                        continue;

                    Component[] components = current.GetComponents<Component>();
                    if (components == null || components.Length == 0)
                        continue;

                    bool hasVisibleUiComponent = false;
                    string visualInfo = "";

                    for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null)
                            continue;

                        Type type = component.GetType();
                        string typeName = type.Name;

                        if (typeName == "Image" ||
                            typeName == "RawImage" ||
                            typeName == "Text" ||
                            typeName == "TextMeshProUGUI" ||
                            typeName == "TMP_SubMeshUI")
                        {
                            Behaviour behaviour = component as Behaviour;
                            if (behaviour != null && !behaviour.enabled)
                                continue;

                            hasVisibleUiComponent = true;
                            visualInfo += DescribeUiVisualComponent(component);
                        }
                    }

                    if (!hasVisibleUiComponent)
                        continue;

                    Vector3 screenPoint;

                    try
                    {
                        Camera eventCamera = null;

                        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                            eventCamera = canvas.worldCamera;

                        screenPoint =
                            RectTransformUtility.WorldToScreenPoint(
                                eventCamera,
                                rect.position);
                    }
                    catch
                    {
                        continue;
                    }

                    float centerX = Screen.width * 0.5f;
                    float centerY = Screen.height * 0.5f;
                    float dx = screenPoint.x - centerX;
                    float dy = screenPoint.y - centerY;

                    if (Mathf.Abs(dx) > Screen.width * 0.22f ||
                        Mathf.Abs(dy) > Screen.height * 0.30f)
                        continue;

                    MelonLogger.Msg(
                        "CENTER_UI: canvas=" + canvas.name +
                        " mode=" + canvas.renderMode +
                        " path=" + GetTransformPath(current) +
                        " screen=" + FormatVector2(new Vector2(screenPoint.x, screenPoint.y)) +
                        " deltaFromCenter=" + FormatVector2(new Vector2(dx, dy)) +
                        " anchored=" + FormatVector2(rect.anchoredPosition) +
                        " size=" + FormatVector2(rect.rect.size) +
                        " scale=" + FormatVector3(rect.localScale) +
                        " components=[" + GetComponentTypeList(current.gameObject) + "]" +
                        visualInfo);

                    logged++;
                }
            }

            MelonLogger.Msg("Visible center UI scan complete. Objects logged=" + logged);
            MelonLogger.Msg("====================================");
        }

        private string DescribeUiVisualComponent(Component component)
        {
            if (component == null)
                return "";

            try
            {
                Type type = component.GetType();
                string typeName = type.Name;
                string result = " " + typeName;

                PropertyInfo spriteProperty = type.GetProperty(
                    "sprite",
                    BindingFlags.Instance | BindingFlags.Public);

                if (spriteProperty != null)
                {
                    UnityEngine.Object sprite =
                        spriteProperty.GetValue(component, null) as UnityEngine.Object;

                    result += ".sprite=\"" +
                              (sprite != null ? sprite.name : "<null>") +
                              "\"";
                }

                PropertyInfo textureProperty = type.GetProperty(
                    "texture",
                    BindingFlags.Instance | BindingFlags.Public);

                if (textureProperty != null)
                {
                    UnityEngine.Object texture =
                        textureProperty.GetValue(component, null) as UnityEngine.Object;

                    result += ".texture=\"" +
                              (texture != null ? texture.name : "<null>") +
                              "\"";
                }

                PropertyInfo textProperty = type.GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public);

                if (textProperty != null &&
                    textProperty.PropertyType == typeof(string))
                {
                    string value = textProperty.GetValue(component, null) as string;

                    if (!string.IsNullOrEmpty(value))
                    {
                        value = value.Replace("\r", " ").Replace("\n", " ");

                        if (value.Length > 160)
                            value = value.Substring(0, 160);

                        result += ".text=\"" + value + "\"";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return " visualReadError=\"" + ex.Message + "\"";
            }
        }

        private string GetComponentTypeList(GameObject gameObject)
        {
            if (gameObject == null)
                return "";

            try
            {
                Component[] components = gameObject.GetComponents<Component>();
                string result = "";

                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                        continue;

                    if (result.Length > 0)
                        result += ",";

                    result += components[i].GetType().Name;
                }

                return result;
            }
            catch
            {
                return "<error>";
            }
        }

        // ====================================================
        // 0.0.89 world-space VR cursor
        // Uses a SpriteRenderer instead of a WorldSpace Canvas.
        // ====================================================

        private Transform vrPcCursorTransform;
        private Vector3 vrPcCursorOriginalScale = Vector3.one;
        private bool vrPcCursorOriginalScaleStored;

        private GameObject vrWorldCursorObject;
        private SpriteRenderer vrWorldCursorRenderer;
        private Sprite vrWorldCursorLastSprite;

        private string vrLastCursorStateName = "";
        private float vrLastCursorStateDumpTime = -1000.0f;

        private float vrNextFramePacingLogTime = -1000.0f;
        private float vrFramePacingWindowStart = -1000.0f;
        private int vrFramePacingFrames = 0;
        private float vrFramePacingWorstDeltaMs = 0.0f;
        private int vrFramePacingOver20Ms = 0;
        private int vrFramePacingOver30Ms = 0;
        private int vrFramePacingOver50Ms = 0;
        private float vrLastFramePacingTime = -1.0f;
        private int vrSubmitDiagLastUnityFrame = -1;
        private int vrSubmitDiagDuplicateUnityFrames = 0;
        private int vrSubmitDiagRenderAdvances = 0;
        private float vrSubmitDiagNextLogTime = -1000.0f;
        private Matrix4x4 vrSubmitDiagLastLeftProjection;
        private Matrix4x4 vrSubmitDiagLastRightProjection;
        private bool vrSubmitDiagHaveProjection = false;
        private float vrSubmitDiagMaxProjectionDelta = 0.0f;

        private float vrGrabProbeUntil = -1000.0f;
        private float vrNextGrabProbeTime = -1000.0f;
        private string vrGrabProbeLastSprite = "";
        private float vrDetailedDiagnosticUntil = -1000.0f;
        private readonly HashSet<string> vrGrabDiagnosticStates = new HashSet<string>();


        private const float VrWorldCursorPhysicalSize = 0.071875f;
        private const float VrWorldCursorSurfaceOffset = 0.020f;
        private const float VrWorldCursorMaxDistance = 10.0f;

        private void UpdateVrPcCursorScale()
        {
            UpdateWorldSpaceVrCursor();
            UpdateFramePacingDiagnostic();
            UpdateGrabHoldProbe();
        }

        private void UpdateWorldSpaceVrCursor()
        {
            if (!continuousVRRendering || playerCamera == null)
            {
                RestoreVrPcCursorScale();
                SetWorldSpaceVrCursorVisible(false);
                return;
            }

            if (vrPcCursorTransform == null)
            {
                vrPcCursorTransform = FindTransformByPath(
                    "MENUS_INGAME/PCCursorCanvas/Cursor PC 256x256");

                if (vrPcCursorTransform == null)
                {
                    SetWorldSpaceVrCursorVisible(false);
                    return;
                }

                vrPcCursorOriginalScale = vrPcCursorTransform.localScale;
                vrPcCursorOriginalScaleStored = true;

                MelonLogger.Msg(
                    "0.0.89 PC cursor source found. Original scale=" +
                    FormatVector3(vrPcCursorOriginalScale));
            }

            // A menu uses the game's screen pointer/selection, not a ray into
            // the 3D scene. Its original cursor shares the projected UI plane.
            if (useMenuCamera || (Cursor.visible && Cursor.lockState != CursorLockMode.Locked))
            {
                if (vrPcCursorOriginalScaleStored)
                    vrPcCursorTransform.localScale = vrPcCursorOriginalScale;
                SetWorldSpaceVrCursorVisible(false);
                return;
            }

            EnsureWorldSpaceVrCursor();

            if (vrWorldCursorObject == null ||
                vrWorldCursorRenderer == null)
                return;

            Sprite currentSprite;
            Color currentColor;

            if (!TryReadPcCursorVisual(out currentSprite, out currentColor) ||
                currentSprite == null)
            {
                SetWorldSpaceVrCursorVisible(false);
                return;
            }

            if (vrWorldCursorRenderer.sprite != currentSprite)
            {
                vrGrabProbeLastSprite = currentSprite.name;
                vrWorldCursorRenderer.sprite = currentSprite;
                vrWorldCursorLastSprite = currentSprite;
                UpdateWorldCursorPhysicalScale(currentSprite);

                MelonLogger.Msg(
                    "0.0.89 VR cursor sprite: " +
                    currentSprite.name);

                DumpAutomaticCursorStateTransition(currentSprite.name);
            }

            vrWorldCursorRenderer.color = currentColor;

            // Keep the original object alive so the game can continue to
            // change its cursor sprite, but hide its 2D visual in VR.
            if (vrPcCursorOriginalScaleStored)
                vrPcCursorTransform.localScale = Vector3.zero;

            RaycastHit hit;
            if (!TryGetVrCursorSurfaceHit(out hit))
            {
                SetWorldSpaceVrCursorVisible(false);
                return;
            }

            Vector3 towardHead =
                playerCamera.transform.position - hit.point;

            if (towardHead.sqrMagnitude > 0.000001f)
                towardHead.Normalize();
            else
                towardHead = hit.normal.normalized;

            vrWorldCursorObject.transform.position =
                hit.point + towardHead * VrWorldCursorSurfaceOffset;

            Vector3 toHead =
                playerCamera.transform.position -
                vrWorldCursorObject.transform.position;

            if (toHead.sqrMagnitude > 0.000001f)
            {
                // SpriteRenderer faces the camera directly.
                vrWorldCursorObject.transform.rotation =
                    Quaternion.LookRotation(
                        toHead.normalized,
                        playerCamera.transform.up);
            }

            SetWorldSpaceVrCursorVisible(true);
        }

        private bool TryReadPcCursorVisual(
            out Sprite sprite,
            out Color color)
        {
            sprite = null;
            color = Color.white;

            if (vrPcCursorTransform == null)
                return false;

            try
            {
                Component[] components =
                    vrPcCursorTransform.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    if (component == null)
                        continue;

                    Type type = component.GetType();

                    PropertyInfo spriteProperty =
                        type.GetProperty(
                            "sprite",
                            BindingFlags.Instance | BindingFlags.Public);

                    if (spriteProperty == null ||
                        !spriteProperty.CanRead)
                        continue;

                    sprite =
                        spriteProperty.GetValue(component, null) as Sprite;

                    PropertyInfo colorProperty =
                        type.GetProperty(
                            "color",
                            BindingFlags.Instance | BindingFlags.Public);

                    if (colorProperty != null &&
                        colorProperty.CanRead &&
                        colorProperty.PropertyType == typeof(Color))
                    {
                        object value =
                            colorProperty.GetValue(component, null);

                        if (value is Color)
                            color = (Color)value;
                    }

                    return sprite != null;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool TryGetVrCursorSurfaceHit(
            out RaycastHit selectedHit)
        {
            selectedHit = new RaycastHit();

            if (playerCamera == null)
                return false;

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction =
                playerCamera.transform.forward.normalized;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    direction,
                    VrWorldCursorMaxDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(
                hits,
                delegate (RaycastHit a, RaycastHit b)
                {
                    return a.distance.CompareTo(b.distance);
                });

            for (int i = 0; i < hits.Length; i++)
            {
                if (IsUsableInteractionSurface(hits[i], false))
                {
                    selectedHit = hits[i];
                    return true;
                }
            }

            for (int i = 0; i < hits.Length; i++)
            {
                if (IsUsableInteractionSurface(hits[i], true))
                {
                    selectedHit = hits[i];
                    return true;
                }
            }

            return false;
        }

        private void EnsureWorldSpaceVrCursor()
        {
            if (vrWorldCursorObject != null &&
                vrWorldCursorRenderer != null)
                return;

            vrWorldCursorObject =
                new GameObject("TCGVR World Cursor");

            vrWorldCursorRenderer =
                vrWorldCursorObject.AddComponent<SpriteRenderer>();

            vrWorldCursorRenderer.sortingOrder = 32767;
            vrWorldCursorRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            vrWorldCursorRenderer.receiveShadows = false;

            vrWorldCursorObject.SetActive(false);

            MelonLogger.Msg(
                "0.0.89 SpriteRenderer world cursor created. " +
                "PhysicalSize=" +
                VrWorldCursorPhysicalSize.ToString("F3") +
                "m surfaceOffset=" +
                VrWorldCursorSurfaceOffset.ToString("F3") +
                "m");
        }

        private void UpdateWorldCursorPhysicalScale(Sprite sprite)
        {
            if (sprite == null || vrWorldCursorObject == null)
                return;

            Vector2 spriteSize = sprite.bounds.size;

            float largest =
                Mathf.Max(spriteSize.x, spriteSize.y);

            if (largest <= 0.000001f)
            {
                vrWorldCursorObject.transform.localScale =
                    Vector3.one;
                return;
            }

            float scale =
                VrWorldCursorPhysicalSize / largest;

            vrWorldCursorObject.transform.localScale =
                new Vector3(scale, scale, scale);
        }

        private void SetWorldSpaceVrCursorVisible(bool visible)
        {
            if (vrWorldCursorObject != null &&
                vrWorldCursorObject.activeSelf != visible)
            {
                vrWorldCursorObject.SetActive(visible);
            }
        }

        private void RestoreVrPcCursorScale()
        {
            if (vrPcCursorTransform != null &&
                vrPcCursorOriginalScaleStored)
            {
                vrPcCursorTransform.localScale =
                    vrPcCursorOriginalScale;
            }

            vrPcCursorTransform = null;
            vrPcCursorOriginalScaleStored = false;
        }

        private void DestroyWorldSpaceVrCursor()
        {
            RestoreVrPcCursorScale();

            if (vrWorldCursorObject != null)
                UnityEngine.Object.Destroy(vrWorldCursorObject);

            vrWorldCursorObject = null;
            vrWorldCursorRenderer = null;
            vrWorldCursorLastSprite = null;
            vrLastCursorStateName = "";
            vrLastCursorStateDumpTime = -1000.0f;
            vrGrabProbeUntil = -1000.0f;
            vrNextGrabProbeTime = -1000.0f;
            vrGrabProbeLastSprite = "";
        }

        private Transform FindTransformByPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;

            Transform[] allTransforms =
                Resources.FindObjectsOfTypeAll<Transform>();

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform current = allTransforms[i];

                if (current == null || current.gameObject == null)
                    continue;

                if (GetTransformPath(current) == fullPath)
                    return current;
            }

            return null;
        }

        // ====================================================
        // 0.0.89 F11 aim comparison diagnostic
        // ====================================================

        private int vrAimDiagnosticSample;

        private void DumpVrAimComparison()
        {
            vrAimDiagnosticSample++;

            MelonLogger.Msg("====================================");
            MelonLogger.Msg(
                "0.0.89 AIM SAMPLE #" +
                vrAimDiagnosticSample);

            if (playerCamera == null)
            {
                MelonLogger.Warning("AIM_SAMPLE: playerCamera is null.");
                MelonLogger.Msg("====================================");
                return;
            }

            Transform cam = playerCamera.transform;

            MelonLogger.Msg(
                "AIM_SAMPLE: cameraPos=" +
                FormatVector3(cam.position) +
                " cameraForward=" +
                FormatVector3(cam.forward.normalized) +
                " cameraEuler=" +
                FormatVector3(cam.eulerAngles));

            MelonLogger.Msg(
                "AIM_SAMPLE: relativeHmdYaw=" +
                GetRelativeHeadYawDegrees().ToString("F4") +
                " injectedYaw=" +
                pmLastInjectedHeadYaw.ToString("F4"));

            MelonLogger.Msg(
                "AIM_SAMPLE: EulerX=" +
                ReadFsmFloat(pmEulerX, 0.0f).ToString("F4") +
                " EulerY=" +
                ReadFsmFloat(pmEulerY, 0.0f).ToString("F4") +
                " neutralEulerY=" +
                pmNeutralEulerY.ToString("F4"));

            RaycastHit hit;
            if (TryGetVrCursorSurfaceHit(out hit))
            {
                MelonLogger.Msg(
                    "AIM_SAMPLE: hmdHit=True distance=" +
                    hit.distance.ToString("F4") +
                    " point=" +
                    FormatVector3(hit.point) +
                    " normal=" +
                    FormatVector3(hit.normal) +
                    " object=" +
                    (hit.collider != null
                        ? GetTransformPath(hit.collider.transform)
                        : "<null>"));
            }
            else
            {
                MelonLogger.Msg("AIM_SAMPLE: hmdHit=False");
            }

            string sourceSprite = "<none>";
            Color sourceColor;
            Sprite sourceSpriteObject;

            if (TryReadPcCursorVisual(
                    out sourceSpriteObject,
                    out sourceColor) &&
                sourceSpriteObject != null)
            {
                sourceSprite = sourceSpriteObject.name;
            }

            MelonLogger.Msg(
                "AIM_SAMPLE: cursorSprite=" +
                sourceSprite);

            MelonLogger.Msg("====================================");
        }

        // ====================================================
        // 0.0.89 render / projection / submit-chain diagnostic
        // ====================================================

        private void UpdateRenderSubmitDiagnostic()
        {
            if (!stereoWanted || leftEyeCamera == null || rightEyeCamera == null)
                return;

            int unityFrame = Time.frameCount;
            if (vrSubmitDiagLastUnityFrame == unityFrame)
                vrSubmitDiagDuplicateUnityFrames++;

            vrSubmitDiagRenderAdvances++;

            Matrix4x4 lp = leftEyeCamera.projectionMatrix;
            Matrix4x4 rp = rightEyeCamera.projectionMatrix;

            if (vrSubmitDiagHaveProjection)
            {
                float d = 0.0f;
                d = Mathf.Max(d, Mathf.Abs(lp.m00 - vrSubmitDiagLastLeftProjection.m00));
                d = Mathf.Max(d, Mathf.Abs(lp.m02 - vrSubmitDiagLastLeftProjection.m02));
                d = Mathf.Max(d, Mathf.Abs(lp.m11 - vrSubmitDiagLastLeftProjection.m11));
                d = Mathf.Max(d, Mathf.Abs(lp.m12 - vrSubmitDiagLastLeftProjection.m12));
                d = Mathf.Max(d, Mathf.Abs(rp.m00 - vrSubmitDiagLastRightProjection.m00));
                d = Mathf.Max(d, Mathf.Abs(rp.m02 - vrSubmitDiagLastRightProjection.m02));
                d = Mathf.Max(d, Mathf.Abs(rp.m11 - vrSubmitDiagLastRightProjection.m11));
                d = Mathf.Max(d, Mathf.Abs(rp.m12 - vrSubmitDiagLastRightProjection.m12));
                vrSubmitDiagMaxProjectionDelta = Mathf.Max(vrSubmitDiagMaxProjectionDelta, d);
            }

            vrSubmitDiagLastLeftProjection = lp;
            vrSubmitDiagLastRightProjection = rp;
            vrSubmitDiagHaveProjection = true;
            vrSubmitDiagLastUnityFrame = unityFrame;

            float now = Time.realtimeSinceStartup;
            if (vrSubmitDiagNextLogTime < 0.0f)
            {
                vrSubmitDiagNextLogTime = now + 2.0f;
                return;
            }
            if (now < vrSubmitDiagNextLogTime)
                return;

            MelonLogger.Msg(
                "0.0.89 RENDER_SUBMIT: unityFrame=" + unityFrame +
                " stereoFrame=" + stereoRenderedFrameCount +
                " renderCalls=" + vrSubmitDiagRenderAdvances +
                " duplicateUnityFrames=" + vrSubmitDiagDuplicateUnityFrames +
                " maxProjectionDelta=" + vrSubmitDiagMaxProjectionDelta.ToString("F7") +
                " left=" + lp.m00.ToString("F6") + "," + lp.m02.ToString("F6") + "," +
                lp.m11.ToString("F6") + "," + lp.m12.ToString("F6") +
                " right=" + rp.m00.ToString("F6") + "," + rp.m02.ToString("F6") + "," +
                rp.m11.ToString("F6") + "," + rp.m12.ToString("F6"));

            vrSubmitDiagNextLogTime = now + 2.0f;
            vrSubmitDiagDuplicateUnityFrames = 0;
            vrSubmitDiagRenderAdvances = 0;
            vrSubmitDiagMaxProjectionDelta = 0.0f;
        }

        // ====================================================
        // 0.0.89 lightweight frame pacing diagnostic
        // ====================================================

        private void UpdateFramePacingDiagnostic()
        {
            if (!stereoWanted)
            {
                vrLastFramePacingTime = -1.0f;
                vrFramePacingWindowStart = -1000.0f;
                vrFramePacingFrames = 0;
                return;
            }

            float now = Time.realtimeSinceStartup;

            if (vrLastFramePacingTime >= 0.0f)
            {
                float deltaMs = (now - vrLastFramePacingTime) * 1000.0f;

                vrFramePacingFrames++;

                if (deltaMs > vrFramePacingWorstDeltaMs)
                    vrFramePacingWorstDeltaMs = deltaMs;

                if (deltaMs > 20.0f)
                    vrFramePacingOver20Ms++;

                if (deltaMs > 30.0f)
                    vrFramePacingOver30Ms++;

                if (deltaMs > 50.0f)
                    vrFramePacingOver50Ms++;
            }

            vrLastFramePacingTime = now;

            if (vrFramePacingWindowStart < 0.0f)
            {
                vrFramePacingWindowStart = now;
                vrNextFramePacingLogTime = now + 5.0f;
                return;
            }

            if (now < vrNextFramePacingLogTime)
                return;

            float elapsed = now - vrFramePacingWindowStart;
            float averageFps =
                elapsed > 0.001f
                    ? vrFramePacingFrames / elapsed
                    : 0.0f;

            MelonLogger.Msg(
                "0.0.89 FRAME_PACING: window=" +
                elapsed.ToString("F2") +
                "s fps=" +
                averageFps.ToString("F1") +
                " worstDelta=" +
                vrFramePacingWorstDeltaMs.ToString("F2") +
                "ms over20=" +
                vrFramePacingOver20Ms +
                " over30=" +
                vrFramePacingOver30Ms +
                " over50=" +
                vrFramePacingOver50Ms);

            vrFramePacingWindowStart = now;
            vrNextFramePacingLogTime = now + 5.0f;
            vrFramePacingFrames = 0;
            vrFramePacingWorstDeltaMs = 0.0f;
            vrFramePacingOver20Ms = 0;
            vrFramePacingOver30Ms = 0;
            vrFramePacingOver50Ms = 0;
        }

        // ====================================================
        // 0.0.89 lightweight physical grab / hold probe
        // ====================================================

        private void UpdateGrabHoldProbe()
        {
            if (!stereoWanted ||
                playerCamera == null ||
                Time.unscaledTime > vrGrabProbeUntil ||
                Time.unscaledTime < vrNextGrabProbeTime)
                return;

            vrNextGrabProbeTime = Time.unscaledTime + 0.5f;

            RaycastHit[] hits = GetSortedHeadRayHits();

            if (hits == null)
                return;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;

                if (collider == null)
                    continue;

                string path = GetTransformPath(collider.transform);

                if (path.StartsWith("PLAYER/Main Player-Camera/"))
                    continue;

                Rigidbody body = collider.attachedRigidbody;

                if (body == null)
                    continue;

                Vector3 cameraPos = playerCamera.transform.position;
                Vector3 cameraForward =
                    playerCamera.transform.forward.normalized;
                Vector3 bodyPos = body.position;
                Vector3 cameraToBody = bodyPos - cameraPos;

                float forwardDistance =
                    Vector3.Dot(cameraToBody, cameraForward);

                Vector3 closestPoint =
                    cameraPos + cameraForward * forwardDistance;

                float lateralDistance =
                    Vector3.Distance(bodyPos, closestPoint);

                MelonLogger.Msg(
                    "0.0.89 GRAB_PROBE: sprite=\"" +
                    vrGrabProbeLastSprite +
                    "\" object=\"" +
                    body.gameObject.name +
                    "\" collider=\"" +
                    collider.gameObject.name +
                    "\" cameraPos=" +
                    FormatVector3(cameraPos) +
                    " cameraForward=" +
                    FormatVector3(cameraForward) +
                    " bodyPos=" +
                    FormatVector3(bodyPos) +
                    " forwardDistance=" +
                    forwardDistance.ToString("F3") +
                    " lateralDistance=" +
                    lateralDistance.ToString("F3") +
                    " velocity=" +
                    FormatVector3(body.velocity) +
                    " angularVelocity=" +
                    FormatVector3(body.angularVelocity) +
                    " kinematic=" +
                    body.isKinematic +
                    " gravity=" +
                    body.useGravity);

                DumpGrabRelevantFsms(
                    collider.transform,
                    body.gameObject.name);

                break;
            }
        }

        private void DumpGrabRelevantFsms(
            Transform startTransform,
            string bodyName)
        {
            Transform current = startTransform;

            for (int depth = 0;
                 current != null && depth < 3;
                 depth++, current = current.parent)
            {
                MonoBehaviour[] behaviours =
                    current.GetComponents<MonoBehaviour>();

                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];

                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();

                    if (type.Name != "PlayMakerFSM" &&
                        type.FullName !=
                        "HutongGames.PlayMaker.PlayMakerFSM")
                        continue;

                    string fsmName =
                        Convert.ToString(
                            GetMemberValue(behaviour, "FsmName"));

                    object fsm =
                        GetMemberValue(behaviour, "Fsm");

                    string activeState =
                        Convert.ToString(
                            GetMemberValue(
                                behaviour,
                                "ActiveStateName"));

                    if (string.IsNullOrEmpty(activeState) &&
                        fsm != null)
                    {
                        object activeStateObject =
                            GetMemberValue(fsm, "ActiveState");

                        if (activeStateObject != null)
                        {
                            activeState =
                                Convert.ToString(
                                    GetMemberValue(
                                        activeStateObject,
                                        "Name"));
                        }
                    }

                    MelonLogger.Msg(
                        "0.0.89 GRAB_FSM: body=\"" +
                        bodyName +
                        "\" fsm=\"" +
                        fsmName +
                        "\" state=\"" +
                        activeState +
                        "\" path=\"" +
                        GetTransformPath(current) +
                        "\"");

                    // Inspect the actual game actions without changing their
                    // state, inputs, targets, or rigidbody properties.
                    string diagnosticKey = behaviour.GetInstanceID() + ":" + activeState;
                    if (Time.unscaledTime <= vrDetailedDiagnosticUntil &&
                        vrGrabDiagnosticStates.Count < 12 &&
                        vrGrabDiagnosticStates.Add(diagnosticKey))
                    {
                        object state = GetMemberValue(fsm, "ActiveState");
                        Array actions = GetMemberValue(state, "Actions") as Array;
                        if (actions != null)
                        {
                            int actionCount = 0;
                            foreach (object action in actions)
                            {
                                if (action == null || actionCount++ >= 12)
                                    continue;
                                MelonLogger.Msg("GRAB_ACTION: body=" + bodyName +
                                    " state=" + activeState + " type=" + action.GetType().FullName);
                                DumpObjectFields(action, "  ");
                            }
                        }
                    }
                }
            }
        }

        // ====================================================
        // 0.0.89 automatic cursor / interaction-state diagnostic
        // ====================================================

        private void DumpAutomaticCursorStateTransition(string newSpriteName)
        {
            if (string.IsNullOrEmpty(newSpriteName))
                return;

            if (string.Equals(
                    vrLastCursorStateName,
                    newSpriteName,
                    StringComparison.Ordinal))
                return;

            string oldSpriteName = vrLastCursorStateName;
            vrLastCursorStateName = newSpriteName;

            // Detailed hierarchy/reflection dumps are opt-in: cursor changes
            // must not stall the main thread while rendering stereo frames.
            if (Time.unscaledTime > vrDetailedDiagnosticUntil ||
                Time.unscaledTime - vrLastCursorStateDumpTime < 1.0f)
                return;

            vrLastCursorStateDumpTime = Time.unscaledTime;

            if (newSpriteName.IndexOf(
                    "GRAB",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                vrGrabProbeUntil = Time.unscaledTime + 3.0f;
                vrNextGrabProbeTime = Time.unscaledTime;
                vrGrabProbeLastSprite = newSpriteName;
            }

            MelonLogger.Msg("====================================");
            MelonLogger.Msg(
                "0.0.89 CURSOR STATE CHANGE: \"" +
                (string.IsNullOrEmpty(oldSpriteName) ? "<none>" : oldSpriteName) +
                "\" -> \"" + newSpriteName + "\"");

            if (playerCamera != null)
            {
                MelonLogger.Msg(
                    "CURSOR_STATE: cameraPos=" +
                    FormatVector3(playerCamera.transform.position) +
                    " cameraForward=" +
                    FormatVector3(playerCamera.transform.forward.normalized) +
                    " cameraEuler=" +
                    FormatVector3(playerCamera.transform.eulerAngles));

                MelonLogger.Msg(
                    "CURSOR_STATE: EulerX=" +
                    ReadFsmFloat(pmEulerX, 0.0f).ToString("F4") +
                    " EulerY=" +
                    ReadFsmFloat(pmEulerY, 0.0f).ToString("F4") +
                    " neutralEulerY=" +
                    pmNeutralEulerY.ToString("F4"));
            }

            RaycastHit[] hits = GetSortedHeadRayHits();

            if (hits != null)
            {
                int loggedHits = 0;

                for (int i = 0; i < hits.Length && loggedHits < 8; i++)
                {
                    Collider collider = hits[i].collider;
                    if (collider == null)
                        continue;

                    string path = GetTransformPath(collider.transform);

                    if (path.StartsWith("PLAYER/Main Player-Camera/"))
                        continue;

                    MelonLogger.Msg(
                        "CURSOR_STATE_HIT[" + loggedHits + "] distance=" +
                        hits[i].distance.ToString("F4") +
                        " point=" +
                        FormatVector3(hits[i].point) +
                        " object=\"" +
                        collider.gameObject.name +
                        "\" layer=\"" +
                        LayerMask.LayerToName(collider.gameObject.layer) +
                        "\" trigger=" +
                        collider.isTrigger +
                        " path=\"" +
                        path + "\"");

                    DumpPlayMakerFsmsOnTransformAndParents(
                        collider.transform,
                        "CURSOR_STATE_HIT[" + loggedHits + "]");

                    loggedHits++;
                }
            }

            if (vrPcCursorTransform != null)
            {
                DumpPlayMakerFsmsOnTransformAndParents(
                    vrPcCursorTransform,
                    "CURSOR_SOURCE");
            }

            if (playerCamera != null)
            {
                DumpPlayMakerFsmsOnTransformAndParents(
                    playerCamera.transform,
                    "CAMERA");
            }

            MelonLogger.Msg("====================================");
        }

        private RaycastHit[] GetSortedHeadRayHits()
        {
            if (playerCamera == null)
                return null;

            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward.normalized,
                VrWorldCursorMaxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null)
                return null;

            Array.Sort(
                hits,
                delegate (RaycastHit a, RaycastHit b)
                {
                    return a.distance.CompareTo(b.distance);
                });

            return hits;
        }

        private void DumpPlayMakerFsmsOnTransformAndParents(
            Transform startTransform,
            string label)
        {
            if (startTransform == null)
                return;

            Transform current = startTransform;

            for (int parentDepth = 0;
                 current != null && parentDepth < 4;
                 parentDepth++, current = current.parent)
            {
                MonoBehaviour[] behaviours =
                    current.GetComponents<MonoBehaviour>();

                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];

                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();

                    if (type.Name != "PlayMakerFSM" &&
                        type.FullName != "HutongGames.PlayMaker.PlayMakerFSM")
                        continue;

                    DumpSinglePlayMakerFsm(
                        behaviour,
                        label +
                        " parentDepth=" +
                        parentDepth +
                        " path=\"" +
                        GetTransformPath(current) +
                        "\"");
                }
            }
        }

        private void DumpSinglePlayMakerFsm(
            MonoBehaviour behaviour,
            string label)
        {
            try
            {
                string fsmName =
                    Convert.ToString(
                        GetMemberValue(behaviour, "FsmName"));

                object fsm =
                    GetMemberValue(behaviour, "Fsm");

                string activeState =
                    Convert.ToString(
                        GetMemberValue(behaviour, "ActiveStateName"));

                if (string.IsNullOrEmpty(activeState) && fsm != null)
                {
                    object activeStateObject =
                        GetMemberValue(fsm, "ActiveState");

                    if (activeStateObject != null)
                    {
                        activeState =
                            Convert.ToString(
                                GetMemberValue(
                                    activeStateObject,
                                    "Name"));
                    }
                }

                MelonLogger.Msg(
                    "CURSOR_FSM: " +
                    label +
                    " fsm=\"" +
                    fsmName +
                    "\" state=\"" +
                    activeState +
                    "\"");

                if (fsm == null)
                    return;

                object variables =
                    GetMemberValue(fsm, "Variables");

                if (variables == null)
                    variables =
                        GetMemberValue(fsm, "FsmVariables");

                if (variables == null)
                    return;

                string[] collections =
                {
                    "FloatVariables",
                    "IntVariables",
                    "BoolVariables",
                    "StringVariables",
                    "GameObjectVariables",
                    "ObjectVariables",
                    "Vector2Variables",
                    "Vector3Variables"
                };

                int loggedVariables = 0;

                for (int collectionIndex = 0;
                     collectionIndex < collections.Length &&
                     loggedVariables < 40;
                     collectionIndex++)
                {
                    Array variableArray =
                        GetMemberValue(
                            variables,
                            collections[collectionIndex]) as Array;

                    if (variableArray == null)
                        continue;

                    for (int variableIndex = 0;
                         variableIndex < variableArray.Length &&
                         loggedVariables < 40;
                         variableIndex++)
                    {
                        object variable =
                            variableArray.GetValue(variableIndex);

                        if (variable == null)
                            continue;

                        string variableName =
                            Convert.ToString(
                                GetMemberValue(variable, "Name"));

                        if (string.IsNullOrEmpty(variableName))
                            continue;

                        object value =
                            GetMemberValue(variable, "Value");

                        string valueText =
                            FormatDiagnosticValue(value);

                        MelonLogger.Msg(
                            "CURSOR_FSM_VAR: fsm=\"" +
                            fsmName +
                            "\" " +
                            collections[collectionIndex] +
                            " \"" +
                            variableName +
                            "\"=\"" +
                            valueText +
                            "\"");

                        loggedVariables++;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "0.0.89 cursor FSM diagnostic failed: " +
                    ex.Message);
            }
        }

        private string FormatDiagnosticValue(object value)
        {
            if (value == null)
                return "<null>";

            GameObject gameObject = value as GameObject;

            if (gameObject != null)
                return GetTransformPath(gameObject.transform);

            Component component = value as Component;

            if (component != null)
                return GetTransformPath(component.transform) +
                    " (" + component.GetType().Name + ")";

            try
            {
                return value.ToString();
            }
            catch
            {
                return "<unprintable>";
            }
        }

        // ====================================================
        // Stereo Render
        // ====================================================

        private void RenderStereoEyes()
        {
            if (!stereoCamerasCreated || !stereoPoseValid)
                return;

            if (!stereoRenderTexturesCreated)
                CreateStereoRenderTextures();

            if (leftEyeCamera == null || rightEyeCamera == null)
                return;

            RenderEyeWithHud(leftEyeCamera);
            RenderEyeWithHud(rightEyeCamera);

            UpdateRenderSubmitDiagnostic();

            // 0.0.61 diagnostic only: capture the real camera/projection state
            // before the existing vertical submit blit. No render state is changed.
            if (orientationStartupSnapshotsLogged < 3)
            {
                orientationStartupSnapshotsLogged++;
                PrintOrientationSnapshot("PRE-BLIT frame " + orientationStartupSnapshotsLogged);
            }

            // Unity/D3D11 -> OpenXR Orientierung korrigieren.
            Graphics.Blit(
                leftEyeRenderTexture,
                leftEyeSubmitTexture,
                new Vector2(1.0f, -1.0f),
                new Vector2(0.0f, 1.0f));

            Graphics.Blit(
                rightEyeRenderTexture,
                rightEyeSubmitTexture,
                new Vector2(1.0f, -1.0f),
                new Vector2(0.0f, 1.0f));

            stereoRenderedFrameCount++;

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            if (textureCopyRequested && nativeTexturePointersRegistered && !continuousVRRendering)
            {
                GL.IssuePluginEvent(renderEventFunction, RenderEventCopyStereoTextures);
                textureCopyRequested = false;
            }

            if (
                singleFrameRequested
                &&
                nativeTexturePointersRegistered
                &&
                !continuousVRRendering
                &&
                renderPoseSnapshotReadyThisFrame
                )
            {
                GL.IssuePluginEvent(renderEventFunction, RenderEventSubmitSingleFrame);
                projectionEventQueuedThisFrame = true;
                singleFrameRequested = false;
            }

            if (
                burstActive
                &&
                nativeTexturePointersRegistered
                &&
                !continuousVRRendering
                &&
                renderPoseSnapshotReadyThisFrame
                )
            {
                GL.IssuePluginEvent(renderEventFunction, RenderEventSubmitSingleFrame);
                projectionEventQueuedThisFrame = true;
                burstFramesQueued++;

                if (burstFramesQueued >= BurstFrameTarget)
                {
                    burstActive = false;
                    MelonLogger.Msg("30-frame burst completed.");
                }
            }

            if (
                continuousVRRendering
                &&
                nativeTexturePointersRegistered
                &&
                renderEventFunction != IntPtr.Zero
                &&
                renderPoseSnapshotReadyThisFrame
                )
            {
                GL.IssuePluginEvent(renderEventFunction, RenderEventSubmitSingleFrame);
                projectionEventQueuedThisFrame = true;
            }

            if (!firstStereoFrameLogged)
            {
                firstStereoFrameLogged = true;
                MelonLogger.Msg("First 0.0.89 Unity stereo frame rendered.");
            }
        }

        // ====================================================
        // F2-F5
        // ====================================================

        private void StartThirtyFrameBurst()
        {
            if (!sessionStarted || !stereoRenderingEnabled)
            {
                MelonLogger.Warning("Start OpenXR and stereo first.");
                return;
            }

            if (continuousVRRendering)
            {
                MelonLogger.Warning("Disable continuous VR first.");
                return;
            }

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            RefreshFrameSubmitInfo();
            burstStartSuccessfulFrames = successfulSubmittedFrames;
            burstFramesQueued = 0;
            burstActive = true;
            MelonLogger.Msg("30-frame OpenXR burst started.");
        }

        private void RequestSingleProjectionFrame()
        {
            if (!sessionStarted || !stereoRenderingEnabled)
            {
                MelonLogger.Warning("Start OpenXR and stereo first.");
                return;
            }

            if (continuousVRRendering)
            {
                MelonLogger.Warning("Continuous VR is already running.");
                return;
            }

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            singleFrameRequested = true;
            MelonLogger.Msg("Single projection frame requested.");
        }

        private void RequestSingleTextureCopy()
        {
            if (!sessionStarted || !stereoRenderingEnabled)
            {
                MelonLogger.Warning("Start OpenXR and stereo first.");
                return;
            }

            if (continuousVRRendering)
            {
                MelonLogger.Warning("Continuous VR is already running.");
                return;
            }

            if (!nativeTexturePointersRegistered)
                RegisterNativeSubmitTextures();

            textureCopyRequested = true;
            MelonLogger.Msg("Texture copy requested.");
        }

        private void RunAcquireTest()
        {
            if (!openXRSwapchainsCreated)
            {
                MelonLogger.Warning("Swapchains are missing.");
                return;
            }

            if (continuousVRRendering)
            {
                MelonLogger.Warning("Disable continuous VR first.");
                return;
            }

            int result = TCGVR_TestSwapchainAcquireRelease();
            MelonLogger.Msg("Acquire test result: " + result);
        }

        // ====================================================
        // Body Follow
        // ====================================================

        private void ApplyImmediateBodyFollow(ref Vector3 relativePosition)
        {
            Vector3 localMove = new Vector3(relativePosition.x, 0.0f, relativePosition.z);

            if (localMove.sqrMagnitude < 0.00000001f)
            {
                relativePosition.x = 0.0f;
                relativePosition.z = 0.0f;
                return;
            }

            Vector3 worldMove = playerRoot.TransformDirection(localMove);
            worldMove.y = 0.0f;

            playerRigidbody.MovePosition(playerRigidbody.position + worldMove);
            referenceHeadPosition += referenceHeadRotation * localMove;

            relativePosition.x = 0.0f;
            relativePosition.z = 0.0f;
        }

        private void RestoreBasePose(Vector3 position, Quaternion rotation)
        {
            if (playerCamera == null)
                return;

            playerCamera.transform.localPosition = position;
            playerCamera.transform.localRotation = rotation;
            lastFinalCameraPosition = position;
            lastFinalCameraRotation = rotation;
        }

        // ====================================================
        // Player / Head finden
        // ====================================================

        private void FindPlayerObjects()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            Camera found = null;

            foreach (Camera cam in cameras)
            {
                if (cam == null || !cam.isActiveAndEnabled || cam.name != "Main Player-Camera")
                    continue;

                bool correctTag = false;
                try { correctTag = cam.CompareTag("MainCamera"); }
                catch { }

                if (correctTag)
                {
                    found = cam;
                    break;
                }
            }

            bool menuCamera = found == null;
            if (menuCamera)
            {
                // Menus/logos can use a different camera before PLAYER exists.
                Camera main = Camera.main;
                if (main != null && main.isActiveAndEnabled &&
                    !main.name.StartsWith("TCGVR", StringComparison.Ordinal) && main.targetTexture == null)
                    found = main;
                if (found == null)
                {
                    foreach (Camera candidate in cameras)
                    {
                        if (candidate == null || !candidate.isActiveAndEnabled ||
                            candidate.targetTexture != null ||
                            candidate.name.StartsWith("TCGVR", StringComparison.Ordinal))
                            continue;
                        if (found == null || candidate.depth > found.depth)
                            found = candidate;
                    }
                }
            }

            if (found == null)
                return;

            if (playerCamera != null && playerCamera != found)
            {
                RestoreMouseLookPitch();
                RestorePlayMakerHorizontalHeadAim();
                RestorePlayMakerVerticalLookActions();
                RestoreBodyMesh();
                RestoreHeadVisibility();
                DestroyStereoResources();
                ResetPlayMakerCameraBinding();
                firstPersonController = null;
                mouseLookObject = null;
                headAimControllerFound = false;
                headBone = null;
                bodyRenderer = null;
                originalHeadScaleStored = false;
                originalEyeStateStored = false;
                neutralCameraRotationStored = false;
                trackingContributionApplied = false;
                referencePoseValid = false;
                recenterRequested = true;
                if (vrMasterWanted)
                {
                    stereoWanted = sessionStarted;
                    continuousWanted = sessionStarted;
                    vrActivationPending = true;
                }
            }

            if (playerCamera != found)
                MelonLogger.Msg("VR source camera: " + GetTransformPath(found.transform) +
                    " menuCamera=" + menuCamera);

            playerCamera = found;
            useMenuCamera = menuCamera;
            playerRoot = null;
            playerRigidbody = null;

            if (!neutralCameraRotationStored)
            {
                neutralCameraLocalRotation = ExtractYawOnly(playerCamera.transform.localRotation);
                playerCamera.transform.localRotation = neutralCameraLocalRotation;
                neutralCameraRotationStored = true;
            }

            Transform current = playerCamera.transform;
            while (current != null)
            {
                if (current.name == "PLAYER")
                {
                    playerRoot = current;
                    playerRigidbody = current.GetComponent<Rigidbody>();
                    break;
                }
                current = current.parent;
            }

            FindHeadObjects();
            FindFirstPersonController();
            FindBodyRenderer();
        }

        private void FindHeadObjects()
        {
            if (playerRoot == null)
                return;

            Transform[] all = playerRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in all)
            {
                if (t == null)
                    continue;

                if (t.name == "mixamorig:Head")
                    headBone = t;

                if (t.name == "Eye_Left")
                    leftEyeRenderer = t.GetComponent<Renderer>();

                if (t.name == "Eye_Right")
                    rightEyeRenderer = t.GetComponent<Renderer>();
            }

            if (headBone != null && !originalHeadScaleStored)
            {
                originalHeadScale = headBone.localScale;
                originalHeadScaleStored = true;
            }

            if (!originalEyeStateStored)
            {
                if (leftEyeRenderer != null)
                    originalLeftEyeEnabled = leftEyeRenderer.enabled;

                if (rightEyeRenderer != null)
                    originalRightEyeEnabled = rightEyeRenderer.enabled;

                originalEyeStateStored = true;
            }
        }

        private void ApplyHeadVisibility()
        {
            if (headBone == null)
                return;

            headBone.localScale = hideOwnHead ? HiddenHeadScale : originalHeadScale;

            if (leftEyeRenderer != null)
                leftEyeRenderer.enabled = hideOwnHead ? false : originalLeftEyeEnabled;

            if (rightEyeRenderer != null)
                rightEyeRenderer.enabled = hideOwnHead ? false : originalRightEyeEnabled;
        }

        private void RestoreHeadVisibility()
        {
            if (headBone != null && originalHeadScaleStored)
                headBone.localScale = originalHeadScale;

            if (leftEyeRenderer != null)
                leftEyeRenderer.enabled = originalLeftEyeEnabled;

            if (rightEyeRenderer != null)
                rightEyeRenderer.enabled = originalRightEyeEnabled;
        }

        // ====================================================
        // Diagnose
        // ====================================================

        private void RefreshFrameSubmitInfo()
        {
            try
            {
                TCGVR_GetFrameSubmitInfo(
                    out lastFrameSubmitStep,
                    out lastFrameSubmitResult,
                    out lastWaitFrameResult,
                    out lastBeginFrameResult,
                    out lastEndFrameResult,
                    out lastShouldRender,
                    out lastSubmitLeftIndex,
                    out lastSubmitRightIndex,
                    out successfulSubmittedFrames);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex.ToString());
            }
        }

        private void PrintDiagnostics()
        {
            RefreshFrameSubmitInfo();

            try
            {
                trackingPredictionMs = TCGVR_GetTrackingPredictionMs();
            }
            catch { }


            try
            {
                TCGVR_GetRenderPoseInfo(
                    out capturedRenderPoseGeneration,
                    out submittedRenderPoseGeneration,
                    out renderPoseInFlight);
            }
            catch { }

            ulong continuousFrames = 0;
            if (successfulSubmittedFrames >= continuousStartSuccessfulFrames)
                continuousFrames = successfulSubmittedFrames - continuousStartSuccessfulFrames;

            ulong burstFrames = 0;
            if (successfulSubmittedFrames >= burstStartSuccessfulFrames)
                burstFrames = successfulSubmittedFrames - burstStartSuccessfulFrames;

            MelonLogger.Msg("====================================");
            MelonLogger.Msg("TCGVR DIAGNOSTICS 0.0.71");
            MelonLogger.Msg("Tracking Prediction: " + trackingPredictionMs + " ms (0.0.89 snapshot mode)");
            MelonLogger.Msg("Render-Pose Capture Result: " + lastRenderPoseCaptureResult);
            MelonLogger.Msg("Render-Pose Generation Capture/Submit: " +
                capturedRenderPoseGeneration + " / " + submittedRenderPoseGeneration);
            MelonLogger.Msg("Render-Pose InFlight: " + renderPoseInFlight);
            MelonLogger.Msg("Recenter Pending: " + recenterRequested);
            MelonLogger.Msg("VR master wanted: " + vrMasterWanted);
            MelonLogger.Msg("VR activation pending: " + vrActivationPending);
            MelonLogger.Msg("Head aiming active: " + headAimActive);
            MelonLogger.Msg("PlayMaker camera binding: " + pmCameraBindingReady);
            MelonLogger.Msg("PlayMaker vertical actions disabled: " + pmDisabledVerticalActions.Count);
            MelonLogger.Msg("PlayMaker neutral EulerY: " + (pmNeutralEulerYStored ? pmNeutralEulerY.ToString() : "<not stored>"));
            MelonLogger.Msg("First-person controller found: " + headAimControllerFound);
            MelonLogger.Msg("Body hands-only mesh active: " + bodyMeshModified);
            MelonLogger.Msg("Body fallback hidden: " + bodyFallbackHidden);
            MelonLogger.Msg("Scene Rebuild Pending: " + pendingSceneRebuild);
            MelonLogger.Msg("Stereo wanted/active: " + stereoWanted + " / " + stereoRenderingEnabled);
            MelonLogger.Msg("Continuous VR wanted/active: " + continuousWanted + " / " + continuousVRRendering);
            MelonLogger.Msg("HUD canvases on last eye render: " + lastHudCanvasCount);
            MelonLogger.Msg("Render RT: " + stereoRenderWidth + " x " + stereoRenderHeight);
            MelonLogger.Msg("Stereo Frames: " + stereoRenderedFrameCount);
            MelonLogger.Msg("Successful continuous frames: " + continuousFrames);
            MelonLogger.Msg("Burst Frames: " + burstFrames);
            MelonLogger.Msg("Step: " + lastFrameSubmitStep + " / " + SubmitStepToString(lastFrameSubmitStep));
            MelonLogger.Msg("Overall result: " + lastFrameSubmitResult);
            MelonLogger.Msg("xrWaitFrame: " + lastWaitFrameResult);
            MelonLogger.Msg("xrBeginFrame: " + lastBeginFrameResult);
            MelonLogger.Msg("shouldRender: " + (lastShouldRender == 1 ? "TRUE" : "FALSE"));
            MelonLogger.Msg("Left Image: " + lastSubmitLeftIndex);
            MelonLogger.Msg("Right Image: " + lastSubmitRightIndex);
            MelonLogger.Msg("xrEndFrame: " + lastEndFrameResult);
            MelonLogger.Msg("Total submitted frames: " + successfulSubmittedFrames);
            MelonLogger.Msg("====================================");

            PrintHudSummary();
            PrintOrientationSnapshot("F12");
        }

        private void PrintOrientationSnapshot(string reason)
        {
            try
            {
                MelonLogger.Msg("====================================");
                MelonLogger.Msg("0.0.89 ORIENTATION SNAPSHOT: " + reason);
                MelonLogger.Msg("Graphics API: " + SystemInfo.graphicsDeviceType);
                MelonLogger.Msg("Graphics UV starts at top: " + SystemInfo.graphicsUVStartsAtTop);
                MelonLogger.Msg("GL.invertCulling: " + GL.invertCulling);
                MelonLogger.Msg("Screen: " + Screen.width + " x " + Screen.height + " orientation=" + Screen.orientation);
                MelonLogger.Msg("RenderTexture.active: " + (RenderTexture.active != null ? RenderTexture.active.name : "<null>"));
                PrintCameraOrientation("Main", playerCamera);
                PrintCameraOrientation("Left", leftEyeCamera);
                PrintCameraOrientation("Right", rightEyeCamera);
                PrintTextureOrientation("Left render", leftEyeRenderTexture);
                PrintTextureOrientation("Right render", rightEyeRenderTexture);
                PrintTextureOrientation("Left submit", leftEyeSubmitTexture);
                PrintTextureOrientation("Right submit", rightEyeSubmitTexture);
                MelonLogger.Msg("Submit blit scale=(1,-1) offset=(0,1): TRUE");
                MelonLogger.Msg("====================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Orientation diagnostic failed: " + ex.Message);
            }
        }

        private void PrintCameraOrientation(string label, Camera camera)
        {
            if (camera == null)
            {
                MelonLogger.Msg(label + " camera: <null>");
                return;
            }

            Transform t = camera.transform;
            Matrix4x4 p = camera.projectionMatrix;
            Matrix4x4 gpuRt = GL.GetGPUProjectionMatrix(p, true);
            Matrix4x4 gpuScreen = GL.GetGPUProjectionMatrix(p, false);

            MelonLogger.Msg(label + " camera: name=" + camera.name +
                " enabled=" + camera.enabled +
                " target=" + (camera.targetTexture != null ? camera.targetTexture.name : "<screen>"));
            MelonLogger.Msg(label + " localPos=" + FormatVector3(t.localPosition) +
                " localEuler=" + FormatVector3(t.localEulerAngles) +
                " worldEuler=" + FormatVector3(t.eulerAngles));
            MelonLogger.Msg(label + " forward=" + FormatVector3(t.forward) +
                " up=" + FormatVector3(t.up) +
                " right=" + FormatVector3(t.right));
            MelonLogger.Msg(label + " projection m00/m11/m12/m22/m23=" +
                FormatFloat(p.m00) + "/" + FormatFloat(p.m11) + "/" + FormatFloat(p.m12) + "/" + FormatFloat(p.m22) + "/" + FormatFloat(p.m23));
            MelonLogger.Msg(label + " GPU-RT m00/m11/m12/m22/m23=" +
                FormatFloat(gpuRt.m00) + "/" + FormatFloat(gpuRt.m11) + "/" + FormatFloat(gpuRt.m12) + "/" + FormatFloat(gpuRt.m22) + "/" + FormatFloat(gpuRt.m23));
            MelonLogger.Msg(label + " GPU-Screen m00/m11/m12/m22/m23=" +
                FormatFloat(gpuScreen.m00) + "/" + FormatFloat(gpuScreen.m11) + "/" + FormatFloat(gpuScreen.m12) + "/" + FormatFloat(gpuScreen.m22) + "/" + FormatFloat(gpuScreen.m23));
        }

        private void PrintTextureOrientation(string label, RenderTexture texture)
        {
            if (texture == null)
            {
                MelonLogger.Msg(label + " texture: <null>");
                return;
            }

            MelonLogger.Msg(label + " texture: name=" + texture.name +
                " size=" + texture.width + "x" + texture.height +
                " created=" + texture.IsCreated() +
                " antiAliasing=" + texture.antiAliasing +
                " dimension=" + texture.dimension);
        }

        private string FormatVector3(Vector3 value)
        {
            return "(" + FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z) + ")";
        }

        private string FormatFloat(float value)
        {
            return value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        }

        private string SubmitStepToString(int step)
        {
            switch (step)
            {
                case 0: return "No submit yet";
                case 1: return "Callback";
                case 2: return "Before xrWaitFrame";
                case 3: return "xrWaitFrame complete";
                case 4: return "Before xrBeginFrame";
                case 5: return "xrBeginFrame complete";
                case 6: return "Render-pose snapshot applied";
                case 7: return "Left image ready";
                case 8: return "Right image ready";
                case 9: return "D3D11 copy complete";
                case 10: return "Images released";
                case 11: return "Before xrEndFrame";
                case 12: return "xrEndFrame returned";
                default: return "Unknown";
            }
        }

        // ====================================================
        // Cleanup
        // ====================================================

        private void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        private void DestroyStereoResources()
        {
            DestroyWorldSpaceVrCursor();
            continuousVRRendering = false;
            stereoRenderingEnabled = false;
            burstActive = false;
            textureCopyRequested = false;
            singleFrameRequested = false;
            nativeTexturePointersRegistered = false;

            try { TCGVR_ReleaseRenderPoseSnapshot(); }
            catch { }

            try { TCGVR_ClearUnityStereoTextures(); }
            catch { }

            renderPoseSnapshotReadyThisFrame = false;
            projectionEventQueuedThisFrame = false;

            RestoreHudCanvases();

            if (leftEyeCamera != null)
                leftEyeCamera.targetTexture = null;

            if (rightEyeCamera != null)
                rightEyeCamera.targetTexture = null;

            ReleaseRenderTexture(ref leftEyeRenderTexture);
            ReleaseRenderTexture(ref rightEyeRenderTexture);
            ReleaseRenderTexture(ref leftEyeSubmitTexture);
            ReleaseRenderTexture(ref rightEyeSubmitTexture);

            stereoRenderTexturesCreated = false;

            if (leftEyeObject != null)
                UnityEngine.Object.Destroy(leftEyeObject);

            if (rightEyeObject != null)
                UnityEngine.Object.Destroy(rightEyeObject);

            leftEyeObject = null;
            rightEyeObject = null;
            leftEyeCamera = null;
            rightEyeCamera = null;
            stereoCamerasCreated = false;
            firstStereoFrameLogged = false;
        }

        public override void OnDeinitializeMelon()
        {
            RestoreVrPromptRootActivation();
            MelonLogger.Msg("The Coin Game VR shutdown...");

            continuousWanted = false;
            stereoWanted = false;
            continuousVRRendering = false;

            try { RestoreMouseLookPitch(); }
            catch { }

            try { RestoreBodyMesh(); }
            catch { }

            try { RestoreHeadVisibility(); }
            catch { }

            try { DestroyStereoResources(); }
            catch { }

            try { TCGVR_ShutdownOpenXR(); }
            catch (Exception ex) { MelonLogger.Error(ex.ToString()); }
        }
    }
}
