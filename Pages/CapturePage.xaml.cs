using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Speech.Synthesis;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;

namespace PhotoBooth.Pages
{
    /// <summary>
    /// Capture Page - Simplified using ultra-fast template
    /// </summary>
    public partial class CapturePage : Page
    {
        private readonly NavigationService _navigationService;
        private DispatcherTimer? countdownTimer;
        private int countdownValue = 3;
        private int initialCountdownValue = 3;
        private int currentPhotoIndex = 0;
        private int totalPhotosNeeded = 1;
        private CanonCameraService? _cameraService;
        private FrameData? currentFrameData;
        private double _aspectRatio = 4.0 / 3.0;

        public CapturePage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            
            // Get photo count for selected style
            totalPhotosNeeded = FrameDataProvider.GetPhotoCountForStyle(App.SelectedStyle);
            
            // Check if we're in retake mode
            if (App.RetakePhotoIndex >= 0 && App.RetakePhotoIndex < totalPhotosNeeded)
            {
                currentPhotoIndex = App.RetakePhotoIndex;
                PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                UpdateStatus($"Retaking photo {currentPhotoIndex + 1}");
            }
            else
            {
                currentPhotoIndex = 0;
                PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                App.CapturedImages.Clear();
            }
            
            // Get frame data
            currentFrameData = FrameDataProvider.GetFrameDataForStyle(App.SelectedStyle);
            if (currentFrameData != null && currentFrameData.PlaceholderW > 0 && currentFrameData.PlaceholderH > 0)
            {
                _aspectRatio = (double)currentFrameData.PlaceholderW / currentFrameData.PlaceholderH;
            }
            
            // Initialize countdown timer
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            
            Loaded += CapturePage_Loaded;
            Unloaded += CapturePage_Unloaded;
        }
        
        private void CapturePage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeCamera();
            UpdateTimerDisplay();
        }

        private void CapturePage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
                countdownTimer.Tick -= CountdownTimer_Tick;
                countdownTimer = null;
            }

            // Unsubscribe from events
            if (_cameraService != null)
            {
                _cameraService.ImageCaptured -= CameraService_ImageCaptured;
            }
        }

        private void InitializeCamera()
        {
            try
            {
                // Use shared camera service (initialized by LivePreviewControl)
                if (App.CanonCameraService == null)
                {
                    UpdateStatus("Camera not initialized");
                    return;
                }

                _cameraService = App.CanonCameraService;
                
                // Subscribe to captured images
                _cameraService.ImageCaptured += CameraService_ImageCaptured;
                
                UpdateStatus("Camera ready");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] InitializeCamera error: {ex.Message}");
                UpdateStatus($"Camera error: {ex.Message}");
            }
        }

        private void CameraService_ImageCaptured(object? sender, byte[]? imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[CapturePage] CameraService_ImageCaptured: imageData is null or empty");
                CaptureButton.IsEnabled = true;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CapturePage] Image captured: {imageData.Length} bytes");

            // Process image in background - OPTIMIZED for speed
            Task.Run(() =>
            {
                try
                {
                    // Convert to BitmapSource (fast decode)
                    var bitmapSource = ConvertBytesToBitmapSource(imageData, fullQuality: true);
                    if (bitmapSource == null || currentFrameData == null) return;

                    // Crop to aspect ratio (optimized)
                    var croppedScaled = ImageProcessor.CropAndScaleToAspectRatio(
                        bitmapSource,
                        currentFrameData.PlaceholderW,
                        currentFrameData.PlaceholderH,
                        quality: 100);

                    if (croppedScaled == null) return;

                    // Apply filters (optimized)
                    var filtered = ImageProcessor.ApplyFilters(croppedScaled, App.Brightness, App.Grayscale);
                    if (filtered == null) return;

                    // Update UI immediately (no delay)
                    Dispatcher.InvokeAsync(() => ProcessCapturedImage(filtered), System.Windows.Threading.DispatcherPriority.Normal);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CapturePage] Process image error: {ex.Message}");
                    Dispatcher.InvokeAsync(() =>
                    {
                        UpdateStatus("Photo processing failed");
                        CaptureButton.IsEnabled = true;
                    });
                }
            });
        }

        private BitmapSource? ConvertBytesToBitmapSource(byte[] imageData, bool fullQuality = false)
        {
            try
            {
                if (imageData == null || imageData.Length == 0) return null;

                BitmapFrame frame;
                using (var ms = new MemoryStream(imageData))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) return null;
                    frame = decoder.Frames[0];
                }

                if (!fullQuality && frame.PixelWidth > 800)
                {
                    double scale = 800.0 / frame.PixelWidth;
                    var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                    scaled.Freeze();
                    return scaled;
                }

                frame.Freeze();
                return frame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ConvertBytesToBitmapSource error: {ex.Message}");
                return null;
            }
        }

        private void ProcessCapturedImage(BitmapImage filtered)
        {
            try
            {
                if (App.RetakePhotoIndex >= 0)
                {
                    // Retake mode
                    App.CapturedImages[App.RetakePhotoIndex] = filtered;
                    PreviewImage.Source = filtered;
                    UpdateStatus($"Photo {App.RetakePhotoIndex + 1} retaken!");
                    App.RetakePhotoIndex = -1;

                    Task.Delay(300).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_cameraService != null) _cameraService.StopLiveView();
                            _navigationService.NavigateTo(typeof(ImageReviewPage));
                        });
                    });
                }
                else
                {
                    // Normal mode
                    App.CapturedImages.Add(filtered);
                    PreviewImage.Source = filtered;
                    currentPhotoIndex++;

                    if (currentPhotoIndex >= totalPhotosNeeded)
                    {
                        // All photos captured
                        PhotoCounterText.Text = $"SHOT {currentPhotoIndex} OF {totalPhotosNeeded}";
                        UpdateStatus("All photos captured!");
                        CaptureButton.IsEnabled = false;

                        int delayMs = totalPhotosNeeded == 1 ? 100 : 300;
                        Task.Delay(delayMs).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_cameraService != null) _cameraService.StopLiveView();
                                _navigationService.NavigateTo(typeof(ImageReviewPage));
                            });
                        });
                    }
                    else
                    {
                        // More photos needed
                        PhotoCounterText.Text = $"SHOT {currentPhotoIndex + 1} OF {totalPhotosNeeded}";
                        UpdateStatus($"Photo {currentPhotoIndex} captured!");
                        CaptureButton.IsEnabled = true;

                        Task.Delay(1000).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PreviewImage.Source = null;
                                UpdateStatus("Ready for next photo");
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ProcessCapturedImage error: {ex.Message}");
                CaptureButton.IsEnabled = true;
            }
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            StartCountdown();
        }

        private void StartCountdown()
        {
            if (_cameraService == null || !_cameraService.IsConnected)
            {
                MessageBox.Show("Camera not connected!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CaptureButton.IsEnabled = false;

            int imageTimer = 5;
            if (App.CurrentMachineConfig != null && int.TryParse(App.CurrentMachineConfig.ImageTimer, out int timer))
            {
                imageTimer = timer;
            }

            initialCountdownValue = imageTimer;
            countdownValue = imageTimer;
            CountdownText.Text = countdownValue.ToString();
            CountdownOverlay.Visibility = Visibility.Visible;

            AnimateCountdownNumber();
            AnimateCountdownRingSegment(initialCountdownValue);
            UpdateTimerDisplay();
            countdownTimer?.Start();
        }

        private void AnimateCountdownNumber()
        {
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
                double circumference = 880;
                ring.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);

                var storyboard = new Storyboard();
                var resetAnimation = new DoubleAnimation
                {
                    To = circumference,
                    Duration = new Duration(TimeSpan.FromSeconds(0.05)),
                    BeginTime = TimeSpan.Zero
                };

                Storyboard.SetTarget(resetAnimation, ring);
                Storyboard.SetTargetProperty(resetAnimation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
                storyboard.Children.Add(resetAnimation);

                var fillAnimation = new DoubleAnimation
                {
                    From = circumference,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                    BeginTime = TimeSpan.FromSeconds(0.05),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                Storyboard.SetTarget(fillAnimation, ring);
                Storyboard.SetTargetProperty(fillAnimation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
                storyboard.Children.Add(fillAnimation);

                storyboard.Begin();
            }
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            countdownValue--;

            if (countdownValue > 0)
            {
                CountdownText.Visibility = Visibility.Visible;
                var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                if (cameraIcon != null)
                {
                    cameraIcon.Visibility = Visibility.Collapsed;
                }

                CountdownText.Text = countdownValue.ToString();
                AnimateCountdownNumber();
                AnimateCountdownRingSegment(initialCountdownValue);
            }
            else if (countdownValue == 0)
            {
                CountdownText.Visibility = Visibility.Collapsed;
                var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                if (cameraIcon != null)
                {
                    cameraIcon.Visibility = Visibility.Visible;
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

                AnimateCountdownRingSegment(initialCountdownValue);
            }
            else
            {
                countdownTimer?.Stop();
                ShowFlashEffect();

                Dispatcher.Invoke(() =>
                {
                    CountdownOverlay.Visibility = Visibility.Collapsed;
                    CountdownText.Visibility = Visibility.Visible;
                    CountdownText.FontSize = 140;
                    var cameraIcon = this.FindName("CountdownCameraIcon") as MahApps.Metro.IconPacks.PackIconFontAwesome;
                    if (cameraIcon != null)
                    {
                        cameraIcon.Visibility = Visibility.Collapsed;
                    }

                    var ring = this.FindName("CountdownRing") as System.Windows.Shapes.Ellipse;
                    if (ring != null)
                    {
                        ring.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
                        ring.StrokeDashOffset = 880;
                    }

                    CapturePhoto();
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

        /// <summary>
        /// Capture photo - Uses simplified CaptureAsync() method
        /// </summary>
        private async void CapturePhoto()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] CapturePhoto - Photo {currentPhotoIndex + 1} of {totalPhotosNeeded}");
                UpdateStatus("Capturing photo...");

                if (_cameraService == null || !_cameraService.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[CapturePage] Camera not ready");
                    UpdateStatus("Photo capture failed - camera not ready");
                    CaptureButton.IsEnabled = true;
                    return;
                }

                UpdateStatus("Capturing...");

                // Use simplified CaptureAsync - handles everything: Stop Live View → Wait → Capture → Download → Restart Live View
                byte[]? imageData = await _cameraService.CaptureAsync();

                if (imageData == null || imageData.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CapturePage] Capture failed or timeout");
                    UpdateStatus("Photo capture failed - please try again");
                    CaptureButton.IsEnabled = true;
                    return;
                }

                // ImageCaptured event will fire automatically from CaptureAsync
                // Process it in CameraService_ImageCaptured handler
                System.Diagnostics.Debug.WriteLine("[CapturePage] Capture completed, image will be processed in event handler");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapturePage] CapturePhoto error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CapturePage] CapturePhoto stack: {ex.StackTrace}");
                UpdateStatus($"Error: {ex.Message}");
                CaptureButton.IsEnabled = true;
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
            if (!string.IsNullOrEmpty(message) &&
                (message.Contains("error") || message.Contains("failed") || message.Contains("Ready") || message.Contains("captured")))
            {
                StatusBorder.Visibility = Visibility.Visible;
                if (message.Contains("captured") && !message.Contains("failed"))
                {
                    ShowPhotoCapturedMessage();
                }
                else
                {
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
            StatusBorder.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.3)));
            fadeIn.Completed += (s, e) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.3)));
                fadeOut.BeginTime = TimeSpan.FromSeconds(1.2);
                StatusBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            StatusBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void UpdateTimerDisplay()
        {
            // Timer display logic if needed
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cameraService != null)
            {
                _cameraService.ImageCaptured -= CameraService_ImageCaptured;
            }
            _navigationService.GoBack();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                System.Diagnostics.Debug.WriteLine($"[CapturePage] ResetButton_Click error: {ex.Message}");
            }
        }

        private async void SpeakTextAsync(string text)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var synthesizer = new SpeechSynthesizer())
                    {
                        var voices = synthesizer.GetInstalledVoices();
                        var femaleVoice = voices.FirstOrDefault(v => v.VoiceInfo.Gender == VoiceGender.Female);
                        if (femaleVoice != null)
                        {
                            synthesizer.SelectVoice(femaleVoice.VoiceInfo.Name);
                        }
                        synthesizer.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
                }
            });
        }
    }
}
