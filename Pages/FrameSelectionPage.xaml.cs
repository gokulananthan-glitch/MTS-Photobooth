using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;
using static PhotoBooth.Services.ConfigService;

namespace PhotoBooth.Pages
{
    public partial class FrameSelectionPage : Page
    {
        private readonly NavigationService _navigationService;
        private readonly ApiService _apiService;
        private readonly DatabaseService _dbService;
        private readonly AppConfig _config;
        private string? _generatedFramePath;
        private List<FrameTemplate> _frameTemplates = new();
        private ObservableCollection<FrameItemViewModel> _frameItems = new();
        private FrameItemViewModel? _selectedFrame;

        public FrameSelectionPage()
        {
            InitializeComponent();
            
            _config = GetConfig();
            _apiService = new ApiService(_config.ApiSettings.BaseUrl, _config.ApiSettings.TimeoutSeconds);
            _dbService = new DatabaseService();
            _navigationService = App.NavigationService;
            
            Loaded += FrameSelectionPage_Loaded;
            Unloaded += FrameSelectionPage_Unloaded;
        }

        private void FrameSelectionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear image sources to release memory
                FramePreviewImage.Source = null;
                
                // Clear frame items and their thumbnails
                foreach (var item in _frameItems)
                {
                    item.ThumbnailSource = null;
                }
                _frameItems.Clear();
                
                // Clear frame list control
                FrameListControl.ItemsSource = null;
                
                // DON'T delete the generated frame file here!
                // It's needed by PrintPage. The periodic maintenance timer
                // will clean up old temp files after 1 hour, or PrintPage
                // will delete it after successful save.
                // This prevents the race condition where the file is deleted
                // before PrintPage can copy it.
                
                // Clear frame templates list
                _frameTemplates.Clear();
                
                System.Diagnostics.Debug.WriteLine("[FrameSelectionPage] Resources cleaned up (temp frame preserved for printing)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Cleanup error: {ex.Message}");
            }
        }

        private async void FrameSelectionPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                LoadingText.Text = "Loading Frames...";

                // Load frame templates
                await LoadFrameTemplatesAsync();

                // Build frame list for display
                BuildFrameList();

                // Auto-select first frame by default
                if (_frameItems.Count > 0)
                {
                    var firstFrame = _frameItems[0];
                    firstFrame.IsSelected = true;
                    _selectedFrame = firstFrame;
                    
                    // Generate preview for first frame
                    await GeneratePreviewAsync(firstFrame);
                    
                    // Enable continue button
                    ContinueButton.IsEnabled = true;
                    
                    // Update selected frame name
                    SelectedFrameNameText.Text = firstFrame.DisplayName;
                    SelectedFrameNameText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                }

                // Fade in animations
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                // ContentGrid and NavigationGrid will fade in

                var fadeInDelayed = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                    BeginTime = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                ContentGrid.BeginAnimation(UIElement.OpacityProperty, fadeInDelayed);
                NavigationGrid.BeginAnimation(UIElement.OpacityProperty, fadeInDelayed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error: {ex.Message}");
                MessageBox.Show($"Error loading page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async System.Threading.Tasks.Task LoadFrameTemplatesAsync()
        {
            try
            {
                var savedFrames = await _dbService.GetFrameTemplatesAsync();
                var savedTimestamp = await _dbService.GetFramesSavedTimestampAsync();

                if (App.CurrentMachineConfig != null && App.CurrentMachineConfig.OfflineMode)
                {
                    System.Diagnostics.Debug.WriteLine("[FrameSelectionPage] Offline mode enabled, using saved frames");
                    _frameTemplates = savedFrames;

                    if (savedTimestamp.HasValue)
                    {
                        var timeSinceSave = DateTime.UtcNow - savedTimestamp.Value;
                        if (timeSinceSave.TotalMinutes >= 30)
                        {
                            var apiFrames = await _apiService.GetFrameTemplatesAsync(_config.ApiSettings.MachineCode);
                            if (apiFrames != null && apiFrames.Count > 0)
                            {
                                await _dbService.SaveFrameTemplatesAsync(apiFrames);
                                _frameTemplates = apiFrames;
                                System.Diagnostics.Debug.WriteLine("[FrameSelectionPage] Frames synced successfully");
                            }
                        }
                    }
                }
                else
                {
                    var apiFrames = await _apiService.GetFrameTemplatesAsync(_config.ApiSettings.MachineCode);

                    if (apiFrames != null && apiFrames.Count > 0)
                    {
                        await _dbService.SaveFrameTemplatesAsync(apiFrames);
                        _frameTemplates = apiFrames;
                    }
                    else if (savedFrames.Count > 0)
                    {
                        _frameTemplates = savedFrames;
                    }
                }

                // Filter frames for the current style
                string gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);
                _frameTemplates = _frameTemplates
                    .Where(f => f.Status == "active" && 
                               (f.Frame.Equals(gridType, StringComparison.OrdinalIgnoreCase) ||
                                f.Frame.StartsWith(gridType, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Loaded {_frameTemplates.Count} frame templates for style {App.SelectedStyle} (grid: {gridType})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error loading frames: {ex.Message}");
            }
        }

        private void BuildFrameList()
        {
            _frameItems.Clear();

            if (_frameTemplates.Count == 0)
            {
                // Add a default "no frame" option
                var noFrameItem = new FrameItemViewModel
                {
                    FrameTemplate = null,
                    DisplayName = "No Frame",
                    IsSelected = false
                };
                _frameItems.Add(noFrameItem);
            }
            else
            {
                // Add all available frames
                for (int i = 0; i < _frameTemplates.Count; i++)
                {
                    var template = _frameTemplates[i];
                    var frameItem = new FrameItemViewModel
                    {
                        FrameTemplate = template,
                        DisplayName = FormatFrameName(template.Frame, i + 1),
                        IsSelected = false
                    };

                    // Load thumbnail
                    try
                    {
                        frameItem.ThumbnailSource = LoadFrameImage(template);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error loading thumbnail: {ex.Message}");
                    }

                    _frameItems.Add(frameItem);
                }
            }

            FrameListControl.ItemsSource = _frameItems;
        }

        private string FormatFrameName(string frameName, int index)
        {
            // Format frame name for display
            if (string.IsNullOrEmpty(frameName))
                return $"Frame {index}";

            // Remove "grid" prefix and capitalize
            if (frameName.StartsWith("grid", StringComparison.OrdinalIgnoreCase))
            {
                return $"Frame Design {index}";
            }

            return frameName;
        }

        private BitmapImage? LoadFrameImage(FrameTemplate template)
        {
            try
            {
                if (string.IsNullOrEmpty(template.Image))
                    return null;

                BitmapImage image;

                if (template.Image.StartsWith("data:image") || template.Image.Length > 500)
                {
                    // Base64 image
                    string base64Data = template.Image;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }

                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    image = new BitmapImage();
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        ms.Position = 0;
                        image.BeginInit();
                        image.StreamSource = ms;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                    }
                    image.Freeze();
                }
                else
                {
                    // URL image
                    image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(template.Image);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                }

                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error loading frame image: {ex.Message}");
                return null;
            }
        }

        private async void FrameItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is FrameItemViewModel frameItem)
            {
                // Deselect previous frame
                if (_selectedFrame != null)
                {
                    _selectedFrame.IsSelected = false;
                }

                // Select new frame
                frameItem.IsSelected = true;
                _selectedFrame = frameItem;

                // Update preview
                await GeneratePreviewAsync(frameItem);

                // Enable continue button
                ContinueButton.IsEnabled = true;

                // Update selected frame name
                SelectedFrameNameText.Text = frameItem.DisplayName;
                SelectedFrameNameText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            }
        }

        private async System.Threading.Tasks.Task GeneratePreviewAsync(FrameItemViewModel frameItem)
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                LoadingText.Text = "Generating Preview...";

                if (App.CapturedImages.Count == 0)
                {
                    MessageBox.Show("No images to generate frame!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Generate base frame with captured images
                string gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);
                string tempPath = Path.Combine(Path.GetTempPath(), $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                string basePath = FrameGenerator.GenerateFrame(App.CapturedImages, gridType, tempPath);

                // Load base image
                var baseImage = new BitmapImage();
                baseImage.BeginInit();
                baseImage.UriSource = new Uri(basePath);
                baseImage.CacheOption = BitmapCacheOption.OnLoad;
                baseImage.EndInit();

                if (frameItem.FrameTemplate != null && frameItem.ThumbnailSource != null)
                {
                    // Composite frame template over base image
                    var compositeImage = ImageProcessor.CompositeFrameOverImage(baseImage, frameItem.ThumbnailSource);

                    if (compositeImage != null)
                    {
                        FramePreviewImage.Source = compositeImage;

                        // Save composite as the final frame
                        string compositePath = Path.Combine(Path.GetTempPath(), $"frame_composite_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        SaveBitmapImage(compositeImage, compositePath);
                        _generatedFramePath = compositePath;
                    }
                    else
                    {
                        FramePreviewImage.Source = baseImage;
                        _generatedFramePath = basePath;
                    }
                }
                else
                {
                    // No frame template, just show base image
                    FramePreviewImage.Source = baseImage;
                    _generatedFramePath = basePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error generating preview: {ex.Message}");
                MessageBox.Show($"Error generating preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveBitmapImage(BitmapImage image, string filePath)
        {
            try
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSelectionPage] Error saving composite: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.NavigateTo(typeof(ImageReviewPage));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_generatedFramePath) || !File.Exists(_generatedFramePath))
            {
                MessageBox.Show("Please select a frame first!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Store temp frame path for PrintPage to use (cleanup happens on StartPage)
            App.TempFramePath = _generatedFramePath;
            
            _navigationService.NavigateTo(new PrintPage(_generatedFramePath));
        }
    }

    // ViewModel for frame list items
    public class FrameItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public FrameTemplate? FrameTemplate { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public BitmapImage? ThumbnailSource { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
