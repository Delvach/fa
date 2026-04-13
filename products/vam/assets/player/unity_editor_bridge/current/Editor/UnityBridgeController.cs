using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using System.Net.Sockets;

namespace FrameAngel.UnityEditorBridge
{
    [InitializeOnLoad]
    internal static class UnityBridgeController
    {
        private const string HostPrefKey = "FrameAngel.UnityBridge.Host";
        private const string PortPrefKey = "FrameAngel.UnityBridge.Port";
        private const string AutoStartPrefKey = "FrameAngel.UnityBridge.AutoStart";
        private const string AllowUnsafePrefKey = "FrameAngel.UnityBridge.AllowUnsafeApiInvoke";
        private const string AutoRefreshPackageOnChangePrefKey = "FrameAngel.UnityBridge.AutoRefreshPackageOnChange";
        private const string InnerPieceExportRootPrefKey = "FrameAngel.UnityBridge.InnerPieceExportRoot";
        private const string InnerPiecePolicyProfilePrefKey = "FrameAngel.UnityBridge.InnerPiecePolicyProfile";
        private const string LastInnerPieceExportPrefKey = "FrameAngel.UnityBridge.LastInnerPieceExport";
        private const string RestartAfterReloadPrefKey = "FrameAngel.UnityBridge.RestartAfterReload";

        private static UnityBridgeServer server;
        private static bool retryStartQueued;
        private static double retryStartAt;
        private static string packageWatchRoot;
        private static long packageWatchStamp;
        private static double nextPackageWatchPollAt;
        private static bool packageRefreshQueued;
        private static double packageRefreshAt;
        private static string packageRefreshReason = "";

        public const string BridgeVersion = "0.7.20";

        public static UnityLastCaptureSummary LastCapture { get; set; }
        public static UnityInnerPieceLastExportSummary LastInnerPieceExport
        {
            get
            {
                string json = EditorPrefs.GetString(LastInnerPieceExportPrefKey, "");
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                try
                {
                    return JsonConvert.DeserializeObject<UnityInnerPieceLastExportSummary>(json);
                }
                catch
                {
                    return null;
                }
            }
            set
            {
                string json = value == null ? "" : JsonConvert.SerializeObject(value, Formatting.None);
                EditorPrefs.SetString(LastInnerPieceExportPrefKey, json);
            }
        }

        static UnityBridgeController()
        {
            InitializePackageWatch();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.delayCall += TryAutoStart;
            EditorApplication.update += PumpRetryStart;
            EditorApplication.update += PumpPackageWatch;
            EditorApplication.update += PumpQueuedPackageRefresh;
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoadBootstrap()
        {
            Debug.Log("[FrameAngel.UnityBridge] InitializeOnLoadBootstrap invoked.");
            EditorApplication.delayCall += TryAutoStart;
            TryAutoStart();
        }

        public static string Host
        {
            get { return EditorPrefs.GetString(HostPrefKey, "127.0.0.1"); }
            set { EditorPrefs.SetString(HostPrefKey, value ?? "127.0.0.1"); }
        }

        public static int Port
        {
            get { return EditorPrefs.GetInt(PortPrefKey, 8797); }
            set { EditorPrefs.SetInt(PortPrefKey, value <= 0 ? 8797 : value); }
        }

        public static bool AutoStart
        {
            get { return EditorPrefs.GetBool(AutoStartPrefKey, false); }
            set { EditorPrefs.SetBool(AutoStartPrefKey, value); }
        }

        public static bool AllowUnsafeApiInvoke
        {
            get { return EditorPrefs.GetBool(AllowUnsafePrefKey, false); }
            set { EditorPrefs.SetBool(AllowUnsafePrefKey, value); }
        }

        public static bool AutoRefreshPackageOnChange
        {
            get { return EditorPrefs.GetBool(AutoRefreshPackageOnChangePrefKey, true); }
            set { EditorPrefs.SetBool(AutoRefreshPackageOnChangePrefKey, value); }
        }

        public static bool IsRunning
        {
            get { return server != null; }
        }

        public static string Endpoint
        {
            get { return "http://" + Host + ":" + Port; }
        }

        public static string PackageWatchRoot
        {
            get { return packageWatchRoot ?? ""; }
        }

        public static string CaptureRoot
        {
            get
            {
                string root = Path.Combine(UnityBridgeInspector.ProjectPath, "Library", "FrameAngelUnityBridge", "Captures");
                Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string InnerPieceExportRoot
        {
            get
            {
                string configured = EditorPrefs.GetString(InnerPieceExportRootPrefKey, "");
                string root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(UnityBridgeInspector.ProjectPath, "Library", "FrameAngelUnityBridge", "InnerPieceExports")
                    : configured;
                Directory.CreateDirectory(root);
                return root;
            }
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value)
                    ? Path.Combine(UnityBridgeInspector.ProjectPath, "Library", "FrameAngelUnityBridge", "InnerPieceExports")
                    : value;
                EditorPrefs.SetString(InnerPieceExportRootPrefKey, normalized);
            }
        }

        public static UnityInnerPiecePolicyProfile InnerPiecePolicyProfile
        {
            get
            {
                string json = EditorPrefs.GetString(InnerPiecePolicyProfilePrefKey, "");
                if (string.IsNullOrWhiteSpace(json))
                {
                    return UnityInnerPiecePolicyProfile.CreateDefault();
                }

                try
                {
                    UnityInnerPiecePolicyProfile profile = JsonConvert.DeserializeObject<UnityInnerPiecePolicyProfile>(json);
                    return profile ?? UnityInnerPiecePolicyProfile.CreateDefault();
                }
                catch
                {
                    return UnityInnerPiecePolicyProfile.CreateDefault();
                }
            }
            set
            {
                UnityInnerPiecePolicyProfile profile = value ?? UnityInnerPiecePolicyProfile.CreateDefault();
                EditorPrefs.SetString(
                    InnerPiecePolicyProfilePrefKey,
                    JsonConvert.SerializeObject(profile, Formatting.None));
            }
        }

        [MenuItem("FrameAngel/Unity Bridge/Open Control Panel")]
        public static void OpenWindow()
        {
            UnityBridgeWindow.Open();
        }

        [MenuItem("FrameAngel/Unity Bridge/Start Server")]
        public static void Start()
        {
            if (IsRunning)
            {
                Debug.Log("[FrameAngel.UnityBridge] Start skipped; server already running at " + Endpoint + ".");
                return;
            }

            try
            {
                Debug.Log("[FrameAngel.UnityBridge] Starting server at " + Endpoint + ".");
                server = new UnityBridgeServer(Host, Port);
                server.Start();
                Debug.Log("[FrameAngel.UnityBridge] Server started at " + Endpoint + ".");
            }
            catch (SocketException ex)
            {
                if (server != null)
                {
                    server.Stop();
                    server = null;
                }

                Debug.LogWarning("FrameAngel Unity Bridge failed to bind immediately and will retry: " + ex.Message);
                ScheduleRetryStart();
            }
            catch (Exception ex)
            {
                if (server != null)
                {
                    server.Stop();
                    server = null;
                }

                Debug.LogError("[FrameAngel.UnityBridge] Start failed: " + ex);
                ScheduleRetryStart();
            }
        }

        [MenuItem("FrameAngel/Unity Bridge/Stop Server")]
        public static void Stop()
        {
            if (server == null)
            {
                return;
            }

            Debug.Log("[FrameAngel.UnityBridge] Stopping server at " + Endpoint + ".");
            server.Stop();
            server = null;
        }

        private static void HandleBeforeAssemblyReload()
        {
            if (server != null)
            {
                Debug.Log("[FrameAngel.UnityBridge] Assembly reload requested; will restart bridge after reload.");
                EditorPrefs.SetBool(RestartAfterReloadPrefKey, true);
            }

            Stop();
        }

        public static void QueuePackageRefresh(string reason)
        {
            packageRefreshQueued = true;
            packageRefreshReason = string.IsNullOrWhiteSpace(reason) ? "manual request" : reason;
            packageRefreshAt = EditorApplication.timeSinceStartup + 0.2d;
        }

        private static void TryAutoStart()
        {
            bool restartAfterReload = EditorPrefs.GetBool(RestartAfterReloadPrefKey, false);
            if (restartAfterReload)
            {
                EditorPrefs.SetBool(RestartAfterReloadPrefKey, false);
            }

            Debug.Log(
                "[FrameAngel.UnityBridge] TryAutoStart: autoStart=" + AutoStart +
                ", restartAfterReload=" + restartAfterReload +
                ", isRunning=" + IsRunning + ".");

            if (!IsRunning && (AutoStart || restartAfterReload))
            {
                Start();
            }

            if (!IsRunning && (AutoStart || restartAfterReload))
            {
                ScheduleRetryStart(1.0);
            }
        }

        private static void ScheduleRetryStart()
        {
            ScheduleRetryStart(2.0);
        }

        private static void ScheduleRetryStart(double delaySeconds)
        {
            retryStartQueued = true;
            retryStartAt = EditorApplication.timeSinceStartup + Math.Max(0.1, delaySeconds);
        }

        private static void PumpRetryStart()
        {
            if (!retryStartQueued || IsRunning)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < retryStartAt)
            {
                return;
            }

            retryStartQueued = false;
            Debug.Log("[FrameAngel.UnityBridge] PumpRetryStart firing.");
            Start();
        }

        private static void InitializePackageWatch()
        {
            packageWatchRoot = ResolvePackageWatchRoot();
            packageWatchStamp = ComputePackageWatchStamp();
        }

        private static string ResolvePackageWatchRoot()
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo packageInfo =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityBridgeController).Assembly);
                if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
                {
                    return Path.GetFullPath(packageInfo.resolvedPath);
                }
            }
            catch
            {
            }

            return "";
        }

        private static void PumpPackageWatch()
        {
            if (!AutoRefreshPackageOnChange)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextPackageWatchPollAt)
            {
                return;
            }

            nextPackageWatchPollAt = EditorApplication.timeSinceStartup + 1.0d;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            long currentStamp = ComputePackageWatchStamp();
            if (currentStamp <= 0)
            {
                return;
            }

            if (packageWatchStamp <= 0)
            {
                packageWatchStamp = currentStamp;
                return;
            }

            if (currentStamp <= packageWatchStamp)
            {
                return;
            }

            packageWatchStamp = currentStamp;
            QueuePackageRefresh("package source change");
        }

        private static void PumpQueuedPackageRefresh()
        {
            if (!packageRefreshQueued)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < packageRefreshAt)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            packageRefreshQueued = false;
            string reason = packageRefreshReason;
            packageRefreshReason = "";
            packageWatchStamp = ComputePackageWatchStamp();
            Debug.Log("[FrameAngel.UnityBridge] Refreshing package because " + reason + ".");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static long ComputePackageWatchStamp()
        {
            if (string.IsNullOrWhiteSpace(packageWatchRoot) || !Directory.Exists(packageWatchRoot))
            {
                return 0L;
            }

            long stamp = 0L;
            foreach (string path in EnumeratePackageWatchFiles(packageWatchRoot))
            {
                try
                {
                    long fileStamp = File.GetLastWriteTimeUtc(path).Ticks;
                    if (fileStamp > stamp)
                    {
                        stamp = fileStamp;
                    }
                }
                catch
                {
                }
            }

            return stamp;
        }

        private static IEnumerable<string> EnumeratePackageWatchFiles(string root)
        {
            string packageJsonPath = Path.Combine(root, "package.json");
            if (File.Exists(packageJsonPath))
            {
                yield return packageJsonPath;
            }

            string readmePath = Path.Combine(root, "README.md");
            if (File.Exists(readmePath))
            {
                yield return readmePath;
            }

            foreach (string subdirectory in new[] { "Editor", "Runtime" })
            {
                string directoryPath = Path.Combine(root, subdirectory);
                if (!Directory.Exists(directoryPath))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path);
                    if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    yield return path;
                }
            }
        }
    }
}
