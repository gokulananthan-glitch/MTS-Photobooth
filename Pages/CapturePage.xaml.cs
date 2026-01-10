using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Speech.Synthesis;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;

namespace PhotoBooth.Pages
{
    public partial class CapturePage : Page
    {
        private readonly NavigationService _navigationService;
        private DispatcherTimer? countdownTimer;
        private int countdownValue = 3;
        private int initialCountdownValue = 3; // Store initial value for ring animation calculation
        private int currentPhotoIndex = 0;
        private int totalPhotosNeeded = 1;
        private WebcamService? webcamService;
        private BitmapImage? lastPreviewFrame;
        private FrameData? currentFrameData;
        private double _aspectRatio = 4.0 / 3.0; // fallback

        public CapturePage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            
            // Get photo count for selected style
            totalPhotosNeeded = FrameDataProvider.GetPhotoCountForStyle(App.SelectedStyle);
            
            // Check if we're in retake mode
            if (App.RetakePhotoIndex >= 0 && App.RetakePhotoIndex < totalPhotosNeeded)
            {
                // Retake mode: start at the specific photo index
                currentPhotoIndex = App.RetakePhotoIndex;
                PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                UpdateStatus($"Retaking photo {currentPhotoIndex + 1}");
            }
            else
            {
                // Normal mode: start fresh
                currentPhotoIndex = 0;
                PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                // Clear previous captured images
                App.CapturedImages.Clear();
            }
            
            // Initialize timer display
            UpdateTimerDisplay();
            
            // Get frame data for selected style (for aspect ratio)
            currentFrameData = FrameDataProvider.GetFrameDataForStyle(App.SelectedStyle);
            
            // Calculate aspect ratio from frame data
            if (currentFrameData != null && currentFrameData.PlaceholderW > 0 && currentFrameData.PlaceholderH > 0)
            {
                _aspectRatio = (double)currentFrameData.PlaceholderW / currentFrameData.PlaceholderH;
                System.Diagnostics.Debug.WriteLine($"[CapturePage] Aspect ratio calculated: {_aspectRatio} ({currentFrameData.PlaceholderW}x{currentFrameData.PlaceholderH})");
            }
            
            // Initialize countdown timer
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            
            // Initialize camera
            InitializeCamera();
            
            // Speak initial instruction
            Loaded += CapturePage_Loaded;
            
            // Clean up when page is unloaded
            Unloaded += CapturePage_Unloaded;
        }
        
        private void CapturePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize countdown ring to empty state (ready to fill up)
            var ring = this.FindName("CountdownRing") as System.Windows.Shapes.Ellipse;
            if (ring != null)
            {
                ring.StrokeDashOffset = 880; // Start empty (matches StrokeDashArray)
            }
            
            // Delay TTS
            Task.Delay(1000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Check if we're in retake mode
                    if (App.RetakePhotoIndex >= 0 && App.RetakePhotoIndex < totalPhotosNeeded)
                    {
                        SpeakTextAsync($"Retake the {GetOrdinalNumber(App.RetakePhotoIndex + 1)} photo. Click the camera button to start.");
                    }
                    else if (totalPhotosNeeded == 1)
                    {
                        SpeakTextAsync("Click the camera button to capture your photo");
                    }
                    else
                    {
                        SpeakTextAsync($"Click the camera button for photo {currentPhotoIndex + 1}");
                    }
                });
            });
        }
        
        private void PreviewBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Don't force sizing - let the border size naturally within MaxWidth/MaxHeight constraints
            // The image inside uses Stretch="Uniform" so it will maintain its aspect ratio
            // Images are already cropped/scaled to PlaceholderW/PlaceholderH by CropAndScaleToAspectRatio
        }

        private void CapturePage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from shared webcam service events (don't dispose - it's shared with LivePreviewControl)
            if (webcamService != null)
            {
                try
                {
                    webcamService.FrameReceived -= WebcamService_FrameReceived;
                    webcamService.ImageCaptured -= WebcamService_ImageCaptured;
                    // Don't stop capture or dispose - LivePreviewControl might still need it
                    webcamService = null;
                    System.Diagnostics.Debug.WriteLine("[CapturePage] Unsubscribed from webcam service (shared instance preserved)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CapturePage] Error unsubscribing from webcam: {ex.Message}");
                }
            }
            
            // Stop countdown timer
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
                countdownTimer.Tick -= CountdownTimer_Tick;
                countdownTimer = null;
            }
        }

        private void InitializeCamera()
        {
            try
            {
                // Use the shared webcam service instance (should already be initialized by LivePreviewControl on FilterSelectionPage)
                if (App.WebcamService == null)
                {
                    UpdateStatus("Webcam not initialized - Please go back to filter selection");
                    System.Diagnostics.Debug.WriteLine("[CapturePage] Webcam service not initialized - should have been initialized on FilterSelectionPage");
                    return;
                }

                webcamService = App.WebcamService;
                System.Diagnostics.Debug.WriteLine("[CapturePage] Using shared webcam service instance (already initialized)");

                // Check if webcam is connected
                if (!webcamService.IsConnected)
                {
                    UpdateStatus("Webcam not connected - Please connect a camera");
                    System.Diagnostics.Debug.WriteLine("[CapturePage] Webcam service exists but not connected");
                    return;
                }

                // Subscribe to webcam frame events
                webcamService.FrameReceived += WebcamService_FrameReceived;
                webcamService.ImageCaptured += WebcamService_ImageCaptured;

                // Ensure capture is running (it should already be running from FilterSelectionPage)
                if (!webcamService.IsInitialized)
                {
                    if (!webcamService.Initialize())
                    {
                        UpdateStatus("Webcam initialization failed");
                        return;
                    }
                }

                if (webcamService.StartCapture())
                {
                    UpdateStatus("Webcam connected - Live view active!");
                    
                    // Wait a moment for first frame to arrive
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (PreviewImage.Source != null)
                            {
                                UpdateStatus("Webcam connected - Live view active!");
                            }
                            else
                            {
                                UpdateStatus("Webcam connected - Waiting for frames...");
                            }
                        });
                    });
                }
                else
                {
                    UpdateStatus("Webcam already running - Live view active!");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Webcam error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CapturePage] Webcam error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ConvertBytesToBitmapSource error: {ex.Message}");
                return null;
            }
        }

        private bool _isProcessingFrame = false;

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
                        if (bitmapSource != null && currentFrameData != null)
                        {
                            // Crop and scale to match placeholder aspect ratio
                            var croppedScaled = ImageProcessor.CropAndScaleToAspectRatio(
                                bitmapSource, 
                                currentFrameData.PlaceholderW, 
                                currentFrameData.PlaceholderH);
                            
                            if (croppedScaled != null)
                            {
                                // Apply filters to live preview
                                var filtered = ImageProcessor.ApplyFilters(croppedScaled, App.Brightness, App.Grayscale);
                                if (filtered != null)
                                {
                                    PreviewImage.Source = filtered;
                                    // Store filtered image as last preview frame
                                    lastPreviewFrame = filtered;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CapturePage] Display webcam frame error: {ex.Message}");
                    }
                    finally
                    {
                        _isProcessingFrame = false;
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void WebcamService_ImageCaptured(object? sender, byte[]? imageData)
        {
            if (imageData != null && imageData.Length > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Convert to BitmapSource first (more reliable)
                        var bitmapSource = ConvertBytesToBitmapSource(imageData);
                        if (bitmapSource != null && currentFrameData != null)
                        {
                            // Crop and scale to match placeholder aspect ratio
                            var croppedScaled = ImageProcessor.CropAndScaleToAspectRatio(
                                bitmapSource, 
                                currentFrameData.PlaceholderW, 
                                currentFrameData.PlaceholderH);
                            
                            if (croppedScaled != null)
                            {
                                // Apply filters to captured image
                                var filtered = ImageProcessor.ApplyFilters(croppedScaled, App.Brightness, App.Grayscale);
                                if (filtered != null)
                                {
                                    // Check if we're in retake mode
                                    if (App.RetakePhotoIndex >= 0)
                                    {
                                        // Replace the specific image at the retake index
                                        App.CapturedImages[App.RetakePhotoIndex] = filtered;
                                        UpdateStatus($"Photo {App.RetakePhotoIndex + 1} retaken!");
                                        
                                        // Reset retake mode
                                        App.RetakePhotoIndex = -1;
                                        
                                        // Go back to review page
                                        _navigationService.NavigateTo(typeof(ImageReviewPage));
                                    }
                                    else
                                    {
                                        // Normal mode: add to list
                                        App.CapturedImages.Add(filtered);
                                        
                                        currentPhotoIndex++;
                                        
                                        if (currentPhotoIndex >= totalPhotosNeeded)
                                        {
                                            // All photos captured, navigate to image review
                                            UpdateStatus("All photos captured!");
                                            _navigationService.NavigateTo(typeof(ImageReviewPage));
                                        }
                                        else
                                        {
                                            // More photos needed
                                            PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                                            UpdateStatus("Photo captured!");
                                            CaptureButton.IsEnabled = true;
                                            
                                            // Speak instruction for next photo
                                            SpeakTextAsync($"Click camera icon to take next {GetOrdinalNumber(currentPhotoIndex + 1)} photo");
                                        }
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[CapturePage] Filter application returned null for captured image");
                                    UpdateStatus("Photo capture failed - filter error");
                                    CaptureButton.IsEnabled = true;
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[CapturePage] Crop and scale returned null for captured image");
                                UpdateStatus("Photo capture failed - crop/scale error");
                                CaptureButton.IsEnabled = true;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[CapturePage] ConvertBytesToBitmapSource returned null for captured image or frameData is null");
                            UpdateStatus("Photo capture failed - conversion error");
                            CaptureButton.IsEnabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CapturePage] ImageCaptured error: {ex.Message}");
                        CaptureButton.IsEnabled = true;
                    }
                });
            }
        }

        private BitmapImage? ConvertBytesToBitmapImage(byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CapturePage] ConvertBytesToBitmapImage: Empty image data");
                    return null;
                }

                // Use BitmapSource conversion, then convert to BitmapImage via ImageProcessor
                // This avoids stream disposal issues
                var bitmapSource = ConvertBytesToBitmapSource(imageData);
                if (bitmapSource == null)
                {
                    return null;
                }
                
                // Apply filters (which returns BitmapImage)
                var filtered = ImageProcessor.ApplyFilters(bitmapSource, App.Brightness, App.Grayscale);
                
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ConvertBytesToBitmapImage: Success, size: {imageData.Length} bytes");
                return filtered;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ConvertBytesToBitmapImage error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CapturePage] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            StartCountdown();
        }

        private void StartCountdown()
        {
            bool isConnected = webcamService != null && webcamService.IsConnected;

            if (!isConnected)
            {
                MessageBox.Show("Camera not connected!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CaptureButton.IsEnabled = false;
            
            // Get image timer from config (default 5 seconds)
            int imageTimer = 5;
            if (App.CurrentMachineConfig != null && int.TryParse(App.CurrentMachineConfig.ImageTimer, out int timer))
            {
                imageTimer = timer;
            }
            
            // Store initial value for ring animation calculations
            initialCountdownValue = imageTimer;
            countdownValue = imageTimer;
            
            CountdownText.Text = countdownValue.ToString();
            CountdownOverlay.Visibility = Visibility.Visible;
            
            // Animate countdown text scale in
            AnimateCountdownNumber();
            
            // Initialize the ring - animate first segment
            AnimateCountdownRingSegment(initialCountdownValue);
            
            UpdateTimerDisplay();
            countdownTimer?.Start();
        }
        
        private void AnimateCountdownNumber()
        {
            // Scale animation for countdown number
            var scaleTransform = new ScaleTransform(0, 0, CountdownText.ActualWidth / 2, CountdownText.ActualHeight / 2);
            CountdownText.RenderTransform = scaleTransform;
            
            var scaleAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
        
        private void AnimateCountdownRingSegment(int totalSeconds)
        {
            var ring = this.FindName("CountdownRing") as System.Windows.Shapes.Ellipse;
            if (ring != null)
            {
                // Use 880 to match the StrokeDashArray value in XAML
                double circumference = 880;
                
                // Stop any existing animations
                ring.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
                
                // Create a storyboard for smooth animation sequence
                var storyboard = new Storyboard();
                
                // Quick reset animation (0.05 seconds) - from current position to empty
                var resetAnimation = new DoubleAnimation
                {
                    To = circumference,
                    Duration = new Duration(TimeSpan.FromSeconds(0.05)),
                    BeginTime = TimeSpan.Zero
                };
                
                Storyboard.SetTarget(resetAnimation, ring);
                Storyboard.SetTargetProperty(resetAnimation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
                storyboard.Children.Add(resetAnimation);
                
                // Main fill animation (0.7 seconds) - from empty to complete - faster
                var fillAnimation = new DoubleAnimation
                {
                    From = circumference,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                    BeginTime = TimeSpan.FromSeconds(0.05), // Start after reset
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                
                Storyboard.SetTarget(fillAnimation, ring);
                Storyboard.SetTargetProperty(fillAnimation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
                storyboard.Children.Add(fillAnimation);
                
                // Start the storyboard
                storyboard.Begin();
                
                System.Diagnostics.Debug.WriteLine($"[CapturePage] Ring animation started with smooth reset + fill");
            }
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            countdownValue--;

            if (countdownValue > 0)
            {
                // Make sure text is visible and icon is hidden
                CountdownText.Visibility = Visibility.Visible;
                var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                if (cameraIcon != null)
                {
                    cameraIcon.Visibility = Visibility.Collapsed;
                }
                
                CountdownText.Text = countdownValue.ToString();
                AnimateCountdownNumber();
                
                // Animate next ring segment
                AnimateCountdownRingSegment(initialCountdownValue);
            }
            else if (countdownValue == 0)
            {
                // Hide text and show camera icon
                CountdownText.Visibility = Visibility.Collapsed;
                var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                if (cameraIcon != null)
                {
                    cameraIcon.Visibility = Visibility.Visible;
                    
                    // Animate the camera icon
                    var scaleTransform = new ScaleTransform(0, 0, cameraIcon.ActualWidth / 2, cameraIcon.ActualHeight / 2);
                    cameraIcon.RenderTransform = scaleTransform;
                    
                    var scaleAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                    };
                    
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                }
                
                // Animate final ring segment
                AnimateCountdownRingSegment(initialCountdownValue);
            }
            else
            {
                countdownTimer?.Stop();
                
                // Show flash effect
                ShowFlashEffect();
                
                // Hide countdown and capture photo
                Task.Delay(200).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        CountdownOverlay.Visibility = Visibility.Collapsed;
                        
                        // Reset visibility for next countdown
                        CountdownText.Visibility = Visibility.Visible;
                        CountdownText.FontSize = 140; // Reset font size
                        var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                        if (cameraIcon != null)
                        {
                            cameraIcon.Visibility = Visibility.Collapsed;
                        }
                        
                        // Reset the ring animation
                        var ring = this.FindName("CountdownRing") as System.Windows.Shapes.Ellipse;
                        if (ring != null)
                        {
                            ring.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
                            // Reset to empty circle (ready for next countdown to fill up)
                            ring.StrokeDashOffset = 880;
                        }
                        
                        CapturePhoto();
                    });
                });
            }
        }
        
        private void ShowFlashEffect()
        {
            var flash = this.FindName("FlashOverlay") as Border;
            if (flash != null)
            {
                flash.Visibility = Visibility.Visible;
                flash.Opacity = 1;
                
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                fadeOut.Completed += (s, e) =>
                {
                    flash.Visibility = Visibility.Collapsed;
                };
                
                flash.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private async void CapturePhoto()
        {
            try
            {
                UpdateStatus("Capturing photo...");

                // Capture from webcam
                if (webcamService != null && webcamService.CapturePhoto())
                {
                    UpdateStatus("Photo captured!");
                    // ImageCaptured event will be raised
                    await Task.Delay(500); // Small delay for UI update
                }
                else
                {
                    UpdateStatus("Photo capture failed - no frame available");
                    CaptureButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                CaptureButton.IsEnabled = true;
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
            // Show status border for important messages
            if (!string.IsNullOrEmpty(message) && 
                (message.Contains("error") || message.Contains("failed") || message.Contains("Ready") || message.Contains("captured")))
            {
                StatusBorder.Visibility = Visibility.Visible;
                
                // If it's a "captured" message, show with fade-in and fade-out after 1.5 seconds
                if (message.Contains("captured") && !message.Contains("failed"))
                {
                    ShowPhotoCapturedMessage();
                }
                else
                {
                    // For other messages, just show normally
                    StatusBorder.Opacity = 1.0;
                }
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ShowPhotoCapturedMessage()
        {
            // Stop any existing animations
            StatusBorder.BeginAnimation(UIElement.OpacityProperty, null);
            
            // Set initial opacity to 0
            StatusBorder.Opacity = 0;
            StatusBorder.Visibility = Visibility.Visible;
            
            // Fade in animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Fade out animation after 1.5 seconds
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                BeginTime = TimeSpan.FromSeconds(1.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            
            // Create storyboard
            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(fadeOut);
            
            // Set target
            Storyboard.SetTarget(fadeIn, StatusBorder);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(fadeOut, StatusBorder);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
            
            // Handle completion
            fadeOut.Completed += (s, e) =>
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusBorder.Opacity = 1.0; // Reset for next time
            };
            
            // Start animation
            storyboard.Begin();
        }
        
        private void UpdateTimerDisplay()
        {
            // Get image timer from config (default 3 seconds)
            int imageTimer = 3;
            if (App.CurrentMachineConfig != null && int.TryParse(App.CurrentMachineConfig.ImageTimer, out int timer))
            {
                imageTimer = timer;
            }
            
            // Find TimerText control and update it
            var timerText = this.FindName("TimerText") as System.Windows.Controls.TextBlock;
            if (timerText != null)
            {
                timerText.Text = $"TIMER: {imageTimer}S";
            }
        }

        private void SpeakTextAsync(string text)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var synthesizer = new SpeechSynthesizer())
                    {
                        synthesizer.SetOutputToDefaultAudioDevice();
                        
                        // Try to select a female voice
                        var voices = synthesizer.GetInstalledVoices();
                        var femaleVoice = voices.FirstOrDefault(v => 
                            v.VoiceInfo.Gender == VoiceGender.Female);
                        
                        if (femaleVoice != null)
                        {
                            synthesizer.SelectVoice(femaleVoice.VoiceInfo.Name);
                        }
                        
                        synthesizer.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] Error speaking text: {ex.Message}");
                }
            });
        }
        
        private string GetOrdinalNumber(int number)
        {
            if (number <= 0) return number.ToString();
            
            if (number % 100 >= 11 && number % 100 <= 13)
            {
                return $"{number}th";
            }
            
            switch (number % 10)
            {
                case 1: return $"{number}st";
                case 2: return $"{number}nd";
                case 3: return $"{number}rd";
                default: return $"{number}th";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from shared webcam service events (don't stop/dispose - it's shared)
            if (webcamService != null)
            {
                webcamService.FrameReceived -= WebcamService_FrameReceived;
                webcamService.ImageCaptured -= WebcamService_ImageCaptured;
                // Don't stop capture - LivePreviewControl on FilterSelectionPage needs it
            }
            
            _navigationService.NavigateTo(typeof(FilterSelectionPage));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop countdown if running
                if (countdownTimer != null)
                {
                    countdownTimer.Stop();
                }
                
                // Reset to default values
                App.Brightness = 1.0;
                App.Grayscale = false;
                App.SelectedStyle = 0;
                App.CapturedImages.Clear();
                App.RetakePhotoIndex = -1;
                App.NumberOfCopies = 1;
                App.PendingTransactionData = null;

                System.Diagnostics.Debug.WriteLine("[CapturePage] Starting over - navigating to StartPage");
                
                // Navigate back to start page
                _navigationService.NavigateTo(typeof(StartPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] Reset error: {ex.Message}");
                MessageBox.Show($"Reset error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

