using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FrameAngel.Runtime.Shared;
using MVR.FileManagementSecure;
using UnityEngine;

public partial class FASyncRuntime : MVRScript
{
    private const float PrimitiveSurfaceDepth = 0.01f;
    private const float MarkerSphereDiameter = 0.06f;
    private const float RigStateRefreshIntervalSeconds = 1.00f;
    private const float MinPrimitiveScale = 0.001f;
    private const float MinPanelDistanceMeters = 0.15f;
    private const float DirectManipulationGrabRadiusMeters = 0.20f;
    private const float DefaultTweenDurationSeconds = 0.40f;
    private const float ReferenceGridDefaultCellMeters = 0.10f;
    private const float ReferenceGridDefaultAimDistanceMeters = 0.75f;
    private const float ReferenceGridPreviewSizeMeters = 0.24f;
    private const float ReferenceGridPreviewThicknessMeters = 0.004f;
    private const float ReferenceSnapPreviewDiameterMeters = 0.03f;
    private const float ReferenceSnapPreviewLiftMeters = 0.02f;
    private const float TableDefaultWidthMeters = 1.20f;
    private const float TableDefaultDepthMeters = 0.70f;
    private const float TableDefaultHeightMeters = 0.75f;
    private const float TableDefaultTopThicknessMeters = 0.05f;
    private const float TableDefaultLegThicknessMeters = 0.06f;
    private const float TableDefaultLegInsetMeters = 0.08f;
    private const string ReferenceStateSchemaVersion = "session_reference_state_v1";
    private const string RecipePreviewSchemaVersion = "session_recipe_preview_v1";
    private const string BuildCycleSchemaVersion = "vam_build_cycle_v1";
    private const string BuildCodeEventSchemaVersion = "vam_build_code_event_v1";
    private const string BuildTeachingRunType = "interactive_build_table_v1";
    private const string TableRecipeId = "table_simple_v1";
    private const float PlayerSeekReferenceNormalized = 0.4904f;
    private const float PlayerResizeDownMultiplier = 0.80f;
    private const float PlayerResizeUpMultiplier = 1.25f;
    private const float PlayerVolumeLowNormalized = 0.25f;
    private const float PlayerVolumeHighNormalized = 0.75f;
    private static readonly bool PlayerSingleDisplayRelease = true;
    private const string PlayerSingleDisplayReleaseAspectMode = GhostScreenAspectModeFit;
    private const string PlayerTestMediaMeasure16x9Path = "Custom\\Images\\_fa_player_aspect\\fa_measure_16x9_1920x1080.mp4";
    private const string PlayerTestMediaMeasure4x3Path = "Custom\\Images\\_fa_player_aspect\\fa_measure_4x3_1600x1200.mp4";
    private const string PlayerTestMediaMeasure9x16Path = "Custom\\Images\\_fa_player_aspect\\fa_measure_9x16_1080x1920.mp4";
    private const string PlayerTestMediaMeasure239x1Path = "Custom\\Images\\_fa_player_aspect\\fa_measure_2.39x1_2390x1000.mp4";
    private static readonly string[] PlayerMediaBrowserSeedPathCandidates = new string[]
    {
        "Custom\\Images",
        "Custom\\Images\\_fa_player_aspect",
        "Custom\\Images\\02_vid",
        "Custom\\Images\\01_img",
        "Custom\\Images\\_fillm",
    };
    private static readonly Color ActiveObjectTint = Color.cyan;
    private static readonly Color ReferenceGridColor = new Color(0.18f, 0.72f, 1.0f, 0.35f);
    private static readonly Color ReferenceSnapColor = new Color(1.0f, 0.86f, 0.22f, 0.95f);
    private static readonly Color PreviewTopColor = new Color(0.46f, 0.82f, 1.0f, 0.45f);
    private static readonly Color PreviewLegColor = new Color(0.96f, 0.96f, 0.96f, 0.40f);
    private static readonly Color FinalTopColor = new Color(0.80f, 0.84f, 0.88f, 1.0f);
    private static readonly Color FinalLegColor = new Color(0.90f, 0.90f, 0.92f, 1.0f);

    private sealed class SyncObjectRecord
    {
        public string objectId = "";
        public string kind = "";
        public string resourceType = "";
        public GameObject gameObject;
        public Rigidbody rigidbody;
        public Coroutine tweenCoroutine;
        public bool internalMarker = false;
        public string status = "active";
        public bool visible = true;
        public string materialMode = "opaque";
        public string tagsCsv = "";
        public string parentGroupId = "";
        public string motionType = "";
        public string motionOperationId = "";
        public string motionStatus = "idle";
        public string motionStartedAtUtc = "";
        public float motionStartedAtUnscaledTime = 0f;
        public float motionDurationSeconds = 0f;
        public string motionFinishedAtUtc = "";
        public Vector3 position = Vector3.zero;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public Color color = Color.white;
    }

    private sealed class SyncGroupRecord
    {
        public string groupId = "";
        public string status = "active";
        public string tagsCsv = "";
        public string updatedAtUtc = "";
        public readonly HashSet<string> memberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // The clean-room release build strips the registration/UI path for these
    // test surfaces, but we keep the declarations so the debug/test build can
    // turn them back on with FRAMEANGEL_TEST_SURFACES without a second code
    // fork. Suppress the dormant-field warnings only in non-test builds.
#if !FRAMEANGEL_TEST_SURFACES
#pragma warning disable 649, 169
#endif
    private JSONStorableString buildVersionField;
    private JSONStorableString playerRuntimeTargetField;
    private JSONStorableString playerRuntimeMediaField;
    private JSONStorableString playerRuntimeStateField;
    private JSONStorableString playerMediaPathField;
    private JSONStorableString playerRuntimeParityField;
    private JSONStorableString playerRuntimeAspectModeField;
    private JSONStorableString playerRuntimeTimelineField;
    private JSONStorableString playerRuntimePlaylistField;
    private JSONStorableString playerInputStateField;
    private JSONStorableBool playerFocusActiveField;
    private JSONStorableString playerVisibleScreenField;
    private JSONStorableFloat playerScrubNormalizedField;
    private JSONStorableFloat playerVolumeNormalizedField;
    private JSONStorableAction playerAspectFitAction;
    private JSONStorableAction playerAspectFullWidthAction;
    private JSONStorableAction playerAspectCropAction;
    private JSONStorableAction playerAspectCycleAction;
    private JSONStorableAction playerLoadMediaAction;
    private JSONStorableAction playerPlayPauseAction;
    private JSONStorableAction playerPreviousAction;
    private JSONStorableAction playerNextAction;
    private JSONStorableAction playerSeekStartAction;
    private JSONStorableAction playerSeekReferenceAction;
    private JSONStorableAction playerSkipBackwardAction;
    private JSONStorableAction playerSkipForwardAction;
    private JSONStorableAction playerVolumeLowAction;
    private JSONStorableAction playerVolumeHighAction;
    private JSONStorableAction playerLoopOffAction;
    private JSONStorableAction playerLoopSingleAction;
    private JSONStorableAction playerLoopPlaylistAction;
    private JSONStorableAction playerRandomToggleAction;
    private JSONStorableAction playerRandomOffAction;
    private JSONStorableAction playerRandomOnAction;
    private JSONStorableAction playerAbLoopStartAction;
    private JSONStorableAction playerAbLoopEndAction;
    private JSONStorableAction playerAbLoopEnableAction;
    private JSONStorableAction playerAbLoopDisableAction;
    private JSONStorableAction playerAbLoopClearAction;
    private JSONStorableAction playerResizeDownAction;
    private JSONStorableAction playerResizeUpAction;
    private JSONStorableString syncLastErrorField;
    private JSONStorableString syncBrokerActionIdField;
    private JSONStorableString syncBrokerArgsJsonField;
    private JSONStorableString syncBrokerResultJsonField;
    private JSONStorableAction syncBrokerExecuteAction;
#if !FRAMEANGEL_CUA_PLAYER
    private JSONStorableString syncEventOutboxPathField;
#endif
#if !FRAMEANGEL_TEST_SURFACES
#pragma warning restore 649, 169
#endif
    private string syncBrokerActionId = "";
    private string syncBrokerArgsJson = "{}";
    private string syncBrokerResultJson = "";
#if !FRAMEANGEL_CUA_PLAYER
    private string syncEventOutboxPath = "";
#endif
    private string playerMediaPath = "";
    private string playerPendingTargetSummary = "";
    private string playerPendingMediaSummary = "";
    private string playerPendingStateSummary = "";
    private string playerPendingParitySummary = "";
    private string playerPendingTimelineSummary = "";
    private string playerPendingPlaylistSummary = "";
    private float lastPlayerScrubNormalizedValue = -1f;
    private float lastPlayerVolumeNormalizedValue = -1f;
    private string pendingPlayerMediaBrowserSuccessStatus = "";
    private bool pendingPlayerMediaBrowserTargetsMetaProof = false;
    private bool playerMediaBrowserOpen = false;
    private bool suppressStandalonePlayerSliderCallbacks = false;
    private float standalonePlayerScrubFieldSyncHoldoffUntil = 0f;
    private bool queuedAttachedPlayerSeekNormalized = false;
    private float queuedAttachedPlayerSeekNormalizedValue = 0f;
    private float queuedAttachedPlayerSeekNormalizedApplyAt = 0f;
    private string queuedAttachedPlayerSeekNormalizedStatus = "";
    private static readonly string[] PlayerRuntimeMediaExtensions = new[]
    {
        ".mp4",
        ".m4v",
        ".mov",
        ".webm",
        ".avi",
        ".mpg",
        ".mpeg",
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".tga",
        ".gif"
    };
    private string activeObjectId = "";
    private string activeGroupId = "";
    private string activeCorrelationId = "";
    private string activeMessageId = "";
    private float nextRigStateRefreshTime = 0f;
#if !FRAMEANGEL_CUA_PLAYER
    private int syncEventSequence = 0;
#endif
    private GameObject runtimeRoot;
    private readonly Dictionary<string, SyncObjectRecord> syncObjects =
        new Dictionary<string, SyncObjectRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SyncGroupRecord> syncGroups =
        new Dictionary<string, SyncGroupRecord>(StringComparer.OrdinalIgnoreCase);

    partial void OnSceneRuntimeDestroy();
    partial void InitializeSceneRuntimeDefaults();
    partial void AppendSceneOnlyStatePayload(StringBuilder sb);

    public override void Init()
    {
        InitializeSceneRuntimeDefaults();

        EnsureRuntimeRoot();
        BuildStorables();
        RegisterStorables();
        BuildUi();
        RefreshRigStateSnapshot();
        RefreshVisiblePlayerDebugFields();
        string readyPayload = BuildRuntimeReadyPayload();
        SetLastReceipt(BuildBrokerResult(true, "sync runtime ready", readyPayload));
        EmitRuntimeEvent(
            "plugin_ready",
            "Plugin.Ready",
            "ok",
            "",
            "sync_runtime_ready",
            "",
            "",
            "",
            "",
            readyPayload
        );

        if (ShouldAutoStartHostedPlayerOnContainingAtom())
            QueueHostedPlayerAutoStart();
#if FRAMEANGEL_CUA_PLAYER
        else
        {
            QueueHostedPlayerAutoStart();
            Atom hostAtom;
            if (TryResolveHostedPlayerAtom(out hostAtom) && hostAtom != null && !HasHostedPlayerAuthoredScreenContract(hostAtom))
                SetLastError("fa_cua_player requires an authored host object with screen_surface");
        }
#endif

#if FRAMEANGEL_TEST_SURFACES
        Atom attachedHostAtom;
        if (!ShouldAutoStartHostedPlayerOnContainingAtom()
            && TryResolveAttachedMetaProofHostAtom(out attachedHostAtom))
        {
            syncMetaProofAutoStartPending = true;
            StartCoroutine(RunAttachedHostPlayerQuickDemoCoroutine());
        }
#endif
    }

    private void Update()
    {
        EnsureRuntimeRoot();
        if (Time.unscaledTime >= nextRigStateRefreshTime)
        {
            nextRigStateRefreshTime = Time.unscaledTime + RigStateRefreshIntervalSeconds;
            RefreshRigStateSnapshot();
            RefreshVisiblePlayerDebugFields();
        }

#if !FRAMEANGEL_CUA_PLAYER
        TickReferencePreview();
#endif
        // Spawned helper atoms (the canonical Empty anchor or optional GrabPoint helper)
        // can appear a frame later in VaM, so their follow binding has to resolve
        // before the normal follow tick.
        TickPendingInnerPieceAnchorSpawns();
        TickInnerPieceFollowBindings();
        TickPlayerControlSurfaceRelativeBindings();
        TickQueuedAttachedPlayerSeekNormalizedAction();
        TickStandalonePlayerRuntime();
#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
        TickCuaPlayerInput();
#endif

#if !FRAMEANGEL_CUA_PLAYER
        if (syncDevMode && IsVrRuntimeActive())
            TickVrInputs();
#endif
    }

    private void OnDestroy()
    {
        SetInputCaptureState(false);
#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
        OnCuaPlayerInputDestroy();
#endif
        OnPlayerPresetDestroy();
#if !FRAMEANGEL_CUA_PLAYER
        ClearReferencePreviewVisuals();
#endif
        OnSceneRuntimeDestroy();
        if (hostedPlayerAutoStartCoroutine != null)
            StopCoroutine(hostedPlayerAutoStartCoroutine);
        ShutdownStandalonePlayerRuntime();
        ShutdownInnerPieceRuntime();
        if (runtimeRoot != null)
            Destroy(runtimeRoot);
    }

    private void BuildStorables()
    {
        buildVersionField = new JSONStorableString("FrameAngel Player Version", BuildRuntimeInfo.BuildVersion);
        ConfigureTransientField(buildVersionField, false);

        playerRuntimeTargetField = new JSONStorableString("FrameAngel Player Target", "target=unresolved");
        ConfigureTransientField(playerRuntimeTargetField, false);

        playerRuntimeMediaField = new JSONStorableString("FrameAngel Player Media", "media=idle");
        ConfigureTransientField(playerRuntimeMediaField, false);

        playerRuntimeStateField = new JSONStorableString("FrameAngel Player State", "state=idle");
        ConfigureTransientField(playerRuntimeStateField, false);

        playerMediaPathField = new JSONStorableString("Player Media Path", playerMediaPath);
        playerMediaPathField.setCallbackFunction = delegate(string v)
        {
            string normalized = string.IsNullOrEmpty(v) ? "" : v.Trim();
            SetPendingPlayerSelection(normalized);
            SetPendingPlayerStateSummary(string.IsNullOrEmpty(normalized)
                ? "state=idle"
                : "state=selection_pending");
            RefreshVisiblePlayerDebugFields();
        };
        ConfigureTransientField(playerMediaPathField, false);

        playerRuntimeParityField = new JSONStorableString("FrameAngel Player Parity", "parity=unbound");
        ConfigureTransientField(playerRuntimeParityField, false);

        playerRuntimeAspectModeField = new JSONStorableString("FrameAngel Player Aspect Mode", "aspect=unknown");
        ConfigureTransientField(playerRuntimeAspectModeField, false);

        playerRuntimeTimelineField = new JSONStorableString("FrameAngel Player Timeline", "timeline=idle");
        ConfigureTransientField(playerRuntimeTimelineField, false);

        playerRuntimePlaylistField = new JSONStorableString("FrameAngel Player Playlist", "playlist=idle");
        ConfigureTransientField(playerRuntimePlaylistField, false);

        BuildPlayerPresetStorables();

#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
        BuildCuaPlayerInputStorables();
#endif

        playerScrubNormalizedField = new JSONStorableFloat(
            "scrub_normalized",
            0f,
            delegate(float value)
            {
                if (suppressStandalonePlayerSliderCallbacks)
                    return;

                ArmStandalonePlayerScrubFieldSyncHoldoff();
                QueueAttachedPlayerSeekNormalizedAction(value, "Player scrub set");
            },
            0f,
            1f,
            true);
        ConfigureTransientField(playerScrubNormalizedField, false);

        playerVolumeNormalizedField = new JSONStorableFloat(
            "volume_normalized",
            1f,
            delegate(float value)
            {
                if (suppressStandalonePlayerSliderCallbacks)
                    return;

                RunAttachedPlayerSetVolumeAction(value, "Player volume set");
            },
            0f,
            1f,
            true);
        ConfigureTransientField(playerVolumeNormalizedField, false);

        playerAspectFitAction = new JSONStorableAction(
            "Player Aspect Fit",
            delegate
            {
                RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFit, "Player aspect set to fit");
            });
        playerAspectFullWidthAction = new JSONStorableAction(
            "Player Aspect Full Width",
            delegate
            {
                RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFullWidth, "Player aspect set to full_width");
            });
        playerAspectCropAction = new JSONStorableAction(
            "Player Aspect Crop",
            delegate
            {
                RunAttachedPlayerAspectModeAction(GhostScreenAspectModeCrop, "Player aspect set to crop");
            });
        playerAspectCycleAction = new JSONStorableAction(
            "Player Aspect Cycle",
            delegate
            {
                RunAttachedPlayerAspectCycleAction();
            });
        playerLoadMediaAction = new JSONStorableAction(
            "Player Load Media",
            delegate
            {
                RunPlayerLoadMedia();
            });
        playerPlayPauseAction = new JSONStorableAction(
            "Player Play Pause",
            delegate
            {
                RunAttachedPlayerPlayPauseAction();
            });
        playerPreviousAction = new JSONStorableAction(
            "Player Previous",
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionPreviousId, "", "Player moved to previous item");
            });
        playerNextAction = new JSONStorableAction(
            "Player Next",
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionNextId, "", "Player moved to next item");
            });
        playerSeekStartAction = new JSONStorableAction(
            "Player Seek Start",
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSeekNormalizedId,
                    "\"normalized\":0",
                    "Player seeked to start");
            });
        playerSeekReferenceAction = new JSONStorableAction(
            "Player Seek Reference",
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSeekNormalizedId,
                    "\"normalized\":" + FormatFloat(PlayerSeekReferenceNormalized),
                    "Player seeked to reference frame");
            });
        playerSkipBackwardAction = new JSONStorableAction(
            "Player Skip Backward",
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionSkipBackwardId, "", "Player skipped backward");
            });
        playerSkipForwardAction = new JSONStorableAction(
            "Player Skip Forward",
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionSkipForwardId, "", "Player skipped forward");
            });
        playerVolumeLowAction = new JSONStorableAction(
            "Player Volume 25",
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(PlayerVolumeLowNormalized),
                    "Player volume set to 25%");
            });
        playerVolumeHighAction = new JSONStorableAction(
            "Player Volume 75",
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(PlayerVolumeHighNormalized),
                    "Player volume set to 75%");
            });
        playerLoopOffAction = new JSONStorableAction(
            "Player Loop Off",
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModeNone, "Player loop set to none");
            });
        playerLoopSingleAction = new JSONStorableAction(
            "Player Loop Single",
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModeSingle, "Player loop set to single");
            });
        playerLoopPlaylistAction = new JSONStorableAction(
            "Player Loop Playlist",
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModePlaylist, "Player loop set to playlist");
            });
        playerRandomToggleAction = new JSONStorableAction(
            "Player Random Toggle",
            delegate
            {
                RunAttachedPlayerToggleRandomAction("Player random toggled");
            });
        playerRandomOffAction = new JSONStorableAction(
            "Player Random Off",
            delegate
            {
                RunAttachedPlayerSetRandomAction(false, "Player random disabled");
            });
        playerRandomOnAction = new JSONStorableAction(
            "Player Random On",
            delegate
            {
                RunAttachedPlayerSetRandomAction(true, "Player random enabled");
            });
        playerAbLoopStartAction = new JSONStorableAction(
            "Player A-B Set Start",
            delegate
            {
                RunAttachedPlayerSetAbLoopStartAction("Player A-B start set");
            });
        playerAbLoopEndAction = new JSONStorableAction(
            "Player A-B Set End",
            delegate
            {
                RunAttachedPlayerSetAbLoopEndAction("Player A-B end set");
            });
        playerAbLoopEnableAction = new JSONStorableAction(
            "Player A-B Enable",
            delegate
            {
                RunAttachedPlayerSetAbLoopEnabledAction(true, "Player A-B loop enabled");
            });
        playerAbLoopDisableAction = new JSONStorableAction(
            "Player A-B Disable",
            delegate
            {
                RunAttachedPlayerSetAbLoopEnabledAction(false, "Player A-B loop disabled");
            });
        playerAbLoopClearAction = new JSONStorableAction(
            "Player A-B Clear",
            delegate
            {
                RunAttachedPlayerClearAbLoopAction("Player A-B loop cleared");
            });
        playerResizeDownAction = new JSONStorableAction(
            "Player Resize Down",
            delegate
            {
                RunAttachedPlayerResizeAction(PlayerResizeDownMultiplier, "Player resized down");
            });
        playerResizeUpAction = new JSONStorableAction(
            "Player Resize Up",
            delegate
            {
                RunAttachedPlayerResizeAction(PlayerResizeUpMultiplier, "Player resized up");
            });
#if FRAMEANGEL_TEST_SURFACES
        syncDevModeToggle = new JSONStorableBool("Sync Dev Mode", syncDevMode);
        syncDevModeToggle.setCallbackFunction = delegate(bool v)
        {
            syncDevMode = v;
            if (!syncDevMode)
                SetInputCaptureState(false);
            EmitRuntimeEvent(
                "input_capture_changed",
                "Session.Sync.SetDevMode",
                "ok",
                "",
                v ? "dev_mode_on" : "dev_mode_off",
                "",
                "",
                "",
                ""
            );
        };
        ConfigureTransientField(syncDevModeToggle, false);

        syncInputCaptureToggle = new JSONStorableBool("Sync Input Capture", syncInputCapture);
        syncInputCaptureToggle.setCallbackFunction = delegate(bool v)
        {
            SetInputCaptureState(v);
        };
        ConfigureTransientField(syncInputCaptureToggle, false);

        syncLastRigStateField = new JSONStorableString("Sync Last Rig State", "");
        ConfigureTransientField(syncLastRigStateField, false);
#if !FRAMEANGEL_CUA_PLAYER
        BuildSceneOnlyReferenceStorables();
#endif
        syncLastReceiptField = new JSONStorableString("Sync Last Receipt", "");
        ConfigureTransientField(syncLastReceiptField, false);
#endif
        syncLastErrorField = new JSONStorableString("Sync Last Error", "");
        ConfigureTransientField(syncLastErrorField, false);

        syncBrokerActionIdField = new JSONStorableString("Sync Broker Action Id", "");
        syncBrokerActionIdField.setCallbackFunction = delegate(string v)
        {
            syncBrokerActionId = string.IsNullOrEmpty(v) ? "" : v.Trim();
        };
        ConfigureTransientField(syncBrokerActionIdField, true);

        syncBrokerArgsJsonField = new JSONStorableString("Sync Broker Args Json", "{}");
        syncBrokerArgsJsonField.setCallbackFunction = delegate(string v)
        {
            syncBrokerArgsJson = string.IsNullOrEmpty(v) ? "{}" : v.Trim();
        };
        ConfigureTransientField(syncBrokerArgsJsonField, true);

        syncBrokerResultJsonField = new JSONStorableString("Sync Broker Result Json", "{}");
        ConfigureTransientField(syncBrokerResultJsonField, true);

        syncBrokerExecuteAction = new JSONStorableAction(
            "Sync Broker Execute",
            delegate
            {
                ExecuteBrokerAction();
            }
        );

#if FRAMEANGEL_TEST_SURFACES
#if !FRAMEANGEL_CUA_PLAYER
        syncEventOutboxPathField = new JSONStorableString("Sync Event Outbox Path", syncEventOutboxPath);
        syncEventOutboxPathField.setCallbackFunction = delegate(string v)
        {
            syncEventOutboxPath = string.IsNullOrEmpty(v) ? SessionBridgeJsonl.GetDefaultEventOutboxPath() : v.Trim();
        };
        ConfigureTransientField(syncEventOutboxPathField, false);
#endif
        BuildMetaProofStorables();
#endif
    }

    private void RegisterStorables()
    {
        RegisterString(buildVersionField);
        RegisterString(playerRuntimeTargetField);
        RegisterString(playerRuntimeMediaField);
        RegisterString(playerRuntimeStateField);
        RegisterString(playerMediaPathField);
        RegisterString(playerRuntimeParityField);
        RegisterString(playerRuntimeAspectModeField);
        RegisterString(playerRuntimeTimelineField);
        RegisterString(playerRuntimePlaylistField);
        RegisterPlayerPresetStorables();
#if FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_FEATURE_PLAYER_INPUT
        RegisterCuaPlayerInputStorables();
#endif
        RegisterFloat(playerScrubNormalizedField);
        RegisterFloat(playerVolumeNormalizedField);
        if (ShouldExposePlayerAspectControls())
        {
            RegisterAction(playerAspectFitAction);
            RegisterAction(playerAspectFullWidthAction);
            RegisterAction(playerAspectCropAction);
            RegisterAction(playerAspectCycleAction);
        }
        RegisterAction(playerLoadMediaAction);
        RegisterAction(playerPlayPauseAction);
        RegisterAction(playerPreviousAction);
        RegisterAction(playerNextAction);
        RegisterAction(playerSeekStartAction);
        RegisterAction(playerSeekReferenceAction);
        RegisterAction(playerSkipBackwardAction);
        RegisterAction(playerSkipForwardAction);
        RegisterAction(playerVolumeLowAction);
        RegisterAction(playerVolumeHighAction);
        RegisterAction(playerLoopOffAction);
        RegisterAction(playerLoopSingleAction);
        RegisterAction(playerLoopPlaylistAction);
        RegisterAction(playerRandomToggleAction);
        RegisterAction(playerRandomOffAction);
        RegisterAction(playerRandomOnAction);
        RegisterAction(playerAbLoopStartAction);
        RegisterAction(playerAbLoopEndAction);
        RegisterAction(playerAbLoopEnableAction);
        RegisterAction(playerAbLoopDisableAction);
        RegisterAction(playerAbLoopClearAction);
        RegisterAction(playerResizeDownAction);
        RegisterAction(playerResizeUpAction);
#if FRAMEANGEL_TEST_SURFACES
        RegisterBool(syncDevModeToggle);
        RegisterBool(syncInputCaptureToggle);
        RegisterString(syncLastRigStateField);
#if !FRAMEANGEL_CUA_PLAYER
        RegisterSceneOnlyReferenceStorables();
#endif
        RegisterString(syncLastReceiptField);
#endif
        RegisterString(syncLastErrorField);
        RegisterString(syncBrokerActionIdField);
        RegisterString(syncBrokerArgsJsonField);
        RegisterString(syncBrokerResultJsonField);
#if FRAMEANGEL_TEST_SURFACES
#if !FRAMEANGEL_CUA_PLAYER
        RegisterString(syncEventOutboxPathField);
#endif
#endif
        RegisterAction(syncBrokerExecuteAction);
#if FRAMEANGEL_TEST_SURFACES
        RegisterMetaProofStorables();
#endif
    }

    private void BuildUi()
    {
        BuildPlayerPresetUi();
        CreateTextField(playerMediaPathField, true);
        CreateSlider(playerScrubNormalizedField, false);
        CreateSlider(playerVolumeNormalizedField, false);
        if (ShouldExposePlayerAspectControls())
        {
            CreateButton("Player Aspect Fit").button.onClick.AddListener(
                delegate
                {
                    RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFit, "Player aspect set to fit");
                }
            );
            CreateButton("Player Aspect Full Width").button.onClick.AddListener(
                delegate
                {
                    RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFullWidth, "Player aspect set to full_width");
                }
            );
            CreateButton("Player Aspect Crop").button.onClick.AddListener(
                delegate
                {
                    RunAttachedPlayerAspectModeAction(GhostScreenAspectModeCrop, "Player aspect set to crop");
                }
            );
            CreateButton("Player Aspect Cycle").button.onClick.AddListener(
                delegate
                {
                    RunAttachedPlayerAspectCycleAction();
                }
            );
        }
        CreateButton("Player Load Media").button.onClick.AddListener(
            delegate
            {
                RunPlayerLoadMedia();
            }
        );
        CreateButton("Player Previous").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionPreviousId, "", "Player moved to previous item");
            }
        );
        CreateButton("Player Play Pause").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerPlayPauseAction();
            }
        );
        CreateButton("Player Next").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionNextId, "", "Player moved to next item");
            }
        );
        CreateButton("Player Seek Start").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSeekNormalizedId,
                    "\"normalized\":0",
                    "Player seeked to start");
            }
        );
        CreateButton("Player Seek Reference").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSeekNormalizedId,
                    "\"normalized\":" + FormatFloat(PlayerSeekReferenceNormalized),
                    "Player seeked to reference frame");
            }
        );
        CreateButton("Player Skip Backward").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionSkipBackwardId, "", "Player skipped backward");
            }
        );
        CreateButton("Player Skip Forward").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(PlayerActionSkipForwardId, "", "Player skipped forward");
            }
        );
        CreateButton("Player Volume 25").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(PlayerVolumeLowNormalized),
                    "Player volume set to 25%");
            }
        );
        CreateButton("Player Volume 75").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerDirectAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(PlayerVolumeHighNormalized),
                    "Player volume set to 75%");
            }
        );
        CreateButton("Player Loop Off").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModeNone, "Player loop set to none");
            }
        );
        CreateButton("Player Loop Single").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModeSingle, "Player loop set to single");
            }
        );
        CreateButton("Player Loop Playlist").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetLoopModeAction(PlayerLoopModePlaylist, "Player loop set to playlist");
            }
        );
        CreateButton("Player Random Off").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetRandomAction(false, "Player random disabled");
            }
        );
        CreateButton("Player Random On").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetRandomAction(true, "Player random enabled");
            }
        );
        CreateButton("Player A-B Set Start").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetAbLoopStartAction("Player A-B start set");
            }
        );
        CreateButton("Player A-B Set End").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetAbLoopEndAction("Player A-B end set");
            }
        );
        CreateButton("Player A-B Enable").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerSetAbLoopEnabledAction(true, "Player A-B loop enabled");
            }
        );
        CreateButton("Player A-B Clear").button.onClick.AddListener(
            delegate
            {
                RunAttachedPlayerClearAbLoopAction("Player A-B loop cleared");
            }
        );
#if FRAMEANGEL_TEST_SURFACES
        CreateToggle(syncDevModeToggle);
        CreateToggle(syncInputCaptureToggle);
        BuildMetaProofUi();
        CreateTextField(syncLastRigStateField, false);
#if !FRAMEANGEL_CUA_PLAYER
        BuildSceneOnlyReferenceUi();
#endif
        CreateTextField(syncLastReceiptField, false);
        CreateTextField(syncLastErrorField, false);
#if !FRAMEANGEL_CUA_PLAYER
        CreateTextField(syncEventOutboxPathField, true);
#endif
#endif
    }

    // These values must stay live for runtime testing and broker transport, but
    // they should not be serialized into scene saves. The export JSON already
    // proved that saveable storables leak implementation details if we leave the
    // defaults alone, so the clean-room runtime treats them as transient.
    private static void ConfigureTransientField(JSONStorableParam field, bool hidden)
    {
        if (field == null)
            return;
        field.isStorable = false;
        field.isRestorable = false;
        field.hidden = hidden;
    }

    private void ExecuteBrokerAction()
    {
        string actionId = string.IsNullOrEmpty(syncBrokerActionId) ? "" : syncBrokerActionId.Trim();
        string argsJson = string.IsNullOrEmpty(syncBrokerArgsJson) ? "{}" : syncBrokerArgsJson;
        string resultJson = "{}";
        string errorMessage = "";

        try
        {
            if (string.IsNullOrEmpty(actionId))
            {
                errorMessage = "sync broker actionId missing";
                SetLastError(errorMessage);
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                SetLastReceipt(resultJson);
                syncBrokerResultJsonField.val = resultJson;
                return;
            }

            bool ok = TryExecuteAction(actionId, argsJson, out resultJson, out errorMessage);
            if (!ok)
                SetLastError(errorMessage);
            else
                SetLastError("");

            SetLastReceipt(resultJson);
            syncBrokerResultJsonField.val = resultJson;
        }
        catch (Exception ex)
        {
            errorMessage = "sync broker action " + actionId + " exception: " + ex.Message;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            SetLastError(errorMessage);
            SetLastReceipt(resultJson);
            if (syncBrokerResultJsonField != null)
                syncBrokerResultJsonField.val = resultJson;
        }
    }

    private void RunAttachedPlayerAspectModeAction(string aspectMode, string successStatus)
    {
        string coercedAspectMode = CoercePlayerReleaseAspectMode(aspectMode);
        RunAttachedPlayerDirectAction(
            PlayerActionSetAspectModeId,
            "\"aspectMode\":\"" + EscapeJsonString(coercedAspectMode ?? "") + "\"",
            string.IsNullOrEmpty(successStatus)
                ? "Player aspect set to " + FrameAngelPlayerMediaParity.DescribeAspectMode(coercedAspectMode)
                : successStatus);
    }

    private void RunAttachedPlayerAspectCycleAction()
    {
        if (!ShouldExposePlayerAspectControls())
        {
            UpdateAttachedPlayerAspectModeField(PlayerSingleDisplayReleaseAspectMode);
            return;
        }

        string selectorJson;
        string errorMessage;
        if (!TryBuildAttachedPlayerSelectorJson(out selectorJson, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdateAttachedPlayerAspectModeField("");
            return;
        }

        string currentMode = ResolveAttachedPlayerCurrentAspectMode();
        string nextMode = ResolveNextPlayerAspectModeCycle(currentMode);
        RunAttachedPlayerAspectModeAction(nextMode, "Player aspect cycled to " + nextMode);
    }

    private bool TryBuildAttachedPlayerSelectorJson(out string selectorJson, out string errorMessage)
    {
        selectorJson = "{}";
        errorMessage = "";

        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
        {
            errorMessage = "attached player host atom not resolved";
            return false;
        }

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "attached player host atom uid missing";
            return false;
        }

        StandalonePlayerRecord ignoredRecord;
        HostedPlayerSurfaceContract ignoredContract;
        if (!TryResolveOrCreateHostedStandalonePlayerRecordForWrite(
                hostAtomUid,
                ResolveAttachedPlayerCurrentAspectMode(),
                out ignoredRecord,
                out ignoredContract,
                out errorMessage))
        {
            return false;
        }

        selectorJson = "{"
            + "\"playbackKey\":\"" + EscapeJsonString(BuildHostedPlayerPlaybackKey(hostAtomUid)) + "\""
            + "}";
        return true;
    }

    private string BuildAttachedPlayerActionArgsJson(string selectorJson, string extraArgsBody)
    {
        string trimmedSelector = string.IsNullOrEmpty(selectorJson) ? "{}" : selectorJson.Trim();
        string trimmedExtra = string.IsNullOrEmpty(extraArgsBody) ? "" : extraArgsBody.Trim();
        if (string.IsNullOrEmpty(trimmedExtra))
            return trimmedSelector;

        return trimmedSelector.Substring(0, trimmedSelector.Length - 1)
            + ","
            + trimmedExtra
            + "}";
    }

    private string ResolveAttachedPlayerCurrentAspectMode()
    {
        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            return CoercePlayerReleaseAspectMode(GhostScreenAspectModeFit);

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (string.IsNullOrEmpty(hostAtomUid))
            return CoercePlayerReleaseAspectMode(GhostScreenAspectModeFit);

        StandalonePlayerRecord record;
        if (!standalonePlayerRecords.TryGetValue(BuildHostedPlayerPlaybackKey(hostAtomUid), out record) || record == null)
            return CoercePlayerReleaseAspectMode(GhostScreenAspectModeFit);

        return CoercePlayerReleaseAspectMode(
            ResolveStandalonePlayerAspectMode("{\"aspectMode\":\"" + EscapeJsonString(record.aspectMode ?? "") + "\"}", GhostScreenAspectModeFit));
    }

    private string ResolveNextPlayerAspectModeCycle(string currentMode)
    {
        if (!ShouldExposePlayerAspectControls())
            return PlayerSingleDisplayReleaseAspectMode;

        currentMode = ResolveStandalonePlayerAspectMode("{\"aspectMode\":\"" + EscapeJsonString(currentMode ?? "") + "\"}", GhostScreenAspectModeFit);
        if (string.Equals(currentMode, GhostScreenAspectModeFullWidth, StringComparison.OrdinalIgnoreCase))
            return GhostScreenAspectModeFit;
        if (string.Equals(currentMode, GhostScreenAspectModeFit, StringComparison.OrdinalIgnoreCase))
            return GhostScreenAspectModeCrop;
        return GhostScreenAspectModeFullWidth;
    }

    private void UpdateAttachedPlayerAspectModeField(string reportedAspectMode)
    {
        string aspectMode = string.IsNullOrEmpty(reportedAspectMode)
            ? ResolveAttachedPlayerCurrentAspectMode()
            : ResolveStandalonePlayerAspectMode("{\"aspectMode\":\"" + EscapeJsonString(reportedAspectMode) + "\"}", GhostScreenAspectModeFit);
        aspectMode = CoercePlayerReleaseAspectMode(aspectMode);

        if (playerRuntimeAspectModeField != null)
            playerRuntimeAspectModeField.valNoCallback = "aspect=" + FrameAngelPlayerMediaParity.DescribeAspectMode(aspectMode);
    }

    private static bool ShouldExposePlayerAspectControls()
    {
        return !PlayerSingleDisplayRelease;
    }

    private static string CoercePlayerReleaseAspectMode(string aspectMode)
    {
        if (!PlayerSingleDisplayRelease)
            return string.IsNullOrEmpty(aspectMode) ? GhostScreenAspectModeFit : aspectMode;

        return PlayerSingleDisplayReleaseAspectMode;
    }

    private string ResolvePlayerMediaBrowserSeedPathFromSelection(string candidatePath)
    {
        string normalizedCandidate = NormalizeStandalonePlayerPath(candidatePath);
        if (string.IsNullOrEmpty(normalizedCandidate))
            return "";

        if (FileManagerSecure.FileExists(normalizedCandidate, false))
            return FileManagerSecure.GetDirectoryName(normalizedCandidate, false) ?? "";

        if (FileManagerSecure.DirectoryExists(normalizedCandidate, false))
            return normalizedCandidate;

        return "";
    }

    private string ResolveDefaultPlayerMediaBrowserSeedPath()
    {
        for (int i = 0; i < PlayerMediaBrowserSeedPathCandidates.Length; i++)
        {
            string candidatePath = PlayerMediaBrowserSeedPathCandidates[i];
            if (string.IsNullOrEmpty(candidatePath))
                continue;

            if (FileManagerSecure.DirectoryExists(candidatePath, false))
                return candidatePath;
        }

        return "";
    }

    private void RunPlayerLoadMedia()
    {
        if (TryOpenPlayerMediaBrowser("Player loading selected media", false))
            return;

        RunAttachedPlayerLoadMedia("", "Player loading selected media");
    }

    private void RunPlayerLoadMedia(string preferredMediaPath, string successStatus)
    {
        RunPlayerLoadMedia(preferredMediaPath, successStatus, false);
    }

    private void RunAttachedPlayerLoadMedia(string preferredMediaPath, string successStatus)
    {
        string selectorJson;
        string errorMessage;
        if (!TryBuildAttachedPlayerSelectorJson(out selectorJson, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            SetPendingPlayerStateSummary("state=selector_unresolved err=" + SanitizePlayerPanelValue(errorMessage));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        string selectedMediaPath = string.IsNullOrEmpty(preferredMediaPath)
            ? (string.IsNullOrEmpty(playerMediaPath) ? "" : playerMediaPath.Trim())
            : preferredMediaPath.Trim();
        if (string.IsNullOrEmpty(selectedMediaPath))
        {
            errorMessage = "Player media path is unavailable";
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            SetPendingPlayerStateSummary("state=media_path_unavailable");
            RefreshVisiblePlayerDebugFields();
            return;
        }

        List<string> mediaPaths;
        if (!TryResolvePlayerRuntimeMediaPaths(selectedMediaPath, out mediaPaths, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            SetPendingPlayerStateSummary("state=media_path_unavailable err=" + SanitizePlayerPanelValue(errorMessage));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        selectedMediaPath = ResolvePrimaryPlayerRuntimeMediaPath(selectedMediaPath, mediaPaths);
        SetPendingPlayerSelection(selectedMediaPath);
        SetPendingPlayerStateSummary("state=load_requested source=player");
        bool shouldPlaySelectedMedia = !FrameAngelPlayerMediaParity.IsSupportedImagePath(selectedMediaPath);

        string argsJson = BuildAttachedPlayerActionArgsJson(
            selectorJson,
            "\"mediaPath\":\"" + EscapeJsonString(selectedMediaPath) + "\""
                + ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths)
                + ",\"play\":" + (shouldPlaySelectedMedia ? "true" : "false"));

        string resultJson;
        if (!TryExecuteAction(PlayerActionLoadPathId, argsJson, out resultJson, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(resultJson);
            SetPendingPlayerStateSummary("state=load_failed err=" + SanitizePlayerPanelValue(errorMessage));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        SetLastError("");
        SetLastReceipt(resultJson);
        SetPendingPlayerStateSummary(BuildPendingPlayerSuccessSummary(successStatus));
        RefreshVisiblePlayerDebugFields();
    }

    private void RunPlayerLoadMedia(string preferredMediaPath, string successStatus, bool allowSampleFallback)
    {
        TryHydrateMetaProofDefaults();

        string targetInstanceId;
        if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
            return;

        string selectedMediaPath = string.IsNullOrEmpty(preferredMediaPath)
            ? (string.IsNullOrEmpty(playerMediaPath) ? "" : playerMediaPath.Trim())
            : preferredMediaPath.Trim();

        List<string> mediaPaths;
        bool usingConfiguredMedia;
        string errorMessage;
        if (string.IsNullOrEmpty(selectedMediaPath))
        {
            if (!allowSampleFallback)
            {
                SetMetaProofStatus("Player media path is unavailable");
                return;
            }

            if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out errorMessage))
            {
                SetMetaProofStatus(string.IsNullOrEmpty(errorMessage) ? "Player media path is unavailable" : errorMessage);
                return;
            }

            selectedMediaPath = mediaPaths.Count > 0 ? mediaPaths[0] : "";
            if (string.IsNullOrEmpty(selectedMediaPath))
            {
                SetMetaProofStatus("Player media path is unavailable");
                return;
            }
        }
        else
        {
            if (!TryResolvePlayerRuntimeMediaPaths(selectedMediaPath, out mediaPaths, out errorMessage))
            {
                SetMetaProofStatus(string.IsNullOrEmpty(errorMessage) ? "Player media path is unavailable" : errorMessage);
                return;
            }

            usingConfiguredMedia = true;
            selectedMediaPath = ResolvePrimaryPlayerRuntimeMediaPath(selectedMediaPath, mediaPaths);
        }

        bool rememberAsPlayerSelection =
            !allowSampleFallback
            || usingConfiguredMedia
            || !IsMetaProofDefaultMediaPath(selectedMediaPath);

        if (rememberAsPlayerSelection)
            playerMediaPath = selectedMediaPath;
        syncMetaProofMediaPath = selectedMediaPath;
        if (playerMediaPathField != null && rememberAsPlayerSelection)
            playerMediaPathField.valNoCallback = playerMediaPath;
        if (syncMetaProofMediaPathField != null)
            syncMetaProofMediaPathField.valNoCallback = selectedMediaPath;
        bool shouldPlaySelectedMedia = !FrameAngelPlayerMediaParity.IsSupportedImagePath(selectedMediaPath);

        string loadArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"displayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
            + ",\"mediaPath\":\"" + EscapeJsonString(selectedMediaPath) + "\""
            + ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths)
            + ",\"play\":" + (shouldPlaySelectedMedia ? "true" : "false")
            + "}";

        if (!TryExecuteMetaProofAction(PlayerActionLoadPathId, loadArgsJson, out _))
            return;

        SetMetaProofStatus(string.IsNullOrEmpty(successStatus)
            ? "Player loading selected media"
            : successStatus);
    }

    private bool TryOpenPlayerMediaBrowser(string successStatus, bool allowMetaProofSeed)
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
            return false;

        if (playerMediaBrowserOpen)
        {
            if (!allowMetaProofSeed)
            {
                SetPendingPlayerStateSummary("state=browser_open source=player");
                RefreshVisiblePlayerDebugFields();
            }
            return true;
        }

        string seedPath = ResolvePlayerMediaBrowserSeedPath(allowMetaProofSeed);
        string browserSeedPath = NormalizeStandalonePlayerPath(seedPath);
        if (!IsSecureRuntimePathCandidate(browserSeedPath))
            browserSeedPath = "";
        List<ShortCut> shortCuts = BuildPlayerMediaBrowserShortCuts(browserSeedPath);
        string dialogSeedPath = NormalizePlayerMediaBrowserDialogPath(browserSeedPath);

        uFileBrowser.FileBrowserCallback callback = delegate(string path)
        {
            HandlePlayerMediaBrowserSelection(path);
        };

        try
        {
            EnsurePlayerMediaFileBrowserActive(sc);

            pendingPlayerMediaBrowserSuccessStatus = string.IsNullOrEmpty(successStatus)
                ? "Player loading selected media"
                : successStatus;
            pendingPlayerMediaBrowserTargetsMetaProof = allowMetaProofSeed;
            playerMediaBrowserOpen = true;
            if (!string.IsNullOrEmpty(dialogSeedPath))
            {
                sc.GetMediaPathDialog(
                    callback,
                    "",
                    dialogSeedPath,
                    false,
                    true,
                    false,
                    "",
                    false,
                    shortCuts,
                    false,
                    true);
            }
            else
            {
                sc.GetMediaPathDialog(callback);
            }
            if (!allowMetaProofSeed)
            {
                SetPendingPlayerStateSummary("state=browser_open source=player");
                RefreshVisiblePlayerDebugFields();
            }
            return true;
        }
        catch (Exception ex)
        {
            playerMediaBrowserOpen = false;
            pendingPlayerMediaBrowserSuccessStatus = "";
            pendingPlayerMediaBrowserTargetsMetaProof = false;
            if (!allowMetaProofSeed)
            {
                SetPendingPlayerStateSummary("state=browser_failed err=" + SanitizePlayerPanelValue(ex.Message));
                RefreshVisiblePlayerDebugFields();
            }
            return false;
        }
    }

    private bool EnsurePlayerMediaFileBrowserActive(SuperController sc)
    {
        if (sc == null || sc.mediaFileBrowserUI == null)
            return false;

        try
        {
            if (sc.mediaFileBrowserUI.gameObject != null && !sc.mediaFileBrowserUI.gameObject.activeSelf)
                sc.mediaFileBrowserUI.gameObject.SetActive(true);

            if (!sc.mediaFileBrowserUI.enabled)
                sc.mediaFileBrowserUI.enabled = true;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void HandlePlayerMediaBrowserSelection(string path)
    {
        HandlePlayerMediaBrowserClosed(path, string.IsNullOrEmpty(path));
    }

    private void HandlePlayerMediaBrowserClosed(string path, bool didClose)
    {
        playerMediaBrowserOpen = false;

        string selectedPath = string.IsNullOrEmpty(path) ? "" : path.Trim();
        string successStatus = string.IsNullOrEmpty(pendingPlayerMediaBrowserSuccessStatus)
            ? "Player loading selected media"
            : pendingPlayerMediaBrowserSuccessStatus;
        bool targetsMetaProof = pendingPlayerMediaBrowserTargetsMetaProof;
        pendingPlayerMediaBrowserSuccessStatus = "";
        pendingPlayerMediaBrowserTargetsMetaProof = false;

        if (string.IsNullOrEmpty(selectedPath))
        {
            if (didClose)
            {
                if (!targetsMetaProof)
                {
                    SetPendingPlayerStateSummary("state=selection_canceled source=player");
                    RefreshVisiblePlayerDebugFields();
                }
            }
            return;
        }

        string normalizedSelectedPath = NormalizePlayerMediaBrowserSelectedPath(selectedPath);
        if (string.IsNullOrEmpty(normalizedSelectedPath))
        {
            if (!targetsMetaProof)
            {
                SetPendingPlayerStateSummary("state=selection_rejected reason=outside_vam_content");
                RefreshVisiblePlayerDebugFields();
            }
            return;
        }

        if (FileManagerSecure.DirectoryExists(normalizedSelectedPath, false))
        {
            if (!targetsMetaProof)
            {
                SetPendingPlayerStateSummary("state=selection_rejected reason=directory");
                RefreshVisiblePlayerDebugFields();
            }
            return;
        }

        if (!FileManagerSecure.FileExists(normalizedSelectedPath, false))
        {
            if (!targetsMetaProof)
            {
                SetPendingPlayerStateSummary("state=selection_rejected reason=file_missing");
                RefreshVisiblePlayerDebugFields();
            }
            return;
        }

        if (!IsSupportedPlayerRuntimeMediaPath(normalizedSelectedPath))
        {
            if (!targetsMetaProof)
            {
                SetPendingPlayerStateSummary("state=selection_rejected reason=unsupported_extension");
                RefreshVisiblePlayerDebugFields();
            }
            return;
        }

        if (targetsMetaProof)
        {
            syncMetaProofMediaPath = normalizedSelectedPath;
            if (syncMetaProofMediaPathField != null)
                syncMetaProofMediaPathField.valNoCallback = normalizedSelectedPath;
            RunPlayerLoadMedia(normalizedSelectedPath, successStatus, true);
            return;
        }

        SetPendingPlayerSelection(normalizedSelectedPath);
        SetPendingPlayerStateSummary("state=file_selected source=player");
        RunAttachedPlayerLoadMedia(normalizedSelectedPath, successStatus);
    }

    private void SetPendingPlayerSelection(string selectedPath)
    {
        playerMediaPath = string.IsNullOrEmpty(selectedPath) ? "" : selectedPath.Trim();
        if (playerMediaPathField != null)
            playerMediaPathField.valNoCallback = playerMediaPath;

        playerPendingTargetSummary = BuildPendingPlayerTargetSummary();
        playerPendingMediaSummary = BuildPendingPlayerMediaSummary();
        playerPendingParitySummary = BuildPendingPlayerParitySummary();
        if (string.IsNullOrEmpty(playerPendingTimelineSummary))
            playerPendingTimelineSummary = "timeline=idle";
        playerPendingPlaylistSummary = BuildPendingPlayerPlaylistSummary();
    }

    private void SetPendingPlayerStateSummary(string summary)
    {
        playerPendingStateSummary = string.IsNullOrEmpty(summary) ? "state=idle" : summary;
    }

    private string BuildPendingPlayerSuccessSummary(string successStatus)
    {
        string normalizedStatus = string.IsNullOrEmpty(successStatus)
            ? ""
            : successStatus.Trim();
        if (string.IsNullOrEmpty(normalizedStatus))
            return "state=load_requested source=player";

        if (normalizedStatus.IndexOf("meta proof", StringComparison.OrdinalIgnoreCase) >= 0)
            return "state=load_requested source=meta_proof";

        if (normalizedStatus.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0)
            return "state=load_requested source=player";

        return "state=" + SanitizePlayerPanelValue(normalizedStatus);
    }

    private string SanitizePlayerPanelValue(string value)
    {
        string normalized = string.IsNullOrEmpty(value) ? "" : value.Trim();
        if (string.IsNullOrEmpty(normalized))
            return "none";

        normalized = Regex.Replace(normalized, "\\s+", "_");
        return normalized.ToLowerInvariant();
    }

    private string ResolvePlayerMediaBrowserSeedPath(bool allowMetaProofSeed)
    {
        string playerSelectionSeedPath = ResolvePlayerMediaBrowserSeedPathFromSelection(playerMediaPath);
        if (!string.IsNullOrEmpty(playerSelectionSeedPath))
            return playerSelectionSeedPath;

        if (allowMetaProofSeed)
        {
            string metaProofSeedPath = ResolvePlayerMediaBrowserSeedPathFromSelection(syncMetaProofMediaPath);
            if (!string.IsNullOrEmpty(metaProofSeedPath))
                return metaProofSeedPath;
        }

        string defaultSeedPath = ResolveDefaultPlayerMediaBrowserSeedPath();
        if (!string.IsNullOrEmpty(defaultSeedPath))
            return defaultSeedPath;

        return allowMetaProofSeed ? ResolveDefaultMetaProofSampleMediaPath() : "";
    }

    private string NormalizePlayerMediaBrowserSelectedPath(string selectedPath)
    {
        string normalizedSelection = NormalizeStandalonePlayerPath(selectedPath);
        if (string.IsNullOrEmpty(normalizedSelection))
            return "";

        if (IsSecureRuntimePathCandidate(normalizedSelection))
            return normalizedSelection;

        string dataPath = NormalizeStandalonePlayerPath(Application.dataPath);
        int slash = dataPath.LastIndexOf('\\');
        if (slash <= 0)
            return "";

        string gameRoot = dataPath.Substring(0, slash).TrimEnd('\\');
        string gameRootWithSlash = gameRoot + "\\";
        if (!normalizedSelection.StartsWith(gameRootWithSlash, StringComparison.OrdinalIgnoreCase))
            return "";

        string relativePath = normalizedSelection.Substring(gameRootWithSlash.Length);
        return IsSecureRuntimePathCandidate(relativePath)
            ? relativePath
            : "";
    }

﻿#if FRAMEANGEL_TEST_SURFACES
    private void RunPlayerQuickDemo()
    {
        if (syncMetaProofSmokeCoroutine != null)
            StopCoroutine(syncMetaProofSmokeCoroutine);

        syncMetaProofSmokeCoroutine = StartCoroutine(RunPlayerQuickDemoCoroutine());
    }

    private IEnumerator RunPlayerQuickDemoCoroutine()
    {
        SetMetaProofStatus("Player quick demo starting");

        TryHydrateMetaProofDefaults();
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            List<string> mediaPaths;
            bool usingConfiguredMedia;
            string mediaError;
            if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out mediaError))
            {
                SetMetaProofStatus(string.IsNullOrEmpty(mediaError) ? "Player media path is unavailable" : mediaError);
                syncMetaProofSmokeCoroutine = null;
                yield break;
            }

            string selectedMediaPath = mediaPaths.Count > 0 ? (mediaPaths[0] ?? "") : "";
            if (string.IsNullOrEmpty(selectedMediaPath))
            {
                SetMetaProofStatus("Player media path is unavailable");
                syncMetaProofSmokeCoroutine = null;
                yield break;
            }

            RunAttachedPlayerLoadMedia(selectedMediaPath, "Player quick demo loading media");
            yield return new WaitForSecondsRealtime(0.25f);

            RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFit, "Player quick demo aspect fit");
            yield return new WaitForSecondsRealtime(0.20f);

            RunAttachedPlayerDirectAction(
                PlayerActionSeekNormalizedId,
                "\"normalized\":0",
                "Player quick demo seek start");
            yield return new WaitForSecondsRealtime(0.20f);

            RunAttachedPlayerDirectAction(PlayerActionPlayId, "", "Player quick demo play");
            SetMetaProofStatus("Player quick demo ready");
            syncMetaProofSmokeCoroutine = null;
            yield break;
        }

        string instanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
        if (string.IsNullOrEmpty(instanceId) || !TryEnsureMetaProofControlSurfaceInstance(instanceId))
        {
            syncMetaProofSmokeCoroutine = null;
            yield break;
        }

        string targetInstanceId;
        if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
        {
            syncMetaProofSmokeCoroutine = null;
            yield break;
        }

        string layoutError;
        if (!TryNormalizeMetaProofTargetScreenForQuickDemo(targetInstanceId, out layoutError))
        {
            SetMetaProofStatus(string.IsNullOrEmpty(layoutError) ? "Player quick demo target layout failed" : layoutError);
            syncMetaProofSmokeCoroutine = null;
            yield break;
        }

        TryLayoutMetaProofControlSurface(instanceId, targetInstanceId, out _);
        TryBindMetaProofToPlayer(instanceId, targetInstanceId, out _);
        if (!TryEnsureMetaProofStandalonePlayerMedia(targetInstanceId))
        {
            syncMetaProofSmokeCoroutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.25f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetAspectModeId,
            "\"aspectMode\":\"fit\"",
            "Player quick demo aspect fit");
        yield return new WaitForSecondsRealtime(0.20f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSeekNormalizedId,
            "\"normalized\":0",
            "Player quick demo seek start");
        yield return new WaitForSecondsRealtime(0.20f);

        RunMetaProofTrigger("scrub_slider", "{\"normalized\":0}");
        yield return new WaitForSecondsRealtime(0.10f);

        RunMetaProofDirectPlayerAction(PlayerActionPlayId, "", "Player quick demo play");
        SetMetaProofStatus("Player quick demo ready");
        syncMetaProofSmokeCoroutine = null;
    }

    private IEnumerator RunAttachedHostPlayerQuickDemoCoroutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(0.10f);

        if (!syncMetaProofAutoStartPending)
            yield break;

        syncMetaProofAutoStartPending = false;
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostUid = attachedHostAtom.uid ?? MetaProofPreferredHostAtomUid;
            string hostedTargetInstanceId = BuildMetaProofHostedTargetInstanceId(hostUid);
            string hostedControlInstanceId = BuildMetaProofHostedControlSurfaceInstanceId(hostUid);
            if (!string.Equals(hostedTargetInstanceId, MetaProofGhostScreenDefaultInstanceId, StringComparison.OrdinalIgnoreCase))
                DeleteInnerPieceInstanceIfPresent(MetaProofGhostScreenDefaultInstanceId);
            if (!string.Equals(hostedControlInstanceId, MetaProofControlSurfaceDefaultInstanceId, StringComparison.OrdinalIgnoreCase))
                DeleteInnerPieceInstanceIfPresent(MetaProofControlSurfaceDefaultInstanceId);
        }

        if (syncMetaProofSmokeCoroutine == null)
            RunPlayerQuickDemo();
    }
#endif


    private bool TryRunMetaToolkitSpawnDefaultThemeDemoAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string traceFieldsJson = BuildMetaToolkitDemoTraceFieldsJson(argsJson);
        string targetInstanceId = ExtractJsonArgString(argsJson, "targetInstanceId", "instanceId");
        if (string.IsNullOrEmpty(targetInstanceId) && !TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta toolkit demo needs target screen" : syncMetaProofStatus;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPiecePlaneData targetPlane;
        if (!TryBuildMetaToolkitDemoTargetPlane(targetInstanceId, out targetPlane, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Quaternion panelRotation = Quaternion.LookRotation(targetPlane.forward, targetPlane.up);
        List<string> spawnedInstanceIds = new List<string>();
        string playerControlSurfaceInstanceId = "";
        string playerResourceId = "";

        for (int i = 0; i < MetaToolkitDefaultThemeDemoSurfaces.Length; i++)
        {
            MetaToolkitDemoSurfaceDefinition definition = MetaToolkitDefaultThemeDemoSurfaces[i];
            DeleteInnerPieceInstanceIfPresent(definition.instanceId);

            string resourceId;
            if (!TryImportMetaToolkitDemoPackage(definition.packageFolderName, traceFieldsJson, out resourceId, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            string spawnArgsJson = "{"
                + "\"resourceId\":\"" + EscapeJsonString(resourceId) + "\""
                + ",\"instanceId\":\"" + EscapeJsonString(definition.instanceId) + "\""
                + traceFieldsJson
                + "}";
            string ignoredResultJson;
            if (!TryExecuteMetaToolkitDemoAction(HostResourceSpawnActionId, spawnArgsJson, out ignoredResultJson, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (definition.bindToPlayer)
            {
                playerControlSurfaceInstanceId = definition.instanceId;
                playerResourceId = resourceId;

                string layoutArgsJson = "{"
                    + "\"controlSurfaceInstanceId\":\"" + EscapeJsonString(definition.instanceId) + "\""
                    + ",\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                    + traceFieldsJson
                    + "}";
                if (!TryExecuteMetaToolkitDemoAction(PlayerActionLayoutControlSurfaceRelativeId, layoutArgsJson, out ignoredResultJson, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                string bindArgsJson = "{"
                    + "\"controlSurfaceInstanceId\":\"" + EscapeJsonString(definition.instanceId) + "\""
                    + ",\"preferStandalonePlayer\":true"
                    + ",\"targetDisplayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
                    + ",\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                    + traceFieldsJson
                    + "}";
                if (!TryExecuteMetaToolkitDemoAction(PlayerActionBindControlSurfaceId, bindArgsJson, out ignoredResultJson, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                if (!Mathf.Approximately(definition.uniformScale, 1f))
                {
                    string scaleArgsJson = "{"
                        + "\"instanceId\":\"" + EscapeJsonString(definition.instanceId) + "\""
                        + ",\"uniformScaleMultiplier\":" + FormatFloat(definition.uniformScale)
                        + traceFieldsJson
                        + "}";
                    if (!TryExecuteMetaToolkitDemoAction(HostInstanceTransformActionId, scaleArgsJson, out ignoredResultJson, out errorMessage))
                    {
                        resultJson = BuildBrokerResult(false, errorMessage, "{}");
                        return false;
                    }
                }
            }
            else
            {
                Vector3 worldPosition =
                    targetPlane.center
                    + (targetPlane.right * definition.rightOffsetMeters)
                    + (targetPlane.up * definition.upOffsetMeters)
                    + (targetPlane.forward * definition.forwardOffsetMeters);

                string transformArgsJson = "{"
                    + "\"instanceId\":\"" + EscapeJsonString(definition.instanceId) + "\""
                    + ",\"position\":" + BuildJsonVector3(worldPosition)
                    + ",\"rotateEuler\":" + BuildJsonVector3(panelRotation.eulerAngles)
                    + (Mathf.Approximately(definition.uniformScale, 1f) ? "" : ",\"uniformScaleMultiplier\":" + FormatFloat(definition.uniformScale))
                    + traceFieldsJson
                    + "}";
                if (!TryExecuteMetaToolkitDemoAction(HostInstanceTransformActionId, transformArgsJson, out ignoredResultJson, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }

            spawnedInstanceIds.Add(definition.instanceId);
        }

        if (!string.IsNullOrEmpty(targetInstanceId))
            TryEnsureMetaProofStandalonePlayerMedia(targetInstanceId);

        StringBuilder payload = new StringBuilder(768);
        payload.Append('{');
        payload.Append("\"targetInstanceId\":\"").Append(EscapeJsonString(targetInstanceId)).Append("\",");
        payload.Append("\"interactiveInstanceId\":\"").Append(EscapeJsonString(playerControlSurfaceInstanceId)).Append("\",");
        payload.Append("\"interactiveResourceId\":\"").Append(EscapeJsonString(playerResourceId)).Append("\",");
        payload.Append("\"demoRootPath\":\"").Append(EscapeJsonString(MetaToolkitDemoDefaultThemeLocalRootPath)).Append("\",");
        payload.Append("\"spawnedInstanceIds\":[");
        for (int i = 0; i < spawnedInstanceIds.Count; i++)
        {
            if (i > 0)
                payload.Append(',');
            payload.Append('"').Append(EscapeJsonString(spawnedInstanceIds[i])).Append('"');
        }
        payload.Append("]}");

        resultJson = BuildBrokerResult(true, "meta_toolkit_default_theme_demo_spawned", payload.ToString());
        return true;
    }

    private bool TryRunMetaToolkitListDefaultThemeElementsAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string payload = BuildMetaToolkitDefaultThemeElementCatalogJson();
        resultJson = BuildBrokerResult(true, "meta_toolkit_default_theme_elements ok", payload);
        EmitRuntimeEvent(
            "meta_toolkit_default_theme_elements",
            actionId,
            "ok",
            "",
            "meta_toolkit_default_theme_elements ok",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryRunMetaToolkitListLocalThemePackagesAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string rootPath = ExtractJsonArgString(argsJson, "rootPath", "themeRootPath", "packageRootPath");
        if (string.IsNullOrEmpty(rootPath))
            rootPath = ResolveMetaToolkitDefaultThemeLocalRootPath();

        string payload = BuildMetaToolkitLocalThemePackageCatalogJson(rootPath);
        resultJson = BuildBrokerResult(true, "meta_toolkit_local_theme_packages ok", payload);
        EmitRuntimeEvent(
            "meta_toolkit_local_theme_packages",
            actionId,
            "ok",
            "",
            "meta_toolkit_local_theme_packages ok",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryRunMetaToolkitSpawnAnchoredDefaultSetAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string traceFieldsJson = BuildMetaToolkitDemoTraceFieldsJson(argsJson);
        string anchorPrefix = ExtractJsonArgString(argsJson, "anchorPrefix", "prefix");
        if (string.IsNullOrEmpty(anchorPrefix))
            anchorPrefix = "target";

        string[] elementKeys = new string[] { "backplate", "search_bar", "primary_button", "small_slider", "dialog" };
        string[] defaultAnchorUids = new string[]
        {
            anchorPrefix + "1",
            anchorPrefix + "2",
            anchorPrefix + "3",
            anchorPrefix + "4",
            anchorPrefix + "5"
        };

        string[] overrideKeys = new string[]
        {
            "backplateAnchorAtomUid",
            "searchBarAnchorAtomUid",
            "primaryButtonAnchorAtomUid",
            "smallSliderAnchorAtomUid",
            "dialogAnchorAtomUid"
        };

        List<string> spawnedInstanceIds = new List<string>();
        StringBuilder payload = new StringBuilder(1024);
        payload.Append('{');
        payload.Append("\"anchorPrefix\":\"").Append(EscapeJsonString(anchorPrefix)).Append("\",");
        payload.Append("\"results\":[");

        for (int i = 0; i < elementKeys.Length; i++)
        {
            string anchorAtomUid = ExtractJsonArgString(argsJson, overrideKeys[i]);
            if (string.IsNullOrEmpty(anchorAtomUid))
                anchorAtomUid = defaultAnchorUids[i];

            string instanceId = "meta_anchored_" + anchorAtomUid + "_" + elementKeys[i];
            instanceId = instanceId.Replace(' ', '_');

            string spawnArgsJson = "{"
                + "\"elementKey\":\"" + EscapeJsonString(elementKeys[i]) + "\""
                + ",\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
                + ",\"anchorAtomUid\":\"" + EscapeJsonString(anchorAtomUid) + "\""
                + ",\"followPosition\":true"
                + ",\"followRotation\":true"
                + ",\"snapUnderAnchor\":true"
                + traceFieldsJson
                + "}";

            string elementResultJson;
            bool ok = TryRunMetaToolkitSpawnDefaultThemeElementAction(actionId, spawnArgsJson, out elementResultJson, out errorMessage);
            if (!ok)
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (i > 0)
                payload.Append(',');
            payload.Append('{');
            payload.Append("\"elementKey\":\"").Append(EscapeJsonString(elementKeys[i])).Append("\",");
            payload.Append("\"anchorAtomUid\":\"").Append(EscapeJsonString(anchorAtomUid)).Append("\",");
            payload.Append("\"instanceId\":\"").Append(EscapeJsonString(instanceId)).Append('"');
            payload.Append('}');

            spawnedInstanceIds.Add(instanceId);
        }

        payload.Append("],\"spawnedInstanceIds\":[");
        for (int i = 0; i < spawnedInstanceIds.Count; i++)
        {
            if (i > 0)
                payload.Append(',');
            payload.Append('"').Append(EscapeJsonString(spawnedInstanceIds[i])).Append('"');
        }
        payload.Append("]}");

        resultJson = BuildBrokerResult(true, "meta_toolkit_anchored_default_set_spawned", payload.ToString());
        EmitRuntimeEvent(
            "meta_toolkit_anchored_default_set_spawned",
            actionId,
            "ok",
            "",
            "meta_toolkit_anchored_default_set_spawned",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload.ToString()
        );
        return true;
    }

    private bool TryRunMetaToolkitSpawnDefaultThemeElementAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string key = ExtractJsonArgString(argsJson, "elementKey", "key", "surfaceKey", "toolkitKey");
        MetaToolkitDemoSurfaceDefinition definition;
        if (!TryFindMetaToolkitDefaultThemeSurfaceDefinition(key, out definition))
        {
            errorMessage = "meta toolkit element definition not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        return TryRunMetaToolkitSpawnResolvedPackageAction(
            actionId,
            argsJson,
            definition.key ?? "",
            definition.controlFamilyId ?? "",
            BuildMetaToolkitDemoPackagePath(definition.packageFolderName),
            definition.instanceId ?? "",
            definition.bindToPlayer,
            definition.uniformScale,
            definition.rightOffsetMeters,
            definition.upOffsetMeters,
            definition.forwardOffsetMeters,
            out resultJson,
            out errorMessage);
    }

    private bool TryRunMetaToolkitSpawnLocalThemePackageAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        MetaToolkitLocalPackageDefinition packageDefinition;
        if (!TryResolveMetaToolkitLocalPackageDefinition(argsJson, out packageDefinition, out errorMessage) || packageDefinition == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        return TryRunMetaToolkitSpawnResolvedPackageAction(
            actionId,
            argsJson,
            packageDefinition.key ?? packageDefinition.packageFolderName ?? "",
            packageDefinition.controlFamilyId ?? "",
            packageDefinition.packagePath ?? "",
            packageDefinition.defaultInstanceId ?? "",
            packageDefinition.bindToPlayer,
            1f,
            0f,
            0f,
            0.05f,
            out resultJson,
            out errorMessage);
    }

    private bool TryRunMetaToolkitSpawnResolvedPackageAction(
        string actionId,
        string argsJson,
        string packageKey,
        string controlFamilyId,
        string packagePath,
        string defaultInstanceId,
        bool defaultBindToPlayer,
        float defaultUniformScale,
        float defaultRightOffsetMeters,
        float defaultUpOffsetMeters,
        float defaultForwardOffsetMeters,
        out string resultJson,
        out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string traceFieldsJson = BuildMetaToolkitDemoTraceFieldsJson(argsJson);
        string instanceId = ExtractJsonArgString(argsJson, "controlSurfaceInstanceId", "instanceId");
        if (string.IsNullOrEmpty(instanceId))
            instanceId = defaultInstanceId;

        DeleteInnerPieceInstanceIfPresent(instanceId);

        string resourceId;
        if (!TryImportMetaToolkitPackage(packagePath, traceFieldsJson, out resourceId, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string spawnArgsJson = "{"
            + "\"resourceId\":\"" + EscapeJsonString(resourceId) + "\""
            + ",\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
            + traceFieldsJson
            + "}";
        string ignoredResultJson;
            if (!TryExecuteMetaToolkitDemoAction(HostResourceSpawnActionId, spawnArgsJson, out ignoredResultJson, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool bindToPlayer = defaultBindToPlayer;
        bool parsedBindToPlayer;
        if (TryReadBoolArg(argsJson, out parsedBindToPlayer, "bindToPlayer", "bind"))
            bindToPlayer = parsedBindToPlayer;

        string targetInstanceId = ExtractJsonArgString(argsJson, "targetInstanceId", "targetScreenInstanceId");
        string targetDisplayId = ExtractJsonArgString(argsJson, "targetDisplayId", "displayId");
        if (string.IsNullOrEmpty(targetDisplayId))
            targetDisplayId = InnerPiecePrimaryPlayerDisplayId;

        string requestedAnchorUid = ExtractJsonArgString(argsJson, "anchorAtomUid", "atomUid", "anchorUid", "uid");
        bool useAnchorFollowPlacement = !bindToPlayer && !string.IsNullOrEmpty(requestedAnchorUid);

        Vector3 worldPosition = Vector3.zero;
        bool hasExplicitPosition =
            TryReadVectorArg(argsJson, "position", out worldPosition)
            || TryReadVectorArg(argsJson, "pos", out worldPosition);

        Quaternion worldRotation = Quaternion.identity;
        bool hasExplicitRotation = TryReadQuaternionComponents(argsJson, out worldRotation);
        if (!hasExplicitRotation)
        {
            Vector3 rotateEuler;
            if (TryReadVectorArg(argsJson, "rotateEuler", out rotateEuler) || TryReadVectorArg(argsJson, "euler", out rotateEuler))
            {
                worldRotation = Quaternion.Euler(rotateEuler);
                hasExplicitRotation = true;
            }
        }

        float rightOffsetMeters = defaultRightOffsetMeters;
        float upOffsetMeters = defaultUpOffsetMeters;
        float forwardOffsetMeters = defaultForwardOffsetMeters;
        float parsedFloat;
        if (TryExtractJsonFloatField(argsJson, "rightOffsetMeters", out parsedFloat))
            rightOffsetMeters = parsedFloat;
        if (TryExtractJsonFloatField(argsJson, "upOffsetMeters", out parsedFloat))
            upOffsetMeters = parsedFloat;
        if (TryExtractJsonFloatField(argsJson, "forwardOffsetMeters", out parsedFloat))
            forwardOffsetMeters = parsedFloat;

        float uniformScaleMultiplier = defaultUniformScale;
        if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out parsedFloat)
            || TryExtractJsonFloatField(argsJson, "scale", out parsedFloat))
        {
            uniformScaleMultiplier = parsedFloat;
        }

        FAInnerPiecePlaneData targetPlane = new FAInnerPiecePlaneData();
        bool hasTargetPlane = false;
        if (!useAnchorFollowPlacement
            && string.IsNullOrEmpty(targetInstanceId)
            && (!hasExplicitPosition || bindToPlayer))
        {
            if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus)
                    ? "meta toolkit element spawn needs target screen or explicit position"
                    : syncMetaProofStatus;
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }

        if (!string.IsNullOrEmpty(targetInstanceId))
        {
            if (!TryBuildMetaToolkitDemoTargetPlane(targetInstanceId, out targetPlane, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            hasTargetPlane = true;
        }

        if (bindToPlayer)
        {
            string layoutArgsJson = "{"
                + "\"controlSurfaceInstanceId\":\"" + EscapeJsonString(instanceId) + "\""
                + ",\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                + traceFieldsJson
                + "}";
            if (!TryExecuteMetaToolkitDemoAction(PlayerActionLayoutControlSurfaceRelativeId, layoutArgsJson, out ignoredResultJson, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            string bindArgsJson = "{"
                + "\"controlSurfaceInstanceId\":\"" + EscapeJsonString(instanceId) + "\""
                + ",\"preferStandalonePlayer\":true"
                + ",\"targetDisplayId\":\"" + EscapeJsonString(targetDisplayId) + "\""
                + ",\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                + traceFieldsJson
                + "}";
            if (!TryExecuteMetaToolkitDemoAction(PlayerActionBindControlSurfaceId, bindArgsJson, out ignoredResultJson, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (!Mathf.Approximately(uniformScaleMultiplier, 1f))
            {
                string scaleArgsJson = "{"
                    + "\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
                    + ",\"uniformScaleMultiplier\":" + FormatFloat(uniformScaleMultiplier)
                    + traceFieldsJson
                    + "}";
                if (!TryExecuteMetaToolkitDemoAction(HostInstanceTransformActionId, scaleArgsJson, out ignoredResultJson, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(targetInstanceId))
                TryEnsureMetaProofStandalonePlayerMedia(targetInstanceId);
        }
        else if (useAnchorFollowPlacement)
        {
            StringBuilder followArgs = new StringBuilder(256);
            followArgs.Append('{');
            followArgs.Append("\"instanceId\":\"").Append(EscapeJsonString(instanceId)).Append('"');
            followArgs.Append(",\"anchorAtomUid\":\"").Append(EscapeJsonString(requestedAnchorUid)).Append('"');
            followArgs.Append(",\"enabled\":true");

            bool followPosition = true;
            TryReadBoolArg(argsJson, out followPosition, "followPosition");
            followArgs.Append(",\"followPosition\":").Append(followPosition ? "true" : "false");

            bool followRotation = true;
            TryReadBoolArg(argsJson, out followRotation, "followRotation");
            followArgs.Append(",\"followRotation\":").Append(followRotation ? "true" : "false");

            bool snapUnderAnchor = true;
            TryReadBoolArg(argsJson, out snapUnderAnchor, "snapUnderAnchor", "placeUnderAnchor", "belowAnchor");
            followArgs.Append(",\"snapUnderAnchor\":").Append(snapUnderAnchor ? "true" : "false");

            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "localPositionOffset");
            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "localOffset");
            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "offset");
            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "localRotationEuler");
            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "localRotateEuler");
            AppendMetaToolkitForwardedVectorField(followArgs, argsJson, "localEuler");
            AppendMetaToolkitForwardedFloatField(followArgs, argsJson, "anchorClearanceMeters");
            AppendMetaToolkitForwardedFloatField(followArgs, argsJson, "verticalOffsetMeters");
            AppendMetaToolkitForwardedTraceFields(followArgs, argsJson);
            followArgs.Append('}');

            if (!TrySetInnerPieceFollowBinding(actionId, followArgs.ToString(), out ignoredResultJson, out errorMessage))
            {
                DeleteInnerPieceInstanceIfPresent(instanceId);
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (!Mathf.Approximately(uniformScaleMultiplier, 1f))
            {
                string scaleArgsJson = "{"
                    + "\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
                    + ",\"uniformScaleMultiplier\":" + FormatFloat(uniformScaleMultiplier)
                    + traceFieldsJson
                    + "}";
                if (!TryExecuteMetaToolkitDemoAction(HostInstanceTransformActionId, scaleArgsJson, out ignoredResultJson, out errorMessage))
                {
                    DeleteInnerPieceInstanceIfPresent(instanceId);
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }
            }
        }
        else
        {
            if (!hasExplicitPosition)
            {
                if (!hasTargetPlane)
                {
                    errorMessage = "meta toolkit element spawn needs target screen or explicit position";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                worldPosition =
                    targetPlane.center
                    + (targetPlane.right * rightOffsetMeters)
                    + (targetPlane.up * upOffsetMeters)
                    + (targetPlane.forward * forwardOffsetMeters);
            }

            if (!hasExplicitRotation)
            {
                worldRotation = hasTargetPlane
                    ? Quaternion.LookRotation(targetPlane.forward, targetPlane.up)
                    : Quaternion.identity;
            }

            string transformArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
                + ",\"position\":" + BuildJsonVector3(worldPosition)
                + ",\"rotateEuler\":" + BuildJsonVector3(worldRotation.eulerAngles)
                + (Mathf.Approximately(uniformScaleMultiplier, 1f) ? "" : ",\"uniformScaleMultiplier\":" + FormatFloat(uniformScaleMultiplier))
                + traceFieldsJson
                + "}";
            if (!TryExecuteMetaToolkitDemoAction(HostInstanceTransformActionId, transformArgsJson, out ignoredResultJson, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }

        string stateJson = BuildMetaToolkitControlSurfaceStateJson(instanceId);
        string payload = "{"
            + "\"key\":\"" + EscapeJsonString(packageKey ?? "") + "\""
            + ",\"controlFamilyId\":\"" + EscapeJsonString(controlFamilyId ?? "") + "\""
            + ",\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
            + ",\"resourceId\":\"" + EscapeJsonString(resourceId) + "\""
            + ",\"packagePath\":\"" + EscapeJsonString(packagePath ?? "") + "\""
            + ",\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"targetDisplayId\":\"" + EscapeJsonString(targetDisplayId) + "\""
            + ",\"bindToPlayer\":" + (bindToPlayer ? "true" : "false")
            + ",\"controlSurfaceState\":" + stateJson
            + "}";

        resultJson = BuildBrokerResult(true, "meta_toolkit_default_theme_element_spawned", payload);
        EmitRuntimeEvent(
            "meta_toolkit_default_theme_element_spawned",
            actionId,
            "ok",
            "",
            "meta_toolkit_default_theme_element_spawned",
            targetInstanceId ?? "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            instanceId,
            payload
        );
        return true;
    }

    private bool TryRunMetaToolkitGetControlSurfaceStateAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId = ExtractJsonArgString(argsJson, "controlSurfaceInstanceId", "instanceId");
        if (string.IsNullOrEmpty(instanceId))
        {
            errorMessage = "controlSurfaceInstanceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildMetaToolkitControlSurfaceStateJson(instanceId);
        if (string.IsNullOrEmpty(payload) || string.Equals(payload, "{}", StringComparison.Ordinal))
        {
            errorMessage = "control surface state not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        resultJson = BuildBrokerResult(true, "meta_toolkit_control_surface_state ok", payload);
        EmitRuntimeEvent(
            "meta_toolkit_control_surface_state",
            actionId,
            "ok",
            "",
            "meta_toolkit_control_surface_state ok",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            instanceId,
            payload
        );
        return true;
    }

    private bool TryRunMetaToolkitCancelElementTweenAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId;
        InnerPieceInstanceRecord instance;
        SyncObjectRecord rootRecord;
        if (!TryResolveMetaToolkitControlSurfaceRootRecord(argsJson, out instanceId, out instance, out rootRecord, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool hasActiveTween = rootRecord.tweenCoroutine != null
            || (string.Equals(rootRecord.motionType, "tween", StringComparison.OrdinalIgnoreCase)
                && string.Equals(rootRecord.motionStatus, "running", StringComparison.OrdinalIgnoreCase));
        if (!hasActiveTween)
        {
            errorMessage = "no active tween";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string requestedOperationId = ExtractJsonArgString(argsJson, "operationId", "motionOperationId");
        if (!string.IsNullOrEmpty(requestedOperationId)
            && !string.Equals(requestedOperationId, rootRecord.motionOperationId ?? "", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "active tween operationId mismatch";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (rootRecord.tweenCoroutine != null)
            StopTween(rootRecord);
        else
            CompleteMotionState(rootRecord, "cancelled");

        string payload = BuildMetaToolkitControlSurfaceStateJson(instance, instance.controlSurface);
        resultJson = BuildBrokerResult(true, "meta_toolkit_element_tween_cancelled", payload);
        EmitRuntimeEvent(
            "meta_toolkit_element_tween_cancelled",
            actionId,
            "ok",
            "",
            "meta_toolkit_element_tween_cancelled",
            "",
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            instanceId,
            payload
        );
        return true;
    }

    private bool TryRunMetaToolkitTriggerElementAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TryTriggerToolkitControlSurfaceElement(actionId, argsJson, out resultJson, out errorMessage);
    }

    private bool TryRunMetaToolkitTransformElementAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId;
        if (!TryResolveMetaToolkitControlSurfaceInstanceId(argsJson, out instanceId, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        StringBuilder forwardedArgs = new StringBuilder(256);
        forwardedArgs.Append('{');
        forwardedArgs.Append("\"instanceId\":\"").Append(EscapeJsonString(instanceId)).Append('"');
        AppendMetaToolkitForwardedTransformFields(forwardedArgs, argsJson, false);
        AppendMetaToolkitForwardedTraceFields(forwardedArgs, argsJson);
        forwardedArgs.Append('}');

        if (!TryTransformInnerPieceInstance(actionId, forwardedArgs.ToString(), out string innerResultJson, out errorMessage))
        {
            resultJson = innerResultJson;
            return false;
        }

        resultJson = innerResultJson;
        return true;
    }

    private bool TryRunMetaToolkitTweenElementAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId;
        if (!TryResolveMetaToolkitControlSurfaceInstanceId(argsJson, out instanceId, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance("{\"instanceId\":\"" + EscapeJsonString(instanceId) + "\"}", out instance, out errorMessage) || instance == null)
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null)
        {
            errorMessage = "instance root not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool clearFollow = false;
        TryReadBoolArg(argsJson, out clearFollow, "clearFollow", "disableFollow", "takeOwnership");
        if (instance.followPosition || instance.followRotation)
        {
            if (!clearFollow)
            {
                errorMessage = "follow binding is active; clear follow first or pass clearFollow=true";
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            ClearInnerPieceFollowBinding(instance);
        }

        StringBuilder forwardedArgs = new StringBuilder(256);
        forwardedArgs.Append('{');
        forwardedArgs.Append("\"objectId\":\"").Append(EscapeJsonString(instance.rootObjectId)).Append('"');
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "operationId");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "motionOperationId");
        AppendMetaToolkitForwardedTweenFields(forwardedArgs, argsJson, rootRecord);
        AppendMetaToolkitForwardedTraceFields(forwardedArgs, argsJson);
        forwardedArgs.Append('}');

        if (!TryTweenTransform(actionId, forwardedArgs.ToString(), out string tweenResultJson, out errorMessage))
        {
            resultJson = tweenResultJson;
            return false;
        }

        resultJson = tweenResultJson;
        return true;
    }

    private bool TryResolveMetaToolkitControlSurfaceRootRecord(
        string argsJson,
        out string instanceId,
        out InnerPieceInstanceRecord instance,
        out SyncObjectRecord rootRecord,
        out string errorMessage)
    {
        instanceId = "";
        instance = null;
        rootRecord = null;
        errorMessage = "";

        if (!TryResolveMetaToolkitControlSurfaceInstanceId(argsJson, out instanceId, out errorMessage))
            return false;

        if (!TryResolveInnerPieceInstance("{\"instanceId\":\"" + EscapeJsonString(instanceId) + "\"}", out instance, out errorMessage) || instance == null)
            return false;

        if (!syncObjects.TryGetValue(instance.rootObjectId, out rootRecord) || rootRecord == null)
        {
            errorMessage = "instance root not found";
            return false;
        }

        return true;
    }

    private bool TryRunMetaToolkitSpawnElementAnchorAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId;
        if (!TryResolveMetaToolkitControlSurfaceInstanceId(argsJson, out instanceId, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        StringBuilder forwardedArgs = new StringBuilder(256);
        forwardedArgs.Append('{');
        forwardedArgs.Append("\"instanceId\":\"").Append(EscapeJsonString(instanceId)).Append('"');
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "anchorAtomType");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "atomType");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "spawnAtomType");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "anchorAtomUid");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "anchorPosition");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "position");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "pos");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "anchorRotationEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "rotationEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "rotateEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "euler");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "anchorScaleFactor");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "anchorScale");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "atomScale");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localPositionOffset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localOffset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "offset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localRotationEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localRotateEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localEuler");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "bindFollow");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "follow");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "followPosition");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "followRotation");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "snapUnderAnchor");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "placeUnderAnchor");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "belowAnchor");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "anchorClearanceMeters");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "verticalOffsetMeters");
        AppendMetaToolkitForwardedTraceFields(forwardedArgs, argsJson);
        forwardedArgs.Append('}');

        return TrySpawnInnerPieceAnchorAtom(HostAnchorSpawnMovementActionId, forwardedArgs.ToString(), out resultJson, out errorMessage);
    }

    private bool TryRunMetaToolkitSetElementFollowAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string instanceId;
        if (!TryResolveMetaToolkitControlSurfaceInstanceId(argsJson, out instanceId, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        StringBuilder forwardedArgs = new StringBuilder(256);
        forwardedArgs.Append('{');
        forwardedArgs.Append("\"instanceId\":\"").Append(EscapeJsonString(instanceId)).Append('"');
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "anchorAtomUid");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "atomUid");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "anchorUid");
        AppendMetaToolkitForwardedStringField(forwardedArgs, argsJson, "uid");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "enabled");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "clear");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "followPosition");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "followRotation");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localPositionOffset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localOffset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "offset");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localRotationEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localRotateEuler");
        AppendMetaToolkitForwardedVectorField(forwardedArgs, argsJson, "localEuler");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "snapUnderAnchor");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "placeUnderAnchor");
        AppendMetaToolkitForwardedBoolField(forwardedArgs, argsJson, "belowAnchor");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "anchorClearanceMeters");
        AppendMetaToolkitForwardedFloatField(forwardedArgs, argsJson, "verticalOffsetMeters");
        AppendMetaToolkitForwardedTraceFields(forwardedArgs, argsJson);
        forwardedArgs.Append('}');

        return TrySetInnerPieceFollowBinding(actionId, forwardedArgs.ToString(), out resultJson, out errorMessage);
    }

    private bool TryFindMetaToolkitDefaultThemeSurfaceDefinition(string key, out MetaToolkitDemoSurfaceDefinition definition)
    {
        definition = null;
        string selector = string.IsNullOrEmpty(key) ? "" : key.Trim();
        if (string.IsNullOrEmpty(selector))
            return false;

        for (int i = 0; i < MetaToolkitDefaultThemeDemoSurfaces.Length; i++)
        {
            MetaToolkitDemoSurfaceDefinition candidate = MetaToolkitDefaultThemeDemoSurfaces[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.key, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.packageFolderName, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.instanceId, selector, StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    private List<ShortCut> BuildPlayerMediaBrowserShortCuts(string suggestedFolder)
    {
        List<ShortCut> list = new List<ShortCut>();

        AddPlayerMediaBrowserShortCutIfMissing(list, suggestedFolder, true);
        for (int i = 0; i < PlayerMediaBrowserSeedPathCandidates.Length; i++)
            AddPlayerMediaBrowserShortCutIfMissing(list, PlayerMediaBrowserSeedPathCandidates[i]);
        AddPlayerMediaBrowserShortCutIfMissing(list, "Custom\\Videos");

        return list;
    }

    private string NormalizePlayerMediaBrowserDialogPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        return path.Replace('\\', '/').Trim();
    }

    private void AddPlayerMediaBrowserShortCutIfMissing(List<ShortCut> list, string path, bool insertFirst = false)
    {
        if (list == null || string.IsNullOrEmpty(path))
            return;

        string normalizedPath = NormalizeStandalonePlayerPath(path);
        if (string.IsNullOrEmpty(normalizedPath) || !FileManagerSecure.DirectoryExists(normalizedPath, false))
            return;
        string dialogPath = NormalizePlayerMediaBrowserDialogPath(normalizedPath);
        if (string.IsNullOrEmpty(dialogPath))
            return;

        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].path, dialogPath, StringComparison.OrdinalIgnoreCase))
                return;
        }

        ShortCut shortCut = new ShortCut();
        shortCut.path = dialogPath;
        shortCut.displayName = dialogPath;
        shortCut.flatten = false;
        shortCut.includeRegularDirsInFlatten = true;
        if (insertFirst)
            list.Insert(0, shortCut);
        else
            list.Add(shortCut);
    }

    private string BuildMetaToolkitLocalPackageDefaultInstanceId(string packageFolderName, string controlSurfaceId)
    {
        string raw = !string.IsNullOrEmpty(packageFolderName) ? packageFolderName : controlSurfaceId;
        if (string.IsNullOrEmpty(raw))
            raw = "meta_toolkit_local";

        string sanitized = Regex.Replace(raw, "[^A-Za-z0-9_]+", "_");
        sanitized = sanitized.Trim('_');
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "meta_toolkit_local";

        return "meta_toolkit_" + sanitized.ToLowerInvariant();
    }

    private bool TryReadMetaToolkitLocalPackageDefinition(string packagePath, out MetaToolkitLocalPackageDefinition definition)
    {
        definition = null;
        string normalizedPackagePath = string.IsNullOrEmpty(packagePath) ? "" : packagePath.Trim();
        if (string.IsNullOrEmpty(normalizedPackagePath) || !FileManagerSecure.DirectoryExists(normalizedPackagePath, false))
            return false;

        string folderName = FileManagerSecure.GetFileName(normalizedPackagePath) ?? "";
        string[] controlsFiles = FileManagerSecure.GetFiles(normalizedPackagePath, "controls.innerpiece.json");
        string controlsPath = controlsFiles != null && controlsFiles.Length > 0 ? controlsFiles[0] : "";

        FAInnerPieceControlSurfaceData controlSurface = null;
        if (!string.IsNullOrEmpty(controlsPath) && FileManagerSecure.FileExists(controlsPath, false))
        {
            string controlsJson = FileManagerSecure.ReadAllText(controlsPath);
            if (!string.IsNullOrEmpty(controlsJson))
                controlSurface = FAInnerPiecePackageSupport.DeserializeControlSurface(controlsJson);
        }

        definition = new MetaToolkitLocalPackageDefinition();
        definition.packageFolderName = folderName;
        definition.packagePath = normalizedPackagePath;
        definition.controlSurfaceId = controlSurface != null ? (controlSurface.controlSurfaceId ?? "") : "";
        definition.controlFamilyId = controlSurface != null ? (controlSurface.controlFamilyId ?? "") : "";
        definition.toolkitCategory = controlSurface != null ? (controlSurface.toolkitCategory ?? "") : "";
        definition.surfaceWidthMeters = controlSurface != null ? controlSurface.surfaceWidthMeters : 0f;
        definition.surfaceHeightMeters = controlSurface != null ? controlSurface.surfaceHeightMeters : 0f;
        definition.elementCount = controlSurface != null && controlSurface.elements != null ? controlSurface.elements.Length : 0;
        definition.bindToPlayer = string.Equals(definition.controlFamilyId, "meta_ui_video_player", StringComparison.OrdinalIgnoreCase);
        definition.defaultInstanceId = BuildMetaToolkitLocalPackageDefaultInstanceId(folderName, definition.controlSurfaceId);

        MetaToolkitDemoSurfaceDefinition curatedDefinition;
        if (TryFindMetaToolkitDefaultThemeSurfaceDefinition(folderName, out curatedDefinition) && curatedDefinition != null)
        {
            definition.key = curatedDefinition.key ?? folderName;
            definition.bindToPlayer = curatedDefinition.bindToPlayer;
            definition.curated = true;
            if (string.IsNullOrEmpty(definition.controlFamilyId))
                definition.controlFamilyId = curatedDefinition.controlFamilyId ?? "";
            if (string.IsNullOrEmpty(definition.defaultInstanceId))
                definition.defaultInstanceId = curatedDefinition.instanceId ?? "";
        }
        else
        {
            definition.key = !string.IsNullOrEmpty(definition.controlSurfaceId) ? definition.controlSurfaceId : folderName;
        }

        return true;
    }

    private bool TryResolveMetaToolkitLocalPackageDefinition(string argsJson, out MetaToolkitLocalPackageDefinition definition, out string errorMessage)
    {
        definition = null;
        errorMessage = "";

        string explicitPackagePath = ExtractJsonArgString(argsJson, "packagePath");
        if (!string.IsNullOrEmpty(explicitPackagePath))
        {
            if (TryReadMetaToolkitLocalPackageDefinition(explicitPackagePath, out definition))
                return true;

            errorMessage = "meta toolkit packagePath not found";
            return false;
        }

        string rootPath = ExtractJsonArgString(argsJson, "rootPath", "themeRootPath", "packageRootPath");
        if (string.IsNullOrEmpty(rootPath))
            rootPath = ResolveMetaToolkitDefaultThemeLocalRootPath();

        string packageFolderName = ExtractJsonArgString(argsJson, "packageFolderName", "packageId", "folderName");
        if (string.IsNullOrEmpty(packageFolderName))
            packageFolderName = ExtractJsonArgString(argsJson, "elementKey", "key", "surfaceKey", "toolkitKey");

        if (string.IsNullOrEmpty(packageFolderName))
        {
            errorMessage = "packageFolderName, packageId, packagePath, or elementKey is required";
            return false;
        }

        string packagePath = BuildMetaToolkitPackagePath(rootPath, packageFolderName);
        if (TryReadMetaToolkitLocalPackageDefinition(packagePath, out definition))
            return true;

        errorMessage = "meta toolkit package not found";
        return false;
    }

    private bool TryResolveMetaToolkitControlSurfaceInstanceId(string argsJson, out string instanceId, out string errorMessage)
    {
        instanceId = ExtractJsonArgString(argsJson, "controlSurfaceInstanceId", "instanceId");
        errorMessage = "";

        if (!string.IsNullOrEmpty(instanceId))
            return true;

        string key = ExtractJsonArgString(argsJson, "elementKey", "key", "surfaceKey", "toolkitKey");
        MetaToolkitDemoSurfaceDefinition definition;
        if (TryFindMetaToolkitDefaultThemeSurfaceDefinition(key, out definition) && definition != null)
        {
            instanceId = definition.instanceId ?? "";
            return !string.IsNullOrEmpty(instanceId);
        }

        errorMessage = "controlSurfaceInstanceId or elementKey is required";
        return false;
    }

    private string BuildMetaToolkitDefaultThemeElementCatalogJson()
    {
        StringBuilder payload = new StringBuilder(1024);
        payload.Append('{');
        payload.Append("\"rootPath\":\"").Append(EscapeJsonString(ResolveMetaToolkitDefaultThemeLocalRootPath())).Append("\",");
        payload.Append("\"elements\":[");
        for (int i = 0; i < MetaToolkitDefaultThemeDemoSurfaces.Length; i++)
        {
            MetaToolkitDemoSurfaceDefinition definition = MetaToolkitDefaultThemeDemoSurfaces[i];
            if (definition == null)
                continue;

            if (i > 0)
                payload.Append(',');

            payload.Append('{');
            payload.Append("\"key\":\"").Append(EscapeJsonString(definition.key)).Append("\",");
            payload.Append("\"controlFamilyId\":\"").Append(EscapeJsonString(definition.controlFamilyId)).Append("\",");
            payload.Append("\"packageFolderName\":\"").Append(EscapeJsonString(definition.packageFolderName)).Append("\",");
            payload.Append("\"packagePath\":\"").Append(EscapeJsonString(BuildMetaToolkitDemoPackagePath(definition.packageFolderName))).Append("\",");
            payload.Append("\"defaultInstanceId\":\"").Append(EscapeJsonString(definition.instanceId)).Append("\",");
            payload.Append("\"defaultBindToPlayer\":").Append(definition.bindToPlayer ? "true" : "false").Append(',');
            payload.Append("\"defaultUniformScale\":").Append(FormatFloat(definition.uniformScale)).Append(',');
            payload.Append("\"capabilities\":{");
            payload.Append("\"spawn\":true,");
            payload.Append("\"transform\":true,");
            payload.Append("\"tween\":true,");
            payload.Append("\"cancelTween\":true,");
            payload.Append("\"state\":true,");
            payload.Append("\"observeMotion\":true,");
            payload.Append("\"stableOperationId\":true,");
            payload.Append("\"anchor\":true,");
            payload.Append("\"follow\":true,");
            payload.Append("\"localInteraction\":true,");
            payload.Append("\"playerBinding\":").Append(definition.bindToPlayer ? "true" : "false");
            payload.Append("},");
            payload.Append("\"defaultOffsets\":{");
            payload.Append("\"right\":").Append(FormatFloat(definition.rightOffsetMeters)).Append(',');
            payload.Append("\"up\":").Append(FormatFloat(definition.upOffsetMeters)).Append(',');
            payload.Append("\"forward\":").Append(FormatFloat(definition.forwardOffsetMeters));
            payload.Append("}}");
        }
        payload.Append("]}");
        return payload.ToString();
    }

    private string BuildMetaToolkitLocalThemePackageCatalogJson(string rootPath)
    {
        string catalogRootPath = string.IsNullOrEmpty(rootPath) ? ResolveMetaToolkitDefaultThemeLocalRootPath() : rootPath.Trim();
        StringBuilder payload = new StringBuilder(2048);
        payload.Append('{');
        payload.Append("\"rootPath\":\"").Append(EscapeJsonString(catalogRootPath)).Append("\",");
        payload.Append("\"packages\":[");

        string[] packageDirectories = FileManagerSecure.DirectoryExists(catalogRootPath, false)
            ? FileManagerSecure.GetDirectories(catalogRootPath, "*")
            : new string[0];

        int emitted = 0;
        for (int i = 0; i < packageDirectories.Length; i++)
        {
            MetaToolkitLocalPackageDefinition definition;
            if (!TryReadMetaToolkitLocalPackageDefinition(packageDirectories[i], out definition) || definition == null)
                continue;

            if (emitted > 0)
                payload.Append(',');
            emitted++;

            payload.Append('{');
            payload.Append("\"key\":\"").Append(EscapeJsonString(definition.key ?? "")).Append("\",");
            payload.Append("\"packageFolderName\":\"").Append(EscapeJsonString(definition.packageFolderName ?? "")).Append("\",");
            payload.Append("\"packagePath\":\"").Append(EscapeJsonString(definition.packagePath ?? "")).Append("\",");
            payload.Append("\"controlSurfaceId\":\"").Append(EscapeJsonString(definition.controlSurfaceId ?? "")).Append("\",");
            payload.Append("\"controlFamilyId\":\"").Append(EscapeJsonString(definition.controlFamilyId ?? "")).Append("\",");
            payload.Append("\"toolkitCategory\":\"").Append(EscapeJsonString(definition.toolkitCategory ?? "")).Append("\",");
            payload.Append("\"defaultInstanceId\":\"").Append(EscapeJsonString(definition.defaultInstanceId ?? "")).Append("\",");
            payload.Append("\"surfaceWidthMeters\":").Append(FormatFloat(definition.surfaceWidthMeters)).Append(',');
            payload.Append("\"surfaceHeightMeters\":").Append(FormatFloat(definition.surfaceHeightMeters)).Append(',');
            payload.Append("\"elementCount\":").Append(definition.elementCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            payload.Append("\"curated\":").Append(definition.curated ? "true" : "false").Append(',');
            payload.Append("\"capabilities\":{");
            payload.Append("\"spawn\":true,");
            payload.Append("\"transform\":true,");
            payload.Append("\"tween\":true,");
            payload.Append("\"cancelTween\":true,");
            payload.Append("\"state\":true,");
            payload.Append("\"observeMotion\":true,");
            payload.Append("\"stableOperationId\":true,");
            payload.Append("\"anchor\":true,");
            payload.Append("\"follow\":true,");
            payload.Append("\"localInteraction\":true,");
            payload.Append("\"playerBinding\":").Append(definition.bindToPlayer ? "true" : "false");
            payload.Append("}}");
        }

        payload.Append("],\"packageCount\":").Append(emitted.ToString(CultureInfo.InvariantCulture)).Append('}');
        return payload.ToString();
    }

    private void AppendMetaToolkitForwardedTraceFields(StringBuilder sb, string argsJson)
    {
        string traceFieldsJson = BuildMetaToolkitDemoTraceFieldsJson(argsJson);
        if (!string.IsNullOrEmpty(traceFieldsJson))
            sb.Append(traceFieldsJson);
    }

    private void AppendMetaToolkitForwardedStringField(StringBuilder sb, string argsJson, string key)
    {
        string value = ExtractJsonArgString(argsJson, key);
        if (!string.IsNullOrEmpty(value))
            sb.Append(",\"").Append(EscapeJsonString(key)).Append("\":\"").Append(EscapeJsonString(value)).Append('"');
    }

    private void AppendMetaToolkitForwardedFloatField(StringBuilder sb, string argsJson, string key)
    {
        float value;
        if (TryExtractJsonFloatField(argsJson, key, out value))
            sb.Append(",\"").Append(EscapeJsonString(key)).Append("\":").Append(FormatFloat(value));
    }

    private void AppendMetaToolkitForwardedBoolField(StringBuilder sb, string argsJson, string key)
    {
        bool value;
        if (TryExtractJsonBoolField(argsJson, key, out value))
            sb.Append(",\"").Append(EscapeJsonString(key)).Append("\":").Append(value ? "true" : "false");
    }

    private void AppendMetaToolkitForwardedVectorField(StringBuilder sb, string argsJson, string key)
    {
        Vector3 value;
        if (TryReadVectorArg(argsJson, key, out value))
            sb.Append(",\"").Append(EscapeJsonString(key)).Append("\":").Append(BuildJsonVector3(value));
    }

    private void AppendMetaToolkitForwardedTransformFields(StringBuilder sb, string argsJson, bool includeTweenFields)
    {
        string transformJson;
        if (TryExtractJsonObjectField(argsJson, "transform", out transformJson) && !string.IsNullOrEmpty(transformJson))
            sb.Append(",\"transform\":").Append(transformJson);

        Vector3 value;
        if (TryReadVectorArg(argsJson, "position", out value))
            sb.Append(",\"position\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "pos", out value))
            sb.Append(",\"pos\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "translate", out value))
            sb.Append(",\"translate\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "positionDelta", out value))
            sb.Append(",\"positionDelta\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "rotateEuler", out value))
            sb.Append(",\"rotateEuler\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "euler", out value))
            sb.Append(",\"euler\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "scaleMultiplier", out value))
            sb.Append(",\"scaleMultiplier\":").Append(BuildJsonVector3(value));
        if (TryReadVectorArg(argsJson, "scale", out value) && !includeTweenFields)
            sb.Append(",\"scale\":").Append(BuildJsonVector3(value));

        Quaternion rotation;
        if (TryReadQuaternionComponents(argsJson, out rotation))
            sb.Append(",\"rotation\":").Append(BuildJsonQuaternion(rotation));

        float uniformScaleMultiplier;
        if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out uniformScaleMultiplier))
            sb.Append(",\"uniformScaleMultiplier\":").Append(FormatFloat(uniformScaleMultiplier));
    }

    private void AppendMetaToolkitForwardedTweenFields(StringBuilder sb, string argsJson, SyncObjectRecord rootRecord)
    {
        Vector3 value;
        if (TryReadVectorArg(argsJson, "position", out value) || TryReadVectorArg(argsJson, "pos", out value))
            sb.Append(",\"pos\":").Append(BuildJsonVector3(value));

        Quaternion rotation;
        if (TryReadQuaternionComponents(argsJson, out rotation))
            sb.Append(",\"rotation\":").Append(BuildJsonQuaternion(rotation));
        else
        {
            Vector3 rotateEuler;
            if (TryReadVectorArg(argsJson, "rotateEuler", out rotateEuler) || TryReadVectorArg(argsJson, "euler", out rotateEuler))
                sb.Append(",\"rotation\":").Append(BuildJsonQuaternion(Quaternion.Euler(rotateEuler)));
        }

        Vector3 absoluteScale;
        if (TryReadVectorArg(argsJson, "scale", out absoluteScale))
        {
            sb.Append(",\"scale\":").Append(BuildJsonVector3(absoluteScale));
        }
        else
        {
            Vector3 scaleMultiplier;
            if (TryReadVectorArg(argsJson, "scaleMultiplier", out scaleMultiplier))
                sb.Append(",\"scale\":").Append(BuildJsonVector3(Vector3.Scale(rootRecord != null ? rootRecord.scale : Vector3.one, scaleMultiplier)));
            else
            {
                float uniformScaleMultiplier;
                if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out uniformScaleMultiplier))
                    sb.Append(",\"scale\":").Append(BuildJsonVector3((rootRecord != null ? rootRecord.scale : Vector3.one) * uniformScaleMultiplier));
            }
        }

        float durationSeconds;
        if (TryReadDurationSeconds(argsJson, out durationSeconds))
            sb.Append(",\"durationSeconds\":").Append(FormatFloat(durationSeconds));
    }

    private bool TryBuildMetaToolkitDemoTargetPlane(string targetInstanceId, out FAInnerPiecePlaneData plane, out string errorMessage)
    {
        plane = new FAInnerPiecePlaneData();
        errorMessage = "";

        InnerPieceInstanceRecord targetInstance;
        if (!TryResolveInnerPieceInstance(
                "{\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\"}",
                out targetInstance,
                out errorMessage) || targetInstance == null)
            return false;

        if (TryBuildInnerPieceGrabHandlePlaneData(targetInstance, out plane))
            return true;

        return TryResolveInnerPieceScreenPlane(targetInstanceId, InnerPiecePrimaryPlayerDisplayId, out plane, out errorMessage);
    }

    private bool TryImportMetaToolkitDemoPackage(string packageFolderName, string traceFieldsJson, out string resourceId, out string errorMessage)
    {
        string resolvedPackagePath;
        if (!TryResolveMetaToolkitDemoPackagePath(packageFolderName, out resolvedPackagePath))
        {
            resourceId = "";
            errorMessage = "meta toolkit package not found";
            return false;
        }

        return TryImportMetaToolkitPackage(resolvedPackagePath, traceFieldsJson, out resourceId, out errorMessage);
    }

    private bool TryImportMetaToolkitPackage(string packagePath, string traceFieldsJson, out string resourceId, out string errorMessage)
    {
        resourceId = "";
        errorMessage = "";

        string normalizedPackagePath = string.IsNullOrEmpty(packagePath) ? "" : packagePath.Trim();
        if (string.IsNullOrEmpty(normalizedPackagePath))
        {
            errorMessage = "meta toolkit packagePath is required";
            return false;
        }

        string importArgsJson = "{"
            + "\"packagePath\":\"" + EscapeJsonString(normalizedPackagePath) + "\""
            + ",\"tagsCsv\":\"meta,toolkit,demo,theme_00\""
            + traceFieldsJson
            + "}";
        string importResultJson;
        if (!TryExecuteMetaToolkitDemoAction(HostPackageImportActionId, importArgsJson, out importResultJson, out errorMessage))
            return false;

        resourceId = ExtractJsonArgString(importResultJson, "resourceId");
        if (string.IsNullOrEmpty(resourceId))
        {
            errorMessage = "meta toolkit demo import missing resourceId";
            return false;
        }

        return true;
    }

    private bool TryExecuteMetaToolkitDemoAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        try
        {
            return TryExecuteAction(actionId, argsJson, out resultJson, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = "meta toolkit demo action " + actionId + " exception: " + ex.Message;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }
    }

    private void DeleteInnerPieceInstanceIfPresent(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        InnerPieceInstanceRecord instance;
        string errorMessage;
        if (!TryResolveInnerPieceInstance(
                "{\"instanceId\":\"" + EscapeJsonString(instanceId) + "\"}",
                out instance,
                out errorMessage) || instance == null)
            return;

        DeleteInnerPieceInstanceInternal(instance, true);
    }

    private string BuildMetaToolkitDemoPackagePath(string packageFolderName)
    {
        string folder = string.IsNullOrEmpty(packageFolderName) ? "" : packageFolderName.Trim();
        if (string.IsNullOrEmpty(folder))
            return ResolveMetaToolkitDefaultThemeLocalRootPath();

        return BuildMetaToolkitPackagePath(ResolveMetaToolkitDefaultThemeLocalRootPath(), folder);
    }

    private bool TryResolveMetaToolkitDemoPackagePath(string packageFolderName, out string packagePath)
    {
        packagePath = BuildMetaToolkitDemoPackagePath(packageFolderName);
        if (FileManagerSecure.DirectoryExists(packagePath, false))
            return true;

        MetaToolkitDemoSurfaceDefinition curatedDefinition;
        if (!TryFindMetaToolkitDefaultThemeSurfaceDefinition(packageFolderName, out curatedDefinition) || curatedDefinition == null)
            return false;

        string rootPath = ResolveMetaToolkitDefaultThemeLocalRootPath();
        if (!FileManagerSecure.DirectoryExists(rootPath, false))
            return false;

        string[] packageDirectories = FileManagerSecure.GetDirectories(rootPath, "*");
        if (packageDirectories == null || packageDirectories.Length <= 0)
            return false;

        for (int i = 0; i < packageDirectories.Length; i++)
        {
            MetaToolkitLocalPackageDefinition localDefinition;
            if (!TryReadMetaToolkitLocalPackageDefinition(packageDirectories[i], out localDefinition) || localDefinition == null)
                continue;

            if (!string.IsNullOrEmpty(curatedDefinition.controlFamilyId)
                && !string.Equals(localDefinition.controlFamilyId, curatedDefinition.controlFamilyId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (curatedDefinition.bindToPlayer && !localDefinition.bindToPlayer)
                continue;

            packagePath = localDefinition.packagePath ?? "";
            if (!string.IsNullOrEmpty(packagePath))
                return true;
        }

        return false;
    }

    private string BuildMetaToolkitPackagePath(string rootPath, string packageFolderName)
    {
        string normalizedRoot = string.IsNullOrEmpty(rootPath) ? ResolveMetaToolkitDefaultThemeLocalRootPath() : rootPath.Trim();
        string folder = string.IsNullOrEmpty(packageFolderName) ? "" : packageFolderName.Trim();
        if (string.IsNullOrEmpty(folder))
            return normalizedRoot;

        return normalizedRoot + "\\" + folder;
    }


    private string ResolveMetaToolkitDefaultThemeLocalRootPath()
    {
        return ResolvePreferredDirectory(
            MetaToolkitDemoDefaultThemeLocalRootPath,
            LegacyMetaToolkitDemoDefaultThemeLocalRootPath,
            MetaToolkitDemoAbsoluteThemeRootPath);
    }

    private string ResolvePreferredDirectory(params string[] candidatePaths)
    {
        string firstSecureNonEmpty = "";
        if (candidatePaths == null || candidatePaths.Length <= 0)
            return firstSecureNonEmpty;

        for (int i = 0; i < candidatePaths.Length; i++)
        {
            string candidatePath = string.IsNullOrEmpty(candidatePaths[i]) ? "" : candidatePaths[i].Trim();
            if (string.IsNullOrEmpty(candidatePath))
                continue;

            if (!IsSecureRuntimePathCandidate(candidatePath))
                continue;

            if (string.IsNullOrEmpty(firstSecureNonEmpty))
                firstSecureNonEmpty = candidatePath;

            try
            {
                if (FileManagerSecure.DirectoryExists(candidatePath, false))
                    return candidatePath;
            }
            catch
            {
            }
        }

        return firstSecureNonEmpty;
    }

    private static bool IsSecureRuntimePathCandidate(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string candidate = path.Trim();
        if (candidate.Length >= 2 && char.IsLetter(candidate[0]) && candidate[1] == ':')
            return false;

        return !candidate.StartsWith("\\\\", StringComparison.Ordinal);
    }

    private string BuildMetaToolkitDemoTraceFieldsJson(string argsJson)
    {
        StringBuilder sb = new StringBuilder(96);
        string correlationId = ExtractJsonArgString(argsJson, "correlationId");
        string messageId = ExtractJsonArgString(argsJson, "messageId");
        if (!string.IsNullOrEmpty(correlationId))
            sb.Append(",\"correlationId\":\"").Append(EscapeJsonString(correlationId)).Append('"');
        if (!string.IsNullOrEmpty(messageId))
            sb.Append(",\"messageId\":\"").Append(EscapeJsonString(messageId)).Append('"');
        return sb.ToString();
    }

    private string BuildJsonVector3(Vector3 value)
    {
        return "{"
            + "\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + ",\"z\":" + FormatFloat(value.z)
            + "}";
    }

    private string BuildJsonQuaternion(Quaternion value)
    {
        return "{"
            + "\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + ",\"z\":" + FormatFloat(value.z)
            + ",\"w\":" + FormatFloat(value.w)
            + "}";
    }

    private MVRPluginManager FindPluginManager(Atom atom)
    {
        if (atom == null)
            return null;

        List<string> ids = null;
        try
        {
            ids = atom.GetStorableIDs();
        }
        catch
        {
            ids = null;
        }

        if (ids == null)
            return null;

        for (int i = 0; i < ids.Count; i++)
        {
            JSONStorable storable = null;
            try
            {
                storable = atom.GetStorableByID(ids[i]);
            }
            catch
            {
                storable = null;
            }

            MVRPluginManager manager = storable as MVRPluginManager;
            if (manager != null)
                return manager;
        }

        return null;
    }

    private bool TryExecuteAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

#if !FRAMEANGEL_CUA_PLAYER
        if (TryExecuteSceneOnlyAction(actionId, argsJson, out resultJson, out errorMessage))
            return true;
#endif

        bool handledPlayerAction;
        if (TryExecutePlayerAction(actionId, argsJson, out resultJson, out errorMessage, out handledPlayerAction))
            return true;
        if (handledPlayerAction)
            return false;

        errorMessage = "action not supported";
        resultJson = BuildBrokerResult(false, errorMessage, "{}");
        return false;
    }

    private bool TryCreateOrUpdatePrimitive(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string kind = ExtractJsonArgString(argsJson, "kind", "resourceType", "type");
        if (string.IsNullOrEmpty(kind))
            kind = "surface";
        kind = kind.Trim().ToLowerInvariant();

        string objectId = ExtractJsonArgString(argsJson, "objectId", "id");
        if (string.IsNullOrEmpty(objectId))
        {
            errorMessage = "objectId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool created = !syncObjects.ContainsKey(objectId);
        SyncObjectRecord record = GetOrCreateObjectRecord(objectId, kind, kind == "surface" ? "surface" : kind, false);
        if (!EnsurePrimitive(record, kind))
        {
            errorMessage = "primitive creation failed";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        ApplyRecordFromArgs(record, kind, argsJson);
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, created ? "scene_object_created" : "scene_object_updated", payload);
        EmitRuntimeEvent(
            created ? "scene_object_created" : "scene_object_updated",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            ""
        );
        return true;
    }

    private bool TrySetActiveObject(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string requestedObjectId = ExtractJsonArgString(argsJson, "objectId", "id");
        string correlationId = ExtractJsonArgString(argsJson, "correlationId");
        string commandMessageId = ExtractJsonArgString(argsJson, "messageId");

        if (string.IsNullOrEmpty(requestedObjectId))
        {
            string previousActiveObjectId = activeObjectId;
            if (IsManipulationActive())
                CancelManipulation("selection_cleared");

            activeObjectId = "";
            activeCorrelationId = "";
            activeMessageId = "";
            RefreshRecordVisuals(previousActiveObjectId);

            resultJson = BuildBrokerResult(true, "scene_object_selection_cleared", BuildSelectionPayload());
            EmitRuntimeEvent(
                "scene_object_selected",
                actionId,
                "ok",
                "",
                "selection_cleared",
                "",
                correlationId,
                commandMessageId,
                ""
            );
            return true;
        }

        SyncObjectRecord requestedRecord;
        if (!syncObjects.TryGetValue(requestedObjectId, out requestedRecord) || requestedRecord == null || requestedRecord.internalMarker)
        {
            errorMessage = "object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string previousObjectId = activeObjectId;
        if (IsManipulationActive() && !IsManipulatingObject(requestedObjectId))
            CancelManipulation("selection_changed");

        activeObjectId = requestedObjectId;
        activeCorrelationId = correlationId;
        activeMessageId = commandMessageId;

        RefreshRecordVisuals(previousObjectId);
        RefreshRecordVisuals(activeObjectId);

        resultJson = BuildBrokerResult(true, "scene_object_selected", BuildSelectionPayload());
        EmitRuntimeEvent(
            "scene_object_selected",
            actionId,
            "ok",
            "",
            activeObjectId,
            activeObjectId,
            activeCorrelationId,
            activeMessageId,
            ""
        );
        return true;
    }

    private bool TryTransformObject(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string objectId = ExtractJsonArgString(argsJson, "objectId", "id");
        if (string.IsNullOrEmpty(objectId))
        {
            errorMessage = "objectId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord record;
        if (!syncObjects.TryGetValue(objectId, out record) || record == null)
        {
            errorMessage = "object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        ApplyRecordFromArgs(record, record.kind, argsJson);
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_updated", payload);
        EmitRuntimeEvent(
            "scene_object_updated",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            ""
        );
        return true;
    }

    private bool TryTransformActiveObject(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string explicitObjectId = ExtractJsonArgString(argsJson, "objectId", "id");
        if (!string.IsNullOrEmpty(explicitObjectId))
        {
            errorMessage = "objectId is not supported for TransformActiveObject";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (string.IsNullOrEmpty(activeObjectId))
        {
            errorMessage = "active object not set";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord activeRecord;
        if (!syncObjects.TryGetValue(activeObjectId, out activeRecord) || activeRecord == null || activeRecord.internalMarker)
        {
            errorMessage = "active object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        return TryTransformObject(
            actionId,
            AppendJsonStringField(argsJson, "objectId", activeObjectId),
            out resultJson,
            out errorMessage
        );
    }

    private bool TryDeleteObject(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string objectId = ExtractJsonArgString(argsJson, "objectId", "id");
        if (string.IsNullOrEmpty(objectId))
        {
            errorMessage = "objectId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord record;
        if (!syncObjects.TryGetValue(objectId, out record) || record == null)
        {
            errorMessage = "object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        InnerPieceInstanceRecord innerPieceInstance;
        if (TryGetInnerPieceInstanceByRootObjectId(objectId, out innerPieceInstance))
            DeleteInnerPieceInstanceInternal(innerPieceInstance, false);

        bool wasActiveObject = string.Equals(activeObjectId, objectId, StringComparison.OrdinalIgnoreCase);
        if (wasActiveObject)
            CancelManipulation("object_deleted");

        DestroyRecord(record);
        syncObjects.Remove(objectId);
        if (wasActiveObject)
        {
            activeObjectId = "";
            activeCorrelationId = "";
            activeMessageId = "";
        }
        RemoveObjectFromAllGroups(objectId);

        resultJson = BuildBrokerResult(true, "scene_object_deleted", BuildSimpleStatePayload("objectId", objectId));
        EmitRuntimeEvent(
            "scene_object_deleted",
            actionId,
            "ok",
            "",
            objectId,
            objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            ""
        );
        return true;
    }

    private bool TryTweenTransform(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float durationSeconds;
        if (!TryReadDurationSeconds(argsJson, out durationSeconds))
            durationSeconds = DefaultTweenDurationSeconds;
        durationSeconds = Mathf.Max(0.01f, durationSeconds);

        Vector3 targetPosition = record.position;
        Quaternion targetRotation = record.rotation;
        Vector3 targetScale = record.scale;

        Vector3 position;
        if (TryReadVectorComponents(argsJson, "pos", out position))
            targetPosition = position;

        Quaternion rotation;
        if (TryReadQuaternionComponents(argsJson, out rotation))
            targetRotation = rotation;

        Vector3 scale;
        if (TryReadScaleComponents(argsJson, record.scale.z <= 0f ? 0.10f : record.scale.z, out scale))
            targetScale = scale;

        string operationId = ExtractJsonArgString(argsJson, "operationId", "motionOperationId");
        StopTween(record);
        BeginTweenMotionState(record, operationId, durationSeconds);
        record.tweenCoroutine = StartCoroutine(
            RunTweenTransform(
                record,
                actionId,
                record.motionOperationId,
                targetPosition,
                targetRotation,
                targetScale,
                durationSeconds,
                ExtractJsonArgString(argsJson, "correlationId"),
                ExtractJsonArgString(argsJson, "messageId")
            )
        );

        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_tween_started", payload);
        EmitRuntimeEvent(
            "scene_object_tween_started",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetVisibility(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool visible;
        if (!TryReadBoolArg(argsJson, out visible, "visible", "enabled", "value"))
        {
            errorMessage = "visible is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.visible = visible;
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, visible ? "scene_object_shown" : "scene_object_hidden", payload);
        EmitRuntimeEvent(
            visible ? "scene_object_shown" : "scene_object_hidden",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetScaleFromAnchor(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 newScale;
        if (!TryReadScaleComponents(argsJson, record.scale.z <= 0f ? 0.10f : record.scale.z, out newScale))
        {
            errorMessage = "scale is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string anchor = ExtractJsonArgString(argsJson, "anchor", "anchorFace", "anchorMode");
        if (string.IsNullOrEmpty(anchor))
            anchor = "center";

        Vector3 delta = newScale - record.scale;
        Vector3 right = record.rotation * Vector3.right;
        Vector3 up = record.rotation * Vector3.up;
        Vector3 forward = record.rotation * Vector3.forward;

        switch (anchor.Trim().ToLowerInvariant())
        {
            case "left":
                record.position += right * (delta.x * 0.5f);
                break;
            case "right":
                record.position -= right * (delta.x * 0.5f);
                break;
            case "bottom":
            case "down":
                record.position += up * (delta.y * 0.5f);
                break;
            case "top":
            case "up":
                record.position -= up * (delta.y * 0.5f);
                break;
            case "front":
                record.position -= forward * (delta.z * 0.5f);
                break;
            case "back":
                record.position += forward * (delta.z * 0.5f);
                break;
        }

        record.scale = newScale;
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_scaled_from_anchor", payload);
        EmitRuntimeEvent(
            "scene_object_scaled_from_anchor",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryApplyImpulse(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 impulse;
        if (!TryReadVectorArg(argsJson, "impulse", out impulse) && !TryReadVectorArg(argsJson, "force", out impulse))
        {
            errorMessage = "impulse is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Rigidbody body = EnsureRuntimeBody(record);
        if (body == null)
        {
            errorMessage = "rigidbody unavailable";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        body.AddForce(impulse, ForceMode.Impulse);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_impulse_applied", payload);
        EmitRuntimeEvent(
            "scene_object_impulse_applied",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetAngularVelocity(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 angularVelocity;
        if (!TryReadVectorArg(argsJson, "angularVelocity", out angularVelocity))
        {
            errorMessage = "angularVelocity is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Rigidbody body = EnsureRuntimeBody(record);
        if (body == null)
        {
            errorMessage = "rigidbody unavailable";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        body.angularVelocity = angularVelocity;
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_angular_velocity_set", payload);
        EmitRuntimeEvent(
            "scene_object_angular_velocity_set",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryCloneObject(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string sourceObjectId = ExtractJsonArgString(argsJson, "sourceObjectId", "sourceId");
        string objectId = ExtractJsonArgString(argsJson, "objectId", "newObjectId", "id");
        if (string.IsNullOrEmpty(sourceObjectId) || string.IsNullOrEmpty(objectId))
        {
            errorMessage = "sourceObjectId and objectId are required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord sourceRecord;
        if (!syncObjects.TryGetValue(sourceObjectId, out sourceRecord) || sourceRecord == null || sourceRecord.internalMarker)
        {
            errorMessage = "source object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if (IsInnerPieceRootObject(sourceRecord))
        {
            errorMessage = "innerpiece clone not supported";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord clone = GetOrCreateObjectRecord(objectId, sourceRecord.kind, sourceRecord.resourceType, false);
        if (!EnsurePrimitive(clone, clone.kind))
        {
            errorMessage = "primitive creation failed";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        clone.position = sourceRecord.position;
        clone.rotation = sourceRecord.rotation;
        clone.scale = sourceRecord.scale;
        clone.color = sourceRecord.color;
        clone.visible = sourceRecord.visible;
        clone.materialMode = sourceRecord.materialMode;
        clone.tagsCsv = sourceRecord.tagsCsv;
        clone.parentGroupId = sourceRecord.parentGroupId;
        ApplyPositionRotationScaleFromArgs(clone, argsJson, clone.scale.z <= 0f ? 0.10f : clone.scale.z);
        ApplyRecordVisuals(clone);
        if (!string.IsNullOrEmpty(clone.parentGroupId))
            EnsureGroupRecord(clone.parentGroupId).memberIds.Add(clone.objectId);

        string payload = BuildSingleObjectPayload(clone);
        resultJson = BuildBrokerResult(true, "scene_object_cloned", payload);
        EmitRuntimeEvent(
            "scene_object_cloned",
            actionId,
            "ok",
            "",
            clone.objectId,
            clone.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetColor(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Color color;
        if (!TryReadColorArg(argsJson, out color))
        {
            errorMessage = "color is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.color = color;
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_color_set", payload);
        EmitRuntimeEvent(
            "scene_object_color_set",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TrySetMaterialMode(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncObjectRecord record;
        if (!TryResolveSceneObject(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string mode = ExtractJsonArgString(argsJson, "mode", "materialMode", "value");
        if (string.IsNullOrEmpty(mode))
            mode = "opaque";

        record.materialMode = mode.Trim().ToLowerInvariant();
        ApplyRecordVisuals(record);
        string payload = BuildSingleObjectPayload(record);
        resultJson = BuildBrokerResult(true, "scene_object_material_mode_set", payload);
        EmitRuntimeEvent(
            "scene_object_material_mode_set",
            actionId,
            "ok",
            "",
            record.objectId,
            record.objectId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryCreateGroup(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        string groupId = ExtractJsonArgString(argsJson, "groupId", "id");
        if (string.IsNullOrEmpty(groupId))
        {
            errorMessage = "groupId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncGroupRecord group = EnsureGroupRecord(groupId);
        string tagsCsv = ExtractJsonArgString(argsJson, "tagsCsv", "tags");
        if (!string.IsNullOrEmpty(tagsCsv))
            group.tagsCsv = tagsCsv;
        group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        activeGroupId = groupId;

        string payload = BuildGroupStatePayload(group);
        resultJson = BuildBrokerResult(true, "scene_group_created", payload);
        EmitRuntimeEvent(
            "scene_group_created",
            actionId,
            "ok",
            "",
            groupId,
            groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryDeleteGroup(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncGroupRecord group;
        if (!TryResolveGroup(argsJson, out group, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        foreach (string memberId in group.memberIds)
        {
            SyncObjectRecord member;
            if (syncObjects.TryGetValue(memberId, out member) && member != null && string.Equals(member.parentGroupId, group.groupId, StringComparison.OrdinalIgnoreCase))
                member.parentGroupId = "";
        }
        syncGroups.Remove(group.groupId);
        if (string.Equals(activeGroupId, group.groupId, StringComparison.OrdinalIgnoreCase))
            activeGroupId = "";

        resultJson = BuildBrokerResult(true, "scene_group_deleted", "{\"groupId\":\"" + EscapeJsonString(group.groupId) + "\"}");
        EmitRuntimeEvent(
            "scene_group_deleted",
            actionId,
            "ok",
            "",
            group.groupId,
            group.groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            ""
        );
        return true;
    }

    private bool TryAddGroupMembers(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        return TryMutateGroupMembers(actionId, argsJson, true, out resultJson, out errorMessage);
    }

    private bool TryRemoveGroupMembers(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        return TryMutateGroupMembers(actionId, argsJson, false, out resultJson, out errorMessage);
    }

    private bool TryGetGroupState(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncGroupRecord group;
        if (!TryResolveGroup(argsJson, out group, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildGroupStatePayload(group);
        resultJson = BuildBrokerResult(true, "scene_group_state ok", payload);
        EmitRuntimeEvent(
            "scene_group_state",
            actionId,
            "ok",
            "",
            group.groupId,
            group.groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryTransformGroup(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncGroupRecord group;
        if (!TryResolveGroup(argsJson, out group, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        List<SyncObjectRecord> members = GetOrderedGroupMembers(group);
        if (members.Count <= 0)
        {
            errorMessage = "group has no members";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 pivot = ComputeGroupPivot(members);
        Vector3 explicitPivot;
        if (TryReadVectorArg(argsJson, "pivot", out explicitPivot))
            pivot = explicitPivot;

        Vector3 translation = Vector3.zero;
        TryReadVectorArg(argsJson, "translate", out translation);
        if (translation == Vector3.zero)
            TryReadVectorArg(argsJson, "positionDelta", out translation);

        Vector3 rotateEuler = Vector3.zero;
        TryReadVectorArg(argsJson, "rotateEuler", out rotateEuler);
        if (rotateEuler == Vector3.zero)
            TryReadVectorArg(argsJson, "euler", out rotateEuler);
        Quaternion deltaRotation = Quaternion.Euler(rotateEuler);

        Vector3 scaleMultiplier = Vector3.one;
        Vector3 explicitScaleMultiplier;
        if (TryReadVectorArg(argsJson, "scaleMultiplier", out explicitScaleMultiplier))
            scaleMultiplier = explicitScaleMultiplier;
        else
        {
            float uniformScaleMultiplier;
            if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out uniformScaleMultiplier))
                scaleMultiplier = new Vector3(uniformScaleMultiplier, uniformScaleMultiplier, uniformScaleMultiplier);
        }

        for (int i = 0; i < members.Count; i++)
        {
            SyncObjectRecord member = members[i];
            Vector3 relative = member.position - pivot;
            relative = Vector3.Scale(relative, scaleMultiplier);
            relative = deltaRotation * relative;
            member.position = pivot + relative + translation;
            member.rotation = deltaRotation * member.rotation;
            member.scale = Vector3.Scale(member.scale, scaleMultiplier);
            ApplyRecordVisuals(member);
        }

        group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        activeGroupId = group.groupId;
        string payload = BuildGroupStatePayload(group);
        resultJson = BuildBrokerResult(true, "scene_group_transformed", payload);
        EmitRuntimeEvent(
            "scene_group_transformed",
            actionId,
            "ok",
            "",
            group.groupId,
            group.groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryExplodeGroup(
        string actionId,
        string argsJson,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncGroupRecord group;
        if (!TryResolveGroup(argsJson, out group, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        List<SyncObjectRecord> members = GetOrderedGroupMembers(group);
        if (members.Count <= 0)
        {
            errorMessage = "group has no members";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 pivot = ComputeGroupPivot(members);
        Vector3 explicitPivot;
        if (TryReadVectorArg(argsJson, "pivot", out explicitPivot))
            pivot = explicitPivot;

        float distance;
        if (!TryExtractJsonFloatField(argsJson, "distance", out distance)
            && !TryExtractJsonFloatField(argsJson, "offsetDistance", out distance)
            && !TryExtractJsonFloatField(argsJson, "explodeDistance", out distance))
            distance = 0.25f;
        distance = Mathf.Max(0f, distance);

        Vector3 translation = Vector3.zero;
        TryReadVectorArg(argsJson, "translate", out translation);

        Vector3 scaleMultiplier = Vector3.one;
        Vector3 explicitScaleMultiplier;
        if (TryReadVectorArg(argsJson, "scaleMultiplier", out explicitScaleMultiplier))
            scaleMultiplier = explicitScaleMultiplier;
        else
        {
            float uniformScaleMultiplier;
            if (TryExtractJsonFloatField(argsJson, "uniformScaleMultiplier", out uniformScaleMultiplier))
                scaleMultiplier = new Vector3(uniformScaleMultiplier, uniformScaleMultiplier, uniformScaleMultiplier);
        }

        string mode = ExtractJsonArgString(argsJson, "mode", "explodeMode");
        if (string.IsNullOrEmpty(mode))
            mode = "radial";
        mode = mode.Trim().ToLowerInvariant();

        for (int i = 0; i < members.Count; i++)
        {
            SyncObjectRecord member = members[i];
            Vector3 direction = ResolveExplodeDirection(member.position - pivot, i, members.Count, mode);
            member.position += direction * distance + translation;
            member.scale = Vector3.Scale(member.scale, scaleMultiplier);
            ApplyRecordVisuals(member);
        }

        group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        activeGroupId = group.groupId;
        string payload = BuildGroupStatePayload(group);
        resultJson = BuildBrokerResult(true, "scene_group_exploded", payload);
        EmitRuntimeEvent(
            "scene_group_exploded",
            actionId,
            "ok",
            "",
            group.groupId,
            group.groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private static Vector3 ResolveExplodeDirection(Vector3 relative, int index, int count, string mode)
    {
        Vector3 direction = relative;
        switch (mode)
        {
            case "horizontal":
            case "xz":
                direction.y = 0f;
                break;
            case "vertical":
            case "y":
                direction = relative.y >= 0f ? Vector3.up : Vector3.down;
                break;
            case "x":
                direction = relative.x >= 0f ? Vector3.right : Vector3.left;
                break;
            case "z":
            case "depth":
                direction = relative.z >= 0f ? Vector3.forward : Vector3.back;
                break;
        }

        if (direction.sqrMagnitude > 0.000001f)
            return direction.normalized;

        if (count <= 0)
            return Vector3.forward;

        float angle = ((float)index / Mathf.Max(1, count)) * Mathf.PI * 2f;
        if (string.Equals(mode, "vertical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "y", StringComparison.OrdinalIgnoreCase))
            return (index % 2 == 0) ? Vector3.up : Vector3.down;

        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
    }

    private bool TryMutateGroupMembers(
        string actionId,
        string argsJson,
        bool add,
        out string resultJson,
        out string errorMessage
    )
    {
        resultJson = "{}";
        errorMessage = "";

        SyncGroupRecord group;
        if (!TryResolveGroup(argsJson, out group, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        List<string> memberIds = ExtractJsonStringList(argsJson, "memberIds", "members", "objectIds");
        if (memberIds.Count <= 0)
        {
            string singleObjectId = ExtractJsonArgString(argsJson, "objectId", "memberId", "id");
            if (!string.IsNullOrEmpty(singleObjectId))
                memberIds.Add(singleObjectId);
        }
        if (memberIds.Count <= 0)
        {
            errorMessage = "memberIds are required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        for (int i = 0; i < memberIds.Count; i++)
        {
            string memberId = memberIds[i];
            SyncObjectRecord member;
            if (!syncObjects.TryGetValue(memberId, out member) || member == null || member.internalMarker)
                continue;

            if (add)
            {
                group.memberIds.Add(memberId);
                member.parentGroupId = group.groupId;
            }
            else
            {
                group.memberIds.Remove(memberId);
                if (string.Equals(member.parentGroupId, group.groupId, StringComparison.OrdinalIgnoreCase))
                    member.parentGroupId = "";
            }
        }

        group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string payload = BuildGroupStatePayload(group);
        resultJson = BuildBrokerResult(true, add ? "scene_group_members_added" : "scene_group_members_removed", payload);
        EmitRuntimeEvent(
            add ? "scene_group_members_added" : "scene_group_members_removed",
            actionId,
            "ok",
            "",
            group.groupId,
            group.groupId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            "",
            payload
        );
        return true;
    }

    private bool TryFindSceneAtomByUid(string uid, out Atom atom)
    {
        atom = null;
        if (string.IsNullOrEmpty(uid))
            return false;

        List<Atom> atoms = GetLiveSceneAtoms();
        for (int i = 0; i < atoms.Count; i++)
        {
            Atom candidate = atoms[i];
            if (candidate == null)
                continue;
            if (string.Equals(candidate.uid, uid, StringComparison.OrdinalIgnoreCase))
            {
                atom = candidate;
                return true;
            }
        }

        return false;
    }

    private List<Atom> GetLiveSceneAtoms()
    {
        List<Atom> atoms = new List<Atom>();
        SuperController sc = SuperController.singleton;
        if (sc == null)
            return atoms;

        try
        {
            List<Atom> liveAtoms = sc.GetAtoms();
            if (liveAtoms != null)
                atoms.AddRange(liveAtoms);
        }
        catch
        {
        }

        return atoms;
    }

    private SyncObjectRecord UpsertBoxRecord(
        string objectId,
        Vector3 position,
        Vector3 scale,
        Color color,
        string materialMode,
        string status,
        string tagsCsv,
        bool internalMarker
    )
    {
        SyncObjectRecord record = GetOrCreateObjectRecord(objectId, "cube", "cube", internalMarker);
        record.kind = "cube";
        record.resourceType = "cube";
        record.position = position;
        record.rotation = Quaternion.identity;
        record.scale = new Vector3(
            Mathf.Max(MinPrimitiveScale, scale.x),
            Mathf.Max(MinPrimitiveScale, scale.y),
            Mathf.Max(MinPrimitiveScale, scale.z)
        );
        record.color = color;
        record.visible = true;
        record.materialMode = string.IsNullOrEmpty(materialMode) ? "opaque" : materialMode;
        record.status = string.IsNullOrEmpty(status) ? "active" : status;
        record.tagsCsv = string.IsNullOrEmpty(tagsCsv) ? "" : tagsCsv;
        EnsurePrimitive(record, "cube");
        ApplyRecordVisuals(record);
        return record;
    }

    private SyncObjectRecord UpsertSphereRecord(
        string objectId,
        Vector3 position,
        float diameterMeters,
        Color color,
        string materialMode,
        string status,
        string tagsCsv,
        bool internalMarker
    )
    {
        SyncObjectRecord record = GetOrCreateObjectRecord(objectId, "sphere", "sphere", internalMarker);
        record.kind = "sphere";
        record.resourceType = "sphere";
        record.position = position;
        record.rotation = Quaternion.identity;
        record.scale = new Vector3(
            Mathf.Max(MinPrimitiveScale, diameterMeters),
            Mathf.Max(MinPrimitiveScale, diameterMeters),
            Mathf.Max(MinPrimitiveScale, diameterMeters)
        );
        record.color = color;
        record.visible = true;
        record.materialMode = string.IsNullOrEmpty(materialMode) ? "opaque" : materialMode;
        record.status = string.IsNullOrEmpty(status) ? "active" : status;
        record.tagsCsv = string.IsNullOrEmpty(tagsCsv) ? "" : tagsCsv;
        EnsurePrimitive(record, "sphere");
        ApplyRecordVisuals(record);
        return record;
    }

    private bool EnsurePrimitive(SyncObjectRecord record, string kind)
    {
        if (record == null)
            return false;
        if (record.gameObject != null)
            return true;

        PrimitiveType primitiveType = kind == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Cube;
        GameObject go = GameObject.CreatePrimitive(primitiveType);
        if (go == null)
            return false;
        go.name = "FASyncRuntime_" + record.objectId;
        go.transform.SetParent(runtimeRoot.transform, false);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        record.gameObject = go;
        return true;
    }

    private SyncObjectRecord GetOrCreateObjectRecord(
        string objectId,
        string kind,
        string resourceType,
        bool internalMarker
    )
    {
        SyncObjectRecord record;
        if (!syncObjects.TryGetValue(objectId, out record) || record == null)
        {
            record = new SyncObjectRecord();
            record.objectId = objectId;
            record.kind = kind;
            record.resourceType = resourceType;
            record.internalMarker = internalMarker;
            record.color = Color.white;
            syncObjects[objectId] = record;
        }

        return record;
    }

    private void ApplyRecordFromArgs(SyncObjectRecord record, string kind, string argsJson)
    {
        if (kind == "surface")
        {
            Vector3 a;
            Vector3 b;
            if (TryResolveSurfaceCorners(argsJson, out a, out b))
            {
                ApplySurfaceFromCorners(record, a, b);
            }
            else
            {
                ApplyPositionRotationScaleFromArgs(record, argsJson, PrimitiveSurfaceDepth);
            }
        }
        else
        {
            ApplyPositionRotationScaleFromArgs(record, argsJson, kind == "sphere" ? MarkerSphereDiameter : 0.10f);
        }

        Color parsedColor;
        if (TryReadColorArg(argsJson, out parsedColor))
            record.color = parsedColor;

        bool visible;
        if (TryReadBoolArg(argsJson, out visible, "visible", "enabled"))
            record.visible = visible;

        string materialMode = ExtractJsonArgString(argsJson, "materialMode", "mode");
        if (!string.IsNullOrEmpty(materialMode))
            record.materialMode = materialMode.Trim().ToLowerInvariant();

        string tagsCsv = ExtractJsonArgString(argsJson, "tagsCsv", "tags");
        if (!string.IsNullOrEmpty(tagsCsv))
            record.tagsCsv = tagsCsv;
    }

    private bool TryResolveSurfaceCorners(string argsJson, out Vector3 a, out Vector3 b)
    {
        a = Vector3.zero;
        b = Vector3.zero;

#if FRAMEANGEL_CUA_PLAYER
        if (!TryReadVectorArg(argsJson, "markerAPosition", out a))
            return false;
        if (!TryReadVectorArg(argsJson, "markerBPosition", out b))
            return false;
        return true;
#else
        return TryResolveSceneOnlySurfaceCorners(argsJson, out a, out b);
#endif
    }

    private void ApplySurfaceFromCorners(SyncObjectRecord record, Vector3 a, Vector3 b)
    {
        Vector3 min = new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        Vector3 max = new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        Vector3 center = (min + max) * 0.5f;
        Vector3 scale = new Vector3(
            Mathf.Max(MinPrimitiveScale, max.x - min.x),
            Mathf.Max(MinPrimitiveScale, max.y - min.y),
            PrimitiveSurfaceDepth
        );

        record.position = center;
        record.rotation = Quaternion.identity;
        record.scale = scale;
    }

    private void ApplyPositionRotationScaleFromArgs(SyncObjectRecord record, string argsJson, float defaultDepth)
    {
        Vector3 position;
        if (TryReadVectorComponents(argsJson, "pos", out position))
            record.position = position;

        Quaternion rotation;
        if (TryReadQuaternionComponents(argsJson, out rotation))
            record.rotation = rotation;

        Vector3 scale;
        if (TryReadScaleComponents(argsJson, defaultDepth, out scale))
            record.scale = scale;
        else if (record.scale == Vector3.zero)
            record.scale = new Vector3(0.10f, 0.10f, Mathf.Max(MinPrimitiveScale, defaultDepth));

        if (Vector3.Distance(Vector3.zero, record.position) < MinPanelDistanceMeters && record.internalMarker)
            record.position = new Vector3(record.position.x, record.position.y, MinPanelDistanceMeters);
    }

    private void ApplyRecordVisuals(SyncObjectRecord record)
    {
        if (record == null || record.gameObject == null)
            return;

        record.gameObject.transform.position = record.position;
        record.gameObject.transform.rotation = record.rotation;
        record.gameObject.transform.localScale = new Vector3(
            Mathf.Max(MinPrimitiveScale, record.scale.x),
            Mathf.Max(MinPrimitiveScale, record.scale.y),
            Mathf.Max(MinPrimitiveScale, record.scale.z)
        );

        Renderer renderer = record.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = record.visible;
            Material material = renderer.material;
            if (material != null)
            {
                Color displayColor = GetDisplayColor(record);
                bool transparent = string.Equals(record.materialMode, "transparent", StringComparison.OrdinalIgnoreCase);
                displayColor.a = transparent ? 0.35f : 1f;
                material.color = displayColor;
            }
        }

        ApplyInnerPieceRecordVisuals(record);

        if (record.rigidbody != null)
            record.rigidbody.isKinematic = !record.visible;
    }

    private void DestroyRecord(SyncObjectRecord record)
    {
        if (record == null || record.gameObject == null)
            return;
        StopTween(record);
        Destroy(record.gameObject);
        record.gameObject = null;
        record.rigidbody = null;
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;
        runtimeRoot = new GameObject("FASyncRuntimeRoot");
        runtimeRoot.transform.position = Vector3.zero;
        runtimeRoot.transform.rotation = Quaternion.identity;
    }

    private void RefreshRigStateSnapshot()
    {
#if !FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_TEST_SURFACES
        if (syncLastRigStateField != null)
            syncLastRigStateField.val = BuildRigStateJson();
#endif
    }

    private string BuildRigStateJson()
    {
#if FRAMEANGEL_CUA_PLAYER
        return "{}";
#else
        return BuildSceneOnlyRigStateJson();
#endif
    }

    private string BuildSceneStatePayload()
    {
        StringBuilder sb = new StringBuilder(4096);
        HashSet<string> emittedObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sb.Append('{');
#if FRAMEANGEL_CUA_PLAYER
        sb.Append("\"markers\":{\"markerA\":null,\"markerB\":null},");
#else
        sb.Append("\"markers\":").Append(BuildSceneOnlyMarkersPayload()).Append(',');
#endif
        sb.Append("\"activeObjectId\":\"").Append(EscapeJsonString(activeObjectId)).Append("\",");
        sb.Append("\"activeGroupId\":\"").Append(EscapeJsonString(activeGroupId)).Append("\",");
        sb.Append("\"reference\":").Append(BuildReferenceStatePayload()).Append(',');
        sb.Append("\"recipePreview\":").Append(BuildTableRecipePayload(RecipePreviewSchemaVersion, true)).Append(',');
#if FRAMEANGEL_CUA_PLAYER
        sb.Append("\"manipulation\":{\"isManipulating\":false,\"objectId\":\"\",\"hand\":\"\",\"startedAtUtc\":\"\",\"grabOffset\":null},");
#else
        sb.Append("\"manipulation\":").Append(BuildManipulationStateJson()).Append(',');
#endif
        AppendSceneOnlyStatePayload(sb);
#if FRAMEANGEL_CUA_PLAYER
        sb.Append("\"character\":{},");
#endif
        sb.Append("\"objects\":[");

        bool first = AppendLiveSceneObjectsJson(sb, emittedObjectIds);
        foreach (KeyValuePair<string, SyncObjectRecord> kvp in syncObjects)
        {
            SyncObjectRecord record = kvp.Value;
            if (record == null || record.internalMarker)
                continue;
            if (string.IsNullOrEmpty(record.objectId) || !emittedObjectIds.Add(record.objectId))
                continue;
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(BuildObjectJson(record));
        }

        sb.Append("],\"groups\":[");

        bool firstGroup = true;
        foreach (KeyValuePair<string, SyncGroupRecord> kvp in syncGroups)
        {
            SyncGroupRecord group = kvp.Value;
            if (group == null)
                continue;
            if (!firstGroup)
                sb.Append(',');
            firstGroup = false;
            sb.Append(BuildGroupJson(group));
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private bool AppendLiveSceneObjectsJson(StringBuilder sb, HashSet<string> emittedObjectIds)
    {
        bool first = true;
        List<Atom> atoms = GetLiveSceneAtoms();
        for (int i = 0; i < atoms.Count; i++)
        {
            Atom atom = atoms[i];
            if (atom == null || string.IsNullOrEmpty(atom.uid))
                continue;
            if (emittedObjectIds != null && !emittedObjectIds.Add(atom.uid))
                continue;
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(BuildLiveSceneObjectJson(atom));
        }

        return first;
    }

    private string BuildSingleObjectPayload(SyncObjectRecord record)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"object\":").Append(BuildObjectJson(record));
        sb.Append('}');
        return sb.ToString();
    }

    private string BuildObjectJson(SyncObjectRecord record)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"objectId\":\"").Append(EscapeJsonString(record.objectId)).Append("\",");
        sb.Append("\"kind\":\"").Append(EscapeJsonString(record.kind)).Append("\",");
        sb.Append("\"resourceType\":\"").Append(EscapeJsonString(record.resourceType)).Append("\",");
        sb.Append("\"status\":\"").Append(EscapeJsonString(record.status)).Append("\",");
        sb.Append("\"isActive\":").Append(IsActiveObject(record) ? "true" : "false").Append(',');
        sb.Append("\"visible\":").Append(record.visible ? "true" : "false").Append(',');
        sb.Append("\"materialMode\":\"").Append(EscapeJsonString(record.materialMode)).Append("\",");
        sb.Append("\"parentGroupId\":\"").Append(EscapeJsonString(record.parentGroupId)).Append("\",");
        sb.Append("\"tagsCsv\":\"").Append(EscapeJsonString(record.tagsCsv)).Append("\",");
        sb.Append("\"motion\":").Append(BuildSyncObjectMotionJson(record)).Append(',');
        sb.Append("\"transform\":{");
        sb.Append("\"posX\":").Append(FormatFloat(record.position.x)).Append(',');
        sb.Append("\"posY\":").Append(FormatFloat(record.position.y)).Append(',');
        sb.Append("\"posZ\":").Append(FormatFloat(record.position.z)).Append(',');
        sb.Append("\"rotX\":").Append(FormatFloat(record.rotation.x)).Append(',');
        sb.Append("\"rotY\":").Append(FormatFloat(record.rotation.y)).Append(',');
        sb.Append("\"rotZ\":").Append(FormatFloat(record.rotation.z)).Append(',');
        sb.Append("\"rotW\":").Append(FormatFloat(record.rotation.w)).Append(',');
        sb.Append("\"scaleX\":").Append(FormatFloat(record.scale.x)).Append(',');
        sb.Append("\"scaleY\":").Append(FormatFloat(record.scale.y)).Append(',');
        sb.Append("\"scaleZ\":").Append(FormatFloat(record.scale.z));
        sb.Append("},");
        sb.Append("\"style\":{");
        sb.Append("\"colorHex\":\"").Append(EscapeJsonString(ColorToHex(record.color))).Append("\"");
        sb.Append("}}");
        return sb.ToString();
    }

    private string BuildLiveSceneObjectJson(Atom atom)
    {
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;
        bool visible = false;
        string atomUid = atom != null ? atom.uid ?? "" : "";
        string atomType = atom != null ? atom.type ?? "" : "";

        try
        {
            if (atom != null && atom.transform != null)
            {
                position = atom.transform.position;
                rotation = atom.transform.rotation;
                scale = atom.transform.localScale;
            }
        }
        catch
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
        }

        try
        {
            visible = atom != null && atom.gameObject != null && atom.gameObject.activeInHierarchy;
        }
        catch
        {
            visible = false;
        }

        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"objectId\":\"").Append(EscapeJsonString(atomUid)).Append("\",");
        sb.Append("\"kind\":\"").Append(EscapeJsonString(atomType)).Append("\",");
        sb.Append("\"resourceType\":\"").Append(EscapeJsonString(atomType)).Append("\",");
        sb.Append("\"status\":\"live_scene_atom\",");
        sb.Append("\"isActive\":").Append(string.Equals(activeObjectId, atomUid, StringComparison.OrdinalIgnoreCase) ? "true" : "false").Append(',');
        sb.Append("\"visible\":").Append(visible ? "true" : "false").Append(',');
        sb.Append("\"materialMode\":\"opaque\",");
        sb.Append("\"parentGroupId\":\"\",");
        sb.Append("\"tagsCsv\":\"live_scene_atom\",");
        sb.Append("\"motion\":").Append(BuildSyncObjectMotionJson(null)).Append(',');
        sb.Append("\"transform\":{");
        sb.Append("\"posX\":").Append(FormatFloat(position.x)).Append(',');
        sb.Append("\"posY\":").Append(FormatFloat(position.y)).Append(',');
        sb.Append("\"posZ\":").Append(FormatFloat(position.z)).Append(',');
        sb.Append("\"rotX\":").Append(FormatFloat(rotation.x)).Append(',');
        sb.Append("\"rotY\":").Append(FormatFloat(rotation.y)).Append(',');
        sb.Append("\"rotZ\":").Append(FormatFloat(rotation.z)).Append(',');
        sb.Append("\"rotW\":").Append(FormatFloat(rotation.w)).Append(',');
        sb.Append("\"scaleX\":").Append(FormatFloat(scale.x)).Append(',');
        sb.Append("\"scaleY\":").Append(FormatFloat(scale.y)).Append(',');
        sb.Append("\"scaleZ\":").Append(FormatFloat(scale.z));
        sb.Append("},");
        sb.Append("\"style\":{");
        sb.Append("\"colorHex\":\"#FFFFFF\"");
        sb.Append("}}");
        return sb.ToString();
    }

    private string BuildGroupStatePayload(SyncGroupRecord group)
    {
        return "{\"group\":" + BuildGroupJson(group) + "}";
    }

    private string BuildGroupJson(SyncGroupRecord group)
    {
        SyncGroupRecord safeGroup = group ?? new SyncGroupRecord();
        StringBuilder sb = new StringBuilder(384);
        sb.Append('{');
        sb.Append("\"groupId\":\"").Append(EscapeJsonString(safeGroup.groupId)).Append("\",");
        sb.Append("\"status\":\"").Append(EscapeJsonString(safeGroup.status)).Append("\",");
        sb.Append("\"tagsCsv\":\"").Append(EscapeJsonString(safeGroup.tagsCsv)).Append("\",");
        sb.Append("\"updatedAtUtc\":\"").Append(EscapeJsonString(safeGroup.updatedAtUtc)).Append("\",");
        sb.Append("\"isActive\":").Append(string.Equals(activeGroupId, safeGroup.groupId, StringComparison.OrdinalIgnoreCase) ? "true" : "false").Append(',');
        sb.Append("\"memberIds\":[");
        bool first = true;
        foreach (string memberId in safeGroup.memberIds)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append('"').Append(EscapeJsonString(memberId)).Append('"');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private string BuildBuiltinsPayload()
    {
        return "{\"builtins\":["
            + "{\"kind\":\"surface\",\"resourceType\":\"surface\",\"description\":\"Thin cube-based surface primitive\"},"
            + "{\"kind\":\"sphere\",\"resourceType\":\"sphere\",\"description\":\"Unity sphere primitive\"},"
            + "{\"kind\":\"cube\",\"resourceType\":\"cube\",\"description\":\"Unity cube primitive\"}"
            + "]}";
    }

    private string BuildReferenceStatePayload()
    {
#if FRAMEANGEL_CUA_PLAYER
        return "{\"schemaVersion\":\"" + ReferenceStateSchemaVersion + "\",\"runType\":\"" + BuildTeachingRunType + "\",\"buildHand\":\"right\",\"lastUpdatedAtUtc\":\"\",\"grid\":{\"enabled\":false,\"visible\":false,\"cellSizeMeters\":0,\"aimDistanceMeters\":0},\"snappedPoint\":null,\"snapSource\":\"\",\"markers\":{\"markerA\":null,\"markerB\":null},\"floorPlane\":{\"defined\":false,\"heightMeters\":0,\"normal\":{\"x\":0,\"y\":1,\"z\":0},\"sourceId\":\"\"},\"tabletopHeight\":{\"defined\":false,\"heightMeters\":0,\"heightAboveFloorMeters\":0},\"span\":{\"defined\":false,\"axis\":\"distance\",\"meters\":0},\"selection\":" + BuildSelectionPayload() + ",\"preview\":{\"active\":false,\"groupId\":\"\",\"objectIds\":[]}}";
#else
        return BuildSceneOnlyReferenceStatePayload();
#endif
    }

    private string BuildTableRecipePayload(string schemaVersion, bool previewPerspective)
    {
#if FRAMEANGEL_CUA_PLAYER
        return "{\"schemaVersion\":\"" + EscapeJsonString(schemaVersion) + "\",\"runType\":\"" + BuildTeachingRunType + "\",\"recipeId\":\"" + TableRecipeId + "\",\"previewActive\":false,\"groupId\":\"\",\"topObjectId\":\"\",\"legObjectIds\":[],\"center\":{\"x\":0,\"y\":0,\"z\":0},\"widthMeters\":0,\"depthMeters\":0,\"tabletopHeightMeters\":0,\"topThicknessMeters\":0,\"legThicknessMeters\":0,\"legInsetMeters\":0,\"floorPlaneHeightMeters\":0,\"taskIds\":[]}";
#else
        return BuildSceneOnlyTableRecipePayload(schemaVersion, previewPerspective);
#endif
    }

    private string BuildBuildCodeEventPayload(string stage, string actionId, string summary, string payloadJson)
    {
        return "{"
            + "\"schemaVersion\":\"" + BuildCodeEventSchemaVersion + "\""
            + ",\"runType\":\"" + BuildTeachingRunType + "\""
            + ",\"recipeId\":\"" + TableRecipeId + "\""
            + ",\"stage\":\"" + EscapeJsonString(stage) + "\""
            + ",\"actionId\":\"" + EscapeJsonString(actionId) + "\""
            + ",\"summary\":\"" + EscapeJsonString(summary) + "\""
            + ",\"data\":" + (string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson)
            + "}";
    }

    private string BuildBuildCyclePayload(string stage, string actionId, string summary, string payloadJson)
    {
        return "{"
            + "\"schemaVersion\":\"" + BuildCycleSchemaVersion + "\""
            + ",\"runType\":\"" + BuildTeachingRunType + "\""
            + ",\"recipeId\":\"" + TableRecipeId + "\""
            + ",\"stage\":\"" + EscapeJsonString(stage) + "\""
            + ",\"actionId\":\"" + EscapeJsonString(actionId) + "\""
            + ",\"summary\":\"" + EscapeJsonString(summary) + "\""
            + ",\"data\":" + (string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson)
            + "}";
    }

    private string BuildStringArrayJson(List<string> values)
    {
        StringBuilder sb = new StringBuilder(128);
        sb.Append('[');
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('"').Append(EscapeJsonString(values[i])).Append('"');
            }
        }
        sb.Append(']');
        return sb.ToString();
    }

    private string BuildSimpleStatePayload(string key, string value)
    {
        return "{\"" + EscapeJsonString(key) + "\":\"" + EscapeJsonString(value) + "\"}";
    }

    private string AppendJsonStringField(string argsJson, string key, string value)
    {
        string trimmed = string.IsNullOrEmpty(argsJson) ? "{}" : argsJson.Trim();
        if (string.IsNullOrEmpty(trimmed))
            trimmed = "{}";

        if (!trimmed.EndsWith("}", StringComparison.Ordinal))
            return argsJson;

        if (trimmed == "{}")
        {
            return "{\""
                + EscapeJsonString(key)
                + "\":\""
                + EscapeJsonString(value)
                + "\"}";
        }

        return trimmed.Substring(0, trimmed.Length - 1)
            + ",\""
            + EscapeJsonString(key)
            + "\":\""
            + EscapeJsonString(value)
            + "\"}";
    }

    private string BuildSelectionPayload()
    {
        return "{"
            + "\"activeObjectId\":\"" + EscapeJsonString(activeObjectId) + "\""
            + ",\"activeGroupId\":\"" + EscapeJsonString(activeGroupId) + "\""
            + "}";
    }

    private string BuildManipulationStateJson()
    {
#if FRAMEANGEL_CUA_PLAYER
        return "{\"isManipulating\":false,\"objectId\":\"\",\"hand\":\"\",\"startedAtUtc\":\"\",\"grabOffset\":null}";
#else
        return BuildSceneOnlyManipulationStateJson();
#endif
    }

    private string BuildRuntimeReadyPayload()
    {
        StringBuilder sb = new StringBuilder(192);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"session_runtime_ready_v1\",");
#if FRAMEANGEL_CUA_PLAYER
        sb.Append("\"pluginId\":\"fa_cua_player\",");
#else
        sb.Append("\"pluginId\":\"fa_scene_player\",");
#endif
        sb.Append("\"buildVersion\":\"").Append(EscapeJsonString(BuildRuntimeInfo.BuildVersion)).Append("\",");
        sb.Append("\"buildChannel\":\"").Append(EscapeJsonString(BuildRuntimeInfo.BuildChannel)).Append("\",");
        sb.Append("\"runtimeRootReady\":").Append(runtimeRoot != null ? "true" : "false").Append(',');
        sb.Append("\"rigRefreshIntervalSeconds\":").Append(RigStateRefreshIntervalSeconds.ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    private string BuildBrokerResult(bool ok, string summary, string payloadJson)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"ok\":").Append(ok ? "true" : "false").Append(',');
        sb.Append("\"summary\":\"").Append(EscapeJsonString(summary)).Append("\",");
        sb.Append("\"message\":\"").Append(EscapeJsonString(summary)).Append("\",");
        sb.Append("\"payload\":");
        sb.Append(string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
        sb.Append('}');
        return sb.ToString();
    }

    private bool TryResolveSceneObject(string argsJson, out SyncObjectRecord record, out string errorMessage)
    {
        record = null;
        errorMessage = "";

        string objectId = ExtractJsonArgString(argsJson, "objectId", "id");
        if (string.IsNullOrEmpty(objectId))
        {
            errorMessage = "objectId is required";
            return false;
        }

        if (!syncObjects.TryGetValue(objectId, out record) || record == null || record.internalMarker)
        {
            errorMessage = "object not found";
            return false;
        }

        return true;
    }

    private SyncGroupRecord EnsureGroupRecord(string groupId)
    {
        SyncGroupRecord group;
        if (!syncGroups.TryGetValue(groupId, out group) || group == null)
        {
            group = new SyncGroupRecord();
            group.groupId = groupId;
            group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            syncGroups[groupId] = group;
        }

        return group;
    }

    private bool TryResolveGroup(string argsJson, out SyncGroupRecord group, out string errorMessage)
    {
        group = null;
        errorMessage = "";

        string groupId = ExtractJsonArgString(argsJson, "groupId", "id");
        if (string.IsNullOrEmpty(groupId))
        {
            errorMessage = "groupId is required";
            return false;
        }

        if (!syncGroups.TryGetValue(groupId, out group) || group == null)
        {
            errorMessage = "group not found";
            return false;
        }

        return true;
    }

    private void RemoveObjectFromAllGroups(string objectId)
    {
        foreach (KeyValuePair<string, SyncGroupRecord> kvp in syncGroups)
        {
            SyncGroupRecord group = kvp.Value;
            if (group == null)
                continue;
            if (group.memberIds.Remove(objectId))
                group.updatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }
    }

    private List<SyncObjectRecord> GetOrderedGroupMembers(SyncGroupRecord group)
    {
        List<SyncObjectRecord> members = new List<SyncObjectRecord>();
        if (group == null)
            return members;

        foreach (string memberId in group.memberIds)
        {
            SyncObjectRecord record;
            if (syncObjects.TryGetValue(memberId, out record) && record != null && !record.internalMarker)
                members.Add(record);
        }

        members.Sort(
            delegate(SyncObjectRecord a, SyncObjectRecord b)
            {
                return string.Compare(a != null ? a.objectId : "", b != null ? b.objectId : "", StringComparison.Ordinal);
            }
        );
        return members;
    }

    private Vector3 ComputeGroupPivot(List<SyncObjectRecord> members)
    {
        if (members == null || members.Count <= 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < members.Count; i++)
            sum += members[i].position;
        return sum / Mathf.Max(1, members.Count);
    }

    private string CreateMotionOperationId(string prefix, string selector)
    {
        string safePrefix = string.IsNullOrEmpty(prefix) ? "motion" : prefix.Trim().Replace(' ', '_');
        string safeSelector = string.IsNullOrEmpty(selector) ? "unknown" : selector.Trim().Replace(' ', '_');
        return safePrefix + "_" + safeSelector + "_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
    }

    private void BeginTweenMotionState(SyncObjectRecord record, string operationId, float durationSeconds)
    {
        if (record == null)
            return;

        record.motionType = "tween";
        record.motionOperationId = string.IsNullOrEmpty(operationId)
            ? CreateMotionOperationId("tween", record.objectId)
            : operationId.Trim();
        record.motionStatus = "running";
        record.motionStartedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        record.motionStartedAtUnscaledTime = Time.unscaledTime;
        record.motionDurationSeconds = Mathf.Max(0.01f, durationSeconds);
        record.motionFinishedAtUtc = "";
    }

    private void CompleteMotionState(SyncObjectRecord record, string status)
    {
        if (record == null)
            return;

        string normalizedStatus = string.IsNullOrEmpty(status) ? "idle" : status.Trim().ToLowerInvariant();
        record.motionStatus = normalizedStatus;
        if (string.Equals(normalizedStatus, "running", StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrEmpty(record.motionType) || !string.IsNullOrEmpty(record.motionOperationId))
            record.motionFinishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }

    private float GetMotionProgress(SyncObjectRecord record)
    {
        if (record == null)
            return 0f;

        if (string.Equals(record.motionStatus, "completed", StringComparison.OrdinalIgnoreCase))
            return 1f;

        if (record.motionDurationSeconds <= 0.0001f)
            return string.Equals(record.motionStatus, "running", StringComparison.OrdinalIgnoreCase) ? 0f : 1f;

        return Mathf.Clamp01((Time.unscaledTime - record.motionStartedAtUnscaledTime) / record.motionDurationSeconds);
    }

    private string BuildSyncObjectMotionJson(SyncObjectRecord record)
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');

        if (record == null)
        {
            sb.Append("\"active\":false,");
            sb.Append("\"type\":\"\",");
            sb.Append("\"status\":\"idle\",");
            sb.Append("\"operationId\":\"\",");
            sb.Append("\"progress\":0,");
            sb.Append("\"durationSeconds\":0,");
            sb.Append("\"startedAtUtc\":\"\",");
            sb.Append("\"finishedAtUtc\":\"\"");
            sb.Append('}');
            return sb.ToString();
        }

        bool active = record.tweenCoroutine != null && string.Equals(record.motionStatus, "running", StringComparison.OrdinalIgnoreCase);
        sb.Append("\"active\":").Append(active ? "true" : "false").Append(',');
        sb.Append("\"type\":\"").Append(EscapeJsonString(record.motionType ?? "")).Append("\",");
        sb.Append("\"status\":\"").Append(EscapeJsonString(record.motionStatus ?? "idle")).Append("\",");
        sb.Append("\"operationId\":\"").Append(EscapeJsonString(record.motionOperationId ?? "")).Append("\",");
        sb.Append("\"progress\":").Append(FormatFloat(GetMotionProgress(record))).Append(',');
        sb.Append("\"durationSeconds\":").Append(FormatFloat(record.motionDurationSeconds)).Append(',');
        sb.Append("\"startedAtUtc\":\"").Append(EscapeJsonString(record.motionStartedAtUtc ?? "")).Append("\",");
        sb.Append("\"finishedAtUtc\":\"").Append(EscapeJsonString(record.motionFinishedAtUtc ?? "")).Append("\"");
        sb.Append('}');
        return sb.ToString();
    }

    private void StopTween(SyncObjectRecord record, string terminalStatus = "cancelled")
    {
        if (record == null || record.tweenCoroutine == null)
            return;
        StopCoroutine(record.tweenCoroutine);
        record.tweenCoroutine = null;
        CompleteMotionState(record, terminalStatus);
    }

    private Rigidbody EnsureRuntimeBody(SyncObjectRecord record)
    {
        if (record == null || record.gameObject == null)
            return null;
        if (record.rigidbody == null)
        {
            record.rigidbody = record.gameObject.GetComponent<Rigidbody>();
            if (record.rigidbody == null)
                record.rigidbody = record.gameObject.AddComponent<Rigidbody>();
            if (record.rigidbody != null)
            {
                record.rigidbody.useGravity = false;
                record.rigidbody.drag = 0f;
                record.rigidbody.angularDrag = 0.05f;
            }
        }

        return record.rigidbody;
    }

    private IEnumerator RunTweenTransform(
        SyncObjectRecord record,
        string actionId,
        string operationId,
        Vector3 targetPosition,
        Quaternion targetRotation,
        Vector3 targetScale,
        float durationSeconds,
        string correlationId,
        string commandMessageId
    )
    {
        if (record == null)
            yield break;

        Vector3 startPosition = record.position;
        Quaternion startRotation = record.rotation;
        Vector3 startScale = record.scale;
        float startedAt = Time.unscaledTime;
        while (record != null && record.gameObject != null)
        {
            float elapsed = Time.unscaledTime - startedAt;
            float t = durationSeconds <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / durationSeconds);
            record.position = Vector3.Lerp(startPosition, targetPosition, t);
            record.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            record.scale = Vector3.Lerp(startScale, targetScale, t);
            ApplyRecordVisuals(record);
            if (t >= 1f)
                break;
            yield return null;
        }

        if (record != null)
        {
            record.tweenCoroutine = null;
            if (string.Equals(record.motionOperationId, operationId ?? "", StringComparison.OrdinalIgnoreCase))
                CompleteMotionState(record, "completed");
            string payload = BuildSingleObjectPayload(record);
            EmitRuntimeEvent(
                "scene_object_tween_finished",
                actionId,
                "ok",
                "",
                record.objectId,
                record.objectId,
                correlationId,
                commandMessageId,
                "",
                payload
            );
        }
    }

    private void SetInputCaptureState(bool enabled)
    {
#if !FRAMEANGEL_CUA_PLAYER
        SetSceneOnlyInputCaptureState(enabled);
#elif FRAMEANGEL_FEATURE_PLAYER_INPUT
        SetCuaPlayerInputCaptureState(enabled);
#endif
    }

    private bool IsVrRuntimeActive()
    {
        SuperController sc = SuperController.singleton;
        if (sc == null || sc.disableVR)
            return false;
        return sc.isOVR || sc.isOpenVR;
    }

    private Transform ResolveVrHandTransform(bool leftHand)
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
            return null;

        if (leftHand)
        {
            if (sc.leftHand != null)
                return sc.leftHand;
            if (sc.leftHandAlternate != null)
                return sc.leftHandAlternate;
            if (sc.touchCenterHandLeft != null)
                return sc.touchCenterHandLeft;
            if (sc.viveCenterHandLeft != null)
                return sc.viveCenterHandLeft;
            return null;
        }

        if (sc.rightHand != null)
            return sc.rightHand;
        if (sc.rightHandAlternate != null)
            return sc.rightHandAlternate;
        if (sc.touchCenterHandRight != null)
            return sc.touchCenterHandRight;
        if (sc.viveCenterHandRight != null)
            return sc.viveCenterHandRight;
        return null;
    }

    private bool SafeReadBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return false;
        }
    }

    private void SetLastReceipt(string value)
    {
        syncBrokerResultJson = string.IsNullOrEmpty(value) ? "" : value;
#if !FRAMEANGEL_CUA_PLAYER && FRAMEANGEL_TEST_SURFACES
        if (syncLastReceiptField != null)
            syncLastReceiptField.val = syncBrokerResultJson;
#endif
        if (syncBrokerResultJsonField != null)
            syncBrokerResultJsonField.val = syncBrokerResultJson;
    }

    private void SetLastError(string value)
    {
        string normalized = string.IsNullOrEmpty(value) ? "" : value;
        if (syncLastErrorField != null)
            syncLastErrorField.val = normalized;
    }

    private void EmitRuntimeEvent(
        string eventType,
        string actionId,
        string status,
        string errorCode,
        string message,
        string objectId,
        string correlationId,
        string commandMessageId,
        string hand,
        string payloadJson = ""
    )
    {
#if !FRAMEANGEL_TEST_SURFACES || FRAMEANGEL_CUA_PLAYER
        return;
#else
        syncEventSequence++;

        string eventMessageId = "sync_evt_msg_" + syncEventSequence.ToString("D6", CultureInfo.InvariantCulture);
        StringBuilder sb = new StringBuilder(640);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"session_plugin_event_v1\",");
        sb.Append("\"eventType\":\"").Append(EscapeJsonString(eventType)).Append("\",");
        sb.Append("\"eventId\":\"sync_evt_")
            .Append(syncEventSequence.ToString("D6", CultureInfo.InvariantCulture))
            .Append("\",");
        sb.Append("\"messageId\":\"").Append(EscapeJsonString(eventMessageId)).Append("\",");
        sb.Append("\"correlationId\":\"").Append(EscapeJsonString(correlationId)).Append("\",");
        sb.Append("\"sequence\":").Append(syncEventSequence.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"timestampUtc\":\"")
            .Append(EscapeJsonString(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)))
            .Append("\",");
        sb.Append("\"source\":\"fa_sync_runtime\",");
        sb.Append("\"actionId\":\"").Append(EscapeJsonString(actionId)).Append("\",");
        sb.Append("\"status\":\"").Append(EscapeJsonString(status)).Append("\",");
        sb.Append("\"errorCode\":\"").Append(EscapeJsonString(errorCode)).Append("\",");
        sb.Append("\"message\":\"").Append(EscapeJsonString(message)).Append("\",");
        sb.Append("\"objectId\":\"").Append(EscapeJsonString(objectId)).Append('"');
        if (!string.IsNullOrEmpty(commandMessageId))
            sb.Append(",\"commandMessageId\":\"").Append(EscapeJsonString(commandMessageId)).Append('"');
        if (!string.IsNullOrEmpty(hand))
            sb.Append(",\"hand\":\"").Append(EscapeJsonString(hand)).Append('"');
        if (!string.IsNullOrEmpty(payloadJson))
            sb.Append(",\"payload\":").Append(payloadJson);
        sb.Append('}');

        string appendError;
        if (
            !SessionBridgeJsonl.TryAppendLine(
                syncEventOutboxPath,
                SessionBridgeJsonl.GetDefaultEventOutboxPath(),
                sb.ToString(),
                out appendError
            )
        )
        {
            if (!string.IsNullOrEmpty(appendError))
            {
                SetLastError("event append failed: " + appendError);
                FrameAngelLog.Error("FASyncRuntime event append failed: " + appendError);
            }
        }
#endif
    }

    private bool IsManipulationActive()
    {
#if FRAMEANGEL_CUA_PLAYER
        return false;
#else
        return IsSceneOnlyManipulationActive();
#endif
    }

    private bool IsSceneDevModeEnabled()
    {
#if FRAMEANGEL_CUA_PLAYER
        return false;
#else
        return syncDevMode;
#endif
    }

    private bool IsSceneInputCaptureEnabled()
    {
#if FRAMEANGEL_CUA_PLAYER
#if FRAMEANGEL_FEATURE_PLAYER_INPUT
        return IsCuaPlayerInputCaptureEnabled();
#else
        return false;
#endif
#else
        return syncInputCapture;
#endif
    }

    private bool IsManipulatingObject(string objectId)
    {
#if FRAMEANGEL_CUA_PLAYER
        return false;
#else
        return IsSceneOnlyManipulatingObject(objectId);
#endif
    }

    private void CancelManipulation(string reason)
    {
#if !FRAMEANGEL_CUA_PLAYER
        CancelSceneOnlyManipulation(reason);
#endif
    }

    private bool IsActiveObject(SyncObjectRecord record)
    {
        return record != null && !string.IsNullOrEmpty(activeObjectId) && string.Equals(record.objectId, activeObjectId, StringComparison.OrdinalIgnoreCase);
    }

    private Color GetDisplayColor(SyncObjectRecord record)
    {
        if (record != null && IsActiveObject(record))
            return ActiveObjectTint;
        return record != null ? record.color : Color.white;
    }

    private void RefreshRecordVisuals(string objectId)
    {
        if (string.IsNullOrEmpty(objectId))
            return;

        SyncObjectRecord record;
        if (syncObjects.TryGetValue(objectId, out record) && record != null)
            ApplyRecordVisuals(record);
    }

    private bool TryReadBoolArg(string json, out bool value, params string[] keys)
    {
        value = false;
        if (keys == null)
            return false;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            if (TryExtractJsonBoolField(json, key, out value))
                return true;

            string stringValue;
            if (!TryExtractJsonStringField(json, key, out stringValue))
                continue;

            if (string.Equals(stringValue, "1", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(stringValue, "0", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            if (bool.TryParse(stringValue, out value))
                return true;
        }

        return false;
    }

    private bool TryReadDurationSeconds(string json, out float value)
    {
        value = 0f;

        float durationSeconds;
        if (TryExtractJsonFloatField(json, "durationSeconds", out durationSeconds))
        {
            value = durationSeconds;
            return true;
        }

        float durationMs;
        if (TryExtractJsonFloatField(json, "durationMs", out durationMs))
        {
            value = durationMs / 1000f;
            return true;
        }

        return false;
    }

    private bool TryReadVectorArg(string json, string key, out Vector3 value)
    {
        value = Vector3.zero;
        if (string.IsNullOrEmpty(key))
            return false;

        string objectJson;
        if (TryExtractJsonObjectField(json, key, out objectJson))
            return TryReadVectorComponents(objectJson, "", out value);

        return false;
    }

    private List<string> ExtractJsonStringList(string json, params string[] keys)
    {
        List<string> values = new List<string>();
        if (keys == null || string.IsNullOrEmpty(json))
            return values;

        for (int i = 0; i < keys.Length; i++)
        {
            string arrayJson;
            if (!TryExtractJsonArrayField(json, keys[i], out arrayJson))
                continue;

            MatchCollection matches = Regex.Matches(arrayJson, "\\\"((?:\\\\.|[^\\\\\\\"])*)\\\"", RegexOptions.Singleline);
            for (int j = 0; j < matches.Count; j++)
            {
                string item = UnescapeJsonString(matches[j].Groups[1].Value);
                if (!string.IsNullOrEmpty(item))
                    values.Add(item);
            }

            if (values.Count > 0)
                return values;
        }

        return values;
    }

    private bool TryReadVectorComponents(string json, string baseKey, out Vector3 value)
    {
        value = Vector3.zero;

        string objectJson;
        if (!string.IsNullOrEmpty(baseKey))
        {
            if (TryExtractJsonObjectField(json, baseKey, out objectJson))
                return TryReadVectorComponents(objectJson, "", out value);

            if (string.Equals(baseKey, "pos", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractJsonObjectField(json, "position", out objectJson))
                    return TryReadVectorComponents(objectJson, "", out value);
            }
        }

        float x;
        float y;
        float z;
        if (
            TryReadFloatFieldVariants(json, baseKey, "X", "x", out x)
            && TryReadFloatFieldVariants(json, baseKey, "Y", "y", out y)
            && TryReadFloatFieldVariants(json, baseKey, "Z", "z", out z)
        )
        {
            value = new Vector3(x, y, z);
            return true;
        }

        return false;
    }

    private bool TryReadQuaternionComponents(string json, out Quaternion value)
    {
        value = Quaternion.identity;

        string objectJson;
        if (TryExtractJsonObjectField(json, "rotation", out objectJson) || TryExtractJsonObjectField(json, "rot", out objectJson))
        {
            return TryReadQuaternionObject(objectJson, out value);
        }

        float x;
        float y;
        float z;
        float w;
        if (
            TryReadFloatFieldVariants(json, "rot", "X", "x", out x)
            && TryReadFloatFieldVariants(json, "rot", "Y", "y", out y)
            && TryReadFloatFieldVariants(json, "rot", "Z", "z", out z)
            && TryReadFloatFieldVariants(json, "rot", "W", "w", out w)
        )
        {
            value = new Quaternion(x, y, z, w);
            return true;
        }

        if (
            TryReadFloatFieldVariants(json, "rotation", "X", "x", out x)
            && TryReadFloatFieldVariants(json, "rotation", "Y", "y", out y)
            && TryReadFloatFieldVariants(json, "rotation", "Z", "z", out z)
            && TryReadFloatFieldVariants(json, "rotation", "W", "w", out w)
        )
        {
            value = new Quaternion(x, y, z, w);
            return true;
        }

        return false;
    }

    private bool TryReadScaleComponents(string json, float defaultDepth, out Vector3 value)
    {
        value = Vector3.zero;

        string objectJson;
        if (TryExtractJsonObjectField(json, "scale", out objectJson))
        {
            if (TryReadVectorComponents(objectJson, "", out value))
                return true;
        }

        float x;
        float y;
        float z;
        if (
            TryExtractJsonFloatField(json, "scaleX", out x)
            && TryExtractJsonFloatField(json, "scaleY", out y)
            && TryExtractJsonFloatField(json, "scaleZ", out z)
        )
        {
            value = new Vector3(x, y, z);
            return true;
        }

        float width;
        float height;
        if (TryExtractJsonFloatField(json, "width", out width) && TryExtractJsonFloatField(json, "height", out height))
        {
            if (!TryExtractJsonFloatField(json, "depth", out z))
                z = defaultDepth;
            value = new Vector3(width, height, z);
            return true;
        }

        return false;
    }

    private bool TryReadColorArg(string json, out Color value)
    {
        value = Color.white;

        string hex = ExtractJsonArgString(json, "colorHex", "hex", "color");
        if (!string.IsNullOrEmpty(hex) && TryParseColorHex(hex, out value))
            return true;

        string colorObject;
        if (TryExtractJsonObjectField(json, "color", out colorObject))
        {
            float r;
            float g;
            float b;
            float a;
            if (
                TryExtractJsonFloatField(colorObject, "r", out r)
                && TryExtractJsonFloatField(colorObject, "g", out g)
                && TryExtractJsonFloatField(colorObject, "b", out b)
            )
            {
                if (!TryExtractJsonFloatField(colorObject, "a", out a))
                    a = 1f;
                value = new Color(r, g, b, a);
                return true;
            }
        }

        return false;
    }

    private bool TryParseColorHex(string raw, out Color value)
    {
        value = Color.white;
        if (string.IsNullOrEmpty(raw))
            return false;

        string hex = raw.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
            hex = hex.Substring(1);

        if (hex.Length != 6 && hex.Length != 8)
            return false;

        byte r;
        byte g;
        byte b;
        byte a = 255;
        if (
            !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            || !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            || !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)
        )
        {
            return false;
        }

        if (
            hex.Length == 8
            && !byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a)
        )
        {
            return false;
        }

        value = new Color32(r, g, b, a);
        return true;
    }

    private bool TryReadQuaternionObject(string json, out Quaternion value)
    {
        value = Quaternion.identity;
        float x;
        float y;
        float z;
        float w;
        if (
            TryExtractJsonFloatField(json, "x", out x)
            && TryExtractJsonFloatField(json, "y", out y)
            && TryExtractJsonFloatField(json, "z", out z)
            && TryExtractJsonFloatField(json, "w", out w)
        )
        {
            value = new Quaternion(x, y, z, w);
            return true;
        }

        return false;
    }

    private bool TryReadFloatFieldVariants(string json, string baseKey, string suffixUpper, string suffixLower, out float value)
    {
        value = 0f;
        if (!string.IsNullOrEmpty(baseKey))
        {
            if (TryExtractJsonFloatField(json, baseKey + suffixUpper, out value))
                return true;
            if (TryExtractJsonFloatField(json, baseKey + suffixLower, out value))
                return true;
        }

        return TryExtractJsonFloatField(json, suffixLower, out value);
    }

    private string BuildVectorJson(Vector3 value)
    {
        return "{"
            + "\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + ",\"z\":" + FormatFloat(value.z)
            + "}";
    }

    private string BuildQuaternionJson(Quaternion value)
    {
        return "{"
            + "\"x\":" + FormatFloat(value.x)
            + ",\"y\":" + FormatFloat(value.y)
            + ",\"z\":" + FormatFloat(value.z)
            + ",\"w\":" + FormatFloat(value.w)
            + "}";
    }

    private string FormatFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private string ColorToHex(Color value)
    {
        Color32 color = value;
        return "#"
            + color.r.ToString("X2", CultureInfo.InvariantCulture)
            + color.g.ToString("X2", CultureInfo.InvariantCulture)
            + color.b.ToString("X2", CultureInfo.InvariantCulture)
            + color.a.ToString("X2", CultureInfo.InvariantCulture);
    }

    private string ExtractJsonArgString(string argsJson, params string[] keys)
    {
        if (keys == null)
            return "";

        for (int i = 0; i < keys.Length; i++)
        {
            string value;
            if (TryExtractJsonStringField(argsJson, keys[i], out value) && !string.IsNullOrEmpty(value))
                return value;
        }

        return "";
    }

    private bool TryExtractJsonStringField(string json, string key, out string value)
    {
        value = "";
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\\\\\"])*)\\\"";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        if (!match.Success)
            return false;

        value = UnescapeJsonString(match.Groups[1].Value);
        return true;
    }

    private bool TryExtractJsonBoolField(string json, string key, out bool value)
    {
        value = false;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)";
        Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return false;

        return bool.TryParse(match.Groups[1].Value, out value);
    }

    private bool TryExtractJsonFloatField(string json, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        if (!match.Success)
            return false;

        return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool TryExtractJsonObjectField(string json, string key, out string objectJson)
    {
        objectJson = "";
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string needle = "\"" + key + "\"";
        int idx = json.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int colon = json.IndexOf(':', idx + needle.Length);
        if (colon < 0)
            return false;

        int start = colon + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start]))
            start++;
        if (start >= json.Length || json[start] != '{')
            return false;

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    objectJson = json.Substring(start, i - start + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryExtractJsonArrayField(string json, string key, out string arrayJson)
    {
        arrayJson = "";
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string needle = "\"" + key + "\"";
        int idx = json.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int colon = json.IndexOf(':', idx + needle.Length);
        if (colon < 0)
            return false;

        int start = colon + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start]))
            start++;
        if (start >= json.Length || json[start] != '[')
            return false;

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '[')
                depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    arrayJson = json.Substring(start, i - start + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private string UnescapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder sb = new StringBuilder(value.Length);
        bool escaped = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!escaped)
            {
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                sb.Append(c);
                continue;
            }

            escaped = false;
            switch (c)
            {
                case '\\':
                    sb.Append('\\');
                    break;
                case '"':
                    sb.Append('"');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case 'u':
                    if (i + 4 < value.Length)
                    {
                        string hex = value.Substring(i + 1, 4);
                        int code;
                        if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                            break;
                        }
                    }
                    sb.Append('u');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder sb = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 32)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}
