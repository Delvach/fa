using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FrameAngel.Runtime.Shared;
using MVR.FileManagementSecure;
using UnityEngine;

public partial class FASyncRuntime
{
    private const string MetaProofCurrentPackagePath = "";
    private const string MetaProofLegacyPackagePath = "";
    private const string MetaProofLocalPackagePath = "Custom\\PluginData\\FrameAngel\\meta_proof\\faipe_ghost_meta_video_player_proof_9c2895ca1cce";
    private const string LegacyMetaProofLocalPackagePath = "Custom\\PluginData\\VamSlideshow\\meta_proof\\faipe_ghost_meta_video_player_proof_9c2895ca1cce";
    private const string MetaProofGhostScreenRectPackagePath = "Custom\\PluginData\\FrameAngel\\GhostScreenVariants\\rect\\package";
    private const string MetaProofGhostScreenLocalPackagePath = "Custom\\PluginData\\FrameAngel\\ghost_screen_proof\\rounded";
    private const string LegacyMetaProofGhostScreenLocalPackagePath = "Custom\\PluginData\\VamSlideshow\\ghost_screen_proof\\rounded";
    private const string MetaToolkitDemoAbsoluteThemeRootPath = "";
    private const string MetaToolkitDemoDefaultThemeLocalRootPath = "Custom\\PluginData\\FrameAngel\\meta_toolkit_demo\\theme_00";
    private const string LegacyMetaToolkitDemoDefaultThemeLocalRootPath = "Custom\\PluginData\\VamSlideshow\\meta_toolkit_demo\\theme_00";
    private const string MetaPlayerRuntimeActiveSurfaceConfigPath = "Custom\\PluginData\\FrameAngel\\meta_player_runtime\\active_surface.json";
    private const string MetaPlayerRuntimeActiveSurfaceSchemaVersion = "frameangel_meta_player_runtime_surface_v1";
    private const string MetaProofCurrentResourceId = "ghost_meta_video_player_proof_4e396b6f5928";
    private const string MetaProofFallbackResourceId = "ghost_meta_video_player_proof_4be7f5517b36";
    private const string MetaProofGhostScreenRectResourceId = "ghost_prototype_screen_rect_f6b49ee33cdb";
    private const string MetaToolkitControlSurfaceStateSchemaVersion = "meta_toolkit_control_surface_state_v1";
    private const string MetaProofGhostScreenDefaultInstanceId = "ghost_screen_rect_main";
    private const string MetaProofControlSurfaceDefaultInstanceId = "meta_video_player_proof_main";
    private const string MetaProofPreferredHostAtomUid = "Empty";
    private const float MetaProofTargetScreenForwardOffsetMeters = 0.72f;
    private const float MetaProofTargetScreenVerticalOffsetMeters = 0.05f;
    private const float MetaProofControlPanelGapMeters = 0.035f;
    private const float MetaProofControlPanelForwardOffsetMeters = 0.015f;
    private const float MetaProofControlPanelUniformScale = 2.0f;
    private const float MetaProofControlPanelLayoutPositionToleranceMeters = 0.01f;
    private const float MetaProofControlPanelLayoutRotationToleranceDegrees = 1.5f;
    private const float MetaProofVolumeLowNormalized = 0.25f;
    private const float MetaProofVolumeHighNormalized = 0.75f;

    private static readonly string[] MetaProofSampleMediaPathCandidates = new string[]
    {
        "Custom\\Images\\_fillm\\The.Fifth.Element.mp4",
        "Custom\\Images\\02_vid\\galaxy__00001.mp4",
        "Cache\\Videos\\BlueScr1080_1356140_133881055000000000.mp4",
    };

    private static readonly MetaToolkitDemoSurfaceDefinition[] MetaToolkitDefaultThemeDemoSurfaces = new MetaToolkitDemoSurfaceDefinition[]
    {
        new MetaToolkitDemoSurfaceDefinition("video_player", "meta_ui_video_player", "faipe_meta_contentuiexample_videoplayer_3c2b98cf4fd1", "meta_toolkit_demo_video_player", 0f, 0f, 0f, MetaProofControlPanelUniformScale, true),
        new MetaToolkitDemoSurfaceDefinition("backplate", "meta_ui_toolkit", "faipe_meta_emptyuibackplatewithcanvas_dcc3fd3c068f", "meta_toolkit_demo_backplate", -0.72f, 0.22f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("search_bar", "meta_ui_search_bar", "faipe_meta_searchbar_423d842aeb2b", "meta_toolkit_demo_search_bar", -0.72f, -0.02f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("primary_button", "meta_ui_toolkit", "faipe_meta_primarybutton_iconandlabel_5b125b2e4465", "meta_toolkit_demo_primary_button", -0.72f, -0.18f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("small_slider", "meta_ui_toolkit", "faipe_meta_smallslider_470d90d890db", "meta_toolkit_demo_small_slider", -0.72f, -0.34f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("dialog", "meta_ui_toolkit", "faipe_meta_dialog1button_iconandtext_ff96a1905247", "meta_toolkit_demo_dialog", 0.72f, 0.22f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("dropdown", "meta_ui_toolkit", "faipe_meta_dropdown1linetextonly_768f1b2c87f8", "meta_toolkit_demo_dropdown", 0.72f, -0.04f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("context_menu", "meta_ui_toolkit", "faipe_meta_contextmenu1linetextonly_0cd58fe25aea", "meta_toolkit_demo_context_menu", 0.72f, -0.24f, 0.05f, 1f, false),
        new MetaToolkitDemoSurfaceDefinition("tooltip", "meta_ui_toolkit", "faipe_meta_tooltip_89b82cff1537", "meta_toolkit_demo_tooltip", 0.72f, -0.42f, 0.05f, 1f, false),
    };

    private sealed class MetaToolkitDemoSurfaceDefinition
    {
        public MetaToolkitDemoSurfaceDefinition(
            string key,
            string controlFamilyId,
            string packageFolderName,
            string instanceId,
            float rightOffsetMeters,
            float upOffsetMeters,
            float forwardOffsetMeters,
            float uniformScale,
            bool bindToPlayer)
        {
            this.key = key ?? "";
            this.controlFamilyId = controlFamilyId ?? "";
            this.packageFolderName = packageFolderName ?? "";
            this.instanceId = instanceId ?? "";
            this.rightOffsetMeters = rightOffsetMeters;
            this.upOffsetMeters = upOffsetMeters;
            this.forwardOffsetMeters = forwardOffsetMeters;
            this.uniformScale = uniformScale;
            this.bindToPlayer = bindToPlayer;
        }

        public readonly string key;
        public readonly string controlFamilyId;
        public readonly string packageFolderName;
        public readonly string instanceId;
        public readonly float rightOffsetMeters;
        public readonly float upOffsetMeters;
        public readonly float forwardOffsetMeters;
        public readonly float uniformScale;
        public readonly bool bindToPlayer;
    }

    private sealed class MetaToolkitLocalPackageDefinition
    {
        public string key = "";
        public string packageFolderName = "";
        public string packagePath = "";
        public string controlSurfaceId = "";
        public string controlFamilyId = "";
        public string toolkitCategory = "";
        public string defaultInstanceId = "";
        public float surfaceWidthMeters = 0f;
        public float surfaceHeightMeters = 0f;
        public int elementCount = 0;
        public bool bindToPlayer = false;
        public bool curated = false;
    }

    private sealed class MetaPlayerRuntimeSurfaceConfig
    {
        public string schemaVersion = "";
        public string packagePath = "";
        public string packageId = "";
        public string resourceId = "";
        public string controlSurfaceId = "";
        public string controlFamilyId = "";
        public string defaultTargetDisplayId = "";
        public string displayName = "";
        public string sourceSummaryPath = "";
        public string videoSurfaceNodeId = "";
        public float videoRectX = 0f;
        public float videoRectY = 0f;
        public float videoRectWidth = 0f;
        public float videoRectHeight = 0f;
        public bool hasVideoRect = false;
    }

#if !FRAMEANGEL_TEST_SURFACES
#pragma warning disable 649, 169
#endif
    private JSONStorableString syncMetaProofPackagePathField;
    private JSONStorableString syncMetaProofResourceIdField;
    private JSONStorableString syncMetaProofInstanceIdField;
    private JSONStorableString syncMetaProofPlayerAtomUidField;
    private JSONStorableString syncMetaProofMediaPathField;
    private JSONStorableString syncMetaProofStatusField;
    private JSONStorableAction syncMetaProofImportAction;
    private JSONStorableAction syncMetaProofSpawnAction;
    private JSONStorableAction syncMetaProofBindAction;
    private JSONStorableAction syncMetaProofLayoutAction;
    private JSONStorableAction syncMetaProofLoadMediaAction;
    private JSONStorableAction syncMetaProofPlayPauseAction;
    private JSONStorableAction syncMetaProofScrubHalfAction;
    private JSONStorableAction syncMetaProofScrubBackAction;
    private JSONStorableAction syncMetaProofScrubForwardAction;
    private JSONStorableAction syncMetaProofVolumeLowAction;
    private JSONStorableAction syncMetaProofVolumeHighAction;
    private JSONStorableAction syncMetaProofSmokeAction;
#if !FRAMEANGEL_TEST_SURFACES
#pragma warning restore 649, 169
#endif

    private string syncMetaProofPackagePath = "";
    private string syncMetaProofResourceId = "";
    private string syncMetaProofInstanceId = MetaProofControlSurfaceDefaultInstanceId;
    private string syncMetaProofPlayerAtomUid = "";
    private string syncMetaProofTargetScreenResourceId = "";
    private string syncMetaProofMediaPath = "";
    private string syncMetaProofStatus = "Meta proof idle";
#if FRAMEANGEL_TEST_SURFACES
    private Coroutine syncMetaProofSmokeCoroutine;
    private bool syncMetaProofAutoStartPending = false;

    private void BuildMetaProofStorables()
    {
#if FRAMEANGEL_TEST_SURFACES
        syncMetaProofPackagePathField = new JSONStorableString("Meta Proof Package Path", syncMetaProofPackagePath);
        syncMetaProofPackagePathField.setCallbackFunction = delegate(string v)
        {
            syncMetaProofPackagePath = string.IsNullOrEmpty(v) ? "" : v.Trim();
        };
        ConfigureTransientField(syncMetaProofPackagePathField, false);

        syncMetaProofResourceIdField = new JSONStorableString("Meta Proof Resource Id", syncMetaProofResourceId);
        syncMetaProofResourceIdField.setCallbackFunction = delegate(string v)
        {
            syncMetaProofResourceId = string.IsNullOrEmpty(v) ? "" : v.Trim();
        };
        ConfigureTransientField(syncMetaProofResourceIdField, false);

        syncMetaProofInstanceIdField = new JSONStorableString("Meta Proof Instance Id", syncMetaProofInstanceId);
        syncMetaProofInstanceIdField.setCallbackFunction = delegate(string v)
        {
            syncMetaProofInstanceId = string.IsNullOrEmpty(v) ? "" : v.Trim();
        };
        ConfigureTransientField(syncMetaProofInstanceIdField, false);

        syncMetaProofPlayerAtomUidField = new JSONStorableString("Meta Proof Target Screen Instance Id", syncMetaProofPlayerAtomUid);
        syncMetaProofPlayerAtomUidField.setCallbackFunction = delegate(string v)
        {
            syncMetaProofPlayerAtomUid = string.IsNullOrEmpty(v) ? "" : v.Trim();
        };
        ConfigureTransientField(syncMetaProofPlayerAtomUidField, false);

        syncMetaProofMediaPathField = new JSONStorableString("Meta Proof Media Path", syncMetaProofMediaPath);
        syncMetaProofMediaPathField.setCallbackFunction = delegate(string v)
        {
            string normalized = string.IsNullOrEmpty(v) ? "" : v.Trim();
            syncMetaProofMediaPath = normalized;
        };
        ConfigureTransientField(syncMetaProofMediaPathField, false);

        syncMetaProofStatusField = new JSONStorableString("Meta Proof Status", syncMetaProofStatus);
        ConfigureTransientField(syncMetaProofStatusField, false);

        syncMetaProofImportAction = new JSONStorableAction(
            "Meta Proof Import Package",
            delegate
            {
                RunMetaProofImport();
            }
        );
        syncMetaProofSpawnAction = new JSONStorableAction(
            "Meta Proof Spawn Instance",
            delegate
            {
                RunMetaProofSpawn();
            }
        );
        syncMetaProofBindAction = new JSONStorableAction(
            "Meta Proof Bind Player",
            delegate
            {
                RunMetaProofBind();
            }
        );
        syncMetaProofLayoutAction = new JSONStorableAction(
            "Meta Proof Layout Panel",
            delegate
            {
                RunMetaProofLayout();
            }
        );
        syncMetaProofLoadMediaAction = new JSONStorableAction(
            "Meta Proof Load Media",
            delegate
            {
                RunMetaProofLoadMedia();
            }
        );
        syncMetaProofPlayPauseAction = new JSONStorableAction(
            "Meta Proof Play/Pause",
            delegate
            {
                RunMetaProofTrigger("play_pause_button", "{}");
            }
        );
        syncMetaProofScrubHalfAction = new JSONStorableAction(
            "Meta Proof Scrub 50%",
            delegate
            {
                RunMetaProofTrigger("scrub_slider", "{\"normalized\":0.5}");
            }
        );
        syncMetaProofScrubBackAction = new JSONStorableAction(
            "Meta Proof Scrub -10s",
            delegate
            {
                RunMetaProofDirectPlayerAction(PlayerActionSkipBackwardId, "", "Meta proof scrubbed backward");
            }
        );
        syncMetaProofScrubForwardAction = new JSONStorableAction(
            "Meta Proof Scrub +10s",
            delegate
            {
                RunMetaProofDirectPlayerAction(PlayerActionSkipForwardId, "", "Meta proof scrubbed forward");
            }
        );
        syncMetaProofVolumeLowAction = new JSONStorableAction(
            "Meta Proof Volume 25%",
            delegate
            {
                RunMetaProofDirectPlayerAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(MetaProofVolumeLowNormalized),
                    "Meta proof volume set to 25%");
            }
        );
        syncMetaProofVolumeHighAction = new JSONStorableAction(
            "Meta Proof Volume 75%",
            delegate
            {
                RunMetaProofDirectPlayerAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(MetaProofVolumeHighNormalized),
                    "Meta proof volume set to 75%");
            }
        );
        syncMetaProofSmokeAction = new JSONStorableAction(
            "Meta Proof Smoke",
            delegate
            {
                RunMetaProofSmoke();
            }
        );
#endif
    }

    private void RegisterMetaProofStorables()
    {
#if FRAMEANGEL_TEST_SURFACES
        RegisterString(syncMetaProofPackagePathField);
        RegisterString(syncMetaProofResourceIdField);
        RegisterString(syncMetaProofInstanceIdField);
        RegisterString(syncMetaProofPlayerAtomUidField);
        RegisterString(syncMetaProofMediaPathField);
        RegisterString(syncMetaProofStatusField);
        RegisterAction(syncMetaProofImportAction);
        RegisterAction(syncMetaProofSpawnAction);
        RegisterAction(syncMetaProofBindAction);
        RegisterAction(syncMetaProofLayoutAction);
        RegisterAction(syncMetaProofLoadMediaAction);
        RegisterAction(syncMetaProofPlayPauseAction);
        RegisterAction(syncMetaProofScrubHalfAction);
        RegisterAction(syncMetaProofScrubBackAction);
        RegisterAction(syncMetaProofScrubForwardAction);
        RegisterAction(syncMetaProofVolumeLowAction);
        RegisterAction(syncMetaProofVolumeHighAction);
        RegisterAction(syncMetaProofSmokeAction);
#endif
    }

    private void BuildMetaProofUi()
    {
#if FRAMEANGEL_TEST_SURFACES
        CreateButton("Meta Proof Import Package").button.onClick.AddListener(
            delegate
            {
                RunMetaProofImport();
            }
        );
        CreateButton("Meta Proof Spawn Instance").button.onClick.AddListener(
            delegate
            {
                RunMetaProofSpawn();
            }
        );
        CreateButton("Meta Proof Bind Player").button.onClick.AddListener(
            delegate
            {
                RunMetaProofBind();
            }
        );
        CreateButton("Meta Proof Layout Panel").button.onClick.AddListener(
            delegate
            {
                RunMetaProofLayout();
            }
        );
        CreateButton("Meta Proof Load Media").button.onClick.AddListener(
            delegate
            {
                RunMetaProofLoadMedia();
            }
        );
        CreateButton("Meta Proof Play/Pause").button.onClick.AddListener(
            delegate
            {
                RunMetaProofTrigger("play_pause_button", "{}");
            }
        );
        CreateButton("Meta Proof Scrub 50%").button.onClick.AddListener(
            delegate
            {
                RunMetaProofTrigger("scrub_slider", "{\"normalized\":0.5}");
            }
        );
        CreateButton("Meta Proof Scrub -10s").button.onClick.AddListener(
            delegate
            {
                RunMetaProofDirectPlayerAction(PlayerActionSkipBackwardId, "", "Meta proof scrubbed backward");
            }
        );
        CreateButton("Meta Proof Scrub +10s").button.onClick.AddListener(
            delegate
            {
                RunMetaProofDirectPlayerAction(PlayerActionSkipForwardId, "", "Meta proof scrubbed forward");
            }
        );
        CreateButton("Meta Proof Volume 25%").button.onClick.AddListener(
            delegate
            {
                RunMetaProofDirectPlayerAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(MetaProofVolumeLowNormalized),
                    "Meta proof volume set to 25%");
            }
        );
        CreateButton("Meta Proof Volume 75%").button.onClick.AddListener(
            delegate
            {
                RunMetaProofDirectPlayerAction(
                    PlayerActionSetVolumeId,
                    "\"volume\":" + FormatFloat(MetaProofVolumeHighNormalized),
                    "Meta proof volume set to 75%");
            }
        );
        CreateButton("Meta Proof Smoke").button.onClick.AddListener(
            delegate
            {
                RunMetaProofSmoke();
            }
        );
        CreateButton("Player Quick Demo").button.onClick.AddListener(
            delegate
            {
                RunPlayerQuickDemo();
            }
        );
        CreateTextField(syncMetaProofStatusField, false);
        CreateTextField(syncMetaProofPlayerAtomUidField, true);
        CreateTextField(syncMetaProofInstanceIdField, true);
        CreateTextField(syncMetaProofResourceIdField, true);
        CreateTextField(syncMetaProofPackagePathField, true);
        CreateTextField(syncMetaProofMediaPathField, true);
#endif
    }

    private void RunMetaProofImport()
    {
        string packagePath = string.IsNullOrEmpty(syncMetaProofPackagePath) ? "" : syncMetaProofPackagePath.Trim();
        if (string.IsNullOrEmpty(packagePath))
        {
            SetMetaProofStatus("Meta proof import needs package path");
            return;
        }

        string argsJson = "{\"packagePath\":\"" + EscapeJsonString(packagePath) + "\"}";
        if (!TryExecuteMetaProofAction(HostPackageImportActionId, argsJson, out string resultJson))
            return;

        string resourceId = ExtractJsonArgString(resultJson, "resourceId");
        if (!string.IsNullOrEmpty(resourceId))
        {
            syncMetaProofResourceId = resourceId;
            if (syncMetaProofResourceIdField != null)
                syncMetaProofResourceIdField.valNoCallback = resourceId;
        }

        SetMetaProofStatus("Meta proof imported");
    }

    private void RunMetaProofSpawn()
    {
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostedError;
            HostedPlayerSurfaceContract hostedContract;
            if (!TryResolveHostedPlayerSurfaceContract(attachedHostAtom.uid ?? "", out hostedContract, out hostedError) || hostedContract == null)
            {
                SetMetaProofStatus(string.IsNullOrEmpty(hostedError) ? "Hosted player surface not ready" : hostedError);
                return;
            }

            SetMetaProofStatus("Hosted player authored surface is core-owned");
            return;
        }

        TryHydrateMetaProofDefaults();

        string instanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
        if (string.IsNullOrEmpty(instanceId))
        {
            SetMetaProofStatus("Meta proof spawn needs instance id");
            return;
        }

        if (!TryEnsureMetaProofControlSurfaceInstance(instanceId))
            return;

        SetMetaProofStatus("Meta proof spawned");
    }

    private void RunMetaProofBind()
    {
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostAtomUid = string.IsNullOrEmpty(attachedHostAtom.uid) ? "" : attachedHostAtom.uid.Trim();
            string hostedInstanceId = string.IsNullOrEmpty(hostAtomUid) ? "" : BuildHostedPlayerInstanceId(hostAtomUid);
            if (string.IsNullOrEmpty(hostedInstanceId)
                || !playerControlSurfaceBindings.TryGetValue(hostedInstanceId, out PlayerControlSurfaceBindingRecord hostedBinding)
                || hostedBinding == null)
            {
                SetMetaProofStatus("Hosted player control surface not ready");
                return;
            }

            SetMetaProofStatus("Hosted player binding is core-owned");
            return;
        }

        TryHydrateMetaProofDefaults();

        string instanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
        if (string.IsNullOrEmpty(instanceId))
        {
            SetMetaProofStatus("Meta proof bind needs instance id");
            return;
        }

        if (!TryEnsureMetaProofControlSurfaceInstance(instanceId))
            return;

        string targetInstanceId;
        if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
            return;

        if (!TryLayoutMetaProofControlSurface(instanceId, targetInstanceId, out _))
            return;

        if (!TryBindMetaProofToPlayer(instanceId, targetInstanceId, out _))
            return;

        if (!TryEnsureMetaProofStandalonePlayerMedia(targetInstanceId))
            return;

        if (!string.Equals(syncMetaProofStatus, "Meta proof loading sample media", StringComparison.Ordinal))
            SetMetaProofStatus("Meta proof bound to standalone player");
    }

    private void RunMetaProofLayout()
    {
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostedError;
            HostedPlayerSurfaceContract hostedContract;
            if (!TryResolveHostedPlayerSurfaceContract(attachedHostAtom.uid ?? "", out hostedContract, out hostedError) || hostedContract == null)
            {
                SetMetaProofStatus(string.IsNullOrEmpty(hostedError) ? "Hosted player surface not ready" : hostedError);
                return;
            }

            SetMetaProofStatus("Hosted player layout is core-owned");
            return;
        }

        TryHydrateMetaProofDefaults();

        string instanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
        if (string.IsNullOrEmpty(instanceId))
        {
            SetMetaProofStatus("Meta proof layout needs instance id");
            return;
        }

        if (!TryEnsureMetaProofControlSurfaceInstance(instanceId))
            return;

        string targetInstanceId;
        if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
            return;

        if (!TryLayoutMetaProofControlSurface(instanceId, targetInstanceId, out _))
            return;

        SetMetaProofStatus("Meta proof control panel laid out");
    }

    private void RunMetaProofLoadMedia()
    {
        if (TryOpenPlayerMediaBrowser("Meta proof loading selected media", true))
            return;

        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            List<string> mediaPaths;
            bool usingConfiguredMedia;
            string errorMessage;
            if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out errorMessage))
            {
                SetMetaProofStatus(string.IsNullOrEmpty(errorMessage) ? "Player media path is unavailable" : errorMessage);
                return;
            }

            string selectedMediaPath = mediaPaths.Count > 0 ? (mediaPaths[0] ?? "") : "";
            if (string.IsNullOrEmpty(selectedMediaPath))
            {
                SetMetaProofStatus("Player media path is unavailable");
                return;
            }

            RunAttachedPlayerLoadMedia(selectedMediaPath, "Meta proof loading selected media");
            return;
        }

        RunPlayerLoadMedia("", "Meta proof loading selected media", true);
    }

    private void RunMetaProofSmoke()
    {
        if (syncMetaProofSmokeCoroutine != null)
            StopCoroutine(syncMetaProofSmokeCoroutine);

        syncMetaProofSmokeCoroutine = StartCoroutine(RunMetaProofSmokeCoroutine());
    }

    private bool TryRunMetaProofSmokeAction(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        errorMessage = "";
        RunMetaProofSmoke();
        resultJson = BuildBrokerResult(true, "meta proof smoke started", BuildMetaProofRuntimePayloadJson());
        return true;
    }

    private IEnumerator RunMetaProofSmokeCoroutine()
    {
        SetMetaProofStatus("Meta proof smoke starting");

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

            RunAttachedPlayerLoadMedia(selectedMediaPath, "Meta proof smoke loading media");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerDirectAction(PlayerActionPlayId, "", "Meta proof smoke play");
            yield return new WaitForSecondsRealtime(0.75f);

            RunAttachedPlayerDirectAction(PlayerActionPauseId, "", "Meta proof smoke pause");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerSeekNormalizedAction(0.5f, "Meta proof smoke scrub 50%");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerDirectAction(
                PlayerActionSkipBackwardId,
                "",
                "Meta proof smoke skip -10s");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerDirectAction(
                PlayerActionSkipForwardId,
                "",
                "Meta proof smoke skip +10s");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerSetVolumeAction(PlayerVolumeLowNormalized, "Meta proof smoke volume 25%");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerSetVolumeAction(PlayerVolumeHighNormalized, "Meta proof smoke volume 75%");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerDirectAction(
                PlayerActionSetMuteId,
                "\"muted\":true",
                "Meta proof smoke mute on");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerDirectAction(
                PlayerActionSetMuteId,
                "\"muted\":false",
                "Meta proof smoke mute off");
            yield return new WaitForSecondsRealtime(0.35f);

            RunAttachedPlayerAspectModeAction(GhostScreenAspectModeFit, "Meta proof smoke aspect fit");
            yield return new WaitForSecondsRealtime(0.35f);

            SetMetaProofStatus("Meta proof smoke complete");
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
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(PlayerActionPlayId, "", "Meta proof smoke play");
        yield return new WaitForSecondsRealtime(0.75f);

        RunMetaProofDirectPlayerAction(PlayerActionPauseId, "", "Meta proof smoke pause");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofTrigger("scrub_slider", "{\"normalized\":0.5}");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSkipBackwardId,
            "",
            "Meta proof smoke skip -10s");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSkipForwardId,
            "",
            "Meta proof smoke skip +10s");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetVolumeId,
            "\"volume\":" + FormatFloat(MetaProofVolumeLowNormalized),
            "Meta proof smoke volume 25%");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetVolumeId,
            "\"volume\":" + FormatFloat(MetaProofVolumeHighNormalized),
            "Meta proof smoke volume 75%");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetMuteId,
            "\"muted\":true",
            "Meta proof smoke mute on");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetMuteId,
            "\"muted\":false",
            "Meta proof smoke mute off");
        yield return new WaitForSecondsRealtime(0.35f);

        RunMetaProofDirectPlayerAction(
            PlayerActionSetAspectModeId,
            "\"aspectMode\":\"fit\"",
            "Meta proof smoke aspect fit");
        yield return new WaitForSecondsRealtime(0.35f);

        SetMetaProofStatus("Meta proof smoke complete");
        syncMetaProofSmokeCoroutine = null;
    }
#endif

    private void TryHydrateMetaProofDefaults()
    {
        bool hadActiveSurfaceConfig = false;
        MetaPlayerRuntimeSurfaceConfig activeSurfaceConfig;
        if (TryResolveMetaPlayerRuntimeActiveSurfaceConfig(out activeSurfaceConfig) && activeSurfaceConfig != null)
        {
            hadActiveSurfaceConfig = true;
            syncMetaProofPackagePath = activeSurfaceConfig.packagePath ?? "";
            if (syncMetaProofPackagePathField != null)
                syncMetaProofPackagePathField.valNoCallback = syncMetaProofPackagePath;

            syncMetaProofResourceId = activeSurfaceConfig.resourceId ?? "";
            if (syncMetaProofResourceIdField != null)
                syncMetaProofResourceIdField.valNoCallback = syncMetaProofResourceId;
        }

        if (!string.IsNullOrEmpty(syncMetaProofPackagePath)
            && (!IsSecureRuntimePathCandidate(syncMetaProofPackagePath) || IsLegacyMetaProofPackagePath(syncMetaProofPackagePath)))
        {
            syncMetaProofPackagePath = "";
            if (syncMetaProofPackagePathField != null)
                syncMetaProofPackagePathField.valNoCallback = "";
        }

        bool hasExplicitMetaProofState = hadActiveSurfaceConfig
            || !string.IsNullOrEmpty(syncMetaProofPackagePath)
            || !string.IsNullOrEmpty(syncMetaProofResourceId)
            || !string.IsNullOrEmpty(syncMetaProofPlayerAtomUid)
            || (!string.IsNullOrEmpty(syncMetaProofInstanceId)
                && !string.Equals(syncMetaProofInstanceId, MetaProofControlSurfaceDefaultInstanceId, StringComparison.OrdinalIgnoreCase));
        if (!hasExplicitMetaProofState)
            return;

        if (string.IsNullOrEmpty(syncMetaProofResourceId))
        {
            syncMetaProofResourceId = MetaProofCurrentResourceId;
            if (syncMetaProofResourceIdField != null)
                syncMetaProofResourceIdField.valNoCallback = syncMetaProofResourceId;
        }

        if (string.IsNullOrEmpty(syncMetaProofTargetScreenResourceId))
            syncMetaProofTargetScreenResourceId = MetaProofGhostScreenRectResourceId;

        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostUid = attachedHostAtom.uid ?? MetaProofPreferredHostAtomUid;
            string targetInstanceId = BuildMetaProofHostedTargetInstanceId(hostUid);
            string controlSurfaceInstanceId = BuildMetaProofHostedControlSurfaceInstanceId(hostUid);

            syncMetaProofPlayerAtomUid = targetInstanceId;
            if (syncMetaProofPlayerAtomUidField != null)
                syncMetaProofPlayerAtomUidField.valNoCallback = targetInstanceId;

            syncMetaProofInstanceId = controlSurfaceInstanceId;
            if (syncMetaProofInstanceIdField != null)
                syncMetaProofInstanceIdField.valNoCallback = controlSurfaceInstanceId;
        }
        else if (string.IsNullOrEmpty(syncMetaProofPlayerAtomUid))
        {
            string targetInstanceId = ResolveDefaultMetaProofTargetInstanceId();
            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                syncMetaProofPlayerAtomUid = targetInstanceId;
                if (syncMetaProofPlayerAtomUidField != null)
                    syncMetaProofPlayerAtomUidField.valNoCallback = targetInstanceId;
            }
        }
    }

    private string ResolveDefaultMetaProofPackagePath()
    {
        return ResolveMetaProofLocalPackagePath();
    }

    private bool TryResolveMetaPlayerRuntimeActiveSurfaceConfig(out MetaPlayerRuntimeSurfaceConfig config)
    {
        config = null;

        if (!FileManagerSecure.FileExists(MetaPlayerRuntimeActiveSurfaceConfigPath, false))
            return false;

        string json = "";
        try
        {
            json = SuperController.singleton != null
                ? (SuperController.singleton.ReadFileIntoString(MetaPlayerRuntimeActiveSurfaceConfigPath) ?? "")
                : "";
        }
        catch
        {
            json = "";
        }

        if (string.IsNullOrEmpty(json))
            return false;

        string schemaVersion = ExtractJsonArgString(json, "schemaVersion");
        if (!string.Equals(schemaVersion, MetaPlayerRuntimeActiveSurfaceSchemaVersion, StringComparison.OrdinalIgnoreCase))
            return false;

        string resourceId = ExtractJsonArgString(json, "resourceId");
        bool hasStoredResource = !string.IsNullOrEmpty(resourceId)
            && FileManagerSecure.FileExists(FAInnerPieceStorage.ResolveResourcePath(resourceId), false);

        string packagePath = ExtractJsonArgString(json, "packagePath");
        if (!string.IsNullOrEmpty(packagePath))
            packagePath = packagePath.Trim();
        bool hasPackagePath = !string.IsNullOrEmpty(packagePath);
        if (hasPackagePath
            && (!IsSecureRuntimePathCandidate(packagePath) || IsLegacyMetaProofPackagePath(packagePath)))
        {
            packagePath = "";
            hasPackagePath = false;
        }

        if (!hasStoredResource && !hasPackagePath)
            return false;

        if (hasPackagePath)
        {
            string[] manifests = null;
            try
            {
                manifests = FileManagerSecure.GetFiles(packagePath, "manifest.json");
            }
            catch
            {
                manifests = null;
            }

            if ((manifests == null || manifests.Length <= 0) && !hasStoredResource)
                return false;
            if (manifests == null || manifests.Length <= 0)
            {
                packagePath = "";
                hasPackagePath = false;
            }
        }

        if (!hasStoredResource && !hasPackagePath)
            return false;

        config = new MetaPlayerRuntimeSurfaceConfig();
        config.schemaVersion = schemaVersion;
        config.packagePath = hasPackagePath ? packagePath : "";
        config.packageId = ExtractJsonArgString(json, "packageId");
        config.resourceId = resourceId;
        config.controlSurfaceId = ExtractJsonArgString(json, "controlSurfaceId");
        config.controlFamilyId = ExtractJsonArgString(json, "controlFamilyId");
        config.defaultTargetDisplayId = ExtractJsonArgString(json, "defaultTargetDisplayId");
        config.displayName = ExtractJsonArgString(json, "displayName");
        config.sourceSummaryPath = ExtractJsonArgString(json, "sourceSummaryPath");
        config.videoSurfaceNodeId = ExtractJsonArgString(json, "videoSurfaceNodeId");
        TryExtractJsonFloatField(json, "videoRectX", out config.videoRectX);
        TryExtractJsonFloatField(json, "videoRectY", out config.videoRectY);
        TryExtractJsonFloatField(json, "videoRectWidth", out config.videoRectWidth);
        TryExtractJsonFloatField(json, "videoRectHeight", out config.videoRectHeight);
        config.hasVideoRect = config.videoRectWidth > 0f && config.videoRectHeight > 0f;
        return true;
    }

    private string ResolveDefaultMetaProofSampleMediaPath()
    {
        List<string> mediaPaths = ResolveMetaProofSampleMediaPaths();
        for (int i = 0; i < mediaPaths.Count; i++)
        {
            if (!string.IsNullOrEmpty(mediaPaths[i]))
                return mediaPaths[i];
        }

        return "";
    }

    private List<string> ResolveMetaProofSampleMediaPaths()
    {
        List<string> resolved = new List<string>();
        for (int i = 0; i < MetaProofSampleMediaPathCandidates.Length; i++)
        {
            string candidate = string.IsNullOrEmpty(MetaProofSampleMediaPathCandidates[i])
                ? ""
                : MetaProofSampleMediaPathCandidates[i].Trim();
            if (string.IsNullOrEmpty(candidate) || !FileManagerSecure.FileExists(candidate, false))
                continue;

            bool alreadyPresent = false;
            for (int j = 0; j < resolved.Count; j++)
            {
                if (AreEquivalentMetaProofMediaPaths(resolved[j], candidate))
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
                resolved.Add(candidate);
        }

        return resolved;
    }

    private string BuildMetaProofSamplePlaylistJson(List<string> mediaPaths)
    {
        if (mediaPaths == null || mediaPaths.Count <= 0)
            return "[]";

        StringBuilder sb = new StringBuilder(128);
        sb.Append('[');
        bool first = true;
        for (int i = 0; i < mediaPaths.Count; i++)
        {
            string candidate = string.IsNullOrEmpty(mediaPaths[i]) ? "" : mediaPaths[i].Trim();
            if (string.IsNullOrEmpty(candidate))
                continue;

            if (!first)
                sb.Append(',');
            sb.Append('"').Append(EscapeJsonString(candidate)).Append('"');
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    private string ResolveDefaultMetaProofTargetInstanceId()
    {
        InnerPieceInstanceRecord liveInstance;
        InnerPieceScreenSlotRuntimeRecord liveSlot;
        string ignoredError;
        if (TryResolveStandalonePlayerTargetScreenSlotForControlSurface(
            InnerPiecePrimaryPlayerDisplayId,
            out liveInstance,
            out liveSlot,
            out ignoredError)
            && liveInstance != null)
        {
            return liveInstance.instanceId ?? "";
        }

        StandalonePlayerRecord match = null;
        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord candidate = kvp.Value;
            if (candidate == null)
                continue;
            if (!AreEquivalentInnerPieceDisplayIds(candidate.displayId, "player_main")
                && !AreEquivalentInnerPieceDisplayIds(candidate.slotId, "player_main"))
                continue;

            if (match != null && !string.Equals(match.instanceId, candidate.instanceId, StringComparison.OrdinalIgnoreCase))
                return "";

            match = candidate;
        }

        return match != null ? (match.instanceId ?? "") : "";
    }

    private static bool IsLegacyMetaProofPackagePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (string.IsNullOrEmpty(MetaProofLegacyPackagePath))
            return false;

        string normalized = FAInnerPieceStorage.NormalizePath(path);
        string legacy = FAInnerPieceStorage.NormalizePath(MetaProofLegacyPackagePath);
        return string.Equals(normalized, legacy, StringComparison.OrdinalIgnoreCase);
    }

#if FRAMEANGEL_TEST_SURFACES
    private string BuildMetaProofRuntimePayloadJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"instanceId\":\"").Append(EscapeJsonString(syncMetaProofInstanceId ?? "")).Append("\",");
        sb.Append("\"playerAtomUid\":\"").Append(EscapeJsonString(syncMetaProofPlayerAtomUid ?? "")).Append("\",");
        sb.Append("\"resourceId\":\"").Append(EscapeJsonString(syncMetaProofResourceId ?? "")).Append("\",");
        sb.Append("\"packagePath\":\"").Append(EscapeJsonString(syncMetaProofPackagePath ?? "")).Append("\",");
        sb.Append("\"status\":\"").Append(EscapeJsonString(syncMetaProofStatus ?? "")).Append("\",");
        sb.Append("\"smokeRunning\":").Append(syncMetaProofSmokeCoroutine != null ? "true" : "false");
        sb.Append('}');
        return sb.ToString();
    }
#endif

    private bool TryResolveMetaProofPreferredHostTransform(out string atomUid, out Transform anchorTransform)
    {
        atomUid = "";
        anchorTransform = null;

        Atom hostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out hostAtom) && hostAtom != null)
        {
            anchorTransform = hostAtom.mainController != null ? hostAtom.mainController.transform : hostAtom.transform;
            if (anchorTransform == null)
                return false;

            atomUid = hostAtom.uid ?? "";
            return !string.IsNullOrEmpty(atomUid);
        }

        if (!TryFindSceneAtomByUid(MetaProofPreferredHostAtomUid, out hostAtom) || hostAtom == null)
            return false;

        anchorTransform = hostAtom.mainController != null ? hostAtom.mainController.transform : hostAtom.transform;
        if (anchorTransform == null)
            return false;

        atomUid = string.IsNullOrEmpty(hostAtom.uid) ? MetaProofPreferredHostAtomUid : hostAtom.uid;
        return !string.IsNullOrEmpty(atomUid);
    }

    private bool TryResolveAttachedMetaProofHostAtom(out Atom hostAtom)
    {
        hostAtom = containingAtom;
        if (hostAtom == null)
            return false;

        string uid = hostAtom.uid ?? "";
        if (string.Equals(uid, "Session", StringComparison.OrdinalIgnoreCase))
        {
            hostAtom = null;
            return false;
        }

        return true;
    }

    private string BuildMetaProofHostedTargetInstanceId(string hostAtomUid)
    {
        string normalized = string.IsNullOrEmpty(hostAtomUid) ? MetaProofPreferredHostAtomUid : hostAtomUid.Trim();
        normalized = Regex.Replace(normalized, "[^A-Za-z0-9_]+", "_");
        if (string.IsNullOrEmpty(normalized))
            normalized = MetaProofPreferredHostAtomUid;

        return "ghost_screen_rect_" + normalized.ToLowerInvariant();
    }

    private string BuildMetaProofHostedControlSurfaceInstanceId(string hostAtomUid)
    {
        string normalized = string.IsNullOrEmpty(hostAtomUid) ? MetaProofPreferredHostAtomUid : hostAtomUid.Trim();
        normalized = Regex.Replace(normalized, "[^A-Za-z0-9_]+", "_");
        if (string.IsNullOrEmpty(normalized))
            normalized = MetaProofPreferredHostAtomUid;

        return "meta_video_player_proof_" + normalized.ToLowerInvariant();
    }

    private bool IsHostedMetaProofControlSurfaceInstanceId(string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId)
            && instanceId.StartsWith("meta_video_player_proof_", StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldDisableHostedMetaProofPanelDrag(string controlSurfaceInstanceId)
    {
        return BuildRuntimeInfo.IsDistributionBuild
            && IsHostedMetaProofControlSurfaceInstanceId(controlSurfaceInstanceId);
    }

    private bool TryNormalizeMetaProofTargetScreenForQuickDemo(string targetInstanceId, out string errorMessage)
    {
        errorMessage = "";
        targetInstanceId = string.IsNullOrEmpty(targetInstanceId) ? "" : targetInstanceId.Trim();
        if (string.IsNullOrEmpty(targetInstanceId))
        {
            errorMessage = "Meta proof target screen instance id is required";
            return false;
        }

        InnerPieceInstanceRecord targetInstance;
        if (!TryResolveInnerPieceInstance(
                "{\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\"}",
                out targetInstance,
                out errorMessage) || targetInstance == null)
        {
            return false;
        }

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        TryBuildMetaProofTargetScreenSpawnPose(out desiredPosition, out desiredRotation);

        string transformArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"position\":{\"x\":" + FormatFloat(desiredPosition.x)
            + ",\"y\":" + FormatFloat(desiredPosition.y)
            + ",\"z\":" + FormatFloat(desiredPosition.z) + "}"
            + ",\"rotX\":" + FormatFloat(desiredRotation.x)
            + ",\"rotY\":" + FormatFloat(desiredRotation.y)
            + ",\"rotZ\":" + FormatFloat(desiredRotation.z)
            + ",\"rotW\":" + FormatFloat(desiredRotation.w)
            + ",\"scaleX\":1"
            + ",\"scaleY\":1"
            + ",\"scaleZ\":1"
            + "}";
        if (!TryExecuteMetaProofAction(HostInstanceTransformActionId, transformArgsJson, out _))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof target screen transform failed" : syncMetaProofStatus;
            return false;
        }

        string preferredHostAtomUid;
        Transform preferredHostTransform;
        if (!TryResolveMetaProofPreferredHostTransform(out preferredHostAtomUid, out preferredHostTransform))
        {
            string clearFollowArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                + ",\"clear\":true"
                + "}";
            TryExecuteMetaProofAction(HostInstanceSetFollowActionId, clearFollowArgsJson, out _);
            return true;
        }

        Quaternion localRotationOffset = Quaternion.Inverse(preferredHostTransform.rotation) * desiredRotation;
        Vector3 localPositionOffset = Quaternion.Inverse(preferredHostTransform.rotation) * (desiredPosition - preferredHostTransform.position);
        Vector3 localRotationEuler = localRotationOffset.eulerAngles;

        string followArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"anchorAtomUid\":\"" + EscapeJsonString(preferredHostAtomUid) + "\""
            + ",\"enabled\":true"
            + ",\"followPosition\":true"
            + ",\"followRotation\":true"
            + ",\"localPositionOffset\":{\"x\":" + FormatFloat(localPositionOffset.x)
            + ",\"y\":" + FormatFloat(localPositionOffset.y)
            + ",\"z\":" + FormatFloat(localPositionOffset.z) + "}"
            + ",\"localRotationEuler\":{\"x\":" + FormatFloat(localRotationEuler.x)
            + ",\"y\":" + FormatFloat(localRotationEuler.y)
            + ",\"z\":" + FormatFloat(localRotationEuler.z) + "}"
            + "}";
        if (!TryExecuteMetaProofAction(HostInstanceSetFollowActionId, followArgsJson, out _))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof target follow bind failed" : syncMetaProofStatus;
            return false;
        }

        return true;
    }

    private string ResolveMetaProofLocalPackagePath()
    {
        return ResolvePreferredDirectory(
            MetaProofLocalPackagePath,
            LegacyMetaProofLocalPackagePath,
            MetaProofCurrentPackagePath,
            MetaProofLegacyPackagePath);
    }

    private string ResolveMetaProofGhostScreenLocalPackagePath()
    {
        return ResolvePreferredDirectory(
            MetaProofGhostScreenRectPackagePath,
            MetaProofGhostScreenLocalPackagePath,
            LegacyMetaProofGhostScreenLocalPackagePath);
    }

    private void RunMetaProofTrigger(string elementId, string extraArgsJson)
    {
        string instanceId = "";
        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostAtomUid = string.IsNullOrEmpty(attachedHostAtom.uid) ? "" : attachedHostAtom.uid.Trim();
            if (string.IsNullOrEmpty(hostAtomUid))
            {
                SetMetaProofStatus("Hosted player host atom uid missing");
                return;
            }

            instanceId = BuildHostedPlayerInstanceId(hostAtomUid);
            if (!playerControlSurfaceBindings.TryGetValue(instanceId, out PlayerControlSurfaceBindingRecord hostedBinding) || hostedBinding == null)
            {
                SetMetaProofStatus("Hosted player control surface not ready");
                return;
            }
        }
        else
        {
            instanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
            if (string.IsNullOrEmpty(instanceId))
            {
                SetMetaProofStatus("Meta proof trigger needs instance id");
                return;
            }

            if (!TryEnsureMetaProofControlSurfaceInstance(instanceId))
                return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"controlSurfaceInstanceId\":\"").Append(EscapeJsonString(instanceId)).Append("\",");
        sb.Append("\"elementId\":\"").Append(EscapeJsonString(elementId ?? "")).Append("\"");
        string trimmedExtraArgs = string.IsNullOrEmpty(extraArgsJson) ? "" : extraArgsJson.Trim();
        if (!string.IsNullOrEmpty(trimmedExtraArgs) && trimmedExtraArgs != "{}")
        {
            string body = trimmedExtraArgs;
            if (body.StartsWith("{", StringComparison.Ordinal) && body.EndsWith("}", StringComparison.Ordinal))
                body = body.Substring(1, body.Length - 2).Trim();
            if (!string.IsNullOrEmpty(body))
                sb.Append(',').Append(body);
        }
        sb.Append('}');

        if (!TryExecuteMetaProofAction(PlayerActionTriggerControlSurfaceElementId, sb.ToString(), out _))
            return;

        SetMetaProofStatus("Meta proof triggered " + (elementId ?? ""));
    }

    private void RunMetaProofDirectPlayerAction(string actionId, string extraArgsBody, string successStatus)
    {
        string selectorJson;
        if (!TryBuildMetaProofDirectPlayerSelectorJson(out selectorJson))
            return;

        StringBuilder args = new StringBuilder(192);
        args.Append(selectorJson.Substring(0, selectorJson.Length - 1));
        if (!string.IsNullOrEmpty(extraArgsBody))
            args.Append(',').Append(extraArgsBody);
        args.Append('}');

        if (!TryExecuteMetaProofAction(actionId, args.ToString(), out _))
            return;

        SetMetaProofStatus(string.IsNullOrEmpty(successStatus) ? "Meta proof player action ok" : successStatus);
    }

    private bool TryBuildMetaProofDirectPlayerSelectorJson(out string selectorJson)
    {
        selectorJson = "{}";

        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string errorMessage;
            if (!TryBuildAttachedPlayerSelectorJson(out selectorJson, out errorMessage))
            {
                SetMetaProofStatus(string.IsNullOrEmpty(errorMessage) ? "Hosted player selector not ready" : errorMessage);
                return false;
            }

            return true;
        }

        string controlSurfaceInstanceId = string.IsNullOrEmpty(syncMetaProofInstanceId) ? "" : syncMetaProofInstanceId.Trim();
        if (!string.IsNullOrEmpty(controlSurfaceInstanceId)
            && playerControlSurfaceBindings.TryGetValue(controlSurfaceInstanceId, out PlayerControlSurfaceBindingRecord binding)
            && binding != null
            && !string.IsNullOrEmpty(binding.playbackKey))
        {
            selectorJson = "{"
                + "\"playbackKey\":\"" + EscapeJsonString(binding.playbackKey) + "\""
                + "}";
            return true;
        }

        string targetInstanceId;
        if (!TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
            return false;

        selectorJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"displayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
            + "}";
        return true;
    }

    private bool TryExecuteMetaProofAction(string actionId, string argsJson, out string resultJson)
    {
        resultJson = "{}";
        syncBrokerActionId = string.IsNullOrEmpty(actionId) ? "" : actionId.Trim();
        syncBrokerArgsJson = string.IsNullOrEmpty(argsJson) ? "{}" : argsJson.Trim();
        if (syncBrokerActionIdField != null)
            syncBrokerActionIdField.valNoCallback = syncBrokerActionId;
        if (syncBrokerArgsJsonField != null)
            syncBrokerArgsJsonField.valNoCallback = syncBrokerArgsJson;

        string errorMessage = "";
        bool ok = false;
        try
        {
            ok = TryExecuteAction(syncBrokerActionId, syncBrokerArgsJson, out resultJson, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = "meta proof action " + syncBrokerActionId + " exception: " + ex.Message;
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            ok = false;
        }

        SetLastReceipt(resultJson);
        if (syncBrokerResultJsonField != null)
            syncBrokerResultJsonField.val = resultJson;
        if (!ok)
        {
            SetLastError(errorMessage);
            bool isPlayerBindControlSurface =
                string.Equals(syncBrokerActionId, PlayerActionBindControlSurfaceId, StringComparison.Ordinal);
#if !FRAMEANGEL_CUA_PLAYER
            isPlayerBindControlSurface = isPlayerBindControlSurface
                || IsSessionPlayerBindControlSurfaceAction(syncBrokerActionId);
#endif
            if (isPlayerBindControlSurface
                && string.Equals(errorMessage, "player consumer not found", StringComparison.Ordinal))
            {
                SetMetaProofStatus("No bound player target found. For the Meta proof, load or reload the standalone player on the Ghost screen, then retry bind.");
            }
            else if (isPlayerBindControlSurface
                && !string.IsNullOrEmpty(errorMessage)
                && (
                    errorMessage.IndexOf("standalone player", StringComparison.OrdinalIgnoreCase) >= 0
                ))
            {
                SetMetaProofStatus(errorMessage);
            }
            else
            {
                SetMetaProofStatus(errorMessage);
            }
            return false;
        }

        SetLastError("");
        return true;
    }

    private bool TryEnsureMetaProofControlSurfaceInstance(string instanceId)
    {
        string resourceId = string.IsNullOrEmpty(syncMetaProofResourceId) ? "" : syncMetaProofResourceId.Trim();
        string packagePath = string.IsNullOrEmpty(syncMetaProofPackagePath) ? "" : syncMetaProofPackagePath.Trim();
        if (string.IsNullOrEmpty(resourceId) && string.IsNullOrEmpty(packagePath))
            resourceId = MetaProofCurrentResourceId;

        if (!string.IsNullOrEmpty(resourceId))
        {
            syncMetaProofResourceId = resourceId;
            if (syncMetaProofResourceIdField != null)
                syncMetaProofResourceIdField.valNoCallback = resourceId;
        }
        else if (!string.IsNullOrEmpty(packagePath))
        {
            string importArgsJson = "{\"packagePath\":\"" + EscapeJsonString(packagePath) + "\"}";
            if (!TryExecuteMetaProofAction(HostPackageImportActionId, importArgsJson, out string importResultJson))
                return false;

            resourceId = ExtractJsonArgString(importResultJson, "resourceId");
            if (!string.IsNullOrEmpty(resourceId))
            {
                syncMetaProofResourceId = resourceId;
                if (syncMetaProofResourceIdField != null)
                    syncMetaProofResourceIdField.valNoCallback = resourceId;
            }
        }
        else if (string.IsNullOrEmpty(resourceId))
        {
            resourceId = MetaProofFallbackResourceId;
        }

        InnerPieceInstanceRecord existingInstance;
        FAInnerPieceControlSurfaceData existingControlSurface;
        if (TryResolveMetaProofControlSurfaceInstance(instanceId, out existingInstance, out existingControlSurface))
        {
            if (existingInstance != null
                && string.Equals(existingInstance.resourceId ?? "", resourceId, StringComparison.OrdinalIgnoreCase))
                return true;

            SetMetaProofStatus("Meta proof replacing stale control surface instance");
            DeleteInnerPieceInstanceInternal(existingInstance, true);
        }

        if (HasMetaProofControlSurfaceInstance(instanceId))
            return true;

        if (string.IsNullOrEmpty(resourceId))
        {
            SetMetaProofStatus("Meta proof spawn needs resource id");
            return false;
        }

        string spawnArgsJson = "{"
            + "\"resourceId\":\"" + EscapeJsonString(resourceId) + "\""
            + ",\"instanceId\":\"" + EscapeJsonString(instanceId) + "\""
            + "}";
        if (!TryExecuteMetaProofAction(HostResourceSpawnActionId, spawnArgsJson, out _))
            return false;

        if (!HasMetaProofControlSurfaceInstance(instanceId))
        {
            SetMetaProofStatus("Meta proof control surface instance still missing after spawn");
            return false;
        }

        return true;
    }

    private bool TryResolveMetaProofControlSurfaceInstance(
        string instanceId,
        out InnerPieceInstanceRecord instance,
        out FAInnerPieceControlSurfaceData controlSurface)
    {
        instance = null;
        controlSurface = null;
        if (string.IsNullOrEmpty(instanceId))
            return false;

        string ignoredError;
        return TryResolveInnerPieceControlSurfaceInstance(
            "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(instanceId) + "\"}",
            out instance,
            out controlSurface,
            out ignoredError);
    }

    private bool HasMetaProofControlSurfaceInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return false;

        InnerPieceInstanceRecord instance;
        FAInnerPieceControlSurfaceData controlSurface;
        string ignoredError;
        return TryResolveInnerPieceControlSurfaceInstance(
            "{\"controlSurfaceInstanceId\":\"" + EscapeJsonString(instanceId) + "\"}",
            out instance,
            out controlSurface,
            out ignoredError);
    }

    private void SetMetaProofStatus(string value)
    {
        syncMetaProofStatus = string.IsNullOrEmpty(value) ? "" : value;
        if (syncMetaProofStatusField != null && syncMetaProofStatusField.val != syncMetaProofStatus)
            syncMetaProofStatusField.valNoCallback = syncMetaProofStatus;
    }

    private bool TryBindMetaProofToPlayer(string instanceId, string targetInstanceId, out string resultJson)
    {
        targetInstanceId = string.IsNullOrEmpty(targetInstanceId) ? "" : targetInstanceId.Trim();
        if (string.IsNullOrEmpty(targetInstanceId) && !TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
        {
            resultJson = "{}";
            return false;
        }

        StringBuilder args = new StringBuilder(192);
        args.Append('{');
        args.Append("\"controlSurfaceInstanceId\":\"").Append(EscapeJsonString(instanceId)).Append("\"");
        args.Append(",\"preferStandalonePlayer\":true");
        args.Append(",\"targetDisplayId\":\"player_main\"");
        if (!string.IsNullOrEmpty(targetInstanceId))
            args.Append(",\"targetInstanceId\":\"").Append(EscapeJsonString(targetInstanceId)).Append("\"");
        args.Append('}');

        if (!TryExecuteMetaProofAction(PlayerActionBindControlSurfaceId, args.ToString(), out resultJson))
            return false;

        return true;
    }

    private bool TryLayoutMetaProofControlSurface(
        string controlSurfaceInstanceId,
        string targetInstanceId,
        out string errorMessage,
        float? gapMetersOverride = null,
        float? forwardOffsetMetersOverride = null)
    {
        errorMessage = "";
        controlSurfaceInstanceId = string.IsNullOrEmpty(controlSurfaceInstanceId) ? "" : controlSurfaceInstanceId.Trim();
        targetInstanceId = string.IsNullOrEmpty(targetInstanceId) ? "" : targetInstanceId.Trim();
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
        {
            errorMessage = "Meta proof layout needs control surface instance id";
            SetMetaProofStatus(errorMessage);
            return false;
        }

        if (IsHostedPlayerInstanceId(targetInstanceId))
        {
            if (!TryLayoutHostedMetaProofControlSurface(
                    controlSurfaceInstanceId,
                    targetInstanceId,
                    out errorMessage,
                    gapMetersOverride,
                    forwardOffsetMetersOverride))
            {
                SetMetaProofStatus(errorMessage);
                return false;
            }

            return true;
        }

        if (string.IsNullOrEmpty(targetInstanceId) && !TryEnsureMetaProofPlayerTargetScreenInstance(out targetInstanceId))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof layout needs target screen" : syncMetaProofStatus;
            return false;
        }

        InnerPieceInstanceRecord controlInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolveMetaProofControlSurfaceInstance(controlSurfaceInstanceId, out controlInstance, out controlSurface) || controlInstance == null)
        {
            errorMessage = "Meta proof control surface instance not found";
            SetMetaProofStatus(errorMessage);
            return false;
        }

        InnerPieceInstanceRecord targetInstance;
        if (!TryResolveInnerPieceInstance(
            "{\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\"}",
            out targetInstance,
            out errorMessage) || targetInstance == null)
        {
            SetMetaProofStatus(errorMessage);
            return false;
        }

        Vector3 worldPosition;
        Quaternion worldRotation;
        string anchorAtomUid;
        if (!TryComputeMetaProofControlSurfaceLayoutPose(
                controlInstance,
                controlSurface,
                targetInstanceId,
                targetInstance,
                gapMetersOverride,
                forwardOffsetMetersOverride,
                out worldPosition,
                out worldRotation,
                out anchorAtomUid,
                out errorMessage))
        {
            SetMetaProofStatus(errorMessage);
            return false;
        }

        bool needsFollowReset =
            !string.IsNullOrEmpty(anchorAtomUid)
            && !string.IsNullOrEmpty(controlInstance.anchorAtomUid)
            && !string.Equals(controlInstance.anchorAtomUid, anchorAtomUid, StringComparison.OrdinalIgnoreCase);
        if (needsFollowReset)
        {
            string clearFollowArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
                + ",\"clear\":true"
                + "}";
            if (!TryExecuteMetaProofAction(HostInstanceSetFollowActionId, clearFollowArgsJson, out _))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof control panel follow reset failed" : syncMetaProofStatus;
                return false;
            }
        }

        string transformArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
            + ",\"position\":{\"x\":" + FormatFloat(worldPosition.x)
            + ",\"y\":" + FormatFloat(worldPosition.y)
            + ",\"z\":" + FormatFloat(worldPosition.z) + "}"
            + ",\"rotX\":" + FormatFloat(worldRotation.x)
            + ",\"rotY\":" + FormatFloat(worldRotation.y)
            + ",\"rotZ\":" + FormatFloat(worldRotation.z)
            + ",\"rotW\":" + FormatFloat(worldRotation.w)
            + ",\"scaleX\":" + FormatFloat(MetaProofControlPanelUniformScale)
            + ",\"scaleY\":" + FormatFloat(MetaProofControlPanelUniformScale)
            + ",\"scaleZ\":" + FormatFloat(MetaProofControlPanelUniformScale)
            + "}";
        if (!TryExecuteMetaProofAction(HostInstanceTransformActionId, transformArgsJson, out _))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof control panel transform failed" : syncMetaProofStatus;
            return false;
        }

        if (!string.IsNullOrEmpty(anchorAtomUid))
        {
            string followArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
                + ",\"anchorAtomUid\":\"" + EscapeJsonString(anchorAtomUid) + "\""
                + ",\"followPosition\":true"
                + ",\"followRotation\":true"
                + "}";
            if (!TryExecuteMetaProofAction(HostInstanceSetFollowActionId, followArgsJson, out _))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof control panel follow bind failed" : syncMetaProofStatus;
                return false;
            }
        }

        return true;
    }

    private bool TryComputeMetaProofControlSurfaceLayoutPose(
        InnerPieceInstanceRecord controlInstance,
        FAInnerPieceControlSurfaceData controlSurface,
        string targetInstanceId,
        InnerPieceInstanceRecord targetInstance,
        float? gapMetersOverride,
        float? forwardOffsetMetersOverride,
        out Vector3 worldPosition,
        out Quaternion worldRotation,
        out string anchorAtomUid,
        out string errorMessage)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;
        anchorAtomUid = "";
        errorMessage = "";

        FAInnerPiecePlaneData screenPlane;
        if (!TryResolveInnerPieceScreenPlane(targetInstanceId, InnerPiecePrimaryPlayerDisplayId, out screenPlane, out errorMessage)
            && !TryBuildInnerPieceGrabHandlePlaneData(targetInstance, out screenPlane))
        {
            return false;
        }

        float panelHeightMeters = controlSurface != null && controlSurface.surfaceHeightMeters > 0f
            ? controlSurface.surfaceHeightMeters
            : 0.24f;
        float panelDepthMeters = 0.01f;

        string surfaceNodeId = controlSurface != null ? (controlSurface.surfaceNodeId ?? "") : "";
        GameObject panelSurfaceObject = ResolveInnerPieceNodeObject(controlInstance, surfaceNodeId);
        FAInnerPiecePlaneData panelPlane;
        if (TryBuildInnerPiecePlaneData(panelSurfaceObject, out panelPlane))
        {
            panelHeightMeters = Mathf.Max(0.001f, panelPlane.heightMeters);
            panelDepthMeters = Mathf.Max(0.001f, panelPlane.depthMeters);
        }

        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            anchorAtomUid = attachedHostAtom.uid ?? "";
        }
        else if (!TryEnsureMetaProofPlayerTargetScreenAnchorUid(targetInstanceId, out anchorAtomUid, out errorMessage))
        {
            // Keep relative panel layout working even when screen anchor discovery lags
            // or fails. The refresh loop can continue to reposition from live screen data,
            // and follow binding can attach later once an anchor is available.
            anchorAtomUid = "";
            errorMessage = "";
        }

        float gapMeters = gapMetersOverride.HasValue
            ? Mathf.Max(0f, gapMetersOverride.Value)
            : MetaProofControlPanelGapMeters;
        float forwardOffsetMeters = forwardOffsetMetersOverride.HasValue
            ? Mathf.Max(0f, forwardOffsetMetersOverride.Value)
            : MetaProofControlPanelForwardOffsetMeters;

        worldPosition =
            screenPlane.center
            - (screenPlane.up * ((screenPlane.heightMeters * 0.5f) + gapMeters + (panelHeightMeters * 0.5f)))
            + (screenPlane.forward * Mathf.Max(forwardOffsetMeters, ((screenPlane.depthMeters + panelDepthMeters) * 0.5f)));
        worldRotation = Quaternion.LookRotation(screenPlane.forward, screenPlane.up);
        return true;
    }

    private bool TryLayoutPlayerControlSurfaceRelative(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string controlSurfaceInstanceId = ExtractJsonArgString(argsJson, "controlSurfaceInstanceId", "instanceId");
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
        {
            errorMessage = "controlSurfaceInstanceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string targetInstanceId = ExtractJsonArgString(argsJson, "targetInstanceId");
        if (string.IsNullOrEmpty(targetInstanceId))
        {
            errorMessage = "targetInstanceId is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float parsedValue;
        bool hasGapMeters =
            TryExtractJsonFloatField(argsJson, "gapMeters", out parsedValue)
            || TryExtractJsonFloatField(argsJson, "panelGapMeters", out parsedValue);
        float gapMeters = parsedValue;
        bool hasForwardOffsetMeters =
            TryExtractJsonFloatField(argsJson, "forwardOffsetMeters", out parsedValue)
            || TryExtractJsonFloatField(argsJson, "panelForwardOffsetMeters", out parsedValue);
        float forwardOffsetMeters = parsedValue;

        if (!TryLayoutMetaProofControlSurface(
                controlSurfaceInstanceId,
                targetInstanceId,
                out errorMessage,
                hasGapMeters ? (float?)gapMeters : null,
                hasForwardOffsetMeters ? (float?)forwardOffsetMeters : null))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        InnerPieceInstanceRecord instance;
        if (!TryResolveInnerPieceInstance(
                "{\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\"}",
                out instance,
                out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = FAInnerPieceStorage.SerializeInstanceState(BuildInnerPieceInstanceState(instance, ""), false);
        resultJson = BuildBrokerResult(true, "player_control_surface_layout ok", payload);
        EmitRuntimeEvent(
            "player_control_surface_layout",
            actionId,
            "ok",
            "",
            controlSurfaceInstanceId,
            targetInstanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            targetInstanceId,
            payload);
        return true;
    }

    private bool TryEnsureMetaProofPlayerTargetScreenAnchorUid(string targetInstanceId, out string anchorAtomUid, out string errorMessage)
    {
        anchorAtomUid = "";
        errorMessage = "";
        targetInstanceId = string.IsNullOrEmpty(targetInstanceId) ? "" : targetInstanceId.Trim();
        if (string.IsNullOrEmpty(targetInstanceId))
        {
            errorMessage = "Meta proof target screen instance id is required";
            return false;
        }

        InnerPieceInstanceRecord targetInstance;
        if (!TryResolveInnerPieceInstance(
            "{\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\"}",
            out targetInstance,
            out errorMessage) || targetInstance == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(targetInstance.anchorAtomUid))
        {
            anchorAtomUid = targetInstance.anchorAtomUid;
            return true;
        }

        if (targetInstance.pendingAnchorDiscovery)
        {
            errorMessage = "Meta proof screen anchor pending discovery";
            return false;
        }

        if (string.Equals(
                targetInstance.lastError,
                "spawned anchor atom was not discoverable after deferred discovery window",
                StringComparison.OrdinalIgnoreCase))
        {
            SuperController sc = SuperController.singleton;
            Atom lateAnchorAtom = TryFindExistingInnerPieceAnchorAtom(
                sc,
                targetInstance.pendingRequestedAnchorUid,
                targetInstance.instanceId);
            if (lateAnchorAtom != null)
            {
                SyncObjectRecord rootRecord;
                string ignoredResultJson;
                string recoveredAnchorAtomUid;
                if (syncObjects.TryGetValue(targetInstance.rootObjectId, out rootRecord)
                    && rootRecord != null
                    && rootRecord.gameObject != null
                    && TryFinalizeInnerPieceAnchorAtomBinding(
                        string.IsNullOrEmpty(targetInstance.pendingAnchorActionId) ? HostAnchorSpawnAtomActionId : targetInstance.pendingAnchorActionId,
                        targetInstance,
                        rootRecord,
                        lateAnchorAtom,
                        targetInstance.pendingRequestedAnchorUid,
                        targetInstance.pendingAnchorPosition,
                        targetInstance.pendingAnchorRotation,
                        targetInstance.pendingAnchorScaleFactor,
                        targetInstance.pendingBindFollow,
                        targetInstance.pendingFollowPosition,
                        targetInstance.pendingFollowRotation,
                        targetInstance.pendingLocalPositionOffset,
                        targetInstance.pendingLocalRotationOffset,
                        "",
                        "",
                        out ignoredResultJson,
                        out errorMessage,
                        out recoveredAnchorAtomUid))
                {
                    anchorAtomUid = recoveredAnchorAtomUid;
                    targetInstance.lastError = "";
                    return true;
                }
            }

            errorMessage = targetInstance.lastError;
            return false;
        }

        string spawnArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"atomType\":\"Empty\""
            + ",\"bindFollow\":true"
            + ",\"followPosition\":true"
            + ",\"followRotation\":true"
            + ",\"snapUnderAnchor\":true"
            + "}";
        if (!TryExecuteMetaProofAction(HostAnchorSpawnAtomActionId, spawnArgsJson, out _))
        {
            errorMessage = string.IsNullOrEmpty(syncMetaProofStatus) ? "Meta proof screen anchor spawn failed" : syncMetaProofStatus;
            return false;
        }

        if (!TryResolveInnerPieceInstance(
            "{\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\"}",
            out targetInstance,
            out errorMessage) || targetInstance == null || string.IsNullOrEmpty(targetInstance.anchorAtomUid))
        {
            errorMessage = "Meta proof screen anchor pending discovery";
            return false;
        }

        anchorAtomUid = targetInstance.anchorAtomUid;
        return true;
    }

    private bool TryEnsureMetaProofStandalonePlayerMedia(string targetInstanceId)
    {
        targetInstanceId = string.IsNullOrEmpty(targetInstanceId) ? "" : targetInstanceId.Trim();
        if (string.IsNullOrEmpty(targetInstanceId))
        {
            SetMetaProofStatus("Meta proof media load needs target instance id");
            return false;
        }

        string selectorJson = "{"
            + "\"targetInstanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"targetDisplayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
            + "}";

        StandalonePlayerRecord record;
        string errorMessage;
        if (!TryResolveStandalonePlayerRecordForControlSurface(
            selectorJson,
            InnerPiecePrimaryPlayerDisplayId,
            out record,
            out errorMessage))
        {
            SetMetaProofStatus(string.IsNullOrEmpty(errorMessage)
                ? "Meta proof standalone player target not resolved for media load"
                : errorMessage);
            return false;
        }

        List<string> mediaPaths;
        bool usingConfiguredMedia;
        if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out errorMessage))
        {
            SetMetaProofStatus(string.IsNullOrEmpty(errorMessage)
                ? "Meta proof media path is unavailable"
                : errorMessage);
            return false;
        }

        string mediaPath = mediaPaths.Count > 0 ? mediaPaths[0] : "";
        if (string.IsNullOrEmpty(mediaPath))
        {
            SetMetaProofStatus(usingConfiguredMedia
                ? "Meta proof media path is unavailable"
                : "Meta proof sample media not found");
            return false;
        }

        if (usingConfiguredMedia)
        {
            if (AreEquivalentMetaProofMediaPaths(GetMetaProofCurrentStandalonePlayerMediaPath(record), mediaPath))
                return true;

            bool playSelectedMedia = ResolveStandalonePlayerLoadDesiredPlaying("{}", mediaPath, true);

            string selectedArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
                + ",\"displayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
                + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
                + ",\"play\":" + (playSelectedMedia ? "true" : "false")
                + "}";
            if (!TryExecuteMetaProofAction(PlayerActionLoadPathId, selectedArgsJson, out _))
                return false;

            SetMetaProofStatus("Meta proof loading selected media");
            return true;
        }

        bool hasMedia =
            !string.IsNullOrEmpty(record.mediaPath)
            || !string.IsNullOrEmpty(record.resolvedMediaPath)
            || (record.playlistPaths != null && record.playlistPaths.Count > 0);

        bool hasDesiredPlaylist =
            record.playlistPaths != null
            && record.playlistPaths.Count == mediaPaths.Count
            && mediaPaths.Count > 0;
        if (hasDesiredPlaylist)
        {
            for (int i = 0; i < mediaPaths.Count; i++)
            {
                if (!AreEquivalentMetaProofMediaPaths(record.playlistPaths[i], mediaPaths[i]))
                {
                    hasDesiredPlaylist = false;
                    break;
                }
            }
        }

        if (hasMedia && hasDesiredPlaylist && !ShouldReplaceMetaProofStandalonePlayerMedia(record, mediaPath))
            return true;

        bool playSampleMedia = ResolveStandalonePlayerLoadDesiredPlaying("{}", mediaPath, true);

        string loadArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(targetInstanceId) + "\""
            + ",\"displayId\":\"" + EscapeJsonString(InnerPiecePrimaryPlayerDisplayId) + "\""
            + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
            + ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths)
            + ",\"play\":" + (playSampleMedia ? "true" : "false")
            + "}";

        if (!TryExecuteMetaProofAction(PlayerActionLoadPathId, loadArgsJson, out _))
            return false;

        SetMetaProofStatus("Meta proof loading sample media");
        return true;
    }

    private bool ShouldReplaceMetaProofStandalonePlayerMedia(StandalonePlayerRecord record, string preferredMediaPath)
    {
        if (record == null)
            return true;

        string currentPath = GetMetaProofCurrentStandalonePlayerMediaPath(record);
        if (string.IsNullOrEmpty(currentPath))
            return true;

        if (AreEquivalentMetaProofMediaPaths(currentPath, preferredMediaPath))
            return false;

        if (!IsMetaProofDefaultMediaPath(currentPath))
            return false;

        if (record.playlistPaths != null)
        {
            for (int i = 0; i < record.playlistPaths.Count; i++)
            {
                string playlistPath = record.playlistPaths[i];
                if (string.IsNullOrEmpty(playlistPath))
                    continue;

                if (AreEquivalentMetaProofMediaPaths(playlistPath, preferredMediaPath))
                    continue;

                if (!IsMetaProofDefaultMediaPath(playlistPath))
                    return false;
            }
        }

        return true;
    }

    private string GetMetaProofCurrentStandalonePlayerMediaPath(StandalonePlayerRecord record)
    {
        if (record == null)
            return "";

        string currentPlaylistPath = GetStandalonePlayerCurrentPlaylistPath(record);
        if (!string.IsNullOrEmpty(currentPlaylistPath))
            return currentPlaylistPath;

        if (!string.IsNullOrEmpty(record.mediaPath))
            return record.mediaPath;

        if (!string.IsNullOrEmpty(record.resolvedMediaPath))
            return record.resolvedMediaPath;

        return "";
    }

    private bool IsMetaProofDefaultMediaPath(string mediaPath)
    {
        if (string.IsNullOrEmpty(mediaPath))
            return false;

        for (int i = 0; i < MetaProofSampleMediaPathCandidates.Length; i++)
        {
            if (AreEquivalentMetaProofMediaPaths(mediaPath, MetaProofSampleMediaPathCandidates[i]))
                return true;
        }

        return false;
    }

    private bool TryResolveMetaProofRequestedMediaPaths(
        out List<string> mediaPaths,
        out bool usingConfiguredMedia,
        out string errorMessage)
    {
        mediaPaths = new List<string>();
        usingConfiguredMedia = false;
        errorMessage = "";

        string selectedMediaPath = string.IsNullOrEmpty(syncMetaProofMediaPath) ? "" : syncMetaProofMediaPath.Trim();
        if (!string.IsNullOrEmpty(selectedMediaPath))
        {
            if (!TryResolvePlayerRuntimeMediaPaths(selectedMediaPath, out mediaPaths, out errorMessage))
                return false;

            usingConfiguredMedia = true;
            return true;
        }

        mediaPaths = ResolveMetaProofSampleMediaPaths();
        if (mediaPaths.Count <= 0)
        {
            errorMessage = "Player sample media not found";
            return false;
        }

        return true;
    }

    private bool TryResolvePlayerRuntimeMediaPaths(
        string selectedMediaPath,
        out List<string> mediaPaths,
        out string errorMessage)
    {
        mediaPaths = new List<string>();
        errorMessage = "";

        string candidatePath = string.IsNullOrEmpty(selectedMediaPath) ? "" : selectedMediaPath.Trim();
        if (string.IsNullOrEmpty(candidatePath))
        {
            errorMessage = "Player media path is unavailable";
            return false;
        }

        if (!IsSecureRuntimePathCandidate(candidatePath))
        {
            errorMessage = "Player media path must use a VaM-safe path";
            return false;
        }

        if (FileManagerSecure.FileExists(candidatePath, false))
        {
            if (!IsSupportedPlayerRuntimeMediaPath(candidatePath))
            {
                errorMessage = "Player media file type is not supported";
                return false;
            }

            AppendPlayerRuntimeMediaDirectoryPlaylist(candidatePath, mediaPaths);
            if (mediaPaths.Count <= 0)
                mediaPaths.Add(candidatePath);
            mediaPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        if (!FileManagerSecure.DirectoryExists(candidatePath, false))
        {
            errorMessage = "Player media file or directory not found";
            return false;
        }

        string[] discoveredEntries = FileManagerSecure.GetFiles(candidatePath, "*");
        if (discoveredEntries != null)
        {
            for (int i = 0; i < discoveredEntries.Length; i++)
            {
                string mediaPath = discoveredEntries[i];
                if (string.IsNullOrEmpty(mediaPath))
                    continue;

                string extension = ExtractPlayerRuntimeMediaExtension(mediaPath);
                for (int extensionIndex = 0; extensionIndex < PlayerRuntimeMediaExtensions.Length; extensionIndex++)
                {
                    if (!string.Equals(extension, PlayerRuntimeMediaExtensions[extensionIndex], StringComparison.OrdinalIgnoreCase))
                        continue;

                    mediaPaths.Add(mediaPath);
                    break;
                }
            }
        }

        if (mediaPaths.Count <= 0)
        {
            errorMessage = "Player media directory has no supported files";
            return false;
        }

        mediaPaths.Sort(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    private void AppendPlayerRuntimeMediaDirectoryPlaylist(string selectedMediaPath, List<string> mediaPaths)
    {
        if (mediaPaths == null || string.IsNullOrEmpty(selectedMediaPath))
            return;

        string parentDirectory = FileManagerSecure.GetDirectoryName(selectedMediaPath, false);
        string[] siblingEntries = null;
        if (!string.IsNullOrEmpty(parentDirectory) && FileManagerSecure.DirectoryExists(parentDirectory, false))
            siblingEntries = FileManagerSecure.GetFiles(parentDirectory, "*");

        List<string> parityPlaylist = FrameAngelPlayerMediaParity.BuildSiblingPlaylist(selectedMediaPath, siblingEntries);
        for (int i = 0; i < parityPlaylist.Count; i++)
            mediaPaths.Add(parityPlaylist[i]);
    }

    private bool IsSupportedPlayerRuntimeMediaPath(string mediaPath)
    {
        return FrameAngelPlayerMediaParity.IsSupportedMediaPath(mediaPath);
    }

    private string ResolvePrimaryPlayerRuntimeMediaPath(string requestedMediaPath, List<string> mediaPaths)
    {
        string wanted = string.IsNullOrEmpty(requestedMediaPath) ? "" : requestedMediaPath.Trim();
        if (!string.IsNullOrEmpty(wanted) && mediaPaths != null)
        {
            for (int i = 0; i < mediaPaths.Count; i++)
            {
                if (AreEquivalentMetaProofMediaPaths(mediaPaths[i], wanted))
                    return mediaPaths[i];
            }
        }

        return mediaPaths != null && mediaPaths.Count > 0 ? mediaPaths[0] : wanted;
    }

    private string ExtractPlayerRuntimeMediaExtension(string mediaPath)
    {
        if (string.IsNullOrEmpty(mediaPath))
            return "";

        int lastSlash = Math.Max(mediaPath.LastIndexOf('/'), mediaPath.LastIndexOf('\\'));
        int lastDot = mediaPath.LastIndexOf('.');
        if (lastDot <= lastSlash || lastDot < 0 || lastDot >= mediaPath.Length - 1)
            return "";

        return mediaPath.Substring(lastDot);
    }

    private bool AreEquivalentMetaProofMediaPaths(string left, string right)
    {
        string normalizedLeft = NormalizeStandalonePlayerPathForMatch(left);
        string normalizedRight = NormalizeStandalonePlayerPathForMatch(right);
        if (string.IsNullOrEmpty(normalizedLeft) || string.IsNullOrEmpty(normalizedRight))
            return false;

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
            return true;

        return normalizedLeft.EndsWith("/" + normalizedRight, StringComparison.Ordinal)
            || normalizedRight.EndsWith("/" + normalizedLeft, StringComparison.Ordinal);
    }

    private bool TryEnsureMetaProofPlayerTargetScreenInstance(out string targetInstanceId)
    {
        targetInstanceId = "";

        Atom attachedHostAtom;
        if (TryResolveAttachedMetaProofHostAtom(out attachedHostAtom) && attachedHostAtom != null)
        {
            string hostAtomUid = string.IsNullOrEmpty(attachedHostAtom.uid) ? "" : attachedHostAtom.uid.Trim();
            if (string.IsNullOrEmpty(hostAtomUid))
            {
                SetMetaProofStatus("Hosted player host atom uid missing");
                return false;
            }

            string hostedInstanceId = BuildHostedPlayerInstanceId(hostAtomUid);
            InnerPieceInstanceRecord hostedInstance;
            InnerPieceScreenSlotRuntimeRecord hostedSlot;
            string hostedError;
            if (TryResolveInnerPieceScreenSlot(
                hostedInstanceId,
                InnerPiecePrimaryPlayerDisplayId,
                out hostedInstance,
                out hostedSlot,
                out hostedError))
            {
                targetInstanceId = hostedInstance != null
                    ? (hostedInstance.instanceId ?? hostedInstanceId)
                    : hostedInstanceId;
                UpdateMetaProofPlayerAtomUid(targetInstanceId);
                return true;
            }

            SetMetaProofStatus(string.IsNullOrEmpty(hostedError)
                ? "Hosted player target not ready"
                : hostedError);
            return false;
        }

        string requestedInstanceId = string.IsNullOrEmpty(syncMetaProofPlayerAtomUid) ? "" : syncMetaProofPlayerAtomUid.Trim();
        if (!string.IsNullOrEmpty(requestedInstanceId))
        {
            InnerPieceInstanceRecord requestedInstance;
            InnerPieceScreenSlotRuntimeRecord requestedSlot;
            string requestedError;
            if (TryResolveInnerPieceScreenSlot(
                requestedInstanceId,
                InnerPiecePrimaryPlayerDisplayId,
                out requestedInstance,
                out requestedSlot,
                out requestedError))
            {
                targetInstanceId = requestedInstance != null
                    ? (requestedInstance.instanceId ?? requestedInstanceId)
                    : requestedInstanceId;
                UpdateMetaProofPlayerAtomUid(targetInstanceId);
                return true;
            }
        }

        InnerPieceInstanceRecord liveInstance;
        InnerPieceScreenSlotRuntimeRecord liveSlot;
        string liveError;
        if (TryResolveStandalonePlayerTargetScreenSlotForControlSurface(
                InnerPiecePrimaryPlayerDisplayId,
                out liveInstance,
                out liveSlot,
                out liveError)
            && liveInstance != null)
        {
            targetInstanceId = liveInstance.instanceId ?? "";
            UpdateMetaProofPlayerAtomUid(targetInstanceId);
            return true;
        }

        string resourceId = string.IsNullOrEmpty(syncMetaProofTargetScreenResourceId)
            ? ""
            : syncMetaProofTargetScreenResourceId.Trim();
        if (string.IsNullOrEmpty(resourceId))
        {
            string importArgsJson = "{\"packagePath\":\"" + EscapeJsonString(ResolveMetaProofGhostScreenLocalPackagePath()) + "\"}";
            string importResultJson;
            if (!TryExecuteMetaProofAction(HostPackageImportActionId, importArgsJson, out importResultJson))
                return false;

            resourceId = ExtractJsonArgString(importResultJson, "resourceId");
            if (string.IsNullOrEmpty(resourceId))
            {
                SetMetaProofStatus("Meta proof player screen import did not return resource id");
                return false;
            }

            syncMetaProofTargetScreenResourceId = resourceId;
        }

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        TryBuildMetaProofTargetScreenSpawnPose(out spawnPosition, out spawnRotation);

        string spawnArgsJson = "{"
            + "\"resourceId\":\"" + EscapeJsonString(resourceId) + "\""
            + ",\"consumerId\":\"scene_runtime\""
            + ",\"targetType\":\"session_scene\""
            + ",\"instanceId\":\"" + EscapeJsonString(string.IsNullOrEmpty(requestedInstanceId) ? MetaProofGhostScreenDefaultInstanceId : requestedInstanceId) + "\""
            + ",\"position\":{\"x\":" + FormatFloat(spawnPosition.x)
            + ",\"y\":" + FormatFloat(spawnPosition.y)
            + ",\"z\":" + FormatFloat(spawnPosition.z) + "}"
            + ",\"rotX\":" + FormatFloat(spawnRotation.x)
            + ",\"rotY\":" + FormatFloat(spawnRotation.y)
            + ",\"rotZ\":" + FormatFloat(spawnRotation.z)
            + ",\"rotW\":" + FormatFloat(spawnRotation.w)
            + "}";
        if (!TryExecuteMetaProofAction(HostResourceSpawnActionId, spawnArgsJson, out _))
            return false;

        string spawnedInstanceId = string.IsNullOrEmpty(requestedInstanceId) ? MetaProofGhostScreenDefaultInstanceId : requestedInstanceId;
        InnerPieceInstanceRecord spawnedInstance;
        InnerPieceScreenSlotRuntimeRecord spawnedSlot;
        string spawnedError;
        if (TryResolveInnerPieceScreenSlot(
                spawnedInstanceId,
                InnerPiecePrimaryPlayerDisplayId,
                out spawnedInstance,
                out spawnedSlot,
                out spawnedError))
        {
            targetInstanceId = spawnedInstance != null
                ? (spawnedInstance.instanceId ?? spawnedInstanceId)
                : spawnedInstanceId;
            UpdateMetaProofPlayerAtomUid(targetInstanceId);
            return true;
        }

        if (!TryResolveStandalonePlayerTargetScreenSlotForControlSurface(
                InnerPiecePrimaryPlayerDisplayId,
                out liveInstance,
                out liveSlot,
                out liveError)
            || liveInstance == null)
        {
            SetMetaProofStatus(string.IsNullOrEmpty(liveError)
                ? "Meta proof player screen missing after auto-spawn"
                : liveError);
            return false;
        }

        targetInstanceId = liveInstance.instanceId ?? "";
        UpdateMetaProofPlayerAtomUid(targetInstanceId);
        return true;
    }

    private void TryBuildMetaProofTargetScreenSpawnPose(out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = new Vector3(0f, 1.25f, 1.00f);
        worldRotation = Quaternion.identity;

        string preferredHostAtomUid;
        Transform preferredHostTransform;
        if (TryResolveMetaProofPreferredHostTransform(out preferredHostAtomUid, out preferredHostTransform) && preferredHostTransform != null)
        {
            Vector3 hostForward = Vector3.ProjectOnPlane(preferredHostTransform.forward, Vector3.up);
            if (hostForward.sqrMagnitude <= 0.0001f)
                hostForward = preferredHostTransform.forward;
            if (hostForward.sqrMagnitude <= 0.0001f)
                hostForward = Vector3.forward;
            hostForward.Normalize();

            worldPosition = preferredHostTransform.position
                + (hostForward * MetaProofTargetScreenForwardOffsetMeters)
                + (Vector3.up * MetaProofTargetScreenVerticalOffsetMeters);
            worldRotation = Quaternion.LookRotation(-hostForward, Vector3.up);
            return;
        }

        Camera mainCamera = Camera.main;
        Transform headset = mainCamera != null ? mainCamera.transform : null;
        if (headset == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(headset.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
            forward = headset.forward;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        worldPosition = headset.position + (forward * 1.05f);
        worldPosition.y = Mathf.Max(0.85f, headset.position.y - 0.12f);
        worldRotation = Quaternion.LookRotation(-forward, Vector3.up);
    }

    private void UpdateMetaProofPlayerAtomUid(string atomUid)
    {
        syncMetaProofPlayerAtomUid = string.IsNullOrEmpty(atomUid) ? "" : atomUid.Trim();
        if (syncMetaProofPlayerAtomUidField != null)
            syncMetaProofPlayerAtomUidField.valNoCallback = syncMetaProofPlayerAtomUid;
    }
}
