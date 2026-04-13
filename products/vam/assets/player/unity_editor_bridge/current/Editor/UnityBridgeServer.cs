using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace FrameAngel.UnityEditorBridge
{
    internal sealed class UnityBridgeServer : IDisposable
    {
        private readonly IPAddress bindAddress;
        private readonly int port;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TcpListener listener;
        private Thread serverThread;

        public UnityBridgeServer(string host, int portValue)
        {
            bindAddress = ResolveAddress(host);
            port = portValue;
            listener = new TcpListener(bindAddress, port);
            listener.ExclusiveAddressUse = false;
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public void Start()
        {
            listener.Start();
            serverThread = new Thread(ListenLoop);
            serverThread.IsBackground = true;
            serverThread.Name = "FrameAngel Unity Editor Bridge";
            serverThread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            try
            {
                listener.Stop();
            }
            catch
            {
            }

            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join(TimeSpan.FromSeconds(2));
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException)
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true })
            {
                try
                {
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine))
                    {
                        return;
                    }

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        WriteResponse(writer, 400, UnityBridgeResponse.Error("BAD_REQUEST", "Malformed HTTP request line.", ""));
                        return;
                    }

                    string method = parts[0].Trim().ToUpperInvariant();
                    string path = parts[1].Trim();
                    Dictionary<string, string> headers = ReadHeaders(reader);
                    string body = ReadBody(reader, headers);
                    UnityBridgeResponse response = UnityBridgeDispatcher.Invoke(
                        () => HandleRequest(method, path, body),
                        TimeSpan.FromSeconds(20));

                    int statusCode = response.Ok ? 200 : ResolveStatusCode(response.Code);
                    WriteResponse(writer, statusCode, response);
                }
                catch (Exception ex)
                {
                    WriteResponse(writer, 500, UnityBridgeResponse.Error("SERVER_ERROR", ex.Message, ""));
                }
            }
        }

        private static Dictionary<string, string> ReadHeaders(StreamReader reader)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                int delimiter = line.IndexOf(':');
                if (delimiter <= 0)
                {
                    continue;
                }

                string name = line.Substring(0, delimiter).Trim();
                string value = line.Substring(delimiter + 1).Trim();
                headers[name] = value;
            }

            return headers;
        }

        private static string ReadBody(StreamReader reader, Dictionary<string, string> headers)
        {
            int contentLength;
            if (!headers.TryGetValue("Content-Length", out string contentLengthValue) ||
                !int.TryParse(contentLengthValue, out contentLength) ||
                contentLength <= 0)
            {
                return "";
            }

            char[] buffer = new char[contentLength];
            int offset = 0;
            while (offset < contentLength)
            {
                int read = reader.Read(buffer, offset, contentLength - offset);
                if (read <= 0)
                {
                    break;
                }

                offset += read;
            }

            return new string(buffer, 0, offset);
        }

        private static void WriteResponse(StreamWriter writer, int statusCode, UnityBridgeResponse payload)
        {
            string body = JsonConvert.SerializeObject(
                payload,
                Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            writer.WriteLine("HTTP/1.1 " + statusCode + " " + GetReasonPhrase(statusCode));
            writer.WriteLine("Content-Type: application/json; charset=utf-8");
            writer.WriteLine("Content-Length: " + bodyBytes.Length);
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Flush();
            writer.BaseStream.Write(bodyBytes, 0, bodyBytes.Length);
            writer.BaseStream.Flush();
        }

        private static UnityBridgeResponse HandleRequest(string method, string path, string body)
        {
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(path, "/v1/health", StringComparison.Ordinal))
                {
                    return new UnityBridgeResponse { Data = UnityBridgeInspector.GetHealth() };
                }

                if (string.Equals(path, "/v1/capabilities", StringComparison.Ordinal))
                {
                    return new UnityBridgeResponse { Data = UnityBridgeInspector.GetCapabilities() };
                }

                if (string.Equals(path, "/v1/state", StringComparison.Ordinal))
                {
                    return new UnityBridgeResponse { Data = UnityBridgeInspector.GetState() };
                }

                return UnityBridgeResponse.Error("NOT_FOUND", "Endpoint not found.", "");
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/v1/command", StringComparison.Ordinal))
            {
                UnityBridgeCommandRequest request = ParseCommandRequest(body);
                return HandleCommand(request);
            }

            return UnityBridgeResponse.Error("NOT_FOUND", "Endpoint not found.", "");
        }

        private static UnityBridgeCommandRequest ParseCommandRequest(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new UnityBridgeCommandRequest();
            }

            try
            {
                return JsonConvert.DeserializeObject<UnityBridgeCommandRequest>(body) ?? new UnityBridgeCommandRequest();
            }
            catch (JsonException ex)
            {
                return new UnityBridgeCommandRequest
                {
                    RequestId = "",
                    Action = "",
                    Metadata = new Dictionary<string, object>
                    {
                        { "parseError", ex.Message }
                    }
                };
            }
        }

        private static UnityBridgeResponse HandleCommand(UnityBridgeCommandRequest request)
        {
            if (request == null)
            {
                return UnityBridgeResponse.Error("BAD_REQUEST", "Missing command body.", "");
            }

            if (string.IsNullOrEmpty(request.Action))
            {
                object parseError;
                if (request.Metadata != null && request.Metadata.TryGetValue("parseError", out parseError))
                {
                    return UnityBridgeResponse.Error("BAD_JSON", parseError != null ? parseError.ToString() : "Invalid JSON body.", request.RequestId);
                }

                return UnityBridgeResponse.Error("BAD_REQUEST", "Command action is required.", request.RequestId);
            }

            if (!request.ExecutionPolicy.Confirmed)
            {
                return UnityBridgeResponse.Error("POLICY_CONFIRMATION_REQUIRED", "ExecutionPolicy.confirmed must be true for bridge commands.", request.RequestId);
            }

            switch (request.Action)
            {
                case "observe.selection":
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = new Dictionary<string, object>
                        {
                            { "selectedObject", UnitySelectionSummary.FromGameObject(UnityEditor.Selection.activeGameObject) }
                        }
                    };
                case "observe.scene_context":
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = UnityBridgeInspector.GetSceneContext()
                    };
                case "observe.prefab_context":
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = UnityBridgeInspector.GetPrefabContext()
                    };
                case "observe.object_children":
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = new Dictionary<string, object>
                        {
                            {
                                "children",
                                UnityBridgeInspector.GetObjectChildren(
                                    request.Args != null
                                        ? request.Args.ToObject<UnityObjectChildrenArgs>()
                                        : new UnityObjectChildrenArgs())
                            }
                        }
                    };
                case "observe.object_bounds":
                    {
                        UnityObjectBoundsData boundsData = UnityBridgeWorkspaceService.GetObjectBounds(
                            request.Args != null
                                ? request.Args.ToObject<UnityObjectReferenceArgs>()
                                : new UnityObjectReferenceArgs());
                        if (boundsData == null)
                        {
                            return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Requested object was not found.", request.RequestId);
                        }

                        return new UnityBridgeResponse
                        {
                            RequestId = request.RequestId,
                            Data = boundsData
                        };
                    }
                case "observe.object_transform":
                    {
                        UnityObjectTransformData transformData = UnityBridgeInspector.GetObjectTransform(
                            request.Args != null
                                ? request.Args.ToObject<UnityObjectReferenceArgs>()
                                : new UnityObjectReferenceArgs());
                        if (transformData == null)
                        {
                            return UnityBridgeResponse.Error("OBJECT_NOT_FOUND", "Requested object was not found.", request.RequestId);
                        }

                        return new UnityBridgeResponse
                        {
                            RequestId = request.RequestId,
                            Data = transformData
                        };
                    }
                case "observe.workspace_state":
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = UnityBridgeWorkspaceService.GetWorkspaceState()
                    };
                case "capture.scene_view":
                    return UnityBridgeCaptureService.CaptureSceneView(request);
                case "capture.game_view":
                    return UnityBridgeCaptureService.CaptureGameView(request);
                case "capture.camera":
                    return UnityBridgeCaptureService.CaptureNamedCamera(request);
                case "capture.orbit_view":
                    return UnityBridgeCaptureService.CaptureOrbitView(request);
                case "capture.section_view":
                    return UnityBridgeCaptureService.CaptureSectionView(request);
                case "capture.multicam_rig":
                    return UnityBridgeCaptureService.CaptureMulticamRig(request);
                case "bridge.refresh_package":
                    UnityBridgeController.QueuePackageRefresh("bridge.refresh_package");
                    return new UnityBridgeResponse
                    {
                        RequestId = request.RequestId,
                        Data = new Dictionary<string, object>
                        {
                            { "queued", true },
                            { "autoRefreshPackageOnChange", UnityBridgeController.AutoRefreshPackageOnChange },
                            { "packageWatchRoot", UnityBridgeController.PackageWatchRoot }
                        }
                    };
                case "scene.workspace_reset":
                    return UnityBridgeWorkspaceService.WorkspaceReset(request);
                case "scene.group_root_upsert":
                    return UnityBridgeWorkspaceService.GroupRootUpsert(request);
                case "scene.primitive_upsert":
                    return UnityBridgeWorkspaceService.PrimitiveUpsert(request);
                case "scene.rounded_rect_prism_upsert":
                    return UnityBridgeWorkspaceService.RoundedRectPrismUpsert(request);
                case "scene.player_screen_authoring_upsert":
                    return UnityBridgeWorkspaceService.PlayerScreenAuthoringUpsert(request);
                case "scene.particle_system_upsert":
                    return UnityBridgeWorkspaceService.ParticleSystemUpsert(request);
                case "scene.spektr_lightning_upsert":
                    return UnityBridgeWorkspaceService.SpektrLightningUpsert(request);
                case "scene.crt_glass_upsert":
                    return UnityBridgeWorkspaceService.CrtGlassUpsert(request);
                case "scene.crt_cabinet_upsert":
                    return UnityBridgeWorkspaceService.CrtCabinetUpsert(request);
                case "scene.seat_shell_upsert":
                    return UnityBridgeWorkspaceService.SeatShellUpsert(request);
                case "scene.armrest_upsert":
                    return UnityBridgeWorkspaceService.ArmrestUpsert(request);
                case "scene.object_duplicate":
                    return UnityBridgeWorkspaceService.DuplicateObject(request);
                case "scene.object_delete":
                    return UnityBridgeWorkspaceService.DeleteObject(request);
                case "scene.object_get_state":
                    return UnityBridgeWorkspaceService.GetObjectState(request);
                case "asset.texture_import_local":
                    return UnityBridgeMaterialStyleService.ImportLocalTexture(request);
                case "scene.material_style_upsert":
                    return UnityBridgeMaterialStyleService.MaterialStyleUpsert(request);
                case "asset.innerpiece.inspect_selection":
                    return UnityBridgeInnerPieceService.InspectSelection(request);
                case "asset.innerpiece.export_selection":
                    return UnityBridgeInnerPieceService.ExportSelection(request);
                case "asset.innerpiece.export_project_asset":
                    return UnityBridgeInnerPieceService.ExportProjectAsset(request);
                case "asset.innerpiece.capture_preview":
                    return UnityBridgeInnerPieceService.CapturePreview(request);
                case "asset.innerpiece.get_last_export":
                    return UnityBridgeInnerPieceService.GetLastExport(request);
                case "unity.api.invoke":
                    return UnityBridgeResponse.Error("COMMAND_DISABLED", "Action 'unity.api.invoke' is disabled in the first slice.", request.RequestId);
                default:
                    return UnityBridgeResponse.Error("COMMAND_NOT_IMPLEMENTED", "Action '" + request.Action + "' is not implemented.", request.RequestId);
            }
        }

        private static int ResolveStatusCode(string code)
        {
            switch (code)
            {
                case "NOT_FOUND":
                case "OBJECT_NOT_FOUND":
                case "CAMERA_NOT_FOUND":
                case "MULTICAM_TARGET_NOT_FOUND":
                    return 404;
                case "BAD_REQUEST":
                case "BAD_JSON":
                case "POLICY_CONFIRMATION_REQUIRED":
                case "SCENE_VIEW_CAPTURE_FAILED":
                case "GAME_VIEW_CAPTURE_FAILED":
                case "CAMERA_CAPTURE_FAILED":
                case "ORBIT_CAPTURE_FAILED":
                case "SECTION_CAPTURE_FAILED":
                case "MULTICAM_CAPTURE_FAILED":
                case "TEXTURE_IMPORT_FAILED":
                case "EXPORT_NOT_READY":
                    return 400;
                case "COMMAND_DISABLED":
                case "WORKSPACE_MUTATION_REJECTED":
                    return 403;
                case "SCENE_VIEW_UNAVAILABLE":
                case "GAME_VIEW_UNAVAILABLE":
                case "COMMAND_NOT_IMPLEMENTED":
                    return 501;
                default:
                    return 500;
            }
        }

        private static string GetReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    return "OK";
                case 400:
                    return "Bad Request";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                default:
                    return "OK";
            }
        }

        private static IPAddress ResolveAddress(string host)
        {
            if (string.IsNullOrEmpty(host) ||
                string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Loopback;
            }

            IPAddress[] addresses = Dns.GetHostAddresses(host);
            return addresses.FirstOrDefault() ?? IPAddress.Loopback;
        }
    }
}
