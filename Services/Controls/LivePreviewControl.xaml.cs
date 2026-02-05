using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoBooth.Services;

namespace PhotoBooth.Controls
{
    /// <summary>
    /// Live Preview Control - Based on proven reference implementation
    /// </summary>
    public partial class LivePreviewControl : UserControl
    {
        private WriteableBitmap? _buffer;
        private CanonCameraService? _cam;
        private bool _running = false;

        private const int WIDTH = 960;
        private const int HEIGHT = 640;

        public LivePreviewControl()
        {
            InitializeComponent();
            Loaded += LivePreviewControl_Loaded;
            Unloaded += LivePreviewControl_Unloaded;
        }

        private void LivePreviewControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Loaded - Initializing...");

                // Use shared camera service or create new one
                if (App.CanonCameraService == null)
                {
                    _cam = new CanonCameraService();
                    if (_cam.Initialize())
                    {
                        App.CanonCameraService = _cam;
                        System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Created and initialized camera service");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Failed to initialize camera");
                        return;
                    }
                }
                else
                {
                    _cam = App.CanonCameraService;
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Using shared camera service");
                }

                // Initialize buffer
                InitBuffer();

                // Start Live View
                _cam.StartLiveView();

                // Start preview loop
                StartPreviewLoop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Load error: {ex.Message}");
            }
        }

        private void LivePreviewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Unloaded - Cleaning up...");
                _running = false;

                // Don't stop Live View - it's shared, just mark as not running
                _cam = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Unload error: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize framebuffer
        /// </summary>
        void InitBuffer()
        {
            _buffer = new WriteableBitmap(
                WIDTH,
                HEIGHT,
                96,
                96,
                PixelFormats.Bgr24,
                null);

            // Apply rendering optimizations
            RenderOptions.SetBitmapScalingMode(_buffer, BitmapScalingMode.LowQuality);
            RenderOptions.SetCachingHint(_buffer, CachingHint.Cache);

            // Set as source ONCE - never replace
            if (PreviewImage != null)
            {
                PreviewImage.Source = _buffer;
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Buffer initialized: {WIDTH}x{HEIGHT}");
            }
        }

        /// <summary>
        /// Start preview loop (30 FPS) - OPTIMIZED
        /// </summary>
        void StartPreviewLoop()
        {
            _running = true;

            Task.Run(async () =>
            {
                System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Preview loop started");

                while (_running && _cam != null && _cam.IsLiveViewOn)
                {
                    try
                    {
                        byte[]? frame = _cam.GetLiveFrame();

                        if (frame != null)
                        {
                            Render(frame);
                        }

                        // 30 FPS = 33ms interval (no delay if frame is null for faster retry)
                        await Task.Delay(frame != null ? 33 : 10);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Preview loop error: {ex.Message}");
                        await Task.Delay(33);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Preview loop stopped");
            });
        }

        /// <summary>
        /// Render JPEG frame to framebuffer - OPTIMIZED for speed
        /// </summary>
        void Render(byte[] jpeg)
        {
            try
            {
                if (_buffer == null) return;

                // Decode JPEG in background thread (faster)
                BitmapFrame? decodedFrame = null;
                using (var ms = new MemoryStream(jpeg))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0)
                    {
                        decodedFrame = decoder.Frames[0];
                        decodedFrame.Freeze();
                    }
                }

                if (decodedFrame == null) return;

                // Scale if needed (fast)
                BitmapSource? scaled = decodedFrame;
                if (decodedFrame.PixelWidth != WIDTH || decodedFrame.PixelHeight != HEIGHT)
                {
                    double scaleX = (double)WIDTH / decodedFrame.PixelWidth;
                    double scaleY = (double)HEIGHT / decodedFrame.PixelHeight;
                    var scaleTransform = new ScaleTransform(scaleX, scaleY);
                    scaleTransform.Freeze();
                    scaled = new TransformedBitmap(decodedFrame, scaleTransform);
                    scaled.Freeze();
                }

                // Convert to Bgr24 if needed
                BitmapSource? bgr24 = scaled;
                if (scaled.Format != PixelFormats.Bgr24)
                {
                    var formatConverted = new FormatConvertedBitmap(scaled, PixelFormats.Bgr24, null, 0);
                    formatConverted.Freeze();
                    bgr24 = formatConverted;
                }

                if (bgr24 == null) return;

                // Update framebuffer on UI thread - use InvokeAsync to auto-drop old frames
                Dispatcher.InvokeAsync(new Action(() =>
                {
                    try
                    {
                        if (_buffer == null || bgr24 == null) return;

                        _buffer.Lock();

                        // Direct pixel copy (fastest)
                        int copyWidth = Math.Min(bgr24.PixelWidth, WIDTH);
                        int copyHeight = Math.Min(bgr24.PixelHeight, HEIGHT);

                        bgr24.CopyPixels(
                            new Int32Rect(0, 0, copyWidth, copyHeight),
                            _buffer.BackBuffer,
                            _buffer.BackBufferStride * HEIGHT,
                            _buffer.BackBufferStride);

                        _buffer.AddDirtyRect(new Int32Rect(0, 0, copyWidth, copyHeight));
                        _buffer.Unlock();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Render error: {ex.Message}");
                        if (_buffer != null && !_buffer.IsFrozen)
                        {
                            try { _buffer.Unlock(); } catch { }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Render decode error: {ex.Message}");
            }
        }
    }
}

