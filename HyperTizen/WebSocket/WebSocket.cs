using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HyperTizen.WebSocket.DataTypes;
using Rssdp;
using Tizen.Applications;
using static HyperTizen.WebSocket.DataTypes.SSDPScanResultEvent;

namespace HyperTizen.WebSocket
{
    public class WSServer
    {
        private HttpListener _httpListener;
        private List<System.Net.WebSockets.WebSocket> _connectedClients = new List<System.Net.WebSockets.WebSocket>();
        private readonly object _clientsLock = new object();
        private List<string> usnList = new List<string>()
        {
            "urn:hyperion-project.org:device:basic:1",
            "urn:hyperhdr.eu:device:basic:1"
        };

        public WSServer(string uriPrefix)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(uriPrefix);
        }

        public async Task StartAsync()
        {
            try
            {
                Helper.Log.Write(Helper.eLogType.Info,
                    "Starting HttpListener on http://*:45677/");

                _httpListener.Start();

                Helper.Log.Write(Helper.eLogType.Info,
                    "Control WebSocket server started successfully on port 45677");

                while (true)
                {
                    var httpContext = await _httpListener.GetContextAsync();
                    if (httpContext.Request.IsWebSocketRequest)
                    {
                        var wsContext = await httpContext.AcceptWebSocketAsync(null);
                        _ = HandleWebSocketAsync(wsContext.WebSocket);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = 400;
                        httpContext.Response.Close();
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"HttpListener failed to start on port 45677: ErrorCode={ex.ErrorCode}, Message={ex.Message}");
                Helper.Log.Write(Helper.eLogType.Error,
                    $"This may be a Tizen 9 permission issue. Check manifest privileges.");

                // Show TV notification so user can see the error even without logs
                try
                {
                    var notif = new Tizen.Applications.Notifications.Notification
                    {
                        Title = "WebSocket Error",
                        Content = $"Control server failed: Code {ex.ErrorCode}",
                        Count = 1
                    };
                    Tizen.Applications.Notifications.NotificationManager.Post(notif);
                }
                catch { /* Ignore notification errors */ }
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"Control WebSocket server failed to start: {ex.GetType().Name} - {ex.Message}");
                Helper.Log.Write(Helper.eLogType.Debug, $"StackTrace: {ex.StackTrace}");

                // Show TV notification so user can see the error even without logs
                try
                {
                    var notif = new Tizen.Applications.Notifications.Notification
                    {
                        Title = "WebSocket Error",
                        Content = $"Control server: {ex.Message}",
                        Count = 1
                    };
                    Tizen.Applications.Notifications.NotificationManager.Post(notif);
                }
                catch { /* Ignore notification errors */ }
            }
        }

        private async Task HandleWebSocketAsync(System.Net.WebSockets.WebSocket webSocket)
        {
            // Add client to connected list
            lock (_clientsLock)
            {
                _connectedClients.Add(webSocket);
            }

            // Send initial status to newly connected client
            try
            {
                bool isCapturing = App.Configuration.Enabled;
                string initialStatus = isCapturing ? "capturing" : "stopped";
                string initialMessage = isCapturing ? "Currently capturing" : "Capture stopped";

                string statusEvent = JsonConvert.SerializeObject(new StatusUpdateEvent(initialStatus, initialMessage));
                await SendAsync(webSocket, statusEvent);

                Helper.Log.Write(Helper.eLogType.Debug,
                    $"Sent initial status to client: {initialStatus}");
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Warning,
                    $"Failed to send initial status to client: {ex.Message}");
            }

            try
            {
                var buffer = new byte[1024 * 4];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                while (result.MessageType != WebSocketMessageType.Close)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await OnMessageAsync(webSocket, message);
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
            finally
            {
                // Remove client from connected list
                lock (_clientsLock)
                {
                    _connectedClients.Remove(webSocket);
                }
            }
        }

        protected async Task OnMessageAsync(System.Net.WebSockets.WebSocket webSocket, string message)
        {
            try
            {
                BasicEvent data = JsonConvert.DeserializeObject<BasicEvent>(message);

                switch (data.Event)
                {
                    case Event.ScanSSDP:
                        {
                            var devices = await ScanSSDPAsync();
                            string resultEvent = JsonConvert.SerializeObject(new SSDPScanResultEvent(devices));
                            await SendAsync(webSocket, resultEvent);
                            break;
                        }

                    case Event.GetLogs:
                        {
                            var logs = Helper.Log.GetRecentLogs();
                            string logPath = Helper.Log.GetWorkingLogPath();
                            string resultEvent = JsonConvert.SerializeObject(new LogsResultEvent(logs, logPath));
                            await SendAsync(webSocket, resultEvent);
                            break;
                        }

                    case Event.ReadConfig:
                        {
                            ReadConfigEvent readConfigEvent = JsonConvert.DeserializeObject<ReadConfigEvent>(message);
                            string result = await ReadConfigAsync(readConfigEvent);
                            await SendAsync(webSocket, result);
                            break;
                        }

                    case Event.SetConfig:
                        {
                            SetConfigEvent setConfigEvent = JsonConvert.DeserializeObject<SetConfigEvent>(message);
                            await SetConfiguration(setConfigEvent);
                            break;
                        }

                    case Event.PauseCapture:
                        {
                            Helper.Log.Write(Helper.eLogType.Debug, "Pause capture requested via WebSocket");
                            if (App.client != null)
                            {
                                App.client.Pause();
                                await BroadcastStatusUpdate("paused", "Capture paused");
                            }
                            else
                            {
                                Helper.Log.Write(Helper.eLogType.Warning, "Cannot pause: client not initialized");
                            }
                            break;
                        }

                    case Event.ResumeCapture:
                        {
                            Helper.Log.Write(Helper.eLogType.Debug, "Resume capture requested via WebSocket");
                            if (App.client != null)
                            {
                                App.client.Resume();
                                await BroadcastStatusUpdate("capturing", "Capture resumed");
                            }
                            else
                            {
                                Helper.Log.Write(Helper.eLogType.Warning, "Cannot resume: client not initialized");
                            }
                            break;
                        }

                    case Event.GetStatus:
                        {
                            Helper.Log.Write(Helper.eLogType.Debug, "Status requested via WebSocket");

                            // Check if client is initialized yet (may be null during startup)
                            if (App.client == null)
                            {
                                Helper.Log.Write(Helper.eLogType.Debug, "Client not yet initialized, returning stopped status");
                                string stoppedEvent = JsonConvert.SerializeObject(new StatusResultEvent(
                                    "Idle",
                                    0,
                                    0.0,
                                    0,
                                    false,
                                    "Service initializing...",
                                    "N/A",
                                    null));
                                await SendAsync(webSocket, stoppedEvent);
                                break;
                            }

                            var status = App.client.GetStatus();

                            string uptime = "N/A";
                            if (status.StartTime != default(DateTime))
                            {
                                var elapsed = DateTime.Now - status.StartTime;
                                uptime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                            }

                            // Build active server URL from current configuration
                            string activeServerUrl = null;
                            if (!string.IsNullOrEmpty(Globals.Instance.ServerIp) && Globals.Instance.ServerPort > 0)
                            {
                                activeServerUrl = $"ws://{Globals.Instance.ServerIp}:{Globals.Instance.ServerPort}";
                            }

                            string resultEvent = JsonConvert.SerializeObject(new StatusResultEvent(
                                status.State.ToString(),
                                status.FramesCaptured,
                                Math.Round(status.AverageFPS, 2),
                                status.ErrorCount,
                                status.IsConnected,
                                status.LastError ?? "None",
                                uptime,
                                activeServerUrl
                            ));
                            await SendAsync(webSocket, resultEvent);
                            break;
                        }

                    case Event.RestartService:
                        {
                            Helper.Log.Write(Helper.eLogType.Info, "Service restart requested via WebSocket");
                            if (App.client == null)
                            {
                                Helper.Log.Write(Helper.eLogType.Warning, "Cannot restart: client not initialized");
                                break;
                            }
                            try
                            {
                                // Stop the current capture
                                await App.client.Stop();
                                await BroadcastStatusUpdate("stopped", "Service restarting...");

                                // Wait a moment for cleanup
                                await System.Threading.Tasks.Task.Delay(500);

                                // Start again
                                System.Threading.Tasks.Task.Run(() => App.client.Start());
                                await BroadcastStatusUpdate("starting", "Service restarted");

                                Helper.Log.Write(Helper.eLogType.Info, "Service restarted successfully");
                            }
                            catch (Exception ex)
                            {
                                Helper.Log.Write(Helper.eLogType.Error,
                                    $"Failed to restart service: {ex.Message}");
                                await SendAsync(webSocket, JsonConvert.SerializeObject(
                                    new StatusUpdateEvent("error", $"Restart failed: {ex.Message}")
                                ));
                            }
                            break;
                        }
                }
            }
            catch (JsonException jsonEx)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"Invalid JSON received from WebSocket client: {jsonEx.Message}");
                Helper.Log.Write(Helper.eLogType.Debug, $"Message: {message}");
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"Error processing WebSocket message: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task<List<SSDPDevice>> ScanSSDPAsync()
        {
            var devices = new List<SSDPDevice>();
            using (var deviceLocator = new SsdpDeviceLocator())
            {
                var foundDevices = await deviceLocator.SearchAsync();
                foreach (var foundDevice in foundDevices)
                {
                    if (!usnList.Contains(foundDevice.NotificationType)) continue;

                    var fullDevice = await foundDevice.GetDeviceInfo();
                    Uri descLocation = foundDevice.DescriptionLocation;
                    devices.Add(new SSDPDevice(fullDevice.FriendlyName, descLocation.OriginalString.Replace(descLocation.PathAndQuery, "")));
                }
            }
            return devices;
        }

        private async Task<string> ReadConfigAsync(ReadConfigEvent readConfigEvent)
        {
            string result;
            if (!Preference.Contains(readConfigEvent.key))
            {
                result = JsonConvert.SerializeObject(new ReadConfigResultEvent(true, readConfigEvent.key, "Key doesn't exist."));
            }
            else
            {
                string value = Preference.Get<string>(readConfigEvent.key);
                result = JsonConvert.SerializeObject(new ReadConfigResultEvent(false, readConfigEvent.key, value));
            }
            return result;
        }

        private async Task SendAsync(System.Net.WebSockets.WebSocket webSocket, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task BroadcastStatusUpdate(string status, string message)
        {
            string statusEvent = JsonConvert.SerializeObject(new StatusUpdateEvent(status, message));
            var buffer = Encoding.UTF8.GetBytes(statusEvent);

            List<System.Net.WebSockets.WebSocket> clients;
            lock (_clientsLock)
            {
                clients = new List<System.Net.WebSockets.WebSocket>(_connectedClients);
            }

            foreach (var client in clients)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Helper.Log.Write(Helper.eLogType.Warning, $"Failed to send status update to client: {ex.Message}");
                }
            }
        }

        async Task SetConfiguration(SetConfigEvent setConfigEvent)
        {
            switch (setConfigEvent.key)
            {
                case "rpcServer":
                    {
                        App.Configuration.RPCServer = setConfigEvent.value;
                        Helper.Log.Write(Helper.eLogType.Info, $"RPC server set to: {setConfigEvent.value}");
                        //App.client.UpdateURI(setConfigEvent.value);
                        break;
                    }
                case "enabled":
                    {
                        // Validate input to prevent crashes
                        if (!bool.TryParse(setConfigEvent.value, out bool value))
                        {
                            Helper.Log.Write(Helper.eLogType.Error,
                                $"Invalid boolean value for 'enabled': {setConfigEvent.value}");
                            return;
                        }

                        // Synchronize state changes with lock to prevent race conditions
                        bool stateChanged = false;
                        bool newState = false;

                        lock (_clientsLock)
                        {
                            bool wasEnabled = App.Configuration.Enabled;
                            App.Configuration.Enabled = value;
                            Globals.Instance.Enabled = value;

                            if (wasEnabled != value)
                            {
                                stateChanged = true;
                                newState = value;
                                Helper.Log.Write(Helper.eLogType.Info,
                                    $"Capture state changed: {wasEnabled} -> {value}");
                            }
                        }

                        // Perform state-dependent actions outside the lock
                        if (stateChanged)
                        {
                            if (App.client == null)
                            {
                                Helper.Log.Write(Helper.eLogType.Warning,
                                    "Cannot change capture state: client not initialized");
                                break;
                            }

                            if (newState)
                            {
                                Helper.Log.Write(Helper.eLogType.Info, "Starting screen capture");
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await App.client.Start();
                                    }
                                    catch (Exception ex)
                                    {
                                        Helper.Log.Write(Helper.eLogType.Error,
                                            $"Unhandled exception in Start(): {ex.Message}");
                                    }
                                });
                            }
                            else
                            {
                                Helper.Log.Write(Helper.eLogType.Info, "Stopping screen capture");
                                await App.client.Stop();
                            }

                            // Broadcast status update with error handling
                            try
                            {
                                if (newState)
                                {
                                    await BroadcastStatusUpdate("capturing", "Screen capture started");
                                }
                                else
                                {
                                    await BroadcastStatusUpdate("stopped", "Screen capture stopped");
                                }
                            }
                            catch (Exception ex)
                            {
                                Helper.Log.Write(Helper.eLogType.Warning,
                                    $"Failed to broadcast status update: {ex.Message}");
                            }
                        }
                        break;
                    }
            }

            Preference.Set(setConfigEvent.key, setConfigEvent.value);
        }
    }

    public static class WebSocketServer
    {
        public static async Task StartServerAsync()
        {
            // Use http://*: instead of http://+: for better Tizen 9 compatibility
            var wsServer = new WSServer("http://*:45677/");
            await wsServer.StartAsync();
        }
    }

    public class WebSocketClient
    {
        private string uri;
        public ClientWebSocket client;
        private byte errorTimes = 0;

        public WebSocketClient(string uri)
        {
            this.uri = uri;
            client = new ClientWebSocket();
        }

        public async Task ConnectAsync()
        {
            await client.ConnectAsync(new Uri(uri), CancellationToken.None);
            _ = ReceiveMessagesAsync();
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 4];
            while (client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessage(message);
                }
            }
        }

        private void OnMessage(string message)
        {

        }
    }
}