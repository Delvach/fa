using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameAngel.UnityEditorBridge
{
    internal static class UnityBridgeCaptureService
    {
        public static UnityBridgeResponse CaptureSceneView(UnityBridgeCommandRequest request)
        {
            UnityCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnityCaptureArgs>()
                : new UnityCaptureArgs();

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                return UnityBridgeResponse.Error("SCENE_VIEW_UNAVAILABLE", "No active SceneView camera is available for capture.", request.RequestId);
            }

            try
            {
                int width = ResolveDimension(args.Width, Mathf.RoundToInt(sceneView.position.width));
                int height = ResolveDimension(args.Height, Mathf.RoundToInt(sceneView.position.height));
                Texture2D capture = CaptureCameraTexture(sceneView.camera, width, height, null);
                try
                {
                    string outputPath = ResolveOutputPath(args.OutputPath, string.IsNullOrWhiteSpace(args.Label) ? "scene_view" : args.Label);
                    string sceneName = SceneManager.GetActiveScene().name ?? "";
                    Dictionary<string, object> data = new Dictionary<string, object>
                    {
                        { "cameraPosition", UnityVector3.FromVector3(sceneView.camera.transform.position) },
                        { "cameraRotationEuler", UnityVector3.FromVector3(sceneView.camera.transform.eulerAngles) },
                        { "sceneName", sceneName }
                    };

                    return CreateSingleCaptureResponse(request.RequestId, args.Label, "scene_view", "SceneView", outputPath, capture, sceneName, data);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("SCENE_VIEW_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
        }

        public static UnityBridgeResponse CaptureGameView(UnityBridgeCommandRequest request)
        {
            UnityCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnityCaptureArgs>()
                : new UnityCaptureArgs();

            EditorWindow gameViewWindow = TryGetOpenGameView();
            if (gameViewWindow == null)
            {
                return UnityBridgeResponse.Error("GAME_VIEW_UNAVAILABLE", "No open GameView window is available for capture.", request.RequestId);
            }

            Texture2D capture = null;
            try
            {
                capture = ScreenCapture.CaptureScreenshotAsTexture();
                if (capture == null)
                {
                    return UnityBridgeResponse.Error("GAME_VIEW_CAPTURE_FAILED", "GameView capture returned no texture.", request.RequestId);
                }

                string sceneName = SceneManager.GetActiveScene().name ?? "";
                UnityBridgeResponse response = CreateSingleCaptureResponse(
                    request.RequestId,
                    args.Label,
                    "game_view",
                    "GameView",
                    ResolveOutputPath(args.OutputPath, string.IsNullOrWhiteSpace(args.Label) ? "game_view" : args.Label),
                    capture,
                    sceneName,
                    new Dictionary<string, object>
                    {
                        { "sceneName", sceneName },
                        { "isPlaying", EditorApplication.isPlaying }
                    });

                if (!EditorApplication.isPlaying)
                {
                    response.Warnings.Add("GameView capture ran outside play mode; returned content depends on the current editor GameView state.");
                }

                return response;
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("GAME_VIEW_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
            finally
            {
                if (capture != null)
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
            }
        }

        public static UnityBridgeResponse CaptureNamedCamera(UnityBridgeCommandRequest request)
        {
            UnityCameraCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnityCameraCaptureArgs>()
                : new UnityCameraCaptureArgs();

            if (string.IsNullOrWhiteSpace(args.CameraName))
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "capture.camera requires args.cameraName.", request.RequestId);
            }

            Camera camera = UnityBridgeInspector.GetCameraByName(args.CameraName);
            if (camera == null)
            {
                return UnityBridgeResponse.Error("CAMERA_NOT_FOUND", "Camera '" + args.CameraName + "' was not found.", request.RequestId);
            }

            try
            {
                Texture2D capture = CaptureCameraTexture(camera, ResolveDimension(args.Width, 1024), ResolveDimension(args.Height, 1024), null);
                try
                {
                    string sceneName = camera.gameObject.scene.IsValid() ? camera.gameObject.scene.name ?? "" : SceneManager.GetActiveScene().name ?? "";
                    Dictionary<string, object> data = new Dictionary<string, object>
                    {
                        { "cameraName", camera.name },
                        { "cameraPosition", UnityVector3.FromVector3(camera.transform.position) },
                        { "cameraRotationEuler", UnityVector3.FromVector3(camera.transform.eulerAngles) },
                        { "sceneName", sceneName }
                    };

                    return CreateSingleCaptureResponse(
                        request.RequestId,
                        args.Label,
                        "camera",
                        camera.name,
                        ResolveOutputPath(args.OutputPath, string.IsNullOrWhiteSpace(args.Label) ? camera.name : args.Label),
                        capture,
                        sceneName,
                        data);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("CAMERA_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
        }

        public static UnityBridgeResponse CaptureOrbitView(UnityBridgeCommandRequest request)
        {
            UnityOrbitCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnityOrbitCaptureArgs>()
                : new UnityOrbitCaptureArgs();

            GameObject target = UnityBridgeWorkspaceService.ResolveObject(new UnityObjectReferenceArgs
            {
                ObjectId = args.ObjectId,
                Path = args.Path,
                InstanceId = args.InstanceId
            });
            if (target == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "capture.orbit_view could not resolve the requested target object.", request.RequestId);
            }

            Bounds bounds;
            if (!UnityBridgeWorkspaceService.TryGetWorldBounds(target, out bounds))
            {
                bounds = new Bounds(target.transform.position, Vector3.one);
            }

            int width = ResolveDimension(args.Width, 1024);
            int height = ResolveDimension(args.Height, 1024);
            float canonicalDistance = UnityBridgeWorkspaceService.ResolveCanonicalDistance(bounds);
            float distance = canonicalDistance * Mathf.Max(0.1f, args.DistanceScale);
            float fieldOfView = Mathf.Clamp(args.FieldOfView, 5f, 120f);
            Color background = ParseColorOrDefault(args.BackgroundColorHex, Color.white);
            Vector3 focus = bounds.center + (args.LookAtOffset != null ? args.LookAtOffset.ToVector3() : Vector3.zero);
            Quaternion orbitRotation = Quaternion.Euler(args.PitchDegrees, args.YawDegrees, args.RollDegrees);
            Vector3 direction = orbitRotation * Vector3.forward;

            GameObject tempCameraObject = null;
            GameObject tempLightObject = null;
            try
            {
                tempCameraObject = new GameObject("FrameAngelOrbitCaptureCamera");
                tempCameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCamera = tempCameraObject.AddComponent<Camera>();
                tempCamera.enabled = false;
                tempCamera.clearFlags = CameraClearFlags.SolidColor;
                tempCamera.backgroundColor = background;
                tempCamera.fieldOfView = fieldOfView;
                tempCamera.nearClipPlane = 0.01f;
                tempCamera.farClipPlane = Mathf.Max(100f, distance * 10f);
                tempCamera.transform.position = focus + (direction * distance);
                tempCamera.transform.rotation = Quaternion.LookRotation(focus - tempCamera.transform.position, Vector3.up);

                tempLightObject = new GameObject("FrameAngelOrbitCaptureLight");
                tempLightObject.hideFlags = HideFlags.HideAndDontSave;
                Light light = tempLightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                tempLightObject.transform.rotation = Quaternion.Euler(45f, args.YawDegrees + 35f, 0f);

                Texture2D capture = CaptureCameraTexture(tempCamera, width, height, background);
                try
                {
                    string sceneName = target.scene.IsValid() ? target.scene.name ?? "" : SceneManager.GetActiveScene().name ?? "";
                    Dictionary<string, object> data = new Dictionary<string, object>
                    {
                        { "targetObjectId", args.ObjectId ?? "" },
                        { "targetPath", UnityBridgeInspector.BuildPath(target.transform) },
                        { "cameraPosition", UnityVector3.FromVector3(tempCamera.transform.position) },
                        { "cameraRotationEuler", UnityVector3.FromVector3(tempCamera.transform.eulerAngles) },
                        { "lookAtPoint", UnityVector3.FromVector3(focus) },
                        { "yawDegrees", args.YawDegrees },
                        { "pitchDegrees", args.PitchDegrees },
                        { "rollDegrees", args.RollDegrees },
                        { "distance", distance },
                        { "fieldOfView", fieldOfView },
                        { "sceneName", sceneName }
                    };

                    return CreateSingleCaptureResponse(
                        request.RequestId,
                        args.Label,
                        "orbit_view",
                        "OrbitView",
                        ResolveOutputPath(args.OutputPath, string.IsNullOrWhiteSpace(args.Label) ? "orbit_view" : args.Label),
                        capture,
                        sceneName,
                        data);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("ORBIT_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
            finally
            {
                if (tempLightObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempLightObject);
                }

                if (tempCameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempCameraObject);
                }
            }
        }

        public static UnityBridgeResponse CaptureSectionView(UnityBridgeCommandRequest request)
        {
            UnitySectionCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnitySectionCaptureArgs>()
                : new UnitySectionCaptureArgs();

            GameObject target = UnityBridgeWorkspaceService.ResolveObject(new UnityObjectReferenceArgs
            {
                ObjectId = args.ObjectId,
                Path = args.Path,
                InstanceId = args.InstanceId
            });
            if (target == null)
            {
                return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "capture.section_view could not resolve the requested target object.", request.RequestId);
            }

            Bounds bounds;
            if (!UnityBridgeWorkspaceService.TryGetWorldBounds(target, out bounds))
            {
                bounds = new Bounds(target.transform.position, Vector3.one);
            }

            string axis = NormalizeSectionAxis(args.Axis);
            Vector3 direction = ResolveSectionDirection(axis);
            Vector3 up = ResolveSectionUpVector(axis);
            float extentAlongAxis = ResolveBoundsExtentAlongDirection(bounds, direction);
            float sliceOffsetNormalized = Mathf.Clamp(args.SliceOffsetNormalized, -1f, 1f);
            Vector3 slicePoint = bounds.center + (direction * (extentAlongAxis * sliceOffsetNormalized));
            int width = ResolveDimension(args.Width, 1024);
            int height = ResolveDimension(args.Height, 1024);
            float orthographicPadding = Mathf.Max(1.01f, args.OrthographicPadding);
            Color background = ParseColorOrDefault(args.BackgroundColorHex, new Color(0f, 1f, 0.4f, 1f));
            float standOff = Mathf.Max(0.5f, extentAlongAxis + (UnityBridgeWorkspaceService.ResolveCanonicalDistance(bounds) * 0.35f));

            GameObject tempCameraObject = null;
            GameObject tempLightObject = null;
            try
            {
                tempCameraObject = new GameObject("FrameAngelSectionCaptureCamera");
                tempCameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCamera = tempCameraObject.AddComponent<Camera>();
                tempCamera.enabled = false;
                tempCamera.clearFlags = CameraClearFlags.SolidColor;
                tempCamera.backgroundColor = background;
                tempCamera.orthographic = true;
                tempCamera.transform.position = slicePoint - (direction * standOff);
                tempCamera.transform.rotation = Quaternion.LookRotation(direction, up);

                float aspect = Mathf.Max(0.01f, width / (float)height);
                float maxAbsX;
                float maxAbsY;
                float maxPositiveZ;
                ResolveCameraFrame(bounds, tempCamera.transform, out maxAbsX, out maxAbsY, out maxPositiveZ);
                tempCamera.orthographicSize = Mathf.Max(maxAbsY, maxAbsX / aspect) * orthographicPadding;

                float sliceEpsilon = Mathf.Max(0.0025f, extentAlongAxis * 0.0025f);
                float sliceDistance = tempCamera.transform.InverseTransformPoint(slicePoint).z;
                tempCamera.nearClipPlane = Mathf.Max(0.01f, sliceDistance + sliceEpsilon);
                tempCamera.farClipPlane = Mathf.Max(tempCamera.nearClipPlane + 1f, maxPositiveZ + 1f);

                tempLightObject = new GameObject("FrameAngelSectionCaptureLight");
                tempLightObject.hideFlags = HideFlags.HideAndDontSave;
                Light light = tempLightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                tempLightObject.transform.rotation = tempCamera.transform.rotation;

                Texture2D capture = CaptureCameraTexture(tempCamera, width, height, background);
                try
                {
                    string sceneName = target.scene.IsValid() ? target.scene.name ?? "" : SceneManager.GetActiveScene().name ?? "";
                    Dictionary<string, object> data = new Dictionary<string, object>
                    {
                        { "targetObjectId", args.ObjectId ?? "" },
                        { "targetPath", UnityBridgeInspector.BuildPath(target.transform) },
                        { "axis", axis },
                        { "sliceOffsetNormalized", sliceOffsetNormalized },
                        { "cameraPosition", UnityVector3.FromVector3(tempCamera.transform.position) },
                        { "cameraRotationEuler", UnityVector3.FromVector3(tempCamera.transform.eulerAngles) },
                        { "slicePoint", UnityVector3.FromVector3(slicePoint) },
                        { "orthographicSize", tempCamera.orthographicSize },
                        { "nearClipPlane", tempCamera.nearClipPlane },
                        { "farClipPlane", tempCamera.farClipPlane },
                        { "sceneName", sceneName }
                    };

                    return CreateSingleCaptureResponse(
                        request.RequestId,
                        args.Label,
                        "section_" + axis,
                        "SectionView/" + axis,
                        ResolveOutputPath(args.OutputPath, string.IsNullOrWhiteSpace(args.Label) ? "section_" + axis : args.Label),
                        capture,
                        sceneName,
                        data);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("SECTION_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
            finally
            {
                if (tempLightObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempLightObject);
                }

                if (tempCameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempCameraObject);
                }
            }
        }

        public static UnityBridgeResponse CaptureMulticamRig(UnityBridgeCommandRequest request)
        {
            UnityMulticamCaptureArgs args = request.Args != null
                ? request.Args.ToObject<UnityMulticamCaptureArgs>()
                : new UnityMulticamCaptureArgs();

            if (string.IsNullOrWhiteSpace(args.CaptureBundleId) ||
                string.IsNullOrWhiteSpace(args.SessionId) ||
                string.IsNullOrWhiteSpace(args.TargetId) ||
                args.Iteration <= 0)
            {
                return UnityBridgeResponse.Error(
                    "BAD_REQUEST",
                    "capture.multicam_rig requires captureBundleId, sessionId, targetId, and iteration > 0.",
                    request.RequestId);
            }

            GameObject target = UnityBridgeWorkspaceService.ResolveCaptureTarget(args);
            if (target == null)
            {
                return UnityBridgeResponse.Error("MULTICAM_TARGET_NOT_FOUND", "No capture target was found for the canonical multicam rig.", request.RequestId);
            }

            Bounds bounds;
            List<string> warnings = new List<string>();
            if (!UnityBridgeWorkspaceService.TryGetWorldBounds(target, out bounds))
            {
                bounds = new Bounds(target.transform.position, Vector3.one);
                warnings.Add("Target has no renderer bounds; canonical rig used a unit fallback bounds centered on the target transform.");
            }

            int width = ResolveDimension(args.Width, 1024);
            int height = ResolveDimension(args.Height, 1024);
            float distance = UnityBridgeWorkspaceService.ResolveCanonicalDistance(bounds);
            string captureDirectory = ResolveOutputDirectory(args.OutputPath, args.CaptureBundleId);
            Directory.CreateDirectory(captureDirectory);

            string workspaceStatePath = Path.Combine(captureDirectory, "workspace_state.iter-" + args.Iteration.ToString("D3") + ".json");
            UnityWorkspaceStateData workspaceState = UnityBridgeWorkspaceService.GetWorkspaceState();
            File.WriteAllText(
                workspaceStatePath,
                JsonConvert.SerializeObject(
                    workspaceState,
                    Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include }));

            List<UnityBridgeArtifact> artifacts = new List<UnityBridgeArtifact>();
            List<UnityCaptureBundleView> views = new List<UnityCaptureBundleView>();
            GameObject tempCameraObject = null;
            GameObject tempLightObject = null;

            try
            {
                tempCameraObject = new GameObject("FrameAngelCanonicalRigCamera");
                tempCameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCamera = tempCameraObject.AddComponent<Camera>();
                tempCamera.enabled = false;
                tempCamera.clearFlags = CameraClearFlags.SolidColor;
                tempCamera.backgroundColor = Color.white;
                tempCamera.fieldOfView = 35f;
                tempCamera.nearClipPlane = 0.01f;
                tempCamera.farClipPlane = Mathf.Max(100f, distance * 10f);

                tempLightObject = new GameObject("FrameAngelCanonicalRigLight");
                tempLightObject.hideFlags = HideFlags.HideAndDontSave;
                Light light = tempLightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                tempLightObject.transform.rotation = Quaternion.Euler(45f, 45f, 0f);

                Vector3 center = bounds.center;
                foreach (KeyValuePair<string, Vector3> view in GetCanonicalViewDirections())
                {
                    Vector3 position = center + (view.Value * distance);
                    Vector3 up = ResolveUpVector(view.Key);
                    tempCamera.transform.position = position;
                    tempCamera.transform.rotation = Quaternion.LookRotation(center - position, up);

                    Texture2D capture = CaptureCameraTexture(tempCamera, width, height, Color.white);
                    try
                    {
                        string outputPath = Path.Combine(captureDirectory, view.Key + ".png");
                        string capturedUtc = SaveCaptureTexture(outputPath, view.Key, "CanonicalRig/" + view.Key, capture, SceneManager.GetActiveScene().name ?? "", artifacts);
                        views.Add(new UnityCaptureBundleView
                        {
                            Label = view.Key,
                            Path = outputPath,
                            ContentType = "image/png",
                            Width = capture.width,
                            Height = capture.height,
                            CapturedUtc = capturedUtc
                        });
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(capture);
                    }
                }
            }
            catch (Exception ex)
            {
                return UnityBridgeResponse.Error("MULTICAM_CAPTURE_FAILED", ex.Message, request.RequestId);
            }
            finally
            {
                if (tempLightObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempLightObject);
                }

                if (tempCameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempCameraObject);
                }
            }

            UnityCaptureBundleData captureBundle = new UnityCaptureBundleData
            {
                CaptureBundleId = args.CaptureBundleId,
                SessionId = args.SessionId,
                TargetId = args.TargetId,
                Iteration = args.Iteration,
                WorkspaceStateRef = workspaceStatePath,
                RigId = UnityBridgeWorkspaceService.CanonicalRigId,
                TargetRootId = ResolveTargetRootId(target),
                TargetRootPath = UnityBridgeInspector.BuildPath(target.transform),
                TargetBounds = UnityBounds3.FromBounds(bounds),
                Views = views,
                CapturedUtc = views.Count > 0 ? views[views.Count - 1].CapturedUtc : UnityBridgeInspector.TimestampUtc()
            };

            return new UnityBridgeResponse
            {
                RequestId = request.RequestId,
                Data = new Dictionary<string, object>
                {
                    { "captureBundle", captureBundle }
                },
                Artifacts = artifacts,
                Warnings = warnings
            };
        }

        internal static string ResolveOutputPath(string requestedOutputPath, string label)
        {
            if (string.IsNullOrEmpty(requestedOutputPath))
            {
                return Path.Combine(UnityBridgeController.CaptureRoot, label + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".png");
            }

            string normalized = requestedOutputPath;
            if (!Path.IsPathRooted(normalized))
            {
                normalized = Path.GetFullPath(Path.Combine(UnityBridgeInspector.ProjectPath, normalized));
            }

            if (Path.HasExtension(normalized))
            {
                return normalized;
            }

            return Path.Combine(normalized, label + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".png");
        }

        internal static string ResolveOutputDirectory(string requestedOutputPath, string defaultDirectoryName)
        {
            if (string.IsNullOrWhiteSpace(requestedOutputPath))
            {
                return Path.Combine(UnityBridgeController.CaptureRoot, defaultDirectoryName);
            }

            string normalized = requestedOutputPath;
            if (!Path.IsPathRooted(normalized))
            {
                normalized = Path.GetFullPath(Path.Combine(UnityBridgeInspector.ProjectPath, normalized));
            }

            return Path.HasExtension(normalized)
                ? (Path.GetDirectoryName(normalized) ?? UnityBridgeController.CaptureRoot)
                : normalized;
        }

        private static UnityBridgeResponse CreateSingleCaptureResponse(
            string requestId,
            string requestedLabel,
            string defaultLabel,
            string cameraName,
            string outputPath,
            Texture2D texture,
            string sceneName,
            Dictionary<string, object> data)
        {
            List<UnityBridgeArtifact> artifacts = new List<UnityBridgeArtifact>();
            SaveCaptureTexture(outputPath, string.IsNullOrWhiteSpace(requestedLabel) ? defaultLabel : requestedLabel, cameraName, texture, sceneName, artifacts);
            return new UnityBridgeResponse
            {
                RequestId = requestId,
                Data = data,
                Artifacts = artifacts
            };
        }

        internal static UnityBridgeArtifact SaveCaptureArtifact(
            string outputPath,
            string label,
            string cameraName,
            Texture2D texture,
            string sceneName)
        {
            List<UnityBridgeArtifact> artifacts = new List<UnityBridgeArtifact>();
            SaveCaptureTexture(outputPath, label, cameraName, texture, sceneName, artifacts);
            return artifacts.Count > 0 ? artifacts[0] : null;
        }

        private static string SaveCaptureTexture(
            string outputPath,
            string label,
            string cameraName,
            Texture2D texture,
            string sceneName,
            List<UnityBridgeArtifact> artifacts)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? UnityBridgeController.CaptureRoot);
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());

            string capturedUtc = UnityBridgeInspector.TimestampUtc();
            UnityBridgeController.LastCapture = new UnityLastCaptureSummary
            {
                Kind = "image",
                Label = label,
                Path = outputPath,
                SceneName = sceneName,
                CameraName = cameraName,
                Width = texture.width,
                Height = texture.height,
                CapturedUtc = capturedUtc
            };

            artifacts.Add(new UnityBridgeArtifact
            {
                Kind = "image",
                Label = label,
                Path = outputPath,
                ContentType = "image/png",
                Metadata = new Dictionary<string, object>
                {
                    { "view", label },
                    { "rigId", cameraName.StartsWith("CanonicalRig/", StringComparison.Ordinal) ? UnityBridgeWorkspaceService.CanonicalRigId : "" },
                    { "cameraName", cameraName },
                    { "width", texture.width },
                    { "height", texture.height },
                    { "capturedUtc", capturedUtc }
                }
            });

            return capturedUtc;
        }

        internal static Texture2D CaptureCameraTexture(Camera camera, int width, int height, Color? backgroundOverride)
        {
            Texture2D capture = null;
            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            CameraClearFlags previousClearFlags = camera.clearFlags;
            Color previousBackground = camera.backgroundColor;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.useMipMap = false;
                renderTexture.autoGenerateMips = false;
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                if (backgroundOverride.HasValue)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = backgroundOverride.Value;
                }

                camera.Render();

                RenderTexture.active = renderTexture;
                capture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply(false, false);
                return capture;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackground;
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
        }

        private static int ResolveDimension(int requested, int fallback)
        {
            return Mathf.Max(1, requested > 0 ? requested : fallback);
        }

        private static Color ParseColorOrDefault(string colorHex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return fallback;
            }

            return ColorUtility.TryParseHtmlString(colorHex, out Color parsed)
                ? parsed
                : fallback;
        }

        private static string NormalizeSectionAxis(string axis)
        {
            switch ((axis ?? "").Trim().ToLowerInvariant())
            {
                case "right":
                case "front":
                case "back":
                case "top":
                case "bottom":
                    return axis.Trim().ToLowerInvariant();
                default:
                    return "left";
            }
        }

        private static Vector3 ResolveSectionDirection(string axis)
        {
            switch (axis)
            {
                case "right":
                    return Vector3.left;
                case "front":
                    return Vector3.back;
                case "back":
                    return Vector3.forward;
                case "top":
                    return Vector3.down;
                case "bottom":
                    return Vector3.up;
                default:
                    return Vector3.right;
            }
        }

        private static Vector3 ResolveSectionUpVector(string axis)
        {
            switch (axis)
            {
                case "top":
                    return Vector3.forward;
                case "bottom":
                    return Vector3.back;
                default:
                    return Vector3.up;
            }
        }

        private static float ResolveBoundsExtentAlongDirection(Bounds bounds, Vector3 direction)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            return Vector3.Dot(bounds.extents, absolute);
        }

        private static void ResolveCameraFrame(Bounds bounds, Transform cameraTransform, out float maxAbsX, out float maxAbsY, out float maxPositiveZ)
        {
            maxAbsX = 0f;
            maxAbsY = 0f;
            maxPositiveZ = 0f;

            foreach (Vector3 corner in GetBoundsCorners(bounds))
            {
                Vector3 local = cameraTransform.InverseTransformPoint(corner);
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(local.x));
                maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(local.y));
                maxPositiveZ = Mathf.Max(maxPositiveZ, local.z);
            }
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        private static EditorWindow TryGetOpenGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return null;
            }

            return Resources
                .FindObjectsOfTypeAll(gameViewType)
                .Cast<UnityEngine.Object>()
                .Select(obj => obj as EditorWindow)
                .FirstOrDefault(window => window != null);
        }

        private static IEnumerable<KeyValuePair<string, Vector3>> GetCanonicalViewDirections()
        {
            yield return new KeyValuePair<string, Vector3>("front", Vector3.forward);
            yield return new KeyValuePair<string, Vector3>("back", Vector3.back);
            yield return new KeyValuePair<string, Vector3>("left", Vector3.left);
            yield return new KeyValuePair<string, Vector3>("right", Vector3.right);
            yield return new KeyValuePair<string, Vector3>("top", Vector3.up);
            yield return new KeyValuePair<string, Vector3>("bottom", Vector3.down);
        }

        private static Vector3 ResolveUpVector(string label)
        {
            switch (label)
            {
                case "top":
                    return Vector3.forward;
                case "bottom":
                    return Vector3.back;
                default:
                    return Vector3.up;
            }
        }

        private static string ResolveTargetRootId(GameObject target)
        {
            if (target == null)
            {
                return "";
            }

            string objectId = UnityBridgeWorkspaceService.TryGetWorkspaceObjectId(target);
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                return objectId;
            }

            if (string.Equals(target.name, UnityBridgeWorkspaceService.TargetRootName, StringComparison.Ordinal))
            {
                return "workspace_root";
            }

            return target.name;
        }
    }
}
