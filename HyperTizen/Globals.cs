using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tizen.Applications;

namespace HyperTizen
{
    public sealed class Globals
    {
        // FILESTEALER MODE: When enabled, the app will copy Tizen OS files to USB and exit
        // This is for research purposes on Tizen 9+ to understand OS structure
        // WARNING: Set to false for normal capture operation!
        public const bool FILESTEALER_ENABLED = false;

        private static readonly Globals instance = new Globals();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Globals()
        {
        }

        private Globals()
        {
        }

        public static Globals Instance
        {
            get
            {
                return instance;
            }
        }
        public void SetConfig()
        {
            // First, try to use the stored rpcServer preference (user's selection from UI)
            if (Preference.Contains("rpcServer"))
            {
                string rpcServer = Preference.Get<string>("rpcServer");
                if (!string.IsNullOrEmpty(rpcServer))
                {
                    // Parse ws://ip:port format to get the IP
                    try
                    {
                        var uri = new Uri(rpcServer.Replace("ws://", "http://").Replace("wss://", "https://"));
                        ServerIp = uri.Host;

                        // IMPORTANT: Don't use the WebSocket port (8090)!
                        // The saved rpcServer is for WebSocket/JSON API, but we need the FlatBuffers port.
                        // Try SSDP to get the correct FlatBuffers port for this IP
                        Helper.Log.Write(Helper.eLogType.Info,
                            $"Config: Saved server IP {ServerIp} from preferences, discovering FlatBuffers port...");

                        (string ssdpIp, int fbsPort) = Helper.SsdpDiscovery.GetHyperIpAndPort();

                        // If SSDP found the same server, use its FlatBuffers port
                        if (!string.IsNullOrEmpty(ssdpIp) && ssdpIp == ServerIp && fbsPort > 0)
                        {
                            ServerPort = fbsPort;
                            Helper.Log.Write(Helper.eLogType.Info,
                                $"Config: Using FlatBuffers port {ServerPort} from SSDP for {ServerIp}");
                        }
                        else
                        {
                            // Fallback to default FlatBuffers port
                            ServerPort = 19400;
                            Helper.Log.Write(Helper.eLogType.Warning,
                                $"Config: SSDP didn't find server, using default FlatBuffers port {ServerPort}");
                        }

                        // Load enabled preference
                        LoadEnabledPreference();

                        Width = 3840/8;
                        Height = 2160/8;
                        return; // Successfully loaded from preferences
                    }
                    catch (Exception ex)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning,
                            $"Failed to parse saved rpcServer '{rpcServer}': {ex.Message}. Falling back to SSDP discovery.");
                    }
                }
            }

            // Fallback to SSDP discovery if no saved preference or parsing failed
            Helper.Log.Write(Helper.eLogType.Info, "SSDP: Starting discovery...");

            (string ip, int port) = Helper.SsdpDiscovery.GetHyperIpAndPort();

            if (string.IsNullOrEmpty(ip) || port <= 0)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    "SSDP FAILED: No HyperHDR found. Check network!");
                // Set defaults or leave as null for validation later
                ServerIp = null;
                ServerPort = 0;
            }
            else
            {
                Helper.Log.Write(Helper.eLogType.Info,
                    $"SSDP OK: Found {ip}:{port}");
                ServerIp = ip;
                ServerPort = port;
            }

            // Load enabled preference
            LoadEnabledPreference();

            Width = 3840/8;
            Height = 2160/8;
        }

        private void LoadEnabledPreference()
        {
            // Load enabled setting from preferences, defaulting to true if not set
            string enabledPref = Preference.Contains("enabled") ? Preference.Get<string>("enabled") : "true";
            Enabled = bool.TryParse(enabledPref, out bool enabledResult) && enabledResult;
            Helper.Log.Write(Helper.eLogType.Info, $"Loaded enabled setting from preferences: {Enabled}");

            // Load diagnostic mode setting from preferences, defaulting to FALSE for normal operation
            string diagnosticPref = Preference.Contains("diagnosticMode") ? Preference.Get<string>("diagnosticMode") : "false";
            DiagnosticMode = bool.TryParse(diagnosticPref, out bool diagnosticResult) && diagnosticResult;
            Helper.Log.Write(Helper.eLogType.Info, $"Loaded diagnosticMode setting from preferences: {DiagnosticMode}");
        }

        public void LoadPreferencesEarly()
        {
            // Load enabled and diagnosticMode BEFORE any testing
            string enabledPref = Preference.Contains("enabled") ? Preference.Get<string>("enabled") : "true";
            Enabled = bool.TryParse(enabledPref, out bool enabledResult) && enabledResult;

            string diagnosticPref = Preference.Contains("diagnosticMode") ? Preference.Get<string>("diagnosticMode") : "false";
            DiagnosticMode = bool.TryParse(diagnosticPref, out bool diagnosticResult) && diagnosticResult;

            Helper.Log.Write(Helper.eLogType.Info, $"Preferences loaded early - DiagnosticMode: {DiagnosticMode}, Enabled: {Enabled}");
        }

        public string ServerIp; //IP of hyperhdr server
        public int ServerPort; //Port of hyperhdr server
        public int Width; //Capture Width
        public int Height; //Capture Height
        public bool Enabled; //Is the service enabled
        public bool DiagnosticMode; //Enable diagnostic mode (pauses for 10 minutes after initialization)
        public bool ShowNotifications = false; //Show notification popups on TV (default: false, use new UI instead)
    }
}
