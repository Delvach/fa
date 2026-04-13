using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    internal static class UnityBridgeInnerPieceService
    {
        private sealed class CollectedNode
        {
            public Transform Transform;
            public string NodeId = "";
            public string ParentNodeId = "";
            public readonly List<CollectedMeshPart> MeshParts = new List<CollectedMeshPart>();
        }

        private sealed class CollectedMeshPart
        {
            public Mesh Mesh;
            public int SubmeshIndex;
            public string MeshId = "";
            public string MaterialRefId = "";
            public int VertexCount;
            public int TriangleCount;
        }

        private sealed class Snapshot
        {
            public readonly List<CollectedNode> Nodes = new List<CollectedNode>();
            public readonly List<UnityInnerPieceMaterialSummary> Materials = new List<UnityInnerPieceMaterialSummary>();
            public readonly List<string> FeatureFlags = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public FAIPEScreenContractPackage ScreenContract;
            public FAIPEControlSurfacePackage ControlSurfaceContract;
            public ControlSurfaceCanvasSnapshot CanvasSnapshot;
            public Bounds Bounds;
            public bool HasBounds;
            public bool HasSkinnedMesh;
            public bool HasAnimation;
            public bool HasInvalidScreenContract;
            public bool HasInvalidControlSurface;
            public int RendererCount;
            public int TotalVertexCount;
            public int TotalTriangleCount;
            public int MeshCount;
        }

        private sealed class ControlSurfaceCanvasSnapshot
        {
            public string SurfaceNodeId = "";
            public string MaterialRefId = "";
            public string TexturePngBase64 = "";
            public float LocalXMin = -0.5f;
            public float LocalXMax = 0.5f;
            public float LocalYMin = -0.5f;
            public float LocalYMax = 0.5f;
        }

        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include };

        public static UnityBridgeResponse InspectSelection(UnityBridgeCommandRequest request)
        {
            UnityInnerPieceExportSource source;
            string errorMessage;
            if (!TryResolveSelectionSource(out source, out errorMessage))
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", errorMessage, request != null ? request.RequestId : "");
            }

            UnityInnerPieceInspectionData inspection = InspectSource(source, UnityBridgeController.InnerPiecePolicyProfile);
            return new UnityBridgeResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Data = new Dictionary<string, object>
                {
                    { "inspection", inspection },
                    { "policyProfile", UnityBridgeController.InnerPiecePolicyProfile }
                }
            };
        }

        public static UnityBridgeResponse ExportSelection(UnityBridgeCommandRequest request)
        {
            UnityInnerPieceExportSelectionArgs args = request != null && request.Args != null
                ? request.Args.ToObject<UnityInnerPieceExportSelectionArgs>()
                : new UnityInnerPieceExportSelectionArgs();

            UnityInnerPieceExportSource source;
            string errorMessage;
            if (!TryResolveSelectionSource(out source, out errorMessage))
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", errorMessage, request != null ? request.RequestId : "");
            }

            return ExportSource(request != null ? request.RequestId : "", source, args);
        }

        public static UnityBridgeResponse ExportProjectAsset(UnityBridgeCommandRequest request)
        {
            UnityInnerPieceExportProjectAssetArgs args = request != null && request.Args != null
                ? request.Args.ToObject<UnityInnerPieceExportProjectAssetArgs>()
                : new UnityInnerPieceExportProjectAssetArgs();

            UnityInnerPieceExportSource source;
            string errorMessage;
            if (!TryResolveProjectAssetSource(args.AssetPath, args.AssetGuid, out source, out errorMessage))
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", errorMessage, request != null ? request.RequestId : "");
            }

            return ExportSource(request != null ? request.RequestId : "", source, args);
        }

        public static UnityBridgeResponse CapturePreview(UnityBridgeCommandRequest request)
        {
            UnityInnerPieceCapturePreviewArgs args = request != null && request.Args != null
                ? request.Args.ToObject<UnityInnerPieceCapturePreviewArgs>()
                : new UnityInnerPieceCapturePreviewArgs();

            UnityInnerPieceExportSource source;
            string errorMessage;
            if (!string.IsNullOrWhiteSpace(args.AssetPath) || !string.IsNullOrWhiteSpace(args.AssetGuid))
            {
                if (!TryResolveProjectAssetSource(args.AssetPath, args.AssetGuid, out source, out errorMessage))
                {
                    return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", errorMessage, request != null ? request.RequestId : "");
                }
            }
            else if (!TryResolveSelectionSource(out source, out errorMessage))
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", errorMessage, request != null ? request.RequestId : "");
            }

            string label = string.IsNullOrWhiteSpace(args.Label) ? "innerpiece_preview" : args.Label.Trim();
            string outputPath = string.IsNullOrWhiteSpace(args.OutputPath)
                ? Path.Combine(UnityBridgeController.InnerPieceExportRoot, "preview", label + ".png")
                : ResolveAbsolutePath(args.OutputPath);

            UnityBridgeArtifact artifact;
            List<string> warnings;
            if (!TryCapturePreviewArtifact(source, label, outputPath, args.Width, args.Height, out artifact, out warnings, out errorMessage))
            {
                return UnityBridgeResponse.Error("CAMERA_CAPTURE_FAILED", errorMessage, request != null ? request.RequestId : "");
            }

            return new UnityBridgeResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Data = new Dictionary<string, object>
                {
                    { "previewPath", artifact.Path },
                    { "sourceKind", source.SourceKind },
                    { "sourcePath", source.SourcePath }
                },
                Artifacts = new List<UnityBridgeArtifact> { artifact },
                Warnings = warnings
            };
        }

        public static UnityBridgeResponse GetLastExport(UnityBridgeCommandRequest request)
        {
            return new UnityBridgeResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Data = new Dictionary<string, object>
                {
                    { "lastExport", UnityBridgeController.LastInnerPieceExport },
                    { "policyProfile", UnityBridgeController.InnerPiecePolicyProfile }
                }
            };
        }

        public static UnityBridgeResponse ExportSelectionFromWindow(
            string displayNameOverride,
            string outputPath,
            bool capturePreview,
            string tagsCsv)
        {
            UnityBridgeCommandRequest request = new UnityBridgeCommandRequest
            {
                RequestId = "window_export_selection",
                Action = "asset.innerpiece.export_selection",
                Args = Newtonsoft.Json.Linq.JObject.FromObject(new UnityInnerPieceExportSelectionArgs
                {
                    DisplayNameOverride = displayNameOverride ?? "",
                    OutputPath = outputPath ?? "",
                    CapturePreview = capturePreview,
                    TagsCsv = tagsCsv ?? ""
                })
            };

            return ExportSelection(request);
        }

        public static UnityInnerPieceInspectionData InspectSelectionFromWindow(out string errorMessage)
        {
            UnityInnerPieceExportSource source;
            if (!TryResolveSelectionSource(out source, out errorMessage))
            {
                return null;
            }

            return InspectSource(source, UnityBridgeController.InnerPiecePolicyProfile);
        }

        private static UnityBridgeResponse ExportSource(
            string requestId,
            UnityInnerPieceExportSource source,
            UnityInnerPieceExportSelectionArgs args)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            UnityInnerPieceInspectionData inspection = InspectSource(source, UnityBridgeController.InnerPiecePolicyProfile);
            if (!inspection.ExportReady)
            {
                return new UnityBridgeResponse
                {
                    Ok = false,
                    Code = "EXPORT_NOT_READY",
                    Message = "Selection is not in the first releasable InnerPiece export surface.",
                    RequestId = requestId,
                    Data = new Dictionary<string, object>
                    {
                        { "inspection", inspection }
                    },
                    Warnings = inspection.Warnings
                };
            }

            Snapshot snapshot = BuildSnapshot(source.RootObject);
            string displayName = string.IsNullOrWhiteSpace(args.DisplayNameOverride)
                ? inspection.DisplayName
                : args.DisplayNameOverride.Trim();
            string[] mergedTags = MergeTags(args.Tags, args.TagsCsv);

            FAIPEGeometryPackage geometry = BuildGeometryPackage(source, snapshot, displayName, mergedTags);
            string fingerprint = BuildGeometryFingerprint(geometry, snapshot.ScreenContract, snapshot.ControlSurfaceContract);
            string resourceId = BuildStableResourceId(displayName, fingerprint);
            string packageId = "faipe_" + resourceId;
            string packageRoot = ResolvePackageRoot(args.OutputPath, packageId);
            string previewPath = Path.Combine(packageRoot, "preview", "thumbnail.png");
            string geometryPath = Path.Combine(packageRoot, "geometry.innerpiece.json");
            string manifestPath = Path.Combine(packageRoot, "manifest.json");
            string materialsPath = Path.Combine(packageRoot, "materials.innerpiece.json");
            string screensPath = Path.Combine(packageRoot, "screens.innerpiece.json");
            string controlsPath = Path.Combine(packageRoot, "controls.innerpiece.json");
            string receiptPath = Path.Combine(packageRoot, "export_receipt.json");

            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, true);
            }

            Directory.CreateDirectory(packageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(geometryPath) ?? packageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? packageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath) ?? packageRoot);

            FAIPEMaterialsPackage materials = BuildMaterialsPackage(snapshot);
            FAIPEScreenContractPackage screenContract = snapshot.ScreenContract;
            FAIPEControlSurfacePackage controlSurfaceContract = snapshot.ControlSurfaceContract;
            List<UnityBridgeArtifact> artifacts = new List<UnityBridgeArtifact>();
            List<string> warnings = new List<string>(inspection.Warnings);
            if (materials.Materials.Count <= 0)
            {
                materialsPath = "";
            }
            if (screenContract == null || screenContract.Slots == null || screenContract.Slots.Count <= 0)
            {
                screensPath = "";
            }
            if (controlSurfaceContract == null || controlSurfaceContract.Elements == null || controlSurfaceContract.Elements.Count <= 0)
            {
                controlsPath = "";
            }

            if (args.CapturePreview)
            {
                UnityBridgeArtifact previewArtifact;
                List<string> previewWarnings;
                string previewError;
                Directory.CreateDirectory(Path.GetDirectoryName(previewPath) ?? packageRoot);
                if (TryCapturePreviewArtifact(source, "innerpiece_preview", previewPath, 512, 512, out previewArtifact, out previewWarnings, out previewError))
                {
                    artifacts.Add(previewArtifact);
                    warnings.AddRange(previewWarnings);
                }
                else
                {
                    previewPath = "";
                    warnings.Add("preview_capture_failed:" + previewError);
                }
            }
            else
            {
                previewPath = "";
            }

            string exportedAtUtc = UnityBridgeInspector.TimestampUtc();
            FAIPEPackageManifest manifest = new FAIPEPackageManifest
            {
                PackageId = packageId,
                ResourceId = resourceId,
                DisplayName = displayName,
                ControlSurfaceId = controlSurfaceContract != null ? controlSurfaceContract.ControlSurfaceId : "",
                ControlFamilyId = controlSurfaceContract != null ? controlSurfaceContract.ControlFamilyId : "",
                ControlThemeId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeId : "",
                ControlThemeLabel = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeLabel : "",
                ControlThemeVariantId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeVariantId : "",
                ControlThemeAssetPath = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetPath : "",
                ControlThemeAssetGuid = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetGuid : "",
                ToolkitCategory = controlSurfaceContract != null ? controlSurfaceContract.ToolkitCategory : "",
                SourcePrefabAssetPath = controlSurfaceContract != null ? controlSurfaceContract.SourcePrefabAssetPath : "",
                ExporterVersion = UnityBridgeController.BridgeVersion,
                ExportedAtUtc = exportedAtUtc,
                SourceKind = source.SourceKind,
                SourcePath = source.SourcePath,
                GeometryPath = "geometry.innerpiece.json",
                MaterialsPath = string.IsNullOrEmpty(materialsPath) ? "" : "materials.innerpiece.json",
                ScreensPath = string.IsNullOrEmpty(screensPath) ? "" : "screens.innerpiece.json",
                ControlsPath = string.IsNullOrEmpty(controlsPath) ? "" : "controls.innerpiece.json",
                PreviewPath = string.IsNullOrEmpty(previewPath) ? "" : "preview/thumbnail.png",
                FeatureFlags = new List<string>(inspection.FeatureFlags),
                Warnings = new List<string>(warnings)
            };

            File.WriteAllText(geometryPath, JsonConvert.SerializeObject(geometry, Formatting.Indented, JsonSettings));
            if (!string.IsNullOrEmpty(materialsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(materialsPath) ?? packageRoot);
                File.WriteAllText(materialsPath, JsonConvert.SerializeObject(materials, Formatting.Indented, JsonSettings));
            }
            if (!string.IsNullOrEmpty(screensPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screensPath) ?? packageRoot);
                File.WriteAllText(screensPath, JsonConvert.SerializeObject(screenContract, Formatting.Indented, JsonSettings));
            }
            if (!string.IsNullOrEmpty(controlsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(controlsPath) ?? packageRoot);
                File.WriteAllText(controlsPath, JsonConvert.SerializeObject(controlSurfaceContract, Formatting.Indented, JsonSettings));
            }

            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented, JsonSettings));
            stopwatch.Stop();

            FAIPEExportReceipt exportReceipt = new FAIPEExportReceipt
            {
                PackageId = packageId,
                ResourceId = resourceId,
                ControlSurfaceId = controlSurfaceContract != null ? controlSurfaceContract.ControlSurfaceId : "",
                ControlFamilyId = controlSurfaceContract != null ? controlSurfaceContract.ControlFamilyId : "",
                ControlThemeId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeId : "",
                ControlThemeLabel = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeLabel : "",
                ControlThemeVariantId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeVariantId : "",
                ControlThemeAssetPath = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetPath : "",
                ControlThemeAssetGuid = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetGuid : "",
                ToolkitCategory = controlSurfaceContract != null ? controlSurfaceContract.ToolkitCategory : "",
                SourcePrefabAssetPath = controlSurfaceContract != null ? controlSurfaceContract.SourcePrefabAssetPath : "",
                Fingerprint = fingerprint,
                SourceKind = source.SourceKind,
                SourcePath = source.SourcePath,
                ExportedAtUtc = exportedAtUtc,
                ExportDurationMs = stopwatch.ElapsedMilliseconds,
                NodeCount = inspection.NodeCount,
                MeshCount = inspection.MeshCount,
                MaterialCount = inspection.MaterialCount,
                TotalVertexCount = inspection.TotalVertexCount,
                TotalTriangleCount = inspection.TotalTriangleCount,
                FeatureFlags = new List<string>(inspection.FeatureFlags),
                Warnings = new List<string>(warnings)
            };

            File.WriteAllText(receiptPath, JsonConvert.SerializeObject(exportReceipt, Formatting.Indented, JsonSettings));
            exportReceipt.PackageBytes = ComputePackageBytes(packageRoot);
            File.WriteAllText(receiptPath, JsonConvert.SerializeObject(exportReceipt, Formatting.Indented, JsonSettings));

            UnityInnerPieceLastExportSummary summary = new UnityInnerPieceLastExportSummary
            {
                PackageId = packageId,
                ResourceId = resourceId,
                DisplayName = displayName,
                ControlSurfaceId = controlSurfaceContract != null ? controlSurfaceContract.ControlSurfaceId : "",
                ControlFamilyId = controlSurfaceContract != null ? controlSurfaceContract.ControlFamilyId : "",
                ControlThemeId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeId : "",
                ControlThemeLabel = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeLabel : "",
                ControlThemeVariantId = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeVariantId : "",
                ControlThemeAssetPath = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetPath : "",
                ControlThemeAssetGuid = controlSurfaceContract != null ? controlSurfaceContract.ControlThemeAssetGuid : "",
                ToolkitCategory = controlSurfaceContract != null ? controlSurfaceContract.ToolkitCategory : "",
                SourcePrefabAssetPath = controlSurfaceContract != null ? controlSurfaceContract.SourcePrefabAssetPath : "",
                SourceKind = source.SourceKind,
                SourcePath = source.SourcePath,
                PackageRootPath = packageRoot,
                GeometryPath = geometryPath,
                MaterialsPath = materialsPath,
                ScreensPath = screensPath,
                ControlsPath = controlsPath,
                PreviewPath = previewPath,
                ExportReceiptPath = receiptPath,
                Fingerprint = fingerprint,
                FeatureFlags = new List<string>(inspection.FeatureFlags),
                Warnings = new List<string>(warnings),
                NodeCount = inspection.NodeCount,
                MeshCount = inspection.MeshCount,
                MaterialCount = inspection.MaterialCount,
                TotalVertexCount = inspection.TotalVertexCount,
                TotalTriangleCount = inspection.TotalTriangleCount,
                PackageBytes = exportReceipt.PackageBytes,
                ExportDurationMs = stopwatch.ElapsedMilliseconds,
                ExportedAtUtc = exportedAtUtc
            };

            UnityBridgeController.LastInnerPieceExport = summary;

            return new UnityBridgeResponse
            {
                RequestId = requestId,
                Data = new UnityInnerPieceExportData
                {
                    Inspection = inspection,
                    LastExport = summary
                },
                Artifacts = artifacts,
                Warnings = warnings
            };
        }

        private static UnityInnerPieceInspectionData InspectSource(UnityInnerPieceExportSource source, UnityInnerPiecePolicyProfile policyProfile)
        {
            Snapshot snapshot = BuildSnapshot(source != null ? source.RootObject : null);
            UnityInnerPieceInspectionData inspection = new UnityInnerPieceInspectionData
            {
                ExportReady = IsExportReady(snapshot),
                AssetClass = ResolveAssetClass(snapshot),
                RecommendedExportStage = ResolveExportStage(snapshot),
                SourceKind = source != null ? source.SourceKind : "",
                SourcePath = source != null ? source.SourcePath : "",
                DisplayName = source != null ? source.DefaultDisplayName : "",
                HasScreenContract = snapshot.ScreenContract != null && snapshot.ScreenContract.Slots != null && snapshot.ScreenContract.Slots.Count > 0,
                ScreenContractVersion = snapshot.ScreenContract != null ? snapshot.ScreenContract.SchemaVersion : "",
                ShellId = snapshot.ScreenContract != null ? snapshot.ScreenContract.ShellId : "",
                ScreenSlotCount = snapshot.ScreenContract != null && snapshot.ScreenContract.Slots != null ? snapshot.ScreenContract.Slots.Count : 0,
                HasControlSurfaceContract = snapshot.ControlSurfaceContract != null && snapshot.ControlSurfaceContract.Elements != null && snapshot.ControlSurfaceContract.Elements.Count > 0,
                ControlSurfaceContractVersion = snapshot.ControlSurfaceContract != null ? snapshot.ControlSurfaceContract.SchemaVersion : "",
                ControlSurfaceId = snapshot.ControlSurfaceContract != null ? snapshot.ControlSurfaceContract.ControlSurfaceId : "",
                ControlElementCount = snapshot.ControlSurfaceContract != null && snapshot.ControlSurfaceContract.Elements != null ? snapshot.ControlSurfaceContract.Elements.Count : 0,
                NodeCount = snapshot.Nodes.Count,
                MeshCount = snapshot.MeshCount,
                MaterialCount = snapshot.Materials.Count,
                RendererCount = snapshot.RendererCount,
                TotalVertexCount = snapshot.TotalVertexCount,
                TotalTriangleCount = snapshot.TotalTriangleCount,
                Bounds = snapshot.HasBounds ? UnityBounds3.FromBounds(snapshot.Bounds) : new UnityBounds3(),
                FeatureFlags = snapshot.FeatureFlags.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = snapshot.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Materials = snapshot.Materials.OrderBy(material => material.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
                PolicyProfile = policyProfile ?? UnityInnerPiecePolicyProfile.CreateDefault()
            };

            if (inspection.PolicyProfile != null)
            {
                if (inspection.NodeCount > inspection.PolicyProfile.MaxNodeCount)
                    inspection.Warnings.Add("policy_node_count_exceeded");
                if (inspection.MeshCount > inspection.PolicyProfile.MaxMeshCount)
                    inspection.Warnings.Add("policy_mesh_count_exceeded");
                if (inspection.TotalVertexCount > inspection.PolicyProfile.MaxTotalVertexCount)
                    inspection.Warnings.Add("policy_vertex_count_exceeded");
                if (inspection.TotalTriangleCount > inspection.PolicyProfile.MaxTotalTriangleCount)
                    inspection.Warnings.Add("policy_triangle_count_exceeded");
                if (inspection.PolicyProfile.AllowedSourceClasses != null &&
                    inspection.PolicyProfile.AllowedSourceClasses.Count > 0 &&
                    !inspection.PolicyProfile.AllowedSourceClasses.Contains(inspection.AssetClass))
                {
                    inspection.Warnings.Add("policy_source_class_not_enabled");
                }
            }

            return inspection;
        }

        private static Snapshot BuildSnapshot(GameObject rootObject)
        {
            Snapshot snapshot = new Snapshot();
            if (rootObject == null)
            {
                snapshot.Warnings.Add("missing_root_object");
                return snapshot;
            }

            Dictionary<Material, UnityInnerPieceMaterialSummary> materialLookup =
                new Dictionary<Material, UnityInnerPieceMaterialSummary>();
            Dictionary<string, string> meshIdentityLookup =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<Transform, string> nodeIdLookup =
                new Dictionary<Transform, string>();
            HashSet<string> usedNodeIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            List<Transform> orderedTransforms = new List<Transform>();
            GatherTransforms(rootObject.transform, orderedTransforms);

            for (int i = 0; i < orderedTransforms.Count; i++)
            {
                Transform current = orderedTransforms[i];
                string relativePath = BuildRelativePath(rootObject.transform, current);
                string nodeId = EnsureUniqueStableId(BuildStableId(relativePath), usedNodeIds);
                CollectedNode collectedNode = new CollectedNode
                {
                    Transform = current,
                    NodeId = nodeId,
                    ParentNodeId = current == rootObject.transform || current.parent == null
                        ? ""
                        : nodeIdLookup[current.parent]
                };

                MeshFilter meshFilter = current.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = current.GetComponent<MeshRenderer>();
                if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null)
                {
                    snapshot.RendererCount++;
                    CollectMeshParts(snapshot, collectedNode, meshFilter.sharedMesh, meshRenderer.sharedMaterials, materialLookup, meshIdentityLookup);
                }

                SkinnedMeshRenderer skinnedMeshRenderer = current.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    snapshot.HasSkinnedMesh = true;
                    snapshot.Warnings.Add("unsupported_skinned_mesh:" + relativePath);
                }

                if (current.GetComponent<Animation>() != null || current.GetComponent<Animator>() != null)
                {
                    snapshot.HasAnimation = true;
                    snapshot.Warnings.Add("unsupported_animation:" + relativePath);
                }

                snapshot.Nodes.Add(collectedNode);
                nodeIdLookup[current] = collectedNode.NodeId;
            }

            snapshot.Materials.AddRange(materialLookup.Values);
            if (snapshot.Materials.Count > 0)
            {
                snapshot.FeatureFlags.Add("material_base_color");
                snapshot.FeatureFlags.Add("submesh_material_slots");
            }

            if (snapshot.Materials.Any(material => material.HasTexture))
                snapshot.FeatureFlags.Add("texture_refs_present");
            if (snapshot.Materials.Any(material => material.HasVertexColors))
                snapshot.FeatureFlags.Add("vertex_colors_present");
            if (snapshot.MeshCount > 0)
                snapshot.FeatureFlags.Add("static_geometry");
            if (snapshot.HasSkinnedMesh)
                snapshot.FeatureFlags.Add("unsupported_skinned_mesh");
            if (snapshot.HasAnimation)
                snapshot.FeatureFlags.Add("unsupported_animation");

            snapshot.ScreenContract = BuildScreenContract(rootObject, nodeIdLookup, snapshot);
            if (snapshot.ScreenContract != null && snapshot.ScreenContract.Slots != null && snapshot.ScreenContract.Slots.Count > 0)
            {
                snapshot.FeatureFlags.Add("screen_contract_present");
                if (snapshot.ScreenContract.Slots.Any(slot => !string.IsNullOrEmpty(slot.ScreenGlassNodeId)))
                    snapshot.FeatureFlags.Add("screen_glass_present");
                if (snapshot.ScreenContract.Slots.All(slot => !string.IsNullOrEmpty(slot.DisconnectSurfaceNodeId)))
                    snapshot.FeatureFlags.Add("screen_placeholders_present");
            }
            if (snapshot.HasInvalidScreenContract)
                snapshot.FeatureFlags.Add("screen_contract_invalid");

            snapshot.ControlSurfaceContract = BuildControlSurfaceContract(rootObject, nodeIdLookup, snapshot);
            if (snapshot.ControlSurfaceContract != null && snapshot.ControlSurfaceContract.Elements != null && snapshot.ControlSurfaceContract.Elements.Count > 0)
            {
                snapshot.FeatureFlags.Add("control_surface_present");
                if (snapshot.ControlSurfaceContract.Elements.Any(element => string.Equals(element.ElementKind, "slider", StringComparison.OrdinalIgnoreCase)))
                    snapshot.FeatureFlags.Add("control_slider_present");
                if (snapshot.ControlSurfaceContract.Elements.Any(element => element.ReadOnly))
                    snapshot.FeatureFlags.Add("control_readonly_present");
                if (IsControlSurfaceCanvas(snapshot))
                {
                    snapshot.FeatureFlags.Add("control_surface_canvas");
                    if (!TryAttachControlSurfaceCanvasSnapshot(rootObject, snapshot))
                    {
                        snapshot.HasInvalidControlSurface = true;
                        snapshot.Warnings.Add("control_surface_canvas_snapshot_missing");
                    }
                }
            }
            if (snapshot.HasInvalidControlSurface)
                snapshot.FeatureFlags.Add("control_surface_invalid");

            if (snapshot.Materials.Count > 0)
            {
                if (!snapshot.FeatureFlags.Contains("material_base_color"))
                    snapshot.FeatureFlags.Add("material_base_color");
                if (!snapshot.FeatureFlags.Contains("submesh_material_slots"))
                    snapshot.FeatureFlags.Add("submesh_material_slots");
            }
            if (snapshot.MeshCount > 0 && !snapshot.FeatureFlags.Contains("static_geometry"))
                snapshot.FeatureFlags.Add("static_geometry");
            if (snapshot.Materials.Any(material => material.HasTexture)
                && !snapshot.FeatureFlags.Contains("texture_refs_present"))
            {
                snapshot.FeatureFlags.Add("texture_refs_present");
            }

            Bounds bounds;
            if (TryComputeBoundsFromRoot(rootObject, out bounds))
            {
                snapshot.Bounds = bounds;
                snapshot.HasBounds = true;
            }
            else if (TryComputeControlSurfaceCanvasBounds(snapshot, out bounds))
            {
                snapshot.Bounds = bounds;
                snapshot.HasBounds = true;
            }

            return snapshot;
        }

        private static void CollectMeshParts(
            Snapshot snapshot,
            CollectedNode collectedNode,
            Mesh mesh,
            Material[] sharedMaterials,
            Dictionary<Material, UnityInnerPieceMaterialSummary> materialLookup,
            Dictionary<string, string> meshIdentityLookup)
        {
            if (mesh == null)
                return;

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int submeshIndex = 0; submeshIndex < subMeshCount; submeshIndex++)
            {
                int safeIndex = Mathf.Min(submeshIndex, Mathf.Max(0, mesh.subMeshCount - 1));
                int[] triangles = mesh.GetTriangles(safeIndex);
                if (triangles == null || triangles.Length <= 0)
                    continue;

                Material material = sharedMaterials != null && submeshIndex < sharedMaterials.Length
                    ? sharedMaterials[submeshIndex]
                    : null;
                UnityInnerPieceMaterialSummary materialSummary = GetOrCreateMaterialSummary(materialLookup, material, mesh);
                string materialRefId = materialSummary != null ? materialSummary.MaterialRefId : "";

                string meshHash = BuildMeshPartIdentity(mesh, triangles, materialRefId, safeIndex);
                string meshId;
                if (!meshIdentityLookup.TryGetValue(meshHash, out meshId))
                {
                    meshId = "mesh_" + meshIdentityLookup.Count.ToString("D4", CultureInfo.InvariantCulture) + "_" + meshHash.Substring(0, 8);
                    meshIdentityLookup[meshHash] = meshId;
                    snapshot.MeshCount++;
                    snapshot.TotalVertexCount += mesh.vertexCount;
                    snapshot.TotalTriangleCount += triangles.Length / 3;
                }

                collectedNode.MeshParts.Add(new CollectedMeshPart
                {
                    Mesh = mesh,
                    SubmeshIndex = safeIndex,
                    MaterialRefId = materialRefId,
                    MeshId = meshId,
                    VertexCount = mesh.vertexCount,
                    TriangleCount = triangles.Length / 3
                });
            }
        }

        private static UnityInnerPieceMaterialSummary GetOrCreateMaterialSummary(
            Dictionary<Material, UnityInnerPieceMaterialSummary> materialLookup,
            Material material,
            Mesh mesh)
        {
            if (material == null)
                return null;

            UnityInnerPieceMaterialSummary existing;
            if (materialLookup.TryGetValue(material, out existing))
            {
                if (mesh != null && mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount)
                {
                    existing.HasVertexColors = true;
                    if (!existing.FeatureFlags.Contains("vertex_colors_present"))
                        existing.FeatureFlags.Add("vertex_colors_present");
                }

                return existing;
            }

            string texturePath;
            Texture mainTexture = TryGetMainTexture(material, out texturePath);
            Color baseColor = TryGetMaterialColor(material, out Color colorValue) ? colorValue : Color.white;
            UnityInnerPieceMaterialSummary summary = new UnityInnerPieceMaterialSummary
            {
                MaterialRefId = BuildStableId((material.name ?? "material") + "_" + material.GetInstanceID().ToString(CultureInfo.InvariantCulture)),
                DisplayName = material.name ?? "Material",
                ShaderName = material.shader != null ? material.shader.name ?? "" : "",
                BaseColorHex = "#" + ColorUtility.ToHtmlStringRGBA(baseColor),
                HasTexture = mainTexture != null,
                TextureAssetPath = texturePath ?? "",
                HasVertexColors = mesh != null && mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount
            };

            summary.FeatureFlags.Add("base_color");
            if (summary.HasTexture)
            {
                summary.FeatureFlags.Add("texture_ref_present");
                summary.Warnings.Add("texture_copy_deferred");
            }

            if (summary.HasVertexColors)
                summary.FeatureFlags.Add("vertex_colors_present");

            materialLookup[material] = summary;
            return summary;
        }

        private static FAIPEGeometryPackage BuildGeometryPackage(
            UnityInnerPieceExportSource source,
            Snapshot snapshot,
            string displayName,
            string[] tags)
        {
            FAIPEGeometryPackage geometry = new FAIPEGeometryPackage
            {
                DisplayName = displayName,
                SourceKind = source != null ? source.SourceKind : "",
                SourcePath = source != null ? source.SourcePath : "",
                Tags = tags != null ? tags.ToList() : new List<string>()
            };

            Dictionary<string, FAIPEExportMesh> exportedMeshes =
                new Dictionary<string, FAIPEExportMesh>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < snapshot.Nodes.Count; i++)
            {
                CollectedNode sourceNode = snapshot.Nodes[i];
                FAIPEExportNode node = new FAIPEExportNode
                {
                    NodeId = sourceNode.NodeId,
                    ParentNodeId = sourceNode.ParentNodeId,
                    DisplayName = sourceNode.Transform != null ? sourceNode.Transform.name : sourceNode.NodeId,
                    LocalPosition = UnityVector3.FromVector3(sourceNode.Transform != null ? sourceNode.Transform.localPosition : Vector3.zero),
                    LocalRotation = UnityQuaternion.FromQuaternion(sourceNode.Transform != null ? sourceNode.Transform.localRotation : Quaternion.identity),
                    LocalScale = UnityVector3.FromVector3(sourceNode.Transform != null ? sourceNode.Transform.localScale : Vector3.one)
                };

                for (int meshIndex = 0; meshIndex < sourceNode.MeshParts.Count; meshIndex++)
                {
                    CollectedMeshPart meshPart = sourceNode.MeshParts[meshIndex];
                    if (string.IsNullOrEmpty(meshPart.MeshId))
                        continue;

                    node.MeshRefIds.Add(meshPart.MeshId);
                    if (exportedMeshes.ContainsKey(meshPart.MeshId))
                        continue;

                    exportedMeshes[meshPart.MeshId] = BuildExportMesh(meshPart);
                }

                geometry.Nodes.Add(node);
            }

            geometry.Meshes = exportedMeshes.Values
                .OrderBy(mesh => mesh.MeshId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            AppendControlSurfaceCanvasSnapshotGeometry(geometry, snapshot);
            return geometry;
        }

        private static FAIPEExportMesh BuildExportMesh(CollectedMeshPart meshPart)
        {
            Mesh mesh = meshPart.Mesh;
            FAIPEExportMesh exportMesh = new FAIPEExportMesh
            {
                MeshId = meshPart.MeshId,
                MaterialRefId = meshPart.MaterialRefId,
                SubmeshIndex = meshPart.SubmeshIndex,
                LocalBounds = UnityBounds3.FromBounds(mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.zero))
            };

            Vector3[] vertices = mesh != null && mesh.vertices != null ? mesh.vertices : new Vector3[0];
            int[] triangles = mesh != null ? mesh.GetTriangles(Mathf.Min(meshPart.SubmeshIndex, Mathf.Max(0, mesh.subMeshCount - 1))) : new int[0];
            Vector3[] normals = mesh != null && mesh.normals != null ? mesh.normals : new Vector3[0];
            Vector2[] uv0 = mesh != null && mesh.uv != null ? mesh.uv : new Vector2[0];

            for (int i = 0; i < vertices.Length; i++)
                exportMesh.Vertices.Add(UnityVector3.FromVector3(vertices[i]));
            for (int i = 0; i < triangles.Length; i++)
                exportMesh.TriangleIndices.Add(triangles[i]);
            if (normals.Length == vertices.Length)
            {
                for (int i = 0; i < normals.Length; i++)
                    exportMesh.Normals.Add(UnityVector3.FromVector3(normals[i]));
            }
            if (uv0.Length == vertices.Length)
            {
                for (int i = 0; i < uv0.Length; i++)
                    exportMesh.Uv0.Add(UnityVector2.FromVector2(uv0[i]));
            }

            return exportMesh;
        }

        private static FAIPEMaterialsPackage BuildMaterialsPackage(Snapshot snapshot)
        {
            FAIPEMaterialsPackage package = new FAIPEMaterialsPackage();
            for (int i = 0; i < snapshot.Materials.Count; i++)
            {
                UnityInnerPieceMaterialSummary source = snapshot.Materials[i];
                package.Materials.Add(new FAIPEMaterialEntry
                {
                    MaterialRefId = source.MaterialRefId,
                    DisplayName = source.DisplayName,
                    ShaderName = source.ShaderName,
                    BaseColorHex = source.BaseColorHex,
                    TextureAssetPath = source.TextureAssetPath,
                    TexturePngBase64 = source.TexturePngBase64,
                    FeatureFlags = new List<string>(source.FeatureFlags),
                    Warnings = new List<string>(source.Warnings)
                });
            }

            return package;
        }

        private static FAIPEScreenContractPackage BuildScreenContract(
            GameObject rootObject,
            Dictionary<Transform, string> nodeIdLookup,
            Snapshot snapshot)
        {
            if (rootObject == null)
                return null;

            FAPlayerShellAuthoring shellAuthoring = rootObject.GetComponent<FAPlayerShellAuthoring>();
            if (shellAuthoring == null)
                return null;

            FAIPEScreenContractPackage contract = new FAIPEScreenContractPackage
            {
                SchemaVersion = string.IsNullOrWhiteSpace(shellAuthoring.screenContractVersion)
                    ? "frameangel_screen_contract_v1"
                    : shellAuthoring.screenContractVersion.Trim(),
                ShellId = string.IsNullOrWhiteSpace(shellAuthoring.shellId)
                    ? "player"
                    : shellAuthoring.shellId.Trim(),
                DefaultDisconnectStateId = string.IsNullOrWhiteSpace(shellAuthoring.defaultDisconnectStateId)
                    ? ""
                    : shellAuthoring.defaultDisconnectStateId.Trim(),
                SurfaceTargetId = string.IsNullOrWhiteSpace(shellAuthoring.surfaceTargetId)
                    ? "player:screen"
                    : shellAuthoring.surfaceTargetId.Trim()
            };

            FAPlayerScreenSlotAuthoring[] slotAuthorings = rootObject.GetComponentsInChildren<FAPlayerScreenSlotAuthoring>(true);
            if (slotAuthorings == null || slotAuthorings.Length <= 0)
            {
                snapshot.HasInvalidScreenContract = true;
                snapshot.Warnings.Add("screen_slot_missing");
                return contract;
            }

            for (int i = 0; i < slotAuthorings.Length; i++)
            {
                FAPlayerScreenSlotAuthoring slotAuthoring = slotAuthorings[i];
                if (slotAuthoring == null)
                    continue;

                string slotId = string.IsNullOrWhiteSpace(slotAuthoring.slotId)
                    ? "main"
                    : slotAuthoring.slotId.Trim();
                string disconnectStateId = string.IsNullOrWhiteSpace(slotAuthoring.disconnectStateId)
                    ? contract.DefaultDisconnectStateId
                    : slotAuthoring.disconnectStateId.Trim();
                string surfaceTargetId = string.IsNullOrWhiteSpace(slotAuthoring.surfaceTargetId)
                    ? contract.SurfaceTargetId
                    : slotAuthoring.surfaceTargetId.Trim();

                FAIPEScreenSlotEntry slot = new FAIPEScreenSlotEntry
                {
                    SlotId = slotId,
                    SurfaceTargetId = surfaceTargetId,
                    DisconnectStateId = disconnectStateId,
                    ScreenSurfaceNodeId = ResolveScreenNodeId(slotAuthoring.screenSurface, nodeIdLookup),
                    ScreenGlassNodeId = ResolveScreenNodeId(slotAuthoring.screenGlass, nodeIdLookup),
                    ScreenApertureNodeId = ResolveScreenNodeId(slotAuthoring.screenAperture, nodeIdLookup),
                    DisconnectSurfaceNodeId = ResolveScreenNodeId(slotAuthoring.disconnectSurface, nodeIdLookup)
                };

                if (string.IsNullOrEmpty(slot.ScreenSurfaceNodeId))
                {
                    snapshot.HasInvalidScreenContract = true;
                    snapshot.Warnings.Add("screen_surface_missing:" + slotId);
                }
                else if (!HasRenderableSurface(slotAuthoring.screenSurface))
                {
                    snapshot.HasInvalidScreenContract = true;
                    snapshot.Warnings.Add("screen_surface_not_renderable:" + slotId);
                }

                if (string.IsNullOrEmpty(slot.DisconnectSurfaceNodeId))
                {
                    snapshot.HasInvalidScreenContract = true;
                    snapshot.Warnings.Add("disconnect_surface_missing:" + slotId);
                }
                else if (!HasRenderableSurface(slotAuthoring.disconnectSurface))
                {
                    snapshot.HasInvalidScreenContract = true;
                    snapshot.Warnings.Add("disconnect_surface_not_renderable:" + slotId);
                }

                if (string.IsNullOrEmpty(slot.DisconnectStateId))
                {
                    snapshot.HasInvalidScreenContract = true;
                    snapshot.Warnings.Add("disconnect_state_missing:" + slotId);
                }

                contract.Slots.Add(slot);
            }

            return contract;
        }

        private static FAIPEControlSurfacePackage BuildControlSurfaceContract(
            GameObject rootObject,
            Dictionary<Transform, string> nodeIdLookup,
            Snapshot snapshot)
        {
            if (rootObject == null)
                return null;

            SerializedObject metadataObject;
            if (!TryFindControlSurfaceMetadata(rootObject, out metadataObject))
                return null;

            string controlSurfaceId = ReadSerializedString(metadataObject, "controlSurfaceId", "");
            string controlSurfaceLabel = ReadSerializedString(metadataObject, "controlSurfaceLabel", "");
            string controlFamilyId = ReadSerializedString(metadataObject, "controlFamilyId", "");
            string controlThemeId = ReadSerializedString(metadataObject, "controlThemeId", "");
            string controlThemeLabel = ReadSerializedString(metadataObject, "controlThemeLabel", "");
            string controlThemeVariantId = ReadSerializedString(metadataObject, "controlThemeVariantId", "");
            string controlThemeAssetPath = ReadSerializedString(metadataObject, "controlThemeAssetPath", "");
            string controlThemeAssetGuid = ReadSerializedString(metadataObject, "controlThemeAssetGuid", "");
            string toolkitCategory = ReadSerializedString(metadataObject, "toolkitCategory", "");
            string sourcePrefabAssetPath = ReadSerializedString(metadataObject, "sourcePrefabAssetPath", "");
            string layoutSource = ReadSerializedString(metadataObject, "layoutSource", "canvas_export_v1");
            List<string> targetDisplayIds = ReadSerializedStringArray(metadataObject.FindProperty("targetDisplayIds"));
            string defaultTargetDisplayId = ReadSerializedString(metadataObject, "defaultTargetDisplayId", "");
            RectTransform surfaceRoot = ResolveSerializedTransform(metadataObject, "surfaceRoot") as RectTransform;
            float surfaceUnitsToMetersMultiplier = ResolveSurfaceUnitsToMetersMultiplier(metadataObject, surfaceRoot);
            string explicitSurfaceNodeId = ReadSerializedString(metadataObject, "surfaceNodeId", "");
            string explicitColliderNodeId = ReadSerializedString(metadataObject, "colliderNodeId", "");

            if (string.IsNullOrWhiteSpace(controlSurfaceId))
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_surface_id_missing");
                controlSurfaceId = "control_surface";
            }

            if (targetDisplayIds.Count <= 0)
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_target_display_missing");
                targetDisplayIds.Add("player_main");
            }

            if (string.IsNullOrWhiteSpace(defaultTargetDisplayId))
            {
                defaultTargetDisplayId = targetDisplayIds[0];
            }

            string surfaceNodeId = ResolveScreenNodeId(surfaceRoot, nodeIdLookup);
            if (string.IsNullOrWhiteSpace(surfaceNodeId))
            {
                surfaceNodeId = explicitSurfaceNodeId;
            }

            if (surfaceRoot == null)
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_surface_root_missing");
            }
            else if (string.IsNullOrWhiteSpace(surfaceNodeId))
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_surface_node_missing");
            }

            FAIPEControlSurfacePackage contract = new FAIPEControlSurfacePackage
            {
                ControlSurfaceId = controlSurfaceId.Trim(),
                ControlSurfaceLabel = string.IsNullOrWhiteSpace(controlSurfaceLabel) ? controlSurfaceId.Trim() : controlSurfaceLabel.Trim(),
                ControlFamilyId = string.IsNullOrWhiteSpace(controlFamilyId) ? controlSurfaceId.Trim() : controlFamilyId.Trim(),
                ControlThemeId = string.IsNullOrWhiteSpace(controlThemeId) ? "" : controlThemeId.Trim(),
                ControlThemeLabel = string.IsNullOrWhiteSpace(controlThemeLabel) ? "" : controlThemeLabel.Trim(),
                ControlThemeVariantId = string.IsNullOrWhiteSpace(controlThemeVariantId) ? "" : controlThemeVariantId.Trim(),
                ControlThemeAssetPath = string.IsNullOrWhiteSpace(controlThemeAssetPath) ? "" : controlThemeAssetPath.Trim(),
                ControlThemeAssetGuid = string.IsNullOrWhiteSpace(controlThemeAssetGuid) ? "" : controlThemeAssetGuid.Trim(),
                ToolkitCategory = string.IsNullOrWhiteSpace(toolkitCategory) ? "" : toolkitCategory.Trim(),
                SourcePrefabAssetPath = string.IsNullOrWhiteSpace(sourcePrefabAssetPath) ? "" : sourcePrefabAssetPath.Trim(),
                LayoutSource = string.IsNullOrWhiteSpace(layoutSource) ? "canvas_export_v1" : layoutSource.Trim(),
                TargetDisplayIds = targetDisplayIds,
                DefaultTargetDisplayId = defaultTargetDisplayId.Trim(),
                SurfaceNodeId = surfaceNodeId,
                ColliderNodeId = string.IsNullOrWhiteSpace(explicitColliderNodeId) ? surfaceNodeId : explicitColliderNodeId.Trim(),
                SurfaceWidthMeters = surfaceRoot != null
                    ? Mathf.Abs(surfaceRoot.rect.width * surfaceRoot.lossyScale.x * surfaceUnitsToMetersMultiplier)
                    : 0f,
                SurfaceHeightMeters = surfaceRoot != null
                    ? Mathf.Abs(surfaceRoot.rect.height * surfaceRoot.lossyScale.y * surfaceUnitsToMetersMultiplier)
                    : 0f
            };

            SerializedProperty elementsProperty = metadataObject.FindProperty("elements");
            if (elementsProperty == null || !elementsProperty.isArray || elementsProperty.arraySize <= 0)
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_elements_missing");
                return contract;
            }

            for (int i = 0; i < elementsProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = elementsProperty.GetArrayElementAtIndex(i);
                if (elementProperty == null)
                    continue;

                string elementId = ReadRelativeString(elementProperty, "elementId", "");
                string elementLabel = ReadRelativeString(elementProperty, "elementLabel", "");
                string actionId = ReadRelativeString(elementProperty, "actionId", "");
                string elementKind = ReadRelativeString(elementProperty, "elementKind", "button");
                string valueKind = ReadRelativeString(elementProperty, "valueKind", "none");
                bool readOnly = ReadRelativeBool(elementProperty, "readOnly", false);
                RectTransform elementRect = ResolveRelativeTransform(elementProperty, "rectTransform") as RectTransform;
                string nodeId = ResolveScreenNodeId(elementRect, nodeIdLookup);

                if (string.IsNullOrWhiteSpace(elementId))
                {
                    snapshot.HasInvalidControlSurface = true;
                    snapshot.Warnings.Add("control_element_id_missing:" + i.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(actionId))
                {
                    snapshot.HasInvalidControlSurface = true;
                    snapshot.Warnings.Add("control_element_action_missing:" + elementId);
                }

                if (elementRect == null)
                {
                    snapshot.HasInvalidControlSurface = true;
                    snapshot.Warnings.Add("control_element_rect_missing:" + elementId);
                }
                else if (string.IsNullOrWhiteSpace(nodeId))
                {
                    snapshot.HasInvalidControlSurface = true;
                    snapshot.Warnings.Add("control_element_node_missing:" + elementId);
                }

                contract.Elements.Add(new FAIPEControlSurfaceElementEntry
                {
                    ElementId = elementId.Trim(),
                    ElementLabel = string.IsNullOrWhiteSpace(elementLabel) ? elementId.Trim() : elementLabel.Trim(),
                    ActionId = actionId.Trim(),
                    NodeId = nodeId,
                    ColliderNodeId = nodeId,
                    ElementKind = string.IsNullOrWhiteSpace(elementKind) ? "button" : elementKind.Trim(),
                    ValueKind = string.IsNullOrWhiteSpace(valueKind) ? "none" : valueKind.Trim(),
                    NormalizedRect = ComputeNormalizedRect(surfaceRoot, elementRect),
                    ReadOnly = readOnly
                });
            }

            if (contract.Elements.Count <= 0)
            {
                snapshot.HasInvalidControlSurface = true;
                snapshot.Warnings.Add("control_elements_empty");
            }

            return contract;
        }

        private static string ResolveScreenNodeId(
            Transform authoredTransform,
            Dictionary<Transform, string> nodeIdLookup)
        {
            if (authoredTransform == null || nodeIdLookup == null)
                return "";

            string nodeId;
            return nodeIdLookup.TryGetValue(authoredTransform, out nodeId) ? nodeId : "";
        }

        private static bool TryFindControlSurfaceMetadata(GameObject rootObject, out SerializedObject metadataObject)
        {
            metadataObject = null;
            if (rootObject == null)
                return false;

            MonoBehaviour[] behaviours = rootObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                SerializedObject candidate = new SerializedObject(behaviour);
                if (candidate.FindProperty("controlSurfaceId") == null)
                    continue;
                if (candidate.FindProperty("layoutSource") == null)
                    continue;
                if (candidate.FindProperty("targetDisplayIds") == null)
                    continue;
                if (candidate.FindProperty("elements") == null)
                    continue;

                metadataObject = candidate;
                return true;
            }

            return false;
        }

        private static string ReadSerializedString(SerializedObject serializedObject, string propertyName, string fallback)
        {
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
                return fallback;

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return fallback;

            return string.IsNullOrWhiteSpace(property.stringValue) ? fallback : property.stringValue;
        }

        private static float ReadSerializedFloat(SerializedObject serializedObject, string propertyName, float fallback)
        {
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
                return fallback;

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return fallback;

            if (property.propertyType == SerializedPropertyType.Float)
                return property.floatValue;

            if (property.propertyType == SerializedPropertyType.Integer)
                return property.intValue;

            return fallback;
        }

        private static float ResolveSurfaceUnitsToMetersMultiplier(
            SerializedObject metadataObject,
            RectTransform surfaceRoot)
        {
            float configured = ReadSerializedFloat(metadataObject, "surfaceUnitsToMetersMultiplier", 0f);
            if (configured > 0f)
                return configured;

            Canvas canvas = surfaceRoot != null ? surfaceRoot.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                return 1f;

            return 0.001f;
        }

        private static List<string> ReadSerializedStringArray(SerializedProperty property)
        {
            List<string> values = new List<string>();
            if (property == null || !property.isArray)
                return values;

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element == null || element.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(element.stringValue))
                    continue;

                values.Add(element.stringValue.Trim());
            }

            return values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ReadRelativeString(SerializedProperty parentProperty, string relativeName, string fallback)
        {
            if (parentProperty == null || string.IsNullOrWhiteSpace(relativeName))
                return fallback;

            SerializedProperty property = parentProperty.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return fallback;

            return string.IsNullOrWhiteSpace(property.stringValue) ? fallback : property.stringValue;
        }

        private static bool ReadRelativeBool(SerializedProperty parentProperty, string relativeName, bool fallback)
        {
            if (parentProperty == null || string.IsNullOrWhiteSpace(relativeName))
                return fallback;

            SerializedProperty property = parentProperty.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
                return fallback;

            return property.boolValue;
        }

        private static Transform ResolveSerializedTransform(SerializedObject serializedObject, string propertyName)
        {
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            return property.objectReferenceValue as Transform;
        }

        private static Transform ResolveRelativeTransform(SerializedProperty parentProperty, string relativeName)
        {
            if (parentProperty == null || string.IsNullOrWhiteSpace(relativeName))
                return null;

            SerializedProperty property = parentProperty.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            return property.objectReferenceValue as Transform;
        }

        private static UnityNormalizedRect ComputeNormalizedRect(RectTransform surfaceRoot, RectTransform elementRect)
        {
            UnityNormalizedRect result = new UnityNormalizedRect();
            if (surfaceRoot == null || elementRect == null)
                return result;

            Rect rootRect = surfaceRoot.rect;
            if (Mathf.Abs(rootRect.width) <= 0.0001f || Mathf.Abs(rootRect.height) <= 0.0001f)
                return result;

            Bounds relativeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(surfaceRoot, elementRect);
            float x = (relativeBounds.min.x - rootRect.xMin) / rootRect.width;
            float y = (relativeBounds.min.y - rootRect.yMin) / rootRect.height;
            float width = relativeBounds.size.x / rootRect.width;
            float height = relativeBounds.size.y / rootRect.height;

            result.X = Mathf.Clamp01(x);
            result.Y = Mathf.Clamp01(y);
            result.Width = Mathf.Clamp01(width);
            result.Height = Mathf.Clamp01(height);
            return result;
        }

        private static bool HasRenderableSurface(Transform authoredTransform)
        {
            if (authoredTransform == null)
                return false;

            Renderer[] renderers = authoredTransform.GetComponentsInChildren<Renderer>(true);
            return renderers != null && renderers.Any(renderer => renderer != null);
        }

        private static string BuildGeometryFingerprint(
            FAIPEGeometryPackage geometry,
            FAIPEScreenContractPackage screenContract,
            FAIPEControlSurfacePackage controlSurfaceContract)
        {
            StringBuilder sb = new StringBuilder(4096);
            sb.Append(geometry.Units ?? "").Append('|');
            AppendBoundsFingerprint(sb, ComputeGeometryBounds(geometry));

            for (int i = 0; i < geometry.Nodes.Count; i++)
            {
                FAIPEExportNode node = geometry.Nodes[i];
                sb.Append("|node|").Append(node.NodeId ?? "").Append('|').Append(node.ParentNodeId ?? "").Append('|');
                AppendVector3Fingerprint(sb, node.LocalPosition);
                AppendQuaternionFingerprint(sb, node.LocalRotation);
                AppendVector3Fingerprint(sb, node.LocalScale);
                for (int j = 0; j < node.MeshRefIds.Count; j++)
                    sb.Append(node.MeshRefIds[j] ?? "").Append(',');
            }

            for (int i = 0; i < geometry.Meshes.Count; i++)
            {
                FAIPEExportMesh mesh = geometry.Meshes[i];
                sb.Append("|mesh|").Append(mesh.MeshId ?? "").Append('|');
                sb.Append(mesh.MaterialRefId ?? "").Append('|');
                sb.Append(mesh.SubmeshIndex.ToString(CultureInfo.InvariantCulture)).Append('|');
                AppendBoundsFingerprint(sb, mesh.LocalBounds);
                for (int j = 0; j < mesh.Vertices.Count; j++)
                    AppendVector3Fingerprint(sb, mesh.Vertices[j]);
                for (int j = 0; j < mesh.TriangleIndices.Count; j++)
                    sb.Append(mesh.TriangleIndices[j].ToString(CultureInfo.InvariantCulture)).Append(',');
                for (int j = 0; j < mesh.Normals.Count; j++)
                    AppendVector3Fingerprint(sb, mesh.Normals[j]);
                for (int j = 0; j < mesh.Uv0.Count; j++)
                {
                    sb.Append(mesh.Uv0[j].X.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(mesh.Uv0[j].Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                }
            }

            if (screenContract != null)
            {
                sb.Append("|screen_contract|").Append(screenContract.SchemaVersion ?? "").Append('|');
                sb.Append(screenContract.ShellId ?? "").Append('|');
                sb.Append(screenContract.DefaultDisconnectStateId ?? "").Append('|');
                sb.Append(screenContract.SurfaceTargetId ?? "").Append('|');
                List<FAIPEScreenSlotEntry> orderedSlots = screenContract.Slots != null
                    ? screenContract.Slots.OrderBy(slot => slot.SlotId, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<FAIPEScreenSlotEntry>();
                for (int i = 0; i < orderedSlots.Count; i++)
                {
                    FAIPEScreenSlotEntry slot = orderedSlots[i] ?? new FAIPEScreenSlotEntry();
                    sb.Append("|slot|").Append(slot.SlotId ?? "").Append('|');
                    sb.Append(slot.SurfaceTargetId ?? "").Append('|');
                    sb.Append(slot.DisconnectStateId ?? "").Append('|');
                    sb.Append(slot.ScreenSurfaceNodeId ?? "").Append('|');
                    sb.Append(slot.ScreenGlassNodeId ?? "").Append('|');
                    sb.Append(slot.ScreenApertureNodeId ?? "").Append('|');
                    sb.Append(slot.DisconnectSurfaceNodeId ?? "").Append('|');
                }
            }

            if (controlSurfaceContract != null)
            {
                sb.Append("|control_surface|").Append(controlSurfaceContract.SchemaVersion ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlSurfaceId ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlFamilyId ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlThemeId ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlThemeLabel ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlThemeVariantId ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlThemeAssetPath ?? "").Append('|');
                sb.Append(controlSurfaceContract.ControlThemeAssetGuid ?? "").Append('|');
                sb.Append(controlSurfaceContract.ToolkitCategory ?? "").Append('|');
                sb.Append(controlSurfaceContract.SourcePrefabAssetPath ?? "").Append('|');
                sb.Append(controlSurfaceContract.LayoutSource ?? "").Append('|');
                sb.Append(controlSurfaceContract.DefaultTargetDisplayId ?? "").Append('|');
                sb.Append(controlSurfaceContract.SurfaceNodeId ?? "").Append('|');
                sb.Append(controlSurfaceContract.ColliderNodeId ?? "").Append('|');
                sb.Append(controlSurfaceContract.SurfaceWidthMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                sb.Append(controlSurfaceContract.SurfaceHeightMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|');

                List<string> orderedTargets = controlSurfaceContract.TargetDisplayIds != null
                    ? controlSurfaceContract.TargetDisplayIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();
                for (int i = 0; i < orderedTargets.Count; i++)
                {
                    sb.Append("|target|").Append(orderedTargets[i] ?? "").Append('|');
                }

                List<FAIPEControlSurfaceElementEntry> orderedElements = controlSurfaceContract.Elements != null
                    ? controlSurfaceContract.Elements.OrderBy(element => element.ElementId, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<FAIPEControlSurfaceElementEntry>();
                for (int i = 0; i < orderedElements.Count; i++)
                {
                    FAIPEControlSurfaceElementEntry element = orderedElements[i] ?? new FAIPEControlSurfaceElementEntry();
                    sb.Append("|element|").Append(element.ElementId ?? "").Append('|');
                    sb.Append(element.ActionId ?? "").Append('|');
                    sb.Append(element.NodeId ?? "").Append('|');
                    sb.Append(element.ColliderNodeId ?? "").Append('|');
                    sb.Append(element.ElementKind ?? "").Append('|');
                    sb.Append(element.ValueKind ?? "").Append('|');
                    sb.Append(element.ReadOnly ? "1" : "0").Append('|');
                    sb.Append(element.NormalizedRect != null ? element.NormalizedRect.X.ToString("R", CultureInfo.InvariantCulture) : "0").Append(',');
                    sb.Append(element.NormalizedRect != null ? element.NormalizedRect.Y.ToString("R", CultureInfo.InvariantCulture) : "0").Append(',');
                    sb.Append(element.NormalizedRect != null ? element.NormalizedRect.Width.ToString("R", CultureInfo.InvariantCulture) : "0").Append(',');
                    sb.Append(element.NormalizedRect != null ? element.NormalizedRect.Height.ToString("R", CultureInfo.InvariantCulture) : "0").Append('|');
                }
            }

            ulong hash = 1469598103934665603UL;
            string text = sb.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static UnityBounds3 ComputeGeometryBounds(FAIPEGeometryPackage geometry)
        {
            Dictionary<string, Matrix4x4> worldMatrices = new Dictionary<string, Matrix4x4>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FAIPEExportNode> nodeLookup = geometry.Nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FAIPEExportMesh> meshLookup = geometry.Meshes.ToDictionary(mesh => mesh.MeshId, StringComparer.OrdinalIgnoreCase);
            bool hasBounds = false;
            Bounds bounds = new Bounds();

            for (int i = 0; i < geometry.Nodes.Count; i++)
            {
                FAIPEExportNode node = geometry.Nodes[i];
                Matrix4x4 matrix = ResolveWorldMatrix(node, nodeLookup, worldMatrices);
                for (int meshIndex = 0; meshIndex < node.MeshRefIds.Count; meshIndex++)
                {
                    FAIPEExportMesh mesh;
                    if (!meshLookup.TryGetValue(node.MeshRefIds[meshIndex], out mesh))
                        continue;

                    for (int vertexIndex = 0; vertexIndex < mesh.Vertices.Count; vertexIndex++)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(mesh.Vertices[vertexIndex].ToVector3());
                        if (!hasBounds)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                }
            }

            return hasBounds ? UnityBounds3.FromBounds(bounds) : new UnityBounds3();
        }

        private static Matrix4x4 ResolveWorldMatrix(
            FAIPEExportNode node,
            Dictionary<string, FAIPEExportNode> nodeLookup,
            Dictionary<string, Matrix4x4> worldMatrices)
        {
            Matrix4x4 cached;
            if (worldMatrices.TryGetValue(node.NodeId, out cached))
                return cached;

            Matrix4x4 local = Matrix4x4.TRS(
                node.LocalPosition.ToVector3(),
                new Quaternion(node.LocalRotation.X, node.LocalRotation.Y, node.LocalRotation.Z, node.LocalRotation.W),
                node.LocalScale.ToVector3());

            if (string.IsNullOrEmpty(node.ParentNodeId) || !nodeLookup.ContainsKey(node.ParentNodeId))
            {
                worldMatrices[node.NodeId] = local;
                return local;
            }

            Matrix4x4 world = ResolveWorldMatrix(nodeLookup[node.ParentNodeId], nodeLookup, worldMatrices) * local;
            worldMatrices[node.NodeId] = world;
            return world;
        }

        private static bool TryResolveSelectionSource(out UnityInnerPieceExportSource source, out string errorMessage)
        {
            source = null;
            errorMessage = "";

            GameObject selection = Selection.activeGameObject;
            if (selection == null)
            {
                errorMessage = "No Unity selection is active.";
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null && selection.scene == prefabStage.scene)
            {
                source = new UnityInnerPieceExportSource
                {
                    RootObject = prefabStage.prefabContentsRoot,
                    SourceKind = "prefab_stage_root",
                    SourcePath = prefabStage.assetPath ?? "",
                    DefaultDisplayName = prefabStage.prefabContentsRoot.name
                };
                return true;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(selection))
            {
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(selection) ?? selection;
                GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(root);
                source = new UnityInnerPieceExportSource
                {
                    RootObject = root,
                    SourceKind = "prefab_instance_root",
                    SourcePath = prefabSource != null ? AssetDatabase.GetAssetPath(prefabSource) ?? "" : "",
                    DefaultDisplayName = root.name
                };
                return true;
            }

            source = new UnityInnerPieceExportSource
            {
                RootObject = selection,
                SourceKind = "scene_object",
                SourcePath = (selection.scene.path ?? "") + "#" + UnityBridgeInspector.BuildPath(selection.transform),
                DefaultDisplayName = selection.name
            };
            return true;
        }

        private static bool TryResolveProjectAssetSource(
            string assetPath,
            string assetGuid,
            out UnityInnerPieceExportSource source,
            out string errorMessage)
        {
            source = null;
            errorMessage = "";

            string resolvedAssetPath = assetPath ?? "";
            if (string.IsNullOrWhiteSpace(resolvedAssetPath) && !string.IsNullOrWhiteSpace(assetGuid))
            {
                resolvedAssetPath = AssetDatabase.GUIDToAssetPath(assetGuid) ?? "";
            }

            if (string.IsNullOrWhiteSpace(resolvedAssetPath))
            {
                errorMessage = "assetPath or assetGuid is required.";
                return false;
            }

            GameObject assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(resolvedAssetPath);
            if (assetRoot == null)
            {
                errorMessage = "Project asset was not found or is not a GameObject asset.";
                return false;
            }

            string extension = Path.GetExtension(resolvedAssetPath) ?? "";
            source = new UnityInnerPieceExportSource
            {
                RootObject = assetRoot,
                SourceKind = string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                    ? "project_prefab_asset"
                    : "project_model_asset",
                SourcePath = resolvedAssetPath,
                DefaultDisplayName = assetRoot.name
            };
            return true;
        }

        private static string ResolveAssetClass(Snapshot snapshot)
        {
            if (IsControlSurfaceCanvas(snapshot))
                return "control_surface_canvas";
            if (snapshot.HasSkinnedMesh)
                return "unsupported_skinned_asset";
            if (snapshot.HasAnimation)
                return "unsupported_animated_asset";
            if (snapshot.MeshCount <= 1 && snapshot.Nodes.Count <= 2)
                return "single_static_mesh";
            if (snapshot.MeshCount <= 8 && snapshot.Nodes.Count <= 16)
                return "static_multi_mesh_prop";
            if (snapshot.MeshCount <= 64 && snapshot.Nodes.Count <= 200)
                return "static_prefab_hierarchy";
            return "large_static_set_piece";
        }

        private static string ResolveExportStage(Snapshot snapshot)
        {
            if (IsControlSurfaceCanvas(snapshot))
                return "control_surface_canvas";
            if (snapshot.HasSkinnedMesh)
                return "skinned_or_deforming_deferred";
            if (snapshot.HasAnimation)
                return "animated_or_rigged_deferred";
            return ResolveAssetClass(snapshot);
        }

        private static bool IsExportReady(Snapshot snapshot)
        {
            if (snapshot == null)
                return false;

            return !snapshot.HasSkinnedMesh &&
                !HasBlockingAnimation(snapshot) &&
                !snapshot.HasInvalidScreenContract &&
                !snapshot.HasInvalidControlSurface;
        }

        private static bool HasBlockingAnimation(Snapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasAnimation)
                return false;

            return !IsControlSurfaceCanvas(snapshot);
        }

        private static bool IsControlSurfaceCanvas(Snapshot snapshot)
        {
            return snapshot != null &&
                snapshot.ControlSurfaceContract != null &&
                snapshot.ControlSurfaceContract.Elements != null &&
                snapshot.ControlSurfaceContract.Elements.Count > 0 &&
                string.Equals(snapshot.ControlSurfaceContract.LayoutSource, "canvas_export_v1", StringComparison.OrdinalIgnoreCase) &&
                !snapshot.HasSkinnedMesh;
        }

        private static bool TryAttachControlSurfaceCanvasSnapshot(GameObject rootObject, Snapshot snapshot)
        {
            if (rootObject == null || snapshot == null || snapshot.ControlSurfaceContract == null)
                return false;

            SerializedObject metadataObject;
            if (!TryFindControlSurfaceMetadata(rootObject, out metadataObject))
                return false;

            RectTransform surfaceRoot = ResolveSerializedTransform(metadataObject, "surfaceRoot") as RectTransform;
            if (surfaceRoot == null || string.IsNullOrWhiteSpace(snapshot.ControlSurfaceContract.SurfaceNodeId))
                return false;

            float surfaceUnitsToMetersMultiplier = ResolveSurfaceUnitsToMetersMultiplier(metadataObject, surfaceRoot);

            Texture2D snapshotTexture;
            if (!TryCaptureControlSurfaceCanvasTexture(surfaceRoot, out snapshotTexture))
                return false;

            try
            {
                byte[] pngBytes = snapshotTexture.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length <= 0)
                    return false;

                Rect rootRect = surfaceRoot.rect;
                string snapshotHash = BuildContentHash(pngBytes);
                string materialRefId = BuildStableId(
                    snapshot.ControlSurfaceContract.ControlSurfaceId + "_snapshot_material_" + snapshotHash);
                string base64 = Convert.ToBase64String(pngBytes);
                snapshot.CanvasSnapshot = new ControlSurfaceCanvasSnapshot
                {
                    SurfaceNodeId = snapshot.ControlSurfaceContract.SurfaceNodeId,
                    MaterialRefId = materialRefId,
                    TexturePngBase64 = base64,
                    LocalXMin = rootRect.xMin * surfaceUnitsToMetersMultiplier,
                    LocalXMax = rootRect.xMax * surfaceUnitsToMetersMultiplier,
                    LocalYMin = rootRect.yMin * surfaceUnitsToMetersMultiplier,
                    LocalYMax = rootRect.yMax * surfaceUnitsToMetersMultiplier
                };

                snapshot.Materials.Add(new UnityInnerPieceMaterialSummary
                {
                    MaterialRefId = materialRefId,
                    DisplayName = snapshot.ControlSurfaceContract.ControlSurfaceLabel + " Snapshot",
                    ShaderName = "Unlit/Texture",
                    BaseColorHex = "#FFFFFF",
                    HasTexture = true,
                    TexturePngBase64 = base64,
                    FeatureFlags = new List<string>
                    {
                        "base_color",
                        "texture_ref_present",
                        "inline_texture_png_base64",
                        "control_surface_canvas_snapshot"
                    }
                });

                snapshot.RendererCount += 1;
                snapshot.MeshCount += 1;
                snapshot.TotalVertexCount += 4;
                snapshot.TotalTriangleCount += 4;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshotTexture);
            }
        }

        private static bool TryCaptureControlSurfaceCanvasTexture(RectTransform surfaceRoot, out Texture2D texture)
        {
            texture = null;
            if (surfaceRoot == null)
                return false;

            Vector3[] worldCorners = new Vector3[4];
            surfaceRoot.GetWorldCorners(worldCorners);
            Vector3 bottomLeft = worldCorners[0];
            Vector3 topLeft = worldCorners[1];
            Vector3 topRight = worldCorners[2];
            Vector3 bottomRight = worldCorners[3];

            Vector3 right = bottomRight - bottomLeft;
            Vector3 up = topLeft - bottomLeft;
            float widthMeters = right.magnitude;
            float heightMeters = up.magnitude;
            if (widthMeters <= 0.0001f || heightMeters <= 0.0001f)
                return false;

            Vector3 center = (bottomLeft + topRight) * 0.5f;
            Vector3 forward = surfaceRoot.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 upDirection = up.sqrMagnitude <= 0.0001f ? Vector3.up : up.normalized;

            int widthPixels;
            int heightPixels;
            ResolveCanvasSnapshotDimensions(widthMeters, heightMeters, out widthPixels, out heightPixels);

            GameObject tempCameraObject = null;
            try
            {
                tempCameraObject = new GameObject("FrameAngelControlSurfaceCanvasSnapshotCamera");
                tempCameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCamera = tempCameraObject.AddComponent<Camera>();
                tempCamera.enabled = false;
                tempCamera.orthographic = true;
                tempCamera.clearFlags = CameraClearFlags.SolidColor;
                tempCamera.backgroundColor = Color.clear;
                tempCamera.cullingMask = ~0;
                tempCamera.nearClipPlane = 0.001f;
                tempCamera.farClipPlane = 10f;
                tempCamera.orthographicSize = heightMeters * 0.5f;
                tempCamera.aspect = (float)widthPixels / Mathf.Max(1f, heightPixels);
                // Meta world-space canvases render operator-facing content on the side opposite
                // the local forward we get from the exported RectTransform basis here.
                // Capturing from the forward side mirrored all text and icons.
                tempCamera.transform.position = center - (forward * 0.5f);
                tempCamera.transform.rotation = Quaternion.LookRotation(forward, upDirection);
                texture = UnityBridgeCaptureService.CaptureCameraTexture(tempCamera, widthPixels, heightPixels, Color.clear);
                return texture != null;
            }
            finally
            {
                if (tempCameraObject != null)
                    UnityEngine.Object.DestroyImmediate(tempCameraObject);
            }
        }

        private static void ResolveCanvasSnapshotDimensions(float widthMeters, float heightMeters, out int widthPixels, out int heightPixels)
        {
            float aspect = widthMeters / Mathf.Max(0.001f, heightMeters);
            const int maxDimension = 1024;
            const int minDimension = 256;
            if (aspect >= 1f)
            {
                widthPixels = maxDimension;
                heightPixels = Mathf.Clamp(Mathf.RoundToInt(maxDimension / Mathf.Max(0.001f, aspect)), minDimension, maxDimension);
            }
            else
            {
                heightPixels = maxDimension;
                widthPixels = Mathf.Clamp(Mathf.RoundToInt(maxDimension * aspect), minDimension, maxDimension);
            }
        }

        private static string BuildContentHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 0)
                return "empty";

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }
        }

        private static bool TryComputeControlSurfaceCanvasBounds(Snapshot snapshot, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (snapshot == null || snapshot.ControlSurfaceContract == null || snapshot.CanvasSnapshot == null)
                return false;

            CollectedNode surfaceNode = snapshot.Nodes.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.NodeId, snapshot.ControlSurfaceContract.SurfaceNodeId, StringComparison.OrdinalIgnoreCase));
            if (surfaceNode == null || surfaceNode.Transform == null)
                return false;

            float width = Mathf.Max(0.001f, snapshot.CanvasSnapshot.LocalXMax - snapshot.CanvasSnapshot.LocalXMin);
            float height = Mathf.Max(0.001f, snapshot.CanvasSnapshot.LocalYMax - snapshot.CanvasSnapshot.LocalYMin);
            Vector3 localCenter = new Vector3(
                (snapshot.CanvasSnapshot.LocalXMin + snapshot.CanvasSnapshot.LocalXMax) * 0.5f,
                (snapshot.CanvasSnapshot.LocalYMin + snapshot.CanvasSnapshot.LocalYMax) * 0.5f,
                0f);
            Vector3 size = new Vector3(
                Mathf.Abs(width * surfaceNode.Transform.lossyScale.x),
                Mathf.Abs(height * surfaceNode.Transform.lossyScale.y),
                0.001f);
            bounds = new Bounds(surfaceNode.Transform.TransformPoint(localCenter), size);
            return true;
        }

        private static void AppendControlSurfaceCanvasSnapshotGeometry(FAIPEGeometryPackage geometry, Snapshot snapshot)
        {
            if (geometry == null || snapshot == null || snapshot.CanvasSnapshot == null)
                return;

            FAIPEExportNode surfaceNode = geometry.Nodes.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.NodeId, snapshot.CanvasSnapshot.SurfaceNodeId, StringComparison.OrdinalIgnoreCase));
            if (surfaceNode == null)
                return;

            string meshId = BuildStableId(snapshot.CanvasSnapshot.SurfaceNodeId + "_snapshot_mesh");
            if (!surfaceNode.MeshRefIds.Contains(meshId))
                surfaceNode.MeshRefIds.Add(meshId);
            if (geometry.Meshes.Any(mesh => mesh != null && string.Equals(mesh.MeshId, meshId, StringComparison.OrdinalIgnoreCase)))
                return;

            float xMin = snapshot.CanvasSnapshot.LocalXMin;
            float xMax = snapshot.CanvasSnapshot.LocalXMax;
            float yMin = snapshot.CanvasSnapshot.LocalYMin;
            float yMax = snapshot.CanvasSnapshot.LocalYMax;
            FAIPEExportMesh mesh = new FAIPEExportMesh
            {
                MeshId = meshId,
                MaterialRefId = snapshot.CanvasSnapshot.MaterialRefId,
                SubmeshIndex = 0,
                LocalBounds = UnityBounds3.FromBounds(new Bounds(
                    new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f),
                    new Vector3(Mathf.Max(0.001f, xMax - xMin), Mathf.Max(0.001f, yMax - yMin), 0.001f)))
            };

            mesh.Vertices.Add(UnityVector3.FromVector3(new Vector3(xMin, yMin, 0f)));
            mesh.Vertices.Add(UnityVector3.FromVector3(new Vector3(xMin, yMax, 0f)));
            mesh.Vertices.Add(UnityVector3.FromVector3(new Vector3(xMax, yMax, 0f)));
            mesh.Vertices.Add(UnityVector3.FromVector3(new Vector3(xMax, yMin, 0f)));

            mesh.TriangleIndices.AddRange(new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 });

            for (int i = 0; i < 4; i++)
                mesh.Normals.Add(UnityVector3.FromVector3(Vector3.forward));

            mesh.Uv0.Add(UnityVector2.FromVector2(new Vector2(0f, 0f)));
            mesh.Uv0.Add(UnityVector2.FromVector2(new Vector2(0f, 1f)));
            mesh.Uv0.Add(UnityVector2.FromVector2(new Vector2(1f, 1f)));
            mesh.Uv0.Add(UnityVector2.FromVector2(new Vector2(1f, 0f)));

            geometry.Meshes.Add(mesh);
        }

        private static string[] MergeTags(List<string> tags, string tagsCsv)
        {
            HashSet<string> merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tags[i]))
                        merged.Add(tags[i].Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(tagsCsv))
            {
                string[] split = tagsCsv.Split(',');
                for (int i = 0; i < split.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(split[i]))
                        merged.Add(split[i].Trim());
                }
            }

            return merged.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string ResolvePackageRoot(string requestedOutputPath, string packageId)
        {
            string baseRoot;
            if (string.IsNullOrWhiteSpace(requestedOutputPath))
            {
                baseRoot = UnityBridgeController.InnerPieceExportRoot;
            }
            else
            {
                string resolved = ResolveAbsolutePath(requestedOutputPath);
                baseRoot = Path.HasExtension(resolved) ? (Path.GetDirectoryName(resolved) ?? UnityBridgeController.InnerPieceExportRoot) : resolved;
            }

            return Path.Combine(baseRoot, packageId);
        }

        private static string ResolveAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return UnityBridgeController.InnerPieceExportRoot;

            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(UnityBridgeInspector.ProjectPath, path));
        }

        private static long ComputePackageBytes(string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
                return 0L;

            long totalBytes = 0L;
            string[] files = Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                totalBytes += new FileInfo(files[i]).Length;
            return totalBytes;
        }

        private static void GatherTransforms(Transform root, List<Transform> orderedTransforms)
        {
            orderedTransforms.Add(root);
            for (int i = 0; i < root.childCount; i++)
                GatherTransforms(root.GetChild(i), orderedTransforms);
        }

        private static string BuildRelativePath(Transform root, Transform current)
        {
            if (root == null || current == null)
                return "";
            if (root == current)
                return root.name;

            List<string> segments = new List<string>();
            Transform cursor = current;
            while (cursor != null)
            {
                segments.Add(cursor.name);
                if (cursor == root)
                    break;
                cursor = cursor.parent;
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static string BuildStableId(string value)
        {
            string sanitized = new string((value ?? "")
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray())
                .Trim('_');

            if (string.IsNullOrEmpty(sanitized))
                sanitized = "item";
            while (sanitized.Contains("__"))
                sanitized = sanitized.Replace("__", "_");
            return sanitized;
        }

        private static string EnsureUniqueStableId(string baseId, HashSet<string> usedIds)
        {
            string candidate = string.IsNullOrWhiteSpace(baseId) ? "item" : baseId;
            if (usedIds == null)
                return candidate;

            if (usedIds.Add(candidate))
                return candidate;

            int suffix = 2;
            while (true)
            {
                string suffixed = candidate + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                if (usedIds.Add(suffixed))
                    return suffixed;

                suffix++;
            }
        }

        private static string BuildStableResourceId(string displayName, string fingerprint)
        {
            string prefix = BuildStableId(displayName);
            string suffix = string.IsNullOrEmpty(fingerprint)
                ? "resource"
                : fingerprint.Substring(0, Math.Min(12, fingerprint.Length)).ToLowerInvariant();
            return prefix + "_" + suffix;
        }

        private static bool TryComputeBoundsFromRoot(GameObject rootObject, out Bounds bounds)
        {
            bounds = new Bounds();
            if (rootObject == null)
                return false;

            Renderer[] renderers = rootObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;
                if (i == 0)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            return renderers.Length > 0;
        }

        private static Texture TryGetMainTexture(Material material, out string assetPath)
        {
            assetPath = "";
            if (material == null)
                return null;

            Texture texture = null;
            if (material.HasProperty("_BaseMap"))
                texture = material.GetTexture("_BaseMap");
            else if (material.HasProperty("_MainTex"))
                texture = material.mainTexture;

            if (texture != null)
                assetPath = AssetDatabase.GetAssetPath(texture) ?? "";
            return texture;
        }

        private static bool TryGetMaterialColor(Material material, out Color colorValue)
        {
            colorValue = Color.white;
            if (material == null)
                return false;
            if (material.HasProperty("_BaseColor"))
            {
                colorValue = material.GetColor("_BaseColor");
                return true;
            }
            if (material.HasProperty("_Color"))
            {
                colorValue = material.color;
                return true;
            }
            return false;
        }

        private static string BuildMeshPartIdentity(Mesh mesh, int[] triangles, string materialRefId, int submeshIndex)
        {
            StringBuilder sb = new StringBuilder(4096);
            sb.Append(mesh != null ? mesh.name : "").Append('|');
            sb.Append(materialRefId ?? "").Append('|');
            sb.Append(submeshIndex.ToString(CultureInfo.InvariantCulture)).Append('|');

            Vector3[] vertices = mesh != null && mesh.vertices != null ? mesh.vertices : new Vector3[0];
            for (int i = 0; i < vertices.Length; i++)
            {
                sb.Append(vertices[i].x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(vertices[i].y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(vertices[i].z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            for (int i = 0; i < triangles.Length; i++)
                sb.Append(triangles[i].ToString(CultureInfo.InvariantCulture)).Append(',');

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void AppendBoundsFingerprint(StringBuilder sb, UnityBounds3 bounds)
        {
            AppendVector3Fingerprint(sb, bounds != null ? bounds.Center : new UnityVector3());
            AppendVector3Fingerprint(sb, bounds != null ? bounds.Size : new UnityVector3());
        }

        private static void AppendVector3Fingerprint(StringBuilder sb, UnityVector3 value)
        {
            sb.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        private static void AppendQuaternionFingerprint(StringBuilder sb, UnityQuaternion value)
        {
            sb.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.W.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        private static bool TryCapturePreviewArtifact(
            UnityInnerPieceExportSource source,
            string label,
            string outputPath,
            int width,
            int height,
            out UnityBridgeArtifact artifact,
            out List<string> warnings,
            out string errorMessage)
        {
            artifact = null;
            warnings = new List<string>();
            errorMessage = "";

            if (source == null || source.RootObject == null)
            {
                errorMessage = "Missing InnerPiece preview source.";
                return false;
            }

            GameObject previewTarget = source.RootObject;
            bool destroyPreviewTarget = false;
            if (EditorUtility.IsPersistent(source.RootObject))
            {
                previewTarget = UnityEngine.Object.Instantiate(source.RootObject);
                previewTarget.name = source.RootObject.name + "_Preview";
                previewTarget.hideFlags = HideFlags.HideAndDontSave;
                ApplyHideFlagsRecursively(previewTarget.transform);
                previewTarget.transform.position = Vector3.zero;
                previewTarget.transform.rotation = Quaternion.identity;
                previewTarget.transform.localScale = Vector3.one;
                destroyPreviewTarget = true;
            }

            GameObject tempCameraObject = null;
            GameObject tempLightObject = null;
            try
            {
                Bounds bounds;
                if (!TryComputeBoundsFromRoot(previewTarget, out bounds))
                {
                    errorMessage = "Preview source has no renderable bounds.";
                    return false;
                }

                tempCameraObject = new GameObject("FrameAngelInnerPiecePreviewCamera");
                tempCameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCamera = tempCameraObject.AddComponent<Camera>();
                tempCamera.enabled = false;
                tempCamera.clearFlags = CameraClearFlags.SolidColor;
                tempCamera.backgroundColor = new Color(0.92f, 0.94f, 0.98f, 1f);
                tempCamera.fieldOfView = 35f;
                tempCamera.nearClipPlane = 0.01f;
                float distance = Mathf.Max(0.5f, bounds.extents.magnitude * 2.75f);
                tempCamera.farClipPlane = Mathf.Max(100f, distance * 10f);
                tempCamera.transform.position = bounds.center + new Vector3(0f, distance * 0.2f, distance);
                tempCamera.transform.rotation = Quaternion.LookRotation(bounds.center - tempCamera.transform.position, Vector3.up);

                tempLightObject = new GameObject("FrameAngelInnerPiecePreviewLight");
                tempLightObject.hideFlags = HideFlags.HideAndDontSave;
                Light light = tempLightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                tempLightObject.transform.rotation = Quaternion.Euler(40f, 35f, 0f);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? UnityBridgeController.CaptureRoot);
                Texture2D texture = UnityBridgeCaptureService.CaptureCameraTexture(
                    tempCamera,
                    Mathf.Max(64, width > 0 ? width : 512),
                    Mathf.Max(64, height > 0 ? height : 512),
                    tempCamera.backgroundColor);
                try
                {
                    artifact = UnityBridgeCaptureService.SaveCaptureArtifact(
                        outputPath,
                        label,
                        "InnerPiecePreview",
                        texture,
                        previewTarget.scene.IsValid() ? previewTarget.scene.name ?? "" : "");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                return artifact != null;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                if (tempLightObject != null)
                    UnityEngine.Object.DestroyImmediate(tempLightObject);
                if (tempCameraObject != null)
                    UnityEngine.Object.DestroyImmediate(tempCameraObject);
                if (destroyPreviewTarget && previewTarget != null)
                    UnityEngine.Object.DestroyImmediate(previewTarget);
            }
        }

        private static void ApplyHideFlagsRecursively(Transform root)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (int i = 0; i < root.childCount; i++)
                ApplyHideFlagsRecursively(root.GetChild(i));
        }
    }
}
