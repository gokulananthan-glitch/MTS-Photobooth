using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EDSDKLib;

namespace PhotoBooth.Services
{
    /// <summary>
    /// Canon Camera Service - Full working flow based on proven reference
    /// </summary>
    public class CanonCameraService : IDisposable
    {
        private IntPtr _camera = IntPtr.Zero;
        private IntPtr _session = IntPtr.Zero;
        private bool _liveViewOn = false;
        private bool _isInitialized = false;
        private bool _disposed = false;

        private AutoResetEvent _photoEvent = new AutoResetEvent(false);
        private IntPtr _lastPhotoItem = IntPtr.Zero;
        private readonly object _cameraLock = new object();

        // Event handlers (must be kept alive)
        private EDSDK.EdsObjectEventHandler? _objectEventHandler;
        private GCHandle _objectEventHandlerHandle;

        // Events
        public event EventHandler<byte[]?>? ImageCaptured;

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _camera != IntPtr.Zero && _session != IntPtr.Zero;
        public bool IsLiveViewOn => _liveViewOn;

        /// <summary>
        /// Initialize camera
        /// </summary>
        public bool Initialize()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Initializing...");

                // Initialize SDK
                uint err = EDSDK.EdsInitializeSDK();
                if (err != EDSDK.EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to initialize SDK: {err:X}");
                    return false;
                }

                // Get camera list
                IntPtr camList = IntPtr.Zero;
                err = EDSDK.EdsGetCameraList(out camList);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to get camera list: {err:X}");
                    EDSDK.EdsTerminateSDK();
                    return false;
                }

                // Get camera count
                int count = 0;
                err = EDSDK.EdsGetChildCount(camList, out count);
                if (err != EDSDK.EDS_ERR_OK || count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CanonCameraService] No cameras found");
                    EDSDK.EdsRelease(camList);
                    EDSDK.EdsTerminateSDK();
                    return false;
                }

                // Get first camera
                err = EDSDK.EdsGetChildAtIndex(camList, 0, out _camera);
                EDSDK.EdsRelease(camList);

                if (err != EDSDK.EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to get camera: {err:X}");
                    EDSDK.EdsTerminateSDK();
                    return false;
                }

                // Open session
                err = EDSDK.EdsOpenSession(_camera);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to open session: {err:X}");
                    EDSDK.EdsRelease(_camera);
                    EDSDK.EdsTerminateSDK();
                    return false;
                }

                _session = _camera;

                // Register callbacks
                RegisterCallbacks();

                // Set save destination to host
                uint saveTo = (uint)EDSDK.EdsSaveTo.Host;
                EDSDK.EdsSetPropertyData(_camera, EDSDK.PropID_SaveTo, 0, sizeof(uint), saveTo);

                // Set capacity
                EDSDK.EdsCapacity capacity = new EDSDK.EdsCapacity
                {
                    NumberOfFreeClusters = 0x7FFFFFFF,
                    BytesPerSector = 0x1000,
                    Reset = 1
                };
                EDSDK.EdsSetCapacity(_camera, capacity);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Initialize error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Register event callbacks
        /// </summary>
        private void RegisterCallbacks()
        {
            _objectEventHandler = OnObjectEvent;
            _objectEventHandlerHandle = GCHandle.Alloc(_objectEventHandler);
            uint err = EDSDK.EdsSetObjectEventHandler(_camera, EDSDK.ObjectEvent_All, _objectEventHandler, IntPtr.Zero);
            if (err != EDSDK.EDS_ERR_OK)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to set object event handler: {err:X}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Object event handler registered");
            }
        }

        /// <summary>
        /// Start Live View
        /// </summary>
        public void StartLiveView()
        {
            lock (_cameraLock)
            {
                if (!IsConnected) return;

                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Starting Live View...");

                // Set EVF mode
                uint evfMode = 1;
                EDSDK.EdsSetPropertyData(_camera, EDSDK.PropID_Evf_Mode, 0, sizeof(uint), evfMode);

                // Set PC as output device
                uint device = 0;
                EDSDK.EdsGetPropertyData(_camera, EDSDK.PropID_Evf_OutputDevice, 0, out device);
                device |= EDSDK.EvfOutputDevice_PC;
                EDSDK.EdsSetPropertyData(_camera, EDSDK.PropID_Evf_OutputDevice, 0, sizeof(uint), device);

                _liveViewOn = true;
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Live View started");
            }
        }

        /// <summary>
        /// Stop Live View
        /// </summary>
        public void StopLiveView()
        {
            lock (_cameraLock)
            {
                if (!IsConnected || !_liveViewOn) return;

                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Stopping Live View...");

                // Remove PC from output device
                uint device = 0;
                EDSDK.EdsGetPropertyData(_camera, EDSDK.PropID_Evf_OutputDevice, 0, out device);
                device &= ~EDSDK.EvfOutputDevice_PC;
                EDSDK.EdsSetPropertyData(_camera, EDSDK.PropID_Evf_OutputDevice, 0, sizeof(uint), device);

                _liveViewOn = false;
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Live View stopped");
            }
        }

        /// <summary>
        /// Get Live View frame (JPEG bytes) - OPTIMIZED for speed
        /// </summary>
        public byte[]? GetLiveFrame()
        {
            // Quick check without lock first
            if (!_liveViewOn) return null;

            // Minimize lock time - only check status, do work outside lock
            bool isLiveViewOn;
            IntPtr camera;
            lock (_cameraLock)
            {
                isLiveViewOn = _liveViewOn;
                camera = _camera;
            }

            if (!isLiveViewOn || camera == IntPtr.Zero) return null;

            try
            {
                IntPtr stream = IntPtr.Zero;
                IntPtr evfImage = IntPtr.Zero;

                try
                {
                    // Create memory stream
                    uint err = EDSDK.EdsCreateMemoryStream(2 * 1024 * 1024, out stream);
                    if (err != EDSDK.EDS_ERR_OK) return null;

                    // Create EVF image ref
                    err = EDSDK.EdsCreateEvfImageRef(stream, out evfImage);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        EDSDK.EdsRelease(stream);
                        return null;
                    }

                    // Download EVF image (no lock needed - read-only operation)
                    err = EDSDK.EdsDownloadEvfImage(camera, evfImage);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        if (err == EDSDK.EDS_ERR_OBJECT_NOTREADY)
                        {
                            // Frame not ready - normal
                            return null;
                        }
                        return null;
                    }

                    // Get stream data
                    IntPtr ptr;
                    UInt64 len;
                    EDSDK.EdsGetPointer(stream, out ptr);
                    EDSDK.EdsGetLength(stream, out len);

                    if (ptr == IntPtr.Zero || len == 0) return null;

                    byte[] data = new byte[(int)len];
                    Marshal.Copy(ptr, data, 0, (int)len);

                    return data;
                }
                finally
                {
                    if (evfImage != IntPtr.Zero) EDSDK.EdsRelease(evfImage);
                    if (stream != IntPtr.Zero) EDSDK.EdsRelease(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] GetLiveFrame error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Capture photo (async)
        /// </summary>
        public async Task<byte[]?> CaptureAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] CaptureAsync started");

                // Step 1: Stop Live View
                lock (_cameraLock)
                {
                    StopLiveView();
                }

                // Step 2: Wait 200ms (CRITICAL) - reduced to 150ms for faster capture
                await Task.Delay(150);

                // Step 3: Take picture
                lock (_cameraLock)
                {
                    System.Diagnostics.Debug.WriteLine("[CanonCameraService] Sending take picture command...");
                    uint err = EDSDK.EdsSendCommand(_camera, EDSDK.CameraCommand_TakePicture, 0);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Take picture failed: {err:X}");
                        StartLiveView(); // Restart on failure
                        return null;
                    }
                }

                // Step 4: Wait for object event (5 second timeout)
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Waiting for image event...");
                bool ok = await Task.Run(() => _photoEvent.WaitOne(5000));

                if (!ok)
                {
                    System.Diagnostics.Debug.WriteLine("[CanonCameraService] Capture timeout");
                    StartLiveView(); // Restart on timeout
                    return null;
                }

                // Step 5: Download photo
                System.Diagnostics.Debug.WriteLine("[CanonCameraService] Downloading photo...");
                byte[]? photo = DownloadPhoto(_lastPhotoItem);

                // Step 6: Restart Live View
                StartLiveView();

                return photo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] CaptureAsync error: {ex.Message}");
                StartLiveView(); // Restart on error
                return null;
            }
        }

        /// <summary>
        /// Object event handler
        /// </summary>
        private uint OnObjectEvent(uint ev, IntPtr item, IntPtr ctx)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] OnObjectEvent: event={ev:X}");

                if (ev == EDSDK.ObjectEvent_DirItemCreated || ev == EDSDK.ObjectEvent_DirItemRequestTransfer)
                {
                    System.Diagnostics.Debug.WriteLine("[CanonCameraService] Image captured event received");
                    
                    // Retain reference
                    EDSDK.EdsRetain(item);
                    
                    _lastPhotoItem = item;
                    _photoEvent.Set();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] OnObjectEvent error: {ex.Message}");
            }

            return EDSDK.EDS_ERR_OK;
        }

        /// <summary>
        /// Download photo
        /// </summary>
        private byte[]? DownloadPhoto(IntPtr item)
        {
            try
            {
                IntPtr stream = IntPtr.Zero;

                try
                {
                    // Get directory item info
                    EDSDK.EdsDirectoryItemInfo dirItemInfo;
                    uint err = EDSDK.EdsGetDirectoryItemInfo(item, out dirItemInfo);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to get directory item info: {err:X}");
                        return null;
                    }

                    // Create memory stream
                    err = EDSDK.EdsCreateMemoryStream(dirItemInfo.Size, out stream);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Failed to create stream: {err:X}");
                        return null;
                    }

                    // Download
                    err = EDSDK.EdsDownload(item, dirItemInfo.Size, stream);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Download failed: {err:X}");
                        return null;
                    }

                    // Complete download
                    err = EDSDK.EdsDownloadComplete(item);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CanonCameraService] DownloadComplete failed: {err:X}");
                        return null;
                    }

                    // Get stream data
                    IntPtr ptr;
                    UInt64 len;
                    EDSDK.EdsGetPointer(stream, out ptr);
                    EDSDK.EdsGetLength(stream, out len);

                    if (ptr == IntPtr.Zero || len == 0) return null;

                    byte[] data = new byte[(int)len];
                    Marshal.Copy(ptr, data, 0, (int)len);

                    System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Photo downloaded: {data.Length} bytes");

                    // Fire event on UI thread
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ImageCaptured?.Invoke(this, data);
                    });

                    return data;
                }
                finally
                {
                    if (stream != IntPtr.Zero) EDSDK.EdsRelease(stream);
                    if (item != IntPtr.Zero) EDSDK.EdsRelease(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] DownloadPhoto error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                StopLiveView();

                _photoEvent?.Dispose();

                if (_objectEventHandlerHandle.IsAllocated)
                {
                    _objectEventHandlerHandle.Free();
                }

                if (_session != IntPtr.Zero)
                {
                    EDSDK.EdsCloseSession(_session);
                    _session = IntPtr.Zero;
                }

                if (_camera != IntPtr.Zero)
                {
                    EDSDK.EdsRelease(_camera);
                    _camera = IntPtr.Zero;
                }

                if (_isInitialized)
                {
                    EDSDK.EdsTerminateSDK();
                    _isInitialized = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanonCameraService] Dispose error: {ex.Message}");
            }
        }
    }
}

