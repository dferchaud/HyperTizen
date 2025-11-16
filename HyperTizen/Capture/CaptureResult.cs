using System;

namespace HyperTizen.Capture
{
    /// <summary>
    /// Result of a screen capture operation
    /// Contains NV12 image data (Y and UV planes) or error information
    /// </summary>
    public class CaptureResult
    {
        /// <summary>
        /// True if capture succeeded, false if it failed
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Y plane data (luminance) in NV12 format
        /// Size = Width * Height bytes
        /// </summary>
        public byte[] YData { get; set; }

        /// <summary>
        /// UV plane data (chrominance) in NV12 format
        /// Interleaved U and V values
        /// Size = (Width * Height) / 2 bytes
        /// </summary>
        public byte[] UVData { get; set; }

        /// <summary>
        /// Actual width of captured frame
        /// May differ from requested width
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Actual height of captured frame
        /// May differ from requested height
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Error message if capture failed (Success = false)
        /// Null or empty if capture succeeded
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Create a successful capture result
        /// </summary>
        public static CaptureResult CreateSuccess(byte[] yData, byte[] uvData, int width, int height)
        {
            return new CaptureResult
            {
                Success = true,
                YData = yData,
                UVData = uvData,
                Width = width,
                Height = height,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Create a failed capture result
        /// </summary>
        public static CaptureResult CreateFailure(string errorMessage)
        {
            return new CaptureResult
            {
                Success = false,
                YData = null,
                UVData = null,
                Width = 0,
                Height = 0,
                ErrorMessage = errorMessage
            };
        }
    }
}
