using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Valve.VR;

public partial class FASyncRuntime : MVRScript
{
#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
    private const float CuaPlayerFocusReleaseGraceSeconds = 0.20f;
    private const float CuaPlayerFocusInputGraceSeconds = 0.45f;
    private const float CuaPlayerNavigationDeadzone = 0.32f;
    private const float CuaPlayerVideoScrubNormalizedPerSecond = 0.40f;
    private const float CuaPlayerVideoSeekApplyIntervalSeconds = 0.08f;
    private const float CuaPlayerImageStepInitialRepeatSeconds = 0.35f;
    private const float CuaPlayerImageStepRepeatSeconds = 0.22f;
    private const float CuaPlayerGazeMinDistanceMeters = 0.05f;
    private const float CuaPlayerInputStateUpdateIntervalSeconds = 0.10f;
    private const float CuaPlayerSurfaceResolveIntervalSeconds = 0.15f;

    private struct CuaPlayerNavigationSnapshot
    {
        public bool disableNavigation;
        public bool disableInternalKeyBindings;
        public bool disableInternalNavigationKeyBindings;
        public bool hasDisableAllNavigationToggle;
        public bool disableAllNavigationToggle;
        public bool hasDisableGrabNavigationToggle;
        public bool disableGrabNavigationToggle;
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
    private bool cuaPlayerVideoScrubTargetKnown = false;
    private float cuaPlayerVideoScrubTargetNormalized = 0f;
    private float cuaPlayerNextVideoSeekApplyAt = 0f;
    private float cuaPlayerNextInputStateUpdateAt = 0f;
    private string cuaPlayerLastInputState = "";
    private string cuaPlayerCachedSurfaceHostAtomUid = "";
    private GameObject cuaPlayerCachedScreenSurfaceObject;
    private float cuaPlayerNextSurfaceResolveAt = 0f;

    private void BuildCuaPlayerInputStorables()
    {
        playerInputStateField = new JSONStorableString(
            "FrameAngel Player Input",
            "focus=off gaze=off mode=idle");
        ConfigureTransientField(playerInputStateField, false);

        playerFocusActiveField = new JSONStorableBool(
            "FrameAngel Player Focus Active",
            false);
        ConfigureTransientField(playerFocusActiveField, true);
    }

    private void RegisterCuaPlayerInputStorables()
    {
        RegisterString(playerInputStateField);
        RegisterBool(playerFocusActiveField);
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
            ReleaseCuaPlayerInputFocus("no_supercontroller");
            return;
        }

        bool gazeActive = TryResolveCuaPlayerGazeHit(out string gazeReason);
        float now = Time.unscaledTime;
        if (gazeActive)
            cuaPlayerLastGazeSeenAt = now;

        Vector2 navigation = ReadCuaPlayerNavigationVector(sc);
        float horizontal = Mathf.Abs(navigation.x) >= CuaPlayerNavigationDeadzone ? navigation.x : 0f;
        if (Mathf.Abs(horizontal) > 0f)
            cuaPlayerLastInputActiveAt = now;

        bool inputActiveRecently = (now - cuaPlayerLastInputActiveAt) <= CuaPlayerFocusInputGraceSeconds;
        bool wantsFocus = gazeActive
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
            UpdateCuaPlayerInputState(
                cuaPlayerFocusActive,
                gazeActive,
                ownerAvailable ? "idle" : "waiting",
                navigation,
                gazeReason);
            return;
        }

        if (!TryResolveAttachedHostedStandalonePlayerRecord(out StandalonePlayerRecord record, out Atom hostAtom) || record == null || hostAtom == null)
        {
            ReleaseCuaPlayerInputFocus("player_unresolved");
            return;
        }

        if (record.mediaIsStillImage)
        {
            cuaPlayerVideoScrubTargetKnown = false;
            cuaPlayerNextVideoSeekApplyAt = 0f;
            TickCuaPlayerImageStepInput(record, horizontal);
            UpdateCuaPlayerInputState(true, gazeActive, "image_step", navigation, gazeReason);
            return;
        }

        TickCuaPlayerVideoScrubInput(record, horizontal);
        UpdateCuaPlayerInputState(true, gazeActive, Mathf.Abs(horizontal) > 0f ? "video_scrub" : "focused", navigation, gazeReason);
    }

    private void OnCuaPlayerInputDestroy()
    {
        ReleaseCuaPlayerInputFocus("destroy");
        if (playerFocusActiveField != null)
            playerFocusActiveField.valNoCallback = false;
        if (playerInputStateField != null)
            playerInputStateField.valNoCallback = "focus=off gaze=off mode=destroyed";
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
            desired.disableGrabNavigationToggle = desired.hasDisableGrabNavigationToggle;
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
        cuaPlayerFocusActive = false;
        cuaPlayerImageStepDirection = 0;
        cuaPlayerNextImageStepTime = 0f;
        cuaPlayerVideoScrubTargetKnown = false;
        cuaPlayerVideoScrubTargetNormalized = 0f;
        cuaPlayerNextVideoSeekApplyAt = 0f;
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
        sb.Append(" axis=").Append(navigation.x.ToString("0.00", CultureInfo.InvariantCulture));
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
        float now = Time.unscaledTime;
        if (!string.IsNullOrEmpty(hostAtomUid)
            && string.Equals(cuaPlayerCachedSurfaceHostAtomUid, hostAtomUid, StringComparison.OrdinalIgnoreCase)
            && cuaPlayerCachedScreenSurfaceObject != null)
        {
            return cuaPlayerCachedScreenSurfaceObject;
        }

        if (now < cuaPlayerNextSurfaceResolveAt
            && !string.IsNullOrEmpty(hostAtomUid)
            && string.Equals(cuaPlayerCachedSurfaceHostAtomUid, hostAtomUid, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "screen_surface_pending";
            return null;
        }

        HostedPlayerSurfaceContract contract;
        string contractErrorMessage;
        if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out contract, out contractErrorMessage)
            || contract == null
            || contract.screenSurfaceObject == null)
        {
            cuaPlayerCachedSurfaceHostAtomUid = hostAtomUid;
            cuaPlayerCachedScreenSurfaceObject = null;
            cuaPlayerNextSurfaceResolveAt = now + CuaPlayerSurfaceResolveIntervalSeconds;
            errorMessage = string.IsNullOrEmpty(contractErrorMessage) ? "screen_surface_missing" : contractErrorMessage;
            return null;
        }

        cuaPlayerCachedSurfaceHostAtomUid = hostAtomUid;
        cuaPlayerCachedScreenSurfaceObject = contract.screenSurfaceObject;
        cuaPlayerNextSurfaceResolveAt = now + CuaPlayerSurfaceResolveIntervalSeconds;
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

    private Vector2 ReadCuaPlayerNavigationVector(SuperController sc)
    {
        if (sc == null)
            return Vector2.zero;

        Vector2 navigation = Vector2.zero;
        try
        {
            SteamVR_Action_Vector2 moveAction = sc.freeMoveAction;
            if (moveAction != null)
            {
                Vector4 raw = sc.GetFreeNavigateVector(moveAction, true);
                Vector2 left = new Vector2(raw.x, raw.y);
                Vector2 right = new Vector2(raw.z, raw.w);
                navigation = left.sqrMagnitude >= right.sqrMagnitude ? left : right;
            }
        }
        catch
        {
            navigation = Vector2.zero;
        }

        return navigation;
    }

    private void TickCuaPlayerVideoScrubInput(StandalonePlayerRecord record, float horizontal)
    {
        if (record == null || record.mediaIsStillImage)
            return;

        if (Mathf.Abs(horizontal) <= 0f)
        {
            cuaPlayerVideoScrubTargetKnown = false;
            cuaPlayerNextVideoSeekApplyAt = 0f;
            return;
        }

        double currentTimeSeconds;
        double durationSeconds;
        string errorMessage;
        if (!TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out errorMessage))
            return;

        if (durationSeconds <= 0.0001d)
            return;

        if (!cuaPlayerVideoScrubTargetKnown)
        {
            cuaPlayerVideoScrubTargetNormalized = Mathf.Clamp01((float)(currentTimeSeconds / durationSeconds));
            cuaPlayerVideoScrubTargetKnown = true;
        }

        cuaPlayerVideoScrubTargetNormalized = Mathf.Clamp01(
            cuaPlayerVideoScrubTargetNormalized
            + (horizontal * CuaPlayerVideoScrubNormalizedPerSecond * Time.unscaledDeltaTime));

        if (Time.unscaledTime < cuaPlayerNextVideoSeekApplyAt)
            return;

        string argsJson = "{\"playbackKey\":\""
            + EscapeJsonString(record.playbackKey)
            + "\",\"normalized\":"
            + FormatFloat(cuaPlayerVideoScrubTargetNormalized)
            + "}";

        string ignoredResult;
        if (TrySeekStandalonePlayerNormalized("Player.InputScrub", argsJson, out ignoredResult, out errorMessage))
        {
            cuaPlayerNextVideoSeekApplyAt = Time.unscaledTime + CuaPlayerVideoSeekApplyIntervalSeconds;
            ArmStandalonePlayerScrubFieldSyncHoldoff();
            RefreshVisiblePlayerDebugFields();
        }
    }

    private void TickCuaPlayerImageStepInput(StandalonePlayerRecord record, float horizontal)
    {
        if (record == null || !record.mediaIsStillImage)
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
        bool ok = desiredDirection > 0
            ? TryNextStandalonePlayer("Player.InputImageNext", argsJson, out ignoredResult, out errorMessage)
            : TryPreviousStandalonePlayer("Player.InputImagePrevious", argsJson, out ignoredResult, out errorMessage);

        if (!ok)
            return;

        cuaPlayerImageStepDirection = desiredDirection;
        cuaPlayerNextImageStepTime = now + (directionChanged
            ? CuaPlayerImageStepInitialRepeatSeconds
            : CuaPlayerImageStepRepeatSeconds);
        RefreshVisiblePlayerDebugFields();
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
        return snapshot;
    }

    private void WriteCuaPlayerNavigationSnapshot(SuperController sc, CuaPlayerNavigationSnapshot snapshot)
    {
        sc.disableNavigation = snapshot.disableNavigation;
        sc.disableInternalKeyBindings = snapshot.disableInternalKeyBindings;
        sc.disableInternalNavigationKeyBindings = snapshot.disableInternalNavigationKeyBindings;
        if (sc.disableAllNavigationToggle != null && snapshot.hasDisableAllNavigationToggle)
            sc.disableAllNavigationToggle.isOn = snapshot.disableAllNavigationToggle;
        if (sc.disableGrabNavigationToggle != null && snapshot.hasDisableGrabNavigationToggle)
            sc.disableGrabNavigationToggle.isOn = snapshot.disableGrabNavigationToggle;
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
