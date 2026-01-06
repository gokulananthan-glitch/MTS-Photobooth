using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Speech.Synthesis;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;

namespace PhotoBooth.Pages
{
    public partial class FilterSelectionPage : Page
    {
        private readonly NavigationService _navigationService;
        private FrameData? _currentFrameData;
        private double _aspectRatio = 4.0 / 3.0; // fallback
        private bool _isWebcamActive = false; // Track if webcam is active

        public FilterSelectionPage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            
            Loaded += FilterSelectionPage_Loaded;
            Unloaded += FilterSelectionPage_Unloaded;
        }

        private void FilterSelectionPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get frame data for selected style (for aspect ratio)
                _currentFrameData = FrameDataProvider.GetFrameDataForStyle(App.SelectedStyle);
                
                if (_currentFrameData != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Frame data loaded: {_currentFrameData.Grid}, {_currentFrameData.PlaceholderW}x{_currentFrameData.PlaceholderH}");
                    if (_currentFrameData.PlaceholderW > 0 && _currentFrameData.PlaceholderH > 0)
                    {
                        _aspectRatio = (double)_currentFrameData.PlaceholderW / _currentFrameData.PlaceholderH;
                    }
                }

                // Check if webcam is active (LivePreviewControl initializes it)
                // Wait a bit for LivePreviewControl to initialize
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isWebcamActive = App.WebcamService != null && App.WebcamService.IsConnected;
                        System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Webcam active: {_isWebcamActive}");
                    });
                });

                // Update chip UI to reflect current state
                UpdateChipStates();

            // Fade in animations
                var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

                if (TitleSection != null)
            {
                    TitleSection.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
                
                // Speak instruction message
                SpeakTextAsync("If you want to choose filter, else click camera icon");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Load error: {ex.Message}");
            }
        }


        private void FilterSelectionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Resources cleaned up");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Cleanup error: {ex.Message}");
            }
        }


        private void UpdateChipStates()
        {
            try
            {
                var inactiveBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a1a1f"));
                var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF"));
                var activeBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f4257b"));
                var activeGlow = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#f4257b"),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };

                // Reset all chips to inactive state
                if (GlowChip != null)
                {
                    GlowChip.Background = inactiveBackground;
                    GlowChip.BorderBrush = inactiveBorder;
                    GlowChip.Effect = null;
                }
                
                if (MoodyChip != null)
                {
                    MoodyChip.Background = inactiveBackground;
                    MoodyChip.BorderBrush = inactiveBorder;
                    MoodyChip.Effect = null;
                }
                
                if (DreamyChip != null)
                {
                    DreamyChip.Background = inactiveBackground;
                    DreamyChip.BorderBrush = inactiveBorder;
                    DreamyChip.Effect = null;
                }
                
                if (FireChip != null)
                {
                    FireChip.Background = inactiveBackground;
                    FireChip.BorderBrush = inactiveBorder;
                    FireChip.Effect = null;
                }
                
                if (RetroChip != null)
                {
                    RetroChip.Background = inactiveBackground;
                    RetroChip.BorderBrush = inactiveBorder;
                    RetroChip.Effect = null;
                }
                
                if (MonoChip != null)
                {
                    MonoChip.Background = inactiveBackground;
                    MonoChip.BorderBrush = inactiveBorder;
                    MonoChip.Effect = null;
                }
                
                if (NormalChip != null)
        {
                    NormalChip.Background = inactiveBackground;
                    NormalChip.BorderBrush = inactiveBorder;
                    NormalChip.Effect = null;
                }

                // Activate current chip based on state
                // Glow: brightness = 1.6
                // Moody: brightness = 0.6
                // Dreamy: brightness = 0.95
                // Fire: brightness = 1.9
                // Retro: brightness = 1.2
                // Mono: grayscale = true
                // Normal: brightness = 1.0, grayscale = false

                const double tolerance = 0.1;

                if (App.Grayscale && MonoChip != null)
                {
                    MonoChip.Background = activeBackground;
                    MonoChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 1.9) < tolerance && FireChip != null)
                {
                    FireChip.Background = activeBackground;
                    FireChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 1.6) < tolerance && GlowChip != null)
                {
                    GlowChip.Background = activeBackground;
                    GlowChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 1.2) < tolerance && RetroChip != null)
                {
                    RetroChip.Background = activeBackground;
                    RetroChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 1.0) < tolerance && !App.Grayscale && NormalChip != null)
                {
                    NormalChip.Background = activeBackground;
                    NormalChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 0.95) < tolerance && DreamyChip != null)
                {
                    DreamyChip.Background = activeBackground;
                    DreamyChip.Effect = activeGlow;
                }
                else if (Math.Abs(App.Brightness - 0.6) < tolerance && MoodyChip != null)
                {
                    MoodyChip.Background = activeBackground;
                    MoodyChip.Effect = activeGlow;
                }
                else if (GlowChip != null) // Default to Glow
                {
                    GlowChip.Background = activeBackground;
                    GlowChip.Effect = activeGlow;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Error updating chip states: {ex.Message}");
            }
        }

        private void GlowChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 1.6; // Bright and vibrant
                UpdateChipStates();
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Glow filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Glow chip error: {ex.Message}");
            }
        }

        private void MoodyChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 0.6; // Dark and moody
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Moody filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Moody chip error: {ex.Message}");
            }
        }

        private void DreamyChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 0.95; // Soft and dreamy (slightly dimmed)
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Dreamy filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Dreamy chip error: {ex.Message}");
            }
        }

        private void FireChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 1.9; // Warm and fiery
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Fire filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Fire chip error: {ex.Message}");
            }
        }

        private void RetroChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 1.2; // Retro vibe with slight brightness
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Retro filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Retro chip error: {ex.Message}");
            }
        }

        private void MonoChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = true;
                App.Brightness = 1.0; // Monochrome
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Mono filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Mono chip error: {ex.Message}");
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reset to default values (Normal filter)
                App.Brightness = 1.0;
                App.Grayscale = false;

                // Update chip states
                UpdateChipStates();

                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Filters reset to defaults (Normal)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Reset error: {ex.Message}");
            }
        }

        private void NormalChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                App.Grayscale = false;
                App.Brightness = 1.0; // Normal, no filter
                UpdateChipStates();
                
                if (!_isWebcamActive)
                {
                    UpdatePreview();
                }
                
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Normal filter applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Normal chip error: {ex.Message}");
            }
        }

        private void UpdatePreview()
        {
            // LivePreviewControl automatically updates when filters change
            // This method is kept for compatibility but doesn't need to do anything
            // since the LivePreviewControl subscribes to frame events and applies filters in real-time
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
        {
            _navigationService.NavigateTo(typeof(StyleSelectionPage));
        }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Back navigation error: {ex.Message}");
                MessageBox.Show($"Navigation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] CaptureButton_Click - Starting navigation to CapturePage");
                _navigationService.NavigateTo(typeof(CapturePage));
                System.Diagnostics.Debug.WriteLine("[FilterSelectionPage] Navigation command sent");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Navigation error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FilterSelectionPage] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Navigation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
