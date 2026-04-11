using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FrameAngel.Runtime.Shared;
using MVR.FileManagementSecure;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public partial class FASyncRuntime : MVRScript
{
    private const string PlayerStateSchemaVersion = "session_player_state_v1";
    private const string PlayerReceiptSchemaVersion = "session_player_receipt_v1";
    private const string StandalonePlayerStateSchemaVersion = "standalone_player_state_v1";
    private const string PlayerActionName = "FA.Player.ExecuteCommand";
    private const string PlayerActionLoadPathId = "Player.LoadPath";
    private const string PlayerActionPlayId = "Player.Play";
    private const string PlayerActionPauseId = "Player.Pause";
    private const string PlayerActionSeekNormalizedId = "Player.SeekNormalized";
    private const string PlayerActionNextId = "Player.Next";
    private const string PlayerActionPreviousId = "Player.Previous";
    private const string PlayerActionSkipForwardId = "Player.SkipForward";
    private const string PlayerActionSkipBackwardId = "Player.SkipBackward";
    private const string PlayerActionSetVolumeId = "Player.SetVolume";
    private const string PlayerActionSetMuteId = "Player.SetMute";
    private const string PlayerActionSetLoopModeId = "Player.SetLoopMode";
    private const string PlayerActionSetRandomId = "Player.SetRandom";
    private const string PlayerActionSetAbLoopStartId = "Player.SetABLoopStart";
    private const string PlayerActionSetAbLoopEndId = "Player.SetABLoopEnd";
    private const string PlayerActionSetAbLoopEnabledId = "Player.SetABLoopEnabled";
    private const string PlayerActionClearAbLoopId = "Player.ClearABLoop";
    private const string PlayerActionSetAspectModeId = "Player.SetAspectMode";
    private const string PlayerActionSetDisplaySizeId = "Player.SetDisplaySize";
    private const string PlayerActionBindControlSurfaceId = "Player.BindControlSurface";
    private const string PlayerActionTriggerControlSurfaceElementId = "Player.TriggerControlSurfaceElement";
    private const string PlayerActionLayoutControlSurfaceRelativeId = "Player.LayoutControlSurfaceRelative";
    private const string PlayerStateFieldName = "FA Player Session State Json";
    private const string PlayerCommandFieldName = "FA Player Session Command Json";
    private const string PlayerLastReceiptFieldName = "FA Player Session Last Receipt";
    private const string GhostScreenRoundedFixedResourceId = "ghost_prototype_screen_only_66de1ff3432e";
    private const string GhostScreenRectFixedResourceId = "ghost_prototype_screen_rect_f6b49ee33cdb";
    private const string GhostScreenRectShellId = "ghost_prototype_screen_rect";
    private const string GhostScreenAspectModeCrop = "crop";
    private const string GhostScreenAspectModeFit = "fit";
    private const string GhostScreenAspectModeFullWidth = "full_width";
    private const string GhostScreenAspectModeStretch = "stretch";
    private const string StandalonePlayerBindingAtomUidPrefix = "standalone_player:";
    private const string PlayerControlSurfaceBindingSchemaVersion = "session_player_control_surface_binding_v1";
    private const string PlayerControlSurfaceReceiptSchemaVersion = "session_player_control_surface_receipt_v1";
    private const string PlayerLoopModeNone = "none";
    private const string PlayerLoopModeSingle = "single";
    private const string PlayerLoopModePlaylist = "playlist";
    private const float StandalonePlayerDefaultSkipSeconds = 10f;
    private const float StandalonePlayerPlaybackRetryIntervalSeconds = 0.20f;
    private const float StandalonePlayerPlaybackStoppedGraceSeconds = 0.35f;
    private const float StandalonePlayerPrepareTimeoutSeconds = 8f;
    private const float StandalonePlayerScrubDisplayHoldoffSeconds = 0.40f;
    private const float StandalonePlayerScrubCommitDebounceSeconds = 0.18f;
    private const float StandalonePlayerVolumeCurveExponent = 2f;
    private const double StandalonePlayerPlaybackMotionEpsilonSeconds = 0.01d;
    private const double StandalonePlayerPlaybackEndThresholdSeconds = 0.05d;
    private const double StandalonePlayerAbLoopMinimumSpanSeconds = 0.05d;
    private const float PlayerControlSurfaceRelativeLayoutCheckIntervalSeconds = 0.25f;
    private const int StandalonePlayerRandomHistoryLimit = 64;

    private sealed class PlayerScreenBindingRecord
    {
        public string atomUid = "";
        public string instanceId = "";
        public string slotId = "";
        public string displayId = "";
        public string screenBindingMode = "";
        public string screenContractVersion = "";
        public string disconnectStateId = "";
        public string surfaceTargetId = "player:screen";
        public string embeddedHostAtomUid = "";
        public string debugJson = "{}";
        public string aspectMode = GhostScreenAspectModeCrop;
        public Renderer[] screenSurfaceRenderers = new Renderer[0];
        public Material[][] originalSurfaceMaterials = new Material[0][];
        public Material[][] appliedSurfaceMaterials = new Material[0][];
        public GameObject runtimeMediaSurfaceObject;
        public Renderer runtimeMediaSurfaceRenderer;
        public Renderer[] hiddenShellRenderers = new Renderer[0];
        public bool[] hiddenShellRendererStates = new bool[0];
        public Transform backdropTransform;
        public Vector3 backdropOriginalLocalPosition = Vector3.zero;
        public Quaternion backdropOriginalLocalRotation = Quaternion.identity;
        public Vector3 backdropOriginalLocalScale = Vector3.one;
        public bool backdropTransformCaptured = false;
        public Renderer[] backdropRenderers = new Renderer[0];
        public bool[] backdropRendererStates = new bool[0];
    }

    private sealed class ProjectedMaterialCandidate
    {
        public Material material;
        public int score;
        public string rendererName = "";
    }

    private sealed class PlayerControlSurfaceBindingRecord
    {
        public string controlSurfaceInstanceId = "";
        public string controlSurfaceResourceId = "";
        public string controlSurfaceId = "";
        public string controlFamilyId = "";
        public string controlThemeId = "";
        public string controlThemeLabel = "";
        public string controlThemeVariantId = "";
        public string toolkitCategory = "";
        public string sourcePrefabAssetPath = "";
        public string targetDisplayId = "";
        public string targetKind = "";
        public string atomUid = "";
        public string playbackKey = "";
        public string targetInstanceId = "";
        public string targetSlotId = "";
        public string boundAtUtc = "";
        public bool matchedCurrentScreenBinding = false;
        public string matchedScreenInstanceId = "";
        public string matchedScreenSlotId = "";
        public string lastElementId = "";
        public string lastActionId = "";
        public string lastReceiptJson = "{}";
        public float nextRelativeLayoutCheckTime = 0f;
        public bool hostedPanelPoseCaptured = false;
        public Vector3 hostedPanelLocalPosition = Vector3.zero;
        public Quaternion hostedPanelLocalRotation = Quaternion.identity;
        public bool hostedFollowCleared = false;
        public bool hostedFollowBound = false;
    }

    // Standalone player is the sandbox path for the future standalone device player core:
    // plugin-owned playback state, plugin-owned VideoPlayer/RenderTexture, and no
    // dependency on the older ImagePanel product architecture.
    private sealed class StandalonePlayerRecord
    {
        public string playbackKey = "";
        public string instanceId = "";
        public string slotId = "";
        public string displayId = "";
        public string mediaPath = "";
        public string resolvedMediaPath = "";
        public string aspectMode = GhostScreenAspectModeFit;
        public readonly List<string> playlistPaths = new List<string>();
        public readonly List<string> randomHistoryPaths = new List<string>();
        public readonly List<int> randomOrderIndices = new List<int>();
        public int randomOrderCursor = -1;
        public int currentIndex = -1;
        public string loopMode = PlayerLoopModePlaylist;
        public bool randomEnabled = true;
        public bool looping = false;
        public bool prepared = false;
        public bool preparePending = false;
        public float prepareStartedAt = 0f;
        public bool desiredPlaying = true;
        public float nextPlaybackStateApplyTime = 0f;
        public bool muted = false;
        public float volume = 1f;
        public float storedVolume = 1f;
        public string lastError = "";
        public int textureWidth = 0;
        public int textureHeight = 0;
        public bool needsScreenRefresh = false;
        public string resizeBehavior = "instant";
        public string resizeAnchor = "bottom_anchor";
        public float targetDisplayWidthMeters = 0f;
        public float targetDisplayHeightMeters = 0f;
        public bool resizeInFlight = false;
        public float resizeProgressNormalized = 1f;
        public float resizeStartedAt = 0f;
        public float resizeDurationSeconds = 0f;
        public bool hasObservedPlaybackTime = false;
        public double lastObservedPlaybackTimeSeconds = 0d;
        public float lastPlaybackMotionObservedAt = 0f;
        public bool naturalEndHandled = false;
        public bool hasAbLoopStart = false;
        public double abLoopStartSeconds = 0d;
        public bool hasAbLoopEnd = false;
        public double abLoopEndSeconds = 0d;
        public bool abLoopEnabled = false;
        public bool runtimeErrorHooked = false;
        public bool runtimeLoopPointHooked = false;
        public GameObject runtimeObject;
        public AudioSource audioSource;
        public VideoPlayer videoPlayer;
        public RenderTexture renderTexture;
        public Texture2D imageTexture;
        public bool mediaIsStillImage = false;
        public PlayerScreenBindingRecord binding;
        public Coroutine resizeCoroutine;
    }

    private readonly Dictionary<string, PlayerScreenBindingRecord> playerScreenBindings =
        new Dictionary<string, PlayerScreenBindingRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlayerControlSurfaceBindingRecord> playerControlSurfaceBindings =
        new Dictionary<string, PlayerControlSurfaceBindingRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StandalonePlayerRecord> standalonePlayerRecords =
        new Dictionary<string, StandalonePlayerRecord>(StringComparer.OrdinalIgnoreCase);

    private bool TryGetPlayerState(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        errorMessage = "";
        string stateJson = BuildSelectedPlayerStateJson(argsJson);
        resultJson = BuildBrokerResult(true, "player_state ok", stateJson);
        EmitRuntimeEvent(
            "player_state",
            actionId,
            "ok",
            "",
            "player_state ok",
            ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid"),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            stateJson
        );
        return true;
    }

    private bool TryLoadPlayerPath(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return ExecutePlayerMutation(actionId, argsJson, "player_load_path", "load_path", out resultJson, out errorMessage);
    }

    private bool TryEnsurePlayerHost(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string atomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        if (string.IsNullOrEmpty(atomUid))
        {
            errorMessage = "player atom uid not resolved";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Atom atom;
        if (!TryFindSceneAtomByUid(atomUid, out atom) || atom == null)
        {
            errorMessage = "player host atom not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string stateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}");
        string payload = BuildPlayerReceiptPayload(actionId, "player_ensure_host", stateJson, "{}");
        resultJson = BuildBrokerResult(true, "player_ensure_host", payload);
        EmitRuntimeEvent(
            "player_host_ready",
            actionId,
            "ok",
            "",
            "player_ensure_host",
            atomUid,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryPlayPlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return ExecutePlayerMutation(actionId, argsJson, "player_play", "play", out resultJson, out errorMessage);
    }

    private bool TryPausePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return ExecutePlayerMutation(actionId, argsJson, "player_pause", "pause", out resultJson, out errorMessage);
    }

    private bool TrySeekPlayerNormalized(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return ExecutePlayerMutation(actionId, argsJson, "player_seek_normalized", "seek_normalized", out resultJson, out errorMessage);
    }

    private static bool IsStandalonePlayerConsumerId(string consumerId)
    {
        return string.Equals(consumerId, "player", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandalonePlayerTargetKind(string targetKind)
    {
        return string.Equals(targetKind, "player", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryExecutePlayerAction(string actionId, string argsJson, out string resultJson, out string errorMessage, out bool handled)
    {
        handled = true;

        switch (actionId)
        {
            case "Player.GetState":
                return HasStandalonePlayerSelector(argsJson)
                    ? TryGetStandalonePlayerState(actionId, argsJson, out resultJson, out errorMessage)
                    : TryGetPlayerState(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.LoadPath":
                return HasStandalonePlayerSelector(argsJson)
                    ? TryLoadStandalonePlayerPath(actionId, argsJson, out resultJson, out errorMessage)
                    : TryLoadPlayerPath(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.EnsureHost":
                return TryEnsurePlayerHost(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.Play":
                return HasStandalonePlayerSelector(argsJson)
                    ? TryPlayStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage)
                    : TryPlayPlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.Pause":
                return HasStandalonePlayerSelector(argsJson)
                    ? TryPauseStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage)
                    : TryPausePlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SeekToSeconds":
                return TrySeekStandalonePlayerToSeconds(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SeekNormalized":
                return HasStandalonePlayerSelector(argsJson)
                    ? TrySeekStandalonePlayerNormalized(actionId, argsJson, out resultJson, out errorMessage)
                    : TrySeekPlayerNormalized(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.Next":
                return TryNextStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.Previous":
                return TryPreviousStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SkipBySeconds":
                return TrySkipStandalonePlayerBySeconds(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SkipPlus":
            case "Player.SkipForward":
                return TrySkipForwardStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SkipMinus":
            case "Player.SkipBackward":
                return TrySkipBackwardStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetPlaylist":
                return TrySetStandalonePlayerPlaylist(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetVolume":
                return TrySetStandalonePlayerVolume(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetMute":
                return TrySetStandalonePlayerMute(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetLoopMode":
                return TrySetStandalonePlayerLoopMode(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetRandom":
                return TrySetStandalonePlayerRandom(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetABLoopStart":
                return TrySetStandalonePlayerAbLoopStart(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetABLoopEnd":
                return TrySetStandalonePlayerAbLoopEnd(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetABLoopEnabled":
                return TrySetStandalonePlayerAbLoopEnabled(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.ClearABLoop":
                return TryClearStandalonePlayerAbLoop(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetAspectMode":
                return TrySetStandalonePlayerAspectMode(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.SetDisplaySize":
                return TrySetStandalonePlayerDisplaySize(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.BindScreen":
                return TryBindPlayerScreen(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.BindControlSurface":
                return TryBindPlayerControlSurface(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.GetControlSurfaceBinding":
                return TryGetPlayerControlSurfaceBinding(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.TriggerControlSurfaceElement":
                return TryTriggerPlayerControlSurfaceElement(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.LayoutControlSurfaceRelative":
                return TryLayoutPlayerControlSurfaceRelative(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.ClearScreenBinding":
                return TryClearPlayerScreenBinding(actionId, argsJson, out resultJson, out errorMessage);
            case "Player.Clear":
                return TryClearStandalonePlayer(actionId, argsJson, out resultJson, out errorMessage);
#if !FRAMEANGEL_CUA_PLAYER
            if (TryExecuteSessionPlayerAliasAction(actionId, argsJson, out resultJson, out errorMessage, out handled))
                return true;
            if (handled)
                return false;
#endif
            default:
                handled = false;
                resultJson = "{}";
                errorMessage = "";
                return false;
        }
    }

    private bool TryBindPlayerScreen(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";
        string bindStage = "resolve_host_atom_uid";
        JSONStorable consumer = null;
        string atomUid = "";
        string instanceId = "";
        string slotId = "";
        InnerPieceInstanceRecord instance = null;
        InnerPieceScreenSlotRuntimeRecord slot = null;
        FAInnerPiecePlaneData plane = new FAInnerPiecePlaneData();
        PlayerScreenBindingRecord previousBinding = null;
        bool hadPreviousBinding = false;
        bool sameBinding = false;
        string consumerReceiptJson = "{}";
        string stateJson = "{}";
        string helperHostAtomUid = "";
        string screenDebugJson = "{}";
        Renderer[] attachedRenderers = new Renderer[0];
        Material[][] originalMaterials = new Material[0][];
        Material[][] appliedMaterials = new Material[0][];
        string aspectMode = GhostScreenAspectModeFit;
        string screenSurfaceTargetId = "player:screen";
        StandalonePlayerRecord standaloneRecord = null;
        HostedPlayerSurfaceContract hostedContract = null;
        string standaloneSelectorJson = "{}";

        try
        {
            atomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
            if (string.IsNullOrEmpty(atomUid))
            {
                errorMessage = "player atom uid not resolved";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            Atom hostAtom;
            if (!TryFindSceneAtomByUid(atomUid, out hostAtom) || hostAtom == null)
            {
                errorMessage = "player host atom not found";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "read_binding_identity";
            instanceId = ExtractJsonArgString(argsJson, "instanceId");
            slotId = ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId");

            bindStage = "read_previous_binding";
            hadPreviousBinding = playerScreenBindings.TryGetValue(atomUid, out previousBinding) && previousBinding != null;

            bindStage = "resolve_aspect_mode";
            aspectMode = ResolveStandalonePlayerAspectMode(
                argsJson,
                hadPreviousBinding && !string.IsNullOrEmpty(previousBinding.aspectMode)
                    ? previousBinding.aspectMode
                    : GhostScreenAspectModeFit);
            screenSurfaceTargetId = ResolvePlayerScreenSurfaceTargetId(argsJson, null, previousBinding);

            bool requireHostedBind = false;
#if FRAMEANGEL_CUA_PLAYER
            string attachedHostedAtomUid = containingAtom != null && !string.IsNullOrEmpty(containingAtom.uid)
                ? containingAtom.uid.Trim()
                : "";
            requireHostedBind =
                !string.IsNullOrEmpty(attachedHostedAtomUid)
                && string.Equals(attachedHostedAtomUid, atomUid, StringComparison.OrdinalIgnoreCase);
#endif

            bindStage = "resolve_hosted_contract";
            string hostedResolveError = "";
            if (TryResolveHostedPlayerSurfaceContract(atomUid, out hostedContract, out hostedResolveError) && hostedContract != null)
            {
                bindStage = "resolve_hosted_runtime";
                if (!TryResolveOrCreateHostedStandalonePlayerRecordForWrite(atomUid, aspectMode, out standaloneRecord, out hostedContract, out errorMessage)
                    || standaloneRecord == null)
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                sameBinding =
                    hadPreviousBinding
                    && string.Equals(previousBinding.instanceId, standaloneRecord.instanceId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(previousBinding.slotId, standaloneRecord.slotId, StringComparison.OrdinalIgnoreCase);

                standaloneRecord.aspectMode = aspectMode;
                standaloneRecord.lastError = "";
                if (!TryEnsureStandalonePlayerRuntime(standaloneRecord, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
                standaloneRecord.needsScreenRefresh = true;

                bindStage = "bind_hosted_screen";
                if (!TryRefreshHostedStandalonePlayerScreenBinding(atomUid, screenSurfaceTargetId, standaloneRecord, hostedContract, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                bindStage = "record_hosted_binding";
                if (standaloneRecord.binding == null)
                {
                    errorMessage = "hosted player screen binding missing";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                standaloneRecord.binding.atomUid = atomUid;
                standaloneRecord.binding.embeddedHostAtomUid = atomUid;
                standaloneRecord.binding.aspectMode = aspectMode;
                standaloneRecord.binding.surfaceTargetId = string.IsNullOrEmpty(screenSurfaceTargetId) ? "player:screen" : screenSurfaceTargetId;

                if (hadPreviousBinding && !sameBinding)
                    TryRestoreDisconnectSurface(previousBinding);
                else if (sameBinding && previousBinding != null)
                    DestroyAppliedScreenSurfaceMaterials(previousBinding.appliedSurfaceMaterials);

                playerScreenBindings[atomUid] = standaloneRecord.binding;
                standaloneSelectorJson = "{\"playbackKey\":\"" + EscapeJsonString(standaloneRecord.playbackKey) + "\"}";

                bindStage = "build_hosted_receipt";
                stateJson = BuildSelectedPlayerStateJson(
                    "{"
                    + "\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\""
                    + "}");
                consumerReceiptJson = BuildStandalonePlayerSelectedStateJson(standaloneSelectorJson);
                string hostedPayload = BuildPlayerReceiptPayload(actionId, "player_screen_bound", stateJson, consumerReceiptJson);
                resultJson = BuildBrokerResult(true, "player_screen_bound", hostedPayload);

                bindStage = "emit_hosted_runtime_event";
                EmitRuntimeEvent(
                    "player_screen_bound",
                    actionId,
                    "ok",
                    "",
                    "player_screen_bound",
                    atomUid,
                    ExtractJsonArgString(argsJson, "correlationId"),
                    ExtractJsonArgString(argsJson, "messageId"),
                    standaloneRecord.instanceId,
                    hostedPayload
                );

                return true;
            }

            if (requireHostedBind)
            {
                errorMessage = string.IsNullOrEmpty(hostedResolveError)
                    ? "hosted player surface contract unresolved"
                    : hostedResolveError;
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "resolve_screen_slot";
            if (!TryResolveInnerPieceScreenSlot(instanceId, slotId, out instance, out slot, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            aspectMode = ResolveStandalonePlayerAspectMode(
                argsJson,
                hadPreviousBinding && !string.IsNullOrEmpty(previousBinding.aspectMode)
                    ? previousBinding.aspectMode
                    : instance.defaultAspectMode);

            bindStage = "resolve_screen_plane";
            if (!TryResolveInnerPieceScreenPlane(instance.instanceId, slot.slotId, out plane, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            screenSurfaceTargetId = ResolvePlayerScreenSurfaceTargetId(argsJson, slot, previousBinding);
            sameBinding =
                hadPreviousBinding
                && string.Equals(previousBinding.instanceId, instance.instanceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(previousBinding.slotId, slot.slotId, StringComparison.OrdinalIgnoreCase);

            bindStage = "resolve_standalone_runtime";
            if (TryResolveOrCreateStandalonePlayerRecordForWrite(argsJson, out standaloneRecord, out instance, out slot, out errorMessage)
                && standaloneRecord != null)
            {
                standaloneRecord.aspectMode = aspectMode;
                standaloneRecord.lastError = "";
                if (!TryEnsureStandalonePlayerRuntime(standaloneRecord, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
                standaloneRecord.needsScreenRefresh = true;

                bindStage = "bind_standalone_screen";
                if (!TryRefreshStandalonePlayerScreenBinding(standaloneRecord, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                bindStage = "record_standalone_binding";
                if (standaloneRecord.binding == null)
                {
                    errorMessage = "standalone player screen binding missing";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                standaloneRecord.binding.atomUid = atomUid;
                standaloneRecord.binding.embeddedHostAtomUid = atomUid;
                standaloneRecord.binding.aspectMode = aspectMode;
                standaloneRecord.binding.surfaceTargetId = string.IsNullOrEmpty(screenSurfaceTargetId) ? "player:screen" : screenSurfaceTargetId;

                if (hadPreviousBinding && !sameBinding)
                    TryRestoreDisconnectSurface(previousBinding);
                else if (sameBinding && previousBinding != null)
                    DestroyAppliedScreenSurfaceMaterials(previousBinding.appliedSurfaceMaterials);

                playerScreenBindings[atomUid] = standaloneRecord.binding;
                standaloneSelectorJson = "{\"playbackKey\":\"" + EscapeJsonString(standaloneRecord.playbackKey) + "\"}";

                bindStage = "build_standalone_receipt";
                stateJson = BuildSelectedPlayerStateJson(
                    "{"
                    + "\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\""
                    + ",\"instanceId\":\"" + EscapeJsonString(instance.instanceId) + "\""
                    + ",\"slotId\":\"" + EscapeJsonString(slot.slotId) + "\""
                    + "}");
                consumerReceiptJson = BuildStandalonePlayerSelectedStateJson(standaloneSelectorJson);
                string standalonePayload = BuildPlayerReceiptPayload(actionId, "player_screen_bound", stateJson, consumerReceiptJson);
                resultJson = BuildBrokerResult(true, "player_screen_bound", standalonePayload);

                bindStage = "emit_standalone_runtime_event";
                EmitRuntimeEvent(
                    "player_screen_bound",
                    actionId,
                    "ok",
                    "",
                    "player_screen_bound",
                    atomUid,
                    ExtractJsonArgString(argsJson, "correlationId"),
                    ExtractJsonArgString(argsJson, "messageId"),
                    instance.instanceId,
                    standalonePayload
                );

                return true;
            }

            if (ShouldUseCleanRoomPlayerPath(argsJson, atomUid))
            {
                errorMessage = string.IsNullOrEmpty(errorMessage)
                    ? "standalone player target not resolved"
                    : errorMessage;
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "resolve_consumer";
            consumer = ResolvePlayerConsumer(argsJson);
            if (consumer == null)
            {
                errorMessage = "player consumer not found";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "execute_player_bind";
            if (!TryExecutePlayerCommand(
                consumer,
                BuildPlayerScreenBindCommandJson(instance, slot, plane, screenSurfaceTargetId),
                out consumerReceiptJson
            ))
            {
                string consumerError = ExtractJsonArgString(consumerReceiptJson, "error");
                if (string.IsNullOrEmpty(consumerError))
                    consumerError = ExtractJsonArgString(BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}"), "lastError");
                errorMessage = string.IsNullOrEmpty(consumerError)
                    ? "player bind_screen execution failed"
                    : ("player bind_screen execution failed: " + consumerError);
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "read_player_state";
            stateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}");
            helperHostAtomUid = ExtractJsonArgString(stateJson, "embeddedHostAtomUid");

            bindStage = "attach_screen_material";
            if (!TryAttachPlayerScreenMaterial(instance, slot, helperHostAtomUid, aspectMode, screenSurfaceTargetId, out attachedRenderers, out originalMaterials, out appliedMaterials, out screenDebugJson, out errorMessage))
            {
                TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out consumerReceiptJson);
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "apply_surface_visibility";
            string visibilityError;
            if (!TryApplyBoundPlayerSurfaceVisibility(instance, slot, null, out visibilityError))
            {
                TryRestoreScreenSurfaceMaterials(attachedRenderers, originalMaterials);
                DestroyAppliedScreenSurfaceMaterials(appliedMaterials);
                TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out consumerReceiptJson);
                errorMessage = visibilityError;
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            bindStage = "restore_previous_disconnect";
            if (hadPreviousBinding && !sameBinding)
                TryRestoreDisconnectSurface(previousBinding);
            else if (sameBinding && previousBinding != null)
                DestroyAppliedScreenSurfaceMaterials(previousBinding.appliedSurfaceMaterials);

            bindStage = "record_binding";
            PlayerScreenBindingRecord nextBinding = new PlayerScreenBindingRecord();
            nextBinding.atomUid = atomUid;
            nextBinding.instanceId = instance.instanceId;
            nextBinding.slotId = slot.slotId;
            nextBinding.displayId = slot.displayId;
            nextBinding.screenContractVersion = instance.screenContractVersion ?? "";
            nextBinding.disconnectStateId = slot.disconnectStateId ?? "";
            nextBinding.surfaceTargetId = string.IsNullOrEmpty(screenSurfaceTargetId) ? "player:screen" : screenSurfaceTargetId;
            nextBinding.embeddedHostAtomUid = helperHostAtomUid ?? "";
            nextBinding.debugJson = string.IsNullOrEmpty(screenDebugJson) ? "{}" : screenDebugJson;
            nextBinding.aspectMode = aspectMode;
            nextBinding.screenSurfaceRenderers = attachedRenderers ?? new Renderer[0];
            nextBinding.originalSurfaceMaterials =
                sameBinding && previousBinding != null && previousBinding.originalSurfaceMaterials != null && previousBinding.originalSurfaceMaterials.Length > 0
                    ? previousBinding.originalSurfaceMaterials
                    : (originalMaterials ?? new Material[0][]);
            nextBinding.appliedSurfaceMaterials = appliedMaterials ?? new Material[0][];
            if (!TryApplyBoundPlayerSurfaceVisibility(instance, slot, nextBinding, out visibilityError))
            {
                TryRestoreScreenSurfaceMaterials(attachedRenderers, originalMaterials);
                DestroyAppliedScreenSurfaceMaterials(appliedMaterials);
                TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out consumerReceiptJson);
                errorMessage = visibilityError;
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
            playerScreenBindings[atomUid] = nextBinding;

            bindStage = "build_receipt_payload";
            stateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}");
            string payload = BuildPlayerReceiptPayload(actionId, "player_screen_bound", stateJson, consumerReceiptJson);
            resultJson = BuildBrokerResult(true, "player_screen_bound", payload);

            bindStage = "emit_runtime_event";
            EmitRuntimeEvent(
                "player_screen_bound",
                actionId,
                "ok",
                "",
                "player_screen_bound",
                atomUid,
                ExtractJsonArgString(argsJson, "correlationId"),
                ExtractJsonArgString(argsJson, "messageId"),
                instance.instanceId,
                payload
            );

            return true;
        }
        catch (Exception ex)
        {
            TryRestoreScreenSurfaceMaterials(attachedRenderers, originalMaterials);
            DestroyAppliedScreenSurfaceMaterials(appliedMaterials);
            if (consumer != null)
                TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out consumerReceiptJson);

            errorMessage = "player screen bind stage " + bindStage + ": " + ex.Message;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }
    }

    private bool TryClearPlayerScreenBinding(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        bool skipConsumer;
        if (!TryReadBoolArg(argsJson, out skipConsumer, "skipConsumer"))
            skipConsumer = false;

        bool restoreDisconnect;
        if (!TryReadBoolArg(argsJson, out restoreDisconnect, "restoreDisconnect"))
            restoreDisconnect = true;

        JSONStorable consumer = ResolvePlayerConsumer(argsJson);
        string atomUid = ResolvePlayerConsumerAtomUid(consumer, argsJson);
        PlayerScreenBindingRecord binding;
        bool foundBinding = TryResolvePlayerScreenBinding(argsJson, atomUid, out binding);
        if (!foundBinding)
            binding = null;
        StandalonePlayerRecord standaloneRecord = null;
        if (binding != null)
            TryResolveStandalonePlayerRecordForScreenBinding(binding, out standaloneRecord);

        string consumerReceiptJson = "{}";
        bool consumerCommandFailed = false;
        if (!skipConsumer && consumer != null)
        {
            consumerCommandFailed = !TryExecutePlayerCommand(
                consumer,
                BuildPlayerScreenClearCommandJson(),
                out consumerReceiptJson
            );
        }

        if (foundBinding)
        {
            playerScreenBindings.Remove(binding.atomUid ?? atomUid ?? "");
            if (standaloneRecord != null && standaloneRecord.binding == binding)
                standaloneRecord.binding = null;
            if (restoreDisconnect)
                TryRestoreDisconnectSurface(binding);
        }

        if (consumerCommandFailed)
        {
            errorMessage = "player clear_screen_binding execution failed";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string stateArgsJson = !string.IsNullOrEmpty(atomUid)
            ? "{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}"
            : argsJson;
        string stateJson = BuildSelectedPlayerStateJson(stateArgsJson);
        string payload = BuildPlayerReceiptPayload(actionId, "player_screen_binding_cleared", stateJson, consumerReceiptJson);
        resultJson = BuildBrokerResult(true, "player_screen_binding_cleared", payload);
        EmitRuntimeEvent(
            "player_screen_binding_cleared",
            actionId,
            "ok",
            "",
            "player_screen_binding_cleared",
            !string.IsNullOrEmpty(atomUid) ? atomUid : (binding != null ? binding.atomUid : ""),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            binding != null ? binding.instanceId : "",
            payload
        );
        return true;
    }

    private bool TryBindPlayerControlSurface(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord controlSurfaceInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolveInnerPieceControlSurfaceInstance(argsJson, out controlSurfaceInstance, out controlSurface, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string targetDisplayId = ResolvePlayerControlSurfaceTargetDisplayId(argsJson, controlSurface);
        if (string.IsNullOrEmpty(targetDisplayId))
        {
            errorMessage = "targetDisplayId not resolved";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool preferStandalonePlayer;
        if (!TryReadBoolArg(argsJson, out preferStandalonePlayer, "preferStandalonePlayer", "preferStandalone"))
            preferStandalonePlayer = false;

        StandalonePlayerRecord standaloneRecord;
        string standaloneErrorMessage;
        bool standaloneResolved = TryResolveStandalonePlayerRecordForControlSurface(
            argsJson,
            targetDisplayId,
            out standaloneRecord,
            out standaloneErrorMessage);
        if (standaloneResolved)
        {
            if (!TryEnsureStandalonePlayerRuntime(standaloneRecord, out standaloneErrorMessage))
            {
                errorMessage = !string.IsNullOrEmpty(standaloneErrorMessage)
                    ? standaloneErrorMessage
                    : "standalone player runtime missing";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            PlayerControlSurfaceBindingRecord standaloneBinding = BuildStandalonePlayerControlSurfaceBinding(
                controlSurfaceInstance,
                controlSurface,
                standaloneRecord,
                targetDisplayId);
            playerControlSurfaceBindings[standaloneBinding.controlSurfaceInstanceId] = standaloneBinding;

            string standaloneStateJson = BuildPlayerControlSurfaceTargetStateJson(standaloneBinding);
            string standalonePayload = BuildPlayerControlSurfaceReceiptPayload(
                "player_control_surface_bound",
                standaloneBinding,
                null,
                standaloneStateJson,
                "{}");
            resultJson = BuildBrokerResult(true, "player_control_surface_bound", standalonePayload);
            EmitRuntimeEvent(
                "player_control_surface_bound",
                actionId,
                "ok",
                "",
                "player_control_surface_bound",
                ResolvePlayerControlSurfaceBindingTargetId(standaloneBinding),
                ExtractJsonArgString(argsJson, "correlationId"),
                ExtractJsonArgString(argsJson, "messageId"),
                standaloneBinding.controlSurfaceInstanceId,
                standalonePayload);
            return true;
        }

        if (preferStandalonePlayer)
        {
            errorMessage = !string.IsNullOrEmpty(standaloneErrorMessage)
                ? standaloneErrorMessage
                : "standalone player target not resolved";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        JSONStorable consumer = ResolvePlayerConsumer(argsJson);
        if (consumer == null)
        {
            errorMessage = !string.IsNullOrEmpty(standaloneErrorMessage)
                ? standaloneErrorMessage
                : "player consumer not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string atomUid = ResolvePlayerConsumerAtomUid(consumer, argsJson);
        if (string.IsNullOrEmpty(atomUid))
        {
            errorMessage = "player atom uid not resolved";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        PlayerControlSurfaceBindingRecord binding = new PlayerControlSurfaceBindingRecord();
        binding.controlSurfaceInstanceId = controlSurfaceInstance.instanceId;
        binding.controlSurfaceResourceId = controlSurfaceInstance.resourceId ?? "";
        binding.controlSurfaceId = controlSurface.controlSurfaceId ?? "";
        binding.controlFamilyId = controlSurface.controlFamilyId ?? "";
        binding.controlThemeId = controlSurface.controlThemeId ?? "";
        binding.controlThemeLabel = controlSurface.controlThemeLabel ?? "";
        binding.controlThemeVariantId = controlSurface.controlThemeVariantId ?? "";
        binding.toolkitCategory = controlSurface.toolkitCategory ?? "";
        binding.sourcePrefabAssetPath = controlSurface.sourcePrefabAssetPath ?? "";
        binding.targetDisplayId = targetDisplayId;
        binding.targetKind = "player";
        binding.atomUid = atomUid;
        binding.boundAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        PlayerScreenBindingRecord screenBinding;
        if (playerScreenBindings.TryGetValue(atomUid, out screenBinding) && screenBinding != null)
        {
            binding.matchedCurrentScreenBinding = AreEquivalentInnerPieceDisplayIds(
                screenBinding.displayId,
                targetDisplayId
            );
            if (binding.matchedCurrentScreenBinding)
            {
                binding.matchedScreenInstanceId = screenBinding.instanceId ?? "";
                binding.matchedScreenSlotId = screenBinding.slotId ?? "";
            }
        }

        playerControlSurfaceBindings[binding.controlSurfaceInstanceId] = binding;

        string playerStateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}");
        string payload = BuildPlayerControlSurfaceReceiptPayload(
            "player_control_surface_bound",
            binding,
            null,
            playerStateJson,
            "{}"
        );
        resultJson = BuildBrokerResult(true, "player_control_surface_bound", payload);
        EmitRuntimeEvent(
            "player_control_surface_bound",
            actionId,
            "ok",
            "",
            "player_control_surface_bound",
            atomUid,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            binding.controlSurfaceInstanceId,
            payload
        );
        return true;
    }

    private bool TryGetPlayerControlSurfaceBinding(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        PlayerControlSurfaceBindingRecord binding;
        InnerPieceInstanceRecord controlSurfaceInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolvePlayerControlSurfaceBinding(argsJson, out binding, out controlSurfaceInstance, out controlSurface, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string playerStateJson = BuildPlayerControlSurfaceTargetStateJson(binding);
        string payload = BuildPlayerControlSurfaceReceiptPayload(
            "player_control_surface_binding",
            binding,
            null,
            playerStateJson,
            binding.lastReceiptJson
        );
        resultJson = BuildBrokerResult(true, "player_control_surface_binding ok", payload);
        EmitRuntimeEvent(
            "player_control_surface_binding",
            actionId,
            "ok",
            "",
            "player_control_surface_binding ok",
            ResolvePlayerControlSurfaceBindingTargetId(binding),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            binding.controlSurfaceInstanceId,
            payload
        );
        return true;
    }

    private bool TryTriggerPlayerControlSurfaceElement(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        PlayerControlSurfaceBindingRecord binding;
        InnerPieceInstanceRecord controlSurfaceInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolvePlayerControlSurfaceBinding(argsJson, out binding, out controlSurfaceInstance, out controlSurface, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPieceControlElementData element;
        if (!TryResolvePlayerControlSurfaceElement(controlSurface, argsJson, out element, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string playerOperation;
        string playerStateJson;
        string consumerReceiptJson;
        if (IsStandalonePlayerTargetKind(binding.targetKind))
        {
            if (!TryTriggerStandalonePlayerControlSurfaceElement(
                binding,
                element,
                argsJson,
                out playerOperation,
                out playerStateJson,
                out consumerReceiptJson,
                out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }
        else
        {
            JSONStorable consumer = ResolvePlayerConsumer("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
            if (consumer == null)
            {
                errorMessage = "player consumer not found";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            string playerCommandJson;
            if (!TryBuildPlayerControlSurfaceCommand(consumer, element, argsJson, out playerCommandJson, out playerOperation, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (!TryExecutePlayerCommand(consumer, playerCommandJson, out consumerReceiptJson))
            {
                string consumerError = ExtractJsonArgString(consumerReceiptJson, "error");
                errorMessage = string.IsNullOrEmpty(consumerError)
                    ? "player control surface command failed"
                    : ("player control surface command failed: " + consumerError);
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            playerStateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
        }

        binding.lastElementId = element.elementId ?? "";
        binding.lastActionId = element.actionId ?? "";
        binding.lastReceiptJson = string.IsNullOrEmpty(consumerReceiptJson) ? "{}" : consumerReceiptJson;
        RecordBoundPlayerControlSurfaceInteraction(controlSurfaceInstance, controlSurface, element, argsJson);

        string payload = BuildPlayerControlSurfaceReceiptPayload(
            string.IsNullOrEmpty(playerOperation) ? "player_control_surface_triggered" : playerOperation,
            binding,
            element,
            playerStateJson,
            consumerReceiptJson
        );
        resultJson = BuildBrokerResult(true, "player_control_surface_element_triggered", payload);
        EmitRuntimeEvent(
            "player_control_surface_element_triggered",
            actionId,
            "ok",
            "",
            "player_control_surface_element_triggered",
            ResolvePlayerControlSurfaceBindingTargetId(binding),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            binding.controlSurfaceInstanceId,
            payload
        );
        return true;
    }

    private bool TryTriggerToolkitControlSurfaceElement(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        InnerPieceInstanceRecord controlSurfaceInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolveInnerPieceControlSurfaceInstance(argsJson, out controlSurfaceInstance, out controlSurface, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPieceControlElementData element;
        if (!TryResolvePlayerControlSurfaceElement(controlSurface, argsJson, out element, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        PlayerControlSurfaceBindingRecord binding;
        if (playerControlSurfaceBindings.TryGetValue(controlSurfaceInstance.instanceId, out binding) && binding != null)
            return TryTriggerPlayerControlSurfaceElement(actionId, argsJson, out resultJson, out errorMessage);

        return TryTriggerLocalControlSurfaceElement(
            actionId,
            argsJson,
            controlSurfaceInstance,
            controlSurface,
            element,
            out resultJson,
            out errorMessage);
    }

    private bool ExecutePlayerMutation(
        string actionId,
        string argsJson,
        string summary,
        string operation,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        JSONStorable consumer = ResolvePlayerConsumer(argsJson);
        if (consumer == null)
        {
            errorMessage = "player consumer not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (!TryExecutePlayerCommand(consumer, EnsurePlayerOperation(argsJson, operation), out string consumerReceiptJson))
        {
            string consumerError = ExtractJsonArgString(consumerReceiptJson, "error");
            errorMessage = string.IsNullOrEmpty(consumerError)
                ? "player command execution failed"
                : ("player command execution failed: " + consumerError);
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string stateJson = BuildSelectedPlayerStateJson(argsJson);
        string payload = BuildPlayerReceiptPayload(actionId, summary, stateJson, consumerReceiptJson);
        resultJson = BuildBrokerResult(true, summary, payload);
        EmitRuntimeEvent(
            "player_mutation",
            actionId,
            "ok",
            "",
            summary,
            ExtractJsonArgString(stateJson, "atomUid"),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private void RefreshPlayerScreenBindingsForInnerPieceInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        List<PlayerScreenBindingRecord> bindings = FindPlayerScreenBindingsForInstance(instanceId);
        for (int i = 0; i < bindings.Count; i++)
        {
            PlayerScreenBindingRecord binding = bindings[i];
            if (binding == null)
                continue;

            StandalonePlayerRecord standaloneRecord;
            if (TryResolveStandalonePlayerRecordForScreenBinding(binding, out standaloneRecord) && standaloneRecord != null)
            {
                standaloneRecord.needsScreenRefresh = true;
                string refreshError;
                if (!TryRefreshStandalonePlayerScreenBinding(standaloneRecord, out refreshError))
                    standaloneRecord.lastError = refreshError;
                continue;
            }

            JSONStorable consumer = ResolvePlayerConsumer("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
            if (consumer == null)
            {
                TryRestoreDisconnectSurface(binding);
                playerScreenBindings.Remove(binding.atomUid ?? "");
                continue;
            }

            InnerPieceInstanceRecord instance;
            InnerPieceScreenSlotRuntimeRecord slot;
            string errorMessage;
            if (!TryResolveInnerPieceScreenSlot(binding.instanceId, binding.slotId, out instance, out slot, out errorMessage))
            {
                playerScreenBindings.Remove(binding.atomUid ?? "");
                continue;
            }

            FAInnerPiecePlaneData plane;
            if (!TryResolveInnerPieceScreenPlane(binding.instanceId, binding.slotId, out plane, out errorMessage))
                continue;

            string screenSurfaceTargetId = ResolvePlayerScreenSurfaceTargetId("{}", slot, binding);

            string ignoredReceipt;
            if (!TryExecutePlayerCommand(consumer, BuildPlayerScreenBindCommandJson(instance, slot, plane, screenSurfaceTargetId), out ignoredReceipt))
                continue;

            string stateJson = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
            string helperHostAtomUid = ExtractJsonArgString(stateJson, "embeddedHostAtomUid");
            string bindingDebugJson;
            Renderer[] attachedRenderers;
            Material[][] originalMaterials;
            Material[][] appliedMaterials;
            if (!TryAttachPlayerScreenMaterial(instance, slot, helperHostAtomUid, binding.aspectMode, screenSurfaceTargetId, out attachedRenderers, out originalMaterials, out appliedMaterials, out bindingDebugJson, out errorMessage))
                continue;

            string visibilityError;
            if (!TryApplyBoundPlayerSurfaceVisibility(instance, slot, binding, out visibilityError))
                continue;
            binding.embeddedHostAtomUid = helperHostAtomUid ?? "";
            binding.debugJson = string.IsNullOrEmpty(bindingDebugJson) ? "{}" : bindingDebugJson;
            binding.screenSurfaceRenderers = attachedRenderers ?? new Renderer[0];
            if (binding.originalSurfaceMaterials == null || binding.originalSurfaceMaterials.Length <= 0)
                binding.originalSurfaceMaterials = originalMaterials ?? new Material[0][];
            DestroyAppliedScreenSurfaceMaterials(binding.appliedSurfaceMaterials);
            binding.appliedSurfaceMaterials = appliedMaterials ?? new Material[0][];
        }
    }

    private void ClearPlayerScreenBindingsForInnerPieceInstance(string instanceId, bool restoreDisconnectSurface)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        List<PlayerScreenBindingRecord> bindings = FindPlayerScreenBindingsForInstance(instanceId);
        for (int i = 0; i < bindings.Count; i++)
        {
            PlayerScreenBindingRecord binding = bindings[i];
            if (binding == null)
                continue;

            StandalonePlayerRecord standaloneRecord;
            bool isStandaloneBinding = TryResolveStandalonePlayerRecordForScreenBinding(binding, out standaloneRecord)
                && standaloneRecord != null;

            if (!isStandaloneBinding)
            {
                JSONStorable consumer = ResolvePlayerConsumer("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
                if (consumer != null)
                {
                    string ignoredReceipt;
                    TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out ignoredReceipt);
                }
            }
            else if (standaloneRecord.binding == binding)
                standaloneRecord.binding = null;

            if (restoreDisconnectSurface)
                TryRestoreDisconnectSurface(binding);

            playerScreenBindings.Remove(binding.atomUid ?? "");
        }
    }

    private void ShutdownPlayerScreenBindings()
    {
        List<PlayerScreenBindingRecord> bindings = new List<PlayerScreenBindingRecord>(playerScreenBindings.Values);
        for (int i = 0; i < bindings.Count; i++)
        {
            PlayerScreenBindingRecord binding = bindings[i];
            if (binding == null)
                continue;

            StandalonePlayerRecord standaloneRecord;
            bool isStandaloneBinding = TryResolveStandalonePlayerRecordForScreenBinding(binding, out standaloneRecord)
                && standaloneRecord != null;

            if (!isStandaloneBinding)
            {
                JSONStorable consumer = ResolvePlayerConsumer("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid) + "\"}");
                if (consumer != null)
                {
                    string ignoredReceipt;
                    TryExecutePlayerCommand(consumer, BuildPlayerScreenClearCommandJson(), out ignoredReceipt);
                }
            }
            else if (standaloneRecord.binding == binding)
                standaloneRecord.binding = null;

            TryRestoreDisconnectSurface(binding);
        }

        playerScreenBindings.Clear();
    }

    private void ClearPlayerControlSurfaceBindingsForInnerPieceInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        playerControlSurfaceBindings.Remove(instanceId);
    }

    private void TickPlayerControlSurfaceRelativeBindings()
    {
        if (playerControlSurfaceBindings.Count == 0)
            return;

        List<PlayerControlSurfaceBindingRecord> bindings = new List<PlayerControlSurfaceBindingRecord>(playerControlSurfaceBindings.Values);
        for (int i = 0; i < bindings.Count; i++)
            RefreshPlayerControlSurfaceRelativeBinding(bindings[i]);
    }

    private void RefreshPlayerControlSurfaceRelativeBinding(PlayerControlSurfaceBindingRecord binding)
    {
        if (binding == null
            || string.IsNullOrEmpty(binding.controlSurfaceInstanceId)
            || string.IsNullOrEmpty(binding.targetInstanceId))
            return;

        float now = Time.unscaledTime;
        if (now < binding.nextRelativeLayoutCheckTime)
            return;

        binding.nextRelativeLayoutCheckTime = now + PlayerControlSurfaceRelativeLayoutCheckIntervalSeconds;

        if (!IsStandalonePlayerTargetKind(binding.targetKind))
            return;

        if (IsHostedPlayerInstanceId(binding.targetInstanceId))
        {
            if (binding.hostedFollowBound && binding.hostedPanelPoseCaptured)
                return;

            string hostedLayoutError;
            TryLayoutHostedMetaProofControlSurface(
                binding.controlSurfaceInstanceId,
                binding.targetInstanceId,
                out hostedLayoutError);
            return;
        }

        InnerPieceInstanceRecord controlSurfaceInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        string errorMessage;
        if (!TryResolveInnerPieceControlSurfaceInstance(
                "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(binding.controlSurfaceInstanceId) + "\"}",
                out controlSurfaceInstance,
                out controlSurface,
                out errorMessage)
            || controlSurfaceInstance == null
            || controlSurface == null)
        {
            return;
        }

        InnerPieceInstanceRecord targetInstance;
        if (!TryResolveInnerPieceInstance(
                "{\"instanceId\":\"" + EscapeJsonString(binding.targetInstanceId) + "\"}",
                out targetInstance,
                out errorMessage)
            || targetInstance == null)
        {
            return;
        }

        Vector3 desiredWorldPosition;
        Quaternion desiredWorldRotation;
        string desiredAnchorAtomUid;
        if (!TryComputeMetaProofControlSurfaceLayoutPose(
                controlSurfaceInstance,
                controlSurface,
                binding.targetInstanceId,
                targetInstance,
                null,
                null,
                out desiredWorldPosition,
                out desiredWorldRotation,
                out desiredAnchorAtomUid,
                out errorMessage))
        {
            return;
        }

        bool sameAnchor = string.Equals(
            controlSurfaceInstance.anchorAtomUid ?? "",
            desiredAnchorAtomUid ?? "",
            StringComparison.OrdinalIgnoreCase);
        bool poseMatchesLayout = TryInnerPieceInstancePoseMatchesWorldPose(
            controlSurfaceInstance,
            desiredWorldPosition,
            desiredWorldRotation);
        bool anchorlessPoseMatches = string.IsNullOrEmpty(desiredAnchorAtomUid)
            && string.IsNullOrEmpty(controlSurfaceInstance.anchorAtomUid)
            && poseMatchesLayout;
        bool unresolvedAnchorPreservedPoseMatches =
            string.IsNullOrEmpty(desiredAnchorAtomUid)
            && !string.IsNullOrEmpty(controlSurfaceInstance.anchorAtomUid)
            && controlSurfaceInstance.followPosition
            && controlSurfaceInstance.followRotation
            && poseMatchesLayout;
        if ((sameAnchor
                && !string.IsNullOrEmpty(desiredAnchorAtomUid)
                && controlSurfaceInstance.followPosition
                && controlSurfaceInstance.followRotation
                && poseMatchesLayout)
            || anchorlessPoseMatches
            || unresolvedAnchorPreservedPoseMatches)
            return;

        TryLayoutMetaProofControlSurface(binding.controlSurfaceInstanceId, binding.targetInstanceId, out errorMessage);
    }

    private bool TryInnerPieceInstancePoseMatchesWorldPose(
        InnerPieceInstanceRecord instance,
        Vector3 desiredWorldPosition,
        Quaternion desiredWorldRotation)
    {
        if (instance == null || string.IsNullOrEmpty(instance.rootObjectId))
            return false;

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord)
            || rootRecord == null
            || rootRecord.gameObject == null)
        {
            return false;
        }

        Transform rootTransform = rootRecord.gameObject.transform;
        if (rootTransform == null)
            return false;

        if (Vector3.Distance(rootTransform.position, desiredWorldPosition) > MetaProofControlPanelLayoutPositionToleranceMeters)
            return false;

        if (Quaternion.Angle(rootTransform.rotation, desiredWorldRotation) > MetaProofControlPanelLayoutRotationToleranceDegrees)
            return false;

        return true;
    }

    private void ShutdownPlayerControlSurfaceBindings()
    {
        playerControlSurfaceBindings.Clear();
    }

    private void MarkStandalonePlayerRecordsForInnerPieceRefresh(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || standalonePlayerRecords.Count <= 0)
            return;

        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord record = kvp.Value;
            if (record == null)
                continue;
            if (!string.Equals(record.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                continue;

            record.needsScreenRefresh = true;
        }
    }

    private List<PlayerScreenBindingRecord> FindPlayerScreenBindingsForInstance(string instanceId)
    {
        List<PlayerScreenBindingRecord> matches = new List<PlayerScreenBindingRecord>();
        if (string.IsNullOrEmpty(instanceId))
            return matches;

        foreach (KeyValuePair<string, PlayerScreenBindingRecord> kvp in playerScreenBindings)
        {
            PlayerScreenBindingRecord binding = kvp.Value;
            if (binding == null)
                continue;
            if (!string.Equals(binding.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                continue;
            matches.Add(binding);
        }

        return matches;
    }

    private bool TryResolvePlayerScreenBinding(string argsJson, string atomUid, out PlayerScreenBindingRecord binding)
    {
        binding = null;

        if (!string.IsNullOrEmpty(atomUid) && playerScreenBindings.TryGetValue(atomUid, out binding) && binding != null)
            return true;

        string instanceId = ExtractJsonArgString(argsJson, "instanceId");
        string slotId = ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId");
        if (string.IsNullOrEmpty(instanceId))
            return false;

        foreach (KeyValuePair<string, PlayerScreenBindingRecord> kvp in playerScreenBindings)
        {
            PlayerScreenBindingRecord candidate = kvp.Value;
            if (candidate == null)
                continue;
            if (!string.Equals(candidate.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrEmpty(slotId)
                && !string.Equals(candidate.slotId, slotId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate.displayId, slotId, StringComparison.OrdinalIgnoreCase))
                continue;

            binding = candidate;
            return true;
        }

        return false;
    }

    private bool IsStandalonePlayerBindingAtomUid(string atomUid)
    {
        return !string.IsNullOrEmpty(atomUid)
            && atomUid.StartsWith(StandalonePlayerBindingAtomUidPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveStandalonePlayerRecordForScreenBinding(PlayerScreenBindingRecord binding, out StandalonePlayerRecord record)
    {
        record = null;
        if (binding == null)
            return false;

        if (IsStandalonePlayerBindingAtomUid(binding.atomUid))
        {
            string playbackKey = binding.atomUid.Substring(StandalonePlayerBindingAtomUidPrefix.Length);
            if (!string.IsNullOrEmpty(playbackKey)
                && standalonePlayerRecords.TryGetValue(playbackKey, out record)
                && record != null)
            {
                return true;
            }
        }

        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord candidate = kvp.Value;
            if (candidate == null)
                continue;

            if (candidate.binding == binding)
            {
                record = candidate;
                return true;
            }

            if (!string.IsNullOrEmpty(candidate.instanceId)
                && string.Equals(candidate.instanceId, binding.instanceId, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(candidate.slotId, binding.slotId, StringComparison.OrdinalIgnoreCase)
                    || AreEquivalentInnerPieceDisplayIds(candidate.displayId, binding.displayId)))
            {
                record = candidate;
                return true;
            }
        }

        return false;
    }

    private void TryRestoreDisconnectSurface(PlayerScreenBindingRecord binding)
    {
        if (binding == null)
            return;

        TryRestoreScreenSurfaceMaterials(binding.screenSurfaceRenderers, binding.originalSurfaceMaterials);
        DestroyAppliedScreenSurfaceMaterials(binding.appliedSurfaceMaterials);
        RestoreHiddenShellRenderers(binding);
        RestoreHostedPlayerBackdropPresentation(binding);
        if (binding.runtimeMediaSurfaceObject != null)
        {
            try
            {
                UnityEngine.Object.Destroy(binding.runtimeMediaSurfaceObject);
            }
            catch
            {
            }
        }

        if (IsHostedPlayerBindingRecord(binding))
            return;

        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        string ignoredError;
        if (TryResolveInnerPieceScreenSlot(binding.instanceId, binding.slotId, out instance, out slot, out ignoredError))
            DestroyRuntimeMediaSurface(slot);
        string errorMessage;
        TrySetInnerPieceDisconnectSurfaceVisible(binding.instanceId, binding.slotId, true, out errorMessage);
    }

    private void RestoreHostedPlayerBackdropPresentation(PlayerScreenBindingRecord binding)
    {
        if (binding == null)
            return;

        if (binding.backdropTransformCaptured && binding.backdropTransform != null)
        {
            try
            {
                binding.backdropTransform.localPosition = binding.backdropOriginalLocalPosition;
                binding.backdropTransform.localRotation = binding.backdropOriginalLocalRotation;
                binding.backdropTransform.localScale = binding.backdropOriginalLocalScale;
            }
            catch
            {
            }
        }

        Renderer[] renderers = binding.backdropRenderers ?? new Renderer[0];
        bool[] states = binding.backdropRendererStates ?? new bool[0];
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            bool enabled = i < states.Length ? states[i] : true;
            try
            {
                renderer.enabled = enabled;
            }
            catch
            {
            }
        }

        binding.backdropTransform = null;
        binding.backdropTransformCaptured = false;
        binding.backdropRenderers = new Renderer[0];
        binding.backdropRendererStates = new bool[0];
    }

    private void CaptureAndHideScreenSurface(InnerPieceScreenSlotRuntimeRecord slot, PlayerScreenBindingRecord binding)
    {
        if (slot == null || binding == null || slot.screenSurfaceObject == null)
            return;

        Renderer[] renderers = slot.screenSurfaceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length <= 0)
            return;

        Transform runtimeSurfaceTransform = slot.runtimeMediaSurfaceObject != null
            ? slot.runtimeMediaSurfaceObject.transform
            : null;

        List<Renderer> capturedRenderers = new List<Renderer>(renderers.Length);
        List<bool> capturedStates = new List<bool>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (runtimeSurfaceTransform != null
                && renderer.transform != null
                && (renderer.transform == runtimeSurfaceTransform
                    || renderer.transform.IsChildOf(runtimeSurfaceTransform)))
            {
                continue;
            }

            capturedRenderers.Add(renderer);
            capturedStates.Add(renderer.enabled);
            renderer.enabled = false;
        }

        binding.hiddenShellRenderers = capturedRenderers.ToArray();
        binding.hiddenShellRendererStates = capturedStates.ToArray();
    }

    private void RestoreHiddenShellRenderers(PlayerScreenBindingRecord binding)
    {
        if (binding == null || binding.hiddenShellRenderers == null)
            return;

        for (int i = 0; i < binding.hiddenShellRenderers.Length; i++)
        {
            Renderer renderer = binding.hiddenShellRenderers[i];
            if (renderer == null)
                continue;

            bool enabled = i < binding.hiddenShellRendererStates.Length
                ? binding.hiddenShellRendererStates[i]
                : true;

            try
            {
                renderer.enabled = enabled;
            }
            catch
            {
            }
        }
    }

    private bool TryGetPlayerScreenRendererVisibilityOverride(Renderer renderer, out bool visible)
    {
        visible = false;
        if (renderer == null)
            return false;

        foreach (KeyValuePair<string, PlayerScreenBindingRecord> kvp in playerScreenBindings)
        {
            PlayerScreenBindingRecord binding = kvp.Value;
            if (binding == null || binding.hiddenShellRenderers == null)
                continue;

            for (int i = 0; i < binding.hiddenShellRenderers.Length; i++)
            {
                if (!ReferenceEquals(binding.hiddenShellRenderers[i], renderer))
                    continue;

                visible = false;
                return true;
            }
        }

        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord record = kvp.Value;
            PlayerScreenBindingRecord binding = record != null ? record.binding : null;
            if (binding == null || binding.hiddenShellRenderers == null)
                continue;

            for (int i = 0; i < binding.hiddenShellRenderers.Length; i++)
            {
                if (!ReferenceEquals(binding.hiddenShellRenderers[i], renderer))
                    continue;

                visible = false;
                return true;
            }
        }

        return false;
    }

    private bool TryAttachPlayerScreenMaterial(
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        string helperHostAtomUid,
        string aspectMode,
        string screenSurfaceTargetId,
        out Renderer[] attachedRenderers,
        out Material[][] originalMaterials,
        out Material[][] appliedMaterials,
        out string debugJson,
        out string errorMessage)
    {
        attachedRenderers = new Renderer[0];
        originalMaterials = new Material[0][];
        appliedMaterials = new Material[0][];
        debugJson = "{}";
        errorMessage = "";

        try
        {
            if (slot == null || slot.screenSurfaceObject == null)
            {
                errorMessage = "screen surface not found";
                return false;
            }

            if (string.IsNullOrEmpty(helperHostAtomUid))
            {
                errorMessage = "player helper host uid missing";
                return false;
            }

            SuperController sc = SuperController.singleton;
            if (sc == null)
            {
                errorMessage = "super controller unavailable";
                return false;
            }

            Atom helperAtom = TryResolvePlayerHelperHostAtom(sc, helperHostAtomUid);
            if (helperAtom == null)
            {
                errorMessage = "player helper host atom not found";
                return false;
            }

            JSONStorable imageStorable = helperAtom.GetStorableByID("Image");
            if (imageStorable == null || imageStorable.transform == null)
            {
                errorMessage = "player helper image storable not found";
                return false;
            }

            Texture directSourceTexture = null;
            Vector2 directSourceScale = Vector2.one;
            Vector2 directSourceOffset = Vector2.zero;
            string directSourceName = "";
            Transform helperSourceRoot = helperAtom.transform != null
                ? helperAtom.transform
                : imageStorable.transform;
            bool hasDirectSourceTexture = TryResolveDirectImageControlTexture(
                imageStorable,
                helperSourceRoot,
                out directSourceTexture,
                out directSourceScale,
                out directSourceOffset,
                out directSourceName);

            Renderer[] sourceRenderers = helperSourceRoot != null
                ? helperSourceRoot.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            List<ProjectedMaterialCandidate> sourceCandidates = FindProjectedSourceMaterialCandidates(sourceRenderers);
            if (!hasDirectSourceTexture && (sourceCandidates == null || sourceCandidates.Count <= 0))
            {
                errorMessage = "player helper source material not found";
                return false;
            }

            DestroyRuntimeMediaSurface(slot);

            GameObject mediaTargetObject = ResolvePlayerMediaTargetObject(instance, slot);
            if (mediaTargetObject == null)
            {
                errorMessage = "screen media target not found";
                return false;
            }

            Renderer[] targetRenderers = mediaTargetObject.GetComponentsInChildren<Renderer>(true);
            if (targetRenderers == null || targetRenderers.Length <= 0)
            {
                errorMessage = "screen surface renderers not found";
                return false;
            }

            Material targetBasisMaterial = null;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                Material[] currentMaterials = renderer.sharedMaterials;
                if (currentMaterials == null || currentMaterials.Length <= 0)
                    continue;

                for (int j = 0; j < currentMaterials.Length; j++)
                {
                    Material candidate = currentMaterials[j];
                    if (candidate == null)
                        continue;

                    targetBasisMaterial = candidate;
                    break;
                }

                if (targetBasisMaterial != null)
                    break;
            }

            Material projectedMaterial = null;
            ProjectedMaterialCandidate selectedCandidate = null;
            string projectionMode = "";

            bool usingDisconnectSurfaceTarget = UsesDisconnectSurfaceAsMediaTarget(instance, slot);
            bool isScreenCoreSurface = IsAuthoredScreenSurfacePresentationTarget(instance, slot, mediaTargetObject);
            bool preserveProjectedAlpha = ShouldPreserveProjectedScreenAlpha(screenSurfaceTargetId);
            bool preferSimpleVideoShader = usingDisconnectSurfaceTarget || isScreenCoreSurface;

            if (hasDirectSourceTexture
                && TryCreateResolvedVideoTextureMaterialFromTexture(
                    FindBestRendererMaterial(sourceRenderers) ?? targetBasisMaterial,
                    directSourceTexture,
                    directSourceScale,
                    directSourceOffset,
                    preferSimpleVideoShader,
                    preserveProjectedAlpha,
                    out projectedMaterial))
            {
                selectedCandidate = new ProjectedMaterialCandidate
                {
                    material = null,
                    score = int.MaxValue,
                    rendererName = string.IsNullOrEmpty(directSourceName) ? "ImageControl.rawImage" : directSourceName
                };
                projectionMode = "imagecontrol_rawimage";
            }
            else
            {
                for (int i = 0; i < sourceCandidates.Count; i++)
                {
                    ProjectedMaterialCandidate candidate = sourceCandidates[i];
                    if (candidate == null || candidate.material == null)
                        continue;

                    // The authored front screen should not silently accept helper/backdrop
                    // materials when no real media-bearing source won the candidate race.
                    if (isScreenCoreSurface && !IsAuthoredScreenSafeProjectedSourceCandidate(candidate))
                        continue;

                    if (TryCreateResolvedVideoTextureMaterial(
                        targetBasisMaterial,
                        candidate.material,
                        preferSimpleVideoShader,
                        preserveProjectedAlpha,
                        out projectedMaterial))
                    {
                        selectedCandidate = candidate;
                        projectionMode = "resolved_video_texture";
                        break;
                    }

                    if (TryCreateProjectedScreenMaterial(targetBasisMaterial, candidate.material, preserveProjectedAlpha, out projectedMaterial))
                    {
                        selectedCandidate = candidate;
                        projectionMode = "projected_copy";
                        break;
                    }

                    if (TryCreateFallbackProjectedScreenMaterial(targetBasisMaterial, candidate.material, preserveProjectedAlpha, out projectedMaterial))
                    {
                        selectedCandidate = candidate;
                        projectionMode = "fallback_copy";
                        break;
                    }

                    Texture liveCloneTexture;
                    Vector2 ignoredCloneScale;
                    Vector2 ignoredCloneOffset;
                    if (TryResolveProjectedSourceTexture(candidate.material, out liveCloneTexture, out ignoredCloneScale, out ignoredCloneOffset)
                        && TryCreateLiveHelperOverlayMaterial(targetBasisMaterial, candidate.material, preserveProjectedAlpha, out projectedMaterial))
                    {
                        selectedCandidate = candidate;
                        projectionMode = "live_helper_clone";
                        break;
                    }
                }
            }

            if (projectedMaterial == null)
            {
                errorMessage = "player projected screen material not created";
                return false;
            }

            float contentAspect = 0f;
            TryResolveProjectedContentAspect(projectedMaterial, directSourceTexture, out contentAspect);

            if (isScreenCoreSurface && directSourceTexture != null)
            {
                float rawTextureAspect = 0f;
                if (TryResolveTextureAspect(null, directSourceTexture, out rawTextureAspect) && rawTextureAspect > 0.001f)
                    contentAspect = rawTextureAspect;
            }

            float targetSurfaceAspect = 0f;
            TryResolveSurfaceAspect(mediaTargetObject, out targetSurfaceAspect);

            bool useFitBlackPresentation = ShouldUseFitBlackAspectMode(aspectMode);
            bool useWidthLockedPresentation = ShouldUseWidthLockedAspectMode(aspectMode);
            bool useCropFillPresentation = ShouldUseCropFillAspectMode(aspectMode);
            // The corrected Ghost screen exports can either take the video directly on the
            // disconnect surface or present it through a runtime overlay quad. Fit/letterbox
            // intentionally switches to the overlay path so the movie aspect can be preserved
            // with explicit bars instead of inheriting the slab material. For the bare
            // screen-core surface, crop/fill stays on the authored front screen material path,
            // matching the older direct-CUA baseline instead of taking the fit/full_width
            // overlay detour.
            bool useDirectProjectedMaterial =
                ShouldUseDirectProjectedMaterialPath(instance)
                && (!isScreenCoreSurface || useCropFillPresentation)
                && !FrameAngelPlayerMediaParity.ShouldUseOverlayPresentation(false, aspectMode);

            if (useDirectProjectedMaterial)
            {
                // The disconnect surface UVs are mirrored relative to the visible front face.
                // The direct-material path needs the horizontal flip so front-view playback is
                // readable, while the authored screen-core overlay path keeps its own mirror
                // correction on the runtime quad instead of inheriting the older Y-180 seam.
                if (usingDisconnectSurfaceTarget)
                    MirrorProjectedScreenTextureHorizontally(projectedMaterial);

                // Crop mode keeps the movie filling the rounded aperture by trimming the
                // overhanging axis in UV space instead of stretching the picture.
                if (useCropFillPresentation)
                    ApplyAspectCropToMaterial(projectedMaterial, targetSurfaceAspect, contentAspect);

                if (!TryApplyProjectedMaterialToTargetRenderers(
                    targetRenderers,
                    projectedMaterial,
                    out attachedRenderers,
                    out originalMaterials,
                    out appliedMaterials,
                    out errorMessage))
                {
                    return false;
                }

                Material debugMaterial = null;
                if (attachedRenderers != null && attachedRenderers.Length > 0 && attachedRenderers[0] != null)
                {
                    Material[] debugMaterials = attachedRenderers[0].sharedMaterials;
                    if (debugMaterials != null && debugMaterials.Length > 0)
                        debugMaterial = debugMaterials[0];
                }

                debugJson = BuildPlayerScreenBindingDebugJson(
                    helperHostAtomUid,
                    helperAtom,
                    mediaTargetObject,
                    usingDisconnectSurfaceTarget ? "disconnect_direct_material" : "direct_target_material",
                    projectionMode,
                    sourceRenderers,
                    sourceCandidates,
                    selectedCandidate,
                    attachedRenderers,
                    debugMaterial,
                    directSourceTexture);
                return true;
            }

            Renderer[] backdropRenderers = new Renderer[0];
            Material[][] backdropOriginalMaterials = new Material[0][];
            Material[][] backdropAppliedMaterials = new Material[0][];

            bool applyBackdropToTarget =
                FrameAngelPlayerMediaParity.ShouldApplyBackdropToTarget(
                    aspectMode,
                    usingDisconnectSurfaceTarget,
                    isScreenCoreSurface);

            if (applyBackdropToTarget)
            {
                // Keep the authored target surface as a black backdrop whenever the runtime quad
                // preserves aspect inside a larger slab. Fit/letterbox needs this for true black
                // bars, and width-locked/full_width needs the same treatment so unused top/bottom
                // space does not stay light-reactive around the centered movie.
                Material backdropMaterial;
                if (!TryCreateBlackBackdropMaterial(targetBasisMaterial, out backdropMaterial))
                {
                    try
                    {
                        UnityEngine.Object.Destroy(projectedMaterial);
                    }
                    catch
                    {
                    }

                    errorMessage = "fit backdrop material not created";
                    return false;
                }

                if (!TryApplyProjectedMaterialToTargetRenderers(
                    targetRenderers,
                    backdropMaterial,
                    out backdropRenderers,
                    out backdropOriginalMaterials,
                    out backdropAppliedMaterials,
                    out errorMessage))
                {
                    try
                    {
                        UnityEngine.Object.Destroy(projectedMaterial);
                    }
                    catch
                    {
                    }

                    return false;
                }
            }

            // The fallback quad should size from the content itself. Using the movie texture
            // aspect here is what keeps phone/pad/landscape surfaces from stretching when the
            // runtime quad is asked to present fit/letterbox content on differently shaped slabs.
            float runtimeSurfaceAspect = contentAspect;

            if (ShouldFlipProjectedOverlayVertically(directSourceTexture, directSourceName, isScreenCoreSurface))
            {
                // The older disconnect/fallback overlay seams still need the VideoPlayer
                // RenderTexture Y flip. The authored direct-CUA screen-core front path does
                // not, and carrying that legacy correction forward makes front-facing video
                // readable only upside down.
                FlipProjectedScreenTextureVertically(projectedMaterial);
            }

            if (isScreenCoreSurface)
            {
                // Once the stale Y-180 front-face correction is removed, the authored
                // screen-core overlay still needs its own horizontal mirror to keep the
                // operator-facing front screen readable on the current runtime quad path.
                MirrorProjectedScreenTextureHorizontally(projectedMaterial);
            }

            if (isScreenCoreSurface)
            {
                // The hosted/runtime screen-core overlay still needs the same opaque depth
                // behavior as the newer reload branch, otherwise behind-screen controls can
                // leak through even though the visible front face looks correct.
                TryEnableScreenCoreOverlayDepthOcclusion(projectedMaterial);

                // Keep the bare direct-CUA screen-core runtime overlay front-only so
                // front-view parity is unambiguous and rear-edge bleed is suppressed.
                TryForceProjectedScreenFrontFaceOnly(projectedMaterial);
            }

            Renderer overlayRenderer;
            Vector3 ignoredTargetCenter;
            Vector3 ignoredTargetSize;
            Vector3 ignoredOverlayPosition;
            Vector3 ignoredOverlayScale;
            if (!TryCreateRuntimeMediaSurface(
                slot,
                mediaTargetObject,
                projectedMaterial,
                runtimeSurfaceAspect,
                aspectMode,
                out overlayRenderer,
                out ignoredTargetCenter,
                out ignoredTargetSize,
                out ignoredOverlayPosition,
                out ignoredOverlayScale,
                out errorMessage))
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }

                TryRestoreScreenSurfaceMaterials(backdropRenderers, backdropOriginalMaterials);
                DestroyAppliedScreenSurfaceMaterials(backdropAppliedMaterials);
                return false;
            }

            Renderer runtimeBackdropRenderer = null;
            Material runtimeBackdropMaterial = null;
            if (isScreenCoreSurface)
            {
                if (!TryCreateRuntimeMediaBackingSurface(
                    slot.runtimeMediaSurfaceObject,
                    slot.slotId,
                    targetBasisMaterial,
                    out runtimeBackdropRenderer,
                    out runtimeBackdropMaterial,
                    out errorMessage))
                {
                    DestroyRuntimeMediaSurface(slot);
                    try
                    {
                        UnityEngine.Object.Destroy(projectedMaterial);
                    }
                    catch
                    {
                    }

                    TryRestoreScreenSurfaceMaterials(backdropRenderers, backdropOriginalMaterials);
                    DestroyAppliedScreenSurfaceMaterials(backdropAppliedMaterials);
                    return false;
                }
            }

            bool applyLegacyFrontFaceCorrection =
                usingDisconnectSurfaceTarget
                || (slot != null && slot.forceOperatorFacingFrontFace);

            if (applyLegacyFrontFaceCorrection)
            {
                // Keep the older Y-180 correction on the fallback disconnect-surface
                // seam and explicit force-flag contracts only. The authored screen-core
                // overlay already uses the modern front-facing rotation contract and
                // must not be rotated back onto its rear side here.
                ApplyStandaloneFitOverlayFrontFace(overlayRenderer);
            }

            if (backdropRenderers != null && backdropRenderers.Length > 0)
            {
                attachedRenderers = AppendRenderer(backdropRenderers, overlayRenderer);
                originalMaterials = AppendMaterialRows(backdropOriginalMaterials, new[] { new Material[0] });
                appliedMaterials = AppendMaterialRows(backdropAppliedMaterials, new[] { new[] { projectedMaterial } });
            }
            else
            {
                attachedRenderers = overlayRenderer != null ? new[] { overlayRenderer } : new Renderer[0];
                originalMaterials = new[] { new Material[0] };
                appliedMaterials = new[] { new[] { projectedMaterial } };
            }

            if (runtimeBackdropRenderer != null && runtimeBackdropMaterial != null)
            {
                attachedRenderers = AppendRenderer(attachedRenderers, runtimeBackdropRenderer);
                originalMaterials = AppendMaterialRows(originalMaterials, new[] { new Material[0] });
                appliedMaterials = AppendMaterialRows(appliedMaterials, new[] { new[] { runtimeBackdropMaterial } });
            }

            debugJson = BuildPlayerScreenBindingDebugJson(
                helperHostAtomUid,
                helperAtom,
                mediaTargetObject,
                usingDisconnectSurfaceTarget ? "disconnect_overlay_quad" : "runtime_overlay_quad",
                projectionMode,
                sourceRenderers,
                sourceCandidates,
                selectedCandidate,
                attachedRenderers,
                projectedMaterial,
                directSourceTexture);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "attach material exception: " + ex.Message;
            return false;
        }
    }

    private void MirrorProjectedScreenTextureHorizontally(Material material)
    {
        if (material == null)
            return;

        bool mirroredExplicitProperty = false;
        mirroredExplicitProperty |= MirrorProjectedTextureProperty(material, "_MainTex");
        mirroredExplicitProperty |= MirrorProjectedTextureProperty(material, "_BaseMap");
        mirroredExplicitProperty |= MirrorProjectedTextureProperty(material, "_EmissionMap");

        if (!mirroredExplicitProperty)
        {
            try
            {
                Vector2 scale = material.mainTextureScale;
                Vector2 offset = material.mainTextureOffset;
                material.mainTextureScale = new Vector2(-scale.x, scale.y);
                material.mainTextureOffset = new Vector2(offset.x + scale.x, offset.y);
            }
            catch
            {
            }
        }
    }

    private void FlipProjectedScreenTextureVertically(Material material)
    {
        if (material == null)
            return;

        bool flippedExplicitProperty = false;
        flippedExplicitProperty |= FlipProjectedTexturePropertyVertically(material, "_MainTex");
        flippedExplicitProperty |= FlipProjectedTexturePropertyVertically(material, "_BaseMap");
        flippedExplicitProperty |= FlipProjectedTexturePropertyVertically(material, "_EmissionMap");

        if (!flippedExplicitProperty)
        {
            try
            {
                Vector2 scale = material.mainTextureScale;
                Vector2 offset = material.mainTextureOffset;
                material.mainTextureScale = new Vector2(scale.x, -scale.y);
                material.mainTextureOffset = new Vector2(offset.x, offset.y + scale.y);
            }
            catch
            {
            }
        }
    }

    private bool ShouldFlipProjectedOverlayVertically(Texture directSourceTexture, string directSourceName, bool isScreenCoreSurface)
    {
        if (isScreenCoreSurface)
            return false;

        return directSourceTexture is RenderTexture
            || (!string.IsNullOrEmpty(directSourceName)
                && directSourceName.IndexOf("VideoPlayer", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private bool MirrorProjectedTextureProperty(Material material, string propertyName)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return false;

        try
        {
            if (!material.HasProperty(propertyName))
                return false;

            Vector2 scale = material.GetTextureScale(propertyName);
            Vector2 offset = material.GetTextureOffset(propertyName);
            material.SetTextureScale(propertyName, new Vector2(-scale.x, scale.y));
            material.SetTextureOffset(propertyName, new Vector2(offset.x + scale.x, offset.y));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool FlipProjectedTexturePropertyVertically(Material material, string propertyName)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return false;

        try
        {
            if (!material.HasProperty(propertyName))
                return false;

            Vector2 scale = material.GetTextureScale(propertyName);
            Vector2 offset = material.GetTextureOffset(propertyName);
            material.SetTextureScale(propertyName, new Vector2(scale.x, -scale.y));
            material.SetTextureOffset(propertyName, new Vector2(offset.x, offset.y + scale.y));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Renderer[] AppendRenderer(Renderer[] renderers, Renderer renderer)
    {
        if (renderer == null)
            return renderers ?? new Renderer[0];

        if (renderers == null || renderers.Length <= 0)
            return new[] { renderer };

        Renderer[] combined = new Renderer[renderers.Length + 1];
        Array.Copy(renderers, combined, renderers.Length);
        combined[combined.Length - 1] = renderer;
        return combined;
    }

    private Material[][] AppendMaterialRows(Material[][] rows, Material[][] tail)
    {
        Material[][] head = rows ?? new Material[0][];
        Material[][] append = tail ?? new Material[0][];
        if (head.Length <= 0)
            return append;
        if (append.Length <= 0)
            return head;

        Material[][] combined = new Material[head.Length + append.Length][];
        Array.Copy(head, combined, head.Length);
        Array.Copy(append, 0, combined, head.Length, append.Length);
        return combined;
    }

    private bool TryCreateBlackBackdropMaterial(Material targetMaterial, out Material backdropMaterial)
    {
        backdropMaterial = null;

        int basisRenderQueue = -1;
        try
        {
            if (targetMaterial != null)
                basisRenderQueue = targetMaterial.renderQueue;
        }
        catch
        {
            basisRenderQueue = -1;
        }

        try
        {
            string[] shaderCandidates = new[]
            {
                "Unlit/Color",
                "Unlit/Texture",
                "Sprites/Default",
                "UI/Default",
            };

            Shader shader = null;
            for (int i = 0; i < shaderCandidates.Length; i++)
            {
                try
                {
                    shader = Shader.Find(shaderCandidates[i]);
                }
                catch
                {
                    shader = null;
                }

                if (shader != null)
                    break;
            }

            if (shader != null)
            {
                backdropMaterial = new Material(shader);
            }
            else if (targetMaterial != null)
            {
                backdropMaterial = new Material(targetMaterial);
            }
            else
            {
                return false;
            }

            Texture whiteTexture = null;
            try
            {
                whiteTexture = Texture2D.whiteTexture;
            }
            catch
            {
                whiteTexture = null;
            }

            if (whiteTexture != null)
            {
                TrySetMaterialTexture(backdropMaterial, "_MainTex", whiteTexture);
                TrySetMaterialTexture(backdropMaterial, "_BaseMap", whiteTexture);
                TrySetMaterialTexture(backdropMaterial, "_EmissionMap", whiteTexture);
            }

            TrySetMaterialColor(backdropMaterial, "_Color", Color.black);
            TrySetMaterialColor(backdropMaterial, "_BaseColor", Color.black);
            TrySetMaterialColor(backdropMaterial, "_EmissionColor", Color.black);
            TrySetMaterialFloat(backdropMaterial, "_Cull", 0f);
            TrySetMaterialFloat(backdropMaterial, "_ZWrite", 0f);
            TrySetMaterialFloat(backdropMaterial, "_Mode", 0f);
            TrySetMaterialFloat(backdropMaterial, "_Surface", 0f);
            TrySetMaterialFloat(backdropMaterial, "_AlphaClip", 0f);

            try
            {
                backdropMaterial.DisableKeyword("_EMISSION");
            }
            catch
            {
            }

            try
            {
                backdropMaterial.SetOverrideTag("RenderType", "Opaque");
            }
            catch
            {
            }

            if (basisRenderQueue >= 0)
            {
                try
                {
                    backdropMaterial.renderQueue = basisRenderQueue + 5;
                }
                catch
                {
                }
            }

            return true;
        }
        catch
        {
            if (backdropMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(backdropMaterial);
                }
                catch
                {
                }
            }

            backdropMaterial = null;
            return false;
        }
    }

    private bool TryApplyProjectedMaterialToTargetRenderers(
        Renderer[] targetRenderers,
        Material projectedMaterial,
        out Renderer[] attachedRenderers,
        out Material[][] originalMaterials,
        out Material[][] appliedMaterials,
        out string errorMessage)
    {
        attachedRenderers = new Renderer[0];
        originalMaterials = new Material[0][];
        appliedMaterials = new Material[0][];
        errorMessage = "";

        if (targetRenderers == null || targetRenderers.Length <= 0)
        {
            errorMessage = "screen surface renderers not found";
            return false;
        }

        if (projectedMaterial == null)
        {
            errorMessage = "projected material not created";
            return false;
        }

        List<Renderer> appliedRenderers = new List<Renderer>();
        List<Material[]> originalRows = new List<Material[]>();
        List<Material[]> appliedRows = new List<Material[]>();

        try
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                Material[] currentMaterials = renderer.sharedMaterials;
                if (currentMaterials == null || currentMaterials.Length <= 0)
                    continue;

                Material[] originals = (Material[])currentMaterials.Clone();
                Material[] replacements = new Material[currentMaterials.Length];
                for (int j = 0; j < replacements.Length; j++)
                    replacements[j] = new Material(projectedMaterial);

                renderer.materials = replacements;
                renderer.enabled = true;

                appliedRenderers.Add(renderer);
                originalRows.Add(originals);
                appliedRows.Add(replacements);
            }

            try
            {
                UnityEngine.Object.Destroy(projectedMaterial);
            }
            catch
            {
            }

            if (appliedRenderers.Count <= 0)
            {
                errorMessage = "screen surface renderers not found";
                return false;
            }

            attachedRenderers = appliedRenderers.ToArray();
            originalMaterials = originalRows.ToArray();
            appliedMaterials = appliedRows.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            for (int i = 0; i < appliedRenderers.Count; i++)
            {
                Renderer renderer = appliedRenderers[i];
                Material[] originals = i < originalRows.Count ? originalRows[i] : null;
                if (renderer == null)
                    continue;

                try
                {
                    renderer.sharedMaterials = originals ?? new Material[0];
                }
                catch
                {
                }
            }

            for (int i = 0; i < appliedRows.Count; i++)
            {
                Material[] row = appliedRows[i];
                if (row == null)
                    continue;

                for (int j = 0; j < row.Length; j++)
                {
                    Material material = row[j];
                    if (material == null)
                        continue;

                    try
                    {
                        UnityEngine.Object.Destroy(material);
                    }
                    catch
                    {
                    }
                }
            }

            try
            {
                UnityEngine.Object.Destroy(projectedMaterial);
            }
            catch
            {
            }

            errorMessage = "apply projected material exception: " + ex.Message;
            return false;
        }
    }

    private void DestroyRuntimeMediaSurface(InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null)
            return;

        GameObject runtimeSurfaceObject = slot.runtimeMediaSurfaceObject;
        slot.runtimeMediaSurfaceObject = null;
        slot.runtimeMediaSurfaceRenderer = null;

        if (runtimeSurfaceObject == null)
            return;

        try
        {
            UnityEngine.Object.Destroy(runtimeSurfaceObject);
        }
        catch
        {
        }
    }

    private GameObject ResolvePlayerMediaTargetObject(InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null)
            return null;

        if (slot.mediaTargetObject != null)
            return slot.mediaTargetObject;

        if (ShouldUseDisconnectSurfaceAsMediaTarget(slot))
            return slot.disconnectSurfaceObject;

        if (slot.screenSurfaceObject != null)
            return slot.screenSurfaceObject;

        return slot.disconnectSurfaceObject;
    }

    private GameObject ResolvePlayerMediaTargetObject(InnerPieceInstanceRecord instance, InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null)
            return null;

        if (slot.mediaTargetObject != null)
            return slot.mediaTargetObject;

        if (ShouldUseDisconnectSurfaceAsMediaTarget(instance, slot))
            return slot.disconnectSurfaceObject;

        if (slot.screenSurfaceObject != null)
            return slot.screenSurfaceObject;

        return slot.disconnectSurfaceObject;
    }

    private bool UsesDisconnectSurfaceAsMediaTarget(InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null)
            return false;

        return ShouldUseDisconnectSurfaceAsMediaTarget(slot);
    }

    private bool UsesDisconnectSurfaceAsMediaTarget(InnerPieceInstanceRecord instance, InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null)
            return false;

        return ShouldUseDisconnectSurfaceAsMediaTarget(instance, slot);
    }

    private bool TryApplyBoundPlayerSurfaceVisibility(
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        PlayerScreenBindingRecord binding,
        out string errorMessage)
    {
        errorMessage = "";
        if (slot == null)
            return true;

        bool useDisconnectSurface = UsesDisconnectSurfaceAsMediaTarget(instance, slot);
        if (slot.disconnectSurfaceObject != null)
        {
            string instanceId = instance != null ? (instance.instanceId ?? "") : "";
            if (!string.IsNullOrEmpty(instanceId) && innerPieceInstances.ContainsKey(instanceId))
            {
                if (!TrySetInnerPieceDisconnectSurfaceVisible(
                    instanceId,
                    slot.slotId,
                    useDisconnectSurface,
                    out errorMessage))
                {
                    return false;
                }
            }
            else
            {
                slot.disconnectSurfaceVisible = useDisconnectSurface;
                SetInnerPieceNodeRenderersVisible(slot.disconnectSurfaceObject, useDisconnectSurface);
            }
        }

        bool hideAuthoredScreenSurface =
            binding != null
            && string.Equals(instance != null ? (instance.screenContractVersion ?? "") : "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ExtractJsonArgString(binding.debugJson, "attachMode"), "runtime_overlay_quad", StringComparison.OrdinalIgnoreCase);

        if (binding != null && (useDisconnectSurface || hideAuthoredScreenSurface))
            CaptureAndHideScreenSurface(slot, binding);

        return true;
    }

    private bool IsAuthoredScreenSurfacePresentationTarget(
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        GameObject mediaTargetObject)
    {
        if (instance != null)
        {
            if (string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(instance.shellId ?? "", GhostScreenRectShellId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (slot == null || mediaTargetObject == null || slot.mediaTargetUsesNormalizedRect)
            return false;

        if (slot.screenSurfaceObject == null || !ReferenceEquals(slot.screenSurfaceObject, mediaTargetObject))
            return false;

        string screenSurfaceNodeId = slot.screenSurfaceNodeId ?? "";
        if (string.Equals(screenSurfaceNodeId, HostedPlayerScreenSurfaceNodeId, StringComparison.OrdinalIgnoreCase))
            return true;

        string targetName = mediaTargetObject.name ?? "";
        return string.Equals(targetName, HostedPlayerScreenSurfaceNodeId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldUseDirectProjectedMaterialPath(InnerPieceInstanceRecord instance)
    {
        if (instance == null)
            return false;

        if (string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenContractVersion, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(instance.shellId ?? "", GhostScreenRectShellId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrEmpty(instance.resourceId))
            return false;

        // These exported Ghost screen resources are the corrected variants whose disconnect
        // surface is safe to target directly. Other resources stay on the more generic runtime
        // overlay path so we do not accidentally apply the rounded-screen assumptions to
        // unrelated inner-piece screens.
        return string.Equals(instance.resourceId, GhostScreenRoundedFixedResourceId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(instance.resourceId, GhostScreenRectFixedResourceId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldUseDisconnectSurfaceAsMediaTarget(InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (slot == null || slot.disconnectSurfaceObject == null)
            return false;

        if (slot.screenSurfaceObject == null)
            return true;

        Vector3 ignoredCenter;
        Vector3 shellSize;
        if (!TryBuildInnerPieceSurfaceLocalBounds(slot.screenSurfaceObject, out ignoredCenter, out shellSize))
            return false;

        float minFaceDimension = Mathf.Max(0.001f, Mathf.Min(shellSize.x, shellSize.y));
        if (shellSize.z >= 0.008f && shellSize.z > (minFaceDimension * 0.01f))
            return true;

        string screenNodeId = slot.screenSurfaceNodeId ?? "";
        if (screenNodeId.IndexOf("rounded_rect_prism", StringComparison.OrdinalIgnoreCase) >= 0
            || screenNodeId.IndexOf("rect_prism", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private bool ShouldUseDisconnectSurfaceAsMediaTarget(InnerPieceInstanceRecord instance, InnerPieceScreenSlotRuntimeRecord slot)
    {
        if (instance != null
            && string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (slot != null
            && slot.screenSurfaceObject != null
            && IsAuthoredScreenSurfacePresentationTarget(instance, slot, slot.screenSurfaceObject))
        {
            return false;
        }

        return ShouldUseDisconnectSurfaceAsMediaTarget(slot);
    }

    private bool TryCreateRuntimeMediaSurface(
        InnerPieceScreenSlotRuntimeRecord slot,
        GameObject targetSurfaceObject,
        Material material,
        float contentAspect,
        string aspectMode,
        out Renderer overlayRenderer,
        out Vector3 targetLocalCenter,
        out Vector3 targetLocalSize,
        out Vector3 overlayLocalPosition,
        out Vector3 overlayLocalScale,
        out string errorMessage)
    {
        overlayRenderer = null;
        targetLocalCenter = Vector3.zero;
        targetLocalSize = Vector3.zero;
        overlayLocalPosition = Vector3.zero;
        overlayLocalScale = Vector3.zero;
        errorMessage = "";

        if (slot == null || targetSurfaceObject == null)
        {
            errorMessage = "screen surface not found";
            return false;
        }

        if (material == null)
        {
            errorMessage = "runtime media material not provided";
            return false;
        }

        if (!TryBuildInnerPieceSurfaceLocalBounds(targetSurfaceObject, out targetLocalCenter, out targetLocalSize))
        {
            errorMessage = "screen surface local bounds unavailable";
            return false;
        }

        GameObject overlayObject = null;
        try
        {
            overlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayObject.name = "FAPlayerRuntimeSurface_" + (string.IsNullOrEmpty(slot.slotId) ? "main" : slot.slotId);
            overlayObject.layer = targetSurfaceObject.layer;
            overlayObject.transform.SetParent(targetSurfaceObject.transform, false);

            float width = Mathf.Max(0.001f, targetLocalSize.x);
            float height = Mathf.Max(0.001f, targetLocalSize.y);
            float centerX = targetLocalCenter.x;
            float centerY = targetLocalCenter.y;

            if (slot.mediaTargetUsesNormalizedRect)
            {
                Rect rect = slot.mediaTargetNormalizedRect;
                float rectWidth = Mathf.Clamp(rect.width, 0.001f, 1f);
                float rectHeight = Mathf.Clamp(rect.height, 0.001f, 1f);
                float rectCenterX = Mathf.Clamp01(rect.x + (rectWidth * 0.5f));
                float rectCenterY = Mathf.Clamp01(rect.y + (rectHeight * 0.5f));

                width = Mathf.Max(0.001f, targetLocalSize.x * rectWidth);
                height = Mathf.Max(0.001f, targetLocalSize.y * rectHeight);
                centerX = targetLocalCenter.x + ((rectCenterX - 0.5f) * targetLocalSize.x);
                centerY = targetLocalCenter.y + ((rectCenterY - 0.5f) * targetLocalSize.y);
            }

            float displayedWidth = width;
            float displayedHeight = height;
            if (TryResolveDisplayedSurfaceSize(targetSurfaceObject, width, height, out displayedWidth, out displayedHeight))
            {
                float presentedDisplayedWidth;
                float presentedDisplayedHeight;
                FrameAngelPlayerMediaParity.ComputePresentedSize(
                    displayedWidth,
                    displayedHeight,
                    contentAspect,
                    aspectMode,
                    out presentedDisplayedWidth,
                    out presentedDisplayedHeight);

                float displayedUnitWidth;
                float displayedUnitHeight;
                if (TryResolveDisplayedSurfaceSize(targetSurfaceObject, 1f, 1f, out displayedUnitWidth, out displayedUnitHeight))
                {
                    width = presentedDisplayedWidth / Mathf.Max(0.001f, displayedUnitWidth);
                    height = presentedDisplayedHeight / Mathf.Max(0.001f, displayedUnitHeight);
                }
                else
                {
                    width = presentedDisplayedWidth;
                    height = presentedDisplayedHeight;
                }
            }
            else
            {
                FrameAngelPlayerMediaParity.ComputePresentedSize(
                    width,
                    height,
                    contentAspect,
                    aspectMode,
                    out width,
                    out height);
            }

            float halfDepth = Mathf.Max(0.0001f, targetLocalSize.z * 0.5f);
            bool isAuthoredFrontScreen =
                !slot.mediaTargetUsesNormalizedRect
                && slot.screenSurfaceObject != null
                && ReferenceEquals(targetSurfaceObject, slot.screenSurfaceObject);
            float surfaceGap = isAuthoredFrontScreen
                ? Mathf.Max(0.0002f, targetLocalSize.z * 0.02f)
                : Mathf.Max(0.0025f, targetLocalSize.z * 0.25f);
            float zOffset = halfDepth + surfaceGap;
            // The bare direct-CUA screen-core contract treats screen_surface as the
            // real front-facing viewport. Keep the runtime media quad on that same
            // operator-facing side so the front view does not read the quad's back
            // face and the rear view does not see media bleeding around the edges.
            float signedZOffset = isAuthoredFrontScreen ? zOffset : zOffset;
            overlayObject.transform.localRotation = FrameAngelPlayerMediaParity.ResolveOverlayLocalRotation(isAuthoredFrontScreen);
            overlayLocalPosition = new Vector3(centerX, centerY, targetLocalCenter.z + signedZOffset);
            overlayLocalScale = new Vector3(width, height, 1f);
            overlayObject.transform.localPosition = overlayLocalPosition;
            overlayObject.transform.localScale = overlayLocalScale;

            Collider overlayCollider = overlayObject.GetComponent<Collider>();
            if (overlayCollider != null)
                UnityEngine.Object.Destroy(overlayCollider);

            overlayRenderer = overlayObject.GetComponent<Renderer>();
            if (overlayRenderer == null)
            {
                UnityEngine.Object.Destroy(overlayObject);
                errorMessage = "runtime media renderer not created";
                return false;
            }

            overlayRenderer.sharedMaterials = new[] { material };
            overlayRenderer.enabled = true;
            overlayRenderer.receiveShadows = false;
#pragma warning disable CS0618
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#pragma warning restore CS0618
            overlayRenderer.allowOcclusionWhenDynamic = false;

            slot.runtimeMediaSurfaceObject = overlayObject;
            slot.runtimeMediaSurfaceRenderer = overlayRenderer;
            return true;
        }
        catch (Exception ex)
        {
            if (overlayObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(overlayObject);
                }
                catch
                {
                }
            }

            overlayRenderer = null;
            errorMessage = "runtime media surface exception: " + ex.Message;
            return false;
        }
    }

    private void TryResolveMaterialPrimaryScaleOffset(Material material, out Vector2 scale, out Vector2 offset)
    {
        scale = Vector2.one;
        offset = Vector2.zero;
        if (material == null)
            return;

        TryGetMaterialTextureScaleOffset(material, "_MainTex", ref scale, ref offset);
        TryGetMaterialTextureScaleOffset(material, "_BaseMap", ref scale, ref offset);
        TryGetMaterialTextureScaleOffset(material, "_EmissionMap", ref scale, ref offset);
    }

    private void TryResolveTextureDimensions(Texture texture, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (texture == null)
            return;

        try
        {
            width = texture.width;
            height = texture.height;
        }
        catch
        {
            width = 0;
            height = 0;
        }
    }

    private bool TryResolvePreparedVideoDimensions(VideoPlayer videoPlayer, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (videoPlayer == null)
            return false;

        try
        {
            Texture resolvedTexture = videoPlayer.texture;
            if (resolvedTexture == null)
                return false;

            int resolvedWidth;
            int resolvedHeight;
            TryResolveTextureDimensions(resolvedTexture, out resolvedWidth, out resolvedHeight);
            resolvedWidth = SafeClampVideoDimension(resolvedWidth);
            resolvedHeight = SafeClampVideoDimension(resolvedHeight);
            if (resolvedWidth > 0 && resolvedHeight > 0)
            {
                width = resolvedWidth;
                height = resolvedHeight;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private string BuildVector2Json(Vector2 value)
    {
        return "{\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + "}";
    }

    private string BuildVector3Json(Vector3 value)
    {
        return "{\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + ",\"z\":" + FormatFloat(value.z)
            + "}";
    }

    private bool TryBuildInnerPieceSurfaceLocalBounds(GameObject surfaceObject, out Vector3 center, out Vector3 size)
    {
        center = Vector3.zero;
        size = Vector3.zero;
        if (surfaceObject == null)
            return false;

        Renderer[] renderers = surfaceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length <= 0)
            return false;

        Transform space = surfaceObject.transform;
        bool hasProjection = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;
        float minZ = 0f;
        float maxZ = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject rendererObject = renderer.gameObject;
            if (rendererObject != null
                && !string.IsNullOrEmpty(rendererObject.name)
                && rendererObject.name.StartsWith("FAPlayerRuntimeSurface_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryAccumulateRendererBoundsInSurfaceSpace(
                renderer,
                space,
                ref hasProjection,
                ref minX,
                ref maxX,
                ref minY,
                ref maxY,
                ref minZ,
                ref maxZ))
            {
                continue;
            }
        }

        if (!hasProjection)
            return false;

        center = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f);
        size = new Vector3(
            Mathf.Max(0.001f, maxX - minX),
            Mathf.Max(0.001f, maxY - minY),
            Mathf.Max(0.001f, maxZ - minZ));
        return true;
    }

    private bool TryAccumulateRendererBoundsInSurfaceSpace(
        Renderer renderer,
        Transform surfaceSpace,
        ref bool hasProjection,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        ref float minZ,
        ref float maxZ)
    {
        if (renderer == null || surfaceSpace == null)
            return false;

        Bounds localBounds;
        if (TryGetRendererLocalBounds(renderer, out localBounds))
        {
            Transform rendererTransform = renderer.transform;
            Vector3 localMin = localBounds.min;
            Vector3 localMax = localBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 rendererLocalCorner = new Vector3(
                    (corner & 1) == 0 ? localMin.x : localMax.x,
                    (corner & 2) == 0 ? localMin.y : localMax.y,
                    (corner & 4) == 0 ? localMin.z : localMax.z);
                Vector3 worldCorner = rendererTransform.TransformPoint(rendererLocalCorner);
                Vector3 surfaceLocalCorner = surfaceSpace.InverseTransformPoint(worldCorner);
                UpdateSurfaceLocalBounds(
                    surfaceLocalCorner,
                    ref hasProjection,
                    ref minX,
                    ref maxX,
                    ref minY,
                    ref maxY,
                    ref minZ,
                    ref maxZ);
            }

            return true;
        }

        Bounds worldBounds = renderer.bounds;
        Vector3 worldMin = worldBounds.min;
        Vector3 worldMax = worldBounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = new Vector3(
                (corner & 1) == 0 ? worldMin.x : worldMax.x,
                (corner & 2) == 0 ? worldMin.y : worldMax.y,
                (corner & 4) == 0 ? worldMin.z : worldMax.z);
            Vector3 surfaceLocalCorner = surfaceSpace.InverseTransformPoint(worldCorner);
            UpdateSurfaceLocalBounds(
                surfaceLocalCorner,
                ref hasProjection,
                ref minX,
                ref maxX,
                ref minY,
                ref maxY,
                ref minZ,
                ref maxZ);
        }

        return true;
    }

    private bool TryGetRendererLocalBounds(Renderer renderer, out Bounds localBounds)
    {
        localBounds = new Bounds();
        if (renderer == null)
            return false;

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            localBounds = meshFilter.sharedMesh.bounds;
            return true;
        }

        SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
        if (skinnedRenderer != null)
        {
            localBounds = skinnedRenderer.localBounds;
            return true;
        }

        return false;
    }

    private void UpdateSurfaceLocalBounds(
        Vector3 surfaceLocalCorner,
        ref bool hasProjection,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        ref float minZ,
        ref float maxZ)
    {
        if (!hasProjection)
        {
            minX = maxX = surfaceLocalCorner.x;
            minY = maxY = surfaceLocalCorner.y;
            minZ = maxZ = surfaceLocalCorner.z;
            hasProjection = true;
            return;
        }

        minX = Mathf.Min(minX, surfaceLocalCorner.x);
        maxX = Mathf.Max(maxX, surfaceLocalCorner.x);
        minY = Mathf.Min(minY, surfaceLocalCorner.y);
        maxY = Mathf.Max(maxY, surfaceLocalCorner.y);
        minZ = Mathf.Min(minZ, surfaceLocalCorner.z);
        maxZ = Mathf.Max(maxZ, surfaceLocalCorner.z);
    }

    private bool TryResolveDisplayedSurfaceAspect(GameObject surfaceObject, float localWidth, float localHeight, out float aspect)
    {
        aspect = 0f;
        if (surfaceObject == null)
            return false;

        Transform surfaceTransform = surfaceObject.transform;
        if (surfaceTransform == null)
            return false;

        float displayedWidth = surfaceTransform.TransformVector(new Vector3(localWidth, 0f, 0f)).magnitude;
        float displayedHeight = surfaceTransform.TransformVector(new Vector3(0f, localHeight, 0f)).magnitude;
        if (displayedWidth <= 0.001f || displayedHeight <= 0.001f)
            return false;

        aspect = displayedWidth / displayedHeight;
        return aspect > 0.001f;
    }

    private bool TryResolveDisplayedSurfaceSize(
        GameObject surfaceObject,
        float localWidth,
        float localHeight,
        out float displayedWidth,
        out float displayedHeight)
    {
        displayedWidth = 0f;
        displayedHeight = 0f;
        if (surfaceObject == null)
            return false;

        Transform surfaceTransform = surfaceObject.transform;
        if (surfaceTransform == null)
            return false;

        displayedWidth = surfaceTransform.TransformVector(new Vector3(localWidth, 0f, 0f)).magnitude;
        displayedHeight = surfaceTransform.TransformVector(new Vector3(0f, localHeight, 0f)).magnitude;
        return displayedWidth > 0.001f && displayedHeight > 0.001f;
    }

    private bool TryResolveTextureAspect(Material material, Texture fallbackTexture, out float aspect)
    {
        aspect = 0f;

        Texture texture = TryGetMaterialTexture(material, "_MainTex");
        if (texture == null)
            texture = TryGetMaterialTexture(material, "_BaseMap");
        if (texture == null)
            texture = TryGetMaterialTexture(material, "_EmissionMap");
        if (texture == null)
            texture = fallbackTexture;

        if (texture == null)
            return false;

        try
        {
            int width = texture.width;
            int height = texture.height;
            if (width <= 0 || height <= 0)
                return false;

            aspect = (float)width / (float)height;
            return aspect > 0.001f;
        }
        catch
        {
            aspect = 0f;
            return false;
        }
    }

    private bool TryResolveProjectedContentAspect(Material material, Texture fallbackTexture, out float aspect)
    {
        aspect = 0f;

        if (!TryResolveTextureAspect(material, fallbackTexture, out float textureAspect))
            return false;

        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        TryGetMaterialTextureScaleOffset(material, "_MainTex", ref scale, ref offset);
        TryGetMaterialTextureScaleOffset(material, "_BaseMap", ref scale, ref offset);
        TryGetMaterialTextureScaleOffset(material, "_EmissionMap", ref scale, ref offset);

        float scaleX = Mathf.Abs(scale.x);
        float scaleY = Mathf.Abs(scale.y);
        if (scaleX <= 0.0001f || scaleY <= 0.0001f)
        {
            aspect = textureAspect;
            return aspect > 0.001f;
        }

        aspect = textureAspect * (scaleX / scaleY);
        return aspect > 0.001f;
    }

    private bool TryResolveSurfaceAspect(GameObject surfaceObject, out float aspect)
    {
        aspect = 0f;
        if (surfaceObject == null)
            return false;

        Vector3 localCenter;
        Vector3 localSize;
        if (!TryBuildInnerPieceSurfaceLocalBounds(surfaceObject, out localCenter, out localSize))
            return false;

        float width = Mathf.Max(0.001f, localSize.x);
        float height = Mathf.Max(0.001f, localSize.y);
        if (TryResolveDisplayedSurfaceAspect(surfaceObject, width, height, out aspect))
            return true;

        aspect = width / height;
        return aspect > 0.001f;
    }

    private void ApplyAspectCropToMaterial(Material material, float targetSurfaceAspect, float contentAspect)
    {
        if (material == null || targetSurfaceAspect <= 0.001f || contentAspect <= 0.001f)
            return;

        // Crop/fill should start from a neutral full-frame window instead of inheriting
        // stale source-material UV state. That keeps crop from preserving an older
        // mirrored/shifted basis while fit/full_width are already using the corrected
        // front-screen presentation seam.
        ResetProjectedTextureWindow(material);

        if (Mathf.Abs(targetSurfaceAspect - contentAspect) <= 0.001f)
            return;

        if (contentAspect > targetSurfaceAspect)
        {
            float visibleWidthFraction = Mathf.Clamp01(targetSurfaceAspect / contentAspect);
            ApplyCenteredTextureWindow(material, visibleWidthFraction, 1f);
            return;
        }

        float visibleHeightFraction = Mathf.Clamp01(contentAspect / targetSurfaceAspect);
        ApplyCenteredTextureWindow(material, 1f, visibleHeightFraction);
    }

    private void ResetProjectedTextureWindow(Material material)
    {
        if (material == null)
            return;

        TrySetMaterialTextureScaleOffset(material, "_MainTex", Vector2.one, Vector2.zero);
        TrySetMaterialTextureScaleOffset(material, "_BaseMap", Vector2.one, Vector2.zero);
        TrySetMaterialTextureScaleOffset(material, "_EmissionMap", Vector2.one, Vector2.zero);

        try
        {
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
        }
        catch
        {
        }
    }

    private void ApplyCenteredTextureWindow(Material material, float normalizedWidth, float normalizedHeight)
    {
        ApplyCenteredTextureWindowToProperty(material, "_MainTex", normalizedWidth, normalizedHeight);
        ApplyCenteredTextureWindowToProperty(material, "_BaseMap", normalizedWidth, normalizedHeight);
        ApplyCenteredTextureWindowToProperty(material, "_EmissionMap", normalizedWidth, normalizedHeight);

        try
        {
            Vector2 scale = material.mainTextureScale;
            Vector2 offset = material.mainTextureOffset;
            material.mainTextureScale = BuildCenteredTextureScale(scale, normalizedWidth, normalizedHeight);
            material.mainTextureOffset = BuildCenteredTextureOffset(scale, offset, normalizedWidth, normalizedHeight);
        }
        catch
        {
        }
    }

    private void ApplyCenteredTextureWindowToProperty(Material material, string propertyName, float normalizedWidth, float normalizedHeight)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            if (!material.HasProperty(propertyName))
                return;

            Vector2 scale = material.GetTextureScale(propertyName);
            Vector2 offset = material.GetTextureOffset(propertyName);
            material.SetTextureScale(propertyName, BuildCenteredTextureScale(scale, normalizedWidth, normalizedHeight));
            material.SetTextureOffset(propertyName, BuildCenteredTextureOffset(scale, offset, normalizedWidth, normalizedHeight));
        }
        catch
        {
        }
    }

    private Vector2 BuildCenteredTextureScale(Vector2 currentScale, float normalizedWidth, float normalizedHeight)
    {
        float signX = currentScale.x < 0f ? -1f : 1f;
        float absWidth = Mathf.Abs(currentScale.x) * Mathf.Clamp(normalizedWidth, 0.0001f, 1f);
        float absHeight = Mathf.Abs(currentScale.y) * Mathf.Clamp(normalizedHeight, 0.0001f, 1f);
        return new Vector2(signX * absWidth, absHeight);
    }

    private Vector2 BuildCenteredTextureOffset(Vector2 currentScale, Vector2 currentOffset, float normalizedWidth, float normalizedHeight)
    {
        float absCurrentWidth = Mathf.Abs(currentScale.x);
        float absCurrentHeight = Mathf.Abs(currentScale.y);
        float absTargetWidth = absCurrentWidth * Mathf.Clamp(normalizedWidth, 0.0001f, 1f);
        float absTargetHeight = absCurrentHeight * Mathf.Clamp(normalizedHeight, 0.0001f, 1f);

        float offsetX = currentScale.x >= 0f
            ? currentOffset.x + ((absCurrentWidth - absTargetWidth) * 0.5f)
            : currentOffset.x - ((absCurrentWidth - absTargetWidth) * 0.5f);
        float offsetY = currentOffset.y + ((absCurrentHeight - absTargetHeight) * 0.5f);

        return new Vector2(offsetX, offsetY);
    }

    private string ResolveGhostScreenAspectMode(string argsJson)
    {
        string raw = ExtractJsonArgString(argsJson, "aspectMode", "displayMode", "screenMode");
        if (string.IsNullOrEmpty(raw))
            return GhostScreenAspectModeCrop;

        // We normalize several operator-facing aliases here because Halo/session commands and
        // older fixtures already use a mix of naming. Keeping the mapping centralized avoids
        // silent regressions when a caller says "fit", "letterbox", or "bars" for the same
        // presentation mode.
        raw = raw.Trim();
        if (string.Equals(raw, "fit_black", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "fit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "letterbox", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "fullscreen", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "contain", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "bars", StringComparison.OrdinalIgnoreCase))
            return GhostScreenAspectModeFit;

        if (string.Equals(raw, "full_width", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "fullwidth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "width_locked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "widthlock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "width_locked_fit", StringComparison.OrdinalIgnoreCase))
            return GhostScreenAspectModeFullWidth;

        if (string.Equals(raw, "stretch", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "cover", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "fill_stretch", StringComparison.OrdinalIgnoreCase))
            return GhostScreenAspectModeStretch;

        return GhostScreenAspectModeCrop;
    }

    private string ResolvePlayerScreenSurfaceTargetId(
        string argsJson,
        InnerPieceScreenSlotRuntimeRecord slot,
        PlayerScreenBindingRecord binding)
    {
        string resolved = ExtractJsonArgString(argsJson, "surfaceTargetId", "screenSurfaceTargetId");
        if (!string.IsNullOrEmpty(resolved))
            return resolved;

        if (binding != null && !string.IsNullOrEmpty(binding.surfaceTargetId))
            return binding.surfaceTargetId;

        if (slot != null && !string.IsNullOrEmpty(slot.surfaceTargetId))
            return slot.surfaceTargetId;

        return "player:screen";
    }

    private bool ShouldUseFitBlackAspectMode(string aspectMode)
    {
        return FrameAngelPlayerMediaParity.IsFitBlackAspectMode(aspectMode);
    }

    private bool ShouldUseWidthLockedAspectMode(string aspectMode)
    {
        return FrameAngelPlayerMediaParity.IsWidthLockedAspectMode(aspectMode);
    }

    private bool ShouldUseCropFillAspectMode(string aspectMode)
    {
        return FrameAngelPlayerMediaParity.IsCropFillAspectMode(aspectMode);
    }

    private Atom TryResolvePlayerHelperHostAtom(SuperController sc, string helperHostAtomUid)
    {
        if (sc == null || string.IsNullOrEmpty(helperHostAtomUid))
            return null;

        try
        {
            Atom direct = sc.GetAtomByUid(helperHostAtomUid);
            if (direct != null)
                return direct;
        }
        catch
        {
        }

        List<Atom> atoms = null;
        try
        {
            atoms = sc.GetAtoms();
        }
        catch
        {
            atoms = null;
        }

        if (atoms == null)
            return null;

        for (int i = 0; i < atoms.Count; i++)
        {
            Atom candidate = atoms[i];
            if (candidate == null)
                continue;

            string candidateUid = "";
            try
            {
                candidateUid = candidate.uid ?? "";
            }
            catch
            {
                candidateUid = "";
            }

            if (!string.Equals(candidateUid, helperHostAtomUid, StringComparison.Ordinal))
                continue;

            try
            {
                if (candidate.GetStorableByID("Image") != null)
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }

    private void TryRestoreScreenSurfaceMaterials(Renderer[] renderers, Material[][] materials)
    {
        if (renderers == null || materials == null)
            return;

        int count = Math.Min(renderers.Length, materials.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] original = materials[i];
            renderer.sharedMaterials = original != null ? (Material[])original.Clone() : new Material[0];
        }
    }

    private void DestroyAppliedScreenSurfaceMaterials(Material[][] materials)
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            Material[] row = materials[i];
            if (row == null)
                continue;

            for (int j = 0; j < row.Length; j++)
            {
                Material material = row[j];
                if (material == null)
                    continue;

                try
                {
                    UnityEngine.Object.Destroy(material);
                }
                catch
                {
                }
            }
        }
    }

    private bool TryCreateProjectedScreenMaterial(
        Material targetMaterial,
        Material sourceMaterial,
        bool preserveProjectedAlpha,
        out Material projectedMaterial)
    {
        projectedMaterial = null;
        if (sourceMaterial == null)
            return false;

        Material basis = targetMaterial != null ? targetMaterial : sourceMaterial;
        int basisRenderQueue = -1;
        try
        {
            if (basis != null)
                basisRenderQueue = basis.renderQueue;
        }
        catch
        {
            basisRenderQueue = -1;
        }

        try
        {
            projectedMaterial = new Material(basis);
            bool copiedTexture = TryCopyProjectedScreenTexture(sourceMaterial, projectedMaterial);
            if (!copiedTexture)
            {
                try
                {
                    projectedMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                }
                catch
                {
                }

                copiedTexture = HasProjectedScreenTexture(projectedMaterial) || TryCopyProjectedScreenTexture(sourceMaterial, projectedMaterial);
            }

            if (basisRenderQueue >= 0)
            {
                try
                {
                    projectedMaterial.renderQueue = basisRenderQueue + 25;
                }
                catch
                {
                }
            }

            if (!copiedTexture)
            {
                UnityEngine.Object.Destroy(projectedMaterial);
                projectedMaterial = null;
                return false;
            }

            TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
            return true;
        }
        catch
        {
            if (projectedMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }
            }

            projectedMaterial = null;
            return false;
        }
    }

    private bool TryCreateResolvedVideoTextureMaterial(
        Material targetMaterial,
        Material sourceMaterial,
        bool preferSimpleVideoShader,
        bool preserveProjectedAlpha,
        out Material projectedMaterial)
    {
        projectedMaterial = null;
        if (sourceMaterial == null)
            return false;

        Texture sourceTexture;
        Vector2 sourceScale;
        Vector2 sourceOffset;
        if (!TryResolveProjectedSourceTexture(sourceMaterial, out sourceTexture, out sourceScale, out sourceOffset)
            || sourceTexture == null)
            return false;

        return TryCreateResolvedVideoTextureMaterialFromTexture(
            targetMaterial,
            sourceTexture,
            sourceScale,
            sourceOffset,
            preferSimpleVideoShader,
            preserveProjectedAlpha,
            out projectedMaterial);
    }

    private bool TryCreateResolvedVideoTextureMaterialFromTexture(
        Material targetMaterial,
        Texture sourceTexture,
        Vector2 sourceScale,
        Vector2 sourceOffset,
        bool preferSimpleVideoShader,
        bool preserveProjectedAlpha,
        out Material projectedMaterial)
    {
        projectedMaterial = null;
        if (sourceTexture == null)
            return false;

        int basisRenderQueue = -1;
        try
        {
            Material basis = targetMaterial;
            if (basis != null)
                basisRenderQueue = basis.renderQueue;
        }
        catch
        {
            basisRenderQueue = -1;
        }

        try
        {
            if (preferSimpleVideoShader)
            {
                if (TryCreateDeterministicStandaloneVideoOverlayMaterial(
                    targetMaterial,
                    sourceTexture,
                    preserveProjectedAlpha,
                    out projectedMaterial,
                    basisRenderQueue))
                {
                    return true;
                }
            }

            Shader overlayShader = null;
            if (targetMaterial != null && !preferSimpleVideoShader)
            {
                try
                {
                    projectedMaterial = new Material(targetMaterial);
                    TrySetMaterialTexture(projectedMaterial, "_MainTex", sourceTexture);
                    TrySetMaterialTexture(projectedMaterial, "_BaseMap", sourceTexture);
                    TrySetMaterialTexture(projectedMaterial, "_EmissionMap", sourceTexture);
                    TrySetMaterialTextureScaleOffset(projectedMaterial, "_MainTex", sourceScale, sourceOffset);
                    TrySetMaterialTextureScaleOffset(projectedMaterial, "_BaseMap", sourceScale, sourceOffset);
                    TrySetMaterialTextureScaleOffset(projectedMaterial, "_EmissionMap", sourceScale, sourceOffset);

                    if (!HasProjectedScreenTexture(projectedMaterial))
                    {
                        try
                        {
                            projectedMaterial.mainTexture = sourceTexture;
                        }
                        catch
                        {
                        }
                    }

                    if (HasProjectedScreenTexture(projectedMaterial))
                    {
                        if (basisRenderQueue >= 0)
                        {
                            try
                            {
                                projectedMaterial.renderQueue = basisRenderQueue + 25;
                            }
                            catch
                            {
                            }
                        }

                        TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
                        return true;
                    }

                    UnityEngine.Object.Destroy(projectedMaterial);
                    projectedMaterial = null;
                }
                catch
                {
                    if (projectedMaterial != null)
                    {
                        try
                        {
                            UnityEngine.Object.Destroy(projectedMaterial);
                        }
                        catch
                        {
                        }
                    }

                    projectedMaterial = null;
                }
            }

            string[] overlayShaderCandidates = preserveProjectedAlpha
                ? new[]
                {
                    "Unlit/Transparent",
                    "Sprites/Default",
                    "UI/Default",
                    "Particles/Standard Unlit",
                    "Unlit/Texture",
                }
                : new[]
                {
                    "Unlit/Texture",
                    "Sprites/Default",
                    "Particles/Standard Unlit",
                    "Unlit/Transparent",
                    "UI/Default",
                };

            for (int i = 0; i < overlayShaderCandidates.Length; i++)
            {
                try
                {
                    overlayShader = Shader.Find(overlayShaderCandidates[i]);
                }
                catch
                {
                    overlayShader = null;
                }

                if (overlayShader != null)
                    break;
            }

            if (overlayShader != null)
            {
                projectedMaterial = new Material(overlayShader);
            }
            else
            {
                Material basis = targetMaterial;
                projectedMaterial = new Material(basis);
            }

            TrySetMaterialTexture(projectedMaterial, "_MainTex", sourceTexture);
            TrySetMaterialTexture(projectedMaterial, "_BaseMap", sourceTexture);
            TrySetMaterialTexture(projectedMaterial, "_EmissionMap", sourceTexture);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_MainTex", sourceScale, sourceOffset);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_BaseMap", sourceScale, sourceOffset);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_EmissionMap", sourceScale, sourceOffset);

            if (!HasProjectedScreenTexture(projectedMaterial))
            {
                try
                {
                    projectedMaterial.mainTexture = sourceTexture;
                }
                catch
                {
                }
            }

            if (basisRenderQueue >= 0)
            {
                try
                {
                    projectedMaterial.renderQueue = basisRenderQueue + 25;
                }
                catch
                {
                }
            }

            if (!HasProjectedScreenTexture(projectedMaterial))
            {
                UnityEngine.Object.Destroy(projectedMaterial);
                projectedMaterial = null;
                return false;
            }

            TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
            return true;
        }
        catch
        {
            if (projectedMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }
            }

            projectedMaterial = null;
            return false;
        }
    }

    private bool TryCreateDeterministicStandaloneVideoOverlayMaterial(
        Material targetMaterial,
        Texture sourceTexture,
        bool preserveProjectedAlpha,
        out Material projectedMaterial,
        int basisRenderQueue)
    {
        projectedMaterial = null;
        if (sourceTexture == null)
            return false;

        string[] deterministicShaderCandidates = preserveProjectedAlpha
            ? new[]
            {
                "Unlit/Transparent",
                "Sprites/Default",
                "UI/Default",
                "Particles/Standard Unlit",
                "Unlit/Texture",
            }
            : new[]
            {
                "Unlit/Texture",
                "Unlit/Transparent",
                "Particles/Standard Unlit",
                "UI/Default",
            };

        Shader overlayShader = null;
        for (int i = 0; i < deterministicShaderCandidates.Length; i++)
        {
            try
            {
                overlayShader = Shader.Find(deterministicShaderCandidates[i]);
            }
            catch
            {
                overlayShader = null;
            }

            if (overlayShader != null)
                break;
        }

        if (overlayShader == null && targetMaterial == null)
            return false;

        try
        {
            projectedMaterial = overlayShader != null
                ? new Material(overlayShader)
                : new Material(targetMaterial);

            TrySetMaterialTexture(projectedMaterial, "_MainTex", sourceTexture);
            TrySetMaterialTexture(projectedMaterial, "_BaseMap", sourceTexture);
            TrySetMaterialTexture(projectedMaterial, "_EmissionMap", sourceTexture);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_MainTex", Vector2.one, Vector2.zero);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_BaseMap", Vector2.one, Vector2.zero);
            TrySetMaterialTextureScaleOffset(projectedMaterial, "_EmissionMap", Vector2.one, Vector2.zero);

            try
            {
                projectedMaterial.mainTexture = sourceTexture;
                projectedMaterial.mainTextureScale = Vector2.one;
                projectedMaterial.mainTextureOffset = Vector2.zero;
            }
            catch
            {
            }

            if (basisRenderQueue >= 0)
            {
                try
                {
                    projectedMaterial.renderQueue = basisRenderQueue + 25;
                }
                catch
                {
                }
            }

            if (!HasProjectedScreenTexture(projectedMaterial))
            {
                UnityEngine.Object.Destroy(projectedMaterial);
                projectedMaterial = null;
                return false;
            }

            TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
            return true;
        }
        catch
        {
            if (projectedMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }
            }

            projectedMaterial = null;
            return false;
        }
    }

    private bool HasProjectedScreenTexture(Material material)
    {
        if (material == null)
            return false;

        Texture texture = TryGetMaterialTexture(material, "_MainTex");
        if (texture != null)
            return true;

        texture = TryGetMaterialTexture(material, "_BaseMap");
        if (texture != null)
            return true;

        texture = TryGetMaterialTexture(material, "_EmissionMap");
        if (texture != null)
            return true;

        try
        {
            return material.mainTexture != null;
        }
        catch
        {
            return false;
        }
    }

    private bool TryCopyProjectedScreenTexture(Material sourceMaterial, Material targetMaterial)
    {
        if (sourceMaterial == null || targetMaterial == null)
            return false;

        Texture sourceMainTexture;
        Vector2 sourceScale;
        Vector2 sourceOffset;
        if (!TryResolveProjectedSourceTexture(sourceMaterial, out sourceMainTexture, out sourceScale, out sourceOffset))
            return false;

        TrySetMaterialTexture(targetMaterial, "_MainTex", sourceMainTexture);
        TrySetMaterialTexture(targetMaterial, "_BaseMap", sourceMainTexture);
        TrySetMaterialTexture(targetMaterial, "_EmissionMap", sourceMainTexture);

        TrySetMaterialTextureScaleOffset(targetMaterial, "_MainTex", sourceScale, sourceOffset);
        TrySetMaterialTextureScaleOffset(targetMaterial, "_BaseMap", sourceScale, sourceOffset);
        TrySetMaterialTextureScaleOffset(targetMaterial, "_EmissionMap", sourceScale, sourceOffset);
        return true;
    }

    private bool TryResolveProjectedSourceTexture(
        Material sourceMaterial,
        out Texture sourceTexture,
        out Vector2 sourceScale,
        out Vector2 sourceOffset)
    {
        sourceTexture = null;
        sourceScale = Vector2.one;
        sourceOffset = Vector2.zero;

        if (sourceMaterial == null)
            return false;

        string[] preferredProperties = new[]
        {
            "_MainTex",
            "_BaseMap",
            "_EmissionMap",
            "_ColorMap",
            "_BaseColorMap",
            "_UnlitColorMap",
            "_Tex",
            "_Texture"
        };
        for (int i = 0; i < preferredProperties.Length; i++)
        {
            if (TryGetMaterialTextureWithScaleOffset(
                sourceMaterial,
                preferredProperties[i],
                out sourceTexture,
                out sourceScale,
                out sourceOffset))
                return true;
        }

        try
        {
            sourceTexture = sourceMaterial.mainTexture;
        }
        catch
        {
            sourceTexture = null;
        }

        if (sourceTexture != null)
        {
            for (int i = 0; i < preferredProperties.Length; i++)
                TryGetMaterialTextureScaleOffset(sourceMaterial, preferredProperties[i], ref sourceScale, ref sourceOffset);
            return true;
        }

        return false;
    }

    private bool TryResolveDirectImageControlTexture(
        JSONStorable imageStorable,
        Transform searchRoot,
        out Texture sourceTexture,
        out Vector2 sourceScale,
        out Vector2 sourceOffset,
        out string sourceName)
    {
        sourceTexture = null;
        sourceScale = Vector2.one;
        sourceOffset = Vector2.zero;
        sourceName = "";

        ImageControl imageControl = imageStorable as ImageControl;
        if (imageControl == null)
            return false;

        try
        {
            if (!imageControl.IsVideoReady())
            {
            }
        }
        catch
        {
        }

        try
        {
            var rawImage = imageControl.rawImage;
            Texture blankTexture = null;
            try
            {
                blankTexture = imageControl.blankTexture;
            }
            catch
            {
                blankTexture = null;
            }

            if (TryResolveRawImageTexture(rawImage, blankTexture, out sourceTexture, out sourceScale, out sourceOffset))
            {
                sourceName = "ImageControl.rawImage";
                return true;
            }

            if (searchRoot != null)
            {
                RawImage[] rawImages = searchRoot.GetComponentsInChildren<RawImage>(true);
                if (rawImages != null)
                {
                    for (int i = 0; i < rawImages.Length; i++)
                    {
                        RawImage candidateRawImage = rawImages[i];
                        if (candidateRawImage == null || candidateRawImage == rawImage)
                            continue;

                        if (!TryResolveRawImageTexture(candidateRawImage, blankTexture, out sourceTexture, out sourceScale, out sourceOffset))
                            continue;

                        sourceName = string.IsNullOrEmpty(candidateRawImage.name)
                            ? "RawImage.texture"
                            : "RawImage.texture:" + candidateRawImage.name;
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            Transform videoSearchRoot = searchRoot != null ? searchRoot : imageStorable.transform;
            VideoPlayer[] videoPlayers = videoSearchRoot != null
                ? videoSearchRoot.GetComponentsInChildren<VideoPlayer>(true)
                : null;
            if (videoPlayers == null)
                return false;

            for (int i = 0; i < videoPlayers.Length; i++)
            {
                VideoPlayer videoPlayer = videoPlayers[i];
                if (videoPlayer == null)
                    continue;

                Texture candidateTexture = null;
                try
                {
                    candidateTexture = videoPlayer.texture;
                }
                catch
                {
                    candidateTexture = null;
                }

                if (candidateTexture == null)
                {
                    try
                    {
                        candidateTexture = videoPlayer.targetTexture;
                    }
                    catch
                    {
                        candidateTexture = null;
                    }
                }

                if (candidateTexture == null)
                    continue;

                sourceTexture = candidateTexture;
                sourceScale = Vector2.one;
                sourceOffset = Vector2.zero;
                try
                {
                    sourceName = string.IsNullOrEmpty(videoPlayer.gameObject != null ? videoPlayer.gameObject.name : "")
                        ? "VideoPlayer.texture"
                        : "VideoPlayer.texture:" + videoPlayer.gameObject.name;
                }
                catch
                {
                    sourceName = "VideoPlayer.texture";
                }

                return true;
            }
        }
        catch
        {
        }

        sourceTexture = null;
        sourceScale = Vector2.one;
        sourceOffset = Vector2.zero;
        sourceName = "";
        return false;
    }

    private bool TryResolveRawImageTexture(
        RawImage rawImage,
        Texture blankTexture,
        out Texture sourceTexture,
        out Vector2 sourceScale,
        out Vector2 sourceOffset)
    {
        sourceTexture = null;
        sourceScale = Vector2.one;
        sourceOffset = Vector2.zero;
        if (rawImage == null)
            return false;

        Texture[] candidates = new Texture[4];
        int candidateCount = 0;

        try
        {
            candidates[candidateCount++] = rawImage.texture;
        }
        catch
        {
        }

        try
        {
            candidates[candidateCount++] = rawImage.mainTexture;
        }
        catch
        {
        }

        try
        {
            Material materialForRendering = rawImage.materialForRendering;
            if (materialForRendering != null)
                candidates[candidateCount++] = materialForRendering.mainTexture;
        }
        catch
        {
        }

        try
        {
            Material material = rawImage.material;
            if (material != null)
                candidates[candidateCount++] = material.mainTexture;
        }
        catch
        {
        }

        for (int i = 0; i < candidateCount; i++)
        {
            Texture candidate = candidates[i];
            if (candidate == null || candidate == blankTexture)
                continue;

            sourceTexture = candidate;
            try
            {
                Rect uvRect = rawImage.uvRect;
                sourceScale = new Vector2(uvRect.width, uvRect.height);
                sourceOffset = new Vector2(uvRect.x, uvRect.y);
            }
            catch
            {
                sourceScale = Vector2.one;
                sourceOffset = Vector2.zero;
            }

            return true;
        }

        return false;
    }

    private bool TryGetMaterialTextureWithScaleOffset(
        Material material,
        string propertyName,
        out Texture texture,
        out Vector2 scale,
        out Vector2 offset)
    {
        texture = null;
        scale = Vector2.one;
        offset = Vector2.zero;

        if (material == null || string.IsNullOrEmpty(propertyName))
            return false;

        try
        {
            if (!material.HasProperty(propertyName))
                return false;

            texture = material.GetTexture(propertyName);
            if (texture == null)
                return false;

            scale = material.GetTextureScale(propertyName);
            offset = material.GetTextureOffset(propertyName);
            return true;
        }
        catch
        {
            texture = null;
            scale = Vector2.one;
            offset = Vector2.zero;
            return false;
        }
    }

    private bool TryCreateFallbackProjectedScreenMaterial(
        Material targetMaterial,
        Material sourceMaterial,
        bool preserveProjectedAlpha,
        out Material projectedMaterial)
    {
        projectedMaterial = null;
        if (sourceMaterial == null)
            return false;

        Texture sourceTexture;
        Vector2 ignoredScale;
        Vector2 ignoredOffset;
        if (!TryResolveProjectedSourceTexture(sourceMaterial, out sourceTexture, out ignoredScale, out ignoredOffset)
            || sourceTexture == null)
            return false;

        int basisRenderQueue = -1;
        try
        {
            Material basis = targetMaterial != null ? targetMaterial : sourceMaterial;
            if (basis != null)
                basisRenderQueue = basis.renderQueue;
        }
        catch
        {
            basisRenderQueue = -1;
        }

        try
        {
            projectedMaterial = new Material(sourceMaterial);

            if (basisRenderQueue >= 0)
            {
                try
                {
                    projectedMaterial.renderQueue = basisRenderQueue + 25;
                }
                catch
                {
                }
            }

            try
            {
                if (projectedMaterial.HasProperty("_Cull"))
                    projectedMaterial.SetInt("_Cull", 2);
            }
            catch
            {
            }

            TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
            return true;
        }
        catch
        {
            if (projectedMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }
            }

            projectedMaterial = null;
            return false;
        }
    }

    private bool TryCreateLiveHelperOverlayMaterial(
        Material targetMaterial,
        Material sourceMaterial,
        bool preserveProjectedAlpha,
        out Material projectedMaterial)
    {
        projectedMaterial = null;
        if (sourceMaterial == null)
            return false;

        int basisRenderQueue = -1;
        try
        {
            Material basis = targetMaterial != null ? targetMaterial : sourceMaterial;
            if (basis != null)
                basisRenderQueue = basis.renderQueue;
        }
        catch
        {
            basisRenderQueue = -1;
        }

        try
        {
            projectedMaterial = new Material(sourceMaterial);

            if (basisRenderQueue >= 0)
            {
                try
                {
                    projectedMaterial.renderQueue = basisRenderQueue + 25;
                }
                catch
                {
                }
            }

            TryFinalizeProjectedScreenMaterialVisibility(projectedMaterial, preserveProjectedAlpha);
            return true;
        }
        catch
        {
            if (projectedMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }
            }

            projectedMaterial = null;
            return false;
        }
    }

    private Texture TryGetMaterialTexture(Material material, string propertyName)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return null;

        try
        {
            if (!material.HasProperty(propertyName))
                return null;
            return material.GetTexture(propertyName);
        }
        catch
        {
            return null;
        }
    }

    private void TrySetMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (material == null || string.IsNullOrEmpty(propertyName) || texture == null)
            return;

        try
        {
            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }
        catch
        {
        }
    }

    private void TryGetMaterialTextureScaleOffset(Material material, string propertyName, ref Vector2 scale, ref Vector2 offset)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            if (!material.HasProperty(propertyName))
                return;
            scale = material.GetTextureScale(propertyName);
            offset = material.GetTextureOffset(propertyName);
        }
        catch
        {
        }
    }

    private void TrySetMaterialTextureScaleOffset(Material material, string propertyName, Vector2 scale, Vector2 offset)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            if (!material.HasProperty(propertyName))
                return;
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
        }
        catch
        {
        }
    }

    private void TryForceProjectedScreenVisibility(Material material)
    {
        if (material == null)
            return;

        TrySetMaterialColor(material, "_Color", new Color(1f, 1f, 1f, 1f));
        TrySetMaterialColor(material, "_BaseColor", new Color(1f, 1f, 1f, 1f));
        TrySetMaterialColor(material, "_EmissionColor", Color.white);

        try
        {
            material.EnableKeyword("_EMISSION");
        }
        catch
        {
        }

        TrySetMaterialFloat(material, "_Mode", 0f);
        TrySetMaterialFloat(material, "_Surface", 0f);
        TrySetMaterialFloat(material, "_SrcBlend", 1f);
        TrySetMaterialFloat(material, "_DstBlend", 0f);
        TrySetMaterialFloat(material, "_Blend", 0f);
        TrySetMaterialFloat(material, "_AlphaClip", 0f);
        TrySetMaterialFloat(material, "_AlphaToMask", 0f);
        TrySetMaterialFloat(material, "_ZWrite", 0f);
        TrySetMaterialFloat(material, "_Cull", 0f);

        try
        {
            material.DisableKeyword("_ALPHATEST_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_ALPHABLEND_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        catch
        {
        }

        try
        {
            material.SetOverrideTag("RenderType", "Opaque");
        }
        catch
        {
        }

        try
        {
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        catch
        {
        }

        try
        {
            if (material.renderQueue < 2450)
                material.renderQueue = 2450;
        }
        catch
        {
        }
    }

    private void TryFinalizeProjectedScreenMaterialVisibility(Material material, bool preserveProjectedAlpha)
    {
        if (preserveProjectedAlpha)
            TryPreserveProjectedScreenAlphaVisibility(material);
        else
            TryForceProjectedScreenVisibility(material);
    }

    private void TryPreserveProjectedScreenAlphaVisibility(Material material)
    {
        if (material == null)
            return;

        TrySetMaterialColor(material, "_Color", new Color(1f, 1f, 1f, 1f));
        TrySetMaterialColor(material, "_BaseColor", new Color(1f, 1f, 1f, 1f));
        TrySetMaterialColor(material, "_EmissionColor", Color.white);

        try
        {
            material.EnableKeyword("_EMISSION");
        }
        catch
        {
        }

        TrySetMaterialFloat(material, "_Cull", 0f);

        try
        {
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        catch
        {
        }

        try
        {
            if (material.renderQueue < 2450)
                material.renderQueue = 2450;
        }
        catch
        {
        }
    }

    private bool ShouldPreserveProjectedScreenAlpha(InnerPieceScreenSlotRuntimeRecord slot)
    {
        return slot != null && ShouldPreserveProjectedScreenAlpha(slot.surfaceTargetId);
    }

    private bool ShouldPreserveProjectedScreenAlpha(string surfaceTargetId)
    {
        if (string.IsNullOrEmpty(surfaceTargetId))
            return false;

        string normalized = surfaceTargetId.Trim().Replace('-', '_').ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            return false;

        return normalized.EndsWith(":alpha", StringComparison.Ordinal)
            || normalized.EndsWith("_alpha", StringComparison.Ordinal)
            || normalized.Contains("screen_alpha")
            || normalized.Contains("preserve_alpha");
    }

    private void TrySetMaterialColor(Material material, string propertyName, Color color)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, color);
        }
        catch
        {
        }
    }

    private void TrySetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }
        catch
        {
        }
    }

    private Material FindBestRendererMaterial(Renderer[] renderers)
    {
        List<ProjectedMaterialCandidate> candidates = FindProjectedSourceMaterialCandidates(renderers);
        if (candidates == null || candidates.Count <= 0)
            return null;

        ProjectedMaterialCandidate bestCandidate = candidates[0];
        return bestCandidate != null ? bestCandidate.material : null;
    }

    private List<ProjectedMaterialCandidate> FindProjectedSourceMaterialCandidates(Renderer[] renderers)
    {
        List<ProjectedMaterialCandidate> candidates = new List<ProjectedMaterialCandidate>();
        if (renderers == null)
            return candidates;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            string rendererName = "";
            try
            {
                rendererName = renderer.gameObject != null ? (renderer.gameObject.name ?? "") : "";
            }
            catch
            {
                rendererName = "";
            }

            Material[] liveMaterials = null;
            try
            {
                liveMaterials = renderer.materials;
            }
            catch
            {
                liveMaterials = null;
            }

            MergeProjectedMaterialCandidates(candidates, rendererName, liveMaterials);
            MergeProjectedMaterialCandidates(candidates, rendererName, renderer.sharedMaterials);
        }

        candidates.Sort((left, right) =>
        {
            int leftScore = left != null ? left.score : int.MinValue;
            int rightScore = right != null ? right.score : int.MinValue;
            return rightScore.CompareTo(leftScore);
        });

        return candidates;
    }

    private void MergeProjectedMaterialCandidates(
        List<ProjectedMaterialCandidate> candidates,
        string rendererName,
        Material[] materials)
    {
        if (candidates == null || materials == null)
            return;

        for (int j = 0; j < materials.Length; j++)
        {
            Material candidate = materials[j];
            if (candidate == null)
                continue;

            int score = ScoreRendererMaterialCandidate(rendererName, candidate);
            bool merged = false;
            for (int k = 0; k < candidates.Count; k++)
            {
                ProjectedMaterialCandidate existing = candidates[k];
                if (existing == null || existing.material != candidate)
                    continue;

                if (score > existing.score)
                    existing.score = score;
                merged = true;
                break;
            }

                if (!merged)
                candidates.Add(new ProjectedMaterialCandidate { material = candidate, score = score, rendererName = rendererName ?? "" });
        }
    }

    private string BuildPlayerScreenBindingDebugJson(
        string helperHostAtomUid,
        Atom helperAtom,
        GameObject mediaTargetObject,
        string attachMode,
        string projectionMode,
        Renderer[] sourceRenderers,
        List<ProjectedMaterialCandidate> sourceCandidates,
        ProjectedMaterialCandidate selectedCandidate,
        Renderer[] appliedRenderers,
        Material projectedMaterial,
        Texture directSourceTexture)
    {
        string helperAtomUid = "";
        try
        {
            helperAtomUid = helperAtom != null ? (helperAtom.uid ?? "") : "";
        }
        catch
        {
            helperAtomUid = "";
        }

        Material sourceMaterial = selectedCandidate != null ? selectedCandidate.material : null;
        Texture selectedTexture = directSourceTexture;
        if (selectedTexture == null)
            selectedTexture = TryGetFirstProjectedTexture(sourceMaterial);
        bool selectedTextureIsRenderTexture = selectedTexture is RenderTexture;
        bool selectedCandidateLooksLikeHelper = selectedCandidate != null
            && IsLikelyHelperOrFallbackMaterialCandidate(selectedCandidate.rendererName, sourceMaterial);

        return "{"
            + "\"helperHostAtomUid\":\"" + EscapeJsonString(helperHostAtomUid ?? "") + "\""
            + ",\"helperAtomUid\":\"" + EscapeJsonString(helperAtomUid) + "\""
            + ",\"mediaTargetObject\":\"" + EscapeJsonString(mediaTargetObject != null ? (mediaTargetObject.name ?? "") : "") + "\""
            + ",\"attachMode\":\"" + EscapeJsonString(attachMode ?? "") + "\""
            + ",\"projectionMode\":\"" + EscapeJsonString(projectionMode ?? "") + "\""
            + ",\"sourceRendererCount\":" + (sourceRenderers != null ? sourceRenderers.Length.ToString() : "0")
            + ",\"sourceCandidateCount\":" + (sourceCandidates != null ? sourceCandidates.Count.ToString() : "0")
            + ",\"selectedRendererName\":\"" + EscapeJsonString(selectedCandidate != null ? (selectedCandidate.rendererName ?? "") : "") + "\""
            + ",\"selectedShader\":\"" + EscapeJsonString(DescribeMaterialShader(sourceMaterial)) + "\""
            + ",\"selectedTexture\":\"" + EscapeJsonString(DescribeTexture(selectedTexture)) + "\""
            + ",\"selectedTextureType\":\"" + EscapeJsonString(DescribeTextureType(selectedTexture)) + "\""
            + ",\"selectedTextureIsRenderTexture\":" + (selectedTextureIsRenderTexture ? "true" : "false")
            + ",\"selectedCandidateLooksLikeHelper\":" + (selectedCandidateLooksLikeHelper ? "true" : "false")
            + ",\"appliedRendererCount\":" + (appliedRenderers != null ? appliedRenderers.Length.ToString() : "0")
            + ",\"appliedShader\":\"" + EscapeJsonString(DescribeMaterialShader(projectedMaterial)) + "\""
            + ",\"appliedTexture\":\"" + EscapeJsonString(DescribeMaterialTexture(projectedMaterial)) + "\""
            + ",\"appliedTextureType\":\"" + EscapeJsonString(DescribeMaterialTextureType(projectedMaterial)) + "\""
            + "}";
    }

    private string DescribeMaterialShader(Material material)
    {
        if (material == null)
            return "";

        try
        {
            return material.shader != null ? (material.shader.name ?? "") : "";
        }
        catch
        {
            return "";
        }
    }

    private string DescribeMaterialTexture(Material material)
    {
        Texture texture = TryGetFirstProjectedTexture(material);
        return DescribeTexture(texture);
    }

    private string DescribeMaterialTextureType(Material material)
    {
        Texture texture = TryGetFirstProjectedTexture(material);
        return DescribeTextureType(texture);
    }

    private string DescribeTexture(Texture texture)
    {
        if (texture == null)
            return "";

        if (texture is RenderTexture)
        {
            try
            {
                return texture.name ?? "";
            }
            catch
            {
                return "";
            }
        }

        try
        {
            return texture.name ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string DescribeTextureType(Texture texture)
    {
        if (texture == null)
            return "";

        if (texture is RenderTexture)
            return "RenderTexture";

        if (texture is Texture2D)
            return "Texture2D";

        return "Texture";
    }

    private string DescribeMaterialName(Material material)
    {
        if (material == null)
            return "";

        try
        {
            return material.name ?? "";
        }
        catch
        {
            return "";
        }
    }

    private bool TryResolveCandidateProjectedTexture(
        Material material,
        out Texture texture)
    {
        texture = null;
        if (material == null)
            return false;

        Vector2 ignoredScale;
        Vector2 ignoredOffset;
        if (!TryResolveProjectedSourceTexture(material, out texture, out ignoredScale, out ignoredOffset))
            return false;

        return texture != null;
    }

    private bool IsLikelyHelperOrFallbackMaterialCandidate(string rendererName, Material material)
    {
        string combined = ((rendererName ?? "") + " " + DescribeMaterialName(material) + " " + DescribeMaterialShader(material)).ToLowerInvariant();
        if (string.IsNullOrEmpty(combined))
            return false;

        return combined.Contains("helper")
            || combined.Contains("disconnect")
            || combined.Contains("background")
            || combined.Contains("backdrop")
            || combined.Contains("frame")
            || combined.Contains("border")
            || combined.Contains("fallback")
            || combined.Contains("blank");
    }

    private bool IsLikelyMediaBearingMaterialCandidate(string rendererName, Material material)
    {
        string combined = ((rendererName ?? "") + " " + DescribeMaterialName(material) + " " + DescribeMaterialShader(material)).ToLowerInvariant();
        if (string.IsNullOrEmpty(combined))
            return false;

        return combined.Contains("screen")
            || combined.Contains("image")
            || combined.Contains("media")
            || combined.Contains("video")
            || combined.Contains("rawimage");
    }

    private bool IsAuthoredScreenSafeProjectedSourceCandidate(ProjectedMaterialCandidate candidate)
    {
        if (candidate == null || candidate.material == null)
            return false;

        Texture sourceTexture;
        if (TryResolveCandidateProjectedTexture(candidate.material, out sourceTexture) && sourceTexture is RenderTexture)
            return true;

        return !IsLikelyHelperOrFallbackMaterialCandidate(candidate.rendererName, candidate.material);
    }

    private Texture TryGetFirstProjectedTexture(Material material)
    {
        if (material == null)
            return null;

        Texture texture = TryGetMaterialTexture(material, "_MainTex");
        if (texture != null)
            return texture;

        texture = TryGetMaterialTexture(material, "_BaseMap");
        if (texture != null)
            return texture;

        texture = TryGetMaterialTexture(material, "_EmissionMap");
        if (texture != null)
            return texture;

        try
        {
            return material.mainTexture;
        }
        catch
        {
            return null;
        }
    }

    private int ScoreRendererMaterialCandidate(string rendererName, Material material)
    {
        if (material == null)
            return int.MinValue;

        int score = 0;
        string normalizedRendererName = (rendererName ?? "").ToLowerInvariant();
        string shaderName = "";
        try
        {
            shaderName = material.shader != null ? (material.shader.name ?? "") : "";
        }
        catch
        {
            shaderName = "";
        }

        string normalizedShaderName = shaderName.ToLowerInvariant();

        if (normalizedRendererName.Contains("screen") || normalizedRendererName.Contains("image") || normalizedRendererName.Contains("media"))
            score += 15;
        if (normalizedRendererName.Contains("background") || normalizedRendererName.Contains("frame") || normalizedRendererName.Contains("border"))
            score -= 10;

        if (IsLikelyMediaBearingMaterialCandidate(rendererName, material))
            score += 20;
        if (IsLikelyHelperOrFallbackMaterialCandidate(rendererName, material))
            score -= 80;

        if (normalizedShaderName.Contains("unlit"))
            score += 10;
        if (normalizedShaderName.Contains("transparent"))
            score += 5;
        if (normalizedShaderName.Contains("standard"))
            score -= 2;

        score += ScoreMaterialTexturePresence(material, "_MainTex", 40);
        score += ScoreMaterialTexturePresence(material, "_BaseMap", 35);
        score += ScoreMaterialTexturePresence(material, "_EmissionMap", 20);

        try
        {
            Texture mainTexture = material.mainTexture;
            if (mainTexture != null)
                score += 45;
        }
        catch
        {
        }

        Texture resolvedTexture;
        if (TryResolveCandidateProjectedTexture(material, out resolvedTexture) && resolvedTexture is RenderTexture)
            score += 80;

        return score;
    }

    private int ScoreMaterialTexturePresence(Material material, string propertyName, int score)
    {
        if (material == null || string.IsNullOrEmpty(propertyName))
            return 0;

        try
        {
            if (!material.HasProperty(propertyName))
                return 0;

            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
                return 0;

            int totalScore = score;
            if (texture is RenderTexture)
                totalScore += 120;
            return totalScore;
        }
        catch
        {
            return 0;
        }
    }

    private string BuildPlayerReceiptPayload(string actionId, string summary, string stateJson, string consumerReceiptJson)
    {
        return "{"
            + "\"schemaVersion\":\"" + EscapeJsonString(PlayerReceiptSchemaVersion) + "\""
            + ",\"actionId\":\"" + EscapeJsonString(actionId ?? "") + "\""
            + ",\"summary\":\"" + EscapeJsonString(summary ?? "") + "\""
            + ",\"state\":" + (string.IsNullOrEmpty(stateJson) ? "{}" : stateJson)
            + ",\"consumerReceipt\":" + (string.IsNullOrEmpty(consumerReceiptJson) ? "{}" : consumerReceiptJson)
            + "}";
    }

    private bool ShouldUseCleanRoomPlayerPath(string argsJson, string atomUid)
    {
        return !string.IsNullOrEmpty(atomUid)
            || HasStandalonePlayerSelector(argsJson);
    }

    private string BuildSelectedPlayerStateJson(string argsJson)
    {
        if (ShouldUseCleanRoomPlayerPath(argsJson, ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid")))
        {
            if (TryBuildStandaloneBoundPlayerStateJson(argsJson, out string standaloneBoundStateJson))
                return standaloneBoundStateJson;

            return BuildPendingCleanRoomPlayerStateJson(argsJson);
        }

        JSONStorable consumer = ResolvePlayerConsumer(argsJson);
        if (TryReadPlayerStateJson(consumer, out string stateJson))
            return TryAppendCurrentBindingDebugJson(argsJson, stateJson);

        if (TryBuildStandaloneBoundPlayerStateJson(argsJson, out string standaloneStateJson))
            return standaloneStateJson;

        return BuildPendingCleanRoomPlayerStateJson(argsJson, "player consumer not found");
    }

    private string BuildPendingCleanRoomPlayerStateJson(string argsJson, string fallbackError = "")
    {
        string atomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        string instanceId = ExtractJsonArgString(argsJson, "instanceId");
        string slotId = ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId");
        string surfaceTargetId = ExtractJsonArgString(argsJson, "surfaceTargetId", "screenSurfaceTargetId", "targetSurfaceId");
        StandalonePlayerRecord record = null;
        string resolveError = "";

        if (HasStandalonePlayerSelector(argsJson))
            TryResolveStandalonePlayerRecord(argsJson, out record, out resolveError);

        string mediaPath = "";
        string mediaMode = "";
        bool hasMedia = false;
        string lastError = fallbackError ?? "";

        if (record != null)
        {
            mediaPath = record.mediaPath ?? "";
            mediaMode = !string.IsNullOrEmpty(record.mediaPath) ? "path" : "";
            hasMedia = !string.IsNullOrEmpty(record.mediaPath) || (record.playlistPaths != null && record.playlistPaths.Count > 0);
            if (string.IsNullOrEmpty(lastError))
                lastError = record.lastError ?? "";
            if (string.IsNullOrEmpty(instanceId))
                instanceId = record.instanceId ?? "";
            if (string.IsNullOrEmpty(slotId))
                slotId = !string.IsNullOrEmpty(record.slotId) ? record.slotId : (record.displayId ?? "");
        }
        else if (string.IsNullOrEmpty(lastError))
        {
            lastError = resolveError ?? "";
        }

        if (string.IsNullOrEmpty(lastError))
            lastError = "screen owner unresolved";

        if (string.IsNullOrEmpty(surfaceTargetId))
            surfaceTargetId = "player:screen";

        return "{"
            + "\"schemaVersion\":\"" + EscapeJsonString(PlayerStateSchemaVersion) + "\""
            + ",\"consumerId\":\"player\""
            + ",\"consumerLabel\":\"Scene Runtime Player\""
            + ",\"atomUid\":\"" + EscapeJsonString(atomUid) + "\""
            + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
            + ",\"mediaMode\":\"" + EscapeJsonString(mediaMode) + "\""
            + ",\"shellId\":\"player\""
            + ",\"playing\":false"
            + ",\"scrubNormalized\":0"
            + ",\"hasMedia\":" + (hasMedia ? "true" : "false")
            + ",\"screenContractVersion\":\"\""
            + ",\"screenBindingMode\":\"session_scene_surface\""
            + ",\"screenBound\":false"
            + ",\"screenBoundState\":\"pending_scene_bind\""
            + ",\"boundInstanceId\":\"" + EscapeJsonString(instanceId) + "\""
            + ",\"boundScreenSlotId\":\"" + EscapeJsonString(slotId) + "\""
            + ",\"disconnectStateId\":\"\""
            + ",\"screenSurfaceTargetId\":\"" + EscapeJsonString(surfaceTargetId) + "\""
            + ",\"embeddedHostAtomUid\":\"" + EscapeJsonString(atomUid) + "\""
            + ",\"harpmoteState\":{}"
            + ",\"lastError\":\"" + EscapeJsonString(lastError) + "\""
            + "}";
    }

    private bool TryBuildStandaloneBoundPlayerStateJson(string argsJson, out string stateJson)
    {
        stateJson = "{}";

        string atomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        PlayerScreenBindingRecord binding;
        if (!TryResolvePlayerScreenBinding(argsJson, atomUid, out binding) || binding == null)
            return false;

        StandalonePlayerRecord record;
        TryResolveStandalonePlayerRecordForScreenBinding(binding, out record);

        bool playing = false;
        double scrubNormalized = 0d;
        bool hasMedia = false;
        string mediaPath = "";
        string mediaMode = "";
        string lastError = "";

        if (record != null)
        {
            mediaPath = record.mediaPath ?? "";
            mediaMode = !string.IsNullOrEmpty(record.mediaPath) ? "path" : "";
            hasMedia = !string.IsNullOrEmpty(record.mediaPath) || (record.playlistPaths != null && record.playlistPaths.Count > 0);
            lastError = record.lastError ?? "";

            try
            {
                if (record.videoPlayer != null)
                    playing = record.videoPlayer.isPlaying;
            }
            catch
            {
                playing = false;
            }

            double currentTimeSeconds = 0d;
            double currentDurationSeconds = 0d;
            try
            {
                if (record.videoPlayer != null)
                {
                    currentTimeSeconds = Math.Max(0d, record.videoPlayer.time);
                    currentDurationSeconds = GetStandalonePlayerDurationSeconds(record);
                }
            }
            catch
            {
                currentTimeSeconds = 0d;
                currentDurationSeconds = 0d;
            }

            if (!double.IsNaN(currentDurationSeconds)
                && !double.IsInfinity(currentDurationSeconds)
                && currentDurationSeconds > 0.0001d)
            {
                scrubNormalized = Math.Max(0d, Math.Min(1d, currentTimeSeconds / currentDurationSeconds));
            }
        }

        string resolvedAtomUid = !string.IsNullOrEmpty(atomUid) ? atomUid : (binding.atomUid ?? "");
        string embeddedHostAtomUid = !string.IsNullOrEmpty(binding.embeddedHostAtomUid)
            ? binding.embeddedHostAtomUid
            : resolvedAtomUid;
        string debugJson = string.IsNullOrEmpty(binding.debugJson) ? "{}" : binding.debugJson;

        stateJson = "{"
            + "\"schemaVersion\":\"" + EscapeJsonString(PlayerStateSchemaVersion) + "\""
            + ",\"consumerId\":\"player\""
            + ",\"consumerLabel\":\"Scene Runtime Player\""
            + ",\"atomUid\":\"" + EscapeJsonString(resolvedAtomUid) + "\""
            + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
            + ",\"mediaMode\":\"" + EscapeJsonString(mediaMode) + "\""
            + ",\"shellId\":\"player\""
            + ",\"playing\":" + (playing ? "true" : "false")
            + ",\"scrubNormalized\":" + scrubNormalized.ToString("0.######", CultureInfo.InvariantCulture)
            + ",\"hasMedia\":" + (hasMedia ? "true" : "false")
            + ",\"screenContractVersion\":\"" + EscapeJsonString(binding.screenContractVersion ?? "") + "\""
            + ",\"screenBindingMode\":\"" + EscapeJsonString(ResolvePlayerScreenBindingMode(binding)) + "\""
            + ",\"screenBound\":true"
            + ",\"screenBoundState\":\"screen_bound\""
            + ",\"boundInstanceId\":\"" + EscapeJsonString(binding.instanceId ?? "") + "\""
            + ",\"boundScreenSlotId\":\"" + EscapeJsonString(binding.slotId ?? "") + "\""
            + ",\"disconnectStateId\":\"" + EscapeJsonString(binding.disconnectStateId ?? "") + "\""
            + ",\"screenSurfaceTargetId\":\"" + EscapeJsonString(binding.surfaceTargetId ?? "player:screen") + "\""
            + ",\"embeddedHostAtomUid\":\"" + EscapeJsonString(embeddedHostAtomUid) + "\""
            + ",\"harpmoteState\":{}"
            + ",\"lastError\":\"" + EscapeJsonString(lastError) + "\""
            + ",\"screenDebug\":" + debugJson
            + "}";
        return true;
    }

    private string TryAppendCurrentBindingDebugJson(string argsJson, string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return stateJson;

        string atomUid = ExtractJsonArgString(stateJson, "atomUid");
        if (string.IsNullOrEmpty(atomUid))
            atomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        if (string.IsNullOrEmpty(atomUid))
            return stateJson;

        PlayerScreenBindingRecord binding;
        if (!playerScreenBindings.TryGetValue(atomUid, out binding) || binding == null || string.IsNullOrEmpty(binding.debugJson))
            return stateJson;

        return AppendJsonProperty(stateJson, "screenDebug", binding.debugJson);
    }

    private string AppendJsonProperty(string json, string propertyName, string propertyValueJson)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyValueJson))
            return json;

        int insertIndex = json.LastIndexOf('}');
        if (insertIndex <= 0)
            return json;

        return json.Substring(0, insertIndex)
            + ",\"" + EscapeJsonString(propertyName) + "\":" + propertyValueJson
            + json.Substring(insertIndex);
    }

    private JSONStorable ResolvePlayerConsumer(string argsJson)
    {
        string requestedAtomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        try
        {
            if (!string.IsNullOrEmpty(requestedAtomUid))
            {
                JSONStorable requestedConsumer;
                if (TryFindPlayerConsumerOnAtom(requestedAtomUid, out requestedConsumer))
                    return requestedConsumer;
            }

            List<JSONStorable> consumers = null;
            try
            {
                consumers = FindPlayerConsumers();
            }
            catch
            {
                consumers = null;
            }

            if (consumers == null || consumers.Count <= 0)
                return null;

            if (!string.IsNullOrEmpty(requestedAtomUid))
            {
                for (int i = 0; i < consumers.Count; i++)
                {
                    if (ConsumerBelongsToAtom(consumers[i], requestedAtomUid))
                        return consumers[i];
                }
            }

            for (int i = 0; i < consumers.Count; i++)
            {
                if (consumers[i] != null)
                    return consumers[i];
            }
        }
        catch
        {
        }

        return null;
    }

    private List<JSONStorable> FindPlayerConsumers()
    {
        return FindConsumers(PlayerActionName, PlayerStateFieldName);
    }

    private List<JSONStorable> FindConsumers(string actionName, string stateFieldName)
    {
        List<JSONStorable> matches = new List<JSONStorable>();
        SuperController sc = SuperController.singleton;
        if (sc == null)
            return matches;

        List<Atom> atoms = sc.GetAtoms();
        if (atoms == null)
            return matches;

        for (int i = 0; i < atoms.Count; i++)
        {
            Atom atom = atoms[i];
            if (atom == null)
                continue;

            List<string> storableIds = null;
            try
            {
                storableIds = atom.GetStorableIDs();
            }
            catch
            {
                storableIds = null;
            }

            if (storableIds == null)
                continue;

            for (int j = 0; j < storableIds.Count; j++)
            {
                JSONStorable storable = null;
                try
                {
                    storable = atom.GetStorableByID(storableIds[j]);
                }
                catch
                {
                    storable = null;
                }

                if (storable == null)
                    continue;

                JSONStorableString stateField = null;
                JSONStorableAction action = null;
                try
                {
                    stateField = storable.GetStringJSONParam(stateFieldName);
                    action = storable.GetAction(actionName);
                }
                catch
                {
                    stateField = null;
                    action = null;
                }

                if (stateField == null || action == null)
                    continue;

                matches.Add(storable);
            }
        }

        return matches;
    }

    private bool ConsumerBelongsToAtom(JSONStorable storable, string atomUid)
    {
        if (storable == null || string.IsNullOrEmpty(atomUid))
            return false;

        try
        {
            Atom atom = storable.containingAtom;
            return atom != null && string.Equals(atom.uid, atomUid, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string ResolvePlayerConsumerAtomUid(JSONStorable consumer, string argsJson)
    {
        string requestedAtomUid = ExtractJsonArgString(argsJson, "hostAtomUid", "atomUid", "targetAtomUid");
        if (!string.IsNullOrEmpty(requestedAtomUid))
            return requestedAtomUid;

        string stateJson;
        if (TryReadPlayerStateJson(consumer, out stateJson))
        {
            string stateAtomUid = ExtractJsonArgString(stateJson, "atomUid");
            if (!string.IsNullOrEmpty(stateAtomUid))
                return stateAtomUid;
        }

        try
        {
            Atom atom = consumer != null ? consumer.containingAtom : null;
            if (atom != null && !string.IsNullOrEmpty(atom.uid))
                return atom.uid;
        }
        catch
        {
        }

        return "";
    }

    private bool TryReadPlayerStateJson(JSONStorable consumer, out string stateJson)
    {
        stateJson = "";
        JSONStorableString stateField = null;
        try
        {
            stateField = consumer != null ? consumer.GetStringJSONParam(PlayerStateFieldName) : null;
        }
        catch
        {
            stateField = null;
        }
        if (stateField == null)
            return false;

        try
        {
            stateJson = stateField.val ?? "";
        }
        catch
        {
            stateJson = "";
        }

        return !string.IsNullOrEmpty(stateJson);
    }

    private bool TryExecutePlayerCommand(JSONStorable consumer, string commandJson, out string receiptJson)
    {
        receiptJson = "{}";
        if (consumer == null)
            return false;

        JSONStorableString commandField = null;
        JSONStorableString lastReceiptField = null;
        JSONStorableAction action = null;
        try
        {
            commandField = consumer.GetStringJSONParam(PlayerCommandFieldName);
            lastReceiptField = consumer.GetStringJSONParam(PlayerLastReceiptFieldName);
            action = consumer.GetAction(PlayerActionName);
        }
        catch
        {
            commandField = null;
            lastReceiptField = null;
            action = null;
        }

        if (commandField == null || action == null)
            return false;

        try
        {
            commandField.val = string.IsNullOrEmpty(commandJson) ? "{}" : commandJson;
            action.actionCallback();
            receiptJson = lastReceiptField != null ? (lastReceiptField.val ?? "{}") : "{}";
        }
        catch
        {
            receiptJson = "{}";
            return false;
        }

        return receiptJson.IndexOf("\"ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryFindPlayerConsumerOnAtom(string atomUid, out JSONStorable consumer)
    {
        consumer = null;
        if (string.IsNullOrEmpty(atomUid))
            return false;

        SuperController sc = SuperController.singleton;
        if (sc == null)
            return false;

        Atom atom = null;
        try
        {
            atom = sc.GetAtomByUid(atomUid);
        }
        catch
        {
            atom = null;
        }

        if (atom == null)
            return false;

        List<string> storableIds = null;
        try
        {
            storableIds = atom.GetStorableIDs();
        }
        catch
        {
            storableIds = null;
        }

        if (storableIds == null)
            return false;

        for (int i = 0; i < storableIds.Count; i++)
        {
            JSONStorable candidate = null;
            try
            {
                candidate = atom.GetStorableByID(storableIds[i]);
            }
            catch
            {
                candidate = null;
            }

            if (candidate == null)
                continue;

            JSONStorableString stateField = null;
            JSONStorableAction action = null;
            try
            {
                stateField = candidate.GetStringJSONParam(PlayerStateFieldName);
                action = candidate.GetAction(PlayerActionName);
            }
            catch
            {
                stateField = null;
                action = null;
            }

            if (stateField == null || action == null)
                continue;

            consumer = candidate;
            return true;
        }

        return false;
    }

    private bool TrySetStandalonePlayerLoopMode(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string requestedMode = ExtractJsonArgString(argsJson, "loopMode", "mode", "value");
        if (string.IsNullOrEmpty(requestedMode) && !HasStandalonePlayerLoopModeArg(argsJson))
        {
            errorMessage = "loopMode is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.loopMode = ResolveStandalonePlayerLoopMode(argsJson, record.loopMode, record != null ? record.playlistPaths.Count : 0);
        ApplyStandalonePlayerLoopMode(record);

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_loop_mode ok", payload);
        EmitRuntimeEvent(
            "player_loop_mode",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerRandom(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool enabled;
        if (!TryReadBoolArg(argsJson, out enabled, "random", "randomEnabled", "enabled", "value"))
        {
            errorMessage = "random is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.randomEnabled = enabled;
        if (enabled)
            EnsureStandalonePlayerRandomOrder(record, record.currentIndex, true);
        else
            ClearStandalonePlayerRandomHistory(record);

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_random ok", payload);
        EmitRuntimeEvent(
            "player_random",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerAbLoopStart(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        double startSeconds;
        if (!TryReadStandalonePlayerAbLoopPointSeconds(record, argsJson, out startSeconds, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.hasAbLoopStart = true;
        record.abLoopStartSeconds = Math.Max(0d, startSeconds);
        if (record.hasAbLoopEnd && record.abLoopEndSeconds <= (record.abLoopStartSeconds + StandalonePlayerAbLoopMinimumSpanSeconds))
        {
            record.hasAbLoopEnd = false;
            record.abLoopEndSeconds = 0d;
            record.abLoopEnabled = false;
        }
        else if (!HasValidStandalonePlayerAbLoopRange(record, out _, out _))
        {
            record.abLoopEnabled = false;
        }

        record.naturalEndHandled = false;
        record.lastError = "";

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_ab_loop_start ok", payload);
        EmitRuntimeEvent(
            "player_ab_loop_start",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerAbLoopEnd(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        double endSeconds;
        if (!TryReadStandalonePlayerAbLoopPointSeconds(record, argsJson, out endSeconds, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        endSeconds = Math.Max(0d, endSeconds);
        if (record.hasAbLoopStart && endSeconds <= (record.abLoopStartSeconds + StandalonePlayerAbLoopMinimumSpanSeconds))
        {
            errorMessage = "player A-B end must be after start";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.hasAbLoopEnd = true;
        record.abLoopEndSeconds = endSeconds;
        if (!HasValidStandalonePlayerAbLoopRange(record, out _, out _))
            record.abLoopEnabled = false;

        record.naturalEndHandled = false;
        record.lastError = "";

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_ab_loop_end ok", payload);
        EmitRuntimeEvent(
            "player_ab_loop_end",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerAbLoopEnabled(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool enabled;
        if (!TryReadBoolArg(argsJson, out enabled, "enabled", "abLoopEnabled", "value"))
        {
            errorMessage = "enabled is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (enabled && !HasValidStandalonePlayerAbLoopRange(record, out _, out _))
        {
            errorMessage = "player A-B loop requires both points";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.abLoopEnabled = enabled;
        record.naturalEndHandled = false;
        record.lastError = "";

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_ab_loop_enabled ok", payload);
        EmitRuntimeEvent(
            "player_ab_loop_enabled",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TryClearStandalonePlayerAbLoop(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        ClearStandalonePlayerAbLoopState(record);
        record.naturalEndHandled = false;
        record.lastError = "";

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_ab_loop_cleared ok", payload);
        EmitRuntimeEvent(
            "player_ab_loop_cleared",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerAspectMode(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string requestedMode = ExtractJsonArgString(argsJson, "aspectMode", "displayMode", "screenMode");
        if (string.IsNullOrEmpty(requestedMode))
        {
            errorMessage = "aspectMode is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.aspectMode = ResolveStandalonePlayerAspectMode(argsJson, record.aspectMode);
        record.needsScreenRefresh = true;

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_aspect_mode ok", payload);
        EmitRuntimeEvent(
            "player_aspect_mode",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TryClearStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        if (!HasStandalonePlayerSelector(argsJson))
        {
            List<StandalonePlayerRecord> allRecords = new List<StandalonePlayerRecord>(standalonePlayerRecords.Values);
            for (int i = 0; i < allRecords.Count; i++)
                DestroyStandalonePlayerRecord(allRecords[i]);
            standalonePlayerRecords.Clear();

            string allPayload = BuildStandalonePlayerSelectedStateJson("{}");
            resultJson = BuildBrokerResult(true, "player_clear ok", allPayload);
            return true;
        }

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string playbackKey = record.playbackKey;
        DestroyStandalonePlayerRecord(record);
        standalonePlayerRecords.Remove(playbackKey);

        string payload = BuildStandalonePlayerSelectedStateJson("{}");
        resultJson = BuildBrokerResult(true, "player_clear ok", payload);
        EmitRuntimeEvent(
            "player_clear",
            actionId,
            "ok",
            "",
            playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TryAdvanceStandalonePlayer(string actionId, string argsJson, bool forward, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (record.playlistPaths.Count <= 0)
        {
            errorMessage = "player playlist is empty";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        int targetIndex;
        bool changed = TryResolveStandalonePlayerStepIndex(record, forward, out targetIndex);
        if (targetIndex < 0 || targetIndex >= record.playlistPaths.Count)
        {
            errorMessage = "player playlist step target invalid";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (changed)
        {
            record.currentIndex = targetIndex;
            string targetPath = record.playlistPaths[targetIndex];
            if (IsHostedPlayerInstanceId(record.instanceId))
            {
                string hostAtomUid = ResolveHostedPlayerHostAtomUid(record);
                if (string.IsNullOrEmpty(hostAtomUid))
                {
                    errorMessage = "hosted player host atom uid not resolved";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                if (!TryLoadHostedStandalonePlayerRecordPath(record, hostAtomUid, record.playlistPaths, targetPath, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }
            else
            {
                InnerPieceInstanceRecord instance;
                InnerPieceScreenSlotRuntimeRecord slot;
                if (!TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                if (!TryLoadStandalonePlayerRecordPath(record, instance, slot, targetPath, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        string eventName = forward ? "player_next" : "player_previous";
        string resultMessage = forward
            ? (changed ? "player_next ok" : "player_next no_change")
            : (changed ? "player_previous ok" : "player_previous no_change");
        resultJson = BuildBrokerResult(true, resultMessage, payload);
        EmitRuntimeEvent(
            eventName,
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySkipStandalonePlayer(
        string actionId,
        string argsJson,
        float defaultDeltaSeconds,
        bool requireExplicitDelta,
        out string resultJson,
        out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float deltaSeconds = defaultDeltaSeconds;
        float parsedDeltaSeconds;
        if (TryReadStandalonePlayerSkipSeconds(argsJson, out parsedDeltaSeconds))
        {
            deltaSeconds = requireExplicitDelta
                ? parsedDeltaSeconds
                : Mathf.Sign(defaultDeltaSeconds == 0f ? 1f : defaultDeltaSeconds) * Mathf.Abs(parsedDeltaSeconds);
        }
        else if (requireExplicitDelta)
        {
            errorMessage = "seconds is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (!TrySkipStandalonePlayerRecord(record, deltaSeconds, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_skip ok", payload);
        EmitRuntimeEvent(
            "player_skip",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TrySeekStandalonePlayer(
        string actionId,
        string argsJson,
        bool useNormalizedTarget,
        out string resultJson,
        out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        double currentTimeSeconds;
        double durationSeconds;
        if (!TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        double targetTimeSeconds;
        string eventName;
        string okMessage;
        if (useNormalizedTarget)
        {
            float normalizedTarget;
            if (!TryReadStandalonePlayerSeekNormalized(argsJson, out normalizedTarget))
            {
                errorMessage = "normalized is required";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            normalizedTarget = Mathf.Clamp01(normalizedTarget);
            if (durationSeconds <= 0.0001d)
            {
                if (FrameAngelPlayerMediaParity.CanSeekWithoutKnownDuration(normalizedTarget))
                {
                    targetTimeSeconds = 0d;
                }
                else
                {
                    errorMessage = "player duration unknown";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }
            else
            {
                targetTimeSeconds = normalizedTarget * durationSeconds;
            }
            eventName = "player_seek_normalized";
            okMessage = "player_seek_normalized ok";
        }
        else
        {
            float parsedTargetSeconds;
            if (!TryReadStandalonePlayerSeekSeconds(argsJson, out parsedTargetSeconds))
            {
                errorMessage = "seconds is required";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            targetTimeSeconds = Math.Max(0d, parsedTargetSeconds);
            if (durationSeconds > 0.0001d)
                targetTimeSeconds = Math.Min(durationSeconds, targetTimeSeconds);

            eventName = "player_seek_seconds";
            okMessage = "player_seek_seconds ok";
        }

        bool shouldResumePlayback = record.desiredPlaying && !record.mediaIsStillImage;
        if (!TrySeekStandalonePlayerRecordToSeconds(record, targetTimeSeconds, shouldResumePlayback, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, okMessage, payload);
        EmitRuntimeEvent(
            eventName,
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private bool TryResolveOrCreateStandalonePlayerRecordForWrite(
        string argsJson,
        out StandalonePlayerRecord record,
        out InnerPieceInstanceRecord instance,
        out InnerPieceScreenSlotRuntimeRecord slot,
        out string errorMessage)
    {
        record = null;
        instance = null;
        slot = null;
        errorMessage = "";

        if (HasStandalonePlayerSelector(argsJson))
        {
            string ignoredError;
            if (TryResolveStandalonePlayerRecord(argsJson, out record, out ignoredError) && record != null)
            {
                if (TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
                    return true;

                record = null;
                return false;
            }
        }

        if (!TryResolveInnerPieceScreenSlot(
            ExtractJsonArgString(argsJson, "instanceId"),
            ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId"),
            out instance,
            out slot,
            out errorMessage))
        {
            return false;
        }

        string playbackKey = BuildStandalonePlayerPlaybackKey(instance.instanceId, slot.displayId);
        if (!standalonePlayerRecords.TryGetValue(playbackKey, out record) || record == null)
        {
            record = new StandalonePlayerRecord();
            record.playbackKey = playbackKey;
            standalonePlayerRecords[playbackKey] = record;
        }

        record.instanceId = instance.instanceId;
        record.slotId = slot.slotId;
        record.displayId = slot.displayId;
        record.aspectMode = string.IsNullOrEmpty(record.aspectMode)
            ? ResolveStandalonePlayerAspectMode("{}", instance.defaultAspectMode)
            : record.aspectMode;
        record.loopMode = NormalizeStandalonePlayerLoopMode(record.loopMode);
        return true;
    }

    private bool TryResolveStandalonePlayerStepIndex(StandalonePlayerRecord record, bool forward, out int targetIndex)
    {
        targetIndex = -1;
        if (record == null || record.playlistPaths.Count <= 0)
            return false;

        int count = record.playlistPaths.Count;
        int currentIndex = record.currentIndex;
        if (currentIndex < 0 || currentIndex >= count)
        {
            currentIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, record.mediaPath);
            if (currentIndex < 0)
                currentIndex = 0;
        }

        if (record.randomEnabled && count > 1)
        {
            EnsureStandalonePlayerRandomOrder(record, currentIndex, false);
            int randomCursor = FindStandalonePlayerRandomOrderCursor(record, currentIndex);
            if (randomCursor < 0)
            {
                EnsureStandalonePlayerRandomOrder(record, currentIndex, true);
                randomCursor = FindStandalonePlayerRandomOrderCursor(record, currentIndex);
            }

            if (randomCursor < 0)
                randomCursor = 0;

            record.randomOrderCursor = randomCursor;
            int targetCursor = randomCursor;
            if (forward)
            {
                if (randomCursor < count - 1)
                    targetCursor = randomCursor + 1;
                else if (string.Equals(record.loopMode, PlayerLoopModePlaylist, StringComparison.OrdinalIgnoreCase))
                    targetCursor = 0;
            }
            else
            {
                if (randomCursor > 0)
                    targetCursor = randomCursor - 1;
                else if (string.Equals(record.loopMode, PlayerLoopModePlaylist, StringComparison.OrdinalIgnoreCase))
                    targetCursor = count - 1;
            }

            if (targetCursor < 0 || targetCursor >= record.randomOrderIndices.Count)
                targetCursor = randomCursor;

            record.randomOrderCursor = targetCursor;
            targetIndex = record.randomOrderIndices[targetCursor];
            return targetIndex != currentIndex;
        }

        if (forward)
        {
            if (currentIndex < count - 1)
            {
                targetIndex = currentIndex + 1;
                return true;
            }

            targetIndex = string.Equals(record.loopMode, PlayerLoopModePlaylist, StringComparison.OrdinalIgnoreCase)
                ? 0
                : currentIndex;
            return targetIndex != currentIndex;
        }

        if (currentIndex > 0)
        {
            targetIndex = currentIndex - 1;
            return true;
        }

        targetIndex = string.Equals(record.loopMode, PlayerLoopModePlaylist, StringComparison.OrdinalIgnoreCase)
            ? (count - 1)
            : currentIndex;
        return targetIndex != currentIndex;
    }

    private bool TrySkipStandalonePlayerRecord(StandalonePlayerRecord record, float deltaSeconds, out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.videoPlayer == null)
        {
            errorMessage = "player runtime missing";
            return false;
        }

        if (!record.prepared)
        {
            errorMessage = "player not prepared";
            return false;
        }

        double currentTimeSeconds;
        double durationSeconds;
        if (!TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out errorMessage))
            return false;

        double targetTimeSeconds = currentTimeSeconds + deltaSeconds;
        if (durationSeconds > 0.0001d)
            targetTimeSeconds = Math.Max(0d, Math.Min(durationSeconds, targetTimeSeconds));
        else
            targetTimeSeconds = Math.Max(0d, targetTimeSeconds);

        try
        {
            record.videoPlayer.time = targetTimeSeconds;
            TryRefreshStandalonePlayerPausedFrame(record);
            record.lastError = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "player skip failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }
    }

    private void TryRefreshStandalonePlayerPausedFrame(StandalonePlayerRecord record)
    {
        if (record == null || record.videoPlayer == null)
            return;

        bool isPlayingNow = false;
        try
        {
            isPlayingNow = record.videoPlayer.isPlaying;
        }
        catch
        {
            return;
        }

        if (isPlayingNow)
            return;

        try
        {
            record.videoPlayer.StepForward();
        }
        catch
        {
        }
    }

    private bool TryReadStandalonePlayerTimeline(
        StandalonePlayerRecord record,
        out double currentTimeSeconds,
        out double durationSeconds,
        out string errorMessage)
    {
        currentTimeSeconds = 0d;
        durationSeconds = 0d;
        errorMessage = "";
        if (record != null && record.mediaIsStillImage)
        {
            errorMessage = "player timeline unavailable for still image";
            return false;
        }

        if (record == null || record.videoPlayer == null)
        {
            errorMessage = "player runtime missing";
            return false;
        }

        try
        {
            currentTimeSeconds = Math.Max(0d, record.videoPlayer.time);
        }
        catch (Exception ex)
        {
            errorMessage = "player timeline read failed: " + ex.Message;
            return false;
        }

        try
        {
            durationSeconds = GetStandalonePlayerDurationSeconds(record);
        }
        catch
        {
            durationSeconds = 0d;
        }

        if (double.IsNaN(currentTimeSeconds) || double.IsInfinity(currentTimeSeconds))
            currentTimeSeconds = 0d;
        if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
            durationSeconds = 0d;

        return true;
    }

    private bool TryLoadStandalonePlayerRecordPath(
        StandalonePlayerRecord record,
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        string mediaPath,
        out string errorMessage)
    {
        errorMessage = "";
        if (record == null || instance == null || slot == null)
        {
            errorMessage = "player load target missing";
            return false;
        }

        if (string.IsNullOrEmpty(mediaPath))
        {
            errorMessage = "mediaPath is required";
            return false;
        }

        string resolvedMediaPath = ResolveStandalonePlayerAbsolutePath(mediaPath);
        if (string.IsNullOrEmpty(resolvedMediaPath))
        {
            errorMessage = "mediaPath could not be resolved";
            return false;
        }

        record.instanceId = instance.instanceId;
        record.slotId = slot.slotId;
        record.displayId = slot.displayId;
        EnsureStandalonePlayerPathPresentInPlaylist(record, mediaPath);
        record.mediaPath = mediaPath;
        record.resolvedMediaPath = resolvedMediaPath;
        record.lastError = "";
        record.prepared = false;
        record.preparePending = false;
        record.prepareStartedAt = 0f;
        record.textureWidth = 0;
        record.textureHeight = 0;
        record.needsScreenRefresh = false;
        record.mediaIsStillImage = false;
        record.hasObservedPlaybackTime = false;
        record.lastObservedPlaybackTimeSeconds = 0d;
        record.lastPlaybackMotionObservedAt = 0f;
        record.naturalEndHandled = false;
        ClearStandalonePlayerAbLoopState(record);

        try
        {
            if (record.videoPlayer != null)
                record.videoPlayer.targetTexture = null;
        }
        catch
        {
        }

        DestroyStandalonePlayerImageTexture(record);

        if (record.renderTexture != null)
        {
            try
            {
                UnityEngine.Object.Destroy(record.renderTexture);
            }
            catch
            {
            }

            record.renderTexture = null;
        }

        if (!TryEnsureStandalonePlayerRuntime(record, out errorMessage))
            return false;

        ApplyStandalonePlayerLoopMode(record);
        ApplyStandalonePlayerAudioState(record);
        if (FrameAngelPlayerMediaParity.IsSupportedImagePath(mediaPath))
            return TryLoadStandalonePlayerImageTexture(record, resolvedMediaPath, out errorMessage);

        try
        {
            if (record.videoPlayer != null)
                record.videoPlayer.Stop();
        }
        catch
        {
        }

        if (record.binding != null)
        {
            TryRestoreDisconnectSurface(record.binding);
            record.binding = null;
        }

        try
        {
            record.videoPlayer.url = resolvedMediaPath;
            record.videoPlayer.Prepare();
            record.preparePending = true;
            record.prepareStartedAt = Time.unscaledTime;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "player prepare failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }
    }

    private void ApplyStandalonePlayerPlaylistArgs(StandalonePlayerRecord record, string argsJson, string fallbackPath)
    {
        if (record == null)
            return;

        List<string> previousPlaylistPaths = new List<string>(record.playlistPaths);
        List<string> requestedPaths = ExtractJsonStringList(argsJson, "playlist", "playlistPaths", "paths");
        if (requestedPaths.Count <= 0)
        {
            if (!string.IsNullOrEmpty(fallbackPath))
                EnsureStandalonePlayerPathPresentInPlaylist(record, fallbackPath);
            return;
        }

        record.playlistPaths.Clear();
        for (int i = 0; i < requestedPaths.Count; i++)
        {
            if (!string.IsNullOrEmpty(requestedPaths[i]))
                record.playlistPaths.Add(requestedPaths[i]);
        }

        if (record.playlistPaths.Count <= 0)
        {
            record.currentIndex = -1;
            return;
        }

        int requestedIndex;
        if (TryReadStandalonePlayerIntArg(argsJson, out requestedIndex, "currentIndex", "playlistIndex", "index"))
        {
            record.currentIndex = Mathf.Clamp(requestedIndex, 0, record.playlistPaths.Count - 1);
            return;
        }

        string requestedPath = ExtractJsonArgString(argsJson, "currentPath", "selectedPath");
        if (string.IsNullOrEmpty(requestedPath))
            requestedPath = fallbackPath;
        if (string.IsNullOrEmpty(requestedPath))
            requestedPath = record.mediaPath;

        int matchedIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, requestedPath);
        record.currentIndex = matchedIndex >= 0 ? matchedIndex : 0;
        if (!AreStandalonePlayerPlaylistsEquivalent(previousPlaylistPaths, record.playlistPaths))
            ClearStandalonePlayerRandomHistory(record);

        if (record.randomEnabled)
            EnsureStandalonePlayerRandomOrder(record, record.currentIndex, false);
    }

    private void EnsureStandalonePlayerPathPresentInPlaylist(StandalonePlayerRecord record, string mediaPath)
    {
        if (record == null || string.IsNullOrEmpty(mediaPath))
            return;

        int matchedIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, mediaPath);
        if (matchedIndex >= 0)
        {
            record.currentIndex = matchedIndex;
            return;
        }

        ClearStandalonePlayerRandomHistory(record);
        record.playlistPaths.Clear();
        record.playlistPaths.Add(mediaPath);
        record.currentIndex = 0;
        if (record.randomEnabled)
            EnsureStandalonePlayerRandomOrder(record, record.currentIndex, false);
    }

    private void ClearStandalonePlayerRandomHistory(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        if (record.randomHistoryPaths != null)
            record.randomHistoryPaths.Clear();
        if (record.randomOrderIndices != null)
            record.randomOrderIndices.Clear();
        record.randomOrderCursor = -1;
    }

    private void EnsureStandalonePlayerRandomOrder(StandalonePlayerRecord record, int anchorIndex, bool rebuild)
    {
        if (record == null)
            return;

        int count = record.playlistPaths != null ? record.playlistPaths.Count : 0;
        if (!record.randomEnabled || count <= 1)
        {
            if (record.randomOrderIndices != null)
                record.randomOrderIndices.Clear();
            record.randomOrderCursor = count > 0 ? Mathf.Clamp(anchorIndex, 0, count - 1) : -1;
            return;
        }

        int clampedAnchorIndex = Mathf.Clamp(anchorIndex, 0, count - 1);
        if (!rebuild && HasValidStandalonePlayerRandomOrder(record, count))
        {
            int existingCursor = FindStandalonePlayerRandomOrderCursor(record, clampedAnchorIndex);
            if (existingCursor >= 0)
            {
                record.randomOrderCursor = existingCursor;
                return;
            }
        }

        record.randomOrderIndices.Clear();
        record.randomOrderIndices.Add(clampedAnchorIndex);

        List<int> remainingIndices = new List<int>(Math.Max(0, count - 1));
        for (int i = 0; i < count; i++)
        {
            if (i != clampedAnchorIndex)
                remainingIndices.Add(i);
        }

        for (int i = remainingIndices.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = remainingIndices[i];
            remainingIndices[i] = remainingIndices[swapIndex];
            remainingIndices[swapIndex] = temp;
        }

        for (int i = 0; i < remainingIndices.Count; i++)
            record.randomOrderIndices.Add(remainingIndices[i]);

        record.randomOrderCursor = 0;
    }

    private bool HasValidStandalonePlayerRandomOrder(StandalonePlayerRecord record, int playlistCount)
    {
        if (record == null || record.randomOrderIndices == null || playlistCount <= 0)
            return false;

        if (record.randomOrderIndices.Count != playlistCount)
            return false;

        bool[] seen = new bool[playlistCount];
        for (int i = 0; i < record.randomOrderIndices.Count; i++)
        {
            int candidateIndex = record.randomOrderIndices[i];
            if (candidateIndex < 0 || candidateIndex >= playlistCount || seen[candidateIndex])
                return false;

            seen[candidateIndex] = true;
        }

        return true;
    }

    private int FindStandalonePlayerRandomOrderCursor(StandalonePlayerRecord record, int playlistIndex)
    {
        if (record == null || record.randomOrderIndices == null || record.randomOrderIndices.Count <= 0)
            return -1;

        for (int i = 0; i < record.randomOrderIndices.Count; i++)
        {
            if (record.randomOrderIndices[i] == playlistIndex)
                return i;
        }

        return -1;
    }

    private void PushStandalonePlayerRandomHistoryPath(StandalonePlayerRecord record, string mediaPath)
    {
        if (record == null || record.randomHistoryPaths == null || string.IsNullOrEmpty(mediaPath))
            return;

        if (record.randomHistoryPaths.Count >= StandalonePlayerRandomHistoryLimit)
            record.randomHistoryPaths.RemoveAt(0);

        record.randomHistoryPaths.Add(mediaPath);
    }

    private bool TryPopStandalonePlayerRandomHistoryIndex(StandalonePlayerRecord record, int currentIndex, out int targetIndex)
    {
        targetIndex = -1;
        if (record == null || record.randomHistoryPaths == null || record.randomHistoryPaths.Count <= 0)
            return false;

        while (record.randomHistoryPaths.Count > 0)
        {
            int historyIndex = record.randomHistoryPaths.Count - 1;
            string historyPath = record.randomHistoryPaths[historyIndex];
            record.randomHistoryPaths.RemoveAt(historyIndex);
            if (string.IsNullOrEmpty(historyPath))
                continue;

            int matchedIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, historyPath);
            if (matchedIndex < 0 || matchedIndex == currentIndex)
                continue;

            targetIndex = matchedIndex;
            return true;
        }

        return false;
    }

    private bool AreStandalonePlayerPlaylistsEquivalent(IList<string> left, IList<string> right)
    {
        int leftCount = left != null ? left.Count : 0;
        int rightCount = right != null ? right.Count : 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            string leftPath = NormalizeStandalonePlayerPathForMatch(left[i]);
            string rightPath = NormalizeStandalonePlayerPathForMatch(right[i]);
            if (!string.Equals(leftPath, rightPath, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private bool DoStandalonePlayerPlaylistsContainSameEntries(IList<string> left, IList<string> right)
    {
        int leftCount = left != null ? left.Count : 0;
        int rightCount = right != null ? right.Count : 0;
        if (leftCount != rightCount)
            return false;

        if (leftCount <= 0)
            return true;

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < leftCount; i++)
        {
            string normalizedPath = NormalizeStandalonePlayerPathForMatch(left[i]);
            int existingCount;
            counts.TryGetValue(normalizedPath, out existingCount);
            counts[normalizedPath] = existingCount + 1;
        }

        for (int i = 0; i < rightCount; i++)
        {
            string normalizedPath = NormalizeStandalonePlayerPathForMatch(right[i]);
            int existingCount;
            if (!counts.TryGetValue(normalizedPath, out existingCount) || existingCount <= 0)
                return false;

            if (existingCount == 1)
                counts.Remove(normalizedPath);
            else
                counts[normalizedPath] = existingCount - 1;
        }

        return counts.Count <= 0;
    }

    private int FindStandalonePlayerPlaylistIndex(List<string> playlistPaths, string mediaPath)
    {
        if (playlistPaths == null || playlistPaths.Count <= 0 || string.IsNullOrEmpty(mediaPath))
            return -1;

        string wanted = NormalizeStandalonePlayerPathForMatch(mediaPath);
        for (int i = 0; i < playlistPaths.Count; i++)
        {
            if (string.Equals(NormalizeStandalonePlayerPathForMatch(playlistPaths[i]), wanted, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private string GetStandalonePlayerCurrentPlaylistPath(StandalonePlayerRecord record)
    {
        if (record == null || record.playlistPaths.Count <= 0)
            return "";

        int clampedIndex = record.currentIndex;
        if (clampedIndex < 0 || clampedIndex >= record.playlistPaths.Count)
            clampedIndex = Mathf.Clamp(clampedIndex, 0, record.playlistPaths.Count - 1);

        return record.playlistPaths[clampedIndex] ?? "";
    }

    private string NormalizeStandalonePlayerPathForMatch(string mediaPath)
    {
        if (string.IsNullOrEmpty(mediaPath))
            return "";

        return mediaPath.Replace('\\', '/').Trim().ToLowerInvariant();
    }

    private double GetStandalonePlayerDurationSeconds(StandalonePlayerRecord record)
    {
        if (record == null || record.mediaIsStillImage || record.videoPlayer == null)
            return 0d;

        try
        {
            ulong frameCount = record.videoPlayer.frameCount;
            float frameRate = record.videoPlayer.frameRate;
            if (frameCount > 0UL && frameRate > 0.0001f)
                return Math.Max(0d, frameCount / (double)frameRate);
        }
        catch
        {
        }

        return 0d;
    }

    private void ResetStandalonePlayerPlaybackMotionState(StandalonePlayerRecord record, double currentTimeSeconds)
    {
        if (record == null)
            return;

        record.hasObservedPlaybackTime = true;
        record.lastObservedPlaybackTimeSeconds = Math.Max(0d, currentTimeSeconds);
        record.lastPlaybackMotionObservedAt = Time.unscaledTime;
        record.naturalEndHandled = false;
    }

    private void TickStandalonePlayerRuntime()
    {
        if (standalonePlayerRecords.Count <= 0)
            return;

        List<StandalonePlayerRecord> records = new List<StandalonePlayerRecord>(standalonePlayerRecords.Values);
        for (int i = 0; i < records.Count; i++)
        {
            StandalonePlayerRecord record = records[i];
            if (record == null)
                continue;

            ApplyStandalonePlayerAudioState(record);

            if (record.mediaIsStillImage)
            {
                record.prepared = record.imageTexture != null;
                record.preparePending = false;
                record.prepareStartedAt = 0f;
                record.desiredPlaying = false;

                if (record.imageTexture != null && (record.textureWidth <= 0 || record.textureHeight <= 0))
                    TryResolveTextureDimensions(record.imageTexture, out record.textureWidth, out record.textureHeight);

                if (record.needsScreenRefresh)
                {
                    string stillRefreshError;
                    if (!TryRefreshStandalonePlayerScreenBinding(record, out stillRefreshError))
                        record.lastError = stillRefreshError;
                    else
                        record.lastError = "";
                }

                continue;
            }

            if (record.videoPlayer == null)
                continue;

            bool preparedNow = false;
            try
            {
                preparedNow = record.videoPlayer.isPrepared;
            }
            catch (Exception ex)
            {
                record.lastError = "player prepare state failed: " + ex.Message;
            }

            if (preparedNow != record.prepared)
            {
                record.prepared = preparedNow;
                if (preparedNow)
                {
                    record.preparePending = false;
                    record.prepareStartedAt = 0f;
                    record.needsScreenRefresh = true;
                }
            }

            if (!preparedNow
                && record.preparePending
                && record.prepareStartedAt > 0f
                && (Time.unscaledTime - record.prepareStartedAt) >= StandalonePlayerPrepareTimeoutSeconds)
            {
                record.preparePending = false;
                record.prepareStartedAt = 0f;
                record.lastError = "player media did not prepare; file may be unsupported or unplayable";
            }

            // Hosted CUA playback should never sit on a live render texture without a binding.
            // If a previous refresh attempt was skipped or the flag got dropped, force the
            // rebind path back on so load can recover without another user round-trip.
            if (IsHostedPlayerInstanceId(record.instanceId)
                && record.renderTexture != null
                && record.binding == null)
            {
                record.needsScreenRefresh = true;
            }

            bool canRefreshPendingHostedBinding =
                record.needsScreenRefresh
                && record.renderTexture != null
                && IsHostedPlayerInstanceId(record.instanceId)
                && record.binding == null;

            bool hostedVideoBindingRequiresKnownDimensions =
                IsHostedPlayerInstanceId(record.instanceId)
                && !record.mediaIsStillImage
                && record.binding != null;
            bool hasKnownPresentedVideoDimensions =
                record.textureWidth > 16
                && record.textureHeight > 16;
            bool canRefreshScreenBindingNow =
                !hostedVideoBindingRequiresKnownDimensions
                || hasKnownPresentedVideoDimensions;

            if (record.prepared || canRefreshPendingHostedBinding)
            {
                if (record.prepared)
                {
                    int detectedWidth = 0;
                    int detectedHeight = 0;
                    TryResolvePreparedVideoDimensions(record.videoPlayer, out detectedWidth, out detectedHeight);

                    if (detectedWidth > 0 && detectedHeight > 0
                        && (record.textureWidth != detectedWidth || record.textureHeight != detectedHeight))
                    {
                        record.textureWidth = detectedWidth;
                        record.textureHeight = detectedHeight;
                        TryEnsureStandalonePlayerRenderTexture(record, detectedWidth, detectedHeight);
                        record.needsScreenRefresh = true;
                    }

                    double currentTimeSeconds = 0d;
                    double durationSeconds = 0d;
                    string timelineError = "";
                    bool hasTimeline = TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out timelineError);
                    if (!hasTimeline && !string.IsNullOrEmpty(timelineError))
                        record.lastError = timelineError;

                    bool isPlayingNow = false;
                    try
                    {
                        isPlayingNow = record.videoPlayer.isPlaying;
                    }
                    catch (Exception ex)
                    {
                        record.lastError = "player play state failed: " + ex.Message;
                    }

                    if (hasTimeline)
                    {
                        ObserveStandalonePlayerPlaybackMotion(record, currentTimeSeconds);
                        if (!IsStandalonePlayerAtNaturalEnd(currentTimeSeconds, durationSeconds))
                            record.naturalEndHandled = false;

                        string naturalEndError = "";
                        if (TryHandleStandalonePlayerNaturalEnd(record, currentTimeSeconds, durationSeconds, isPlayingNow, out naturalEndError))
                        {
                            if (!string.IsNullOrEmpty(naturalEndError))
                                record.lastError = naturalEndError;
                            continue;
                        }
                    }

                    if (record.desiredPlaying)
                    {
                        try
                        {
                            if (!record.videoPlayer.isPlaying
                                && Time.unscaledTime >= record.nextPlaybackStateApplyTime)
                            {
                                record.videoPlayer.Play();
                                record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
                            }
                        }
                        catch (Exception ex)
                        {
                            record.lastError = "player play tick failed: " + ex.Message;
                        }
                    }
                    else
                    {
                        try
                        {
                            if (record.videoPlayer.isPlaying
                                && Time.unscaledTime >= record.nextPlaybackStateApplyTime)
                            {
                                record.videoPlayer.Pause();
                                record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
                            }
                        }
                        catch (Exception ex)
                        {
                            record.lastError = "player pause tick failed: " + ex.Message;
                        }
                    }
                }

                if (record.needsScreenRefresh && canRefreshScreenBindingNow)
                {
                    string refreshError;
                    if (!TryRefreshStandalonePlayerScreenBinding(record, out refreshError))
                    {
                        record.lastError = refreshError;
                    }
                    else
                    {
                        record.lastError = "";
                    }
                }
            }
        }
    }

    private void ShutdownStandalonePlayerRuntime()
    {
        List<StandalonePlayerRecord> records = new List<StandalonePlayerRecord>(standalonePlayerRecords.Values);
        for (int i = 0; i < records.Count; i++)
            DestroyStandalonePlayerRecord(records[i]);
        standalonePlayerRecords.Clear();
    }

    private bool HasStandalonePlayerSelector(string argsJson)
    {
        return !string.IsNullOrEmpty(ExtractJsonArgString(argsJson, "playbackKey", "id"))
            || !string.IsNullOrEmpty(ExtractJsonArgString(argsJson, "instanceId"))
            || !string.IsNullOrEmpty(ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId"));
    }

    private bool TryResolveStandalonePlayerRecord(string argsJson, out StandalonePlayerRecord record, out string errorMessage)
    {
        record = null;
        errorMessage = "";

        string playbackKey = ExtractJsonArgString(argsJson, "playbackKey", "id");
        if (!string.IsNullOrEmpty(playbackKey))
        {
            if (standalonePlayerRecords.TryGetValue(playbackKey, out record) && record != null)
                return true;

            errorMessage = "player record not found";
            return false;
        }

        string instanceId = ExtractJsonArgString(argsJson, "instanceId");
        string slotSelector = ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId");
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(slotSelector))
        {
            errorMessage = "playbackKey or instanceId plus slotId/displayId is required";
            return false;
        }

        string directKey = BuildStandalonePlayerPlaybackKey(instanceId, slotSelector);
        if (standalonePlayerRecords.TryGetValue(directKey, out record) && record != null)
            return true;

        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord candidate = kvp.Value;
            if (candidate == null)
                continue;
            if (!string.Equals(candidate.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(candidate.slotId, slotSelector, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate.displayId, slotSelector, StringComparison.OrdinalIgnoreCase)
                && !AreEquivalentInnerPieceDisplayIds(candidate.slotId, slotSelector)
                && !AreEquivalentInnerPieceDisplayIds(candidate.displayId, slotSelector))
                continue;

            record = candidate;
            return true;
        }

        errorMessage = "player record not found";
        return false;
    }

    private string BuildStandalonePlayerPlaybackKey(string instanceId, string displayId)
    {
        return (instanceId ?? "").Trim() + "::" + (displayId ?? "").Trim();
    }

    private string ResolveStandalonePlayerAspectMode(string argsJson, string defaultAspectMode)
    {
        string raw = ExtractJsonArgString(argsJson, "aspectMode", "displayMode", "screenMode");
        if (string.IsNullOrEmpty(raw))
            raw = defaultAspectMode;

        if (string.IsNullOrEmpty(raw))
            return CoercePlayerReleaseAspectMode(GhostScreenAspectModeFit);

        string normalized = FrameAngelPlayerMediaParity.NormalizeAspectMode(raw, "");
        if (!string.IsNullOrEmpty(normalized))
            return CoercePlayerReleaseAspectMode(normalized);

        return CoercePlayerReleaseAspectMode(GhostScreenAspectModeCrop);
    }

    private string ResolveStandalonePlayerLoopMode(string argsJson, string defaultLoopMode, int playlistCount = 0)
    {
        string raw = ExtractJsonArgString(argsJson, "loopMode", "mode", "value");
        if (string.IsNullOrEmpty(raw))
        {
            bool looping;
            if (TryReadBoolArg(argsJson, out looping, "loop", "isLooping"))
                raw = looping
                    ? (playlistCount > 1 ? PlayerLoopModePlaylist : PlayerLoopModeSingle)
                    : PlayerLoopModeNone;
            else
                raw = defaultLoopMode;
        }

        return NormalizeStandalonePlayerLoopMode(raw);
    }

    private string NormalizeStandalonePlayerLoopMode(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return PlayerLoopModeNone;

        raw = raw.Trim();
        if (string.Equals(raw, "single", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "one", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "track", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "single_loop", StringComparison.OrdinalIgnoreCase))
            return PlayerLoopModeSingle;

        if (string.Equals(raw, "playlist", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "loop_all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "playlist_loop", StringComparison.OrdinalIgnoreCase))
            return PlayerLoopModePlaylist;

        return PlayerLoopModeNone;
    }

    private bool HasStandalonePlayerLoopModeArg(string argsJson)
    {
        bool ignored;
        return !string.IsNullOrEmpty(ExtractJsonArgString(argsJson, "loopMode", "mode", "value"))
            || TryExtractJsonBoolField(argsJson, "loop", out ignored)
            || TryExtractJsonBoolField(argsJson, "isLooping", out ignored)
            || !string.IsNullOrEmpty(ExtractJsonArgString(argsJson, "loop", "isLooping"));
    }

    private bool TryReadStandalonePlayerDesiredPlaying(string argsJson, bool defaultValue)
    {
        bool value;
        if (TryReadBoolArg(argsJson, out value, "autoPlay", "play", "desiredPlaying"))
            return value;
        return defaultValue;
    }

    private bool TryReadStandalonePlayerLooping(string argsJson, bool defaultValue)
    {
        return TryReadStandalonePlayerLoopFlagArg(argsJson, defaultValue);
    }

    private bool TryReadStandalonePlayerLoopFlagArg(string argsJson, bool defaultValue)
    {
        bool value;
        if (TryReadBoolArg(argsJson, out value, "loop", "isLooping"))
            return value;
        return defaultValue;
    }

    private bool TryReadStandalonePlayerRandomEnabled(string argsJson, bool defaultValue)
    {
        bool value;
        if (TryReadBoolArg(argsJson, out value, "random", "randomEnabled"))
            return value;
        return defaultValue;
    }

    private bool TryReadStandalonePlayerSkipSeconds(string argsJson, out float value)
    {
        value = 0f;
        if (TryExtractJsonFloatField(argsJson, "seconds", out value)
            || TryExtractJsonFloatField(argsJson, "deltaSeconds", out value)
            || TryExtractJsonFloatField(argsJson, "skipSeconds", out value)
            || TryExtractJsonFloatField(argsJson, "amountSeconds", out value))
            return true;

        return false;
    }

    private bool TryReadStandalonePlayerSeekSeconds(string argsJson, out float value)
    {
        value = 0f;
        if (TryExtractJsonFloatField(argsJson, "seconds", out value)
            || TryExtractJsonFloatField(argsJson, "timeSeconds", out value)
            || TryExtractJsonFloatField(argsJson, "positionSeconds", out value)
            || TryExtractJsonFloatField(argsJson, "seekSeconds", out value)
            || TryExtractJsonFloatField(argsJson, "value", out value))
            return true;

        return false;
    }

    private bool TryReadStandalonePlayerSeekNormalized(string argsJson, out float value)
    {
        value = 0f;
        if (TryExtractJsonFloatField(argsJson, "normalized", out value)
            || TryExtractJsonFloatField(argsJson, "normalizedTime", out value)
            || TryExtractJsonFloatField(argsJson, "seekNormalized", out value)
            || TryExtractJsonFloatField(argsJson, "progress", out value)
            || TryExtractJsonFloatField(argsJson, "value", out value))
            return true;

        return false;
    }

    private bool TryReadStandalonePlayerAbLoopPointSeconds(StandalonePlayerRecord record, string argsJson, out double seconds, out string errorMessage)
    {
        seconds = 0d;
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "player runtime missing";
            return false;
        }

        if (record.mediaIsStillImage)
        {
            errorMessage = "player A-B loop is only available for video";
            return false;
        }

        float parsedSeconds;
        if (TryReadStandalonePlayerSeekSeconds(argsJson, out parsedSeconds))
        {
            seconds = Math.Max(0d, parsedSeconds);
            double knownDurationSeconds = GetStandalonePlayerDurationSeconds(record);
            if (!double.IsNaN(knownDurationSeconds)
                && !double.IsInfinity(knownDurationSeconds)
                && knownDurationSeconds > 0.0001d)
            {
                seconds = Math.Min(knownDurationSeconds, seconds);
            }
            return true;
        }

        double currentTimeSeconds;
        double durationSeconds;
        if (!TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out errorMessage))
            return false;

        seconds = Math.Max(0d, currentTimeSeconds);
        if (!double.IsNaN(durationSeconds)
            && !double.IsInfinity(durationSeconds)
            && durationSeconds > 0.0001d)
        {
            seconds = Math.Min(durationSeconds, seconds);
        }
        return true;
    }

    private bool TryReadStandalonePlayerIntArg(string argsJson, out int value, params string[] keys)
    {
        value = 0;
        if (keys == null)
            return false;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            string raw = ExtractJsonArgString(argsJson, key);
            int parsedValue;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out parsedValue))
            {
                value = parsedValue;
                return true;
            }

            float floatValue;
            if (TryExtractJsonFloatField(argsJson, key, out floatValue))
            {
                value = Mathf.RoundToInt(floatValue);
                return true;
            }
        }

        return false;
    }

    private void ObserveStandalonePlayerPlaybackMotion(StandalonePlayerRecord record, double currentTimeSeconds)
    {
        if (record == null)
            return;

        if (!record.hasObservedPlaybackTime)
        {
            record.hasObservedPlaybackTime = true;
            record.lastObservedPlaybackTimeSeconds = currentTimeSeconds;
            if (currentTimeSeconds > StandalonePlayerPlaybackMotionEpsilonSeconds)
                record.lastPlaybackMotionObservedAt = Time.unscaledTime;
            return;
        }

        if (currentTimeSeconds + StandalonePlayerPlaybackMotionEpsilonSeconds < record.lastObservedPlaybackTimeSeconds)
        {
            record.lastPlaybackMotionObservedAt = Time.unscaledTime;
            record.naturalEndHandled = false;
        }
        else if (Math.Abs(currentTimeSeconds - record.lastObservedPlaybackTimeSeconds) > StandalonePlayerPlaybackMotionEpsilonSeconds)
        {
            record.lastPlaybackMotionObservedAt = Time.unscaledTime;
            record.naturalEndHandled = false;
        }

        record.lastObservedPlaybackTimeSeconds = currentTimeSeconds;
    }

    private bool IsStandalonePlayerAtNaturalEnd(double currentTimeSeconds, double durationSeconds)
    {
        return durationSeconds > StandalonePlayerPlaybackEndThresholdSeconds
            && currentTimeSeconds >= (durationSeconds - StandalonePlayerPlaybackEndThresholdSeconds);
    }

    private bool IsStandalonePlayerStalledAtEnd(StandalonePlayerRecord record, bool isPlayingNow)
    {
        if (record == null)
            return false;

        if (isPlayingNow)
            return false;

        if (record.lastPlaybackMotionObservedAt <= 0f)
            return true;

        return (Time.unscaledTime - record.lastPlaybackMotionObservedAt) >= StandalonePlayerPlaybackStoppedGraceSeconds;
    }

    private bool TryHandleStandalonePlayerNaturalEnd(
        StandalonePlayerRecord record,
        double currentTimeSeconds,
        double durationSeconds,
        bool isPlayingNow,
        out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.videoPlayer == null || !record.desiredPlaying)
            return false;

        if (TryHandleStandalonePlayerAbLoop(record, currentTimeSeconds, out errorMessage))
            return true;

        if (!IsStandalonePlayerAtNaturalEnd(currentTimeSeconds, durationSeconds))
            return false;

        if (!IsStandalonePlayerStalledAtEnd(record, isPlayingNow))
            return false;

        return TryHandleStandalonePlayerCompletedPlayback(record, out errorMessage);
    }

    private bool TryHandleStandalonePlayerCompletedPlayback(StandalonePlayerRecord record, out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.videoPlayer == null || !record.desiredPlaying)
            return false;

        if (record.naturalEndHandled)
            return true;

        record.naturalEndHandled = true;
        string normalizedLoopMode = NormalizeStandalonePlayerLoopMode(record.loopMode);
        if (string.Equals(normalizedLoopMode, PlayerLoopModeSingle, StringComparison.OrdinalIgnoreCase))
            return TryRestartStandalonePlayerFromBeginning(record, out errorMessage);

        if (string.Equals(normalizedLoopMode, PlayerLoopModePlaylist, StringComparison.OrdinalIgnoreCase))
            return TryAdvanceStandalonePlayerAfterNaturalEnd(record, out errorMessage);

        record.desiredPlaying = false;
        record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
        TryRefreshStandalonePlayerPausedFrame(record);
        return true;
    }

    private bool TryRestartStandalonePlayerFromBeginning(StandalonePlayerRecord record, out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.videoPlayer == null)
        {
            errorMessage = "player runtime missing";
            return false;
        }

        try
        {
            record.videoPlayer.time = 0d;
            record.videoPlayer.Play();
            record.desiredPlaying = true;
            record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
            record.hasObservedPlaybackTime = false;
            record.lastObservedPlaybackTimeSeconds = 0d;
            record.lastPlaybackMotionObservedAt = Time.unscaledTime;
            record.naturalEndHandled = false;
            record.needsScreenRefresh = true;
            record.lastError = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "player restart from end failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }
    }

    private bool TrySeekStandalonePlayerRecordToSeconds(StandalonePlayerRecord record, double targetTimeSeconds, bool shouldResumePlayback, out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.videoPlayer == null)
        {
            errorMessage = "player runtime missing";
            return false;
        }

        try
        {
            targetTimeSeconds = Math.Max(0d, targetTimeSeconds);
            double durationSeconds = GetStandalonePlayerDurationSeconds(record);
            if (!double.IsNaN(durationSeconds)
                && !double.IsInfinity(durationSeconds)
                && durationSeconds > 0.0001d)
            {
                targetTimeSeconds = Math.Min(durationSeconds, targetTimeSeconds);
            }

            if (shouldResumePlayback)
            {
                try
                {
                    record.videoPlayer.Pause();
                }
                catch
                {
                }
            }

            record.videoPlayer.time = targetTimeSeconds;
            ResetStandalonePlayerPlaybackMotionState(record, targetTimeSeconds);

            if (shouldResumePlayback)
            {
                record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
                ApplyStandalonePlayerAudioState(record);
                record.videoPlayer.Play();
            }
            else
            {
                TryRefreshStandalonePlayerPausedFrame(record);
            }

            record.lastError = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "player seek failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }
    }

    private void ClearStandalonePlayerAbLoopState(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        record.hasAbLoopStart = false;
        record.abLoopStartSeconds = 0d;
        record.hasAbLoopEnd = false;
        record.abLoopEndSeconds = 0d;
        record.abLoopEnabled = false;
    }

    private bool HasValidStandalonePlayerAbLoopRange(StandalonePlayerRecord record, out double startSeconds, out double endSeconds)
    {
        startSeconds = 0d;
        endSeconds = 0d;
        if (record == null || !record.hasAbLoopStart || !record.hasAbLoopEnd)
            return false;

        startSeconds = Math.Max(0d, record.abLoopStartSeconds);
        endSeconds = Math.Max(0d, record.abLoopEndSeconds);
        if (double.IsNaN(startSeconds) || double.IsInfinity(startSeconds) || double.IsNaN(endSeconds) || double.IsInfinity(endSeconds))
            return false;

        return endSeconds > (startSeconds + StandalonePlayerAbLoopMinimumSpanSeconds);
    }

    private bool TryHandleStandalonePlayerAbLoop(StandalonePlayerRecord record, double currentTimeSeconds, out string errorMessage)
    {
        errorMessage = "";
        if (record == null
            || record.videoPlayer == null
            || record.mediaIsStillImage
            || !record.desiredPlaying
            || !record.abLoopEnabled)
        {
            return false;
        }

        if (!HasValidStandalonePlayerAbLoopRange(record, out double startSeconds, out double endSeconds))
        {
            record.abLoopEnabled = false;
            return false;
        }

        if (currentTimeSeconds + StandalonePlayerPlaybackMotionEpsilonSeconds < endSeconds)
            return false;

        return TrySeekStandalonePlayerRecordToSeconds(record, startSeconds, true, out errorMessage);
    }

    private const int ScreenCoreOverlayBackingRenderQueue = 4495;
    private const int ScreenCoreOverlayRenderQueue = 4500;

    private bool TryCreateRuntimeBackingMaterial(Material targetMaterial, out Material backingMaterial)
    {
        backingMaterial = null;
        if (!TryCreateBlackBackdropMaterial(targetMaterial, out backingMaterial) || backingMaterial == null)
            return false;

        try
        {
            TrySetMaterialFloat(backingMaterial, "_ZWrite", 1f);
            TrySetMaterialFloat(backingMaterial, "_Cull", 0f);
            TrySetMaterialFloat(backingMaterial, "_Mode", 0f);
            TrySetMaterialFloat(backingMaterial, "_Surface", 0f);

            int basisRenderQueue = -1;
            try
            {
                if (targetMaterial != null)
                    basisRenderQueue = targetMaterial.renderQueue;
            }
            catch
            {
                basisRenderQueue = -1;
            }

            try
            {
                // VaM controls can render on late UI-style queues that do not respect the
                // earlier opaque backing as a real blocker. Keep the attached rear backing on
                // the same late screen-core occlusion band as the visible media face so behind-
                // screen controls cannot paint over it from either side.
                int backingQueue = ScreenCoreOverlayBackingRenderQueue;
                if (basisRenderQueue >= ScreenCoreOverlayBackingRenderQueue)
                    backingQueue = basisRenderQueue;

                backingMaterial.renderQueue = backingQueue;
            }
            catch
            {
            }

            return true;
        }
        catch
        {
            if (backingMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(backingMaterial);
                }
                catch
                {
                }
            }

            backingMaterial = null;
            return false;
        }
    }

    private bool TryCreateRuntimeMediaBackingSurface(
        GameObject runtimeSurfaceObject,
        string slotId,
        Material targetMaterial,
        out Renderer backingRenderer,
        out Material backingMaterial,
        out string errorMessage)
    {
        backingRenderer = null;
        backingMaterial = null;
        errorMessage = "";

        if (runtimeSurfaceObject == null)
        {
            errorMessage = "runtime media surface not found";
            return false;
        }

        if (!TryCreateRuntimeBackingMaterial(targetMaterial, out backingMaterial) || backingMaterial == null)
        {
            errorMessage = "runtime backing material not created";
            return false;
        }

        GameObject backingObject = null;
        try
        {
            backingObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backingObject.name = "FAPlayerRuntimeSurface_Backing_" + (string.IsNullOrEmpty(slotId) ? "main" : slotId);
            backingObject.layer = runtimeSurfaceObject.layer;
            backingObject.transform.SetParent(runtimeSurfaceObject.transform, false);
            backingObject.transform.localRotation = Quaternion.identity;
            backingObject.transform.localScale = Vector3.one;

            Vector3 overlayScale = runtimeSurfaceObject.transform.localScale;
            float backingGap = Mathf.Max(
                0.00005f,
                Mathf.Max(Mathf.Abs(overlayScale.x), Mathf.Abs(overlayScale.y)) * 0.0001f);
            backingObject.transform.localPosition = new Vector3(0f, 0f, -backingGap);

            Collider backingCollider = backingObject.GetComponent<Collider>();
            if (backingCollider != null)
                UnityEngine.Object.Destroy(backingCollider);

            backingRenderer = backingObject.GetComponent<Renderer>();
            if (backingRenderer == null)
            {
                UnityEngine.Object.Destroy(backingObject);
                UnityEngine.Object.Destroy(backingMaterial);
                backingMaterial = null;
                errorMessage = "runtime backing renderer not created";
                return false;
            }

            backingRenderer.sharedMaterials = new[] { backingMaterial };
            backingRenderer.enabled = true;
            backingRenderer.receiveShadows = false;
#pragma warning disable CS0618
            backingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#pragma warning restore CS0618
            backingRenderer.allowOcclusionWhenDynamic = false;
            return true;
        }
        catch
        {
            if (backingObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(backingObject);
                }
                catch
                {
                }
            }

            if (backingMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(backingMaterial);
                }
                catch
                {
                }
            }

            backingRenderer = null;
            backingMaterial = null;
            errorMessage = "runtime backing surface not created";
            return false;
        }
    }

    private bool TryAdvanceStandalonePlayerAfterNaturalEnd(StandalonePlayerRecord record, out string errorMessage)
    {
        errorMessage = "";
        if (record == null || record.playlistPaths.Count <= 0)
        {
            errorMessage = "player playlist is empty";
            return false;
        }

        int targetIndex;
        bool changed = TryResolveStandalonePlayerStepIndex(record, true, out targetIndex);
        if (targetIndex < 0 || targetIndex >= record.playlistPaths.Count)
        {
            errorMessage = "player playlist step target invalid";
            return false;
        }

        if (!changed)
            return TryRestartStandalonePlayerFromBeginning(record, out errorMessage);

        record.currentIndex = targetIndex;
        string targetPath = record.playlistPaths[targetIndex];
        if (IsHostedPlayerInstanceId(record.instanceId))
        {
            string hostAtomUid = ResolveHostedPlayerHostAtomUid(record);
            if (string.IsNullOrEmpty(hostAtomUid))
            {
                errorMessage = "hosted player host atom uid not resolved";
                return false;
            }

            return TryLoadHostedStandalonePlayerRecordPath(record, hostAtomUid, record.playlistPaths, targetPath, out errorMessage);
        }

        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        if (!TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
            return false;

        return TryLoadStandalonePlayerRecordPath(record, instance, slot, targetPath, out errorMessage);
    }

    private void ApplyStandalonePlayerLoopMode(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        record.loopMode = NormalizeStandalonePlayerLoopMode(record.loopMode);
        record.looping = string.Equals(record.loopMode, PlayerLoopModeSingle, StringComparison.OrdinalIgnoreCase);

        if (record.videoPlayer == null)
            return;

        try
        {
            // Keep Unity's raw looping disabled and let the clean-room runtime own
            // restart and playlist advancement explicitly so single and playlist loop
            // modes stay on one deterministic end-of-media path.
            record.videoPlayer.isLooping = false;
        }
        catch (Exception ex)
        {
            record.lastError = "player loop mode failed: " + ex.Message;
        }
    }

    private bool TryEnsureStandalonePlayerRuntime(StandalonePlayerRecord record, out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "player record missing";
            return false;
        }

        EnsureRuntimeRoot();
        if (record.runtimeObject == null)
        {
            GameObject runtimeObject = new GameObject("FAStandalonePlayer_" + SanitizeStandalonePlayerName(record.playbackKey));
            if (runtimeRoot != null)
                runtimeObject.transform.SetParent(runtimeRoot.transform, false);
            runtimeObject.transform.localPosition = Vector3.zero;
            runtimeObject.transform.localRotation = Quaternion.identity;
            runtimeObject.transform.localScale = Vector3.one;
            record.runtimeObject = runtimeObject;
        }

        if (record.videoPlayer == null)
        {
            record.videoPlayer = record.runtimeObject.GetComponent<VideoPlayer>();
            if (record.videoPlayer == null)
                record.videoPlayer = record.runtimeObject.AddComponent<VideoPlayer>();
        }

        if (record.audioSource == null)
        {
            record.audioSource = record.runtimeObject.GetComponent<AudioSource>();
            if (record.audioSource == null)
                record.audioSource = record.runtimeObject.AddComponent<AudioSource>();
        }

        if (record.videoPlayer == null)
        {
            errorMessage = "player video player not created";
            return false;
        }

        if (record.audioSource == null)
        {
            errorMessage = "player audio source not created";
            return false;
        }

        record.audioSource.playOnAwake = false;
        record.audioSource.loop = false;
        record.audioSource.spatialBlend = 0f;
        record.audioSource.dopplerLevel = 0f;
        record.audioSource.volume = record.muted ? 0f : MapStandalonePlayerNormalizedVolumeToAudioGain(record.storedVolume);
        record.audioSource.mute = record.muted;

        record.videoPlayer.playOnAwake = false;
        record.videoPlayer.waitForFirstFrame = true;
        // Match the Volodeck proof runtime so seek/skip behavior stays on the same
        // playback contract while we chase appearance parity on the screen-core lane.
        record.videoPlayer.skipOnDrop = false;
        record.videoPlayer.source = VideoSource.Url;
        record.videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        record.videoPlayer.controlledAudioTrackCount = 1;
        record.videoPlayer.EnableAudioTrack(0, true);
        record.videoPlayer.SetTargetAudioSource(0, record.audioSource);
        record.videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        record.videoPlayer.isLooping = false;
        record.videoPlayer.aspectRatio = VideoAspectRatio.NoScaling;

        if (!record.runtimeErrorHooked)
        {
            record.videoPlayer.errorReceived += delegate(VideoPlayer source, string message)
            {
                if (record == null)
                    return;

                record.preparePending = false;
                record.prepareStartedAt = 0f;
                record.prepared = false;
                record.needsScreenRefresh = true;
                record.lastError = string.IsNullOrEmpty(message)
                    ? "player media error"
                    : "player media error: " + message;
            };
            record.runtimeErrorHooked = true;
        }

        if (!record.runtimeLoopPointHooked)
        {
            record.videoPlayer.loopPointReached += delegate(VideoPlayer source)
            {
                if (record == null)
                    return;

                record.hasObservedPlaybackTime = false;
                record.lastObservedPlaybackTimeSeconds = 0d;
                record.lastPlaybackMotionObservedAt = Time.unscaledTime;

                string completionError;
                bool handled = TryHandleStandalonePlayerCompletedPlayback(record, out completionError);
                if (!string.IsNullOrEmpty(completionError))
                    record.lastError = completionError;
                else if (handled)
                    record.lastError = "";
            };
            record.runtimeLoopPointHooked = true;
        }

        // For the first hosted bind we still need a tiny eager RT, but once a
        // live hosted screen already has a real RT attached we should not
        // downgrade it back to 16x16 during a media switch. Doing that forces a
        // transient square rebind before the new movie dimensions arrive.
        if (record.renderTexture == null)
        {
            if (!TryEnsureStandalonePlayerRenderTexture(record, 16, 16))
            {
                errorMessage = string.IsNullOrEmpty(record.lastError)
                    ? "player render texture not created"
                    : record.lastError;
                return false;
            }
        }
        else
        {
            try
            {
                if (record.videoPlayer != null && record.videoPlayer.targetTexture != record.renderTexture)
                    record.videoPlayer.targetTexture = record.renderTexture;
            }
            catch (Exception ex)
            {
                errorMessage = "player render texture reuse failed: " + ex.Message;
                record.lastError = errorMessage;
                return false;
            }
        }

        return true;
    }

    private void DestroyStandalonePlayerImageTexture(StandalonePlayerRecord record)
    {
        if (record == null || record.imageTexture == null)
            return;

        try
        {
            UnityEngine.Object.Destroy(record.imageTexture);
        }
        catch
        {
        }

        record.imageTexture = null;
    }

    private bool TryLoadStandalonePlayerImageTexture(
        StandalonePlayerRecord record,
        string resolvedMediaPath,
        out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "player record missing";
            return false;
        }

        if (string.IsNullOrEmpty(resolvedMediaPath))
        {
            errorMessage = "mediaPath could not be resolved";
            record.lastError = errorMessage;
            return false;
        }

        byte[] imageBytes;
        try
        {
            if (!FileManagerSecure.FileExists(resolvedMediaPath, false))
            {
                errorMessage = "image file not found";
                record.lastError = errorMessage;
                return false;
            }

            imageBytes = FileManagerSecure.ReadAllBytes(resolvedMediaPath);
        }
        catch (Exception ex)
        {
            errorMessage = "image read failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }

        if (imageBytes == null || imageBytes.Length <= 0)
        {
            errorMessage = "image file was empty";
            record.lastError = errorMessage;
            return false;
        }

        Texture2D loadedTexture = null;
        try
        {
            loadedTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!loadedTexture.LoadImage(imageBytes, false))
            {
                errorMessage = "image decode failed";
                record.lastError = errorMessage;
                return false;
            }

            loadedTexture.name = "FAPlayerImage_" + SanitizeStandalonePlayerName(record.playbackKey);
            loadedTexture.wrapMode = TextureWrapMode.Clamp;
            loadedTexture.filterMode = FilterMode.Bilinear;

            try
            {
                if (record.videoPlayer != null)
                {
                    record.videoPlayer.Stop();
                    record.videoPlayer.targetTexture = null;
                    record.videoPlayer.url = "";
                }
            }
            catch
            {
            }

            if (record.renderTexture != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(record.renderTexture);
                }
                catch
                {
                }

                record.renderTexture = null;
            }

            DestroyStandalonePlayerImageTexture(record);
            record.imageTexture = loadedTexture;
            record.mediaIsStillImage = true;
            record.prepared = true;
            record.preparePending = false;
            record.prepareStartedAt = 0f;
            record.desiredPlaying = false;
            record.nextPlaybackStateApplyTime = 0f;
            record.lastError = "";
            record.needsScreenRefresh = true;
            TryResolveTextureDimensions(loadedTexture, out record.textureWidth, out record.textureHeight);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "image load failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(errorMessage) && loadedTexture != null && loadedTexture != record.imageTexture)
            {
                try
                {
                    UnityEngine.Object.Destroy(loadedTexture);
                }
                catch
                {
                }
            }
        }
    }

    private bool TryEnsureStandalonePlayerRenderTexture(StandalonePlayerRecord record, int width, int height)
    {
        if (record == null)
            return false;

        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);
        if (record.renderTexture != null
            && record.renderTexture.width == width
            && record.renderTexture.height == height)
        {
            try
            {
                if (record.videoPlayer != null && record.videoPlayer.targetTexture != record.renderTexture)
                    record.videoPlayer.targetTexture = record.renderTexture;
            }
            catch
            {
            }

            return true;
        }

        try
        {
            if (record.videoPlayer != null)
                record.videoPlayer.targetTexture = null;
        }
        catch
        {
        }

        if (record.renderTexture != null)
        {
            try
            {
                UnityEngine.Object.Destroy(record.renderTexture);
            }
            catch
            {
            }

            record.renderTexture = null;
        }

        try
        {
            RenderTexture renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.name = "FAPlayerRT_" + SanitizeStandalonePlayerName(record.playbackKey);
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();
            record.renderTexture = renderTexture;

            if (record.videoPlayer != null)
            {
                record.videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                record.videoPlayer.targetTexture = renderTexture;
            }

            record.needsScreenRefresh = true;
            return true;
        }
        catch (Exception ex)
        {
            record.lastError = "player render texture failed: " + ex.Message;
            return false;
        }
    }

    private void ApplyStandalonePlayerAudioState(StandalonePlayerRecord record)
    {
        if (record == null || record.videoPlayer == null || record.audioSource == null)
            return;

        try
        {
            record.audioSource.mute = record.muted;
            record.audioSource.volume = record.muted ? 0f : MapStandalonePlayerNormalizedVolumeToAudioGain(record.storedVolume);
            record.videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            record.videoPlayer.EnableAudioTrack(0, true);
            record.videoPlayer.SetTargetAudioSource(0, record.audioSource);
        }
        catch (Exception ex)
        {
            record.lastError = "player audio state failed: " + ex.Message;
        }
    }

    private static float MapStandalonePlayerNormalizedVolumeToAudioGain(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (normalized <= 0f)
            return 0f;

        return Mathf.Pow(normalized, StandalonePlayerVolumeCurveExponent);
    }

    private bool TryResolveStandalonePlayerSourceTexture(
        StandalonePlayerRecord record,
        out Texture sourceTexture,
        out Vector2 sourceScale,
        out Vector2 sourceOffset,
        out string sourceName)
    {
        sourceTexture = null;
        sourceScale = Vector2.one;
        sourceOffset = Vector2.zero;
        sourceName = "";

        if (record == null)
            return false;

        if (record.mediaIsStillImage && record.imageTexture != null)
        {
            sourceTexture = record.imageTexture;
            sourceName = "StandalonePlayer.imageTexture";
            return true;
        }

        if (record.renderTexture != null
            && record.textureWidth > 0
            && record.textureHeight > 0)
        {
            try
            {
                if (record.renderTexture.width != record.textureWidth
                    || record.renderTexture.height != record.textureHeight)
                {
                    if (!TryEnsureStandalonePlayerRenderTexture(record, record.textureWidth, record.textureHeight))
                    {
                        if (string.IsNullOrEmpty(record.lastError))
                            record.lastError = "player render texture did not resize to detected video dimensions";
                    }
                }
            }
            catch (Exception ex)
            {
                record.lastError = "player source texture sync failed: " + ex.Message;
            }
        }

        if (record.renderTexture != null)
        {
            sourceTexture = record.renderTexture;
            sourceName = "VideoPlayer.targetTexture";
            return true;
        }

        if (record.videoPlayer != null)
        {
            try
            {
                Texture liveVideoTexture = record.videoPlayer.texture;
                if (liveVideoTexture != null)
                {
                    int liveWidth;
                    int liveHeight;
                    TryResolveTextureDimensions(liveVideoTexture, out liveWidth, out liveHeight);
                    if (liveWidth > 16
                        && liveHeight > 16
                        && (record.textureWidth <= 0
                            || record.textureHeight <= 0
                            || (liveWidth == record.textureWidth && liveHeight == record.textureHeight)))
                    {
                        sourceTexture = liveVideoTexture;
                        sourceName = "VideoPlayer.texture";
                        return true;
                    }
                }
            }
            catch
            {
            }
        }

        if (record.videoPlayer != null)
        {
            try
            {
                sourceTexture = record.videoPlayer.texture;
                if (sourceTexture != null)
                {
                    sourceName = "VideoPlayer.texture";
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private bool TryRefreshStandalonePlayerScreenBinding(StandalonePlayerRecord record, out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "player record missing";
            return false;
        }

        if (IsHostedPlayerInstanceId(record.instanceId))
        {
            string hostAtomUid = ResolveHostedPlayerHostAtomUid(record);
            if (string.IsNullOrEmpty(hostAtomUid))
            {
                errorMessage = "hosted player host atom uid not resolved";
                return false;
            }

            HostedPlayerSurfaceContract hostedContract;
            if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out hostedContract, out errorMessage) || hostedContract == null)
                return false;

            return TryRefreshHostedStandalonePlayerScreenBinding(
                hostAtomUid,
                record.binding != null ? record.binding.surfaceTargetId : "player:screen",
                record,
                hostedContract,
                out errorMessage);
        }

        string bindingOwnerAtomUid = "";
        if (record.binding != null
            && !string.IsNullOrEmpty(record.binding.atomUid)
            && !IsStandalonePlayerBindingAtomUid(record.binding.atomUid))
        {
            bindingOwnerAtomUid = record.binding.atomUid;
        }

        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        if (!TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
            return false;

        Texture sourceTexture;
        Vector2 sourceScale;
        Vector2 sourceOffset;
        string sourceName;
        if (!TryResolveStandalonePlayerSourceTexture(record, out sourceTexture, out sourceScale, out sourceOffset, out sourceName)
            || sourceTexture == null)
        {
            errorMessage = "player source texture unavailable";
            return false;
        }

        if (record.binding != null)
        {
            TryRestoreDisconnectSurface(record.binding);
            record.binding = null;
        }

        Renderer[] attachedRenderers;
        Material[][] originalMaterials;
        Material[][] appliedMaterials;
        string debugJson;
        if (!TryAttachStandalonePlayerScreenMaterial(
            record,
            instance,
            slot,
            sourceTexture,
            sourceScale,
            sourceOffset,
            sourceName,
            out attachedRenderers,
            out originalMaterials,
            out appliedMaterials,
            out debugJson,
            out errorMessage))
        {
            return false;
        }

        string visibilityError;
        if (!TryApplyBoundPlayerSurfaceVisibility(instance, slot, null, out visibilityError))
        {
            errorMessage = visibilityError;
            return false;
        }

        PlayerScreenBindingRecord nextBinding = new PlayerScreenBindingRecord();
        nextBinding.atomUid = !string.IsNullOrEmpty(bindingOwnerAtomUid)
            ? bindingOwnerAtomUid
            : (StandalonePlayerBindingAtomUidPrefix + record.playbackKey);
        nextBinding.instanceId = instance.instanceId;
        nextBinding.slotId = slot.slotId;
        nextBinding.displayId = slot.displayId;
        nextBinding.screenBindingMode = "session_scene_surface";
        nextBinding.screenContractVersion = instance.screenContractVersion ?? "";
        nextBinding.disconnectStateId = slot.disconnectStateId ?? "";
        nextBinding.surfaceTargetId = string.IsNullOrEmpty(slot.surfaceTargetId) ? "player:screen" : slot.surfaceTargetId;
        nextBinding.embeddedHostAtomUid = !string.IsNullOrEmpty(bindingOwnerAtomUid) ? bindingOwnerAtomUid : "";
        nextBinding.aspectMode = record.aspectMode;
        nextBinding.debugJson = string.IsNullOrEmpty(debugJson) ? "{}" : debugJson;
        nextBinding.screenSurfaceRenderers = attachedRenderers ?? new Renderer[0];
        nextBinding.originalSurfaceMaterials = originalMaterials ?? new Material[0][];
        nextBinding.appliedSurfaceMaterials = appliedMaterials ?? new Material[0][];

        if (!TryApplyBoundPlayerSurfaceVisibility(instance, slot, nextBinding, out visibilityError))
        {
            errorMessage = visibilityError;
            return false;
        }

        record.binding = nextBinding;
        if (!string.IsNullOrEmpty(bindingOwnerAtomUid))
            playerScreenBindings[bindingOwnerAtomUid] = nextBinding;
        record.needsScreenRefresh = false;
        return true;
    }

    private bool TryAttachStandalonePlayerScreenMaterial(
        StandalonePlayerRecord record,
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        Texture directSourceTexture,
        Vector2 directSourceScale,
        Vector2 directSourceOffset,
        string directSourceName,
        out Renderer[] attachedRenderers,
        out Material[][] originalMaterials,
        out Material[][] appliedMaterials,
        out string debugJson,
        out string errorMessage)
    {
        attachedRenderers = new Renderer[0];
        originalMaterials = new Material[0][];
        appliedMaterials = new Material[0][];
        debugJson = "{}";
        errorMessage = "";

        if (record == null || slot == null || directSourceTexture == null)
        {
            errorMessage = "player attach arguments invalid";
            return false;
        }

        DestroyRuntimeMediaSurface(slot);

        GameObject mediaTargetObject = ResolvePlayerMediaTargetObject(instance, slot);
        if (mediaTargetObject == null)
        {
            errorMessage = "screen media target not found";
            return false;
        }

        Renderer[] targetRenderers = mediaTargetObject.GetComponentsInChildren<Renderer>(true);
        if (targetRenderers == null || targetRenderers.Length <= 0)
        {
            errorMessage = "screen surface renderers not found";
            return false;
        }

        Material targetBasisMaterial = null;
        for (int i = 0; i < targetRenderers.Length && targetBasisMaterial == null; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            Material[] currentMaterials = renderer.sharedMaterials;
            if (currentMaterials == null)
                continue;

            for (int j = 0; j < currentMaterials.Length; j++)
            {
                if (currentMaterials[j] == null)
                    continue;
                targetBasisMaterial = currentMaterials[j];
                break;
            }
        }

        bool usingDisconnectSurfaceTarget = UsesDisconnectSurfaceAsMediaTarget(instance, slot);
        bool preserveProjectedAlpha = ShouldPreserveProjectedScreenAlpha(slot);
        bool isScreenCoreSurface = IsAuthoredScreenSurfacePresentationTarget(instance, slot, mediaTargetObject);
        bool isHostedMetaDirectSurface =
            string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenContractVersion, StringComparison.OrdinalIgnoreCase)
            && !isScreenCoreSurface;
        bool preferDeterministicVideoOverlay = usingDisconnectSurfaceTarget || isScreenCoreSurface;
        Material projectedMaterial;
        if (!TryCreateResolvedVideoTextureMaterialFromTexture(
            targetBasisMaterial,
            directSourceTexture,
            directSourceScale,
            directSourceOffset,
            preferDeterministicVideoOverlay,
            preserveProjectedAlpha,
            out projectedMaterial))
        {
            errorMessage = "player projected screen material not created";
            return false;
        }

        ProjectedMaterialCandidate selectedCandidate = new ProjectedMaterialCandidate
        {
            material = null,
            score = int.MaxValue,
            rendererName = string.IsNullOrEmpty(directSourceName) ? "VideoPlayer.texture" : directSourceName
        };

        float contentAspect = 0f;
        TryResolveProjectedContentAspect(projectedMaterial, directSourceTexture, out contentAspect);

        float targetSurfaceAspect = 0f;
        TryResolveSurfaceAspect(mediaTargetObject, out targetSurfaceAspect);

        bool useFitBlackPresentation = ShouldUseFitBlackAspectMode(record.aspectMode) && !isHostedMetaDirectSurface;
        bool useWidthLockedPresentation = ShouldUseWidthLockedAspectMode(record.aspectMode);
        bool useCropFillPresentation = ShouldUseCropFillAspectMode(record.aspectMode);
        bool useDirectProjectedMaterial =
            ShouldUseDirectProjectedMaterialPath(instance)
            // Restored from the 0.94 screen-core parity seam: authored screen-core assets
            // can take direct crop/fill on screen_surface, while fit/full_width still
            // route through the explicit overlay/backdrop path below.
            && !FrameAngelPlayerMediaParity.ShouldUseOverlayPresentation(false, record.aspectMode)
            && !slot.mediaTargetUsesNormalizedRect;

        if (isScreenCoreSurface && directSourceTexture != null)
        {
            float rawTextureAspect;
            if (TryResolveTextureAspect(null, directSourceTexture, out rawTextureAspect) && rawTextureAspect > 0.001f)
                contentAspect = rawTextureAspect;
        }

        if (useDirectProjectedMaterial)
        {
            if (usingDisconnectSurfaceTarget)
                MirrorProjectedScreenTextureHorizontally(projectedMaterial);

            if (useCropFillPresentation)
                ApplyAspectCropToMaterial(projectedMaterial, targetSurfaceAspect, contentAspect);

            if (!TryApplyProjectedMaterialToTargetRenderers(
                targetRenderers,
                projectedMaterial,
                out attachedRenderers,
                out originalMaterials,
                out appliedMaterials,
                out errorMessage))
            {
                return false;
            }

            Material debugMaterial = null;
            if (attachedRenderers != null && attachedRenderers.Length > 0 && attachedRenderers[0] != null)
            {
                Material[] debugMaterials = attachedRenderers[0].sharedMaterials;
                if (debugMaterials != null && debugMaterials.Length > 0)
                    debugMaterial = debugMaterials[0];
            }

            debugJson = BuildPlayerScreenBindingDebugJson(
                "",
                null,
                mediaTargetObject,
                usingDisconnectSurfaceTarget ? "disconnect_direct_material" : "direct_target_material",
                "standalone_videoplayer",
                new Renderer[0],
                new List<ProjectedMaterialCandidate>(),
                selectedCandidate,
                attachedRenderers,
                debugMaterial,
                directSourceTexture);
            return true;
        }

        Renderer[] backdropRenderers = new Renderer[0];
        Material[][] backdropOriginalMaterials = new Material[0][];
        Material[][] backdropAppliedMaterials = new Material[0][];

        bool applyBackdropToTarget =
            FrameAngelPlayerMediaParity.ShouldApplyBackdropToTarget(
                record.aspectMode,
                usingDisconnectSurfaceTarget,
                isScreenCoreSurface);

        if (applyBackdropToTarget)
        {
            Material backdropMaterial;
            if (!TryCreateBlackBackdropMaterial(targetBasisMaterial, out backdropMaterial))
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }

                errorMessage = "player overlay backdrop material not created";
                return false;
            }

            if (!TryApplyProjectedMaterialToTargetRenderers(
                targetRenderers,
                backdropMaterial,
                out backdropRenderers,
                out backdropOriginalMaterials,
                out backdropAppliedMaterials,
                out errorMessage))
            {
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }

                return false;
            }

            // Keep the backdrop material untouched here. For fit it provides the
            // true black letterbox slab, and for full_width it keeps the authored
            // front screen from staying light-reactive around the centered movie
            // while the overlay quad preserves aspect on top.
        }

        float runtimeSurfaceAspect = contentAspect;
        if (useCropFillPresentation)
        {
            ApplyAspectCropToMaterial(projectedMaterial, targetSurfaceAspect, contentAspect);
            runtimeSurfaceAspect = targetSurfaceAspect > 0.001f
                ? targetSurfaceAspect
                : contentAspect;
        }

        if (ShouldFlipProjectedOverlayVertically(directSourceTexture, directSourceName, isScreenCoreSurface))
        {
            FlipProjectedScreenTextureVertically(projectedMaterial);
        }

        if (isScreenCoreSurface)
        {
            // The authored screen-core runtime overlay now stays on the real front
            // face, so it needs an explicit horizontal mirror to keep the visible
            // operator side readable without the older Y-180 fallback correction.
            MirrorProjectedScreenTextureHorizontally(projectedMaterial);
        }

        if (isScreenCoreSurface)
        {
            // The direct-CUA screen-core overlay should behave like a real front
            // screen, not a double-sided floating helper. Keep the rear black so
            // we can diagnose true aspect/alignment issues without back-face bleed.
            TryEnableScreenCoreOverlayDepthOcclusion(projectedMaterial);
            TryForceProjectedScreenFrontFaceOnly(projectedMaterial);
        }

        Renderer overlayRenderer;
        Vector3 runtimeTargetLocalCenter;
        Vector3 runtimeTargetLocalSize;
        Vector3 runtimeOverlayLocalPosition;
        Vector3 runtimeOverlayLocalScale;
        if (!TryCreateRuntimeMediaSurface(
            slot,
            mediaTargetObject,
            projectedMaterial,
            runtimeSurfaceAspect,
            record.aspectMode,
            out overlayRenderer,
            out runtimeTargetLocalCenter,
            out runtimeTargetLocalSize,
            out runtimeOverlayLocalPosition,
            out runtimeOverlayLocalScale,
            out errorMessage))
        {
            try
            {
                UnityEngine.Object.Destroy(projectedMaterial);
            }
            catch
            {
            }

            TryRestoreScreenSurfaceMaterials(backdropRenderers, backdropOriginalMaterials);
            DestroyAppliedScreenSurfaceMaterials(backdropAppliedMaterials);
            return false;
        }

        Renderer runtimeBackdropRenderer = null;
        Material runtimeBackdropMaterial = null;
        if (isScreenCoreSurface)
        {
            if (!TryCreateRuntimeMediaBackingSurface(
                slot.runtimeMediaSurfaceObject,
                slot.slotId,
                targetBasisMaterial,
                out runtimeBackdropRenderer,
                out runtimeBackdropMaterial,
                out errorMessage))
            {
                DestroyRuntimeMediaSurface(slot);
                try
                {
                    UnityEngine.Object.Destroy(projectedMaterial);
                }
                catch
                {
                }

                TryRestoreScreenSurfaceMaterials(backdropRenderers, backdropOriginalMaterials);
                DestroyAppliedScreenSurfaceMaterials(backdropAppliedMaterials);
                return false;
            }
        }

        bool applyFrontFaceCorrection =
            (useFitBlackPresentation && usingDisconnectSurfaceTarget)
            || (slot != null && slot.forceOperatorFacingFrontFace);

        if (applyFrontFaceCorrection)
        {
            // Keep front-face correction scoped to the exact overlay-backed seams
            // that still need it: the older disconnect-surface fallback path and
            // explicit force-flag contracts. The bare screen-core runtime overlay
            // already uses the authored front-screen rotation contract and should
            // stay operator-facing without the legacy Y-180 flip.
            ApplyStandaloneFitOverlayFrontFace(overlayRenderer);
        }

        if (backdropRenderers != null && backdropRenderers.Length > 0)
        {
            attachedRenderers = AppendRenderer(backdropRenderers, overlayRenderer);
            originalMaterials = AppendMaterialRows(backdropOriginalMaterials, new[] { new Material[0] });
            appliedMaterials = AppendMaterialRows(backdropAppliedMaterials, new[] { new[] { projectedMaterial } });
        }
        else
        {
            attachedRenderers = overlayRenderer != null ? new[] { overlayRenderer } : new Renderer[0];
            originalMaterials = new[] { new Material[0] };
            appliedMaterials = new[] { new[] { projectedMaterial } };
        }

        if (runtimeBackdropRenderer != null && runtimeBackdropMaterial != null)
        {
            attachedRenderers = AppendRenderer(attachedRenderers, runtimeBackdropRenderer);
            originalMaterials = AppendMaterialRows(originalMaterials, new[] { new Material[0] });
            appliedMaterials = AppendMaterialRows(appliedMaterials, new[] { new[] { runtimeBackdropMaterial } });
        }

        debugJson = BuildPlayerScreenBindingDebugJson(
            "",
            null,
            mediaTargetObject,
            usingDisconnectSurfaceTarget ? "disconnect_overlay_quad" : "runtime_overlay_quad",
            record.mediaIsStillImage ? "standalone_image" : "standalone_videoplayer",
            new Renderer[0],
            new List<ProjectedMaterialCandidate>(),
            selectedCandidate,
            attachedRenderers,
            projectedMaterial,
            directSourceTexture);
        Vector2 projectedScale;
        Vector2 projectedOffset;
        TryResolveMaterialPrimaryScaleOffset(projectedMaterial, out projectedScale, out projectedOffset);
        int sourceTextureWidth;
        int sourceTextureHeight;
        TryResolveTextureDimensions(directSourceTexture, out sourceTextureWidth, out sourceTextureHeight);
        debugJson = AppendJsonProperty(debugJson, "contentAspect", FormatFloat(contentAspect));
        debugJson = AppendJsonProperty(debugJson, "targetSurfaceAspect", FormatFloat(targetSurfaceAspect));
        debugJson = AppendJsonProperty(debugJson, "sourceTextureWidth", sourceTextureWidth.ToString(CultureInfo.InvariantCulture));
        debugJson = AppendJsonProperty(debugJson, "sourceTextureHeight", sourceTextureHeight.ToString(CultureInfo.InvariantCulture));
        debugJson = AppendJsonProperty(debugJson, "projectedTextureScale", BuildVector2Json(projectedScale));
        debugJson = AppendJsonProperty(debugJson, "projectedTextureOffset", BuildVector2Json(projectedOffset));
        debugJson = AppendJsonProperty(debugJson, "runtimeTargetLocalCenter", BuildVector3Json(runtimeTargetLocalCenter));
        debugJson = AppendJsonProperty(debugJson, "runtimeTargetLocalSize", BuildVector3Json(runtimeTargetLocalSize));
        debugJson = AppendJsonProperty(debugJson, "runtimeOverlayLocalPosition", BuildVector3Json(runtimeOverlayLocalPosition));
        debugJson = AppendJsonProperty(debugJson, "runtimeOverlayLocalScale", BuildVector3Json(runtimeOverlayLocalScale));
        return true;
    }

    private void ApplyStandaloneFitOverlayFrontFace(Renderer overlayRenderer)
    {
        if (overlayRenderer == null)
            return;

        try
        {
            Transform overlayTransform = overlayRenderer.transform;
            if (overlayTransform == null)
                return;

            // The standalone fit path presents a runtime quad over the disconnect
            // slab. On VaM's available fallback sprite shader path the quad can
            // remain mirrored even after material UV changes, so we correct the
            // operator-facing side by turning the quad to present its front face.
            overlayTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        catch
        {
        }
    }

    private void TryForceProjectedScreenFrontFaceOnly(Material material)
    {
        if (material == null)
            return;

        TrySetMaterialFloat(material, "_Cull", 2f);
    }

    private void TryEnableScreenCoreOverlayDepthOcclusion(Material material)
    {
        if (material == null)
            return;

        TrySetMaterialFloat(material, "_ZWrite", 1f);
        TrySetMaterialFloat(material, "_Mode", 0f);
        TrySetMaterialFloat(material, "_Surface", 0f);
        TrySetMaterialFloat(material, "_SrcBlend", 1f);
        TrySetMaterialFloat(material, "_DstBlend", 0f);
        TrySetMaterialFloat(material, "_Blend", 0f);
        TrySetMaterialFloat(material, "_AlphaClip", 0f);

        try
        {
            material.DisableKeyword("_ALPHATEST_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_ALPHABLEND_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        catch
        {
        }

        try
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        catch
        {
        }

        try
        {
            material.SetOverrideTag("RenderType", "Opaque");
        }
        catch
        {
        }

        try
        {
            if (material.renderQueue < ScreenCoreOverlayRenderQueue)
                material.renderQueue = ScreenCoreOverlayRenderQueue;
        }
        catch
        {
        }
    }

    private void DestroyStandalonePlayerRecord(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        StopStandalonePlayerResize(record);
        if (record.videoPlayer != null)
        {
            try
            {
                record.videoPlayer.Stop();
            }
            catch
            {
            }
        }

        if (record.binding != null)
        {
            TryRestoreDisconnectSurface(record.binding);
            record.binding = null;
        }

        if (record.renderTexture != null)
        {
            try
            {
                UnityEngine.Object.Destroy(record.renderTexture);
            }
            catch
            {
            }

            record.renderTexture = null;
        }

        DestroyStandalonePlayerImageTexture(record);

        if (record.runtimeObject != null)
        {
            try
            {
                UnityEngine.Object.Destroy(record.runtimeObject);
            }
            catch
            {
            }

            record.runtimeObject = null;
        }

        record.videoPlayer = null;
        record.audioSource = null;
    }

    private string BuildStandalonePlayerSelectedStateJson(string argsJson)
    {
        List<StandalonePlayerRecord> selectedRecords = new List<StandalonePlayerRecord>();
        if (HasStandalonePlayerSelector(argsJson))
        {
            StandalonePlayerRecord selectedRecord;
            string ignoredError;
            if (TryResolveStandalonePlayerRecord(argsJson, out selectedRecord, out ignoredError) && selectedRecord != null)
                selectedRecords.Add(selectedRecord);
        }
        else
        {
            foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
            {
                if (kvp.Value != null)
                    selectedRecords.Add(kvp.Value);
            }
        }

        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"").Append(StandalonePlayerStateSchemaVersion).Append("\",");
        sb.Append("\"recordCount\":").Append(selectedRecords.Count).Append(',');
        sb.Append("\"records\":[");
        for (int i = 0; i < selectedRecords.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            AppendStandalonePlayerRecordJson(sb, selectedRecords[i]);
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private void RefreshVisiblePlayerDebugFields()
    {
        if (playerRuntimeTargetField == null
            && playerRuntimeMediaField == null
            && playerRuntimeStateField == null
            && playerRuntimeParityField == null
            && playerRuntimeTimelineField == null
            && playerRuntimePlaylistField == null
            && playerScrubNormalizedField == null
            && playerVolumeNormalizedField == null)
        {
            return;
        }

        StandalonePlayerRecord record;
        Atom hostAtom;
        if (!TryResolveAttachedHostedStandalonePlayerRecord(out record, out hostAtom) || record == null)
        {
            EnsurePendingPlayerDebugSummaries();
            if (playerRuntimeTargetField != null)
                playerRuntimeTargetField.valNoCallback = playerPendingTargetSummary;
            if (playerRuntimeMediaField != null)
                playerRuntimeMediaField.valNoCallback = playerPendingMediaSummary;
            if (playerRuntimeStateField != null)
                playerRuntimeStateField.valNoCallback = playerPendingStateSummary;
            if (playerRuntimeParityField != null)
                playerRuntimeParityField.valNoCallback = playerPendingParitySummary;
            if (playerRuntimeTimelineField != null)
                playerRuntimeTimelineField.valNoCallback = playerPendingTimelineSummary;
            if (playerRuntimePlaylistField != null)
                playerRuntimePlaylistField.valNoCallback = playerPendingPlaylistSummary;
            UpdateStandalonePlayerSliderFields(0f, 1f);
            return;
        }

        bool bindingMissing = record.binding == null;
        string effectiveLastError = !string.IsNullOrEmpty(record.lastError)
            ? record.lastError
            : (bindingMissing ? "screen owner unresolved" : "");
        string debugJson = record.binding != null && !string.IsNullOrEmpty(record.binding.debugJson)
            ? record.binding.debugJson
            : "{}";
        string mediaTargetObject = ExtractJsonArgString(debugJson, "mediaTargetObject");
        string attachMode = ExtractJsonArgString(debugJson, "attachMode");
        string projectionMode = ExtractJsonArgString(debugJson, "projectionMode");
        string selectedShader = ExtractJsonArgString(debugJson, "selectedShader");
        string selectedTexture = ExtractJsonArgString(debugJson, "selectedTexture");

        bool isPlaying = false;
        double currentTimeSeconds = 0d;
        double currentDurationSeconds = 0d;
        double currentTimeNormalized = 0d;
        if (!record.mediaIsStillImage)
        {
            try
            {
                if (record.videoPlayer != null)
                {
                    isPlaying = record.videoPlayer.isPlaying;
                    currentTimeSeconds = Math.Max(0d, record.videoPlayer.time);
                    currentDurationSeconds = GetStandalonePlayerDurationSeconds(record);
                }
            }
            catch
            {
                isPlaying = false;
                currentTimeSeconds = 0d;
                currentDurationSeconds = 0d;
            }
        }
        if (double.IsNaN(currentTimeSeconds) || double.IsInfinity(currentTimeSeconds))
            currentTimeSeconds = 0d;
        if (double.IsNaN(currentDurationSeconds) || double.IsInfinity(currentDurationSeconds))
            currentDurationSeconds = 0d;
        if (currentDurationSeconds > 0.0001d)
            currentTimeNormalized = Math.Max(0d, Math.Min(1d, currentTimeSeconds / currentDurationSeconds));
        if (double.IsNaN(currentTimeNormalized) || double.IsInfinity(currentTimeNormalized))
            currentTimeNormalized = 0d;

        int renderTextureWidth = 0;
        int renderTextureHeight = 0;
        TryResolveTextureDimensions(record.renderTexture, out renderTextureWidth, out renderTextureHeight);
        float displayWidthMeters = 0f;
        float displayHeightMeters = 0f;
        FAInnerPiecePlaneData plane;
        string ignoredError;
        if (TryResolveInnerPieceScreenPlane(record.instanceId, record.slotId, out plane, out ignoredError))
        {
            displayWidthMeters = plane.widthMeters;
            displayHeightMeters = plane.heightMeters;
        }

        string targetSummary = "target="
            + (string.IsNullOrEmpty(mediaTargetObject) ? "unresolved" : mediaTargetObject)
            + " contract="
            + (record.binding != null && !string.IsNullOrEmpty(record.binding.screenContractVersion)
                ? record.binding.screenContractVersion
                : "none")
            + " mode="
            + (string.IsNullOrEmpty(attachMode) ? "unknown" : attachMode);

        string mediaName = GetStandalonePlayerCurrentPlaylistPath(record);
        if (string.IsNullOrEmpty(mediaName))
            mediaName = record.resolvedMediaPath;
        if (string.IsNullOrEmpty(mediaName))
            mediaName = playerMediaPath;
        mediaName = TryGetPathLeafName(mediaName);
        if (string.IsNullOrEmpty(mediaName))
            mediaName = "none";

        string mediaSummary = "media="
            + mediaName
            + " kind="
            + (record.mediaIsStillImage ? "image" : "video")
            + " playing="
            + (isPlaying ? "true" : "false")
            + " aspect="
            + FrameAngelPlayerMediaParity.DescribeAspectMode(record.aspectMode)
            + " rt="
            + renderTextureWidth.ToString(CultureInfo.InvariantCulture)
            + "x"
            + renderTextureHeight.ToString(CultureInfo.InvariantCulture);

        string stateKind = !string.IsNullOrEmpty(effectiveLastError)
            ? "error"
            : (record.preparePending ? "loading" : "ok");

        string stateSummary = "state="
            + stateKind
            + " proj="
            + (string.IsNullOrEmpty(projectionMode) ? "unknown" : projectionMode)
            + " shader="
            + (string.IsNullOrEmpty(selectedShader) ? "unknown" : selectedShader)
            + " tex="
            + (string.IsNullOrEmpty(selectedTexture) ? "none" : selectedTexture);

        if (hostAtom != null && !string.IsNullOrEmpty(hostAtom.uid))
            stateSummary += " atom=" + hostAtom.uid;
        if (!string.IsNullOrEmpty(effectiveLastError))
            stateSummary += " err=" + effectiveLastError;
        string paritySummary = BuildStandalonePlayerParitySummary(debugJson, record.aspectMode);
        string timelineSummary = "time="
            + currentTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + "/"
            + currentDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " norm="
            + currentTimeNormalized.ToString("0.###", CultureInfo.InvariantCulture);
        string playlistSummary = "item="
            + (record.currentIndex >= 0 ? (record.currentIndex + 1).ToString(CultureInfo.InvariantCulture) : "0")
            + "/"
            + record.playlistPaths.Count.ToString(CultureInfo.InvariantCulture)
            + " loop="
            + NormalizeStandalonePlayerLoopMode(record.loopMode)
            + " random="
            + (record.randomEnabled ? "on" : "off")
            + " volume="
            + record.volume.ToString("0.##", CultureInfo.InvariantCulture)
            + " size="
            + displayWidthMeters.ToString("0.###", CultureInfo.InvariantCulture)
            + "x"
            + displayHeightMeters.ToString("0.###", CultureInfo.InvariantCulture);
        if (record.playlistPaths == null || record.playlistPaths.Count <= 0)
        {
            string selectedDirectory = TryGetPathParentLeafName(playerMediaPath);
            if (!string.IsNullOrEmpty(selectedDirectory))
                playlistSummary += " dir=" + selectedDirectory;
        }

        playerPendingTargetSummary = targetSummary;
        playerPendingMediaSummary = mediaSummary;
        playerPendingStateSummary = stateSummary;
        playerPendingParitySummary = paritySummary;
        playerPendingTimelineSummary = timelineSummary;
        playerPendingPlaylistSummary = playlistSummary;

        if (playerRuntimeTargetField != null)
            playerRuntimeTargetField.valNoCallback = targetSummary;
        if (playerRuntimeMediaField != null)
            playerRuntimeMediaField.valNoCallback = mediaSummary;
        if (playerRuntimeStateField != null)
            playerRuntimeStateField.valNoCallback = stateSummary;
        if (playerRuntimeParityField != null)
            playerRuntimeParityField.valNoCallback = paritySummary;
        if (playerRuntimeTimelineField != null)
            playerRuntimeTimelineField.valNoCallback = timelineSummary;
        if (playerRuntimePlaylistField != null)
            playerRuntimePlaylistField.valNoCallback = playlistSummary;
        UpdateStandalonePlayerSliderFields((float)currentTimeNormalized, record.volume);
    }

    private void UpdateStandalonePlayerSliderFields(float scrubNormalized, float volumeNormalized)
    {
        if (playerScrubNormalizedField == null && playerVolumeNormalizedField == null)
            return;

        float now = Time.unscaledTime;
        suppressStandalonePlayerSliderCallbacks = true;
        try
        {
            if (playerScrubNormalizedField != null && now >= standalonePlayerScrubFieldSyncHoldoffUntil)
                playerScrubNormalizedField.valNoCallback = Mathf.Clamp01(scrubNormalized);
            if (playerVolumeNormalizedField != null)
                playerVolumeNormalizedField.valNoCallback = Mathf.Clamp01(volumeNormalized);
        }
        finally
        {
            suppressStandalonePlayerSliderCallbacks = false;
        }
    }

    private void ArmStandalonePlayerScrubFieldSyncHoldoff()
    {
        standalonePlayerScrubFieldSyncHoldoffUntil = Mathf.Max(
            standalonePlayerScrubFieldSyncHoldoffUntil,
            Time.unscaledTime + StandalonePlayerScrubDisplayHoldoffSeconds);
    }

    private bool TryResolveAttachedHostedStandalonePlayerRecord(out StandalonePlayerRecord record, out Atom hostAtom)
    {
        record = null;
        hostAtom = null;

        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            return false;

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (string.IsNullOrEmpty(hostAtomUid))
            return false;

        return standalonePlayerRecords.TryGetValue(BuildHostedPlayerPlaybackKey(hostAtomUid), out record)
            && record != null;
    }

    private string TryGetPathLeafName(string rawPath)
    {
        string value = string.IsNullOrEmpty(rawPath) ? "" : rawPath.Trim();
        if (string.IsNullOrEmpty(value))
            return "";

        int slashIndex = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        if (slashIndex < 0 || slashIndex >= value.Length - 1)
            return value;

        return value.Substring(slashIndex + 1);
    }

    private void EnsurePendingPlayerDebugSummaries()
    {
        if (string.IsNullOrEmpty(playerPendingTargetSummary))
            playerPendingTargetSummary = BuildPendingPlayerTargetSummary();
        if (string.IsNullOrEmpty(playerPendingMediaSummary))
            playerPendingMediaSummary = BuildPendingPlayerMediaSummary();
        if (string.IsNullOrEmpty(playerPendingStateSummary))
            playerPendingStateSummary = "state=no_record";
        if (string.IsNullOrEmpty(playerPendingParitySummary))
            playerPendingParitySummary = BuildPendingPlayerParitySummary();
        if (string.IsNullOrEmpty(playerPendingTimelineSummary))
            playerPendingTimelineSummary = "timeline=idle";
        if (string.IsNullOrEmpty(playerPendingPlaylistSummary))
            playerPendingPlaylistSummary = BuildPendingPlayerPlaylistSummary();
    }

    private string BuildPendingPlayerTargetSummary()
    {
        Atom hostedAtom;
        string hostAtomUid = "";
        if (TryResolveHostedPlayerAtom(out hostedAtom) && hostedAtom != null && !string.IsNullOrEmpty(hostedAtom.uid))
            hostAtomUid = hostedAtom.uid.Trim();

        return "target="
            + (string.IsNullOrEmpty(hostAtomUid) ? "screen_surface" : hostAtomUid)
            + " contract=single_display_fit mode="
            + FrameAngelPlayerMediaParity.DescribeAspectMode(PlayerSingleDisplayReleaseAspectMode);
    }

    private string BuildPendingPlayerMediaSummary()
    {
        string mediaName = TryGetPathLeafName(playerMediaPath);
        if (string.IsNullOrEmpty(mediaName))
            mediaName = "none";

        return "media="
            + mediaName
            + " playing=pending aspect="
            + FrameAngelPlayerMediaParity.DescribeAspectMode(PlayerSingleDisplayReleaseAspectMode)
            + " rt=0x0";
    }

    private string BuildPendingPlayerParitySummary()
    {
        return "parity=pending mode="
            + FrameAngelPlayerMediaParity.DescribeAspectMode(PlayerSingleDisplayReleaseAspectMode);
    }

    private string BuildPendingPlayerPlaylistSummary()
    {
        string directoryName = TryGetPathParentLeafName(playerMediaPath);
        if (string.IsNullOrEmpty(directoryName))
            return "playlist=idle loop=playlist random=on";

        return "playlist=selected dir=" + directoryName + " loop=playlist random=on";
    }

    private string TryGetPathParentLeafName(string rawPath)
    {
        string value = string.IsNullOrEmpty(rawPath) ? "" : rawPath.Trim();
        if (string.IsNullOrEmpty(value))
            return "";

        int slashIndex = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        if (slashIndex <= 0)
            return "";

        string parentPath = value.Substring(0, slashIndex);
        int parentSlashIndex = Math.Max(parentPath.LastIndexOf('/'), parentPath.LastIndexOf('\\'));
        if (parentSlashIndex < 0 || parentSlashIndex >= parentPath.Length - 1)
            return parentPath;

        return parentPath.Substring(parentSlashIndex + 1);
    }

    private string BuildStandalonePlayerParitySummary(string debugJson, string aspectMode)
    {
        if (string.IsNullOrEmpty(debugJson))
            return "parity=missing_debug mode=" + FrameAngelPlayerMediaParity.DescribeAspectMode(aspectMode);

        float contentAspect;
        float targetSurfaceAspect;
        Vector2 textureScale;
        Vector2 textureOffset;
        Vector3 targetLocalSize;
        Vector3 overlayLocalScale;
        Vector3 overlayLocalPosition;

        bool hasContentAspect = TryExtractJsonFloatField(debugJson, "contentAspect", out contentAspect);
        bool hasTargetSurfaceAspect = TryExtractJsonFloatField(debugJson, "targetSurfaceAspect", out targetSurfaceAspect);
        bool hasTextureScale = TryExtractJsonVector2Field(debugJson, "projectedTextureScale", out textureScale);
        bool hasTextureOffset = TryExtractJsonVector2Field(debugJson, "projectedTextureOffset", out textureOffset);
        bool hasTargetLocalSize = TryExtractJsonVector3Field(debugJson, "runtimeTargetLocalSize", out targetLocalSize);
        bool hasOverlayLocalScale = TryExtractJsonVector3Field(debugJson, "runtimeOverlayLocalScale", out overlayLocalScale);
        bool hasOverlayLocalPosition = TryExtractJsonVector3Field(debugJson, "runtimeOverlayLocalPosition", out overlayLocalPosition);

        if (!hasContentAspect
            && !hasTargetSurfaceAspect
            && !hasTextureScale
            && !hasTextureOffset
            && !hasTargetLocalSize
            && !hasOverlayLocalScale
            && !hasOverlayLocalPosition)
        {
            return "parity=missing_debug mode=" + FrameAngelPlayerMediaParity.DescribeAspectMode(aspectMode);
        }

        StringBuilder summary = new StringBuilder(220);
        summary.Append("mode=");
        summary.Append(FrameAngelPlayerMediaParity.DescribeAspectMode(aspectMode));
        if (hasContentAspect && hasTargetSurfaceAspect)
        {
            summary.Append(" note=");
            summary.Append(FrameAngelPlayerMediaParity.DescribeAspectOutcome(aspectMode, contentAspect, targetSurfaceAspect));
        }
        summary.Append(" ca=");
        summary.Append(hasContentAspect ? FormatCompactPlayerParityFloat(contentAspect) : "?");
        summary.Append(" sa=");
        summary.Append(hasTargetSurfaceAspect ? FormatCompactPlayerParityFloat(targetSurfaceAspect) : "?");
        summary.Append(" ts=");
        summary.Append(hasTextureScale ? FormatCompactPlayerParityVector2(textureScale) : "(?,?)");
        summary.Append(" to=");
        summary.Append(hasTextureOffset ? FormatCompactPlayerParityVector2(textureOffset) : "(?,?)");
        summary.Append(" tls=");
        summary.Append(hasTargetLocalSize ? FormatCompactPlayerParityVector3(targetLocalSize) : "(?,?,?)");
        summary.Append(" ols=");
        summary.Append(hasOverlayLocalScale ? FormatCompactPlayerParityVector3(overlayLocalScale) : "(?,?,?)");
        summary.Append(" olp=");
        summary.Append(hasOverlayLocalPosition ? FormatCompactPlayerParityVector3(overlayLocalPosition) : "(?,?,?)");
        return summary.ToString();
    }

    private bool TryExtractJsonVector2Field(string json, string key, out Vector2 value)
    {
        value = Vector2.zero;
        string objectJson;
        if (!TryExtractJsonObjectField(json, key, out objectJson) || string.IsNullOrEmpty(objectJson))
            return false;

        float x;
        float y;
        if (!TryExtractJsonFloatField(objectJson, "x", out x)
            || !TryExtractJsonFloatField(objectJson, "y", out y))
        {
            return false;
        }

        value = new Vector2(x, y);
        return true;
    }

    private bool TryExtractJsonVector3Field(string json, string key, out Vector3 value)
    {
        value = Vector3.zero;
        string objectJson;
        if (!TryExtractJsonObjectField(json, key, out objectJson) || string.IsNullOrEmpty(objectJson))
            return false;

        float x;
        float y;
        float z;
        if (!TryExtractJsonFloatField(objectJson, "x", out x)
            || !TryExtractJsonFloatField(objectJson, "y", out y)
            || !TryExtractJsonFloatField(objectJson, "z", out z))
        {
            return false;
        }

        value = new Vector3(x, y, z);
        return true;
    }

    private string FormatCompactPlayerParityFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private string FormatCompactPlayerParityVector2(Vector2 value)
    {
        return "("
            + FormatCompactPlayerParityFloat(value.x)
            + ","
            + FormatCompactPlayerParityFloat(value.y)
            + ")";
    }

    private string FormatCompactPlayerParityVector3(Vector3 value)
    {
        return "("
            + FormatCompactPlayerParityFloat(value.x)
            + ","
            + FormatCompactPlayerParityFloat(value.y)
            + ","
            + FormatCompactPlayerParityFloat(value.z)
            + ")";
    }

    private void AppendStandalonePlayerRecordJson(StringBuilder sb, StandalonePlayerRecord record)
    {
        if (sb == null)
            return;

        if (record == null)
        {
            sb.Append("{}");
            return;
        }

        float displayWidthMeters = 0f;
        float displayHeightMeters = 0f;
        FAInnerPiecePlaneData plane;
        string ignoredError;
        if (TryResolveInnerPieceScreenPlane(record.instanceId, record.slotId, out plane, out ignoredError))
        {
            displayWidthMeters = plane.widthMeters;
            displayHeightMeters = plane.heightMeters;
        }

        bool isPlaying = false;
        double currentTimeSeconds = 0d;
        double currentDurationSeconds = 0d;
        double currentTimeNormalized = 0d;
        if (!record.mediaIsStillImage)
        {
            try
            {
                if (record.videoPlayer != null)
                {
                    isPlaying = record.videoPlayer.isPlaying;
                    currentTimeSeconds = Math.Max(0d, record.videoPlayer.time);
                    currentDurationSeconds = GetStandalonePlayerDurationSeconds(record);
                }
            }
            catch
            {
                isPlaying = false;
            }
        }

        if (double.IsNaN(currentTimeSeconds) || double.IsInfinity(currentTimeSeconds))
            currentTimeSeconds = 0d;
        if (double.IsNaN(currentDurationSeconds) || double.IsInfinity(currentDurationSeconds))
            currentDurationSeconds = 0d;
        if (currentDurationSeconds > 0.0001d)
            currentTimeNormalized = Math.Max(0d, Math.Min(1d, currentTimeSeconds / currentDurationSeconds));
        if (double.IsNaN(currentTimeNormalized) || double.IsInfinity(currentTimeNormalized))
            currentTimeNormalized = 0d;

        sb.Append('{');
        sb.Append("\"playbackKey\":\"").Append(EscapeJsonString(record.playbackKey)).Append("\",");
        sb.Append("\"instanceId\":\"").Append(EscapeJsonString(record.instanceId)).Append("\",");
        sb.Append("\"slotId\":\"").Append(EscapeJsonString(record.slotId)).Append("\",");
        sb.Append("\"displayId\":\"").Append(EscapeJsonString(record.displayId)).Append("\",");
        sb.Append("\"mediaPath\":\"").Append(EscapeJsonString(record.mediaPath)).Append("\",");
        sb.Append("\"resolvedMediaPath\":\"").Append(EscapeJsonString(record.resolvedMediaPath)).Append("\",");
        sb.Append("\"currentIndex\":").Append(record.currentIndex).Append(',');
        sb.Append("\"playlistCount\":").Append(record.playlistPaths.Count).Append(',');
        sb.Append("\"playlistPaths\":");
        AppendStandalonePlayerStringArrayJson(sb, record.playlistPaths);
        sb.Append(',');
        sb.Append("\"currentPath\":\"").Append(EscapeJsonString(GetStandalonePlayerCurrentPlaylistPath(record))).Append("\",");
        sb.Append("\"mediaKind\":\"").Append(record.mediaIsStillImage ? "image" : "video").Append("\",");
        sb.Append("\"aspectMode\":\"").Append(EscapeJsonString(record.aspectMode)).Append("\",");
        sb.Append("\"loopMode\":\"").Append(EscapeJsonString(record.loopMode)).Append("\",");
        sb.Append("\"randomEnabled\":").Append(record.randomEnabled ? "true" : "false").Append(',');
        sb.Append("\"abLoopEnabled\":").Append(record.abLoopEnabled ? "true" : "false").Append(',');
        sb.Append("\"hasAbLoopStart\":").Append(record.hasAbLoopStart ? "true" : "false").Append(',');
        sb.Append("\"abLoopStartSeconds\":").Append(record.abLoopStartSeconds.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"hasAbLoopEnd\":").Append(record.hasAbLoopEnd ? "true" : "false").Append(',');
        sb.Append("\"abLoopEndSeconds\":").Append(record.abLoopEndSeconds.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"prepared\":").Append(record.prepared ? "true" : "false").Append(',');
        sb.Append("\"desiredPlaying\":").Append(record.desiredPlaying ? "true" : "false").Append(',');
        sb.Append("\"isPlaying\":").Append(isPlaying ? "true" : "false").Append(',');
        sb.Append("\"looping\":").Append(record.looping ? "true" : "false").Append(',');
        sb.Append("\"currentTimeSeconds\":").Append(currentTimeSeconds.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"currentDurationSeconds\":").Append(currentDurationSeconds.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"currentTimeNormalized\":").Append(currentTimeNormalized.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"durationKnown\":").Append(currentDurationSeconds > 0.0001d ? "true" : "false").Append(',');
        sb.Append("\"muted\":").Append(record.muted ? "true" : "false").Append(',');
        sb.Append("\"volume\":").Append(FormatFloat(record.volume)).Append(',');
        sb.Append("\"storedVolume\":").Append(FormatFloat(record.storedVolume)).Append(',');
        sb.Append("\"textureWidth\":").Append(record.textureWidth).Append(',');
        sb.Append("\"textureHeight\":").Append(record.textureHeight).Append(',');
        int renderTextureWidth = 0;
        int renderTextureHeight = 0;
        TryResolveTextureDimensions(record.renderTexture, out renderTextureWidth, out renderTextureHeight);
        sb.Append("\"renderTextureWidth\":").Append(renderTextureWidth).Append(',');
        sb.Append("\"renderTextureHeight\":").Append(renderTextureHeight).Append(',');
        sb.Append("\"displayWidthMeters\":").Append(FormatFloat(displayWidthMeters)).Append(',');
        sb.Append("\"displayHeightMeters\":").Append(FormatFloat(displayHeightMeters)).Append(',');
        sb.Append("\"targetDisplayWidthMeters\":").Append(FormatFloat(record.targetDisplayWidthMeters)).Append(',');
        sb.Append("\"targetDisplayHeightMeters\":").Append(FormatFloat(record.targetDisplayHeightMeters)).Append(',');
        sb.Append("\"resizeBehavior\":\"").Append(EscapeJsonString(record.resizeBehavior)).Append("\",");
        sb.Append("\"resizeAnchor\":\"").Append(EscapeJsonString(record.resizeAnchor)).Append("\",");
        sb.Append("\"resizeInFlight\":").Append(record.resizeInFlight ? "true" : "false").Append(',');
        sb.Append("\"resizeProgressNormalized\":").Append(FormatFloat(record.resizeProgressNormalized)).Append(',');
        sb.Append("\"lastError\":\"").Append(EscapeJsonString(record.lastError)).Append("\",");
        sb.Append("\"screenDebug\":").Append(record.binding != null && !string.IsNullOrEmpty(record.binding.debugJson) ? record.binding.debugJson : "{}");
        sb.Append('}');
    }

    private void AppendStandalonePlayerStringArrayJson(StringBuilder sb, List<string> values)
    {
        if (sb == null)
            return;

        sb.Append('[');
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('"').Append(EscapeJsonString(values[i] ?? "")).Append('"');
            }
        }
        sb.Append(']');
    }

    private string ResolveStandalonePlayerAbsolutePath(string path)
    {
        string normalized = NormalizeStandalonePlayerPath(path);
        if (string.IsNullOrEmpty(normalized))
            return "";

        if (normalized.Length > 1 && normalized[1] == ':')
            return normalized;
        if (normalized.StartsWith("\\\\", StringComparison.Ordinal))
            return normalized;
        if (normalized.StartsWith(".\\", StringComparison.Ordinal))
            normalized = normalized.Substring(2);

        string dataPath = NormalizeStandalonePlayerPath(Application.dataPath);
        int slash = dataPath.LastIndexOf('\\');
        if (slash <= 0)
            return normalized;

        string rootPath = dataPath.Substring(0, slash);
        return CombineStandalonePlayerPath(rootPath, normalized);
    }

    private string NormalizeStandalonePlayerPath(string path)
    {
        string normalized = (path ?? string.Empty).Trim().Replace('/', '\\');
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
            normalized = normalized.Substring(1, normalized.Length - 2);
        return normalized;
    }

    private string CombineStandalonePlayerPath(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return NormalizeStandalonePlayerPath(right);
        if (string.IsNullOrEmpty(right))
            return NormalizeStandalonePlayerPath(left);
        return NormalizeStandalonePlayerPath(left).TrimEnd('\\') + "\\" + NormalizeStandalonePlayerPath(right).TrimStart('\\');
    }

    private string SanitizeStandalonePlayerName(string value)
    {
        string safe = string.IsNullOrEmpty(value) ? "player" : value;
        safe = safe.Replace('\\', '_').Replace('/', '_').Replace(':', '_').Replace('*', '_').Replace('?', '_').Replace('"', '_').Replace('<', '_').Replace('>', '_').Replace('|', '_');
        return safe;
    }

    private int SafeClampVideoDimension(int value)
    {
        return value < 0 ? 0 : value;
    }

    private string BuildPlayerScreenBindCommandJson(
        InnerPieceInstanceRecord instance,
        InnerPieceScreenSlotRuntimeRecord slot,
        FAInnerPiecePlaneData plane,
        string surfaceTargetId)
    {
        bool keepLiveHostVisible = !UsesDisconnectSurfaceAsMediaTarget(instance, slot);
        string resolvedSurfaceTargetId = string.IsNullOrEmpty(surfaceTargetId)
            ? (slot != null && !string.IsNullOrEmpty(slot.surfaceTargetId) ? slot.surfaceTargetId : "player:screen")
            : surfaceTargetId;
        return "{"
            + "\"operation\":\"bind_screen\""
            + ",\"instanceId\":\"" + EscapeJsonString(instance != null ? instance.instanceId : "") + "\""
            + ",\"slotId\":\"" + EscapeJsonString(slot != null ? slot.slotId : "") + "\""
            + ",\"displayId\":\"" + EscapeJsonString(slot != null ? slot.displayId : "") + "\""
            + ",\"screenContractVersion\":\"" + EscapeJsonString(instance != null ? instance.screenContractVersion : "") + "\""
            + ",\"disconnectStateId\":\"" + EscapeJsonString(slot != null ? slot.disconnectStateId : "") + "\""
            + ",\"surfaceTargetId\":\"" + EscapeJsonString(resolvedSurfaceTargetId) + "\""
            + ",\"keepLiveHostVisible\":" + (keepLiveHostVisible ? "true" : "false")
            + ",\"centerX\":" + FormatFloat(plane != null ? plane.center.x : 0f)
            + ",\"centerY\":" + FormatFloat(plane != null ? plane.center.y : 0f)
            + ",\"centerZ\":" + FormatFloat(plane != null ? plane.center.z : 0f)
            + ",\"rightX\":" + FormatFloat(plane != null ? plane.right.x : 1f)
            + ",\"rightY\":" + FormatFloat(plane != null ? plane.right.y : 0f)
            + ",\"rightZ\":" + FormatFloat(plane != null ? plane.right.z : 0f)
            + ",\"upX\":" + FormatFloat(plane != null ? plane.up.x : 0f)
            + ",\"upY\":" + FormatFloat(plane != null ? plane.up.y : 1f)
            + ",\"upZ\":" + FormatFloat(plane != null ? plane.up.z : 0f)
            + ",\"forwardX\":" + FormatFloat(plane != null ? plane.forward.x : 0f)
            + ",\"forwardY\":" + FormatFloat(plane != null ? plane.forward.y : 0f)
            + ",\"forwardZ\":" + FormatFloat(plane != null ? plane.forward.z : 1f)
            + ",\"width\":" + FormatFloat(plane != null ? plane.widthMeters : 0.18f)
            + ",\"height\":" + FormatFloat(plane != null ? plane.heightMeters : 0.12f)
            + ",\"depth\":" + FormatFloat(plane != null ? plane.depthMeters : 0.01f)
            + "}";
    }

    private string BuildPlayerScreenClearCommandJson()
    {
        return "{\"operation\":\"clear_screen_binding\"}";
    }

}
