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

        // Track which API variant and library path works
        private string _workingVariant = null; // T6, T7, T9A, T9B, T9C
        private string _workingLibPath = null; // SO, SO010, SO0

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

        // ===== Tizen 6 API (cs_ve_* prefix) - Test all library paths =====

        // Library: /usr/lib/libvideoenhance.so
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T6_SO(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T6_SO(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T6_SO(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0.1.0
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T6_SO010(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T6_SO010(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T6_SO010(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T6_SO0(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T6_SO0(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "cs_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T6_SO0(int index, out Color color);

        // ===== Tizen 7 API (ve_* prefix) - Test all library paths =====

        // Library: /usr/lib/libvideoenhance.so
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T7_SO(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T7_SO(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T7_SO(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0.1.0
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T7_SO010(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T7_SO010(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T7_SO010(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T7_SO0(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T7_SO0(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T7_SO0(int index, out Color color);

        // ===== Tizen 9+ API variant A (tizen_ve_* prefix) - Test all library paths =====

        // Library: /usr/lib/libvideoenhance.so
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9A_SO(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9A_SO(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9A_SO(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0.1.0
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9A_SO010(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9A_SO010(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9A_SO010(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9A_SO0(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9A_SO0(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tizen_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9A_SO0(int index, out Color color);

        // ===== Tizen 9+ API variant B (samsung_ve_* prefix) - Test all library paths =====

        // Library: /usr/lib/libvideoenhance.so
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9B_SO(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9B_SO(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9B_SO(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0.1.0
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9B_SO010(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9B_SO010(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9B_SO010(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9B_SO0(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_set_rgb_measure_position")]
        private static extern int MeasurePosition_T9B_SO0(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "samsung_ve_get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9B_SO0(int index, out Color color);

        // ===== Tizen 9+ API variant C (no prefix) - Test all library paths =====

        // Library: /usr/lib/libvideoenhance.so
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9C_SO(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "set_rgb_measure_position")]
        private static extern int MeasurePosition_T9C_SO(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9C_SO(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0.1.0
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9C_SO010(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "set_rgb_measure_position")]
        private static extern int MeasurePosition_T9C_SO010(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0.1.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9C_SO010(int index, out Color color);

        // Library: /usr/lib/libvideoenhance.so.0
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_condition")]
        private static extern int MeasureCondition_T9C_SO0(out Condition condition);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "set_rgb_measure_position")]
        private static extern int MeasurePosition_T9C_SO0(int index, int x, int y);
        [DllImport("/usr/lib/libvideoenhance.so.0", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "get_rgb_measure_pixel")]
        private static extern int MeasurePixel_T9C_SO0(int index, out Color color);

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
        /// Tests ALL combinations of API variants and library paths systematically
        /// </summary>
        private bool GetCondition()
        {
            // TODO: DIAGNOSTIC - Remove struct size logging after verification
            Helper.Log.Write(Helper.eLogType.Debug,
                $"PixelSampling: Condition struct size: {Marshal.SizeOf<Condition>()} bytes");
            Helper.Log.Write(Helper.eLogType.Debug,
                $"PixelSampling: Color struct size: {Marshal.SizeOf<Color>()} bytes");

            Helper.Log.Write(Helper.eLogType.Info,
                "PixelSampling: Testing ALL combinations of entry points and library paths...");

            // Test Tizen 6 (cs_ve_*) with all library paths
            if (TryVariant(
                () => MeasureCondition_T6_SO(out _condition),
                (idx, x, y) => MeasurePosition_T6_SO(idx, x, y),
                MeasurePixel_T6_SO,
                "T6", "SO", "cs_ve_*", ".so")) return true;
            if (TryVariant(
                () => MeasureCondition_T6_SO010(out _condition),
                (idx, x, y) => MeasurePosition_T6_SO010(idx, x, y),
                MeasurePixel_T6_SO010,
                "T6", "SO010", "cs_ve_*", ".so.0.1.0")) return true;
            if (TryVariant(
                () => MeasureCondition_T6_SO0(out _condition),
                (idx, x, y) => MeasurePosition_T6_SO0(idx, x, y),
                MeasurePixel_T6_SO0,
                "T6", "SO0", "cs_ve_*", ".so.0")) return true;

            // Test Tizen 7 (ve_*) with all library paths
            if (TryVariant(
                () => MeasureCondition_T7_SO(out _condition),
                (idx, x, y) => MeasurePosition_T7_SO(idx, x, y),
                MeasurePixel_T7_SO,
                "T7", "SO", "ve_*", ".so")) return true;
            if (TryVariant(
                () => MeasureCondition_T7_SO010(out _condition),
                (idx, x, y) => MeasurePosition_T7_SO010(idx, x, y),
                MeasurePixel_T7_SO010,
                "T7", "SO010", "ve_*", ".so.0.1.0")) return true;
            if (TryVariant(
                () => MeasureCondition_T7_SO0(out _condition),
                (idx, x, y) => MeasurePosition_T7_SO0(idx, x, y),
                MeasurePixel_T7_SO0,
                "T7", "SO0", "ve_*", ".so.0")) return true;

            // Test Tizen 9+ variant A (tizen_ve_*) with all library paths
            if (TryVariant(
                () => MeasureCondition_T9A_SO(out _condition),
                (idx, x, y) => MeasurePosition_T9A_SO(idx, x, y),
                MeasurePixel_T9A_SO,
                "T9A", "SO", "tizen_ve_*", ".so")) return true;
            if (TryVariant(
                () => MeasureCondition_T9A_SO010(out _condition),
                (idx, x, y) => MeasurePosition_T9A_SO010(idx, x, y),
                MeasurePixel_T9A_SO010,
                "T9A", "SO010", "tizen_ve_*", ".so.0.1.0")) return true;
            if (TryVariant(
                () => MeasureCondition_T9A_SO0(out _condition),
                (idx, x, y) => MeasurePosition_T9A_SO0(idx, x, y),
                MeasurePixel_T9A_SO0,
                "T9A", "SO0", "tizen_ve_*", ".so.0")) return true;

            // Test Tizen 9+ variant B (samsung_ve_*) with all library paths
            if (TryVariant(
                () => MeasureCondition_T9B_SO(out _condition),
                (idx, x, y) => MeasurePosition_T9B_SO(idx, x, y),
                MeasurePixel_T9B_SO,
                "T9B", "SO", "samsung_ve_*", ".so")) return true;
            if (TryVariant(
                () => MeasureCondition_T9B_SO010(out _condition),
                (idx, x, y) => MeasurePosition_T9B_SO010(idx, x, y),
                MeasurePixel_T9B_SO010,
                "T9B", "SO010", "samsung_ve_*", ".so.0.1.0")) return true;
            if (TryVariant(
                () => MeasureCondition_T9B_SO0(out _condition),
                (idx, x, y) => MeasurePosition_T9B_SO0(idx, x, y),
                MeasurePixel_T9B_SO0,
                "T9B", "SO0", "samsung_ve_*", ".so.0")) return true;

            // Test Tizen 9+ variant C (no prefix) with all library paths
            if (TryVariant(
                () => MeasureCondition_T9C_SO(out _condition),
                (idx, x, y) => MeasurePosition_T9C_SO(idx, x, y),
                MeasurePixel_T9C_SO,
                "T9C", "SO", "no prefix", ".so")) return true;
            if (TryVariant(
                () => MeasureCondition_T9C_SO010(out _condition),
                (idx, x, y) => MeasurePosition_T9C_SO010(idx, x, y),
                MeasurePixel_T9C_SO010,
                "T9C", "SO010", "no prefix", ".so.0.1.0")) return true;
            if (TryVariant(
                () => MeasureCondition_T9C_SO0(out _condition),
                (idx, x, y) => MeasurePosition_T9C_SO0(idx, x, y),
                MeasurePixel_T9C_SO0,
                "T9C", "SO0", "no prefix", ".so.0")) return true;

            // All combinations failed
            Helper.Log.Write(Helper.eLogType.Error,
                "PixelSampling: ALL 15 combinations failed (5 entry point variants × 3 library paths)");
            Helper.Log.Write(Helper.eLogType.Error,
                "PixelSampling: libvideoenhance.so does not support RGB pixel sampling on this Tizen version");
            return false;
        }

        /// <summary>
        /// Try a specific API variant + library path combination
        /// Tests ALL 3 entry points to ensure complete API surface exists
        /// </summary>
        private delegate int MeasurePixelDelegate(int index, out Color color);

        private bool TryVariant(
            Func<int> conditionFunc,
            Func<int, int, int, int> positionFunc,
            MeasurePixelDelegate pixelFunc,
            string variant,
            string libPath,
            string entryPrefix,
            string libSuffix)
        {
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: Testing {variant} ({entryPrefix}) with libvideoenhance{libSuffix}");

                // Test 1: MeasureCondition
                int conditionResult = conditionFunc();
                if (conditionResult < 0)
                {
                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"PixelSampling: {variant}/{libPath} condition returned error {conditionResult}");
                    return false;
                }

                // Test 2: MeasurePosition (validate entry point exists with dummy coordinates)
                int positionResult = positionFunc(0, 0, 0);
                // Position may fail if called before proper setup, but entry point should exist
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: {variant}/{libPath} position entry point exists (result: {positionResult})");

                // Test 3: MeasurePixel (validate entry point exists)
                Color dummyColor;
                int pixelResult = pixelFunc(0, out dummyColor);
                // Pixel may fail if no position set yet, but entry point should exist
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: {variant}/{libPath} pixel entry point exists (result: {pixelResult})");

                // Success - all three entry points exist and condition succeeded
                _workingVariant = variant;
                _workingLibPath = libPath;
                Helper.Log.Write(Helper.eLogType.Info,
                    $"PixelSampling: ✓ SUCCESS - All 3 entry points validated for {variant} ({entryPrefix}) with libvideoenhance{libSuffix}");
                Helper.Log.Write(Helper.eLogType.Debug, $"PixelSampling: Condition result: {conditionResult}");
                LogConditionDetails();
                return true;
            }
            catch (EntryPointNotFoundException ex)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: {variant}/{libPath} entry point not found: {ex.Message}");
                return false;
            }
            catch (DllNotFoundException ex)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: {variant}/{libPath} library file not found: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"PixelSampling: {variant}/{libPath} exception: {ex.GetType().Name}: {ex.Message}");
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
        /// Call correct MeasurePosition variant based on working combination
        /// </summary>
        private int CallMeasurePosition(int index, int x, int y)
        {
            string key = $"{_workingVariant}_{_workingLibPath}";
            switch (key)
            {
                case "T6_SO": return MeasurePosition_T6_SO(index, x, y);
                case "T6_SO010": return MeasurePosition_T6_SO010(index, x, y);
                case "T6_SO0": return MeasurePosition_T6_SO0(index, x, y);
                case "T7_SO": return MeasurePosition_T7_SO(index, x, y);
                case "T7_SO010": return MeasurePosition_T7_SO010(index, x, y);
                case "T7_SO0": return MeasurePosition_T7_SO0(index, x, y);
                case "T9A_SO": return MeasurePosition_T9A_SO(index, x, y);
                case "T9A_SO010": return MeasurePosition_T9A_SO010(index, x, y);
                case "T9A_SO0": return MeasurePosition_T9A_SO0(index, x, y);
                case "T9B_SO": return MeasurePosition_T9B_SO(index, x, y);
                case "T9B_SO010": return MeasurePosition_T9B_SO010(index, x, y);
                case "T9B_SO0": return MeasurePosition_T9B_SO0(index, x, y);
                case "T9C_SO": return MeasurePosition_T9C_SO(index, x, y);
                case "T9C_SO010": return MeasurePosition_T9C_SO010(index, x, y);
                case "T9C_SO0": return MeasurePosition_T9C_SO0(index, x, y);
                default:
                    throw new InvalidOperationException($"Unknown variant: {key}");
            }
        }

        /// <summary>
        /// Call correct MeasurePixel variant based on working combination
        /// </summary>
        private int CallMeasurePixel(int index, out Color color)
        {
            string key = $"{_workingVariant}_{_workingLibPath}";
            switch (key)
            {
                case "T6_SO": return MeasurePixel_T6_SO(index, out color);
                case "T6_SO010": return MeasurePixel_T6_SO010(index, out color);
                case "T6_SO0": return MeasurePixel_T6_SO0(index, out color);
                case "T7_SO": return MeasurePixel_T7_SO(index, out color);
                case "T7_SO010": return MeasurePixel_T7_SO010(index, out color);
                case "T7_SO0": return MeasurePixel_T7_SO0(index, out color);
                case "T9A_SO": return MeasurePixel_T9A_SO(index, out color);
                case "T9A_SO010": return MeasurePixel_T9A_SO010(index, out color);
                case "T9A_SO0": return MeasurePixel_T9A_SO0(index, out color);
                case "T9B_SO": return MeasurePixel_T9B_SO(index, out color);
                case "T9B_SO010": return MeasurePixel_T9B_SO010(index, out color);
                case "T9B_SO0": return MeasurePixel_T9B_SO0(index, out color);
                case "T9C_SO": return MeasurePixel_T9C_SO(index, out color);
                case "T9C_SO010": return MeasurePixel_T9C_SO010(index, out color);
                case "T9C_SO0": return MeasurePixel_T9C_SO0(index, out color);
                default:
                    throw new InvalidOperationException($"Unknown variant: {key}");
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
                    int res = CallMeasurePosition(j, x, y);

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
                    int res = CallMeasurePixel(k, out color);

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
