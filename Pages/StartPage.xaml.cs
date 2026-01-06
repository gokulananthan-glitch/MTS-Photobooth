using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Speech.Synthesis;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;
using static PhotoBooth.Services.ConfigService;
using System.Windows.Media.Imaging;

namespace PhotoBooth.Pages
{
    public partial class StartPage : Page
    {
        private readonly ApiService _apiService;
        private readonly DatabaseService _dbService;
        private readonly NavigationService _navigationService;
        private readonly AppConfig _config;
        private bool _syncInProgress = false;

        public StartPage()
        {
            InitializeComponent();
            
            // Load configuration from appsettings.json
            _config = GetConfig();
            
            _apiService = new ApiService(_config.ApiSettings.BaseUrl, _config.ApiSettings.TimeoutSeconds);
            _dbService = new DatabaseService();
            _navigationService = App.NavigationService;
            
            // Animate elements on load
            Loaded += StartPage_Loaded;
        }

        private void CleanupSessionResources()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[StartPage] === Session Cleanup Started ===");

                // Clear all captured images to free memory
                App.CapturedImages.Clear();
                
                // Reset session state
                App.SelectedStyle = 0;
                App.RetakePhotoIndex = -1;
                App.TempFramePath = null;
                
                // Clean up temporary frame files from this session
                var tempPath = System.IO.Path.GetTempPath();
                int deletedCount = 0;
                
                try
                {
                    // First, try to delete the temp frame path from last session if it exists
                    if (!string.IsNullOrEmpty(App.TempFramePath) && System.IO.File.Exists(App.TempFramePath))
                    {
                        try
                        {
                            System.IO.File.Delete(App.TempFramePath);
                            deletedCount++;
                            System.Diagnostics.Debug.WriteLine($"[StartPage] Deleted session temp frame: {App.TempFramePath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[StartPage] Could not delete temp frame: {ex.Message}");
                        }
                    }
                    
                    // Delete other frame files from last 10 minutes (current session)
                    var cutoffTime = DateTime.Now.AddMinutes(-10);
                    
                    foreach (var file in System.IO.Directory.GetFiles(tempPath, "frame_*.jpg"))
                    {
                        try
                        {
                            if (System.IO.File.GetLastWriteTime(file) > cutoffTime)
                            {
                                System.IO.File.Delete(file);
                                deletedCount++;
                            }
                        }
                        catch { /* Ignore locked files */ }
                    }
                    
                    foreach (var file in System.IO.Directory.GetFiles(tempPath, "frame_composite_*.png"))
                    {
                        try
                        {
                            if (System.IO.File.GetLastWriteTime(file) > cutoffTime)
                            {
                                System.IO.File.Delete(file);
                                deletedCount++;
                            }
                        }
                        catch { /* Ignore locked files */ }
                    }
                    
                    if (deletedCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StartPage] Cleaned up {deletedCount} session temp files");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartPage] Temp file cleanup warning: {ex.Message}");
                }

                // Clear image processor cache
                ResourceManager.ClearCaches();
                
                // Gentle garbage collection between sessions
                ResourceManager.OptimizeMemory(aggressive: false);
                
                System.Diagnostics.Debug.WriteLine("[StartPage] === Session Cleanup Complete ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartPage] Cleanup error: {ex.Message}");
            }
        }

        private async void StartPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Clean up resources from previous session
            CleanupSessionResources();
            
            // Initialize error border state
            ErrorBorder.Visibility = Visibility.Collapsed;
            InitializeErrorBorderState();
            
            // Load machine code and site code from appsettings.json and store in DB
            try
            {
                var existingMachineCode = await _dbService.GetAppSettingAsync("MachineCode");
                var existingSiteCode = await _dbService.GetAppSettingAsync("SiteCode");
                
                // If not in DB, load from appsettings.json and save to DB
                if (string.IsNullOrEmpty(existingMachineCode))
                {
                    await _dbService.SaveAppSettingAsync("MachineCode", _config.ApiSettings.MachineCode);
                    System.Diagnostics.Debug.WriteLine($"[StartPage] Machine code saved to DB: {_config.ApiSettings.MachineCode}");
                }
                
                if (string.IsNullOrEmpty(existingSiteCode))
                {
                    await _dbService.SaveAppSettingAsync("SiteCode", _config.ApiSettings.SiteCode);
                    System.Diagnostics.Debug.WriteLine($"[StartPage] Site code saved to DB: {_config.ApiSettings.SiteCode}");
                }
                
                // Load machine config from database if available
                if (App.CurrentMachineConfig == null)
                {
                    var savedConfig = await _dbService.GetMachineConfigAsync();
                    if (savedConfig != null)
                    {
                        App.CurrentMachineConfig = savedConfig;
                        System.Diagnostics.Debug.WriteLine($"[StartPage] Machine config loaded from database. OfflineMode: {savedConfig.OfflineMode}, PaymentType: {savedConfig.PaymentType}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[StartPage] No machine config found in database. Will sync from API when needed.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartPage] Error loading config: {ex.Message}");
            }
            
            // Enhanced fade in animations with easing
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(1.2)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Animate event chip
            if (EventChip != null)
            {
                EventChip.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            
            // Animate hero section (main content)
            if (HeroSection != null)
            {
                var heroFadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(1.2)),
                    BeginTime = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                HeroSection.BeginAnimation(UIElement.OpacityProperty, heroFadeIn);
            }
            
            // Button fade in with delay
            var buttonFadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StartButton.BeginAnimation(UIElement.OpacityProperty, buttonFadeIn);
            
            // Button slide up animation
            var buttonSlideUp = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var buttonTransform = new TranslateTransform();
            StartButton.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(1, 1, 0.5, 0.5),
                    buttonTransform
                }
            };
            buttonTransform.BeginAnimation(TranslateTransform.YProperty, buttonSlideUp);
            
            // Speak welcome message
            SpeakTextAsync("Welcome to Memora PhotoBooth");
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

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to style selection page
            _navigationService.NavigateTo(typeof(StyleSelectionPage));
        }

        private async Task<bool> SyncConfigAndFramesAsync()
        {
            if (_syncInProgress) return false;
            _syncInProgress = true;

            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                ErrorBorder.Visibility = Visibility.Collapsed;

                var apiConfig = await _apiService.GetMachineConfigAsync(_config.ApiSettings.MachineCode);

                if (apiConfig != null)
                {
                    await _dbService.SaveMachineConfigAsync(apiConfig);
                    App.CurrentMachineConfig = apiConfig;
                    System.Diagnostics.Debug.WriteLine("[StartPage] Settings sync: config saved");

                    var apiFrames = await _apiService.GetFrameTemplatesAsync(_config.ApiSettings.MachineCode);
                    if (apiFrames != null && apiFrames.Count > 0)
                    {
                        await _dbService.SaveFrameTemplatesAsync(apiFrames);
                        System.Diagnostics.Debug.WriteLine("[StartPage] Settings sync: frames saved");
                    }
                    return true;
                }

                // Fallback to saved config if API fails
                var saved = await _dbService.GetMachineConfigAsync();
                if (saved != null)
                {
                    App.CurrentMachineConfig = saved;
                    System.Diagnostics.Debug.WriteLine("[StartPage] Settings sync: using saved config");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartPage] Settings sync error: {ex.Message}");
                ErrorText.Text = $"Error: {ex.Message}";
                ErrorBorder.Visibility = Visibility.Visible;
                return false;
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                _syncInProgress = false;
            }
        }

        private void SettingsBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Toggle popup visibility
            if (SettingsPopup.IsOpen)
            {
                SettingsPopup.IsOpen = false;
            }
            else
            {
                // Fixed position: appear above the settings icon
                // Position: centered horizontally above the icon, with 5px gap
                SettingsPopup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
                {
                    // Center the popup horizontally above the icon
                    // X: Center popup relative to icon center (icon center - popup center)
                    // Y: Position above icon (negative Y to go up)
                    var xPosition = (targetSize.Width - popupSize.Width) / 2;
                    var yPosition = -popupSize.Height - 5; // 5px gap above icon
                    
                    var fixedPlacement = new CustomPopupPlacement
                    {
                        Point = new System.Windows.Point(xPosition, yPosition),
                        PrimaryAxis = PopupPrimaryAxis.None
                    };
                    
                    return new[] { fixedPlacement };
                };
                
                SettingsPopup.IsOpen = true;
            }
            e.Handled = true;
        }

        private async void SyncConfigButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
            
            if (_syncInProgress)
            {
                MessageBox.Show("Sync is already in progress. Please wait for it to complete.", 
                    "Sync In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _syncInProgress = true;
            
            // Hide error border initially
            ErrorBorder.Visibility = Visibility.Collapsed;
            
            // Show loader on settings icon
            await Dispatcher.InvokeAsync(() =>
            {
                SettingsIcon.Visibility = Visibility.Collapsed;
                SyncLoaderBorder.Visibility = Visibility.Visible;
                SyncProgressText.Visibility = Visibility.Visible;
                SyncProgressText.Text = "Syncing...";
                StartSyncLoaderAnimation();
            });
            
            Console.WriteLine($"[SYNC] Config and Frame Sync button clicked");
            Console.WriteLine($"[SYNC] Base URL: {_config.ApiSettings.BaseUrl}");
            
            try
            {
                var machineCode = await _dbService.GetAppSettingAsync("MachineCode") ?? _config.ApiSettings.MachineCode;
                Console.WriteLine($"[SYNC] Using Machine Code: {machineCode}");
                
                // Update progress text
                await Dispatcher.InvokeAsync(() =>
                {
                    SyncProgressText.Text = "Syncing config...";
                    SettingsBorder.ToolTip = "Syncing configuration...";
                });
                
                Console.WriteLine($"[SYNC] Starting API call: GetMachineConfigAsync");
                var apiConfig = await _apiService.GetMachineConfigAsync(machineCode);
                if (apiConfig != null)
                {
                    await _dbService.SaveMachineConfigAsync(apiConfig);
                    App.CurrentMachineConfig = apiConfig;
                    Console.WriteLine($"[SYNC] Machine config saved successfully");
                    
                    // Update progress text
                    await Dispatcher.InvokeAsync(() =>
                    {
                        SyncProgressText.Text = "Syncing frames...";
                        SettingsBorder.ToolTip = "Syncing frames...";
                    });
                    
                    Console.WriteLine($"[SYNC] Starting API call: GetFrameTemplatesAsync");
                    var apiFrames = await _apiService.GetFrameTemplatesAsync(machineCode);
                    if (apiFrames != null && apiFrames.Count > 0)
                    {
                        await _dbService.SaveFrameTemplatesAsync(apiFrames);
                        Console.WriteLine($"[SYNC] {apiFrames.Count} frame templates saved successfully");
                    }
                    else
                    {
                        Console.WriteLine($"[SYNC] WARNING: No frame templates received from API");
                    }
                    
                    Console.WriteLine($"[SYNC] SUCCESS: Config and frames synced successfully");
                    
                    // Hide loader on settings icon
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StopSyncLoaderAnimation();
                        SettingsIcon.Visibility = Visibility.Visible;
                        SyncLoaderBorder.Visibility = Visibility.Collapsed;
                        SyncProgressText.Visibility = Visibility.Collapsed;
                        SettingsBorder.ToolTip = null;
                    });
                    
                    // Show success message on page with success styling
                    ErrorText.Text = "Config and frames synced successfully.";
                    
                    // Change icon border and main border color to green for success
                    var innerBorder = ErrorBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        innerBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
                        innerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x00),
                            BlurRadius = 60,
                            ShadowDepth = 0,
                            Opacity = 0.5
                        };
                    }
                    
                    // Update error border elements for success
                    UpdateErrorBorderForSuccess();
                    
                    ErrorBorder.Visibility = Visibility.Visible;
                    
                    // Auto-hide success message after 3 seconds
                    await Task.Delay(3000);
                    ErrorBorder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Console.WriteLine($"[SYNC] FAILED: Unable to sync configuration - API returned null");
                    
                    // Hide loader on settings icon
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StopSyncLoaderAnimation();
                        SettingsIcon.Visibility = Visibility.Visible;
                        SyncLoaderBorder.Visibility = Visibility.Collapsed;
                        SyncProgressText.Visibility = Visibility.Collapsed;
                        SettingsBorder.ToolTip = null;
                    });
                    
                    // Show error on page with error styling
                    ErrorText.Text = "Unable to sync configuration. Please check your connection.";
                    
                    // Reset border color to red for error
                    var innerBorder = ErrorBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        innerBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60)); // Red
                        innerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0x00),
                            BlurRadius = 60,
                            ShadowDepth = 0,
                            Opacity = 0.7
                        };
                    }
                    
                    // Update error border elements for error
                    UpdateErrorBorderForError();
                    
                    ErrorBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[SYNC] EXCEPTION StackTrace: {ex.StackTrace}");
                
                // Hide loader on settings icon
                await Dispatcher.InvokeAsync(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                    SettingsBorder.ToolTip = null;
                });
                
                // Show error on page with error styling
                ErrorText.Text = $"Error syncing: {ex.Message}";
                
                // Reset border color to red for error
                var innerBorder = ErrorBorder.Child as Border;
                if (innerBorder != null)
                {
                    innerBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60)); // Red
                    innerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0x00),
                        BlurRadius = 60,
                        ShadowDepth = 0,
                        Opacity = 0.7
                    };
                }
                
                // Update error border elements for error
                UpdateErrorBorderForError();
                
                ErrorBorder.Visibility = Visibility.Visible;
            }
            finally
            {
                _syncInProgress = false;
                // Ensure loader is hidden
                Dispatcher.Invoke(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                    SettingsBorder.ToolTip = null;
                });
            }
        }

        private async void SyncOfflineDataButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
            
            if (_syncInProgress)
            {
                MessageBox.Show("Sync is already in progress. Please wait for it to complete.", 
                    "Sync In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Get all transaction data from database
                var transactions = await _dbService.GetAllTransactionDataAsync();

                if (transactions.Count == 0)
                {
                    MessageBox.Show("No offline transaction data found to sync.", 
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Perform sync
                await SyncOfflineTransactionsAsync(transactions, retryFailed: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting sync: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task SyncOfflineTransactionsAsync(List<TransactionData> transactions, bool retryFailed)
        {
            _syncInProgress = true;
            int syncedCount = 0;
            int failedCount = 0;
            var syncedIds = new List<string>();
            var failedTransactions = new List<TransactionData>();

            try
            {
                // Show loader on settings icon
                await Dispatcher.InvokeAsync(() =>
                {
                    SettingsIcon.Visibility = Visibility.Collapsed;
                    SyncLoaderBorder.Visibility = Visibility.Visible;
                    SyncProgressText.Visibility = Visibility.Visible;
                    UpdateSyncProgress(syncedCount, failedCount, transactions.Count);
                    StartSyncLoaderAnimation();
                });

                // Get machine code and site code from app settings as fallback
                var fallbackMachineCode = await _dbService.GetAppSettingAsync("MachineCode") ?? _config.ApiSettings.MachineCode;
                var fallbackSiteCode = await _dbService.GetAppSettingAsync("SiteCode") ?? _config.ApiSettings.SiteCode;

                // Process one at a time - wait for each to complete before starting next
                foreach (var txn in transactions)
                {
                    var orderId = txn.OrderId;
                    
                    if (string.IsNullOrEmpty(orderId))
                    {
                        failedCount++;
                        failedTransactions.Add(txn);
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Skipping transaction with empty order_id");
                        continue;
                    }

                    // Ensure machine_code and site_code are not empty - use fallback if needed
                    var machineCode = !string.IsNullOrWhiteSpace(txn.MachineCode) ? txn.MachineCode : fallbackMachineCode;
                    var siteCode = !string.IsNullOrWhiteSpace(txn.SiteCode) ? txn.SiteCode : fallbackSiteCode;

                    if (string.IsNullOrWhiteSpace(machineCode) || string.IsNullOrWhiteSpace(siteCode))
                    {
                        failedCount++;
                        failedTransactions.Add(txn);
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Skipping transaction {orderId}: MachineCode or SiteCode is empty. MachineCode: '{machineCode}', SiteCode: '{siteCode}'");
                        continue;
                    }

                    // Create a copy of the transaction with corrected machine_code and site_code
                    var correctedTxn = new TransactionData
                    {
                        OrderId = txn.OrderId,
                        MachineCode = machineCode,
                        SiteCode = siteCode,
                        Frame = txn.Frame,
                        Amount = txn.Amount,
                        CreatedAt = txn.CreatedAt,
                        SaleDate = txn.SaleDate,
                        PaymentMode = txn.PaymentMode,
                        TotalCopies = txn.TotalCopies,
                        TotalAmount = txn.TotalAmount,
                        EventId = txn.EventId,
                        OnEvent = txn.OnEvent
                    };

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Uploading transaction: {orderId}, MachineCode: '{machineCode}', SiteCode: '{siteCode}'");

                        // Upload - wait for this to complete before next
                        bool success = await _apiService.CreateCompletedSaleTransactionAsync(correctedTxn);

                        if (success)
                        {
                            syncedIds.Add(orderId);
                            syncedCount++;
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Success: {orderId}");
                        }
                        else
                        {
                            failedCount++;
                            failedTransactions.Add(txn);
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Failed: {orderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        failedTransactions.Add(txn);
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Error: {orderId}, {ex.Message}");
                    }

                    // Update progress on settings icon after each transaction is processed
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateSyncProgress(syncedCount, failedCount, transactions.Count);
                    });
                }

                // Delete successfully synced transactions from database
                if (syncedIds.Count > 0)
                {
                    foreach (var orderId in syncedIds)
                    {
                        try
                        {
                            await _dbService.DeleteTransactionDataAsync(orderId);
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Deleted synced transaction: {orderId}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Error deleting transaction {orderId}: {ex.Message}");
                        }
                    }
                }

                // Hide loader on settings icon
                await Dispatcher.InvokeAsync(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                });

                System.Diagnostics.Debug.WriteLine($"[OFFLINE_TXN_SYNC] Complete - Synced: {syncedCount}, Failed: {failedCount}");

                // Show summary message
                string message = $"Sync Complete!\n\n" +
                               $"Synced: {syncedCount}\n" +
                               $"Failed: {failedCount}";

                MessageBoxButton buttons = MessageBoxButton.OK;
                if (failedCount > 0)
                {
                    buttons = MessageBoxButton.YesNo;
                    message += "\n\nWould you like to retry the failed transactions?";
                }

                var result = MessageBox.Show(message, "Sync Offline Transactions", buttons, 
                    failedCount > 0 ? MessageBoxImage.Question : MessageBoxImage.Information);

                // If retry requested and there are failed transactions, retry them
                if (result == MessageBoxResult.Yes && failedTransactions.Count > 0)
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    await SyncOfflineTransactionsAsync(failedTransactions, retryFailed: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during sync: {ex.Message}", 
                    "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _syncInProgress = false;
                // Ensure loader is hidden on error
                Dispatcher.Invoke(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                    SettingsBorder.ToolTip = null; // Clear tooltip
                });
            }
        }

        private async void UploadOfflineFramesButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;

            if (_syncInProgress)
            {
                MessageBox.Show("Upload is already in progress. Please wait for it to complete.", 
                    "Upload In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Check machine config - skip if offline_mode is true
                var machineConfig = await _dbService.GetMachineConfigAsync();
                if (machineConfig != null && machineConfig.OfflineMode)
                {
                    System.Diagnostics.Debug.WriteLine("[OFFLINE_SYNC] Machine is in offline mode, skipping upload");
                    MessageBox.Show("Machine is in offline mode. Upload is disabled.", 
                        "Offline Mode", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get all offline frames
                var offlineFrames = await _dbService.GetAllOfflineFramesAsync();

                if (offlineFrames.Count == 0)
                {
                    MessageBox.Show("No offline frames to upload.", 
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Found {offlineFrames.Count} offline photos to sync");

                // Show confirmation
                var result = MessageBox.Show(
                    $"Found {offlineFrames.Count} offline frame(s) to upload.\n\n" +
                    "Do you want to proceed?",
                    "Upload Offline Frames",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Perform upload
                await UploadOfflineFramesAsync(offlineFrames, retryFailed: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting upload: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task UploadOfflineFramesAsync(List<OfflineFrame> framesToUpload, bool retryFailed)
        {
            _syncInProgress = true;
            int successCount = 0;
            int failedCount = 0;
            var syncedIds = new List<string>();
            var failedFrames = new List<OfflineFrame>();

            try
            {
                // Show loader on settings icon
                await Dispatcher.InvokeAsync(() =>
                {
                    SettingsIcon.Visibility = Visibility.Collapsed;
                    SyncLoaderBorder.Visibility = Visibility.Visible;
                    SyncProgressText.Visibility = Visibility.Visible;
                    UpdateSyncProgress(successCount, failedCount, framesToUpload.Count);
                    StartSyncLoaderAnimation();
                });

                // Process one at a time - wait for each to complete before starting next
                foreach (var frame in framesToUpload)
                {
                    var frameId = frame.FrameId;
                    var filePath = frame.FilePath;
                    
                    // Validate required fields
                    if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(frameId))
                    {
                        failedCount++;
                        failedFrames.Add(frame);
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Skipping frame with missing filePath or frame_id: {frameId}");
                        continue;
                    }

                    try
                    {
                        // Check if file exists
                        var cleanPath = filePath;
                        if (!System.IO.File.Exists(cleanPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] File missing: {cleanPath}");
                            failedCount++;
                            failedFrames.Add(frame);
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Uploading: {frameId}");

                        // Upload - wait for this to complete before next
                        bool success = await _apiService.UploadOfflineFrameAsync(
                            cleanPath,
                            frameId,
                            frame.MachineCode,
                            frame.SiteCode,
                            frame.CreatedAt
                        );

                        if (success)
                        {
                            syncedIds.Add(frameId);
                            successCount++;
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Success: {frameId}");
                            
                            // Delete file from disk after successful upload
                            try
                            {
                                System.IO.File.Delete(cleanPath);
                                System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Deleted file from disk: {cleanPath}");
                            }
                            catch (Exception deleteEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Warning: Could not delete file {cleanPath}: {deleteEx.Message}");
                            }
                            
                            // Remove from database
                            await _dbService.DeleteOfflineFrameAsync(frameId);
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Removed from database: {frameId}");
                        }
                        else
                        {
                            failedCount++;
                            failedFrames.Add(frame);
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Failed: {frameId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        failedFrames.Add(frame);
                        System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Error: {frameId}, {ex.Message}");
                    }

                    // Update progress on settings icon after each frame is processed
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateSyncProgress(successCount, failedCount, framesToUpload.Count);
                    });
                }

                // Hide loader on settings icon
                await Dispatcher.InvokeAsync(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                });

                // Show summary dialog
                var summaryDialog = new UploadSummaryDialog(successCount, failedCount);
                summaryDialog.Owner = Application.Current.MainWindow;
                summaryDialog.ShowDialog();

                // Handle dialog actions
                if (summaryDialog.DeleteFailedRequested && failedFrames.Count > 0)
                {
                    // Delete failed frames from database
                    int deletedCount = 0;
                    foreach (var frame in failedFrames)
                    {
                        try
                        {
                            await _dbService.DeleteOfflineFrameAsync(frame.FrameId);
                            deletedCount++;
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Deleted failed frame from database: {frame.FrameId}");
                        }
                        catch (Exception deleteEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[OFFLINE_SYNC] Error deleting frame {frame.FrameId} from database: {deleteEx.Message}");
                        }
                    }
                    
                    MessageBox.Show(
                        $"Deleted {deletedCount} failed frame(s) from the database.",
                        "Delete Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else if (summaryDialog.RetryRequested && failedFrames.Count > 0)
                {
                    // Retry failed frames
                    await System.Threading.Tasks.Task.Delay(500);
                    await UploadOfflineFramesAsync(failedFrames, retryFailed: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during upload: {ex.Message}", 
                    "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _syncInProgress = false;
                // Ensure loader is hidden on error
                Dispatcher.Invoke(() =>
                {
                    StopSyncLoaderAnimation();
                    SettingsIcon.Visibility = Visibility.Visible;
                    SyncLoaderBorder.Visibility = Visibility.Collapsed;
                    SyncProgressText.Visibility = Visibility.Collapsed;
                    SettingsBorder.ToolTip = null; // Clear tooltip
                });
            }
        }

        private async void UpdateCodesButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
            
            // Get current values from DB or config
            var currentMachineCode = await _dbService.GetAppSettingAsync("MachineCode") ?? _config.ApiSettings.MachineCode;
            var currentSiteCode = await _dbService.GetAppSettingAsync("SiteCode") ?? _config.ApiSettings.SiteCode;
            
            var dialog = new UpdateCodesDialog(currentMachineCode, currentSiteCode);
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Save to appsettings.json
                    _config.ApiSettings.MachineCode = dialog.MachineCode;
                    _config.ApiSettings.SiteCode = dialog.SiteCode;
                    SaveConfig(_config);
                    
                    // Save to database
                    await _dbService.SaveAppSettingAsync("MachineCode", dialog.MachineCode);
                    await _dbService.SaveAppSettingAsync("SiteCode", dialog.SiteCode);
                    
                    MessageBox.Show("Machine code and site code updated successfully. Please restart the application for changes to take full effect.", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating codes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void StartSyncLoaderAnimation()
        {
            var rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            SyncLoaderRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
        }

        private void StopSyncLoaderAnimation()
        {
            SyncLoaderRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private void UpdateSyncProgress(int synced, int failed, int total)
        {
            string progressText = $"✓{synced}";
            if (failed > 0)
            {
                progressText += $" ✗{failed}";
            }
            SyncProgressText.Text = progressText;
            
            // Update tooltip with detailed info
            if (total > 0)
            {
                SettingsBorder.ToolTip = $"Syncing offline frames...\n" +
                                        $"Total: {total}\n" +
                                        $"Synced: {synced}\n" +
                                        $"Failed: {failed}\n" +
                                        $"Progress: {synced + failed}/{total}";
            }
        }

        private void InitializeErrorBorderState()
        {
            // Find elements in ErrorBorder without Name attributes
            var stackPanel = ErrorBorder.Child as Border;
            if (stackPanel != null)
            {
                var innerStackPanel = stackPanel.Child as StackPanel;
                if (innerStackPanel != null)
                {
                    // Find icon border (first Border)
                    var iconBorder = innerStackPanel.Children.OfType<Border>().FirstOrDefault();
                    if (iconBorder != null)
                    {
                        var iconText = iconBorder.Child as TextBlock;
                        if (iconText != null)
                        {
                            iconText.Text = "⚠️";
                        }
                    }
                    
                    // Find title TextBlock (first TextBlock with FontSize 40)
                    var titleText = innerStackPanel.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => Math.Abs(tb.FontSize - 40) < 0.1);
                    if (titleText != null)
                    {
                        titleText.Text = "Connection Error";
                    }
                    
                    // Find retry button (Button with Content "RETRY")
                    var retryButton = innerStackPanel.Children.OfType<Button>()
                        .FirstOrDefault(btn => btn.Content?.ToString() == "RETRY");
                    if (retryButton != null)
                    {
                        retryButton.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void UpdateErrorBorderForSuccess()
        {
            var stackPanel = ErrorBorder.Child as Border;
            if (stackPanel != null)
            {
                var innerStackPanel = stackPanel.Child as StackPanel;
                if (innerStackPanel != null)
                {
                    // Find icon border and update
                    var iconBorder = innerStackPanel.Children.OfType<Border>().FirstOrDefault();
                    if (iconBorder != null)
                    {
                        iconBorder.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
                        var iconText = iconBorder.Child as TextBlock;
                        if (iconText != null)
                        {
                            iconText.Text = "✓";
                        }
                    }
                    
                    // Find title TextBlock and update
                    var titleText = innerStackPanel.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => Math.Abs(tb.FontSize - 40) < 0.1);
                    if (titleText != null)
                    {
                        titleText.Text = "Success";
                    }
                    
                    // Find retry button and hide
                    var retryButton = innerStackPanel.Children.OfType<Button>()
                        .FirstOrDefault(btn => btn.Content?.ToString() == "RETRY");
                    if (retryButton != null)
                    {
                        retryButton.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateErrorBorderForError()
        {
            var stackPanel = ErrorBorder.Child as Border;
            if (stackPanel != null)
            {
                var innerStackPanel = stackPanel.Child as StackPanel;
                if (innerStackPanel != null)
                {
                    // Find icon border and update
                    var iconBorder = innerStackPanel.Children.OfType<Border>().FirstOrDefault();
                    if (iconBorder != null)
                    {
                        iconBorder.Background = new SolidColorBrush(Color.FromRgb(0xE9, 0x45, 0x60)); // Red
                        var iconText = iconBorder.Child as TextBlock;
                        if (iconText != null)
                        {
                            iconText.Text = "⚠️";
                        }
                    }
                    
                    // Find title TextBlock and update
                    var titleText = innerStackPanel.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => Math.Abs(tb.FontSize - 40) < 0.1);
                    if (titleText != null)
                    {
                        titleText.Text = "Connection Error";
                    }
                    
                    // Find retry button and show
                    var retryButton = innerStackPanel.Children.OfType<Button>()
                        .FirstOrDefault(btn => btn.Content?.ToString() == "RETRY");
                    if (retryButton != null)
                    {
                        retryButton.Visibility = Visibility.Visible;
                    }
                }
            }
        }
    }
}

