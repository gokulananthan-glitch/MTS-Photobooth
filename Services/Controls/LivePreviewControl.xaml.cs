using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;

namespace PhotoBooth.Controls
{
    /// <summary>
    /// Live preview control that uses the shared WebcamService instance
    /// </summary>
    public partial class LivePreviewControl : UserControl
    {
        private bool _isProcessingFrame = false;
        private FrameData? _currentFrameData;

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
                // Get frame data for selected style (for aspect ratio)
                _currentFrameData = FrameDataProvider.GetFrameDataForStyle(App.SelectedStyle);

                // Initialize shared webcam service instance if not already initialized
                if (App.WebcamService == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Initializing webcam service...");
                    
                    // List available webcams
                    WebcamService.ListAvailableWebcams();
                    
                    // Create and initialize the shared instance
                    App.WebcamService = new WebcamService();
                    
                    if (!App.WebcamService.Initialize())
                    {
                        System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Failed to initialize webcam service");
                        App.WebcamService.Dispose();
                        App.WebcamService = null;
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Webcam service initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Using existing webcam service instance");
                }

                // Subscribe to webcam frame events BEFORE starting capture
                App.WebcamService.FrameReceived += WebcamService_FrameReceived;

                // Start capture (StartCapture will handle if already running)
                if (App.WebcamService.StartCapture())
                {
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Webcam capture started/active");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Failed to start webcam capture");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Load error: {ex.Message}");
            }
        }

        private void LivePreviewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from shared webcam service (don't dispose - it's shared)
            if (App.WebcamService != null)
            {
                try
                {
                    App.WebcamService.FrameReceived -= WebcamService_FrameReceived;
                    System.Diagnostics.Debug.WriteLine("[LivePreviewControl] Unsubscribed from webcam service");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Error unsubscribing: {ex.Message}");
                }
            }
        }

        private BitmapSource? ConvertBytesToBitmapSource(byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                {
                    return null;
                }

                // Decode JPEG bytes using BitmapDecoder - returns BitmapFrame which is a BitmapSource
                BitmapFrame frame;
                using (var ms = new MemoryStream(imageData))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0)
                    {
                        return null;
                    }
                    frame = decoder.Frames[0];
                }
                
                // Ensure color format - convert grayscale to color if needed
                if (frame.Format == PixelFormats.Gray8 || frame.Format == PixelFormats.Gray16)
                {
                    var formatConverted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    return formatConverted;
                }
                
                // JPEG decoder typically returns Bgr24 or Bgr32 format (not RGB!)
                return frame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] ConvertBytesToBitmapSource error: {ex.Message}");
                return null;
            }
        }

        private void WebcamService_FrameReceived(object? sender, byte[]? frameData)
        {
            if (frameData != null && frameData.Length > 0)
            {
                // Skip frame if still processing previous frame
                if (_isProcessingFrame)
                {
                    return;
                }
                
                _isProcessingFrame = true;
                
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // Convert bytes to BitmapSource directly
                        BitmapSource? bitmapSource = ConvertBytesToBitmapSource(frameData);
                        if (bitmapSource != null && _currentFrameData != null)
                        {
                            // Crop and scale to match placeholder aspect ratio
                            var croppedScaled = ImageProcessor.CropAndScaleToAspectRatio(
                                bitmapSource, 
                                _currentFrameData.PlaceholderW, 
                                _currentFrameData.PlaceholderH);
                            
                            if (croppedScaled != null)
                            {
                                // Apply filters to live preview
                                var filtered = ImageProcessor.ApplyFilters(croppedScaled, App.Brightness, App.Grayscale);
                                if (filtered != null)
                                {
                                    PreviewImage.Source = filtered;
                                }
                            }
                        }
                        else if (bitmapSource != null)
                        {
                            // If no frame data, just apply filters directly
                            var filtered = ImageProcessor.ApplyFilters(bitmapSource, App.Brightness, App.Grayscale);
                            if (filtered != null)
                            {
                                PreviewImage.Source = filtered;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LivePreviewControl] Display webcam frame error: {ex.Message}");
                    }
                    finally
                    {
                        _isProcessingFrame = false;
                    }
                }, DispatcherPriority.Background);
            }
        }
    }
}

