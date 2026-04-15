using System;
using System.Globalization;
using System.Text;
using UnityEngine;
public partial class FASyncRuntime : MVRScript
{
#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
    private static readonly string[] CuaPlayerLeftStickXCandidates = new string[]
    {
        "Oculus_CrossPlatform_PrimaryThumbstickHorizontal",
        "Horizontal",
        "Joy1 Axis 1",
        "Joy2 Axis 1",
        "Joy3 Axis 1",
        "Joy4 Axis 1",
        "X Axis",
    };

    private static readonly string[] CuaPlayerLeftStickYCandidates = new string[]
    {
        "Oculus_CrossPlatform_PrimaryThumbstickVertical",
        "Vertical",
        "Joy1 Axis 2",
        "Joy2 Axis 2",
        "Joy3 Axis 2",
        "Joy4 Axis 2",
        "Y Axis",
    };

    private static readonly string[] CuaPlayerRightStickXCandidates = new string[]
    {
        "Oculus_CrossPlatform_SecondaryThumbstickHorizontal",
        "Horizontal2",
        "4th axis",
        "Joy2 Axis 1",
        "Joy3 Axis 1",
        "Joy4 Axis 1",
        "Joy1 Axis 1",
        "Joy2 Axis 4",
        "Joy3 Axis 4",
        "Joy4 Axis 4",
        "Joy1 Axis 4",
    };

    private static readonly string[] CuaPlayerRightStickYCandidates = new string[]
    {
        "Oculus_CrossPlatform_SecondaryThumbstickVertical",
        "Vertical2",
        "5th axis",
        "Joy2 Axis 2",
        "Joy3 Axis 2",
        "Joy4 Axis 2",
        "Joy1 Axis 2",
        "Joy2 Axis 5",
        "Joy3 Axis 5",
        "Joy4 Axis 5",
        "Joy1 Axis 5",
    };

    private const float CuaPlayerFocusReleaseGraceSeconds = 0.20f;
    private const float CuaPlayerFocusInputGraceSeconds = 0.45f;
    private const float CuaPlayerNavigationDeadzone = 0.32f;
    private const float CuaPlayerNavigationAxisReleaseDeadzone = 0.18f;
    private const float CuaPlayerNavigationSourceActiveThreshold = 0.02f;
    private const float CuaPlayerNavigationStickContinuitySeconds = 0.30f;
    private const float CuaPlayerTriggerModifierThreshold = 0.50f;
    private const float CuaPlayerImageStepInitialRepeatSeconds = 0.35f;
    private const float CuaPlayerImageStepRepeatSeconds = 0.22f;
    private const float CuaPlayerGazeMinDistanceMeters = 0.05f;
    private const float CuaPlayerInputStateUpdateIntervalSeconds = 0.10f;

    private struct CuaPlayerNavigationSnapshot
    {
        public bool disableNavigation;
        public bool disableInternalKeyBindings;
        public bool disableInternalNavigationKeyBindings;
        public bool hasDisableAllNavigationToggle;
        public bool disableAllNavigationToggle;
        public bool hasDisableGrabNavigationToggle;
        public bool disableGrabNavigationToggle;
        public bool alwaysEnablePointers;
        public bool hasRayLineLeft;
        public bool rayLineLeftEnabled;
        public bool hasRayLineRight;
        public bool rayLineRightEnabled;
    }

    private struct CuaPlayerStickRead
    {
        public string slot;
        public Vector2 navigation;
        public string source;
        public bool valid;
        public bool active;
    }

    private enum CuaPlayerNavigationAxisLock
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2
    }

    private static FASyncRuntime cuaPlayerInputOwner;
    private static bool cuaPlayerNavigationCaptureActive;
    private static bool cuaPlayerNavigationSnapshotKnown;
    private static CuaPlayerNavigationSnapshot cuaPlayerNavigationSnapshot;

    private bool cuaPlayerFocusActive = false;
    private float cuaPlayerLastGazeSeenAt = -1000f;
    private float cuaPlayerLastInputActiveAt = -1000f;
    private float cuaPlayerNextImageStepTime = 0f;
    private int cuaPlayerImageStepDirection = 0;
    private CuaPlayerNavigationAxisLock cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.None;
    private bool cuaPlayerVideoScrubTargetKnown = false;
    private float cuaPlayerVideoScrubTargetNormalized = 0f;
    private bool cuaPlayerVideoScrubSessionActive = false;
    private bool cuaPlayerVideoScrubResumeAfterRelease = false;
    private string cuaPlayerVideoScrubPlaybackKey = "";
    private bool cuaPlayerTriggerTapArmed = false;
    private bool cuaPlayerTriggerTapUsedWithNavigation = false;
    private bool cuaPlayerLastTriggerTapActive = false;
    private float cuaPlayerNextInputStateUpdateAt = 0f;
    private string cuaPlayerLastInputState = "";
    private string cuaPlayerLastNavigationSource = "none";
    private string cuaPlayerLastNavigationStick = "none";
    private string cuaPlayerLastActiveDirectStick = "none";
    private bool cuaPlayerLastTriggerModifierActive = false;
    private bool cuaPlayerLastGrabNavigateOverrideActive = false;
    private float cuaPlayerLastActiveDirectStickAt = -1000f;
    private string cuaPlayerCachedSurfaceHostAtomUid = "";
    private GameObject cuaPlayerCachedScreenSurfaceObject;
    private string cuaPlayerLastVisibleScreenHostAtomUid = "";
    private void BuildCuaPlayerInputStorables()
    {
        playerInputStateField = new JSONStorableString(
            "FrameAngel Player CUA Status",
            "focus=off gaze=off mode=idle");
        ConfigureTransientField(playerInputStateField, false);

        playerFocusActiveField = new JSONStorableBool(
            "FrameAngel Player Focus Active",
            false);
        ConfigureTransientField(playerFocusActiveField, true);

        playerVisibleScreenField = new JSONStorableString(
            "FrameAngel Player Active Screen",
            "screen=none");
        ConfigureTransientField(playerVisibleScreenField, true);
    }

    private void RegisterCuaPlayerInputStorables()
    {
        RegisterString(playerInputStateField);
        RegisterBool(playerFocusActiveField);
        RegisterString(playerVisibleScreenField);
    }

    private void BuildCuaPlayerInputUi()
    {
        CreateTextField(playerInputStateField, false);
    }

    private void TickCuaPlayerInput()
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
        {
            ResetCuaPlayerTriggerTapState();
            UpdateCuaPlayerVisibleScreenState("");
            ReleaseCuaPlayerInputFocus("no_supercontroller");
            return;
        }

        bool gazeActive = TryResolveCuaPlayerGazeHit(out string gazeReason);
        float now = Time.unscaledTime;
        if (gazeActive)
            cuaPlayerLastGazeSeenAt = now;

        Vector2 navigation = ReadCuaPlayerNavigationVector(sc);
        bool navigationActive = IsCuaPlayerNavigationActive(navigation);
        bool triggerModifierActive = ReadCuaPlayerTriggerModifier();
        bool grabNavigateOverrideActive = ReadCuaPlayerGrabNavigateOverrideActive();
        cuaPlayerLastTriggerModifierActive = triggerModifierActive;
        float horizontal = Mathf.Abs(navigation.x) >= CuaPlayerNavigationDeadzone ? navigation.x : 0f;
        float vertical = Mathf.Abs(navigation.y) >= CuaPlayerNavigationDeadzone ? navigation.y : 0f;
        if (navigationActive)
            cuaPlayerLastInputActiveAt = now;

        string currentHostAtomUid = ResolveCuaPlayerCurrentHostAtomUid();
        UpdateCuaPlayerVisibleScreenState(currentHostAtomUid);

        if (grabNavigateOverrideActive)
        {
            ResetCuaPlayerTriggerTapState();
            if (ReferenceEquals(cuaPlayerInputOwner, this) || cuaPlayerNavigationCaptureActive || cuaPlayerFocusActive)
                ReleaseCuaPlayerInputFocus("grab_nav_override");

            cuaPlayerLastGrabNavigateOverrideActive = true;
            UpdateCuaPlayerInputState(false, gazeActive, "grab_nav", navigation, gazeReason);
            return;
        }

        cuaPlayerLastGrabNavigateOverrideActive = false;

        if (gazeActive)
            TryPreemptCuaPlayerInputOwnerForGazeTarget(currentHostAtomUid);

        bool inputActiveRecently = (now - cuaPlayerLastInputActiveAt) <= CuaPlayerFocusInputGraceSeconds;
        bool wantsFocus = (gazeActive && !navigationActive)
            || (cuaPlayerFocusActive
                && ((now - cuaPlayerLastGazeSeenAt) <= CuaPlayerFocusReleaseGraceSeconds
                    || inputActiveRecently));
        bool ownerAvailable = cuaPlayerInputOwner == null || ReferenceEquals(cuaPlayerInputOwner, this);
        bool ownsFocus = false;

        if (wantsFocus && ownerAvailable)
        {
            bool becameOwner = !ReferenceEquals(cuaPlayerInputOwner, this);
            if (becameOwner)
                cuaPlayerInputOwner = this;

            ownsFocus = true;
            cuaPlayerFocusActive = true;
            if (gazeActive)
                cuaPlayerLastGazeSeenAt = now;
            if (becameOwner || !cuaPlayerNavigationCaptureActive)
                SetInputCaptureState(true);
        }
        else if (ReferenceEquals(cuaPlayerInputOwner, this))
        {
            ReleaseCuaPlayerInputFocus(gazeActive ? "focus_blocked" : "gaze_off");
        }
        else
        {
            cuaPlayerFocusActive = false;
        }

        if (!ownsFocus)
        {
            ResetCuaPlayerTriggerTapState();
            UpdateCuaPlayerInputState(
                cuaPlayerFocusActive,
                gazeActive,
                ownerAvailable
                    ? (navigationActive ? "moving" : "idle")
                    : "waiting",
                navigation,
                gazeReason);
            return;
        }

        if (!TryResolveAttachedHostedStandalonePlayerRecord(out StandalonePlayerRecord record, out Atom hostAtom) || record == null || hostAtom == null)
        {
            ResetCuaPlayerTriggerTapState();
            ReleaseCuaPlayerInputFocus("player_unresolved");
            return;
        }

        UpdateCuaPlayerVisibleScreenState(string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim());
        TickCuaPlayerTriggerTapState(record, triggerModifierActive, navigationActive);

        if (record.mediaIsStillImage)
        {
            EndCuaPlayerVideoScrubSession(false);
            if (cuaPlayerNavigationAxisLock != CuaPlayerNavigationAxisLock.Horizontal)
                horizontal = 0f;
            cuaPlayerVideoScrubTargetKnown = false;
            TickCuaPlayerImageStepInput(record, horizontal, triggerModifierActive);
            UpdateCuaPlayerInputState(
                true,
                gazeActive,
                Mathf.Abs(horizontal) > 0f
                    ? (triggerModifierActive ? "image_step_fast" : "image_step")
                    : "focused",
                navigation,
                gazeReason);
            return;
        }

        if (cuaPlayerNavigationAxisLock != CuaPlayerNavigationAxisLock.Horizontal)
            horizontal = 0f;

        TickCuaPlayerVideoScrubInput(record, horizontal, triggerModifierActive);
        UpdateCuaPlayerInputState(
            true,
            gazeActive,
            Mathf.Abs(horizontal) > 0f
                ? (triggerModifierActive ? "video_step" : "video_skip")
                : "focused",
            navigation,
            gazeReason);
    }

    private void OnCuaPlayerInputDestroy()
    {
        ResetCuaPlayerTriggerTapState();
        UpdateCuaPlayerVisibleScreenState("");
        ReleaseCuaPlayerInputFocus("destroy");
        if (playerFocusActiveField != null)
            playerFocusActiveField.valNoCallback = false;
        if (playerInputStateField != null)
            playerInputStateField.valNoCallback = "focus=off gaze=off mode=destroyed";
        if (playerVisibleScreenField != null)
            playerVisibleScreenField.valNoCallback = "screen=none";
    }

    private void SetCuaPlayerInputCaptureState(bool enabled)
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
        {
            if (!enabled && ReferenceEquals(cuaPlayerInputOwner, this))
                cuaPlayerInputOwner = null;
            cuaPlayerNavigationCaptureActive = false;
            cuaPlayerNavigationSnapshotKnown = false;
            return;
        }

        if (enabled)
        {
            if (!cuaPlayerNavigationSnapshotKnown)
            {
                cuaPlayerNavigationSnapshot = ReadCuaPlayerNavigationSnapshot(sc);
                cuaPlayerNavigationSnapshotKnown = true;
            }

            CuaPlayerNavigationSnapshot desired = cuaPlayerNavigationSnapshot;
            desired.disableNavigation = true;
            desired.disableInternalKeyBindings = true;
            desired.disableInternalNavigationKeyBindings = true;
            desired.disableAllNavigationToggle = desired.hasDisableAllNavigationToggle;
            desired.alwaysEnablePointers = false;
            desired.rayLineLeftEnabled = false;
            desired.rayLineRightEnabled = false;
            WriteCuaPlayerNavigationSnapshot(sc, desired);
            cuaPlayerNavigationCaptureActive = true;
            return;
        }

        if (!ReferenceEquals(cuaPlayerInputOwner, this) && cuaPlayerInputOwner != null)
            return;

        if (cuaPlayerNavigationSnapshotKnown)
            WriteCuaPlayerNavigationSnapshot(sc, cuaPlayerNavigationSnapshot);

        cuaPlayerNavigationCaptureActive = false;
        cuaPlayerNavigationSnapshotKnown = false;
        if (ReferenceEquals(cuaPlayerInputOwner, this))
            cuaPlayerInputOwner = null;
    }

    private bool IsCuaPlayerInputCaptureEnabled()
    {
        return cuaPlayerNavigationCaptureActive;
    }

    private void ReleaseCuaPlayerInputFocus(string reason)
    {
        bool wasOwner = ReferenceEquals(cuaPlayerInputOwner, this);
        EndCuaPlayerVideoScrubSession(true);
        ResetCuaPlayerTriggerTapState();
        cuaPlayerFocusActive = false;
        cuaPlayerImageStepDirection = 0;
        cuaPlayerNextImageStepTime = 0f;
        cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.None;
        if (wasOwner || cuaPlayerNavigationCaptureActive)
            SetInputCaptureState(false);

        UpdateCuaPlayerInputState(false, false, "idle", Vector2.zero, reason);
    }

    private void UpdateCuaPlayerInputState(bool focusActive, bool gazeActive, string mode, Vector2 navigation, string reason)
    {
        if (playerFocusActiveField != null)
            playerFocusActiveField.valNoCallback = focusActive;

        if (playerInputStateField == null)
            return;

        StringBuilder sb = new StringBuilder(160);
        sb.Append("focus=").Append(focusActive ? "on" : "off");
        sb.Append(" gaze=").Append(gazeActive ? "on" : "off");
        sb.Append(" mode=").Append(string.IsNullOrEmpty(mode) ? "idle" : mode);
        sb.Append(" stick=").Append(string.IsNullOrEmpty(cuaPlayerLastNavigationStick) ? "none" : cuaPlayerLastNavigationStick);
        sb.Append(" src=").Append(string.IsNullOrEmpty(cuaPlayerLastNavigationSource) ? "none" : cuaPlayerLastNavigationSource);
        sb.Append(" mod=").Append(cuaPlayerLastTriggerModifierActive ? "trigger" : "none");
        sb.Append(" grab=").Append(cuaPlayerLastGrabNavigateOverrideActive ? "on" : "off");
        sb.Append(" lock=").Append(FormatCuaPlayerNavigationAxisLock(cuaPlayerNavigationAxisLock));
        sb.Append(" screen=").Append(string.IsNullOrEmpty(cuaPlayerLastVisibleScreenHostAtomUid) ? "none" : cuaPlayerLastVisibleScreenHostAtomUid);
        sb.Append(" x=").Append(navigation.x.ToString("0.00", CultureInfo.InvariantCulture));
        sb.Append(" y=").Append(navigation.y.ToString("0.00", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(reason))
            sb.Append(" note=").Append(reason);

        string summary = sb.ToString();
        float now = Time.unscaledTime;
        if (string.Equals(summary, cuaPlayerLastInputState, StringComparison.Ordinal)
            && now < cuaPlayerNextInputStateUpdateAt)
            return;

        playerInputStateField.valNoCallback = summary;
        cuaPlayerLastInputState = summary;
        cuaPlayerNextInputStateUpdateAt = now + CuaPlayerInputStateUpdateIntervalSeconds;
    }

    private void UpdateCuaPlayerVisibleScreenState(string hostAtomUid)
    {
        string normalizedHostAtomUid = string.IsNullOrEmpty(hostAtomUid) ? "" : hostAtomUid.Trim();
        if (string.Equals(cuaPlayerLastVisibleScreenHostAtomUid, normalizedHostAtomUid, StringComparison.Ordinal))
            return;

        cuaPlayerLastVisibleScreenHostAtomUid = normalizedHostAtomUid;
        if (playerVisibleScreenField == null)
            return;

        string summary = "screen=" + (string.IsNullOrEmpty(normalizedHostAtomUid) ? "none" : normalizedHostAtomUid);
        if (!string.Equals(playerVisibleScreenField.val, summary, StringComparison.Ordinal))
            playerVisibleScreenField.valNoCallback = summary;
    }

    private void TickCuaPlayerTriggerTapState(StandalonePlayerRecord record, bool triggerActive, bool navigationActive)
    {
        if (record == null || string.IsNullOrEmpty(record.playbackKey))
        {
            ResetCuaPlayerTriggerTapState();
            return;
        }

        if (triggerActive)
        {
            if (!cuaPlayerLastTriggerTapActive)
            {
                cuaPlayerTriggerTapArmed = true;
                cuaPlayerTriggerTapUsedWithNavigation = navigationActive;
            }
            else if (navigationActive)
            {
                cuaPlayerTriggerTapUsedWithNavigation = true;
            }
        }
        else if (cuaPlayerLastTriggerTapActive)
        {
            if (cuaPlayerTriggerTapArmed && !cuaPlayerTriggerTapUsedWithNavigation)
                TryApplyCuaPlayerTriggerTapAction(record);

            cuaPlayerTriggerTapArmed = false;
            cuaPlayerTriggerTapUsedWithNavigation = false;
        }

        cuaPlayerLastTriggerTapActive = triggerActive;
    }

    private void ResetCuaPlayerTriggerTapState()
    {
        cuaPlayerTriggerTapArmed = false;
        cuaPlayerTriggerTapUsedWithNavigation = false;
        cuaPlayerLastTriggerTapActive = false;
    }

    private void TryApplyCuaPlayerTriggerTapAction(StandalonePlayerRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.playbackKey))
            return;

        bool shouldPause = record.desiredPlaying;
        if (!record.mediaIsStillImage)
        {
            try
            {
                shouldPause = shouldPause || (record.videoPlayer != null && record.videoPlayer.isPlaying);
            }
            catch
            {
            }
        }

        string argsJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
        string ignoredResult;
        string errorMessage;
        if (shouldPause)
            TryPauseStandalonePlayer("Player.InputTriggerTapPause", argsJson, out ignoredResult, out errorMessage);
        else
            TryPlayStandalonePlayer("Player.InputTriggerTapPlay", argsJson, out ignoredResult, out errorMessage);
    }

    private bool TryResolveCuaPlayerGazeHit(out string reason)
    {
        reason = "";

        SuperController sc = SuperController.singleton;
        if (sc == null || sc.lookCamera == null)
        {
            reason = "look_camera_missing";
            return false;
        }

        if (!TryResolveAttachedHostedStandalonePlayerRecord(out StandalonePlayerRecord record, out Atom hostAtom) || record == null || hostAtom == null)
        {
            reason = "player_missing";
            return false;
        }

        GameObject surfaceObject = ResolveCuaPlayerScreenSurfaceObject(hostAtom, out reason);
        if (surfaceObject == null)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "screen_surface_missing";
            return false;
        }

        return TryResolveCuaPlayerGazeIntersection(surfaceObject, sc.lookCamera);
    }

    private GameObject ResolveCuaPlayerScreenSurfaceObject(Atom hostAtom, out string errorMessage)
    {
        errorMessage = "";
        if (hostAtom == null)
        {
            errorMessage = "host_atom_missing";
            return null;
        }

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (!string.IsNullOrEmpty(hostAtomUid)
            && string.Equals(cuaPlayerCachedSurfaceHostAtomUid, hostAtomUid, StringComparison.OrdinalIgnoreCase)
            && cuaPlayerCachedScreenSurfaceObject != null)
        {
            return cuaPlayerCachedScreenSurfaceObject;
        }

        HostedPlayerSurfaceContract contract;
        string contractErrorMessage;
        if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out contract, out contractErrorMessage)
            || contract == null
            || contract.screenSurfaceObject == null)
        {
            cuaPlayerCachedSurfaceHostAtomUid = hostAtomUid;
            cuaPlayerCachedScreenSurfaceObject = null;
            errorMessage = string.IsNullOrEmpty(contractErrorMessage) ? "screen_surface_missing" : contractErrorMessage;
            return null;
        }

        cuaPlayerCachedSurfaceHostAtomUid = hostAtomUid;
        cuaPlayerCachedScreenSurfaceObject = contract.screenSurfaceObject;
        return cuaPlayerCachedScreenSurfaceObject;
    }

    private bool TryResolveCuaPlayerGazeIntersection(GameObject surfaceObject, Camera lookCamera)
    {
        if (surfaceObject == null || lookCamera == null)
            return false;

        Vector3 localCenter;
        Vector3 localSize;
        if (!TryBuildInnerPieceSurfaceLocalBounds(surfaceObject, out localCenter, out localSize))
            return false;

        Transform surfaceTransform = surfaceObject.transform;
        Vector3 planeNormal = surfaceTransform.forward;
        Vector3 planePoint = surfaceTransform.TransformPoint(localCenter);
        Ray gazeRay = new Ray(lookCamera.transform.position, lookCamera.transform.forward);
        float denominator = Vector3.Dot(planeNormal, gazeRay.direction);
        if (Mathf.Abs(denominator) <= 0.0001f)
            return false;

        float distance = Vector3.Dot(planePoint - gazeRay.origin, planeNormal) / denominator;
        if (distance < CuaPlayerGazeMinDistanceMeters)
            return false;

        Vector3 worldHit = gazeRay.origin + gazeRay.direction * distance;
        Vector3 localHit = surfaceTransform.InverseTransformPoint(worldHit);
        Vector3 extents = localSize * 0.5f;

        return localHit.x >= localCenter.x - extents.x
            && localHit.x <= localCenter.x + extents.x
            && localHit.y >= localCenter.y - extents.y
            && localHit.y <= localCenter.y + extents.y;
    }

    private string ResolveCuaPlayerCurrentHostAtomUid()
    {
        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            return "";

        return string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
    }

    private void TryPreemptCuaPlayerInputOwnerForGazeTarget(string targetHostAtomUid)
    {
        if (string.IsNullOrEmpty(targetHostAtomUid))
            return;

        FASyncRuntime owner = cuaPlayerInputOwner;
        if (owner == null || ReferenceEquals(owner, this))
            return;

        string ownerHostAtomUid = owner.ResolveCuaPlayerCurrentHostAtomUid();
        if (string.Equals(targetHostAtomUid, ownerHostAtomUid, StringComparison.OrdinalIgnoreCase))
            return;

        owner.ReleaseCuaPlayerInputFocus("gaze_preempted");
    }

    private Vector2 ReadCuaPlayerNavigationVector(SuperController sc)
    {
        if (sc == null)
        {
            cuaPlayerLastNavigationSource = "none";
            cuaPlayerLastNavigationStick = "none";
            return Vector2.zero;
        }

        CuaPlayerStickRead leftRead = ReadCuaPlayerStick(
            "left",
            CuaPlayerLeftStickXCandidates,
            CuaPlayerLeftStickYCandidates,
            JoystickControl.Axis.LeftStickX,
            JoystickControl.Axis.LeftStickY);
        CuaPlayerStickRead rightRead = ReadCuaPlayerStick(
            "right",
            CuaPlayerRightStickXCandidates,
            CuaPlayerRightStickYCandidates,
            JoystickControl.Axis.RightStickX,
            JoystickControl.Axis.RightStickY);

        CuaPlayerStickRead selectedRead = SelectCuaPlayerStickRead(leftRead, rightRead);
        cuaPlayerLastNavigationStick = string.IsNullOrEmpty(selectedRead.slot) ? "none" : selectedRead.slot;
        cuaPlayerLastNavigationSource = string.IsNullOrEmpty(selectedRead.source) ? "none" : selectedRead.source;
        return ApplyCuaPlayerNavigationAxisLock(selectedRead.navigation);
    }

    private CuaPlayerStickRead ReadCuaPlayerStick(
        string slot,
        string[] rawXCandidates,
        string[] rawYCandidates,
        JoystickControl.Axis joystickXAxis,
        JoystickControl.Axis joystickYAxis)
    {
        CuaPlayerStickRead read = new CuaPlayerStickRead();
        read.slot = string.IsNullOrEmpty(slot) ? "none" : slot;
        read.navigation = Vector2.zero;
        read.source = "none";
        read.valid = false;
        read.active = false;

        Vector2 rawNavigation = Vector2.zero;
        bool rawValid = false;
        Vector2 joystickNavigation = Vector2.zero;
        bool joystickValid = false;
        try
        {
            float rawX;
            float rawY;
            bool rawXValid = TryReadCuaPlayerRawAxisCandidates(rawXCandidates, out rawX);
            bool rawYValid = TryReadCuaPlayerRawAxisCandidates(rawYCandidates, out rawY);
            if (rawXValid || rawYValid)
            {
                rawNavigation = new Vector2(rawXValid ? rawX : 0f, rawYValid ? rawY : 0f);
                rawValid = true;
            }

            float joystickX;
            float joystickY;
            bool joystickXValid = TryReadCuaPlayerJoystickAxis(joystickXAxis, out joystickX);
            bool joystickYValid = TryReadCuaPlayerJoystickAxis(joystickYAxis, out joystickY);
            if (joystickXValid || joystickYValid)
            {
                joystickNavigation = new Vector2(joystickXValid ? joystickX : 0f, joystickYValid ? joystickY : 0f);
                joystickValid = true;
            }
        }
        catch
        {
            rawNavigation = Vector2.zero;
            rawValid = false;
            joystickNavigation = Vector2.zero;
            joystickValid = false;
        }

        bool rawActive = IsCuaPlayerNavigationActive(rawNavigation);
        bool joystickActive = IsCuaPlayerNavigationActive(joystickNavigation);
        read.valid = rawValid || joystickValid;

        if (rawActive && joystickActive)
        {
            if (rawNavigation.sqrMagnitude >= joystickNavigation.sqrMagnitude)
            {
                read.navigation = rawNavigation;
                read.source = "raw";
            }
            else
            {
                read.navigation = joystickNavigation;
                read.source = "joystick";
            }
        }
        else if (rawActive)
        {
            read.navigation = rawNavigation;
            read.source = "raw";
        }
        else if (joystickActive)
        {
            read.navigation = joystickNavigation;
            read.source = "joystick";
        }
        else if (rawValid)
        {
            read.navigation = rawNavigation;
            read.source = "raw_idle";
        }
        else if (joystickValid)
        {
            read.navigation = joystickNavigation;
            read.source = "joystick_idle";
        }

        read.active = IsCuaPlayerNavigationActive(read.navigation);
        return read;
    }

    private CuaPlayerStickRead SelectCuaPlayerStickRead(CuaPlayerStickRead leftRead, CuaPlayerStickRead rightRead)
    {
        CuaPlayerStickRead selectedRead = new CuaPlayerStickRead
        {
            slot = "none",
            navigation = Vector2.zero,
            source = "none",
            valid = false,
            active = false,
        };

        if (leftRead.active && rightRead.active)
        {
            if (leftRead.navigation.sqrMagnitude > rightRead.navigation.sqrMagnitude)
                selectedRead = leftRead;
            else if (rightRead.navigation.sqrMagnitude > leftRead.navigation.sqrMagnitude)
                selectedRead = rightRead;
            else if (string.Equals(cuaPlayerLastActiveDirectStick, "left", StringComparison.Ordinal))
                selectedRead = leftRead;
            else
                selectedRead = rightRead;
        }
        else if (leftRead.active)
        {
            selectedRead = leftRead;
        }
        else if (rightRead.active)
        {
            selectedRead = rightRead;
        }
        else
        {
            float now = Time.unscaledTime;
            if ((now - cuaPlayerLastActiveDirectStickAt) <= CuaPlayerNavigationStickContinuitySeconds)
            {
                if (string.Equals(cuaPlayerLastActiveDirectStick, "left", StringComparison.Ordinal) && leftRead.valid)
                    selectedRead = leftRead;
                else if (string.Equals(cuaPlayerLastActiveDirectStick, "right", StringComparison.Ordinal) && rightRead.valid)
                    selectedRead = rightRead;
            }

            if (!selectedRead.valid)
            {
                if (rightRead.valid)
                    selectedRead = rightRead;
                else if (leftRead.valid)
                    selectedRead = leftRead;
            }
        }

        if (selectedRead.active && !string.IsNullOrEmpty(selectedRead.slot) && !string.Equals(selectedRead.slot, "none", StringComparison.Ordinal))
        {
            cuaPlayerLastActiveDirectStick = selectedRead.slot;
            cuaPlayerLastActiveDirectStickAt = Time.unscaledTime;
        }

        return selectedRead;
    }

    private static bool TryReadCuaPlayerJoystickAxis(JoystickControl.Axis axis, out float value)
    {
        value = 0f;

        try
        {
            value = JoystickControl.GetAxis(axis);
            return true;
        }
        catch
        {
            value = 0f;
            return false;
        }
    }

    private static bool TryReadCuaPlayerRawAxisCandidates(string[] candidates, out float value)
    {
        value = 0f;
        if (candidates == null)
            return false;

        bool foundAnyAxis = false;
        float bestValue = 0f;
        for (int i = 0; i < candidates.Length; i++)
        {
            float candidateValue;
            if (!TryReadCuaPlayerRawAxis(candidates[i], out candidateValue))
                continue;

            if (!foundAnyAxis || Mathf.Abs(candidateValue) > Mathf.Abs(bestValue))
            {
                bestValue = candidateValue;
                foundAnyAxis = true;
            }
        }

        value = foundAnyAxis ? bestValue : 0f;
        return foundAnyAxis;
    }

    private static bool TryReadCuaPlayerRawAxis(string axisName, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(axisName))
            return false;

        try
        {
            value = Input.GetAxis(axisName);
            return true;
        }
        catch
        {
            value = 0f;
            return false;
        }
    }

    private static bool IsCuaPlayerNavigationActive(Vector2 navigation)
    {
        return Mathf.Abs(navigation.x) >= CuaPlayerNavigationSourceActiveThreshold
            || Mathf.Abs(navigation.y) >= CuaPlayerNavigationSourceActiveThreshold;
    }

    private bool ReadCuaPlayerTriggerModifier()
    {
        float triggerValue;
        if (!TryReadCuaPlayerJoystickAxis(JoystickControl.Axis.Triggers, out triggerValue))
            return false;

        return Mathf.Abs(triggerValue) >= CuaPlayerTriggerModifierThreshold;
    }

    private bool ReadCuaPlayerGrabNavigateOverrideActive()
    {
        return ReadCuaPlayerThumbstickButtonActive(
                   OVRInput.Button.PrimaryThumbstick,
                   OVRInput.RawButton.LThumbstick)
            || ReadCuaPlayerThumbstickButtonActive(
                   OVRInput.Button.SecondaryThumbstick,
                   OVRInput.RawButton.RThumbstick);
    }

    private static bool ReadCuaPlayerThumbstickButtonActive(OVRInput.Button button, OVRInput.RawButton rawButton)
    {
        try
        {
            if (OVRInput.Get(button) || OVRInput.Get(rawButton))
                return true;
        }
        catch
        {
        }

        return false;
    }

    private Vector2 ApplyCuaPlayerNavigationAxisLock(Vector2 navigation)
    {
        float absX = Mathf.Abs(navigation.x);
        float absY = Mathf.Abs(navigation.y);
        bool xActive = absX >= CuaPlayerNavigationDeadzone;
        bool yActive = absY >= CuaPlayerNavigationDeadzone;

        if (absX <= CuaPlayerNavigationAxisReleaseDeadzone
            && absY <= CuaPlayerNavigationAxisReleaseDeadzone)
        {
            cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.None;
            return Vector2.zero;
        }

        if (cuaPlayerNavigationAxisLock == CuaPlayerNavigationAxisLock.Horizontal)
        {
            if (absX <= CuaPlayerNavigationAxisReleaseDeadzone)
            {
                cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.None;
                return Vector2.zero;
            }

            return new Vector2(navigation.x, 0f);
        }

        if (cuaPlayerNavigationAxisLock == CuaPlayerNavigationAxisLock.Vertical)
        {
            if (absY <= CuaPlayerNavigationAxisReleaseDeadzone)
            {
                cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.None;
                return Vector2.zero;
            }

            return new Vector2(0f, navigation.y);
        }

        if (xActive && (!yActive || absX >= absY))
            cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.Horizontal;
        else if (yActive)
            cuaPlayerNavigationAxisLock = CuaPlayerNavigationAxisLock.Vertical;

        if (cuaPlayerNavigationAxisLock == CuaPlayerNavigationAxisLock.Horizontal)
            return new Vector2(navigation.x, 0f);

        if (cuaPlayerNavigationAxisLock == CuaPlayerNavigationAxisLock.Vertical)
            return new Vector2(0f, navigation.y);

        return Vector2.zero;
    }

    private string FormatCuaPlayerNavigationAxisLock(CuaPlayerNavigationAxisLock axisLock)
    {
        switch (axisLock)
        {
            case CuaPlayerNavigationAxisLock.Horizontal:
                return "horizontal";
            case CuaPlayerNavigationAxisLock.Vertical:
                return "vertical";
            default:
                return "none";
        }
    }

    private void TickCuaPlayerVideoScrubInput(StandalonePlayerRecord record, float horizontal, bool triggerModifierActive)
    {
        if (record == null || record.mediaIsStillImage)
        {
            EndCuaPlayerVideoScrubSession(false);
            ResetCuaPlayerStepRepeatState();
            return;
        }

        if (triggerModifierActive)
        {
            EndCuaPlayerVideoScrubSession(true);
            TickCuaPlayerStepInput(
                record,
                horizontal,
                CuaPlayerImageStepInitialRepeatSeconds,
                CuaPlayerImageStepRepeatSeconds,
                "Player.InputVideoNext",
                "Player.InputVideoPrevious");
            return;
        }

        EndCuaPlayerVideoScrubSession(true);
        TickCuaPlayerStepInput(
            record,
            horizontal,
            CuaPlayerImageStepInitialRepeatSeconds,
            CuaPlayerImageStepRepeatSeconds,
            PlayerActionSkipForwardId,
            PlayerActionSkipBackwardId);
    }

    private void BeginCuaPlayerVideoScrubSession(StandalonePlayerRecord record)
    {
        if (record == null || record.mediaIsStillImage || string.IsNullOrEmpty(record.playbackKey))
            return;

        if (cuaPlayerVideoScrubSessionActive
            && string.Equals(cuaPlayerVideoScrubPlaybackKey, record.playbackKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EndCuaPlayerVideoScrubSession(false);
        cuaPlayerVideoScrubSessionActive = true;
        cuaPlayerVideoScrubPlaybackKey = record.playbackKey;
        cuaPlayerVideoScrubResumeAfterRelease = record.desiredPlaying;
        if (!cuaPlayerVideoScrubResumeAfterRelease)
            return;

        string argsJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
        string ignoredResult;
        string errorMessage;
        if (!TryPauseStandalonePlayer("Player.InputScrubBegin", argsJson, out ignoredResult, out errorMessage))
            cuaPlayerVideoScrubResumeAfterRelease = false;
    }

    private void EndCuaPlayerVideoScrubSession(bool resumePlayback)
    {
        string playbackKey = cuaPlayerVideoScrubPlaybackKey;
        bool targetKnown = cuaPlayerVideoScrubTargetKnown;
        float targetNormalized = cuaPlayerVideoScrubTargetNormalized;
        bool shouldResumePlayback = resumePlayback && cuaPlayerVideoScrubResumeAfterRelease;

        cuaPlayerVideoScrubSessionActive = false;
        cuaPlayerVideoScrubResumeAfterRelease = false;
        cuaPlayerVideoScrubPlaybackKey = "";
        cuaPlayerVideoScrubTargetKnown = false;
        cuaPlayerVideoScrubTargetNormalized = 0f;

        if (string.IsNullOrEmpty(playbackKey))
            return;

        if (targetKnown)
        {
            string seekArgsJson = "{\"playbackKey\":\""
                + EscapeJsonString(playbackKey)
                + "\",\"normalized\":"
                + FormatFloat(targetNormalized)
                + "}";
            string ignoredSeekResult;
            string seekErrorMessage;
            TrySeekStandalonePlayerNormalized("Player.InputScrubFinalize", seekArgsJson, out ignoredSeekResult, out seekErrorMessage);
        }

        if (!shouldResumePlayback)
            return;

        string playArgsJson = "{\"playbackKey\":\"" + EscapeJsonString(playbackKey) + "\"}";
        string ignoredPlayResult;
        string playErrorMessage;
        TryPlayStandalonePlayer("Player.InputScrubResume", playArgsJson, out ignoredPlayResult, out playErrorMessage);
    }

    private void TickCuaPlayerImageStepInput(StandalonePlayerRecord record, float horizontal, bool triggerModifierActive)
    {
        if (record == null || !record.mediaIsStillImage)
            return;

        float initialRepeatSeconds = triggerModifierActive
            ? (CuaPlayerImageStepInitialRepeatSeconds * 0.5f)
            : CuaPlayerImageStepInitialRepeatSeconds;
        float repeatSeconds = triggerModifierActive
            ? (CuaPlayerImageStepRepeatSeconds * 0.5f)
            : CuaPlayerImageStepRepeatSeconds;

        TickCuaPlayerStepInput(
            record,
            horizontal,
            initialRepeatSeconds,
            repeatSeconds,
            "Player.InputImageNext",
            "Player.InputImagePrevious");
    }

    private void TickCuaPlayerStepInput(
        StandalonePlayerRecord record,
        float horizontal,
        float initialRepeatSeconds,
        float repeatSeconds,
        string nextActionId,
        string previousActionId)
    {
        if (record == null)
            return;

        int desiredDirection = 0;
        if (horizontal >= CuaPlayerNavigationDeadzone)
            desiredDirection = 1;
        else if (horizontal <= -CuaPlayerNavigationDeadzone)
            desiredDirection = -1;

        if (desiredDirection == 0)
        {
            cuaPlayerImageStepDirection = 0;
            cuaPlayerNextImageStepTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        int previousDirection = cuaPlayerImageStepDirection;
        bool directionChanged = desiredDirection != previousDirection;
        if (!directionChanged && now < cuaPlayerNextImageStepTime)
            return;

        string argsJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
        string ignoredResult;
        string errorMessage;
        bool ok;
        if (desiredDirection > 0)
        {
            ok = string.Equals(nextActionId, PlayerActionSkipForwardId, StringComparison.Ordinal)
                ? TrySkipForwardStandalonePlayer(nextActionId, argsJson, out ignoredResult, out errorMessage)
                : TryNextStandalonePlayer(nextActionId, argsJson, out ignoredResult, out errorMessage);
        }
        else
        {
            ok = string.Equals(previousActionId, PlayerActionSkipBackwardId, StringComparison.Ordinal)
                ? TrySkipBackwardStandalonePlayer(previousActionId, argsJson, out ignoredResult, out errorMessage)
                : TryPreviousStandalonePlayer(previousActionId, argsJson, out ignoredResult, out errorMessage);
        }

        if (!ok)
            return;

        cuaPlayerImageStepDirection = desiredDirection;
        cuaPlayerNextImageStepTime = now + (directionChanged
            ? initialRepeatSeconds
            : repeatSeconds);
        RefreshVisiblePlayerDebugFields();
    }

    private void ResetCuaPlayerStepRepeatState()
    {
        cuaPlayerImageStepDirection = 0;
        cuaPlayerNextImageStepTime = 0f;
    }

    private CuaPlayerNavigationSnapshot ReadCuaPlayerNavigationSnapshot(SuperController sc)
    {
        CuaPlayerNavigationSnapshot snapshot = new CuaPlayerNavigationSnapshot();
        snapshot.disableNavigation = SafeReadBool(delegate { return sc.disableNavigation; });
        snapshot.disableInternalKeyBindings = SafeReadBool(delegate { return sc.disableInternalKeyBindings; });
        snapshot.disableInternalNavigationKeyBindings = SafeReadBool(delegate { return sc.disableInternalNavigationKeyBindings; });
        snapshot.hasDisableAllNavigationToggle = sc.disableAllNavigationToggle != null;
        snapshot.disableAllNavigationToggle = snapshot.hasDisableAllNavigationToggle && sc.disableAllNavigationToggle.isOn;
        snapshot.hasDisableGrabNavigationToggle = sc.disableGrabNavigationToggle != null;
        snapshot.disableGrabNavigationToggle = snapshot.hasDisableGrabNavigationToggle && sc.disableGrabNavigationToggle.isOn;
        snapshot.alwaysEnablePointers = SafeReadBool(delegate { return sc.alwaysEnablePointers; });
        snapshot.hasRayLineLeft = sc.rayLineLeft != null;
        snapshot.rayLineLeftEnabled = snapshot.hasRayLineLeft && SafeReadBool(delegate { return sc.rayLineLeft.enabled; });
        snapshot.hasRayLineRight = sc.rayLineRight != null;
        snapshot.rayLineRightEnabled = snapshot.hasRayLineRight && SafeReadBool(delegate { return sc.rayLineRight.enabled; });
        return snapshot;
    }

    private void WriteCuaPlayerNavigationSnapshot(SuperController sc, CuaPlayerNavigationSnapshot snapshot)
    {
        sc.disableNavigation = snapshot.disableNavigation;
        sc.disableInternalKeyBindings = snapshot.disableInternalKeyBindings;
        sc.disableInternalNavigationKeyBindings = snapshot.disableInternalNavigationKeyBindings;
        sc.alwaysEnablePointers = snapshot.alwaysEnablePointers;
        if (sc.disableAllNavigationToggle != null && snapshot.hasDisableAllNavigationToggle)
            sc.disableAllNavigationToggle.isOn = snapshot.disableAllNavigationToggle;
        if (sc.disableGrabNavigationToggle != null && snapshot.hasDisableGrabNavigationToggle)
            sc.disableGrabNavigationToggle.isOn = snapshot.disableGrabNavigationToggle;
        if (sc.rayLineLeft != null && snapshot.hasRayLineLeft)
            sc.rayLineLeft.enabled = snapshot.rayLineLeftEnabled;
        if (sc.rayLineRight != null && snapshot.hasRayLineRight)
            sc.rayLineRight.enabled = snapshot.rayLineRightEnabled;
    }
#else
    private void BuildCuaPlayerInputStorables() { }
    private void RegisterCuaPlayerInputStorables() { }
    private void BuildCuaPlayerInputUi() { }
    private void TickCuaPlayerInput() { }
    private void OnCuaPlayerInputDestroy() { }
    private void SetCuaPlayerInputCaptureState(bool enabled) { }
    private bool IsCuaPlayerInputCaptureEnabled() { return false; }
#endif
}
