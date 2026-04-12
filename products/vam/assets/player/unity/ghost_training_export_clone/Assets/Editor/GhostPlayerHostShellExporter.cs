using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrameAngel.UnityEditorBridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GhostPlayerHostShellExporter
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DefaultOutputRoot = "C:/projects/frameangel/products/vam/plugins/player/build/host_shell_exports";

    [Serializable]
    private sealed class ShellExportEntry
    {
        public string shellKey = "";
        public string rootObjectSuffix = "";
        public string hostDisplayName = "";
        public string hostResourceId = "";
        public string hostPackageId = "";
        public string deviceClass = "";
        public string orientationSupport = "";
        public string defaultAspectMode = "fit";
        public string inputStyle = "fixed";
        public bool autoOrientToGround;
        public float safeCornerRadius;
    }

    [Serializable]
    private sealed class ShellHostProfile
    {
        public string schemaVersion = "frameangel_player_host_shell_profile_v1";
        public string shellKey = "";
        public string hostDisplayName = "";
        public string hostResourceId = "";
        public string hostPackageId = "";
        public string packageRootPath = "";
        public string packageId = "";
        public string resourceId = "";
        public string scenePath = "";
        public string rootObjectName = "";
        public string screenSurfaceNodeId = "";
        public string disconnectSurfaceNodeId = "";
        public string screenGlassNodeId = "";
        public string controlsAnchorNodeId = "";
        public string bottomAnchorNodeId = "";
        public string deviceClass = "";
        public string orientationSupport = "";
        public string defaultAspectMode = "fit";
        public string inputStyle = "fixed";
        public bool autoOrientToGround;
        public float safeCornerRadius;
    }

    [Serializable]
    private sealed class ShellHostProfileSummary
    {
        public string schemaVersion = "frameangel_player_host_shell_export_summary_v1";
        public string generatedAtUtc = "";
        public string scenePath = "";
        public string outputRoot = "";
        public List<ShellHostProfile> shells = new List<ShellHostProfile>();
    }

    [Serializable]
    private sealed class GeometryPackage
    {
        public List<GeometryNode> nodes = new List<GeometryNode>();
    }

    [Serializable]
    private sealed class GeometryNode
    {
        public string nodeId = "";
        public string displayName = "";
    }

    [Serializable]
    private sealed class ScreenContractPackage
    {
        public string shellId = "";
        public List<ScreenSlotEntry> slots = new List<ScreenSlotEntry>();
    }

    [Serializable]
    private sealed class ScreenSlotEntry
    {
        public string screenSurfaceNodeId = "";
        public string screenGlassNodeId = "";
        public string disconnectSurfaceNodeId = "";
    }

    private sealed class AuthoringSnapshot
    {
        public FAPlayerShellAuthoring shellAuthoring;
        public bool shellAuthoringExisted;
        public string shellId = "";
        public string screenContractVersion = "";
        public string defaultDisconnectStateId = "";
        public string surfaceTargetId = "";

        public FAPlayerScreenSlotAuthoring slotAuthoring;
        public bool slotAuthoringExisted;
        public string slotId = "";
        public string slotSurfaceTargetId = "";
        public string slotDisconnectStateId = "";
        public Transform screenSurface;
        public Transform screenGlass;
        public Transform screenAperture;
        public Transform disconnectSurface;
    }

    private static readonly ShellExportEntry[] ShellEntries =
    {
        new ShellExportEntry
        {
            shellKey = "player_host",
            rootObjectSuffix = ".player_host",
            hostDisplayName = "FA CUA Player Host",
            hostResourceId = "fa_cua_player_host_v1",
            hostPackageId = "faipe_fa_cua_player_host_v1",
            deviceClass = "monitor",
            orientationSupport = "landscape",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.035f
        },
        new ShellExportEntry
        {
            shellKey = "mcbrooke_laptop",
            rootObjectSuffix = ".mcbrooke_laptop",
            hostDisplayName = "FA CUA Player Laptop",
            hostResourceId = "fa_cua_player_laptop_v1",
            hostPackageId = "faipe_fa_cua_player_laptop_v1",
            deviceClass = "laptop",
            orientationSupport = "landscape",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.014f
        },
        new ShellExportEntry
        {
            shellKey = "ivone_phone",
            rootObjectSuffix = ".ivone_phone",
            hostDisplayName = "FA CUA Player Phone",
            hostResourceId = "fa_cua_player_phone_v1",
            hostPackageId = "faipe_fa_cua_player_phone_v1",
            deviceClass = "phone",
            orientationSupport = "portrait",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.028f
        },
        new ShellExportEntry
        {
            shellKey = "ivad_tablet",
            rootObjectSuffix = ".ivad_tablet",
            hostDisplayName = "FA CUA Player Tablet",
            hostResourceId = "fa_cua_player_tablet_v1",
            hostPackageId = "faipe_fa_cua_player_tablet_v1",
            deviceClass = "tablet",
            orientationSupport = "both",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.022f
        },
        new ShellExportEntry
        {
            shellKey = "modern_tv",
            rootObjectSuffix = ".modern_tv",
            hostDisplayName = "FA CUA Player Modern TV",
            hostResourceId = "fa_cua_player_modern_tv_v1",
            hostPackageId = "faipe_fa_cua_player_modern_tv_v1",
            deviceClass = "tv",
            orientationSupport = "landscape",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.01f
        },
        new ShellExportEntry
        {
            shellKey = "retro_tv",
            rootObjectSuffix = ".retro_tv",
            hostDisplayName = "FA CUA Player Retro TV",
            hostResourceId = "fa_cua_player_retro_tv_v1",
            hostPackageId = "faipe_fa_cua_player_retro_tv_v1",
            deviceClass = "tv",
            orientationSupport = "landscape",
            defaultAspectMode = "fit",
            inputStyle = "fixed",
            autoOrientToGround = false,
            safeCornerRadius = 0.01f
        }
    };

    [MenuItem("FrameAngel/Ghost/Meta UI Set/Export Player Host Shell Catalog")]
    public static void ExportPlayerHostShellCatalog()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ExportShellCatalogInternal(DefaultOutputRoot, capturePreview: true);
    }

    public static void ExportPlayerHostShellCatalogBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string outputRoot = GetArg(args, "-playerHostShellOutputRoot", DefaultOutputRoot);
        bool capturePreview = GetBoolArg(args, "-playerHostShellCapturePreview", true);
        ExportShellCatalogInternal(outputRoot, capturePreview);
    }

    private static void ExportShellCatalogInternal(string outputRoot, bool capturePreview)
    {
        string resolvedOutputRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? DefaultOutputRoot
            : Path.GetFullPath(outputRoot);

        Directory.CreateDirectory(resolvedOutputRoot);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ShellHostProfileSummary summary = new ShellHostProfileSummary
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            scenePath = Path.GetFullPath(ScenePath),
            outputRoot = resolvedOutputRoot
        };

        for (int i = 0; i < ShellEntries.Length; i++)
        {
            ShellHostProfile profile = ExportEntry(ShellEntries[i], resolvedOutputRoot, capturePreview);
            summary.shells.Add(profile);
        }

        string summaryPath = Path.Combine(resolvedOutputRoot, "ghost_player_host_shell_export_summary.json");
        File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
        Debug.Log(
            "GhostPlayerHostShellExporter: exported shell catalog to " + resolvedOutputRoot +
            " | summary=" + summaryPath +
            " | shells=" + summary.shells.Count.ToString());
    }

    private static ShellHostProfile ExportEntry(ShellExportEntry entry, string outputRoot, bool capturePreview)
    {
        GameObject rootObject = FindObjectBySuffix(entry.rootObjectSuffix);
        if (rootObject == null)
        {
            throw new InvalidOperationException("GhostPlayerHostShellExporter: root object was not found for " + entry.shellKey);
        }

        Transform screenSurface = FindChildBySuffix(rootObject.transform, ".screen");
        if (screenSurface == null)
        {
            throw new InvalidOperationException("GhostPlayerHostShellExporter: screen surface was not found for " + entry.shellKey);
        }

        Transform disconnectSurface = FindChildBySuffix(rootObject.transform, ".disconnect_mask");
        Transform screenGlass = FindChildBySuffix(rootObject.transform, ".glass");
        Transform controlsAnchor = FindChildBySuffix(rootObject.transform, ".controls_anchor");
        Transform bottomAnchor = FindChildBySuffix(rootObject.transform, ".bottom_anchor");

        AuthoringSnapshot snapshot = CaptureAndApplyAuthoring(rootObject, entry, screenSurface, disconnectSurface, screenGlass);
        try
        {
            string shellOutputRoot = Path.Combine(outputRoot, entry.shellKey);
            Directory.CreateDirectory(shellOutputRoot);

            Selection.activeGameObject = rootObject;
            UnityBridgeResponse response = UnityBridgeInnerPieceFacade.ExportSelectionFromWindow(
                entry.hostDisplayName,
                shellOutputRoot,
                capturePreview,
                "frameangel,cua_player,host_shell," + entry.shellKey);
            EnsureExportSucceeded(response, entry.shellKey);

            UnityInnerPieceLastExportSummary lastExport = UnityBridgeInnerPieceFacade.LastInnerPieceExport;
            if (lastExport == null || string.IsNullOrWhiteSpace(lastExport.PackageRootPath))
            {
                throw new InvalidOperationException("GhostPlayerHostShellExporter: no export summary was recorded for " + entry.shellKey);
            }

            string geometryPath = lastExport.GeometryPath;
            string screensPath = lastExport.ScreensPath;
            if (string.IsNullOrWhiteSpace(geometryPath) || !File.Exists(geometryPath))
            {
                throw new FileNotFoundException("GhostPlayerHostShellExporter: geometry export is missing.", geometryPath ?? "");
            }

            if (string.IsNullOrWhiteSpace(screensPath) || !File.Exists(screensPath))
            {
                throw new FileNotFoundException("GhostPlayerHostShellExporter: screen contract export is missing.", screensPath ?? "");
            }

            GeometryPackage geometry = JsonUtility.FromJson<GeometryPackage>(File.ReadAllText(geometryPath));
            ScreenContractPackage screenContract = JsonUtility.FromJson<ScreenContractPackage>(File.ReadAllText(screensPath));
            if (geometry == null || geometry.nodes == null || geometry.nodes.Count <= 0)
            {
                throw new InvalidOperationException("GhostPlayerHostShellExporter: geometry export contained no nodes for " + entry.shellKey);
            }

            ScreenSlotEntry slot = screenContract != null && screenContract.slots != null && screenContract.slots.Count > 0
                ? screenContract.slots[0]
                : new ScreenSlotEntry();

            ShellHostProfile profile = new ShellHostProfile
            {
                shellKey = entry.shellKey,
                hostDisplayName = entry.hostDisplayName,
                hostResourceId = entry.hostResourceId,
                hostPackageId = entry.hostPackageId,
                packageRootPath = lastExport.PackageRootPath,
                packageId = lastExport.PackageId ?? "",
                resourceId = lastExport.ResourceId ?? "",
                scenePath = Path.GetFullPath(ScenePath),
                rootObjectName = rootObject.name,
                screenSurfaceNodeId = slot != null ? slot.screenSurfaceNodeId ?? "" : "",
                disconnectSurfaceNodeId = slot != null ? slot.disconnectSurfaceNodeId ?? "" : "",
                screenGlassNodeId = slot != null ? slot.screenGlassNodeId ?? "" : "",
                controlsAnchorNodeId = ResolveNodeIdByTransformSuffix(geometry, controlsAnchor != null ? controlsAnchor.name : "", ".controls_anchor"),
                bottomAnchorNodeId = ResolveNodeIdByTransformSuffix(geometry, bottomAnchor != null ? bottomAnchor.name : "", ".bottom_anchor"),
                deviceClass = entry.deviceClass,
                orientationSupport = entry.orientationSupport,
                defaultAspectMode = entry.defaultAspectMode,
                inputStyle = entry.inputStyle,
                autoOrientToGround = entry.autoOrientToGround,
                safeCornerRadius = entry.safeCornerRadius
            };

            string profilePath = Path.Combine(lastExport.PackageRootPath, "host_profile.json");
            File.WriteAllText(profilePath, JsonUtility.ToJson(profile, true));
            Debug.Log(
                "GhostPlayerHostShellExporter: exported " + entry.shellKey +
                " to " + lastExport.PackageRootPath +
                " | controlsAnchor=" + profile.controlsAnchorNodeId);
            return profile;
        }
        finally
        {
            RestoreAuthoring(snapshot);
        }
    }

    private static AuthoringSnapshot CaptureAndApplyAuthoring(
        GameObject rootObject,
        ShellExportEntry entry,
        Transform screenSurface,
        Transform disconnectSurface,
        Transform screenGlass)
    {
        AuthoringSnapshot snapshot = new AuthoringSnapshot();

        FAPlayerShellAuthoring shellAuthoring = rootObject.GetComponent<FAPlayerShellAuthoring>();
        snapshot.shellAuthoring = shellAuthoring;
        snapshot.shellAuthoringExisted = shellAuthoring != null;
        if (shellAuthoring == null)
        {
            shellAuthoring = rootObject.AddComponent<FAPlayerShellAuthoring>();
        }

        snapshot.shellId = shellAuthoring.shellId;
        snapshot.screenContractVersion = shellAuthoring.screenContractVersion;
        snapshot.defaultDisconnectStateId = shellAuthoring.defaultDisconnectStateId;
        snapshot.surfaceTargetId = shellAuthoring.surfaceTargetId;

        shellAuthoring.shellId = entry.shellKey;
        shellAuthoring.screenContractVersion = "frameangel_screen_contract_v1";
        shellAuthoring.defaultDisconnectStateId = "media_controls";
        shellAuthoring.surfaceTargetId = "player:screen";

        FAPlayerScreenSlotAuthoring slotAuthoring = rootObject.GetComponent<FAPlayerScreenSlotAuthoring>();
        snapshot.slotAuthoring = slotAuthoring;
        snapshot.slotAuthoringExisted = slotAuthoring != null;
        if (slotAuthoring == null)
        {
            slotAuthoring = rootObject.AddComponent<FAPlayerScreenSlotAuthoring>();
        }

        snapshot.slotId = slotAuthoring.slotId;
        snapshot.slotSurfaceTargetId = slotAuthoring.surfaceTargetId;
        snapshot.slotDisconnectStateId = slotAuthoring.disconnectStateId;
        snapshot.screenSurface = slotAuthoring.screenSurface;
        snapshot.screenGlass = slotAuthoring.screenGlass;
        snapshot.screenAperture = slotAuthoring.screenAperture;
        snapshot.disconnectSurface = slotAuthoring.disconnectSurface;

        slotAuthoring.slotId = "main";
        slotAuthoring.surfaceTargetId = "player:screen";
        slotAuthoring.disconnectStateId = "media_controls";
        slotAuthoring.screenSurface = screenSurface;
        slotAuthoring.screenGlass = screenGlass;
        slotAuthoring.screenAperture = null;
        slotAuthoring.disconnectSurface = disconnectSurface;

        return snapshot;
    }

    private static void RestoreAuthoring(AuthoringSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        if (snapshot.slotAuthoring != null)
        {
            if (snapshot.slotAuthoringExisted)
            {
                snapshot.slotAuthoring.slotId = snapshot.slotId;
                snapshot.slotAuthoring.surfaceTargetId = snapshot.slotSurfaceTargetId;
                snapshot.slotAuthoring.disconnectStateId = snapshot.slotDisconnectStateId;
                snapshot.slotAuthoring.screenSurface = snapshot.screenSurface;
                snapshot.slotAuthoring.screenGlass = snapshot.screenGlass;
                snapshot.slotAuthoring.screenAperture = snapshot.screenAperture;
                snapshot.slotAuthoring.disconnectSurface = snapshot.disconnectSurface;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(snapshot.slotAuthoring);
            }
        }

        if (snapshot.shellAuthoring != null)
        {
            if (snapshot.shellAuthoringExisted)
            {
                snapshot.shellAuthoring.shellId = snapshot.shellId;
                snapshot.shellAuthoring.screenContractVersion = snapshot.screenContractVersion;
                snapshot.shellAuthoring.defaultDisconnectStateId = snapshot.defaultDisconnectStateId;
                snapshot.shellAuthoring.surfaceTargetId = snapshot.surfaceTargetId;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(snapshot.shellAuthoring);
            }
        }
    }

    private static GameObject FindObjectBySuffix(string suffix)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        return transforms
            .Where(transform => transform != null && transform.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(transform => transform.gameObject)
            .FirstOrDefault();
    }

    private static Transform FindChildBySuffix(Transform root, string suffix)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform == root)
            {
                continue;
            }

            if (transform.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }
        }

        return null;
    }

    private static string ResolveNodeIdByTransformSuffix(GeometryPackage geometry, string preferredDisplayName, string suffix)
    {
        if (geometry == null || geometry.nodes == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(preferredDisplayName))
        {
            GeometryNode exact = geometry.nodes.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.displayName ?? "", preferredDisplayName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.nodeId ?? "";
            }
        }

        GeometryNode match = geometry.nodes.FirstOrDefault(node =>
            node != null &&
            ((node.displayName ?? "").EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
             (node.nodeId ?? "").EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
        return match != null ? match.nodeId ?? "" : "";
    }

    private static void EnsureExportSucceeded(UnityBridgeResponse response, string shellKey)
    {
        if (response != null && response.Ok)
        {
            return;
        }

        string message = response != null
            ? response.Code + ": " + response.Message
            : "unknown export failure";
        throw new InvalidOperationException("GhostPlayerHostShellExporter: export failed for " + shellKey + ". " + message);
    }

    private static string GetArg(string[] args, string key, string fallback)
    {
        if (args == null)
        {
            return fallback;
        }

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return args[i + 1];
        }

        return fallback;
    }

    private static bool GetBoolArg(string[] args, string key, bool fallback)
    {
        string value = GetArg(args, key, fallback ? "true" : "false");
        bool parsed;
        return bool.TryParse(value, out parsed) ? parsed : fallback;
    }
}
