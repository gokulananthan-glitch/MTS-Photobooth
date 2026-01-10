using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Speech.Synthesis;
using PhotoBooth.Services;
using PhotoBooth.Models;

namespace PhotoBooth.Pages
{
    public partial class ImageReviewPage : Page
    {
        private readonly NavigationService _navigationService;
        private int _selectedImageIndex = 0;
        private HashSet<int> _approvedImages = new HashSet<int>();
        private FrameData? _currentFrameData;

        public ImageReviewPage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            
            Loaded += ImageReviewPage_Loaded;
            Unloaded += ImageReviewPage_Unloaded;
        }
        
        private void ImageReviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clear preview to release memory
            SelectedImage.Source = null;
            
            // Clear thumbnails
            ThumbnailsPanel.Children.Clear();
        }

        private void ImageReviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Get frame data for selected style (for thumbnail aspect ratio only)
            _currentFrameData = FrameDataProvider.GetFrameDataForStyle(App.SelectedStyle);
            
            // Populate thumbnails
            LoadThumbnails();
            
            // Show first image by default
            if (App.CapturedImages.Count > 0)
            {
                SelectImage(0);
            }

            // Fade in animations with easing
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Animate progress text if it exists
            if (ProgressText != null)
            {
                ProgressText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            
            // Speak instruction message
            SpeakTextAsync("Review captured photos to retake");
        }

        private void LoadThumbnails()
        {
            ThumbnailsPanel.Children.Clear();

            // Calculate thumbnail dimensions based on frame aspect ratio (responsive)
            double thumbnailWidth = 100;
            double thumbnailHeight = 140;
            
            if (_currentFrameData != null)
            {
                double aspectRatio = (double)_currentFrameData.FrameW / _currentFrameData.FrameH;
                thumbnailHeight = thumbnailWidth / aspectRatio;
            }

            // Only create thumbnails for captured images - no empty placeholders
            for (int i = 0; i < App.CapturedImages.Count; i++)
            {
                int index = i; // Capture for closure
                
                // Create thumbnail container with responsive aspect ratio
                var thumbnailBorder = new Border
                {
                    Width = thumbnailWidth,
                    Height = thumbnailHeight,
                    Margin = new Thickness(6, 0, 6, 0),
                    Padding = new Thickness(3),
                    BorderThickness = new Thickness(2),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 49, 46)), // #32312E border color
                    CornerRadius = new CornerRadius(10),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = index,
                    ClipToBounds = true
                };

                var overlayGrid = new Grid();

                // Has image with responsive sizing
                var thumbnailImage = new Image
                {
                    Source = App.CapturedImages[i],
                    Stretch = Stretch.UniformToFill,
                    Width = thumbnailWidth,
                    Height = thumbnailHeight,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                overlayGrid.Children.Add(thumbnailImage);

                // Green checkmark for approved images
                if (_approvedImages.Contains(i))
                {
                    var checkmarkBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green #10B981
                        CornerRadius = new CornerRadius(8),
                        Width = 20,
                        Height = 20,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 4, 4, 0)
                    };
                    var checkmark = new TextBlock
                    {
                        Text = "✓",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    checkmarkBorder.Child = checkmark;
                    overlayGrid.Children.Add(checkmarkBorder);
                }

                // Click handler
                thumbnailBorder.MouseLeftButtonDown += (s, e) =>
                {
                    SelectImage(index);
                };

                thumbnailBorder.Child = overlayGrid;

                // Hover effects
                thumbnailBorder.MouseEnter += (s, e) =>
                {
                    if ((int)thumbnailBorder.Tag != _selectedImageIndex)
                    {
                        thumbnailBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    }
                };

                thumbnailBorder.MouseLeave += (s, e) =>
                {
                    if ((int)thumbnailBorder.Tag != _selectedImageIndex)
                    {
                        thumbnailBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 49, 46)); // #32312E
                    }
                };

                ThumbnailsPanel.Children.Add(thumbnailBorder);
            }
            
            // Update selected thumbnail border
            if (App.CapturedImages.Count > 0)
            {
                SelectImage(_selectedImageIndex);
            }
        }

        private void SelectImage(int index)
        {
            if (index < 0 || index >= App.CapturedImages.Count)
                return;

            _selectedImageIndex = index;

            // Update preview
            SelectedImage.Source = App.CapturedImages[index];
            
            // Update progress text
            if (ProgressText != null)
            {
                ProgressText.Text = $"Snap {index + 1} of {App.CapturedImages.Count}";
            }

            // Update thumbnail borders and effects with golden theme
            foreach (Border border in ThumbnailsPanel.Children)
            {
                int thumbIndex = (int)border.Tag;
                if (thumbIndex == index && thumbIndex < App.CapturedImages.Count)
                {
                    // Selected thumbnail - golden border with glow
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8)); // #EAB308
                    border.BorderThickness = new Thickness(3);
                    border.Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(234, 179, 8),
                        BlurRadius = 15,
                        ShadowDepth = 0,
                        Opacity = 0.6
                    };
                }
                else if (thumbIndex < App.CapturedImages.Count)
                {
                    // Unselected thumbnails
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 49, 46)); // #32312E
                    border.BorderThickness = new Thickness(2);
                    border.Effect = null;
                }
            }
        }

        private void RetakeButton_Click(object sender, RoutedEventArgs e)
        {
            // Store which image index needs to be retaken
            App.RetakePhotoIndex = _selectedImageIndex;
            
            // Go back to capture page to retake only this specific photo
            _navigationService.NavigateTo(typeof(CapturePage));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all images and go back to capture
            App.CapturedImages.Clear();
            _navigationService.NavigateTo(typeof(CapturePage));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Approve current image
            if (_selectedImageIndex < App.CapturedImages.Count)
            {
                _approvedImages.Add(_selectedImageIndex);
                
                // Update thumbnail to show checkmark
                LoadThumbnails();
            }
            
            // Move to next image or continue to frame selection
            if (_selectedImageIndex < App.CapturedImages.Count - 1)
            {
                SelectImage(_selectedImageIndex + 1);
            }
            else
            {
                // All images reviewed, continue to frame selection
                _navigationService.NavigateTo(typeof(FrameSelectionPage));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to start or capture page
            _navigationService.NavigateTo(typeof(StartPage));
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

