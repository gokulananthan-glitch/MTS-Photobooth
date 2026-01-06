using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Speech.Synthesis;
using PhotoBooth.Services;
using PhotoBooth.Models;
using static PhotoBooth.Services.ConfigService;

namespace PhotoBooth.Pages
{
    public partial class StyleSelectionPage : Page
    {
        private readonly NavigationService _navigationService;
        private readonly DatabaseService _dbService;
        private Button? _selectedStyleButton;
        private TextBlock? _photoCountBadge;

        public StyleSelectionPage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            _dbService = new DatabaseService();
            Loaded += StyleSelectionPage_Loaded;
        }

        private async void StyleSelectionPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Load machine config from database if not already loaded
            if (App.CurrentMachineConfig == null)
            {
                var savedConfig = await _dbService.GetMachineConfigAsync();
                if (savedConfig != null)
                {
                    App.CurrentMachineConfig = savedConfig;
                    System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Machine config loaded from database. SupportedFrames count: {savedConfig.SupportedFrames?.Count ?? 0}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] No machine config found in database.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Machine config already loaded. SupportedFrames count: {App.CurrentMachineConfig.SupportedFrames?.Count ?? 0}");
            }

            // Filter style buttons based on supported frames from machine config
            FilterStyleButtons();

            // Set default style to 2 (Dynamic Duo) if not set
            if (App.SelectedStyle == 0)
            {
                App.SelectedStyle = 2;
            }

            // Ensure selected style is supported, if not select first visible style
            if (!IsStyleSupported(App.SelectedStyle))
            {
                var firstSupportedStyle = GetFirstSupportedStyle();
                if (firstSupportedStyle > 0)
                {
                    App.SelectedStyle = firstSupportedStyle;
                }
            }

            // Find and highlight the selected style button
            UpdateSelectedStyle(App.SelectedStyle);

            // Update photo count badge
            UpdatePhotoCountBadge();

            // Fade in animations with easing
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new System.Windows.Duration(TimeSpan.FromSeconds(0.8)),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            if (TitleSection != null)
            {
                TitleSection.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            
            var fadeInDelayed = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new System.Windows.Duration(TimeSpan.FromSeconds(0.8)),
                BeginTime = TimeSpan.FromSeconds(0.2),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            if (StyleButtonsGrid != null)
            {
                StyleButtonsGrid.BeginAnimation(UIElement.OpacityProperty, fadeInDelayed);
            }
            
            if (BackButton != null)
            {
                BackButton.BeginAnimation(UIElement.OpacityProperty, fadeInDelayed);
            }
            
            // Speak instruction message
            SpeakTextAsync("Choose style as per your choice");
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

        private void UpdateSelectedStyle(int style)
        {
            // Reset all buttons
            foreach (var child in StyleButtonsGrid.Children)
            {
                if (child is Button btn && btn.Tag != null)
                {
                    var border = FindVisualChild<Border>(btn);
                    if (border != null)
                    {
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF
                        border.BorderThickness = new Thickness(1);
                        border.Effect = null;
                        border.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)); // #1AFFFFFF
                    }

                    // Hide checkmark
                    var checkMark = FindVisualChildByName<FrameworkElement>(btn, "CheckMark");
                    if (checkMark != null)
                    {
                        checkMark.Opacity = 0;
                    }

                    // Reset title color
                    var titleText = FindVisualChild<TextBlock>(btn);
                    if (titleText != null && titleText.Text != null)
                    {
                        titleText.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
            }

            // Highlight selected button
            foreach (var child in StyleButtonsGrid.Children)
            {
                if (child is Button btn && btn.Tag != null)
                {
                    string styleStr = btn.Tag.ToString() ?? "1";
                    if (int.TryParse(styleStr, out int btnStyle) && btnStyle == style)
                    {
                        _selectedStyleButton = btn;
                        var border = FindVisualChild<Border>(btn);
                        if (border != null)
                        {
                            border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 244, 37, 123)); // #f4257b
                            border.BorderThickness = new Thickness(2);
                            border.Effect = new DropShadowEffect
                            {
                                Color = Color.FromArgb(255, 244, 37, 123),
                                BlurRadius = 20,
                                ShadowDepth = 0,
                                Opacity = 0.6
                            };
                        }

                        // Show checkmark
                        var checkMark = FindVisualChildByName<FrameworkElement>(btn, "CheckMark");
                        if (checkMark != null)
                        {
                            checkMark.Opacity = 1;
                        }

                        // Set title color to pink
                        var stackPanel = FindVisualChild<StackPanel>(btn);
                        if (stackPanel != null)
                        {
                            foreach (var item in stackPanel.Children)
                            {
                                if (item is TextBlock tb && tb.FontSize == 22)
                                {
                                    tb.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 37, 123)); // #f4257b
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void UpdatePhotoCountBadge()
        {
            int photoCount = FrameDataProvider.GetPhotoCountForStyle(App.SelectedStyle);
            
            // Find the photo count badge in StartSnapButton
            if (StartSnapButton != null)
            {
                var stackPanel = FindVisualChild<StackPanel>(StartSnapButton);
                if (stackPanel != null)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is Border border && border.Background is SolidColorBrush brush && brush.Color == Colors.White)
                        {
                            var textBlock = FindVisualChild<TextBlock>(border);
                            if (textBlock != null)
                            {
                                textBlock.Text = photoCount.ToString();
                                _photoCountBadge = textBlock;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void StyleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag != null)
                {
                    string styleStr = button.Tag.ToString() ?? "1";
                    if (int.TryParse(styleStr, out int style))
                    {
                        App.SelectedStyle = style;
                        System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Selected style: {style}");
                        UpdateSelectedStyle(style);
                        UpdatePhotoCountBadge();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Invalid style value: {styleStr}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Error: {ex.Message}");
                MessageBox.Show($"Error selecting style: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartSnapButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button and show spinner
                StartSnapButton.IsEnabled = false;
                ShowLoadingSpinner(true);

                if (App.SelectedStyle == 0)
                {
                    App.SelectedStyle = 2; // Default to Style 2
                }
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Starting with style: {App.SelectedStyle}");
                
                // Get machine config
                if (App.CurrentMachineConfig == null)
                {
                    System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] WARNING: CurrentMachineConfig is null! Config not loaded from database.");
                    MessageBox.Show("Machine configuration not loaded. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowLoadingSpinner(false);
                    StartSnapButton.IsEnabled = true;
                    return;
                }

                bool offlineMode = App.CurrentMachineConfig.OfflineMode;
                string paymentType = App.CurrentMachineConfig.PaymentType ?? "";
                
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Machine config loaded - offline_mode: {offlineMode}, paymentType: \"{paymentType}\"");
                
                // Explicit check: If offline mode is true, skip payment page
                if (offlineMode)
                {
                    System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] Machine is in OFFLINE MODE - Payment page will be skipped");
                }

                // If machine is online (not offline mode), create pending transaction
                if (!offlineMode)
                {
                    try
                    {
                        // Get grid type and frame count
                        string gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);
                        // Extract number from grid type (e.g., "grid2" -> 2, "grid4" -> 4)
                        int frameCount = 0;
                        if (gridType.StartsWith("grid", StringComparison.OrdinalIgnoreCase))
                        {
                            string numberPart = gridType.Substring(4);
                            int.TryParse(numberPart, out frameCount);
                        }

                        // Get amount from supported frames
                        double amount = 0;
                        if (App.CurrentMachineConfig.SupportedFrames != null)
                        {
                            var supportedFrame = App.CurrentMachineConfig.SupportedFrames
                                .FirstOrDefault(f => f.Type.Equals(gridType, StringComparison.OrdinalIgnoreCase));
                            
                            if (supportedFrame != null && !string.IsNullOrEmpty(supportedFrame.Amount))
                            {
                                double.TryParse(supportedFrame.Amount, out amount);
                            }
                        }

                        // Get machine code and site code with proper fallback
                        var machineCode = !string.IsNullOrWhiteSpace(App.CurrentMachineConfig.MachineCode) 
                            ? App.CurrentMachineConfig.MachineCode 
                            : null;
                        var siteCode = !string.IsNullOrWhiteSpace(App.CurrentMachineConfig.SiteCode) 
                            ? App.CurrentMachineConfig.SiteCode 
                            : null;

                        // Fallback to database app settings if not in machine config
                        if (string.IsNullOrWhiteSpace(machineCode))
                        {
                            machineCode = await _dbService.GetAppSettingAsync("MachineCode");
                        }
                        if (string.IsNullOrWhiteSpace(siteCode))
                        {
                            siteCode = await _dbService.GetAppSettingAsync("SiteCode");
                        }

                        // Final fallback to config file
                        if (string.IsNullOrWhiteSpace(machineCode) || string.IsNullOrWhiteSpace(siteCode))
                        {
                            var fallbackConfig = GetConfig();
                            machineCode = machineCode ?? fallbackConfig.ApiSettings.MachineCode;
                            siteCode = siteCode ?? fallbackConfig.ApiSettings.SiteCode;
                        }

                        // Validate we have both codes
                        if (string.IsNullOrWhiteSpace(machineCode) || string.IsNullOrWhiteSpace(siteCode))
                        {
                            System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] ERROR: MachineCode or SiteCode is still empty after all fallbacks. MachineCode: '{machineCode}', SiteCode: '{siteCode}'");
                            MessageBox.Show("Machine code or site code is not configured. Please update codes in settings.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Creating pending transaction: frameCount={frameCount}, amount={amount}, machineCode={machineCode}, siteCode={siteCode}");

                        // Create API service and call create pending transaction
                        var config = GetConfig();
                        var apiService = new ApiService(config.ApiSettings.BaseUrl, config.ApiSettings.TimeoutSeconds);
                        
                        var transactionData = await apiService.CreatePendingSaleTransactionAsync(
                            frameCount, 
                            amount, 
                            machineCode, 
                            siteCode
                        );

                        if (transactionData != null)
                        {
                            // Store transaction data
                            App.PendingTransactionData = transactionData;
                            System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Pending transaction created: OrderId={transactionData.OrderId}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] Failed to create pending transaction");
                            ShowLoadingSpinner(false);
                            StartSnapButton.IsEnabled = true;
                            MessageBox.Show("Failed to initiate transaction. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Error creating pending transaction: {apiEx.Message}");
                        ShowLoadingSpinner(false);
                        StartSnapButton.IsEnabled = true;
                        MessageBox.Show($"Error initiating transaction: {apiEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Hide spinner before navigation
                ShowLoadingSpinner(false);

                // Navigate based on payment type and offline mode
                // Payment page should NOT show if:
                // 1. Machine is in offline mode, OR
                // 2. Payment type is "N" (No payment)
                bool shouldShowPayment = !offlineMode && paymentType != "N";
                
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Navigation decision - offlineMode: {offlineMode}, paymentType: '{paymentType}', shouldShowPayment: {shouldShowPayment}");

                if (shouldShowPayment)
                {
                    System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] Navigating to PaymentPage");
                    _navigationService.NavigateTo(typeof(PaymentPage));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] Navigating to FilterSelectionPage (Camera)");
                    _navigationService.NavigateTo(typeof(FilterSelectionPage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Error: {ex.Message}");
                ShowLoadingSpinner(false);
                StartSnapButton.IsEnabled = true;
                MessageBox.Show($"Error starting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowLoadingSpinner(bool show)
        {
            try
            {
                // Find the button template parts
                var buttonTemplate = StartSnapButton.Template;
                if (buttonTemplate != null)
                {
                    var normalContent = buttonTemplate.FindName("NormalContent", StartSnapButton) as FrameworkElement;
                    var loadingContent = buttonTemplate.FindName("LoadingContent", StartSnapButton) as FrameworkElement;
                    var spinnerRotate = buttonTemplate.FindName("SpinnerRotate", StartSnapButton) as RotateTransform;

                    if (show)
                    {
                        if (normalContent != null) normalContent.Visibility = Visibility.Collapsed;
                        if (loadingContent != null) loadingContent.Visibility = Visibility.Visible;
                        
                        // Start spinner animation
                        if (spinnerRotate != null)
                        {
                            var rotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 0,
                                To = 360,
                                Duration = new System.Windows.Duration(TimeSpan.FromSeconds(1)),
                                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                            };
                            spinnerRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
                        }
                    }
                    else
                    {
                        if (normalContent != null) normalContent.Visibility = Visibility.Visible;
                        if (loadingContent != null) loadingContent.Visibility = Visibility.Collapsed;
                        
                        // Stop spinner animation
                        if (spinnerRotate != null)
                        {
                            spinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Error showing spinner: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.NavigateTo(typeof(StartPage));
        }

        private void FilterStyleButtons()
        {
            if (App.CurrentMachineConfig == null || App.CurrentMachineConfig.SupportedFrames == null || App.CurrentMachineConfig.SupportedFrames.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[StyleSelectionPage] No machine config or supported frames - showing all styles");
                // If no config, show all buttons (default behavior)
                return;
            }

            // Get list of supported grid types from machine config
            var supportedGridTypes = App.CurrentMachineConfig.SupportedFrames
                .Select(sf => sf.Type.ToLower())
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Supported grid types: {string.Join(", ", supportedGridTypes)}");

            // Filter style buttons based on supported frames
            foreach (var child in StyleButtonsGrid.Children)
            {
                if (child is Button btn && btn.Tag != null)
                {
                    string styleStr = btn.Tag.ToString() ?? "1";
                    if (int.TryParse(styleStr, out int styleNumber))
                    {
                        // Get grid type for this style
                        string gridType = FrameDataProvider.GetGridForStyle(styleNumber);
                        
                        // Check if this grid type is supported
                        bool isSupported = supportedGridTypes.Contains(gridType.ToLower());
                        
                        // Show/hide button based on support
                        btn.Visibility = isSupported ? Visibility.Visible : Visibility.Collapsed;
                        
                        System.Diagnostics.Debug.WriteLine($"[StyleSelectionPage] Style {styleNumber} (grid: {gridType}) - Visible: {isSupported}");
                    }
                }
            }
        }

        private bool IsStyleSupported(int styleNumber)
        {
            if (App.CurrentMachineConfig == null || App.CurrentMachineConfig.SupportedFrames == null || App.CurrentMachineConfig.SupportedFrames.Count == 0)
            {
                return true; // If no config, assume all styles are supported
            }

            string gridType = FrameDataProvider.GetGridForStyle(styleNumber);
            return App.CurrentMachineConfig.SupportedFrames
                .Any(sf => sf.Type.Equals(gridType, StringComparison.OrdinalIgnoreCase));
        }

        private int GetFirstSupportedStyle()
        {
            // Check styles in order: 1, 2, 3, 4, 5
            for (int style = 1; style <= 5; style++)
            {
                if (IsStyleSupported(style))
                {
                    // Also check if the button is visible
                    var button = FindStyleButton(style);
                    if (button != null && button.Visibility == Visibility.Visible)
                    {
                        return style;
                    }
                }
            }
            return 0; // No supported style found
        }

        private Button? FindStyleButton(int styleNumber)
        {
            foreach (var child in StyleButtonsGrid.Children)
            {
                if (child is Button btn && btn.Tag != null)
                {
                    string styleStr = btn.Tag.ToString() ?? "1";
                    if (int.TryParse(styleStr, out int btnStyle) && btnStyle == styleNumber)
                    {
                        return btn;
                    }
                }
            }
            return null;
        }

        // Helper methods to find visual children
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                var childOfChild = FindVisualChildByName<T>(child, name);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}

