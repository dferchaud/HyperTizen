using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HyperTizen.Capture
{
    /// <summary>
    /// Systematically tests all available capture methods and selects the best one
    /// Tests in priority order: T8SDK (fastest) → T7SDK (fast) → PixelSampling (slowest)
    /// Thread-safe selection process with automatic cleanup of unused methods
    /// </summary>
    public class CaptureMethodSelector
    {
        private readonly List<ICaptureMethod> _methods;
        private readonly object _selectionLock = new object();
        private ICaptureMethod _selectedMethod = null;
        private bool _hasSelectedMethod = false;

        /// <summary>
        /// Initialize all capture methods for testing
        /// Methods are created in priority order (highest to lowest)
        /// </summary>
        public CaptureMethodSelector()
        {
            Helper.Log.Write(Helper.eLogType.Info, "CaptureMethodSelector: Initializing all capture methods");

            _methods = new List<ICaptureMethod>
            {
                new T8SdkCaptureMethod(),    // Priority 3 (highest)
                new T7SdkCaptureMethod(),    // Priority 2 (medium)
                new PixelSamplingCaptureMethod()  // Priority 1 (lowest, fallback)
            };

            Helper.Log.Write(Helper.eLogType.Info, $"CaptureMethodSelector: {_methods.Count} capture methods initialized");
        }

        /// <summary>
        /// Select the best available capture method by testing each in priority order
        /// Returns the first method that passes both IsAvailable() and Test() checks
        /// Cleans up all failed and unused methods automatically
        /// SAFE: Already called from background thread via HyperionClient constructor Task.Run()
        /// </summary>
        /// <returns>The best working capture method, or null if all methods fail</returns>
        public ICaptureMethod SelectBestMethod()
        {
            // Check cache outside lock
            if (_hasSelectedMethod)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"CaptureMethodSelector: Returning cached selection: {_selectedMethod?.Name ?? "null"}");
                return _selectedMethod;
            }

            Helper.Log.Write(Helper.eLogType.Info, "CaptureMethodSelector: Starting capture method selection");
            Helper.Log.Write(Helper.eLogType.Info, $"Current Tizen version: {SDK.SystemInfo.TizenVersionMajor}.{SDK.SystemInfo.TizenVersionMinor}");
            Helper.Log.Write(Helper.eLogType.Info, "Testing methods in priority order (T8SDK → T7SDK → PixelSampling)");

            // Sort methods by priority (highest to lowest)
            var sortedMethods = _methods.OrderByDescending(m => m.Type).ToList();

            ICaptureMethod selectedMethod = null;
            var failedMethods = new List<ICaptureMethod>();

            // Test each method in priority order
            foreach (var method in sortedMethods)
            {
                try
                {
                    Helper.Log.Write(Helper.eLogType.Info,
                        $"CaptureMethodSelector: Testing {method.Name} (Priority: {(int)method.Type})");

                    // Step 1: Quick availability check (no capture, just checks prerequisites)
                    bool isAvailable = method.IsAvailable();
                    if (!isAvailable)
                    {
                        Helper.Log.Write(Helper.eLogType.Info,
                            $"CaptureMethodSelector: {method.Name} - Not available (prerequisites not met)");
                        failedMethods.Add(method);
                        continue;
                    }

                    Helper.Log.Write(Helper.eLogType.Info,
                        $"CaptureMethodSelector: {method.Name} - Availability check PASSED");

                    // Step 2: Real capture test (direct call - safe because HyperionClient.Start() already runs in Task.Run())
                    Helper.Log.Write(Helper.eLogType.Info,
                        $"CaptureMethodSelector: {method.Name} - Running capture test...");

                    bool testPassed = false;

                    try
                    {
                        // Direct call - no Task.Run() to avoid thread pool saturation
                        // HyperionClient constructor already does Task.Run(() => Start()),
                        // so we're already on a background thread
                        testPassed = method.Test();
                    }
                    catch (Exception ex)
                    {
                        Helper.Log.Write(Helper.eLogType.Error,
                            $"CaptureMethodSelector: {method.Name} - Test threw exception: {ex.Message}");
                        testPassed = false;
                    }

                    if (!testPassed)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning,
                            $"CaptureMethodSelector: {method.Name} - Capture test FAILED");
                        failedMethods.Add(method);
                        continue;
                    }

                        // Success! We found a working method
                        Helper.Log.Write(Helper.eLogType.Info,
                            $"CaptureMethodSelector: {method.Name} - Capture test PASSED");
                        Helper.Log.Write(Helper.eLogType.Info,
                            $"CaptureMethodSelector: ✓ SELECTED: {method.Name}");

                        selectedMethod = method;

                        // Mark all remaining untested methods as failed (will be cleaned up)
                        foreach (var remainingMethod in sortedMethods)
                        {
                            if (remainingMethod != selectedMethod && !failedMethods.Contains(remainingMethod))
                            {
                                failedMethods.Add(remainingMethod);
                            }
                        }

                        break; // Stop testing, we have a winner
                    }
                    catch (Exception ex)
                    {
                        Helper.Log.Write(Helper.eLogType.Error,
                            $"CaptureMethodSelector: {method.Name} - Exception during testing: {ex.Message}");
                        failedMethods.Add(method);
                    }
                }

            // Clean up all failed and unused methods
            CleanupFailedMethods(failedMethods);

            // Log final result
            if (selectedMethod != null)
            {
                Helper.Log.Write(Helper.eLogType.Info,
                    $"CaptureMethodSelector: Selection complete - Using {selectedMethod.Name}");
            }
            else
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    "CaptureMethodSelector: Selection FAILED - No working capture methods found!");
                Helper.Log.Write(Helper.eLogType.Error,
                    "CaptureMethodSelector: All capture methods failed. Please check system compatibility.");
            }

            // Cache the result (thread-safe)
            lock (_selectionLock)
            {
                _selectedMethod = selectedMethod;
                _hasSelectedMethod = true;
            }

            return selectedMethod;
        }

        /// <summary>
        /// Clean up resources for all failed and unused capture methods
        /// </summary>
        private void CleanupFailedMethods(List<ICaptureMethod> failedMethods)
        {
            if (failedMethods.Count == 0)
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    "CaptureMethodSelector: No failed methods to cleanup");
                return;
            }

            Helper.Log.Write(Helper.eLogType.Info,
                $"CaptureMethodSelector: Cleaning up {failedMethods.Count} failed/unused method(s)");

            foreach (var method in failedMethods)
            {
                try
                {
                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"CaptureMethodSelector: Cleaning up {method.Name}");
                    method.Cleanup();
                }
                catch (Exception ex)
                {
                    Helper.Log.Write(Helper.eLogType.Warning,
                        $"CaptureMethodSelector: Exception during cleanup of {method.Name}: {ex.Message}");
                }
            }

            Helper.Log.Write(Helper.eLogType.Info,
                "CaptureMethodSelector: Cleanup complete");
        }

        /// <summary>
        /// Get the currently selected method (if SelectBestMethod has been called)
        /// Returns null if no method has been selected yet
        /// </summary>
        public ICaptureMethod GetSelectedMethod()
        {
            lock (_selectionLock)
            {
                return _selectedMethod;
            }
        }

        /// <summary>
        /// Reset the selector and cleanup all methods
        /// Allows re-selection on next SelectBestMethod() call
        /// </summary>
        public void Reset()
        {
            lock (_selectionLock)
            {
                Helper.Log.Write(Helper.eLogType.Info,
                    "CaptureMethodSelector: Resetting selector");

                // Cleanup selected method if exists
                if (_selectedMethod != null)
                {
                    try
                    {
                        _selectedMethod.Cleanup();
                    }
                    catch (Exception ex)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning,
                            $"CaptureMethodSelector: Exception during reset cleanup: {ex.Message}");
                    }
                }

                _selectedMethod = null;
                _hasSelectedMethod = false;
                _methods.Clear();

                Helper.Log.Write(Helper.eLogType.Info,
                    "CaptureMethodSelector: Reset complete");
            }
        }
    }
}
