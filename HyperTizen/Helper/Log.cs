using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tizen.Applications.Notifications;

namespace HyperTizen.Helper
{
    public enum eLogType
    {
        Debug,
        Info,
        Warning,
        Error,
        Performance
    }
    public static class Log
    {
        // Store last 1000 log messages for WebSocket/UI access (increased from 50)
        private static readonly Queue<string> recentLogs = new Queue<string>(1000);
        private static readonly object logLock = new object();

        // WebSocket server instance
        private static LogWebSocketServer webSocketServer;

        public static void StartWebSocketServer(int port = 45678)
        {
            if (webSocketServer == null)
            {
                webSocketServer = new LogWebSocketServer(port);
                webSocketServer.Start();
            }
        }

        public static void StopWebSocketServer()
        {
            webSocketServer?.Stop();
            webSocketServer = null;
        }

        public static string GetWebSocketDiagnostics()
        {
            if (webSocketServer == null)
            {
                return "WebSocket: NOT STARTED";
            }

            bool running = webSocketServer.IsRunning();
            int port = webSocketServer.GetPort();
            int clients = webSocketServer.GetConnectedClientCount();

            return $"WebSocket: {(running ? "RUNNING" : "STOPPED")} on port {port}, {clients} client(s) connected";
        }

        public static List<string> GetRecentLogs()
        {
            lock (logLock)
            {
                return new List<string>(recentLogs);
            }
        }
        
        public static string GetWorkingLogPath()
        {
            return "Logs via WebSocket only";
        }
        
        public static void Write(eLogType type, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logMessage = $"[{timestamp}] [{type}] {message}";

            // Store in recent logs for WebSocket access
            lock (logLock)
            {
                if (recentLogs.Count >= 1000)
                    recentLogs.Dequeue();
                recentLogs.Enqueue(logMessage);
            }

            // Broadcast to WebSocket clients
            try
            {
                webSocketServer?.BroadcastLog(logMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebSocket broadcast error: {ex.Message}");
            }

            // Always write to Debug output
            Debug.WriteLine(logMessage);

            // Show notification ONLY for important short messages
            if (Globals.Instance.ShowNotifications &&
                (type == eLogType.Error || type == eLogType.Warning || type == eLogType.Info) &&
                message.Length < 100 &&
                !message.Contains("Command:"))
            {
                try
                {
                    ShowToast(message);
                }
                catch
                {
                    // Silently fail
                }
            }
        }
        
        private static void ShowToast(string message)
        {
            try
            {
                Notification notification = new Notification
                {
                    Title = "HyperTizen",
                    Content = message,
                    Count = 1
                };
                NotificationManager.Post(notification);
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
