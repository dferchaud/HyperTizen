using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Tizen.System;

namespace HyperTizen.Capture
{
    /// <summary>
    /// Pixel sampling capture method using libvideoenhance.so
    /// Samples individual pixels from screen edges for ambient lighting
    /// Adapted from original HyperTizen Capturer.cs to use NV12/FlatBuffers format
    /// </summary>
    public class PixelSamplingCaptureMethod : ICaptureMethod
    {
        private bool _isInitialized = false;
        private Condition _condition;
        private bool _usingTizen7Api = false; // Track which API variant works

        // Default 16-point sampling grid (normalized coordinates 0.0-1.0)
        // Maps to screen edges for ambient lighting effect
        private CapturePoint[] _capturedPoints = new CapturePoint[] {
            // Top edge (4 points)
            new CapturePoint(0.21, 0.05),
            new CapturePoint(0.45, 0.05),
            new CapturePoint(0.7, 0.05),
            // Right edge (4 points)
            new CapturePoint(0.93, 0.07),
            new CapturePoint(0.95, 0.275),
            new CapturePoint(0.95, 0.5),
            new CapturePoint(0.95, 0.8),
            // Bottom edge (4 points)
            new CapturePoint(0.79, 0.95),
            new CapturePoint(0.65, 0.95),
            new CapturePoint(0.35, 0.95),
            new CapturePoint(0.15, 0.95),
            // Left edge (3 points)
            new CapturePoint(0.05, 0.725),
            new CapturePoint(0.05, 0.4),
            new CapturePoint(0.05, 0.2),
            // Center points (2 points)
            new CapturePoint(0.35, 0.5),
            new CapturePoint(0.65, 0.5)
        };

        public string Name => "Pixel Sampling";
        public CaptureMethodType Type => CaptureMethodType.PixelSampling;

        #region P/Invoke Declarations

        // dlopen/dlsym for dynamic symbol enumeration
        // Using "libdl.so.2" which is standard on Linux systems
        // Falls back to trying libdl.so.2 or libc if not found
        private const int RTLD_NOW = 2;
        private const int RTLD_LAZY = 1;

        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();

        // Tizen 6 API (cs_ve_* prefix)
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition(out Condition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition(int index, int x, int y);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel(int index, out Color color);

        // Tizen 7+ API (ve_* prefix)
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition7(out Condition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_set_rgb_measure_position")]
        private static extern int MeasurePosition7(int index, int x, int y);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel7(int index, out Color color);

        // Tizen 9+ API variants (potential naming patterns to test)
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition9A(out Condition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition9A(int index, int x, int y);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel9A(int index, out Color color);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition9B(out Condition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition9B(int index, int x, int y);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel9B(int index, out Color color);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_condition")]
        private static extern int MeasureCondition9C(out Condition condition);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "set_rgb_measure_position")]
        private static extern int MeasurePosition9C(int index, int x, int y);

        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_pixel")]
        private static extern int MeasurePixel9C(int index, out Color color);

        #endregion

        #region Native Structs

        /// <summary>
        /// Color struct for 10-bit RGB values (0-1023)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Color
        {
            public int R;
            public int G;
            public int B;
        }

        /// <summary>
        /// Condition struct containing screen parameters and sampling configuration
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Condition
        {
            public int ScreenCapturePoints;  // Max number of points that can be sampled simultaneously
            public int PixelDensityX;         // Pixel density in X direction
            public int PixelDensityY;         // Pixel density in Y direction
            public int SleepMS;               // Milliseconds to sleep between position set and pixel read
            public int Width;                 // Screen width in pixels
            public int Height;                // Screen height in pixels
        }

        /// <summary>
        /// Capture point with normalized coordinates (0.0-1.0)
        /// </summary>
        public struct CapturePoint
        {
            public CapturePoint(double x, double y)
            {
                this.X = x;
                this.Y = y;
            }

            public double X;
            public double Y;
        }

        #endregion

        /// <summary>
        /// Check if pixel sampling library is available
        /// </summary>
        public bool IsAvailable()
        {
            Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Checking availability...");

            // Check if library file exists
            if (!System.IO.File.Exists("/usr/lib/libvideoenhance.so"))
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: libvideoenhance.so not found");
                return false;
            }

            Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Library found, available");
            return true;
        }

        /// <summary>
        /// Test pixel sampling by attempting to get screen condition
        /// </summary>
        public bool Test()
        {
            if (!IsAvailable())
                return false;

            try
            {
                Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Testing capture...");

                bool success = GetCondition();

                if (success)
                {
                    Helper.Log.Write(Helper.eLogType.Info,
                        $"PixelSampling Test: SUCCESS - Screen: {_condition.Width}x{_condition.Height}, " +
                        $"Points: {_condition.ScreenCapturePoints}, Sleep: {_condition.SleepMS}ms");
                    _isInitialized = true;
                    return true;
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Warning, "PixelSampling Test: GetCondition failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"PixelSampling Test exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get screen condition parameters from VideoEnhance library
        /// Uses API fallback: Try multiple Tizen API variants (6, 7, 9+)
        /// </summary>
        private bool GetCondition()
        {
            int res = -1;

            // TODO: DIAGNOSTIC - Remove struct size logging after verification
            Helper.Log.Write(Helper.eLogType.Debug,
                $"PixelSampling: Condition struct size: {Marshal.SizeOf<Condition>()} bytes");
            Helper.Log.Write(Helper.eLogType.Debug,
                $"PixelSampling: Color struct size: {Marshal.SizeOf<Color>()} bytes");

            // Try Tizen 6 API first (cs_ve_get_rgb_measure_condition)
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Trying Tizen 6 API (cs_ve_*)");
                res = MeasureCondition(out _condition);

                if (res >= 0)
                {
                    _usingTizen7Api = false;
                    Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Using Tizen 6 API (cs_ve_*)");
                    Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: MeasureCondition result: {res}");
                    LogConditionDetails();
                    return true;
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Warning,
                        $"PixelSampling: Tizen 6 API failed with error code {res}, trying next variant");
                }
            }
            catch (EntryPointNotFoundException)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    "PixelSampling: Tizen 6 API entry point not found, trying next variant");
            }
            catch (DllNotFoundException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"PixelSampling: Library not found: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Warning,
                    $"PixelSampling: Tizen 6 API exception: {ex.GetType().Name}: {ex.Message}, trying next variant");
            }

            // Try Tizen 7+ API (ve_get_rgb_measure_condition)
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Trying Tizen 7+ API (ve_*)");
                res = MeasureCondition7(out _condition);

                if (res >= 0)
                {
                    _usingTizen7Api = true;
                    Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Using Tizen 7+ API (ve_*)");
                    Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: MeasureCondition7 result: {res}");
                    LogConditionDetails();
                    return true;
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Warning,
                        $"PixelSampling: Tizen 7+ API failed with error code {res}, trying Tizen 9+ variants");
                }
            }
            catch (EntryPointNotFoundException)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    "PixelSampling: Tizen 7+ API entry point not found, trying Tizen 9+ variants");
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Warning,
                    $"PixelSampling: Tizen 7+ API exception: {ex.GetType().Name}: {ex.Message}, trying Tizen 9+ variants");
            }

            // Try Tizen 9+ API variant A (tizen_ve_*)
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Trying Tizen 9+ API variant A (tizen_ve_*)");
                res = MeasureCondition9A(out _condition);

                if (res >= 0)
                {
                    _usingTizen7Api = true; // Will use the 9A variants in GetColors
                    Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Using Tizen 9+ API variant A (tizen_ve_*)");
                    Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: MeasureCondition9A result: {res}");
                    LogConditionDetails();
                    return true;
                }
            }
            catch (EntryPointNotFoundException)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    "PixelSampling: Tizen 9+ API variant A entry point not found, trying variant B");
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: Tizen 9+ API variant A exception: {ex.GetType().Name}: {ex.Message}");
            }

            // Try Tizen 9+ API variant B (samsung_ve_*)
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Trying Tizen 9+ API variant B (samsung_ve_*)");
                res = MeasureCondition9B(out _condition);

                if (res >= 0)
                {
                    _usingTizen7Api = true; // Will use the 9B variants in GetColors
                    Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Using Tizen 9+ API variant B (samsung_ve_*)");
                    Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: MeasureCondition9B result: {res}");
                    LogConditionDetails();
                    return true;
                }
            }
            catch (EntryPointNotFoundException)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    "PixelSampling: Tizen 9+ API variant B entry point not found, trying variant C");
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: Tizen 9+ API variant B exception: {ex.GetType().Name}: {ex.Message}");
            }

            // Try Tizen 9+ API variant C (no prefix)
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Trying Tizen 9+ API variant C (no prefix)");
                res = MeasureCondition9C(out _condition);

                if (res >= 0)
                {
                    _usingTizen7Api = true; // Will use the 9C variants in GetColors
                    Helper.Log.Write(Helper.eLogType.Info, "PixelSampling: Using Tizen 9+ API variant C (no prefix)");
                    Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: MeasureCondition9C result: {res}");
                    LogConditionDetails();
                    return true;
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Error,
                        $"PixelSampling: ALL API variants failed - last attempt (variant C) returned error code {res}");

                    // List available entry points for diagnostics
                    LogAvailableEntryPoints();

                    return false;
                }
            }
            catch (EntryPointNotFoundException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"PixelSampling: Tizen 9+ API variant C entry point not found: {ex.Message}");
                Helper.Log.Write(Helper.eLogType.Error,
                    "PixelSampling: ALL API variants failed - library incompatible with this Tizen version");

                // List available entry points for diagnostics
                LogAvailableEntryPoints();

                return false;
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"PixelSampling: Tizen 9+ API variant C exception: {ex.GetType().Name}: {ex.Message}");

                // List available entry points for diagnostics
                LogAvailableEntryPoints();

                return false;
            }
        }

        /// <summary>
        /// Helper method to log condition details
        /// </summary>
        private void LogConditionDetails()
        {
            Helper.Log.Write(Helper.eLogType.Info,
                $"PixelSampling: Condition - Width: {_condition.Width}, Height: {_condition.Height}, " +
                $"Points: {_condition.ScreenCapturePoints}, PixelDensity: {_condition.PixelDensityX}x{_condition.PixelDensityY}, " +
                $"Sleep: {_condition.SleepMS}ms");
        }

        /// <summary>
        /// Log available entry points in libvideoenhance.so for diagnostics
        /// Uses dlopen/dlsym for programmatic symbol testing
        /// </summary>
        private void LogAvailableEntryPoints()
        {
            Helper.Log.Write(Helper.eLogType.Info,
                "PixelSampling: ===== LIBRARY SYMBOL ENUMERATION DIAGNOSTICS =====");

            // Use dlopen/dlsym to test specific entry points
            TryDlopenSymbolTest();

            Helper.Log.Write(Helper.eLogType.Info,
                "PixelSampling: ===== END DIAGNOSTICS =====");

            Helper.Log.Write(Helper.eLogType.Info,
                "PixelSampling: ACTIONABLE NEXT STEPS:");
            Helper.Log.Write(Helper.eLogType.Info,
                "  1. Check diagnostics output above for available symbols");
            Helper.Log.Write(Helper.eLogType.Info,
                "  2. If symbols found, add P/Invoke declarations with correct entry point names");
            Helper.Log.Write(Helper.eLogType.Info,
                "  3. If no symbols found, libvideoenhance.so may not support RGB pixel sampling on this Tizen version");
            Helper.Log.Write(Helper.eLogType.Info,
                "  4. Consider alternative capture methods (TBM/DRM capture for Tizen 8+)");
        }

        /// <summary>
        /// Try to test specific entry points using dlopen/dlsym
        /// Tests multiple library paths including Tizen 8+ .so.0.1.0 variants
        /// </summary>
        private bool TryDlopenSymbolTest()
        {
            try
            {
                Helper.Log.Write(Helper.eLogType.Info,
                    "PixelSampling: Testing entry points with dlopen/dlsym...");

                // Test multiple library paths (Tizen 8+ may use versioned .so.0.1.0)
                string[] libraryPaths = new string[]
                {
                    "/usr/lib/libvideoenhance.so",
                    "/usr/lib/libvideoenhance.so.0.1.0",
                    "/usr/lib/libvideoenhance.so.0",
                    "/lib/libvideoenhance.so",
                    "/lib/libvideoenhance.so.0.1.0",
                    "/lib/libvideoenhance.so.0"
                };

                IntPtr handle = IntPtr.Zero;
                string successPath = null;

                // Try to open library with different paths
                foreach (var libPath in libraryPaths)
                {
                    handle = dlopen(libPath, RTLD_NOW);
                    if (handle != IntPtr.Zero)
                    {
                        successPath = libPath;
                        Helper.Log.Write(Helper.eLogType.Info,
                            $"PixelSampling: ✓ Opened library: {libPath}");
                        break;
                    }
                }

                if (handle == IntPtr.Zero)
                {
                    IntPtr errorPtr = dlerror();
                    string error = errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
                    Helper.Log.Write(Helper.eLogType.Error,
                        $"PixelSampling: dlopen failed for all paths: {error}");
                    return false;
                }

                // Test known entry point patterns
                string[] knownSymbols = new string[]
                {
                    // Tizen 6/7 variants
                    "cs_ve_get_rgb_measure_condition",
                    "cs_ve_set_rgb_measure_position",
                    "cs_ve_get_rgb_measure_pixel",
                    "ve_get_rgb_measure_condition",
                    "ve_set_rgb_measure_position",
                    "ve_get_rgb_measure_pixel",

                    // Tizen 9+ potential variants
                    "tizen_ve_get_rgb_measure_condition",
                    "tizen_ve_set_rgb_measure_position",
                    "tizen_ve_get_rgb_measure_pixel",
                    "samsung_ve_get_rgb_measure_condition",
                    "samsung_ve_set_rgb_measure_position",
                    "samsung_ve_get_rgb_measure_pixel",
                    "get_rgb_measure_condition",
                    "set_rgb_measure_position",
                    "get_rgb_measure_pixel",

                    // Other potential naming patterns
                    "ve_rgb_measure_condition_get",
                    "ve_rgb_measure_position_set",
                    "ve_rgb_measure_pixel_get",
                    "videoenhance_get_rgb_condition",
                    "videoenhance_set_rgb_position",
                    "videoenhance_get_rgb_pixel",

                    // Completely different naming
                    "rgb_measure_condition",
                    "rgb_measure_position",
                    "rgb_measure_pixel",
                    "get_condition",
                    "set_position",
                    "get_pixel",

                    // Mangled/decorated variants
                    "_cs_ve_get_rgb_measure_condition",
                    "_ve_get_rgb_measure_condition",
                    "RGB_measure_condition",
                    "RGBMeasureCondition"
                };

                var foundSymbols = new List<string>();

                foreach (var symbol in knownSymbols)
                {
                    IntPtr sym = dlsym(handle, symbol);
                    if (sym != IntPtr.Zero)
                    {
                        foundSymbols.Add(symbol);
                    }
                }

                dlclose(handle);

                // Log results
                if (foundSymbols.Count > 0)
                {
                    Helper.Log.Write(Helper.eLogType.Info,
                        $"PixelSampling: ✓ Found {foundSymbols.Count} entry point(s) in {successPath}:");
                    foreach (var symbol in foundSymbols)
                    {
                        Helper.Log.Write(Helper.eLogType.Info, $"    ✓ {symbol}");
                    }
                }
                else
                {
                    Helper.Log.Write(Helper.eLogType.Warning,
                        $"PixelSampling: ✗ NO matching entry points found in {successPath}");
                    Helper.Log.Write(Helper.eLogType.Info,
                        $"PixelSampling: Tested {knownSymbols.Length} symbol patterns but none matched");
                }

                return foundSymbols.Count > 0;
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Warning,
                    $"PixelSampling: dlopen/dlsym test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sample pixel colors from predefined screen positions
        /// </summary>
        private Color[] GetColors()
        {
            Color[] colorData = new Color[_capturedPoints.Length];
            int[] updatedIndexes = new int[_condition.ScreenCapturePoints];

            int i = 0;
            while (i < _capturedPoints.Length)
            {
                if (_condition.ScreenCapturePoints == 0)
                {
                    Helper.Log.Write(Helper.eLogType.Error, "PixelSampling: ScreenCapturePoints is 0");
                    break;
                }

                // Set positions for batch sampling
                for (int j = 0; j < _condition.ScreenCapturePoints && i < _capturedPoints.Length; j++)
                {
                    updatedIndexes[j] = i;

                    // Convert normalized coordinates to pixel coordinates
                    int x = (int)(_capturedPoints[i].X * (double)_condition.Width) - _condition.PixelDensityX / 2;
                    int y = (int)(_capturedPoints[i].Y * (double)_condition.Height) - _condition.PixelDensityY / 2;

                    // Clamp coordinates to valid screen bounds
                    x = (x >= _condition.Width - _condition.PixelDensityX) ?
                        _condition.Width - (_condition.PixelDensityX + 1) : x;
                    y = (y >= _condition.Height - _condition.PixelDensityY) ?
                        (_condition.Height - _condition.PixelDensityY + 1) : y;

                    // Ensure coordinates are not negative
                    x = Math.Max(0, x);
                    y = Math.Max(0, y);

                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"PixelSampling: Point {i} - Normalized: ({_capturedPoints[i].X:F2}, {_capturedPoints[i].Y:F2}), " +
                        $"Pixel: ({x}, {y})");

                    // Set the measurement position
                    int res;
                    if (_usingTizen7Api)
                    {
                        res = MeasurePosition7(j, x, y);
                    }
                    else
                    {
                        res = MeasurePosition(j, x, y);
                    }

                    if (res < 0)
                    {
                        Helper.Log.Write(Helper.eLogType.Error,
                            $"PixelSampling: MeasurePosition failed for point {i} at ({x}, {y}) with error {res}");
                    }

                    i++;
                }

                // Sleep if required by the API
                if (_condition.SleepMS > 0)
                {
                    Thread.Sleep(_condition.SleepMS);
                }

                // Read the pixel colors
                int k = 0;
                while (k < _condition.ScreenCapturePoints && (i - _condition.ScreenCapturePoints + k) < _capturedPoints.Length)
                {
                    Color color;
                    int res;

                    if (_usingTizen7Api)
                    {
                        res = MeasurePixel7(k, out color);
                    }
                    else
                    {
                        res = MeasurePixel(k, out color);
                    }

                    if (res < 0)
                    {
                        Helper.Log.Write(Helper.eLogType.Error,
                            $"PixelSampling: MeasurePixel failed for index {k} with error {res}");
                        // Use black as fallback
                        color.R = 0;
                        color.G = 0;
                        color.B = 0;
                    }
                    else
                    {
                        // Validate color data (10-bit values should be 0-1023)
                        bool invalidColorData = color.R > 1023 || color.G > 1023 || color.B > 1023 ||
                                                color.R < 0 || color.G < 0 || color.B < 0;

                        if (invalidColorData)
                        {
                            Helper.Log.Write(Helper.eLogType.Warning,
                                $"PixelSampling: Invalid color data at index {k}: R={color.R}, G={color.G}, B={color.B}");
                            // Clamp to valid range
                            color.R = Math.Max(0, Math.Min(1023, color.R));
                            color.G = Math.Max(0, Math.Min(1023, color.G));
                            color.B = Math.Max(0, Math.Min(1023, color.B));
                        }

                        // TODO: DIAGNOSTIC - Remove color scaling comparison after verification
                        // Log both clamping and scaling approaches to determine which is needed
                        byte clampedR = (byte)Math.Min(color.R, 255);
                        byte clampedG = (byte)Math.Min(color.G, 255);
                        byte clampedB = (byte)Math.Min(color.B, 255);
                        byte scaledR = (byte)Math.Min(255, color.R * 255 / 1023);
                        byte scaledG = (byte)Math.Min(255, color.G * 255 / 1023);
                        byte scaledB = (byte)Math.Min(255, color.B * 255 / 1023);

                        Helper.Log.Write(Helper.eLogType.Debug,
                            $"PixelSampling: Point {i - _condition.ScreenCapturePoints + k} color (10-bit): " +
                            $"R={color.R}, G={color.G}, B={color.B}");
                        Helper.Log.Write(Helper.eLogType.Debug,
                            $"  → Clamped (8-bit): R={clampedR}, G={clampedG}, B={clampedB}");
                        Helper.Log.Write(Helper.eLogType.Debug,
                            $"  → Scaled  (8-bit): R={scaledR}, G={scaledG}, B={scaledB}");
                    }

                    colorData[i - _condition.ScreenCapturePoints + k] = color;
                    k++;
                }
            }

            return colorData;
        }

        /// <summary>
        /// Convert sampled pixel colors to NV12 format using BT.2020 color space
        /// Creates a virtual 64x48 image with sampled colors mapped to screen edges
        /// Uses BT.2020 coefficients for HDR10+ compatibility
        /// </summary>
        private (byte[] yData, byte[] uvData) ConvertColorsToNV12(Color[] colors)
        {
            const int width = 64;
            const int height = 48;

            // Allocate NV12 buffers
            byte[] yData = new byte[width * height];
            byte[] uvData = new byte[width * height / 2]; // UV plane is half the size

            // Create virtual RGB image (same logic as original ToImage method)
            byte[] rgbImage = new byte[width * height * 3]; // RGB888

            // Initialize with black
            for (int i = 0; i < rgbImage.Length; i++)
            {
                rgbImage[i] = 0;
            }

            // Top edge (first 4 colors)
            for (int x = 0; x < 64; x++)
            {
                Color color = colors[x / 16]; // 0, 1, 2, 3
                byte r = ClampTo8Bit(color.R);
                byte g = ClampTo8Bit(color.G);
                byte b = ClampTo8Bit(color.B);

                for (int y = 0; y < 4; y++)
                {
                    int idx = (y * width + x) * 3;
                    rgbImage[idx + 0] = r;
                    rgbImage[idx + 1] = g;
                    rgbImage[idx + 2] = b;
                }
            }

            // Bottom edge (colors 7-10)
            for (int x = 0; x < 64; x++)
            {
                Color color = colors[x / 16 + 7]; // 7, 8, 9, 10
                byte r = ClampTo8Bit(color.R);
                byte g = ClampTo8Bit(color.G);
                byte b = ClampTo8Bit(color.B);

                for (int y = 44; y < 48; y++)
                {
                    int idx = (y * width + x) * 3;
                    rgbImage[idx + 0] = r;
                    rgbImage[idx + 1] = g;
                    rgbImage[idx + 2] = b;
                }
            }

            // Left edge (colors 11-13)
            for (int y = 0; y < 48; y++)
            {
                Color color = colors[11 + y / 16]; // 11, 12, 13
                byte r = ClampTo8Bit(color.R);
                byte g = ClampTo8Bit(color.G);
                byte b = ClampTo8Bit(color.B);

                for (int x = 0; x < 3; x++)
                {
                    int idx = (y * width + x) * 3;
                    rgbImage[idx + 0] = r;
                    rgbImage[idx + 1] = g;
                    rgbImage[idx + 2] = b;
                }
            }

            // Right edge (colors 4-6)
            for (int y = 0; y < 48; y++)
            {
                Color color = colors[4 + y / 16]; // 4, 5, 6
                byte r = ClampTo8Bit(color.R);
                byte g = ClampTo8Bit(color.G);
                byte b = ClampTo8Bit(color.B);

                for (int x = 61; x < 64; x++)
                {
                    int idx = (y * width + x) * 3;
                    rgbImage[idx + 0] = r;
                    rgbImage[idx + 1] = g;
                    rgbImage[idx + 2] = b;
                }
            }

            // Convert RGB to NV12 using BT.2020 color space (HDR10+ compatible)
            // Y plane
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int rgbIdx = (y * width + x) * 3;
                    byte r = rgbImage[rgbIdx + 0];
                    byte g = rgbImage[rgbIdx + 1];
                    byte b = rgbImage[rgbIdx + 2];

                    // BT.2020 Y = 0.2627R + 0.678G + 0.0593B
                    int yVal = (int)(0.2627 * r + 0.678 * g + 0.0593 * b);
                    yData[y * width + x] = (byte)Math.Max(0, Math.Min(255, yVal));
                }
            }

            // UV plane (interleaved, subsampled 2x2)
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    // Sample 2x2 block
                    int rgbIdx = (y * width + x) * 3;
                    byte r = rgbImage[rgbIdx + 0];
                    byte g = rgbImage[rgbIdx + 1];
                    byte b = rgbImage[rgbIdx + 2];

                    // BT.2020 U = -0.1396R - 0.36037G + 0.5B + 128
                    // BT.2020 V = 0.5R - 0.4598G - 0.0402B + 128
                    int uVal = (int)(-0.1396 * r - 0.36037 * g + 0.5 * b + 128);
                    int vVal = (int)(0.5 * r - 0.4598 * g - 0.0402 * b + 128);

                    int uvIdx = (y / 2) * width + x;
                    uvData[uvIdx + 0] = (byte)Math.Max(0, Math.Min(255, uVal)); // U
                    uvData[uvIdx + 1] = (byte)Math.Max(0, Math.Min(255, vVal)); // V
                }
            }

            return (yData, uvData);
        }

        /// <summary>
        /// Clamp 10-bit color value (0-1023) to 8-bit (0-255)
        /// Note: This uses simple clamping, not scaling. Based on original implementation.
        /// TODO: DIAGNOSTIC - Review log output to determine if scaling (value * 255 / 1023) is needed instead
        /// </summary>
        private byte ClampTo8Bit(int value)
        {
            return (byte)Math.Min(value, 255);
        }

        /// <summary>
        /// Capture screen using pixel sampling
        /// </summary>
        public CaptureResult Capture(int width, int height)
        {
            try
            {
                // Initialize if not already done
                if (!_isInitialized)
                {
                    if (!GetCondition())
                    {
                        return CaptureResult.CreateFailure("PixelSampling: Failed to get condition");
                    }
                    _isInitialized = true;
                }

                // Sample pixels from screen
                Color[] colors = GetColors();

                if (colors == null || colors.Length == 0)
                {
                    return CaptureResult.CreateFailure("PixelSampling: No colors captured");
                }

                // Convert to NV12 format
                var (yData, uvData) = ConvertColorsToNV12(colors);

                // Return success with 64x48 sampled image
                return CaptureResult.CreateSuccess(yData, uvData, 64, 48);
            }
            catch (Exception ex)
            {
                return CaptureResult.CreateFailure($"PixelSampling exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Cleanup()
        {
            _isInitialized = false;
            Helper.Log.Write(Helper.eLogType.Debug, "PixelSampling: Cleaned up");
        }
    }
}
