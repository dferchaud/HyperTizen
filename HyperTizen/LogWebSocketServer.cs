using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HyperTizen
{
    /// <summary>
    /// WebSocket server for streaming logs to web browsers
    /// Implements RFC 6455 WebSocket protocol
    /// </summary>
    public class LogWebSocketServer
    {
        private TcpListener listener;
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private readonly object clientLock = new object();
        private bool isRunning = false;
        private Thread listenerThread;
        private int port;

        // Event handler for new log messages
        public event Action<string> OnLogMessage;

        public LogWebSocketServer(int port = 8765)
        {
            this.port = port;
        }

        public void Start()
        {
            if (isRunning)
            {
                Helper.Log.Write(Helper.eLogType.Warning, "LogWebSocketServer already running");
                return;
            }

            try
            {
                listener = new TcpListener(IPAddress.Any, port);

                // Set socket options to allow address reuse
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                listener.Start();
                isRunning = true;

                Helper.Log.Write(Helper.eLogType.Info, $"WebSocket log server started on port {port}");
                Helper.Log.Write(Helper.eLogType.Info, $"Access at http://<TV_IP>:{port} (see logs.html)");

                // Start accepting clients in background thread
                listenerThread = new Thread(AcceptClientsLoop);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                // Send recent logs to new clients
                var recentLogs = Helper.Log.GetRecentLogs();
                foreach (var log in recentLogs)
                {
                    BroadcastLog(log);
                }
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"Failed to start WebSocket log server on port {port}: {ex.Message}");

                // Show TV notification so user can see the error even without logs
                try
                {
                    var notif = new Tizen.Applications.Notifications.Notification
                    {
                        Title = "WebSocket Log Server Error",
                        Content = $"Port {port} failed: {ex.Message}",
                        Count = 1
                    };
                    Tizen.Applications.Notifications.NotificationManager.Post(notif);
                }
                catch { /* Ignore notification errors */ }
            }
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            isRunning = false;

            lock (clientLock)
            {
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        client.Close();
                    }
                    catch { }
                }
                clients.Clear();
            }

            try
            {
                listener?.Stop();
            }
            catch { }

            Helper.Log.Write(Helper.eLogType.Info, "WebSocket log server stopped");
        }

        private void AcceptClientsLoop()
        {
            Helper.Log.Write(Helper.eLogType.Debug, "WebSocket: Accept loop started");

            while (isRunning)
            {
                try
                {
                    // Use a short timeout to check isRunning periodically
                    if (listener.Server.Poll(100000, SelectMode.SelectRead)) // 100ms in microseconds
                    {
                        if (!isRunning) break;

                        var client = listener.AcceptTcpClient();
                        Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Accepted TCP connection");

                        Task.Run(() => HandleClient(client));
                    }
                }
                catch (SocketException ex)
                {
                    if (isRunning)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, $"WebSocket accept socket error: {ex.ErrorCode} - {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, $"WebSocket accept error: {ex.GetType().Name} - {ex.Message}");
                    }
                }
            }

            Helper.Log.Write(Helper.eLogType.Debug, "WebSocket: Accept loop stopped");
        }

        private async void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            bool isWebSocket = false;
            string clientEndpoint = "unknown";

            try
            {
                clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: New connection from {clientEndpoint}");

                stream = client.GetStream();
                stream.ReadTimeout = 5000; // 5 second timeout for handshake
                stream.WriteTimeout = 5000;

                // Read HTTP handshake
                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Helper.Log.Write(Helper.eLogType.Warning, $"WebSocket: No data received from {clientEndpoint}");
                    return;
                }

                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Received {bytesRead} bytes from {clientEndpoint}");

                if (request.StartsWith("GET"))
                {
                    // WebSocket handshake
                    if (request.Contains("Upgrade: websocket"))
                    {
                        Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Handshake request from {clientEndpoint}");

                        string key = ExtractWebSocketKey(request);
                        if (string.IsNullOrEmpty(key))
                        {
                            Helper.Log.Write(Helper.eLogType.Error, $"WebSocket: No Sec-WebSocket-Key from {clientEndpoint}");
                            return;
                        }

                        Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Key={key}");

                        string response = BuildWebSocketHandshakeResponse(key);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                        Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Sending handshake response ({responseBytes.Length} bytes)");

                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        await stream.FlushAsync();

                        Helper.Log.Write(Helper.eLogType.Info, $"WebSocket client connected from {clientEndpoint}");

                        isWebSocket = true;
                        lock (clientLock)
                        {
                            clients.Add(client);
                        }

                        // Send all recent logs to new client
                        var recentLogs = Helper.Log.GetRecentLogs();
                        Helper.Log.Write(Helper.eLogType.Debug, $"WebSocket: Sending {recentLogs.Count} recent logs to {clientEndpoint}");

                        foreach (var log in recentLogs)
                        {
                            await SendWebSocketMessage(stream, log);
                        }

                        // Keep connection alive and wait for close
                        await WaitForClientClose(stream);
                    }
                    else
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, $"WebSocket: No 'Upgrade: websocket' header from {clientEndpoint}");
                    }
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Warning, $"WebSocket: Not a GET request from {clientEndpoint}");
                }
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"WebSocket client error from {clientEndpoint}: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Helper.Log.Write(Helper.eLogType.Error, $"  Inner: {ex.InnerException.Message}");
                }
            }
            finally
            {
                if (isWebSocket)
                {
                    lock (clientLock)
                    {
                        clients.Remove(client);
                    }
                    Helper.Log.Write(Helper.eLogType.Info, "WebSocket client disconnected");
                }

                stream?.Close();
                client?.Close();
            }
        }

        private async Task WaitForClientClose(NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            while (isRunning)
            {
                try
                {
                    // Try to read - if client closes, this will throw or return 0
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    // Check for close frame (opcode 0x8)
                    if (buffer[0] == 0x88)
                        break;
                }
                catch
                {
                    break;
                }
            }
        }

        private string ExtractWebSocketKey(string request)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:"))
                {
                    return line.Substring("Sec-WebSocket-Key:".Length).Trim();
                }
            }
            return null;
        }

        private string BuildWebSocketHandshakeResponse(string key)
        {
            const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            string acceptKey = Convert.ToBase64String(
                SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + guid))
            );

            return "HTTP/1.1 101 Switching Protocols\r\n" +
                   "Upgrade: websocket\r\n" +
                   "Connection: Upgrade\r\n" +
                   $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                   "\r\n";
        }

        public void BroadcastLog(string logMessage)
        {
            if (!isRunning)
                return;

            List<TcpClient> disconnectedClients = new List<TcpClient>();

            lock (clientLock)
            {
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            SendWebSocketMessage(stream, logMessage).Wait();
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch
                    {
                        disconnectedClients.Add(client);
                    }
                }

                // Remove disconnected clients
                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                    try { client.Close(); } catch { }
                }
            }
        }

        private async Task SendWebSocketMessage(NetworkStream stream, string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] frame = EncodeWebSocketFrame(messageBytes);
            await stream.WriteAsync(frame, 0, frame.Length);
            await stream.FlushAsync();
        }

        private byte[] EncodeWebSocketFrame(byte[] payload)
        {
            int payloadLength = payload.Length;
            byte[] frame;
            int frameIndex = 0;

            // Calculate frame size
            if (payloadLength <= 125)
            {
                frame = new byte[2 + payloadLength];
                frame[1] = (byte)payloadLength;
                frameIndex = 2;
            }
            else if (payloadLength <= 65535)
            {
                frame = new byte[4 + payloadLength];
                frame[1] = 126;
                frame[2] = (byte)((payloadLength >> 8) & 0xFF);
                frame[3] = (byte)(payloadLength & 0xFF);
                frameIndex = 4;
            }
            else
            {
                frame = new byte[10 + payloadLength];
                frame[1] = 127;
                for (int i = 0; i < 8; i++)
                {
                    frame[2 + i] = (byte)((payloadLength >> (56 - 8 * i)) & 0xFF);
                }
                frameIndex = 10;
            }

            // Set FIN bit and text opcode (0x1)
            frame[0] = 0x81;

            // Copy payload
            Array.Copy(payload, 0, frame, frameIndex, payloadLength);

            return frame;
        }

        public int GetConnectedClientCount()
        {
            lock (clientLock)
            {
                return clients.Count;
            }
        }

        public bool IsRunning()
        {
            return isRunning;
        }

        public int GetPort()
        {
            return port;
        }
    }
}
