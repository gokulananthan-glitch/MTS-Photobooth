using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;
using static PhotoBooth.Services.ConfigService;

namespace PhotoBooth.Pages
{
    public partial class PrintPage : Page
    {
        private readonly NavigationService _navigationService;
        private readonly DatabaseService _dbService;
        private readonly string _framePath;
        private int _quantity = 1;

        public PrintPage(string framePath)
        {
            InitializeComponent();
            _navigationService = App.NavigationService;
            _dbService = new DatabaseService();
            _framePath = framePath;
            Loaded += PrintPage_Loaded;
        }

        private string GenerateFrameId()
        {
            var now = DateTime.Now;
            var yy = now.Year.ToString().Substring(2);
            var mm = now.Month.ToString().PadLeft(2, '0');
            var dd = now.Day.ToString().PadLeft(2, '0');
            
            // Generate 3 random uppercase alphanumeric characters
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rnd = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            
            return $"OF-{yy}{mm}{dd}-{rnd}";
        }

        private void PrintPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load preview image
                if (File.Exists(_framePath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_framePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    PreviewImage.Source = bitmap;
                }

                // Check if copy selection should be hidden
                // Hide if payment_type is not 'N' OR offline_mode is not true
                bool shouldHideCopySelection = false;
                
                if (App.CurrentMachineConfig != null)
                {
                    string paymentType = App.CurrentMachineConfig.PaymentType ?? "";
                    bool offlineMode = App.CurrentMachineConfig.OfflineMode;
                    
                    // Hide copy selection if payment is required (payment_type != 'N') OR not in offline mode
                    // Show copy selection only if payment_type is 'N' AND offline_mode is true
                    shouldHideCopySelection = (paymentType != "N") || !offlineMode;
                    
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] PaymentType: '{paymentType}', OfflineMode: {offlineMode}, ShouldHideCopySelection: {shouldHideCopySelection}");
                }
                else
                {
                    // If no machine config, default to showing copy selection (safe fallback)
                    shouldHideCopySelection = false;
                    System.Diagnostics.Debug.WriteLine("[PrintPage] No machine config found, showing copy selection by default");
                }

                if (shouldHideCopySelection)
                {
                    // Hide the copy selection Border
                    QuantitySelectorBorder.Visibility = Visibility.Collapsed;
                    
                    // Use copy count from PaymentPage (App.NumberOfCopies)
                    // Ensure it's at least 1
                    _quantity = App.NumberOfCopies > 0 ? App.NumberOfCopies : 1;
                    QuantityText.Text = _quantity.ToString();
                    
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] Copy selection hidden. Using {_quantity} copies from PaymentPage.");
                }
                else
                {
                    // Show copy selection and use default quantity (1)
                    QuantitySelectorBorder.Visibility = Visibility.Visible;
                    _quantity = 1;
                    QuantityText.Text = _quantity.ToString();
                    
                    System.Diagnostics.Debug.WriteLine("[PrintPage] Copy selection visible. User can select quantity.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Error loading preview: {ex.Message}");
            }
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_quantity < 10) // Max 10 copies
            {
                _quantity++;
                QuantityText.Text = _quantity.ToString();
            }
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_quantity > 1)
            {
                _quantity--;
                QuantityText.Text = _quantity.ToString();
            }
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable print button to prevent double-clicks
                PrintButton.IsEnabled = false;

                // Save copies to print folder
                await SaveToPrintFolderAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Print error: {ex.Message}");
                PrintButton.IsEnabled = true;
            }
        }

        private async Task SaveToPrintFolderAsync()
        {
            try
            {
                // Validate source file exists
                if (string.IsNullOrEmpty(_framePath) || !File.Exists(_framePath))
                {
                    throw new FileNotFoundException($"Source frame file not found: {_framePath}");
                }

                System.Diagnostics.Debug.WriteLine($"[PrintPage] Source file: {_framePath}");
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Source file size: {new FileInfo(_framePath).Length} bytes");

                // Get current style to determine if it's grid8 (Style 5)
                string gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);
                bool isGrid8 = gridType == "grid8" || App.SelectedStyle == 5;

                System.Diagnostics.Debug.WriteLine($"[PrintPage] Selected style: {App.SelectedStyle}, Grid: {gridType}, IsGrid8: {isGrid8}");

                // Generate unique timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Calculate the stored file path (event_photos path)
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
                string eventPhotosPath = Path.Combine(@"D:\event_photos", currentDate);
                string storedFilePath = Path.Combine(eventPhotosPath, $"PhotoBooth_{timestamp}.jpg");

                // Run all save operations in parallel
                var saveTasks = new List<Task>();
                var saveErrors = new System.Collections.Concurrent.ConcurrentBag<string>();

                // Task 1: Save all styles EXCEPT grid8 to s4x6\RX1HS-1 with copy duplication
                if (!isGrid8)
                {
                    saveTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            SaveToDnpFolder(
                                @"C:\DNP\HotFolderPrint\Prints\s4x6\RX1HS-1",
                                timestamp,
                                _quantity,
                                "s4x6");
                        }
                        catch (Exception ex)
                        {
                            saveErrors.Add($"DNP s4x6 folder: {ex.Message}");
                        }
                    }));
                }

                // Task 2: Only grid8 (Style 5) saves to s6x2_2 with copy duplication
                if (isGrid8)
                {
                    saveTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            SaveToDnpFolder(
                                @"C:\DNP\HotFolderPrint\Prints\s6x2_2",
                                timestamp,
                                _quantity,
                                "s6x2_2");
                        }
                        catch (Exception ex)
                        {
                            saveErrors.Add($"DNP s6x2_2 folder: {ex.Message}");
                        }
                    }));
                }

                // Task 3: Save to event_photos folder (no copy logic, just one file)
                saveTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        SaveToEventPhotosFolder(timestamp);
                    }
                    catch (Exception ex)
                    {
                        saveErrors.Add($"Event photos folder: {ex.Message}");
                    }
                }));

                // Wait for all tasks to complete
                Task.WaitAll(saveTasks.ToArray());

                // If there were any errors, show them but continue
                if (saveErrors.Count > 0)
                {
                    string errorSummary = string.Join("\n", saveErrors);
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] Some save operations failed:\n{errorSummary}");
                    
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Warning: Some files could not be saved:\n\n{errorSummary}\n\n" +
                            "Please check:\n" +
                            "- Folder permissions\n" +
                            "- Disk space\n" +
                            "- Network drive availability (if applicable)\n\n" +
                            "The photo may have been saved to other locations.",
                            "Save Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }

                if (saveErrors.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] ✓ All files saved successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] ⚠ Some files saved with errors");
                }

                // Store data in offline_frames and transaction_data tables (only if at least one save succeeded)
                // Use the calculated path even if the save failed - we'll store the intended path
                try
                {
                    await StoreOfflineDataAsync(storedFilePath);
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] Error storing offline data: {dbEx.Message}");
                    // Don't show error for database storage failure, just log it
                }

                // DON'T delete temp file here - let StartPage handle cleanup
                
                // Show success overlay
                Dispatcher.Invoke(() =>
                {
                    SuccessOverlay.Visibility = Visibility.Visible;
                    var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new System.Windows.Duration(TimeSpan.FromSeconds(0.3))
                    };
                    SuccessOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });
                
                // Auto-navigate to home after 2 seconds
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Dispatcher.Invoke(() => HomeButton_Click(null, null));
                });
                
                // Increment session count
                App.IncrementSessionCount();
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string detailedError = $"Error Type: {ex.GetType().Name}\n" +
                                      $"Message: {ex.Message}\n" +
                                      $"Source: {ex.Source}";
                
                if (ex.InnerException != null)
                {
                    detailedError += $"\nInner: {ex.InnerException.Message}";
                }
                
                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✗ Error saving file:");
                System.Diagnostics.Debug.WriteLine(detailedError);
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Stack trace: {ex.StackTrace}");
                
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Error saving photo:\n\n{errorMessage}\n\nPlease check:\n" +
                        "- Folder permissions\n" +
                        "- Disk space\n" +
                        "- Antivirus settings",
                        "Save Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    // Re-enable print button on error
                    PrintButton.IsEnabled = true;
                    
                    // Show home button even on error
                    HomeButton.Visibility = Visibility.Visible;
                    var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new System.Windows.Duration(TimeSpan.FromSeconds(0.5))
                    };
                    HomeButton.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });
            }
        }

        private void SaveToDnpFolder(string folderPath, string timestamp, int quantity, string folderType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Saving to {folderType} folder: {folderPath}");

                // Ensure the folder exists
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] Created {folderType} folder: {folderPath}");
                }

                // Copy file multiple times based on quantity
                for (int copy = 1; copy <= quantity; copy++)
                {
                    string filename = quantity > 1 
                        ? $"PhotoBooth_{timestamp}_copy{copy}.jpg" 
                        : $"PhotoBooth_{timestamp}.jpg";
                    string destinationPath = Path.Combine(folderPath, filename);

                    System.Diagnostics.Debug.WriteLine($"[PrintPage] [{folderType}] Copy {copy}/{quantity}: {destinationPath}");

                    // Validate destination path
                    if (destinationPath.Length > 260)
                    {
                        throw new PathTooLongException($"Destination path is too long: {destinationPath.Length} characters");
                    }

                    // Copy file with retry logic
                    int retryCount = 3;
                    Exception? lastException = null;

                    for (int i = 0; i < retryCount; i++)
                    {
                        try
                        {
                            File.Copy(_framePath, destinationPath, overwrite: true);
                            System.Diagnostics.Debug.WriteLine($"[PrintPage] [{folderType}] Copy {copy} saved successfully on attempt {i + 1}");
                            lastException = null;
                            break;
                        }
                        catch (IOException ex) when (i < retryCount - 1)
                        {
                            lastException = ex;
                            System.Diagnostics.Debug.WriteLine($"[PrintPage] [{folderType}] Copy attempt {i + 1} failed: {ex.Message}, retrying...");
                            System.Threading.Thread.Sleep(500); // Wait before retry
                        }
                    }

                    if (lastException != null)
                    {
                        throw new IOException($"Failed to copy file to {folderType} folder after {retryCount} attempts", lastException);
                    }

                    // Verify file was saved correctly
                    if (!File.Exists(destinationPath))
                    {
                        throw new IOException($"File copy reported success but destination file does not exist: {destinationPath}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✓ All {quantity} copies saved to {folderType} folder");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✗ Error saving to {folderType} folder: {ex.Message}");
                throw; // Re-throw to be caught by main handler
            }
        }

        private void SaveToEventPhotosFolder(string timestamp)
        {
            try
            {
                // Create folder path with current date (yyyy-MM-dd format)
                string basePath = @"D:\event_photos";
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
                string eventPhotosPath = Path.Combine(basePath, currentDate);

                System.Diagnostics.Debug.WriteLine($"[PrintPage] Saving to event_photos folder: {eventPhotosPath}");

                // Ensure the base directory exists first
                if (!Directory.Exists(basePath))
                {
                    try
                    {
                        Directory.CreateDirectory(basePath);
                        System.Diagnostics.Debug.WriteLine($"[PrintPage] Created base event_photos folder: {basePath}");
                    }
                    catch (Exception ex)
                    {
                        throw new DirectoryNotFoundException($"Failed to create base directory '{basePath}': {ex.Message}", ex);
                    }
                }

                // Ensure the date-specific folder exists
                if (!Directory.Exists(eventPhotosPath))
                {
                    try
                    {
                        Directory.CreateDirectory(eventPhotosPath);
                        System.Diagnostics.Debug.WriteLine($"[PrintPage] Created event_photos date folder: {eventPhotosPath}");
                    }
                    catch (Exception ex)
                    {
                        throw new DirectoryNotFoundException($"Failed to create date directory '{eventPhotosPath}': {ex.Message}", ex);
                    }
                }

                // Save single file (no copy logic)
                string filename = $"PhotoBooth_{timestamp}.jpg";
                string destinationPath = Path.Combine(eventPhotosPath, filename);

                System.Diagnostics.Debug.WriteLine($"[PrintPage] [event_photos] Saving: {destinationPath}");

                // Validate destination path
                if (destinationPath.Length > 260)
                {
                    throw new PathTooLongException($"Destination path is too long: {destinationPath.Length} characters");
                }

                // Copy file with retry logic
                int retryCount = 3;
                Exception? lastException = null;

                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        File.Copy(_framePath, destinationPath, overwrite: true);
                        System.Diagnostics.Debug.WriteLine($"[PrintPage] [event_photos] File saved successfully on attempt {i + 1}");
                        lastException = null;
                        break;
                    }
                    catch (IOException ex) when (i < retryCount - 1)
                    {
                        lastException = ex;
                        System.Diagnostics.Debug.WriteLine($"[PrintPage] [event_photos] Copy attempt {i + 1} failed: {ex.Message}, retrying...");
                        System.Threading.Thread.Sleep(500); // Wait before retry
                    }
                }

                if (lastException != null)
                {
                    throw new IOException($"Failed to copy file to event_photos folder after {retryCount} attempts", lastException);
                }

                // Verify file was saved correctly
                if (!File.Exists(destinationPath))
                {
                    throw new IOException($"File copy reported success but destination file does not exist: {destinationPath}");
                }

                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✓ File saved to event_photos folder");
            }
            catch (DirectoryNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✗ Directory error saving to event_photos folder: {ex.Message}");
                throw; // Re-throw to be caught by task handler
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✗ Error saving to event_photos folder: {ex.Message}");
                throw; // Re-throw to be caught by task handler
            }
        }

        private void DigitalCopyButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // TODO: Implement QR code generation and display
                // For now, show a message
                MessageBox.Show(
                    "Digital copy feature coming soon!\n\nScan the QR code to download your photos instantly.",
                    "Digital Copy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                System.Diagnostics.Debug.WriteLine("[PrintPage] Digital copy requested");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Digital copy error: {ex.Message}");
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mark session as complete (for statistics)
                App.IncrementSessionCount();
                
                // Note: Actual cleanup will happen on StartPage_Loaded
                // This ensures clean state for next session
                
                _navigationService.NavigateTo(typeof(StartPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Navigation error: {ex.Message}");
                MessageBox.Show($"Navigation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StoreOfflineDataAsync(string storedFilePath)
        {
            try
            {
                // Get machine config from locally stored data
                var machineConfig = App.CurrentMachineConfig;
                if (machineConfig == null)
                {
                    System.Diagnostics.Debug.WriteLine("[PrintPage] Warning: MachineConfig not available, skipping offline data storage");
                    return;
                }

                // Get grid type (frame)
                string gridType = FrameDataProvider.GetGridForStyle(App.SelectedStyle);

                // Get machine code and site code with fallback to app settings
                var machineCode = !string.IsNullOrWhiteSpace(machineConfig.MachineCode) 
                    ? machineConfig.MachineCode 
                    : await _dbService.GetAppSettingAsync("MachineCode") ?? "";
                var siteCode = !string.IsNullOrWhiteSpace(machineConfig.SiteCode) 
                    ? machineConfig.SiteCode 
                    : await _dbService.GetAppSettingAsync("SiteCode") ?? "";

                // Validate that we have machine code and site code
                if (string.IsNullOrWhiteSpace(machineCode) || string.IsNullOrWhiteSpace(siteCode))
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] Warning: MachineCode or SiteCode is empty. MachineCode: '{machineCode}', SiteCode: '{siteCode}'. Skipping offline data storage.");
                    return;
                }

                var now = DateTime.UtcNow;
                bool offlineMode = machineConfig.OfflineMode;

                // Store in offline_frames table (always store, regardless of offline mode)
                string frameId = GenerateFrameId();
                await _dbService.SaveOfflineFrameAsync(
                    frameId: frameId,
                    machineCode: machineCode,
                    siteCode: siteCode,
                    filePath: storedFilePath,
                    eventId: machineConfig.EventId,
                    createdAt: now
                );

                // If offline mode, store transaction data in database
                if (offlineMode)
                {
                    await _dbService.SaveTransactionDataAsync(
                        orderId: frameId,
                        machineCode: machineCode,
                        siteCode: siteCode,
                        frame: gridType,
                        amount: 0,
                        createdAt: now,
                        saleDate: now,
                        paymentMode: "OFFLINE",
                        totalCopies: _quantity,
                        totalAmount: 0,
                        eventId: machineConfig.EventId,
                        onEvent: machineConfig.OnEvent
                    );
                    System.Diagnostics.Debug.WriteLine($"[PrintPage] ✓ Offline data stored: FrameId={frameId}, Grid={gridType}, Copies={_quantity}");
                }
                else
                {
                    // Online mode - make API call directly
                    if (App.PendingTransactionData != null && !string.IsNullOrEmpty(App.PendingTransactionData.OrderId))
                    {
                        try
                        {
                            // Get amount from supported frames
                            double amount = 0;
                            if (machineConfig.SupportedFrames != null)
                            {
                                var supportedFrame = machineConfig.SupportedFrames
                                    .FirstOrDefault(f => f.Type.Equals(gridType, StringComparison.OrdinalIgnoreCase));
                                
                                if (supportedFrame != null && !string.IsNullOrEmpty(supportedFrame.Amount))
                                {
                                    double.TryParse(supportedFrame.Amount, out amount);
                                }
                            }

                            // Calculate total amount
                            double totalAmount = _quantity * amount;

                            // Get payment method from pending transaction (should be set from payment received API)
                            // Check both PaymentMethod and PaymentMode fields
                            string paymentMethod = App.PendingTransactionData.PaymentMethod 
                                ?? App.PendingTransactionData.PaymentMode 
                                ?? "CASH";

                            // Create transaction data for API call
                            // Frame will be formatted in API method (gridType is already in "grid2" format)
                            var completedTransaction = new TransactionData
                            {
                                OrderId = App.PendingTransactionData.OrderId,
                                MachineCode = machineCode,
                                SiteCode = siteCode,
                                Frame = gridType, // Already in "grid2" format
                                Amount = amount,
                                CreatedAt = App.PendingTransactionData.CreatedAt ?? now,
                                SaleDate = now,
                                PaymentMode = paymentMethod,
                                TotalCopies = _quantity,
                                TotalAmount = totalAmount,
                                EventId = machineConfig.EventId,
                                OnEvent = machineConfig.OnEvent
                            };

                            // Create API service and call
                            var config = GetConfig();
                            var apiService = new ApiService(config.ApiSettings.BaseUrl, config.ApiSettings.TimeoutSeconds);
                            
                            System.Diagnostics.Debug.WriteLine($"[PrintPage] Creating completed sale transaction via API: OrderId={completedTransaction.OrderId}, PaymentMethod={paymentMethod}, TotalAmount={totalAmount}");

                            bool success = await apiService.CreateCompletedSaleTransactionAsync(completedTransaction, paymentMethod);

                            if (success)
                            {
                                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✓ Completed sale transaction created via API: OrderId={completedTransaction.OrderId}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[PrintPage] ✗ Failed to create completed sale transaction via API: OrderId={completedTransaction.OrderId}");
                                // Don't throw - API failure shouldn't block the print process
                            }
                        }
                        catch (Exception apiEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PrintPage] Error creating completed sale transaction via API: {apiEx.Message}");
                            // Don't throw - API failure shouldn't block the print process
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[PrintPage] Warning: PendingTransactionData is null or OrderId is empty. OrderId: '{App.PendingTransactionData?.OrderId ?? "null"}'");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintPage] Error storing offline data: {ex.Message}");
                // Don't throw - offline storage failure shouldn't block the print process
            }
        }
    }
}

