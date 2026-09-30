using System;
using System.Runtime.InteropServices;
using Tizen.Applications;

namespace HyperTizen
{
    public static class VideoCapture
    {
        public const int Width  = 480;  // 3840 / 8
        public const int Height = 270;  // 2160 / 8

        private static readonly int YSize  = Width * Height;
        private static readonly int UVSize = (Width * Height) / 2;

        private static IntPtr _pImageY;
        private static IntPtr _pImageUV;
        private static byte[] _yData;
        private static byte[] _uvData;

        public static void InitCapture()
        {
            if (_pImageY != IntPtr.Zero) return;
            _pImageY  = Marshal.AllocHGlobal(YSize);
            _pImageUV = Marshal.AllocHGlobal(UVSize);
            _yData    = new byte[YSize];
            _uvData   = new byte[UVSize];
            Diag.Log($"VideoCapture: buffers allocated ({Width}x{Height} NV12)");
        }

        // Raw result of one capture call; throws if the native library or entry point is missing.
        public static int ProbeCapture()
        {
            var info = new SDK.SecVideoCapture.Info_t
            {
                iGivenBufferSize1 = YSize,
                iGivenBufferSize2 = UVSize,
                pImageY           = _pImageY,
                pImageUV          = _pImageUV
            };
            return SDK.SecVideoCapture.CaptureScreen(Width, Height, ref info);
        }

        // Returns captured frame data, or null if capture failed (DRM, scaler error, etc.)
        public static (byte[] yData, byte[] uvData)? CaptureFrame()
        {
            var info = new SDK.SecVideoCapture.Info_t
            {
                iGivenBufferSize1 = YSize,
                iGivenBufferSize2 = UVSize,
                pImageY           = _pImageY,
                pImageUV          = _pImageUV
            };

            int result = SDK.SecVideoCapture.CaptureScreen(Width, Height, ref info);

            if (result < 0)
            {
                switch (result)
                {
                    case -4:
                        Diag.Log("VideoCapture: DRM content (-4), skipping frame");
                        break;
                    case -2:
                        Diag.Log("VideoCapture: scaler failure (-2), try cold reboot if persistent");
                        break;
                    default:
                        Diag.Log($"VideoCapture: capture error {result}");
                        break;
                }
                return null;
            }

            Marshal.Copy(info.pImageY,  _yData,  0, YSize);
            Marshal.Copy(info.pImageUV, _uvData, 0, UVSize);

            return (_yData, _uvData);
        }
    }
}
