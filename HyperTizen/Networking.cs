using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Google.FlatBuffers;
using hyperhdrnet;
using Tizen.Messaging.Messages;

namespace HyperTizen
{
    public static class Networking
    {
        private static readonly object _lock = new object();
        private static TcpClient _client;
        private static NetworkStream _stream;

        public static TcpClient client
        {
            get { lock (_lock) { return _client; } }
            set { lock (_lock) { _client = value; } }
        }

        public static NetworkStream stream
        {
            get { lock (_lock) { return _stream; } }
            set { lock (_lock) { _stream = value; } }
        }

        public static void DisconnectClient()
        {
            lock (_lock)
            {
                try
                {
                    if (_stream != null)
                    {
                        _stream.Flush();
                        _stream.Close(500);
                    }
                }
                catch (Exception ex)
                {
                    Helper.Log.Write(Helper.eLogType.Warning, $"DisconnectClient: Stream close error: {ex.Message}");
                }

                try
                {
                    if (_client != null)
                    {
                        _client.Close();
                    }
                }
                catch (Exception ex)
                {
                    Helper.Log.Write(Helper.eLogType.Warning, $"DisconnectClient: Client close error: {ex.Message}");
                }

                // CRITICAL FIX: Null out references to prevent race conditions
                _stream = null;
                _client = null;
                Helper.Log.Write(Helper.eLogType.Info, "DisconnectClient: Client and stream nulled");
            }
        }

        public static void SendRegister()
        {
            try
            {
                // Validate before connecting
                if (string.IsNullOrEmpty(Globals.Instance.ServerIp) || Globals.Instance.ServerPort <= 0)
                {
                    Helper.Log.Write(Helper.eLogType.Error, 
                        $"TCP FAILED: Bad config {Globals.Instance.ServerIp ?? "null"}:{Globals.Instance.ServerPort}");
                    return;
                }

                Helper.Log.Write(Helper.eLogType.Info,
                    $"TCP: Connecting to {Globals.Instance.ServerIp}:{Globals.Instance.ServerPort}");

                lock (_lock)
                {
                    _client = new TcpClient(Globals.Instance.ServerIp, Globals.Instance.ServerPort);

                    // Disable Nagle's algorithm to prevent buffering delays
                    _client.NoDelay = true;

                    Helper.Log.Write(Helper.eLogType.Info, "TCP: Socket created (NoDelay=true)");

                    if (_client == null || !_client.Connected)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, "TCP FAILED: Client null/not connected");
                        return;
                    }

                    Helper.Log.Write(Helper.eLogType.Info, "TCP: Connected! Getting stream...");

                    _stream = _client.GetStream();
                    if (_stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, "TCP FAILED: No stream");
                        return;
                    }

                    Helper.Log.Write(Helper.eLogType.Info, "TCP: Stream OK, creating FlatBuffer msg...");

                    byte[] registrationMessage = Networking.CreateRegistrationMessage();
                    if (registrationMessage == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, "TCP FAILED: No FlatBuffer message");
                        return;
                    }

                    // HyperHDR expects BIG-ENDIAN 4-byte size prefix (not FlatBuffers standard little-endian)
                    var header = new byte[4];
                    header[0] = (byte)((registrationMessage.Length >> 24) & 0xFF);  // Big-endian
                    header[1] = (byte)((registrationMessage.Length >> 16) & 0xFF);
                    header[2] = (byte)((registrationMessage.Length >> 8) & 0xFF);
                    header[3] = (byte)(registrationMessage.Length & 0xFF);

                    Helper.Log.Write(Helper.eLogType.Info,
                        $"TCP: Sending {registrationMessage.Length} bytes with big-endian header...");
                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"TCP: Header (big-endian size={registrationMessage.Length}): {BitConverter.ToString(header)}");
                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"TCP: Message bytes: {BitConverter.ToString(registrationMessage)}");

                    _stream.Write(header, 0, header.Length);
                    _stream.Write(registrationMessage, 0, registrationMessage.Length);

                    // CRITICAL FIX: Flush the stream to ensure data is actually sent!
                    // Without this, data stays in buffer and HyperHDR never receives it
                    _stream.Flush();

                    Helper.Log.Write(Helper.eLogType.Info, "TCP: Data sent and flushed, waiting for reply...");
                }

                ReadRegisterReply();
                
                Helper.Log.Write(Helper.eLogType.Info, "TCP OK: Fully registered!");
            }
            catch (SocketException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, 
                    $"SOCKET ERROR: {ex.Message} (Code:{ex.ErrorCode})");
                DisconnectClient();
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, 
                    $"ERROR: {ex.GetType().Name}: {ex.Message}");
                DisconnectClient();
            }
        }

        public static async Task SendImageAsync(byte[] yData, byte[] uvData, int width, int height)
        {
            Helper.Log.Write(Helper.eLogType.Debug,
                $"→ SendImageAsync: ENTRY - buffers Y={yData?.Length ?? 0} UV={uvData?.Length ?? 0}, size {width}x{height}");

            // ENHANCED NULL SAFETY: Check client validity before proceeding
            try
            {
                lock (_lock)
                {
                    if (_client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "SendImageAsync: ❌ client is null");
                        return;
                    }

                    if (_client.Client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "SendImageAsync: client.Client is null");
                        return;
                    }

                    if (!_client.Connected)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "SendImageAsync: client not connected");
                        return;
                    }

                    if (_stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "SendImageAsync: stream is null");
                        return;
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"SendImageAsync: NullRef during validation: {ex.Message}");
                return;
            }
            catch (ObjectDisposedException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"SendImageAsync: Object disposed during validation: {ex.Message}");
                return;
            }

            // CRITICAL: Validate buffer parameters at entry point
            if (yData == null || uvData == null)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"SendImageAsync: Null buffers (yData={yData == null}, uvData={uvData == null})");
                return;
            }

            if (width <= 0 || height <= 0)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"SendImageAsync: Invalid dimensions ({width}x{height})");
                return;
            }

            Helper.Log.Write(Helper.eLogType.Debug, "SendImageAsync: Creating FlatBuffer message...");
            byte[] message = CreateFlatBufferMessage(yData, uvData, width, height);
            if (message == null)
            {
                Helper.Log.Write(Helper.eLogType.Error, "SendImageAsync: ❌ CreateFlatBufferMessage returned null");
                return;
            }

            Helper.Log.Write(Helper.eLogType.Debug,
                $"SendImageAsync: ✓ FlatBuffer created ({message.Length} bytes), queueing send...");

            // Fire-and-forget required to avoid blocking capture loop
            // Validation above ensures buffers are correct before sending
            var watchFPS = System.Diagnostics.Stopwatch.StartNew();
            _ = SendMessageAndReceiveReplyAsync(message);
            watchFPS.Stop();
            Helper.Log.Write(Helper.eLogType.Debug,
                $"SendImageAsync: ✓ Message queued for async send in {watchFPS.ElapsedMilliseconds}ms");
        }
        static byte[] CreateFlatBufferMessage(byte[] yData, byte[] uvData, int width, int height)
        {
            // ENHANCED NULL SAFETY: Detailed checks with logging
            try
            {
                lock (_lock)
                {
                    if (_client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateFlatBufferMessage: client is null");
                        return null;
                    }

                    if (_client.Client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateFlatBufferMessage: client.Client is null");
                        return null;
                    }

                    if (!_client.Connected)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateFlatBufferMessage: client not connected");
                        return null;
                    }

                    if (_stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateFlatBufferMessage: stream is null");
                        return null;
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"CreateFlatBufferMessage: NullRef during validation: {ex.Message}");
                return null;
            }
            catch (ObjectDisposedException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"CreateFlatBufferMessage: Object disposed: {ex.Message}");
                return null;
            }

            // CRITICAL: Validate buffer parameters before using them
            if (yData == null)
            {
                Helper.Log.Write(Helper.eLogType.Error, "CreateFlatBufferMessage: yData is null");
                return null;
            }

            if (uvData == null)
            {
                Helper.Log.Write(Helper.eLogType.Error, "CreateFlatBufferMessage: uvData is null");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"CreateFlatBufferMessage: Invalid dimensions ({width}x{height})");
                return null;
            }

            // CRITICAL: Validate buffer sizes match NV12 format
            int expectedYSize = width * height;
            int expectedUVSize = (width * height) / 2;

            if (yData.Length != expectedYSize)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"CreateFlatBufferMessage: Invalid Y buffer size. Expected {expectedYSize}, got {yData.Length}");
                return null;
            }

            if (uvData.Length != expectedUVSize)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"CreateFlatBufferMessage: Invalid UV buffer size. Expected {expectedUVSize}, got {uvData.Length}");
                return null;
            }

            Helper.Log.Write(Helper.eLogType.Debug,
                $"CreateFlatBufferMessage: Validated buffers (Y={yData.Length}, UV={uvData.Length}) for {width}x{height}");

            var builder = new FlatBufferBuilder(yData.Length + uvData.Length + 100);

            var yVector = NV12Image.CreateDataYVector(builder, yData);
            var uvVector = NV12Image.CreateDataUvVector(builder, uvData);

            NV12Image.StartNV12Image(builder);
            NV12Image.AddDataY(builder, yVector);
            NV12Image.AddDataUv(builder, uvVector);
            NV12Image.AddWidth(builder, width);
            NV12Image.AddHeight(builder, height);
            NV12Image.AddStrideY(builder, width);  //TODO: Check if this is correct
            NV12Image.AddStrideUv(builder, width);
            var nv12Image = NV12Image.EndNV12Image(builder);

            Image.StartImage(builder);
            Image.AddDataType(builder, ImageType.NV12Image);
            Image.AddData(builder, nv12Image.Value);
            Image.AddDuration(builder, -1);
            var imageOffset = Image.EndImage(builder);

            Request.StartRequest(builder);
            Request.AddCommandType(builder, Command.Image);
            Request.AddCommand(builder, imageOffset.Value);
            var requestOffset = Request.EndRequest(builder);

            // Use regular Finish (NOT FinishSizePrefixed)
            // HyperHDR expects big-endian size prefix which we'll add manually in SendMessageAndReceiveReplyAsync()
            builder.Finish(requestOffset.Value);
            return builder.SizedByteArray();
        }

        static Reply ParseReply(byte[] receivedData)
        {
            var byteBuffer = new ByteBuffer(receivedData, 4); //shift for header
            return Reply.GetRootAsReply(byteBuffer);
        }

        public static byte[] CreateRegistrationMessage()
        {
            // ENHANCED NULL SAFETY: Detailed checks with logging
            try
            {
                lock (_lock)
                {
                    if (_client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateRegistrationMessage: client is null");
                        return null;
                    }

                    if (_client.Client == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateRegistrationMessage: client.Client is null");
                        return null;
                    }

                    if (!_client.Connected)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateRegistrationMessage: client not connected");
                        return null;
                    }

                    if (_stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning, "CreateRegistrationMessage: stream is null");
                        return null;
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"CreateRegistrationMessage: NullRef during validation: {ex.Message}");
                return null;
            }
            catch (ObjectDisposedException ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, $"CreateRegistrationMessage: Object disposed: {ex.Message}");
                return null;
            }

            var builder = new FlatBufferBuilder(256); //TODO:Check how to calculate correctly

            var originOffset = builder.CreateString("HyperTizen");

            Helper.Log.Write(Helper.eLogType.Debug,
                $"CreateRegistrationMessage: Building Register with origin='HyperTizen', priority=123");

            Register.StartRegister(builder);
            Register.AddPriority(builder, 123);
            Register.AddOrigin(builder, originOffset);
            var registerOffset = Register.EndRegister(builder);

            Helper.Log.Write(Helper.eLogType.Debug,
                $"CreateRegistrationMessage: Register offset={registerOffset.Value}");

            Request.StartRequest(builder);
            Request.AddCommandType(builder, Command.Register);
            Request.AddCommand(builder, registerOffset.Value);
            var requestOffset = Request.EndRequest(builder);

            Helper.Log.Write(Helper.eLogType.Debug,
                $"CreateRegistrationMessage: Request offset={requestOffset.Value}");

            // Use regular Finish (NOT FinishSizePrefixed)
            // HyperHDR expects big-endian size prefix which we'll add manually in SendRegister()
            builder.Finish(requestOffset.Value);
            byte[] message = builder.SizedByteArray();

            Helper.Log.Write(Helper.eLogType.Debug,
                $"CreateRegistrationMessage: Generated {message.Length} byte message (without size prefix)");

            return message;
        }

        public static void ReadRegisterReply()
        {
            try
            {
                lock (_lock)
                {
                    if (_client == null || !_client.Connected || _stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Error, "ReadRegisterReply: No client/stream");
                        return;
                    }

                    Helper.Log.Write(Helper.eLogType.Info, "ReadRegisterReply: Waiting for server reply...");

                    // Log stream status for debugging
                    Helper.Log.Write(Helper.eLogType.Debug,
                        $"ReadRegisterReply: Stream readable={_stream.CanRead}, writable={_stream.CanWrite}, dataAvailable={_stream.DataAvailable}");

                    // Set read timeout to prevent infinite blocking
                    _stream.ReadTimeout = 5000; // 5 second timeout

                    byte[] buffer = new byte[1024];
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        Helper.Log.Write(Helper.eLogType.Info, $"ReadRegisterReply: Got {bytesRead} bytes");

                        byte[] replyData = new byte[bytesRead];
                        Array.Copy(buffer, replyData, bytesRead);

                        // Log raw reply bytes for debugging
                        Helper.Log.Write(Helper.eLogType.Debug,
                            $"ReadRegisterReply: Raw bytes: {BitConverter.ToString(replyData)}");

                        Reply reply = ParseReply(replyData);

                        Helper.Log.Write(Helper.eLogType.Debug,
                            $"ReadRegisterReply: Parsed - Registered={reply.Registered}, Video={reply.Video}");

                        if (reply.Registered > 0)
                        {
                            Helper.Log.Write(Helper.eLogType.Info, "ReadRegisterReply: REGISTERED OK!");
                        }
                        else
                        {
                            Helper.Log.Write(Helper.eLogType.Error, $"ReadRegisterReply: NOT registered (code: {reply.Registered})");
                        }
                    }
                    else
                    {
                        Helper.Log.Write(Helper.eLogType.Error, "ReadRegisterReply: No data received");
                    }
                }
            }
            catch (System.IO.IOException ex)
            {
                // Log stream state at timeout
                string streamState;
                lock (_lock)
                {
                    streamState = _stream != null ?
                        $"CanRead={_stream.CanRead}, CanWrite={_stream.CanWrite}, DataAvail={_stream.DataAvailable}" :
                        "stream is null";
                }

                Helper.Log.Write(Helper.eLogType.Error,
                    $"ReadRegisterReply TIMEOUT: {ex.Message}");
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"ReadRegisterReply TIMEOUT: Stream state at timeout: {streamState}");
                DisconnectClient();
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error,
                    $"ReadRegisterReply ERROR: {ex.GetType().Name}: {ex.Message}");
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"ReadRegisterReply ERROR: Stack trace: {ex.StackTrace}");
                DisconnectClient();
            }
        }

        public static async Task ReadImageReply()
        {
            NetworkStream localStream;
            lock (_lock)
            {
                if (_client == null || !_client.Connected || _stream == null)
                    return;
                localStream = _stream;
            }

            byte[] buffer = new byte[1024];
            int bytesRead = await localStream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {

                byte[] replyData = new byte[bytesRead];
                Array.Copy(buffer, replyData, bytesRead);
                Reply reply = ParseReply(replyData);


                Helper.Log.Write(Helper.eLogType.Info, $"SendMessageAndReceiveReply: Reply_Video: {reply.Video}");
                Helper.Log.Write(Helper.eLogType.Info, $"SendMessageAndReceiveReply: Reply_Registered: {reply.Registered}");
                if (!string.IsNullOrEmpty(reply.Error))
                {
                    Helper.Log.Write(Helper.eLogType.Error, "SendMessageAndReceiveReply: (closing tcp client now) Reply_Error: " + reply.Error);
                    //Debug.WriteLine("SendMessageAndReceiveReply: Faulty msg(size:" + message.Length + "): " + BitConverter.ToString(message));
                    DisconnectClient();
                    return;
                }
            }
            else
            {
                Helper.Log.Write(Helper.eLogType.Error, "SendMessageAndReceiveReply: (closing tcp client now) No Answer from Server.");
                DisconnectClient();
                return;
            }
        }

        static async Task SendMessageAndReceiveReplyAsync(byte[] message)
        {
            try
            {
                Helper.Log.Write(Helper.eLogType.Debug,
                    $"  → SendMessageAndReceiveReplyAsync: ENTRY with {message.Length} byte message");

                NetworkStream localStream;
                lock (_lock)
                {
                    if (_client == null || !_client.Connected || _stream == null)
                    {
                        Helper.Log.Write(Helper.eLogType.Warning,
                            $"  → SendMessageAndReceiveReplyAsync: ❌ Connection not ready (client={_client != null}, connected={_client?.Connected ?? false}, stream={_stream != null})");
                        return;
                    }
                    localStream = _stream;
                }

                // HyperHDR expects BIG-ENDIAN 4-byte size prefix (not FlatBuffers standard little-endian)
                var header = new byte[4];
                header[0] = (byte)((message.Length >> 24) & 0xFF);  // Big-endian
                header[1] = (byte)((message.Length >> 16) & 0xFF);
                header[2] = (byte)((message.Length >> 8) & 0xFF);
                header[3] = (byte)(message.Length & 0xFF);

                Helper.Log.Write(Helper.eLogType.Debug,
                    $"  → SendMessageAndReceiveReplyAsync: Writing {message.Length} bytes to HyperHDR...");

                await localStream.WriteAsync(header, 0, header.Length);
                await localStream.WriteAsync(message, 0, message.Length);
                await localStream.FlushAsync();

                Helper.Log.Write(Helper.eLogType.Info,
                    $"  → SendMessageAndReceiveReplyAsync: ✓ Frame sent to HyperHDR ({message.Length} bytes)");

                _ = ReadImageReply();
            }
            catch (Exception ex)
            {
                Helper.Log.Write(Helper.eLogType.Error, "SendMessageAndReceiveReply: Exception (closing tcp client now) Sending/Receiving: " + ex.Message);
                DisconnectClient();
                return;
            }
        }

    }
}
