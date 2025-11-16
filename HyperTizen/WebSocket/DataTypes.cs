using System.Collections.Generic;

namespace HyperTizen.WebSocket.DataTypes
{
    public enum Event
    {
        SetConfig,
        ReadConfig,
        ReadConfigResult,
        ScanSSDP,
        SSDPScanResult,
        GetLogs,
        LogsResult,
        StatusUpdate,
        PauseCapture,
        ResumeCapture,
        GetStatus,
        StatusResult,
        RestartService
    }

    public class BasicEvent
    {
        public Event Event { get; set; }
    }

    public class SetConfigEvent : BasicEvent
    {
        public string key { get; set; }
        public string value { get; set; }
    }

    public class ReadConfigEvent : BasicEvent
    {
        public string key { get; set; }
    }

    public class ReadConfigResultEvent : BasicEvent
    {
        public ReadConfigResultEvent(bool error, string key, object value)
        {
            this.Event = Event.ReadConfigResult;
            this.error = error;
            this.value = value;
            this.key = key;
        }

        public bool error { get; set; }
        public string key { get; set; }
        public object value { get; set; }
    }

    public class SSDPScanResultEvent : BasicEvent
    {
        public SSDPScanResultEvent(List<SSDPDevice> devices)
        {
            this.devices = devices;
            this.Event = Event.SSDPScanResult;
        }
        public List<SSDPDevice> devices { get; set; }
        public class SSDPDevice
        {
            public string FriendlyName { get; set; }
            public string UrlBase { get; set; }

            public SSDPDevice(string friendlyName, string urlBase)
            {
                FriendlyName = friendlyName;
                UrlBase = urlBase;
            }
        }
    }

    public class LogsResultEvent : BasicEvent
    {
        public LogsResultEvent(List<string> logs, string logPath)
        {
            this.logs = logs;
            this.logPath = logPath;
            this.Event = Event.LogsResult;
        }
        public List<string> logs { get; set; }
        public string logPath { get; set; }
    }

    public class StatusUpdateEvent : BasicEvent
    {
        public StatusUpdateEvent(string status, string message)
        {
            this.status = status;
            this.message = message;
            this.Event = Event.StatusUpdate;
        }
        public string status { get; set; }
        public string message { get; set; }
    }

    public class StatusResultEvent : BasicEvent
    {
        public StatusResultEvent(string state, long framesCaptured, double averageFPS,
            int errorCount, bool isConnected, string lastError, string uptime, string activeServerUrl = null)
        {
            this.Event = Event.StatusResult;
            this.state = state;
            this.framesCaptured = framesCaptured;
            this.averageFPS = averageFPS;
            this.errorCount = errorCount;
            this.isConnected = isConnected;
            this.lastError = lastError;
            this.uptime = uptime;
            this.activeServerUrl = activeServerUrl;
        }

        public string state { get; set; }
        public long framesCaptured { get; set; }
        public double averageFPS { get; set; }
        public int errorCount { get; set; }
        public bool isConnected { get; set; }
        public string lastError { get; set; }
        public string uptime { get; set; }
        public string activeServerUrl { get; set; }
    }

    public class ImageCommand
    {
        public ImageCommand(string image)
        {
            imagedata = image;
        }

        public string command { get; set; } = "image";
        public string imagedata { get; set; }
        public string name { get; set; } = "HyperTizen Data";
        public string format { get; set; } = "auto";
        public byte priority { get; set; } = 99;
        public string origin { get; set; } = "HyperTizen";
    }
}
