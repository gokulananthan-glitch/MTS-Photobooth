using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;
using static PhotoBooth.Services.ConfigService;
using System.Speech.Synthesis;

namespace PhotoBooth.Pages
{
    public partial class PaymentPage : Page
    {
        private readonly NavigationService _navigationService;
        private readonly ApiService? _apiService;
        private readonly DatabaseService _dbService;
        private int _numberOfCopies = 1;
        private string _frameAmount = "0";
        private string _gridType = "";
        private bool _isLoading = false;
        private bool _cashPaymentVisible = false;
        private bool _upiPaymentVisible = false;

        public PaymentPage()
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            _dbService = new DatabaseService();
            
            // Initialize API service if base URL is available
            var config = GetConfig();
            var baseUrl = config.ApiSettings.BaseUrl;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _apiService = new ApiService(baseUrl, config.ApiSettings.TimeoutSeconds);
            }
            
            Loaded += PaymentPage_Loaded;
        }

        private async void PaymentPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get grid type for selected style
                _gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);
                
                // Get frame amount from machine config
                if (App.CurrentMachineConfig != null && App.CurrentMachineConfig.SupportedFrames != null)
                {
                    var supportedFrame = App.CurrentMachineConfig.SupportedFrames
                        .FirstOrDefault(f => f.Type == _gridType);
                    
                    if (supportedFrame != null && !string.IsNullOrEmpty(supportedFrame.Amount))
                    {
                        _frameAmount = supportedFrame.Amount;
                    }
                }
                
                // Update UI
                UpdatePriceInfo();
                UpdatePayButtonText();
                
                // Set payment type visibility
                if (App.CurrentMachineConfig != null && !string.IsNullOrEmpty(App.CurrentMachineConfig.PaymentType))
                {
                    string paymentType = App.CurrentMachineConfig.PaymentType;
                    _cashPaymentVisible = paymentType == "A" || paymentType == "C";
                    _upiPaymentVisible = paymentType == "A" || paymentType == "U";
                    
                    // Update UI visibility directly
                    CashPaymentButton.Visibility = _cashPaymentVisible ? Visibility.Visible : Visibility.Collapsed;
                    UpiPaymentButton.Visibility = _upiPaymentVisible ? Visibility.Visible : Visibility.Collapsed;
                }
                
                // Load frame preview image (if available)
                LoadFramePreview();
                
                // Speak TTS message
                await Task.Delay(1000);
                await SpeakTextAsync($"You have selected {GetStyleName()} image frame. Please select the number of copies you need.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error: {ex.Message}");
            }
        }

        private string GetStyleName()
        {
            return App.SelectedStyle switch
            {
                1 => "Solo Shot",
                2 => "Dynamic Duo",
                3 => "Quad Grid",
                4 => "Six Pack",
                5 => "Double Column",
                _ => "Solo Shot"
            };
        }

        private string GetStyleDescription()
        {
            return App.SelectedStyle switch
            {
                1 => "1 Frame • Classic",
                2 => "2 Frames • Vertical",
                3 => "4 Frames • Grid",
                4 => "6 Frames • Horizontal",
                5 => "4 Frames • Duplicated",
                _ => "1 Frame • Classic"
            };
        }

        private void LoadFramePreview()
        {
            try
            {
                // Update style name and description
                StyleNameText.Text = GetStyleName();
                StyleDescriptionText.Text = GetStyleDescription();
                
                // Create preview layout based on selected style
                FrameworkElement previewLayout = CreateStylePreview(App.SelectedStyle);
                StylePreviewViewbox.Child = previewLayout;
                
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Loading preview for style {App.SelectedStyle}, grid: {_gridType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error loading preview: {ex.Message}");
            }
        }

        private FrameworkElement CreateStylePreview(int styleNumber)
        {
            return styleNumber switch
            {
                1 => CreateStyle1Preview(),
                2 => CreateStyle2Preview(),
                3 => CreateStyle3Preview(),
                4 => CreateStyle4Preview(),
                5 => CreateStyle5Preview(),
                _ => CreateStyle1Preview()
            };
        }

        private FrameworkElement CreateStyle1Preview()
        {
            var border = new Border
            {
                Width = 120,
                Height = 160,
                CornerRadius = new CornerRadius(12)
            };
            border.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(99, 102, 241), 0),
                    new GradientStop(Color.FromRgb(236, 72, 153), 1)
                },
                new Point(0, 0),
                new Point(1, 1)
            );
            return border;
        }

        private FrameworkElement CreateStyle2Preview()
        {
            var stackPanel = new StackPanel { Width = 100, Height = 200 };
            
            var border1 = new Border
            {
                Height = 90,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            border1.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(99, 102, 241), 0),
                    new GradientStop(Color.FromRgb(139, 92, 246), 1)
                },
                new Point(0, 0),
                new Point(1, 1)
            );
            
            var border2 = new Border
            {
                Height = 90,
                CornerRadius = new CornerRadius(8)
            };
            border2.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(236, 72, 153), 0),
                    new GradientStop(Color.FromRgb(244, 63, 94), 1)
                },
                new Point(0, 0),
                new Point(1, 1)
            );
            
            stackPanel.Children.Add(border1);
            stackPanel.Children.Add(border2);
            return stackPanel;
        }

        private FrameworkElement CreateStyle3Preview()
        {
            var grid = new Grid { Width = 120, Height = 160 };
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    var border = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(3)
                    };
                    border.Background = new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop((row + col) % 2 == 0 ? Color.FromRgb(99, 102, 241) : Color.FromRgb(236, 72, 153), 0),
                            new GradientStop((row + col) % 2 == 0 ? Color.FromRgb(139, 92, 246) : Color.FromRgb(244, 63, 94), 1)
                        },
                        new Point(0, 0),
                        new Point(1, 1)
                    );
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    grid.Children.Add(border);
                }
            }
            return grid;
        }

        private FrameworkElement CreateStyle4Preview()
        {
            var grid = new Grid { Width = 150, Height = 120 };
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var border = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(2)
                    };
                    border.Background = new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop((row + col) % 2 == 0 ? Color.FromRgb(99, 102, 241) : Color.FromRgb(236, 72, 153), 0),
                            new GradientStop((row + col) % 2 == 0 ? Color.FromRgb(139, 92, 246) : Color.FromRgb(244, 63, 94), 1)
                        },
                        new Point(0, 0),
                        new Point(1, 1)
                    );
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    grid.Children.Add(border);
                }
            }
            return grid;
        }

        private FrameworkElement CreateStyle5Preview()
        {
            var grid = new Grid { Width = 120, Height = 200 };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            
            for (int col = 0; col < 2; col++)
            {
                var stackPanel = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 4, 0, col == 1 ? 0 : 4, 0) };
                for (int i = 0; i < 4; i++)
                {
                    var border = new Border
                    {
                        Height = 45,
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    border.Background = new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop(i % 2 == 0 ? Color.FromRgb(99, 102, 241) : Color.FromRgb(236, 72, 153), 0),
                            new GradientStop(i % 2 == 0 ? Color.FromRgb(139, 92, 246) : Color.FromRgb(244, 63, 94), 1)
                        },
                        new Point(0, 0),
                        new Point(1, 1)
                    );
                    stackPanel.Children.Add(border);
                }
                Grid.SetColumn(stackPanel, col);
                grid.Children.Add(stackPanel);
            }
            return grid;
        }

        private void UpdatePriceInfo()
        {
            if (double.TryParse(_frameAmount, out double amount))
            {
                double totalAmount = amount * _numberOfCopies;
                
                if (_numberOfCopies > 1)
                {
                    PriceInfoText.Text = $"Just ₹{amount} x {_numberOfCopies} to keep the memories alive with extra photo copies! You'll get printed photos and digital copies too.";
                }
                else
                {
                    PriceInfoText.Text = $"Just ₹{amount} to freeze the moment! Get your photo printed & a digital copy too.";
                }
            }
        }

        private void UpdatePayButtonText()
        {
            if (double.TryParse(_frameAmount, out double amount))
            {
                double totalAmount = amount * _numberOfCopies;
                PayButton.Content = $"Pay ₹{totalAmount}";
            }
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_numberOfCopies < 5)
            {
                _numberOfCopies++;
                CopyCountText.Text = _numberOfCopies.ToString();
                UpdatePriceInfo();
                UpdatePayButtonText();
            }
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_numberOfCopies > 1)
            {
                _numberOfCopies--;
                CopyCountText.Text = _numberOfCopies.ToString();
                UpdatePriceInfo();
                UpdatePayButtonText();
            }
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SpeakTextAsync("Select your preferred payment mode");
                PaymentModal.Visibility = Visibility.Visible;
                // Apply blur effect to background
                if (BackgroundBlurEffect != null)
                {
                    BackgroundBlurEffect.Radius = 10;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error: {ex.Message}");
            }
        }

        private void CloseModalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoading)
            {
                PaymentModal.Visibility = Visibility.Collapsed;
                // Remove blur effect from background
                if (BackgroundBlurEffect != null)
                {
                    BackgroundBlurEffect.Radius = 0;
                }
            }
        }

        private async void CashPaymentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                
                await SpeakTextAsync("You have selected cash payment. Kindly handover the cash to front desk person and enter the OTP to proceed further.");
                
                // Create processing entry (simplified - you may want to add API call here)
                // For now, just show OTP modal
                PaymentModal.Visibility = Visibility.Collapsed;
                OtpModal.Visibility = Visibility.Visible;
                // Keep blur effect active for OTP modal
                OtpTextBox.Text = "";
                VerifyOtpButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Cash payment error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void UpiPaymentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                
                await SpeakTextAsync("You have selected online payment. We will support only the UPI payments. Please wait for the payment page to be open.");
                
                // Store number of copies
                App.NumberOfCopies = _numberOfCopies;
                
                // Call payment received API if pending transaction exists
                if (App.PendingTransactionData != null && _apiService != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[PaymentPage] Calling payment received API: OrderId={App.PendingTransactionData.OrderId}, PaymentMethod=UPI");
                        
                        var paymentReceivedData = await _apiService.CreatePaymentReceivedTransactionAsync(
                            App.PendingTransactionData.OrderId,
                            "UPI"
                        );

                        if (paymentReceivedData != null)
                        {
                            // Overwrite pending transaction with payment received response
                            App.PendingTransactionData = paymentReceivedData;
                            System.Diagnostics.Debug.WriteLine($"[PaymentPage] Payment received transaction stored: OrderId={paymentReceivedData.OrderId}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[PaymentPage] Warning: Payment received API returned null, keeping pending transaction");
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error calling payment received API: {apiEx.Message}");
                        // Continue even if API call fails
                    }
                }
                
                PaymentModal.Visibility = Visibility.Collapsed;
                // Remove blur effect from background
                if (BackgroundBlurEffect != null)
                {
                    BackgroundBlurEffect.Radius = 0;
                }
                
                // Navigate to filter selection
                _navigationService.NavigateTo(typeof(FilterSelectionPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] UPI payment error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void OtpTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            VerifyOtpButton.IsEnabled = !string.IsNullOrWhiteSpace(OtpTextBox.Text);
        }

        private async void VerifyOtpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                VerifyOtpButton.IsEnabled = false;
                
                string otp = OtpTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(otp))
                {
                    MessageBox.Show("Please enter the OTP", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Verify OTP with API
                if (App.CurrentMachineConfig != null && _apiService != null)
                {
                    // TODO: Add OTP verification API call
                    // For now, just proceed if OTP is not empty
                    bool isValid = await VerifyOtpAsync(otp);
                    
                    if (isValid)
                    {
                        // Store number of copies
                        App.NumberOfCopies = _numberOfCopies;
                        
                        // Call payment received API if pending transaction exists
                        if (App.PendingTransactionData != null && _apiService != null)
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Calling payment received API: OrderId={App.PendingTransactionData.OrderId}, PaymentMethod=CASH");
                                
                                var paymentReceivedData = await _apiService.CreatePaymentReceivedTransactionAsync(
                                    App.PendingTransactionData.OrderId,
                                    "CASH"
                                );

                                if (paymentReceivedData != null)
                                {
                                    // Overwrite pending transaction with payment received response
                                    App.PendingTransactionData = paymentReceivedData;
                                    System.Diagnostics.Debug.WriteLine($"[PaymentPage] Payment received transaction stored: OrderId={paymentReceivedData.OrderId}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[PaymentPage] Warning: Payment received API returned null, keeping pending transaction");
                                }
                            }
                            catch (Exception apiEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error calling payment received API: {apiEx.Message}");
                                // Continue even if API call fails
                            }
                        }
                        
                        // Navigate to filter selection
                        OtpModal.Visibility = Visibility.Collapsed;
                        // Remove blur effect from background
                        if (BackgroundBlurEffect != null)
                        {
                            BackgroundBlurEffect.Radius = 0;
                        }
                        _navigationService.NavigateTo(typeof(FilterSelectionPage));
                    }
                    else
                    {
                        MessageBox.Show("Invalid OTP. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        OtpTextBox.Text = "";
                        VerifyOtpButton.IsEnabled = false;
                    }
                }
                else
                {
                    // If no API service, just proceed (for offline mode)
                    App.NumberOfCopies = _numberOfCopies;
                    OtpModal.Visibility = Visibility.Collapsed;
                    // Remove blur effect from background
                    if (BackgroundBlurEffect != null)
                    {
                        BackgroundBlurEffect.Radius = 0;
                    }
                    _navigationService.NavigateTo(typeof(FilterSelectionPage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] OTP verification error: {ex.Message}");
                MessageBox.Show($"Error verifying OTP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task<bool> VerifyOtpAsync(string otp)
        {
            try
            {
                if (App.CurrentMachineConfig == null || _apiService == null)
                {
                    return false;
                }
                
                // TODO: Implement actual OTP verification API call
                // For now, return true if OTP matches machine OTP (for testing)
                if (!string.IsNullOrEmpty(App.CurrentMachineConfig.MachineOtp) && 
                    otp == App.CurrentMachineConfig.MachineOtp)
                {
                    return true;
                }
                
                // In production, make API call:
                // POST /api/machines/verify-otp/{machineCode}
                // Body: { "otp": "123456" }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] VerifyOtpAsync error: {ex.Message}");
                return false;
            }
        }

        private async void CancelOtpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isLoading)
                {
                    OtpModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    // Keep blur effect active for payment modal
                    await SpeakTextAsync("Payment cancelled");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Cancel OTP error: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _navigationService.NavigateTo(typeof(StyleSelectionPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Back button error: {ex.Message}");
            }
        }

        private async Task SpeakTextAsync(string text)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var synthesizer = new SpeechSynthesizer())
                    {
                        // Select female voice
                        var voices = synthesizer.GetInstalledVoices();
                        var femaleVoice = voices.FirstOrDefault(v => v.VoiceInfo.Gender == VoiceGender.Female);
                        
                        if (femaleVoice != null)
                        {
                            synthesizer.SelectVoice(femaleVoice.VoiceInfo.Name);
                        }
                        
                        synthesizer.Rate = 0;
                        synthesizer.Volume = 100;
                        synthesizer.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PaymentPage] TTS error: {ex.Message}");
                }
            });
        }
    }
}

