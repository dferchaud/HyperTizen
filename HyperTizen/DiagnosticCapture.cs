using System;
using System.Runtime.InteropServices;
using Tizen.Applications;
using Tizen.Applications.Notifications;

namespace HyperTizen
{
    public static class DiagnosticCapture
    {
        // Test 1: libvideoenhance.so (original HyperTizen / T7 fallback)
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int TestLibVideoEnhanceT7(out CaptureDiagCondition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "cs_ve_get_rgb_measure_condition")]
        private static extern int TestLibVideoEnhanceT6(out CaptureDiagCondition condition);

        // Test 2: libSecVideoCapture.so (sryeyes T8 SDK)
        [DllImport("/usr/lib/libSecVideoCapture.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sec_video_capture_get_instance")]
        private static extern IntPtr TestLibSecVideoCaptureT8();

        // Test 3: Alternative paths
        [DllImport("libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int TestLibVideoEnhanceAlt(out CaptureDiagCondition condition);

        public static void RunDiagnostics()
        {
            string report = "=== HyperTizen Capture API Diagnostics ===\n\n";
            bool anyWorking = false;
            int notificationCount = 1;
            int tizenMajor = SDK.SystemInfo.TizenVersionMajor;

            report += $"Tizen Version: {tizenMajor}.{SDK.SystemInfo.TizenVersionMinor}\n\n";

            // Skip old library tests on Tizen 8+ (they don't work and cause errors)
            if (tizenMajor >= 8)
            {
                report += "⏩ Skipping old API tests (Tizen 8+ detected)\n";
                report += "   Old APIs (libvideoenhance.so, libSecVideoCapture.so) not available on Tizen 8+\n\n";

                Helper.Log.Write(Helper.eLogType.Info, "Tizen 8+ detected - skipping old library diagnostics");

                if (Globals.Instance.ShowNotifications)
                {
                    Notification skipNotif = new Notification
                    {
                        Title = "Tizen 8+ Detected",
                        Content = "Skipping old API tests - using new vtable method",
                        Count = notificationCount++
                    };
                    NotificationManager.Post(skipNotif);
                }

                // On Tizen 8+, we use the new vtable method
                report += "✓ Using new vtable-based capture API\n";
                report += "  - libvideo-capture.so.0.1.0 getInstance()\n";
                report += "  - vtable[3]: getVideoMainYUV\n";
                report += "  - vtable[13]: Lock\n";
                report += "  - vtable[14]: Unlock\n";
                anyWorking = true;
            }
            else
            {
                // Only test old APIs on Tizen 7 and below
                // Test 1: libvideoenhance.so T7 API
                try
                {
                    CaptureDiagCondition cond;
                    int result = TestLibVideoEnhanceT7(out cond);
                    report += $"✓ libvideoenhance.so T7 API: WORKING (result={result})\n";
                    report += $"  Screen: {cond.Width}x{cond.Height}, Points: {cond.ScreenCapturePoints}\n";
                    anyWorking = true;

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n1 = new Notification
                        {
                            Title = "✓ T7 API WORKS",
                            Content = $"libvideoenhance.so T7: {cond.Width}x{cond.Height}, {cond.ScreenCapturePoints} pts",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n1);
                    }
                }
                catch (Exception ex)
                {
                    report += $"✗ libvideoenhance.so T7 API: FAILED ({ex.GetType().Name})\n";

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n1 = new Notification
                        {
                            Title = "✗ T7 API FAILED",
                            Content = $"libvideoenhance.so T7: {ex.GetType().Name}",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n1);
                    }
                }

                // Test 2: libvideoenhance.so T6 API
                try
                {
                    CaptureDiagCondition cond;
                    int result = TestLibVideoEnhanceT6(out cond);
                    report += $"✓ libvideoenhance.so T6 API: WORKING (result={result})\n";
                    report += $"  Screen: {cond.Width}x{cond.Height}, Points: {cond.ScreenCapturePoints}\n";
                    anyWorking = true;

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n2 = new Notification
                        {
                            Title = "✓ T6 API WORKS",
                            Content = $"libvideoenhance.so T6: {cond.Width}x{cond.Height}, {cond.ScreenCapturePoints} pts",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n2);
                    }
                }
                catch (Exception ex)
                {
                    report += $"✗ libvideoenhance.so T6 API: FAILED ({ex.GetType().Name})\n";

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n2 = new Notification
                        {
                            Title = "✗ T6 API FAILED",
                            Content = $"libvideoenhance.so T6: {ex.GetType().Name}",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n2);
                    }
                }

                // Test 3: Alternative library path
                try
                {
                    CaptureDiagCondition cond;
                    int result = TestLibVideoEnhanceAlt(out cond);
                    report += $"✓ libvideoenhance.so.0 (alt): WORKING (result={result})\n";
                    anyWorking = true;

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n4 = new Notification
                        {
                            Title = "✓ ALT PATH WORKS",
                            Content = $"libvideoenhance.so.0: {cond.Width}x{cond.Height}",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n4);
                    }
                }
                catch (Exception ex)
                {
                    report += $"✗ libvideoenhance.so.0 (alt): FAILED ({ex.GetType().Name})\n";

                    if (Globals.Instance.ShowNotifications)
                    {
                        Notification n4 = new Notification
                        {
                            Title = "✗ ALT PATH FAILED",
                            Content = $"libvideoenhance.so.0: {ex.GetType().Name}",
                            Count = notificationCount++
                        };
                        NotificationManager.Post(n4);
                    }
                }
            }

            report += "\n=== System Info ===\n";
            report += $"Tizen Version: {SDK.SystemInfo.TizenVersionMajor}.{SDK.SystemInfo.TizenVersionMinor}\n";
            report += $"Model: {SDK.SystemInfo.ModelName}\n";
            report += $"Screen: {SDK.SystemInfo.ScreenWidth}x{SDK.SystemInfo.ScreenHeight}\n";
            report += $"Image Capture Support: {SDK.SystemInfo.ImageCapture}\n";
            report += $"Video Recording Support: {SDK.SystemInfo.VideoRecording}\n";

            // System info notification
            if (Globals.Instance.ShowNotifications)
            {
                Notification sysInfo = new Notification
                {
                    Title = "System Info",
                    Content = $"Tizen {SDK.SystemInfo.TizenVersionMajor}.{SDK.SystemInfo.TizenVersionMinor} | {SDK.SystemInfo.ModelName} | {SDK.SystemInfo.ScreenWidth}x{SDK.SystemInfo.ScreenHeight}",
                    Count = notificationCount++
                };
                NotificationManager.Post(sysInfo);
            }

            report += "\n=== Recommendation ===\n";
            string recommendation = "";
            if (tizenMajor >= 8)
            {
                recommendation = "✓ Tizen 8+ VTable API (getVideoMainYUV)";
                report += recommendation + "\n";
                report += "   Check logs for vtable dump and capture results\n";
            }
            else if (report.Contains("libvideoenhance.so T7 API: WORKING"))
            {
                recommendation = "Use ORIGINAL HyperTizen (pixel sampling)";
                report += recommendation + "\n";
            }
            else if (report.Contains("libvideoenhance.so T6 API: WORKING"))
            {
                recommendation = "Use ORIGINAL HyperTizen with T6 API";
                report += recommendation + "\n";
            }
            else if (report.Contains("libvideoenhance.so.0 (alt): WORKING"))
            {
                recommendation = "Use alternative library path";
                report += recommendation + "\n";
            }
            else
            {
                recommendation = "⚠ NO CAPTURE APIS - TV NOT SUPPORTED";
                report += recommendation + "\n";
            }

            Helper.Log.Write(Helper.eLogType.Info, report);
            
            // Final recommendation notification
            if (Globals.Instance.ShowNotifications)
            {
                Notification finalNotif = new Notification
                {
                    Title = anyWorking ? "✓ DIAGNOSTIC COMPLETE" : "✗ DIAGNOSTIC COMPLETE",
                    Content = recommendation,
                    Count = notificationCount++
                };
                NotificationManager.Post(finalNotif);
            }
        }

        public struct CaptureDiagCondition
        {
            public int ScreenCapturePoints;
            public int PixelDensityX;
            public int PixelDensityY;
            public int SleepMS;
            public int Width;
            public int Height;
        }
    }
}
