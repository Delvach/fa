using UnityEditor;
using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    internal sealed class UnityBridgeWindow : EditorWindow
    {
        private string host;
        private int port;
        private string innerPieceExportRoot;
        private string innerPieceDisplayNameOverride = "";
        private string innerPieceTagsCsv = "";
        private bool innerPieceCapturePreview = true;
        private Vector2 scrollPosition;

        public static void Open()
        {
            UnityBridgeWindow window = GetWindow<UnityBridgeWindow>("Unity Bridge");
            window.minSize = new Vector2(420f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            host = UnityBridgeController.Host;
            port = UnityBridgeController.Port;
            innerPieceExportRoot = UnityBridgeController.InnerPieceExportRoot;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("FrameAngel Unity Editor Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Bounded Unity editor bridge with observe/capture/workspace commands plus InnerPiece export packaging.", MessageType.Info);

            using (new EditorGUI.DisabledScope(UnityBridgeController.IsRunning))
            {
                host = EditorGUILayout.TextField("Host", host);
                port = EditorGUILayout.IntField("Port", port);
            }

            bool autoStart = EditorGUILayout.Toggle("Auto Start", UnityBridgeController.AutoStart);
            if (autoStart != UnityBridgeController.AutoStart)
            {
                UnityBridgeController.AutoStart = autoStart;
            }

            bool allowUnsafe = EditorGUILayout.Toggle("Allow Unsafe API Invoke", UnityBridgeController.AllowUnsafeApiInvoke);
            if (allowUnsafe != UnityBridgeController.AllowUnsafeApiInvoke)
            {
                UnityBridgeController.AllowUnsafeApiInvoke = allowUnsafe;
            }

            bool autoRefreshPackage = EditorGUILayout.Toggle("Auto Refresh Package On Change", UnityBridgeController.AutoRefreshPackageOnChange);
            if (autoRefreshPackage != UnityBridgeController.AutoRefreshPackageOnChange)
            {
                UnityBridgeController.AutoRefreshPackageOnChange = autoRefreshPackage;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", UnityBridgeController.IsRunning ? "Running" : "Stopped");
            EditorGUILayout.SelectableLabel(UnityBridgeController.Endpoint, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(UnityBridgeController.CaptureRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(UnityBridgeController.PackageWatchRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            innerPieceExportRoot = EditorGUILayout.TextField("InnerPiece Export Root", innerPieceExportRoot);

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Settings"))
            {
                UnityBridgeController.Host = string.IsNullOrEmpty(host) ? "127.0.0.1" : host;
                UnityBridgeController.Port = port <= 0 ? 8797 : port;
                UnityBridgeController.InnerPieceExportRoot = string.IsNullOrWhiteSpace(innerPieceExportRoot)
                    ? UnityBridgeController.InnerPieceExportRoot
                    : innerPieceExportRoot;
            }

            using (new EditorGUI.DisabledScope(UnityBridgeController.IsRunning))
            {
                if (GUILayout.Button("Start"))
                {
                    UnityBridgeController.Host = string.IsNullOrEmpty(host) ? "127.0.0.1" : host;
                    UnityBridgeController.Port = port <= 0 ? 8797 : port;
                    UnityBridgeController.Start();
                }
            }

            using (new EditorGUI.DisabledScope(!UnityBridgeController.IsRunning))
            {
                if (GUILayout.Button("Stop"))
                {
                    UnityBridgeController.Stop();
                }
            }

            if (GUILayout.Button("Refresh Package"))
            {
                UnityBridgeController.QueuePackageRefresh("control panel");
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();
            GUILayout.Label("InnerPiece Exporter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Selection export packages static geometry into a hybrid InnerPiece directory with manifest, geometry, optional materials, optional preview, and export receipt.",
                MessageType.None);
            innerPieceDisplayNameOverride = EditorGUILayout.TextField("Display Name Override", innerPieceDisplayNameOverride);
            innerPieceTagsCsv = EditorGUILayout.TextField("Tags CSV", innerPieceTagsCsv);
            innerPieceCapturePreview = EditorGUILayout.Toggle("Capture Preview", innerPieceCapturePreview);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Inspect Selection"))
            {
                string inspectError;
                UnityInnerPieceInspectionData inspection = UnityBridgeInnerPieceService.InspectSelectionFromWindow(out inspectError);
                if (inspection == null)
                {
                    Debug.LogWarning("[FrameAngel.UnityBridge] InnerPiece inspect failed: " + inspectError);
                }
                else
                {
                    Debug.Log("[FrameAngel.UnityBridge] InnerPiece inspect: " +
                        inspection.DisplayName + " | " + inspection.AssetClass + " | exportReady=" + inspection.ExportReady);
                }
            }

            if (GUILayout.Button("Export Selection"))
            {
                UnityBridgeController.InnerPieceExportRoot = string.IsNullOrWhiteSpace(innerPieceExportRoot)
                    ? UnityBridgeController.InnerPieceExportRoot
                    : innerPieceExportRoot;
                UnityBridgeResponse response = UnityBridgeInnerPieceService.ExportSelectionFromWindow(
                    innerPieceDisplayNameOverride,
                    innerPieceExportRoot,
                    innerPieceCapturePreview,
                    innerPieceTagsCsv);
                if (!response.Ok)
                {
                    Debug.LogWarning("[FrameAngel.UnityBridge] InnerPiece export failed: " + response.Message);
                }
                else
                {
                    Debug.Log("[FrameAngel.UnityBridge] InnerPiece export succeeded.");
                }
            }
            GUILayout.EndHorizontal();

            UnityInnerPieceLastExportSummary lastExport = UnityBridgeController.LastInnerPieceExport;
            if (lastExport != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label("Last InnerPiece Export", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Display Name", lastExport.DisplayName ?? "");
                EditorGUILayout.LabelField("Package Id", lastExport.PackageId ?? "");
                EditorGUILayout.LabelField("Resource Id", lastExport.ResourceId ?? "");
                EditorGUILayout.LabelField("Fingerprint", lastExport.Fingerprint ?? "");
                EditorGUILayout.LabelField("Package Bytes", lastExport.PackageBytes.ToString());
                EditorGUILayout.LabelField("Export Duration (ms)", lastExport.ExportDurationMs.ToString());
                EditorGUILayout.SelectableLabel(lastExport.PackageRootPath ?? "", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Implemented commands now include observe.*, capture.*, scene.*, and asset.innerpiece.*.",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
