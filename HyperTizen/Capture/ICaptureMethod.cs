using System;

namespace HyperTizen.Capture
{
    /// <summary>
    /// Common interface for all screen capture methods
    /// Allows runtime selection and fallback between T8 SDK, T7 SDK, and Pixel Sampling
    /// </summary>
    public interface ICaptureMethod
    {
        /// <summary>
        /// Human-readable name of this capture method
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Capture method type and priority level
        /// </summary>
        CaptureMethodType Type { get; }

        /// <summary>
        /// Check if this capture method is available on current system
        /// Does NOT perform actual capture, just checks prerequisites
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Test this capture method by performing a real capture
        /// Returns true if capture succeeded, false otherwise
        /// </summary>
        bool Test();

        /// <summary>
        /// Capture screen at specified resolution
        /// Returns CaptureResult with success status and image data
        /// </summary>
        CaptureResult Capture(int width, int height);

        /// <summary>
        /// Clean up any resources used by this capture method
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Capture method types with priority levels
    /// Higher numeric value = higher priority
    /// </summary>
    public enum CaptureMethodType
    {
        PixelSampling = 1,  // Lowest priority (fallback, slow but works on all Tizen 8+)
        T7SDK = 2,          // Medium priority (fast, works on Tizen 7 and below)
        T8SDK = 3           // Highest priority (fast, works on some Tizen 8+ models)
    }
}
