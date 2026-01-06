using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBooth
{
    /// <summary>
    /// Simplified Canon EDSDK wrapper using EdsDownloadEvfImage for live view
    /// and PressShutterButton for capture
    /// </summary>
    public class EDSDKWrapper : IDisposable
    {
        #region Constants

        // Error Codes
        private const uint EDS_ERR_OK = 0x00000000;
        private const uint EDS_ERR_INVALID_PARAMETER = 0x00000007;
        private const uint EDS_ERR_EVF_NOTREADY = 0x0000A102;

        // Camera Commands
        private const uint kEdsCameraCommand_PressShutterButton = 0x0000000A;  // From sample code

        // Shutter Button States
        private const uint kEdsCameraCommand_ShutterButton_OFF = 0x00000000;
        private const uint kEdsCameraCommand_ShutterButton_Completely = 0x00000003;

        // Property IDs
        private const uint kEdsPropID_Evf_OutputDevice = 0x00000500;

        // EVF Values
        private const uint kEdsEvfOutputDevice_PC = 2;  // PC output = 0x02

        // Event IDs
        private const uint kEdsObjectEvent_All = 0x00000200;
        private const uint kEdsObjectEvent_DirItemCreated = 0x00000204;

        #endregion

        #region P/Invoke Declarations

        // SDK Initialization
        [DllImport("EDSDK.dll", EntryPoint = "EdsInitializeSDK", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsInitializeSDK();

        [DllImport("EDSDK.dll", EntryPoint = "EdsTerminateSDK", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsTerminateSDK();

        // Camera Discovery
        [DllImport("EDSDK.dll", EntryPoint = "EdsGetCameraList", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetCameraList(out IntPtr outCameraListRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsGetChildAtIndex", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetChildAtIndex(IntPtr inRef, int inIndex, out IntPtr outRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsRelease", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsRelease(IntPtr inRef);

        // Session Management
        [DllImport("EDSDK.dll", EntryPoint = "EdsOpenSession", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsOpenSession(IntPtr inCameraRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsCloseSession", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsCloseSession(IntPtr inCameraRef);

        // Property Access
        [DllImport("EDSDK.dll", EntryPoint = "EdsGetPropertyData", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetPropertyData(IntPtr inRef, uint inPropertyID, int inParam, int inPropertySize, IntPtr outPropertyData);

        [DllImport("EDSDK.dll", EntryPoint = "EdsSetPropertyData", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsSetPropertyData(IntPtr inRef, uint inPropertyID, int inParam, int inPropertySize, IntPtr inPropertyData);

        // Camera Commands
        [DllImport("EDSDK.dll", EntryPoint = "EdsSendCommand", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsSendCommand(IntPtr inCameraRef, uint inCommand, uint inParameter);

        // EVF (Live View) Functions
        [DllImport("EDSDK.dll", EntryPoint = "EdsCreateEvfImageRef", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsCreateEvfImageRef(IntPtr inStreamRef, out IntPtr outEvfImageRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsDownloadEvfImage", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsDownloadEvfImage(IntPtr inCameraRef, IntPtr outEvfImageRef);

        // Stream Functions
        [DllImport("EDSDK.dll", EntryPoint = "EdsCreateMemoryStream", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsCreateMemoryStream(uint inBufferSize, out IntPtr outStreamRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsGetLength", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetLength(IntPtr inStreamRef, out uint outLength);

        [DllImport("EDSDK.dll", EntryPoint = "EdsGetPointer", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetPointer(IntPtr inStreamRef, out IntPtr outPointer);

        // Event Handlers
        [DllImport("EDSDK.dll", EntryPoint = "EdsSetObjectEventHandler", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsSetObjectEventHandler(IntPtr inCameraRef, uint inEvent, EdsObjectEventHandler? inObjectEventHandler, IntPtr inContext);

        // Storage Functions
        [DllImport("EDSDK.dll", EntryPoint = "EdsGetDirectoryItemInfo", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsGetDirectoryItemInfo(IntPtr inDirItemRef, out EdsDirectoryItemInfo outDirItemInfo);

        [DllImport("EDSDK.dll", EntryPoint = "EdsDownload", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsDownload(IntPtr inDirItemRef, uint inReadSize, IntPtr outStreamRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsDownloadComplete", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsDownloadComplete(IntPtr inDirItemRef);

        [DllImport("EDSDK.dll", EntryPoint = "EdsCreateFileStream", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint EdsCreateFileStream([MarshalAs(UnmanagedType.LPStr)] string inFileName, uint inCreateDisposition, uint inDesiredAccess, out IntPtr outStreamRef);

        #endregion

        #region Delegates and Structures

        private delegate uint EdsObjectEventHandler(uint inEvent, IntPtr inRef, IntPtr inContext);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct EdsDirectoryItemInfo
        {
            public uint Size;
            public int IsFolder;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szFileName;
            public ulong Format;
            public uint DateTime;
        }

        #endregion

        #region Private Fields

        private bool _isInitialized = false;
        private IntPtr _cameraRef = IntPtr.Zero;
        private CancellationTokenSource? _liveViewCancellation;
        private Task? _liveViewTask;
        private readonly object _lockObject = new object();

        // Event handlers (must be kept alive)
        private EdsObjectEventHandler? _objectEventHandler;

        // Event for live view frames
        public event EventHandler<byte[]?>? EvfFrameReceived;

        // Event for captured images
        public event EventHandler<byte[]?>? ImageCaptured;

        #endregion

        #region Public Properties

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _cameraRef != IntPtr.Zero;
        public bool IsLiveViewActive { get; private set; }

        #endregion

        #region SDK Initialization

        /// <summary>
        /// Initialize EDSDK
        /// </summary>
        public bool Initialize()
        {
            if (_isInitialized)
                return true;

            lock (_lockObject)
            {
                try
                {
                    uint result = EdsInitializeSDK();
                    _isInitialized = (result == EDS_ERR_OK);

                    if (!_isInitialized)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] Initialize failed: 0x{result:X8}");
                    }

                    return _isInitialized;
                }
                catch (DllNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] EDSDK.dll not found: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] Initialize exception: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Connect to first available camera
        /// </summary>
        public bool ConnectToCamera()
        {
            if (!_isInitialized)
                return false;

            if (_cameraRef != IntPtr.Zero)
                return true;

            lock (_lockObject)
            {
                try
                {
                    IntPtr cameraList = IntPtr.Zero;
                    uint result = EdsGetCameraList(out cameraList);

                    if (result != EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] GetCameraList failed: 0x{result:X8}");
                        return false;
                    }

                    result = EdsGetChildAtIndex(cameraList, 0, out _cameraRef);
                    EdsRelease(cameraList);

                    if (result != EDS_ERR_OK || _cameraRef == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] No camera found");
                        return false;
                    }

                    result = EdsOpenSession(_cameraRef);
                    if (result != EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] OpenSession failed: 0x{result:X8}");
                        EdsRelease(_cameraRef);
                        _cameraRef = IntPtr.Zero;
                        return false;
                    }

                    // Register event handler for image capture
                    _objectEventHandler = OnObjectEvent;
                    EdsSetObjectEventHandler(_cameraRef, kEdsObjectEvent_All, _objectEventHandler, IntPtr.Zero);

                    System.Diagnostics.Debug.WriteLine("[EDSDK] Camera connected successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] ConnectToCamera exception: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when camera object events occur (e.g., DirItemCreated for new images)
        /// </summary>
        private uint OnObjectEvent(uint inEvent, IntPtr inRef, IntPtr inContext)
        {
            try
            {
                if (inEvent == kEdsObjectEvent_DirItemCreated)
                {
                    System.Diagnostics.Debug.WriteLine("[EDSDK] DirItemCreated event received - downloading image");
                    Task.Run(() => DownloadImage(inRef));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDSDK] OnObjectEvent exception: {ex.Message}");
            }

            return EDS_ERR_OK;
        }

        /// <summary>
        /// Download image from camera
        /// </summary>
        private void DownloadImage(IntPtr dirItemRef)
        {
            IntPtr streamRef = IntPtr.Zero;
            IntPtr fileStreamRef = IntPtr.Zero;

            try
            {
                // Get directory item info
                EdsDirectoryItemInfo itemInfo = new EdsDirectoryItemInfo();
                uint result = EdsGetDirectoryItemInfo(dirItemRef, out itemInfo);
                if (result != EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] GetDirectoryItemInfo failed: 0x{result:X8}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[EDSDK] Downloading image: {itemInfo.szFileName}");

                // Create memory stream
                result = EdsCreateMemoryStream(0, out streamRef);
                if (result != EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] CreateMemoryStream failed: 0x{result:X8}");
                    return;
                }

                // Download image
                result = EdsDownload(dirItemRef, 0, streamRef);
                if (result != EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] Download failed: 0x{result:X8}");
                    EdsRelease(streamRef);
                    return;
                }

                // Get stream length and pointer
                result = EdsGetLength(streamRef, out uint length);
                if (result != EDS_ERR_OK || length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] GetLength failed: 0x{result:X8}");
                    EdsRelease(streamRef);
                    return;
                }

                result = EdsGetPointer(streamRef, out IntPtr imagePtr);
                if (result != EDS_ERR_OK || imagePtr == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] GetPointer failed: 0x{result:X8}");
                    EdsRelease(streamRef);
                    return;
                }

                // Copy image data
                byte[] imageData = new byte[length];
                Marshal.Copy(imagePtr, imageData, 0, (int)length);

                // Mark download complete
                EdsDownloadComplete(dirItemRef);

                // Raise event
                System.Diagnostics.Debug.WriteLine($"[EDSDK] Image downloaded: {imageData.Length} bytes");
                ImageCaptured?.Invoke(this, imageData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDSDK] DownloadImage exception: {ex.Message}");
            }
            finally
            {
                if (fileStreamRef != IntPtr.Zero) EdsRelease(fileStreamRef);
                if (streamRef != IntPtr.Zero) EdsRelease(streamRef);
            }
        }

        #endregion

        #region Live View (EVF) Implementation

        /// <summary>
        /// Start live view by setting Evf_OutputDevice |= PC
        /// </summary>
        public bool StartLiveView()
        {
            if (_cameraRef == IntPtr.Zero)
                return false;

            lock (_lockObject)
            {
                if (IsLiveViewActive)
                    return true;

                try
                {
                    // Get current Evf_OutputDevice value
                    IntPtr devicePtr = Marshal.AllocHGlobal(sizeof(uint));
                    try
                    {
                        uint result = EdsGetPropertyData(_cameraRef, kEdsPropID_Evf_OutputDevice, 0, sizeof(uint), devicePtr);
                        uint device = 0;

                        if (result == EDS_ERR_OK)
                        {
                            device = (uint)Marshal.ReadInt32(devicePtr);
                        }

                        // Set Evf_OutputDevice |= PC (bitwise OR with 0x02)
                        device |= kEdsEvfOutputDevice_PC;

                        Marshal.WriteInt32(devicePtr, (int)device);
                        result = EdsSetPropertyData(_cameraRef, kEdsPropID_Evf_OutputDevice, 0, sizeof(uint), devicePtr);

                        if (result != EDS_ERR_OK)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EDSDK] Set Evf_OutputDevice failed: 0x{result:X8}");
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine($"[EDSDK] Evf_OutputDevice set to PC (0x{device:X8})");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(devicePtr);
                    }

                    // Start background thread for EVF frame download
                    IsLiveViewActive = true;
                    _liveViewCancellation = new CancellationTokenSource();
                    _liveViewTask = Task.Run(() => LiveViewLoop(_liveViewCancellation.Token), _liveViewCancellation.Token);

                    System.Diagnostics.Debug.WriteLine("[EDSDK] Live view started successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] StartLiveView exception: {ex.Message}");
                    IsLiveViewActive = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Stop live view
        /// </summary>
        public void StopLiveView()
        {
            lock (_lockObject)
            {
                if (!IsLiveViewActive)
                    return;

                try
                {
                    // Stop background thread
                    _liveViewCancellation?.Cancel();
                    _liveViewTask?.Wait(1000);

                    IsLiveViewActive = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] StopLiveView exception: {ex.Message}");
                }
                finally
                {
                    _liveViewCancellation?.Dispose();
                    _liveViewCancellation = null;
                    _liveViewTask = null;
                }
            }
        }

        /// <summary>
        /// Background thread loop for downloading EVF frames using EdsDownloadEvfImage
        /// </summary>
        private void LiveViewLoop(CancellationToken cancellationToken)
        {
            IntPtr evfStream = IntPtr.Zero;
            IntPtr evfImage = IntPtr.Zero;

            try
            {
                // Create persistent stream and image ref
                uint result = EdsCreateMemoryStream(0, out evfStream);
                if (result != EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] CreateMemoryStream failed: 0x{result:X8}");
                    return;
                }

                result = EdsCreateEvfImageRef(evfStream, out evfImage);
                if (result != EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] CreateEvfImageRef failed: 0x{result:X8}");
                    EdsRelease(evfStream);
                    return;
                }

                // Download loop - ~60ms interval (~16 FPS)
                while (!cancellationToken.IsCancellationRequested && IsLiveViewActive)
                {
                    try
                    {
                        // Download EVF image
                        result = EdsDownloadEvfImage(_cameraRef, evfImage);

                        if (result == EDS_ERR_OK)
                        {
                            // Get stream length and pointer
                            result = EdsGetLength(evfStream, out uint size);
                            if (result == EDS_ERR_OK && size > 0)
                            {
                                result = EdsGetPointer(evfStream, out IntPtr imagePtr);
                                if (result == EDS_ERR_OK && imagePtr != IntPtr.Zero)
                                {
                                    // Copy image data
                                    byte[] buffer = new byte[size];
                                    Marshal.Copy(imagePtr, buffer, 0, (int)size);
                                    EvfFrameReceived?.Invoke(this, buffer);
                                }
                            }
                        }
                        else if (result == EDS_ERR_EVF_NOTREADY)
                        {
                            // Normal - EVF not ready yet, wait and retry
                            Thread.Sleep(100);
                            continue;
                        }

                        // Wait ~60ms for next frame
                        Thread.Sleep(60);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] LiveViewLoop iteration exception: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDSDK] LiveViewLoop exception: {ex.Message}");
            }
            finally
            {
                if (evfImage != IntPtr.Zero) EdsRelease(evfImage);
                if (evfStream != IntPtr.Zero) EdsRelease(evfStream);
            }
        }

        #endregion

        #region Photo Capture

        /// <summary>
        /// Take photo using PressShutterButton Completely then OFF
        /// </summary>
        public uint TakePhoto(out string errorMessage)
        {
            errorMessage = "";
            if (_cameraRef == IntPtr.Zero)
            {
                errorMessage = "Camera not connected";
                return 0xFFFFFFFF;
            }

            lock (_lockObject)
            {
                try
                {
                    // Press shutter completely
                    uint result = EdsSendCommand(_cameraRef, kEdsCameraCommand_PressShutterButton, kEdsCameraCommand_ShutterButton_Completely);
                    if (result != EDS_ERR_OK)
                    {
                        errorMessage = $"PressShutterButton failed: 0x{result:X8}";
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] PressShutterButton (Completely) failed: 0x{result:X8}");
                        return result;
                    }

                    System.Diagnostics.Debug.WriteLine("[EDSDK] PressShutterButton (Completely) sent");

                    // Small delay
                    Thread.Sleep(100);

                    // Release shutter (OFF)
                    result = EdsSendCommand(_cameraRef, kEdsCameraCommand_PressShutterButton, kEdsCameraCommand_ShutterButton_OFF);
                    if (result != EDS_ERR_OK)
                    {
                        errorMessage = $"ReleaseShutterButton failed: 0x{result:X8}";
                        System.Diagnostics.Debug.WriteLine($"[EDSDK] PressShutterButton (OFF) failed: 0x{result:X8}");
                        return result;
                    }

                    System.Diagnostics.Debug.WriteLine("[EDSDK] PressShutterButton (OFF) sent - image capture initiated");
                    return EDS_ERR_OK;
                }
                catch (Exception ex)
                {
                    errorMessage = $"TakePhoto exception: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"[EDSDK] TakePhoto exception: {ex.Message}");
                    return 0xFFFFFFFF;
                }
            }
        }

        /// <summary>
        /// Take photo - simple version
        /// </summary>
        public bool TakePhoto()
        {
            string errorMsg;
            return TakePhoto(out errorMsg) == EDS_ERR_OK;
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            StopLiveView();

            lock (_lockObject)
            {
                if (_cameraRef != IntPtr.Zero)
                {
                    try
                    {
                        EdsCloseSession(_cameraRef);
                        EdsRelease(_cameraRef);
                        _cameraRef = IntPtr.Zero;
                    }
                    catch { }
                }

                if (_isInitialized)
                {
                    try
                    {
                        EdsTerminateSDK();
                        _isInitialized = false;
                    }
                    catch { }
                }
            }

            _liveViewCancellation?.Dispose();

            // Clear event handler references
            _objectEventHandler = null;
        }

        #endregion
    }
}
