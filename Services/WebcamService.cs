using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace PhotoBooth.Services
{
    /// <summary>
    /// Webcam service for camera preview and capture
    /// Uses OpenCvSharp for webcam access
    /// </summary>
    public class WebcamService : IDisposable
    {
        private VideoCapture? _videoCapture;
        private Mat? _currentFrame;
        private readonly object _frameLock = new object();
        private bool _isCapturing = false;
        private bool _disposed = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _captureTask;

        public event EventHandler<byte[]?>? FrameReceived;
        public event EventHandler<byte[]?>? ImageCaptured;

        public bool IsInitialized { get; private set; }
        public bool IsConnected => _videoCapture != null && _videoCapture.IsOpened();

        /// <summary>
        /// List all available webcam devices
        /// </summary>
        public static void ListAvailableWebcams()
        {
            Console.WriteLine("=== Scanning for available webcams ===");
            int foundCount = 0;
            const int maxCamerasToCheck = 10; // Check up to 10 camera indices

            for (int i = 0; i < maxCamerasToCheck; i++)
            {
                try
                {
                    using (var testCapture = new VideoCapture(i))
                    {
                        if (testCapture.IsOpened())
                        {
                            // Try to read a frame to verify it's working
                            using (var testFrame = new Mat())
                            {
                                if (testCapture.Read(testFrame) && !testFrame.Empty())
                                {
                                    int width = testFrame.Width;
                                    int height = testFrame.Height;
                                    int channels = testFrame.Channels();
                                    
                                    Console.WriteLine($"  Camera {i}: Available");
                                    Console.WriteLine($"    Resolution: {width}x{height}");
                                    Console.WriteLine($"    Channels: {channels}");
                                    
                                    // Try to get backend info
                                    try
                                    {
                                        var backend = testCapture.Get(VideoCaptureProperties.Backend);
                                        Console.WriteLine($"    Backend: {backend}");
                                    }
                                    catch { }
                                    
                                    foundCount++;
                                }
                                else
                                {
                                    Console.WriteLine($"  Camera {i}: Opened but cannot read frames");
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Camera index doesn't exist or failed to open
                    // This is normal, just continue
                }
            }

            Console.WriteLine($"=== Found {foundCount} available webcam(s) ===");
            Console.WriteLine();
        }

        public bool Initialize()
        {
            try
            {
                // Try to open default camera (index 0)
                _videoCapture = new VideoCapture(0);
                
                if (!_videoCapture.IsOpened())
                {
                    System.Diagnostics.Debug.WriteLine("[WebcamService] Failed to open camera");
                    _videoCapture?.Dispose();
                    _videoCapture = null;
                    return false;
                }

                // Try to set maximum resolution for better quality
                // Try common high resolutions first
                int[] widths = { 1920, 1280, 1024, 800, 640 };
                int[] heights = { 1080, 720, 768, 600, 480 };
                
                bool resolutionSet = false;
                for (int i = 0; i < widths.Length; i++)
                {
                    try
                    {
                        _videoCapture.Set(VideoCaptureProperties.FrameWidth, widths[i]);
                        _videoCapture.Set(VideoCaptureProperties.FrameHeight, heights[i]);
                        
                        // Verify the resolution was actually set
                        int actualWidth = (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth);
                        int actualHeight = (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight);
                        
                        if (actualWidth >= widths[i] * 0.9 && actualHeight >= heights[i] * 0.9)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebcamService] Resolution set to {actualWidth}x{actualHeight}");
                            resolutionSet = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Try next resolution
                        continue;
                    }
                }
                
                if (!resolutionSet)
                {
                    // Fallback to default
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);
                    System.Diagnostics.Debug.WriteLine("[WebcamService] Using fallback resolution 640x480");
                }
                
                IsInitialized = true;
                System.Diagnostics.Debug.WriteLine("[WebcamService] Initialized with default camera");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] Initialize error: {ex.Message}");
                _videoCapture?.Dispose();
                _videoCapture = null;
                return false;
            }
        }

        public bool StartCapture()
        {
            if (!IsInitialized || _videoCapture == null)
            {
                if (!Initialize())
                {
                    return false;
                }
            }

            try
            {
                if (!_isCapturing && _videoCapture != null && _videoCapture.IsOpened())
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _isCapturing = true;
                    
                    // Start frame capture loop in background thread
                    _captureTask = Task.Run(() => CaptureFrames(_cancellationTokenSource.Token));
                    
                    System.Diagnostics.Debug.WriteLine("[WebcamService] Started capturing");
                    return true;
                }
                return _isCapturing;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] StartCapture error: {ex.Message}");
                return false;
            }
        }

        private void CaptureFrames(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[WebcamService] CaptureFrames loop started");
                int frameSkipCounter = 0;
                
                while (_isCapturing && !cancellationToken.IsCancellationRequested && _videoCapture != null && _videoCapture.IsOpened())
                {
                    try
                    {
                        using (var frame = new Mat())
                        {
                            if (_videoCapture.Read(frame) && !frame.Empty())
                            {
                                // Skip every other frame for better performance
                                frameSkipCounter++;
                                if (frameSkipCounter % 2 != 0)
                                {
                                    continue;
                                }
                                
                                // Double-check we're still capturing before processing
                                if (!_isCapturing || cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }
                                
                                lock (_frameLock)
                                {
                                    // Dispose previous frame
                                    _currentFrame?.Dispose();
                                    
                                    // Clone the new frame
                                    _currentFrame = frame.Clone();
                                    
                                    // Convert to byte array for preview (lower quality for performance)
                                    byte[] frameData = MatToByteArray(_currentFrame, isCapture: false);
                                    if (frameData != null && frameData.Length > 0)
                                    {
                                        FrameReceived?.Invoke(this, frameData);
                                    }
                                }
                            }
                        }
                        
                        // Small delay to prevent CPU overload
                        Thread.Sleep(33);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebcamService] Error in capture loop: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[WebcamService] CaptureFrames loop ended");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] CaptureFrames error: {ex.Message}");
            }
        }

        public void StopCapture()
        {
            try
            {
                _isCapturing = false;
                _cancellationTokenSource?.Cancel();
                
                // Wait for capture task to finish
                if (_captureTask != null)
                {
                    try
                    {
                        _captureTask.Wait(1000);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebcamService] Error waiting for capture task: {ex.Message}");
                    }
                }
                
                _captureTask = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                System.Diagnostics.Debug.WriteLine("[WebcamService] Stopped capturing");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] StopCapture error: {ex.Message}");
            }
        }

        public bool CapturePhoto()
        {
            try
            {
                lock (_frameLock)
                {
                    if (_currentFrame != null && !_currentFrame.Empty())
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebcamService] Capturing photo from frame: {_currentFrame.Width}x{_currentFrame.Height}");
                        
                        // Clone the frame to ensure we have a copy
                        using (var frameCopy = _currentFrame.Clone())
                        {
                            // Convert to byte array with high quality for capture
                            byte[] imageData = MatToByteArray(frameCopy, isCapture: true);
                            
                            if (imageData != null && imageData.Length > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WebcamService] Photo captured: {imageData.Length} bytes (high quality)");
                                ImageCaptured?.Invoke(this, imageData);
                                return true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[WebcamService] MatToByteArray returned null or empty");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[WebcamService] CapturePhoto: _currentFrame is null or empty");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] CapturePhoto error: {ex.Message}");
                return false;
            }
        }

        private byte[] MatToByteArray(Mat mat, bool isCapture = false)
        {
            if (mat == null || mat.Empty())
            {
                return Array.Empty<byte>();
            }

            try
            {
                int channels = mat.Channels();
                
                Mat matToEncode = new Mat();
                if (channels == 3)
                {
                    matToEncode = mat.Clone();
                }
                else if (channels == 1)
                {
                    Cv2.CvtColor(mat, matToEncode, ColorConversionCodes.GRAY2BGR);
                }
                else
                {
                    matToEncode = mat.Clone();
                }
                
                // Flip horizontally for mirror effect
                Cv2.Flip(matToEncode, matToEncode, FlipMode.Y);

                // Use higher quality for captured images (95), lower for preview (80)
                int jpegQuality = isCapture ? 95 : 80;
                
                // Encode as JPEG
                Cv2.ImEncode(".jpg", matToEncode, out byte[] jpegData, new int[] { (int)ImwriteFlags.JpegQuality, jpegQuality });
                
                matToEncode.Dispose();
                
                if (jpegData != null && jpegData.Length > 0)
                {
                    // Only log for actual captures, not preview frames (reduces log spam)
                    if (isCapture)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebcamService] MatToByteArray: {jpegData.Length} bytes, quality: {jpegQuality}, isCapture: {isCapture}");
                    }
                    return jpegData;
                }
                else
                {
                    return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] MatToByteArray error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                StopCapture();
                
                FrameReceived = null;
                ImageCaptured = null;
                
                lock (_frameLock)
                {
                    _currentFrame?.Dispose();
                    _currentFrame = null;
                }
                
                _videoCapture?.Release();
                _videoCapture?.Dispose();
                _videoCapture = null;
                
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                IsInitialized = false;
                _disposed = true;
                
                System.Diagnostics.Debug.WriteLine("[WebcamService] Disposed");
                
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamService] Dispose error: {ex.Message}");
            }
        }
    }
}

