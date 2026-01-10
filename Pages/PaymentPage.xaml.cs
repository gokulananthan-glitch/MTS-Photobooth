using System;
using System.IO;
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
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

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
        private string? _currentRazorpayOrderId = null;

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

        private void UpdateButtonStates()
        {
            bool isEnabled = !_isLoading;
            
            // Main page buttons
            if (PayButton != null)
                PayButton.IsEnabled = isEnabled;
            if (BackButton != null)
                BackButton.IsEnabled = isEnabled;
            if (IncreaseButton != null)
                IncreaseButton.IsEnabled = isEnabled;
            if (DecreaseButton != null)
                DecreaseButton.IsEnabled = isEnabled;
            
            // Payment modal buttons
            if (CashPaymentButton != null)
                CashPaymentButton.IsEnabled = isEnabled;
            if (UpiPaymentButton != null)
                UpiPaymentButton.IsEnabled = isEnabled;
            if (CloseModalButton != null)
                CloseModalButton.IsEnabled = isEnabled;
            
            // Razorpay modal buttons
            if (CloseRazorpayModalButton != null)
                CloseRazorpayModalButton.IsEnabled = isEnabled;
            
            // OTP modal buttons
            if (CancelOtpButton != null)
                CancelOtpButton.IsEnabled = isEnabled;
            // VerifyOtpButton is managed separately based on OTP input, but disable if loading
            if (VerifyOtpButton != null && _isLoading)
                VerifyOtpButton.IsEnabled = false;
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
                CornerRadius = new CornerRadius(8)
            };
            border.Background = new SolidColorBrush(Color.FromArgb(102, 234, 179, 8)); // #EAB308 with 40% opacity
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
            border1.Background = new SolidColorBrush(Color.FromArgb(102, 234, 179, 8)); // #EAB308 with 40% opacity
            
            var border2 = new Border
            {
                Height = 90,
                CornerRadius = new CornerRadius(8)
            };
            border2.Background = new SolidColorBrush(Color.FromArgb(102, 214, 173, 112)); // #D6AD70 with 40% opacity
            
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
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(2)
                    };
                    // Alternate between primary (#EAB308) and accent (#D6AD70) with 40% opacity
                    border.Background = (row + col) % 2 == 0 
                        ? new SolidColorBrush(Color.FromArgb(102, 234, 179, 8))   // #EAB308 with 40% opacity
                        : new SolidColorBrush(Color.FromArgb(102, 214, 173, 112)); // #D6AD70 with 40% opacity
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
                    // Alternate between primary (#EAB308) and accent (#D6AD70) with 40% opacity
                    border.Background = (row + col) % 2 == 0 
                        ? new SolidColorBrush(Color.FromArgb(102, 234, 179, 8))   // #EAB308 with 40% opacity
                        : new SolidColorBrush(Color.FromArgb(102, 214, 173, 112)); // #D6AD70 with 40% opacity
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
                var stackPanel = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 2, 0, col == 1 ? 0 : 2, 0) };
                for (int i = 0; i < 4; i++)
                {
                    var border = new Border
                    {
                        Height = 45,
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, i < 3 ? 4 : 0)
                    };
                    // Alternate between primary (#EAB308) and accent (#D6AD70) with 40% opacity
                    border.Background = i % 2 == 0 
                        ? new SolidColorBrush(Color.FromArgb(102, 234, 179, 8))   // #EAB308 with 40% opacity
                        : new SolidColorBrush(Color.FromArgb(102, 214, 173, 112)); // #D6AD70 with 40% opacity
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
                // Show modal immediately for better UX
                PaymentModal.Visibility = Visibility.Visible;
                // Apply blur effect to background
                if (BackgroundBlurEffect != null)
                {
                    BackgroundBlurEffect.Radius = 10;
                }
                
                // Speak text asynchronously without blocking UI
                _ = Task.Run(async () =>
                {
                    await SpeakTextAsync("Select your preferred payment mode");
                });
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
                UpdateButtonStates();
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                
                await SpeakTextAsync("You have selected cash payment. Kindly handover the cash to front desk person and enter the OTP to proceed further.");
                
                // Get machine code and site code with fallback
                if (_apiService == null)
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("API service not available. Please check configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                
                if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!double.TryParse(_frameAmount, out double amount))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Invalid amount. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                double totalAmount = amount * _numberOfCopies;
                string frame = $"grid{App.SelectedStyle}";
                
                // Create processing entry (like React Native)
                var processingTransaction = await _apiService.CreateProcessingSaleTransactionAsync(
                    machineCode,
                    siteCode,
                    frame,
                    amount,
                    "CASH",
                    _numberOfCopies,
                    totalAmount,
                    App.PendingTransactionData?.OrderId ?? $"ORDER_{DateTime.Now:yyyyMMddHHmmss}"
                );
                
                if (processingTransaction == null)
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Failed to create processing transaction. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Update pending transaction data
                App.PendingTransactionData = processingTransaction;
                
                // Show OTP modal
                PaymentModal.Visibility = Visibility.Collapsed;
                OtpModal.Visibility = Visibility.Visible;
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
                UpdateButtonStates();
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void UpiPaymentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                UpdateButtonStates();
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                
                await SpeakTextAsync("You have selected online payment. We will support only the UPI payments. Please wait for the payment page to be open.");
                
                // Store number of copies
                App.NumberOfCopies = _numberOfCopies;
                
                // Check if API service is available
                if (_apiService == null)
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("API service is not configured. Please configure API settings in appsettings.json", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Check if Razorpay KeyId is configured
                var config = GetConfig();
                if (string.IsNullOrEmpty(config.RazorpaySettings.KeyId))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Razorpay Key ID is not configured. Please configure Razorpay KeyId in appsettings.json", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Get machine code and site code with fallback
                var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                
                if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Calculate total amount
                if (!double.TryParse(_frameAmount, out double amount))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Invalid amount. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                double totalAmount = amount * _numberOfCopies;
                string frame = $"grid{App.SelectedStyle}";
                
                // Step 1: Create Razorpay order through backend API
                var razorpayOrder = await _apiService.CreateRazorpayOrderAsync(
                    totalAmount, 
                    config.RazorpaySettings.Currency
                );
                
                if (razorpayOrder == null || string.IsNullOrEmpty(razorpayOrder.Id))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Failed to create payment order. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                _currentRazorpayOrderId = razorpayOrder.Id;
                
                // Step 2: Create processing entry (like React Native)
                var processingTransaction = await _apiService.CreateProcessingSaleTransactionAsync(
                    machineCode,
                    siteCode,
                    frame,
                    amount,
                    "UPI",
                    _numberOfCopies,
                    totalAmount,
                    App.PendingTransactionData?.OrderId ?? razorpayOrder.Receipt
                );
                
                if (processingTransaction == null)
                {
                    MessageBox.Show("Failed to create processing transaction. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Update pending transaction data
                App.PendingTransactionData = processingTransaction;
                
                // Step 3: Generate Razorpay checkout HTML and show
                string checkoutHtml = GenerateRazorpayCheckoutHtml(
                    razorpayOrder.Id,
                    config.RazorpaySettings.KeyId,
                    totalAmount,
                    processingTransaction.OrderId
                );
                
                // Show Razorpay modal and load checkout
                PaymentModal.Visibility = Visibility.Collapsed;
                RazorpayModal.Visibility = Visibility.Visible;
                RazorpayStatusText.Visibility = Visibility.Collapsed;
                
                // Initialize WebView2 and load checkout HTML
                await InitializeWebView2Async(checkoutHtml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] UPI payment error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                UpdateButtonStates();
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }
        
        private string GenerateRazorpayCheckoutHtml(string orderId, string keyId, double amount, string receipt)
        {
            // Use a simpler approach that works better with WPF WebBrowser (IE engine)
            // Load script synchronously and use inline script
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Razorpay Checkout</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: #f5f5f5;
        }}
        .container {{
            text-align: center;
            padding: 20px;
        }}
        .amount {{
            font-size: 24px;
            font-weight: bold;
            color: #333;
            margin: 20px 0;
        }}
        .pay-button {{
            background: #528FF0;
            color: white;
            border: none;
            padding: 15px 40px;
            font-size: 18px;
            border-radius: 5px;
            cursor: pointer;
            margin-top: 20px;
        }}
        .pay-button:hover {{
            background: #4178D0;
        }}
        .pay-button:disabled {{
            background: #ccc;
            cursor: not-allowed;
        }}
        .loading {{
            color: #666;
            margin-top: 10px;
        }}
        .error {{
            color: #d32f2f;
            margin-top: 10px;
        }}
    </style>
    <script src='https://checkout.razorpay.com/v1/checkout.js'></script>
</head>
<body>
    <div class='container'>
        <h2>Complete Your Payment</h2>
        <div class='amount'>Amount: ₹{amount:F2}</div>
        <button id='payButton' class='pay-button' onclick='openRazorpay()'>Pay Now</button>
        <div id='status' class='loading'></div>
    </div>
    
    <script type='text/javascript'>
        var rzpOptions = {{
            'key': '{keyId}',
            'amount': {((int)(amount * 100))},
            'currency': 'INR',
            'name': 'PhotoBooth',
            'description': 'PhotoBooth Payment',
            'order_id': '{orderId}',
            'handler': function (response) {{
                window.location.href = 'razorpay://success?payment_id=' + encodeURIComponent(response.razorpay_payment_id) + 
                                      '&order_id=' + encodeURIComponent(response.razorpay_order_id) + 
                                      '&signature=' + encodeURIComponent(response.razorpay_signature);
            }},
            'prefill': {{
                'contact': '',
                'email': ''
            }},
            'notes': {{
                'receipt': '{receipt}'
            }},
            'theme': {{
                'color': '#f4257b'
            }},
            'modal': {{
                'ondismiss': function() {{
                    window.location.href = 'razorpay://cancel';
                }}
            }}
        }};
        
        function openRazorpay() {{
            try {{
                if (typeof Razorpay === 'undefined') {{
                    document.getElementById('status').textContent = 'Razorpay is loading. Please wait and try again...';
                    document.getElementById('status').className = 'error';
                    setTimeout(openRazorpay, 1000);
                    return;
                }}
                
                var rzp = new Razorpay(rzpOptions);
                rzp.open();
                document.getElementById('status').textContent = 'Opening payment gateway...';
            }} catch (error) {{
                document.getElementById('status').textContent = 'Error: ' + error.message;
                document.getElementById('status').className = 'error';
            }}
        }}
        
        // Auto-open when Razorpay is loaded
        window.onload = function() {{
            var checkRazorpay = setInterval(function() {{
                if (typeof Razorpay !== 'undefined') {{
                    clearInterval(checkRazorpay);
                    setTimeout(openRazorpay, 500);
                }}
            }}, 100);
            
            // Timeout after 10 seconds
            setTimeout(function() {{
                clearInterval(checkRazorpay);
                if (typeof Razorpay === 'undefined') {{
                    document.getElementById('status').textContent = 'Failed to load Razorpay. Please click Pay Now button.';
                    document.getElementById('status').className = 'error';
                }}
            }}, 10000);
        }};
    </script>
</body>
</html>";
        }
        
        private async Task InitializeWebView2Async(string htmlContent)
                {
                    try
                    {
                // Ensure WebView2 is initialized
                if (RazorpayWebView.CoreWebView2 == null)
                {
                    await RazorpayWebView.EnsureCoreWebView2Async();
                }
                
                // Set up navigation event handler
                RazorpayWebView.CoreWebView2.NavigationStarting += (sender, e) =>
                {
                    string url = e.Uri;
                    
                    // Check for payment success callback
                    if (url.StartsWith("razorpay://success"))
                    {
                        e.Cancel = true; // Cancel navigation
                        HandleRazorpaySuccess(url);
                    }
                    // Check for payment cancellation
                    else if (url.StartsWith("razorpay://cancel"))
                    {
                        e.Cancel = true; // Cancel navigation
                        HandleRazorpayCancel();
                    }
                };
                
                // Navigate to HTML content
                RazorpayWebView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error initializing WebView2: {ex.Message}");
                MessageBox.Show($"Error initializing payment gateway: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void HandleRazorpaySuccess(string callbackUrl)
        {
            try
            {
                _isLoading = true;
                UpdateButtonStates();
                if (RazorpayStatusText != null)
                {
                    RazorpayStatusText.Text = "Processing payment...";
                }
                if (RazorpayStatusPanel != null)
                {
                    RazorpayStatusPanel.Visibility = Visibility.Visible;
                }
                
                // Parse payment details from callback URL
                var uri = new Uri(callbackUrl);
                var query = uri.Query.TrimStart('?');
                var queryParams = query.Split('&')
                    .Select(p => p.Split('='))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
                
                string paymentId = queryParams.GetValueOrDefault("payment_id", "");
                string razorpayOrderId = queryParams.GetValueOrDefault("order_id", "");
                string signature = queryParams.GetValueOrDefault("signature", "");
                
                if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(razorpayOrderId) || string.IsNullOrEmpty(signature))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Invalid payment response. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Get machine code and site code with fallback
                if (App.PendingTransactionData == null || _apiService == null)
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Configuration error. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                
                if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!double.TryParse(_frameAmount, out double amount))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Invalid amount configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                double totalAmount = amount * _numberOfCopies;
                string frame = $"grid{App.SelectedStyle}";
                
                // Store number of copies (like React Native: setSelectedFrameData)
                App.NumberOfCopies = _numberOfCopies;
                
                // Create payment received transaction (like React Native: createPaymentReceived("UPI", paymentId, razorpayOrderId))
                // Note: React Native passes razor_pay_payment__id and razorpay_order_id, but API expects razor_pay_order_id
                var paymentReceivedData = await _apiService.CreatePaymentReceivedSaleTransactionAsync(
                    machineCode,
                    siteCode,
                    frame,
                    amount,
                    "UPI",
                    _numberOfCopies,
                    totalAmount,
                            App.PendingTransactionData.OrderId,
                    razorpayOrderId
                        );

                        if (paymentReceivedData != null)
                        {
                            App.PendingTransactionData = paymentReceivedData;
                            System.Diagnostics.Debug.WriteLine($"[PaymentPage] Payment received transaction stored: OrderId={paymentReceivedData.OrderId}");
                    
                    // Close Razorpay modal and navigate (like React Native)
                    RazorpayModal.Visibility = Visibility.Collapsed;
                    if (BackgroundBlurEffect != null)
                    {
                        BackgroundBlurEffect.Radius = 0;
                    }
                    
                    // Navigate to Camera/FilterSelection (like React Native: navigation.navigate("Camera"))
                    _navigationService.NavigateTo(typeof(FilterSelectionPage));
                        }
                        else
                        {
                    MessageBox.Show("Failed to create payment received transaction. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error handling Razorpay success: {ex.Message}");
                MessageBox.Show($"Error processing payment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                UpdateButtonStates();
            }
        }
        
        private async void HandleRazorpayCancel()
        {
            try
            {
                _isLoading = true;
                UpdateButtonStates();
                
                // Show alert (like React Native: Alert.alert("Payment cancelled"))
                MessageBox.Show("Payment cancelled", "Payment Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Get machine code and site code with fallback
                if (App.PendingTransactionData == null || _apiService == null)
                    {
                    _isLoading = false;
                    UpdateButtonStates();
                    RazorpayModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    return;
                }
                
                var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                
                if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    RazorpayModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    return;
                }
                
                if (!double.TryParse(_frameAmount, out double amount))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    RazorpayModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    return;
                }
                
                double totalAmount = amount * _numberOfCopies;
                string frame = $"grid{App.SelectedStyle}";
                
                // Create cancelled transaction (like React Native: CancelledEntryCreation("UPI"))
                var cancelledData = await _apiService.CreateCancelledSaleTransactionAsync(
                    machineCode,
                    siteCode,
                    frame,
                    amount,
                    "UPI",
                    _numberOfCopies,
                    totalAmount,
                    App.PendingTransactionData.OrderId
                );
                
                if (cancelledData != null)
                {
                    App.PendingTransactionData = cancelledData;
                    System.Diagnostics.Debug.WriteLine($"[PaymentPage] Cancelled transaction stored: OrderId={cancelledData.OrderId}");
                    
                    // Close modal and navigate (like React Native)
                    RazorpayModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    // Navigate to styles page (like React Native: navigation.navigate("styles"))
                    // Note: In WPF, we might want to navigate back to style selection
                }
                else
                {
                    MessageBox.Show("Failed to cancel the payment", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Navigate to Home (like React Native fallback)
                    // _navigationService.NavigateTo(typeof(StartPage));
                }
                
                RazorpayStatusText.Visibility = Visibility.Collapsed;
                _currentRazorpayOrderId = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Error handling Razorpay cancel: {ex.Message}");
                MessageBox.Show("Failed to cancel the payment", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RazorpayModal.Visibility = Visibility.Collapsed;
                PaymentModal.Visibility = Visibility.Visible;
            }
            finally
            {
                _isLoading = false;
                UpdateButtonStates();
            }
        }
        
        private void CloseRazorpayModalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoading)
            {
                HandleRazorpayCancel();
            }
        }
        
        private void CancelPaymentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoading)
            {
                HandleRazorpayCancel();
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
                UpdateButtonStates();
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                VerifyOtpButton.IsEnabled = false;
                
                string otp = OtpTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(otp))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Please enter the OTP", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Verify OTP with API
                if (App.CurrentMachineConfig != null && _apiService != null)
                {
                    bool isValid = await VerifyOtpAsync(otp);
                    
                    if (isValid)
                    {
                        // Store number of copies (like React Native: setSelectedFrameData)
                        App.NumberOfCopies = _numberOfCopies;
                        
                        // Get machine code and site code with fallback
                        var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                        
                        if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                        {
                            _isLoading = false;
                            UpdateButtonStates();
                            MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        if (!double.TryParse(_frameAmount, out double amount))
                        {
                            _isLoading = false;
                            UpdateButtonStates();
                            MessageBox.Show("Invalid amount configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        double totalAmount = amount * _numberOfCopies;
                        string frame = $"grid{App.SelectedStyle}";
                        
                        // Create payment received transaction (like React Native: createPaymentReceived("CASH"))
                        if (App.PendingTransactionData != null)
                        {
                            var paymentReceivedData = await _apiService.CreatePaymentReceivedSaleTransactionAsync(
                                machineCode,
                                siteCode,
                                frame,
                                amount,
                                "CASH",
                                _numberOfCopies,
                                totalAmount,
                                    App.PendingTransactionData.OrderId,
                                null // No razorpay order ID for cash
                                );

                                if (paymentReceivedData != null)
                                {
                                    App.PendingTransactionData = paymentReceivedData;
                                    System.Diagnostics.Debug.WriteLine($"[PaymentPage] Payment received transaction stored: OrderId={paymentReceivedData.OrderId}");
                        
                                // Navigate to Camera/FilterSelection (like React Native: navigation.navigate("Camera"))
                        OtpModal.Visibility = Visibility.Collapsed;
                        if (BackgroundBlurEffect != null)
                        {
                            BackgroundBlurEffect.Radius = 0;
                        }
                        _navigationService.NavigateTo(typeof(FilterSelectionPage));
                            }
                            else
                            {
                                MessageBox.Show("Failed to create payment received transaction. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
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
                    MessageBox.Show("API service not available. Please check configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                UpdateButtonStates();
            }
        }

        private async Task<(string machineCode, string siteCode)> GetMachineAndSiteCodesAsync()
        {
            // Try to get from CurrentMachineConfig first
            string machineCode = null;
            string siteCode = null;
            
            if (App.CurrentMachineConfig != null)
            {
                machineCode = !string.IsNullOrWhiteSpace(App.CurrentMachineConfig.MachineCode) 
                    ? App.CurrentMachineConfig.MachineCode 
                    : null;
                siteCode = !string.IsNullOrWhiteSpace(App.CurrentMachineConfig.SiteCode) 
                    ? App.CurrentMachineConfig.SiteCode 
                    : null;
            }
            
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
            
            return (machineCode ?? "", siteCode ?? "");
        }

        private async Task<bool> VerifyOtpAsync(string otp)
        {
            try
            {
                if (_apiService == null)
                {
                    return false;
                }
                
                var (machineCode, _) = await GetMachineAndSiteCodesAsync();
                if (string.IsNullOrEmpty(machineCode))
                {
                    return false;
                }
                
                // Call OTP verification API (like React Native)
                bool isValid = await _apiService.VerifyOtpAsync(machineCode, otp);
                return isValid;
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
                // Check if OTP verification is in progress (like React Native: !isOtpVerificationLoading)
                if (_isLoading)
                    return;
                
                _isLoading = true;
                UpdateButtonStates();
                
                // Get machine code and site code with fallback
                if (App.PendingTransactionData == null || _apiService == null)
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    OtpModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    await SpeakTextAsync("Payment cancelled");
                    return;
                }
                
                var (machineCode, siteCode) = await GetMachineAndSiteCodesAsync();
                
                if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(siteCode))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    MessageBox.Show("Machine code or Site code not found. Please configure machine settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    OtpModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    return;
                }
                
                if (!double.TryParse(_frameAmount, out double amount))
                {
                    _isLoading = false;
                    UpdateButtonStates();
                    OtpModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    return;
                }
                
                double totalAmount = amount * _numberOfCopies;
                string frame = $"grid{App.SelectedStyle}";
                
                // Create cancelled transaction (like React Native: CancelledEntryCreation("UPI"))
                // Note: Following React Native code exactly - it uses "UPI" even for cash payment cancellation
                var cancelledData = await _apiService.CreateCancelledSaleTransactionAsync(
                    machineCode,
                    siteCode,
                    frame,
                    amount,
                    "UPI", // Matching React Native code exactly
                    _numberOfCopies,
                    totalAmount,
                    App.PendingTransactionData.OrderId
                );
                
                if (cancelledData != null)
                {
                    App.PendingTransactionData = cancelledData;
                    System.Diagnostics.Debug.WriteLine($"[PaymentPage] Cancelled transaction stored: OrderId={cancelledData.OrderId}");
                    
                    // Close OTP modal and navigate (like React Native)
                    OtpModal.Visibility = Visibility.Collapsed;
                    PaymentModal.Visibility = Visibility.Visible;
                    await SpeakTextAsync("Payment cancelled");
                    // Note: React Native navigates to "styles" on success, but we'll keep payment modal visible
                }
                else
                {
                    MessageBox.Show("Failed to cancel the payment", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // React Native navigates to Home on failure, but we'll keep modal visible
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentPage] Cancel OTP error: {ex.Message}");
                MessageBox.Show("Failed to cancel the payment", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                UpdateButtonStates();
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

