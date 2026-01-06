using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;
using PhotoBooth.Utils;

namespace PhotoBooth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static NavigationService NavigationService { get; } = new NavigationService();
        public static MachineConfig? CurrentMachineConfig { get; set; }
        public static int SelectedStyle { get; set; }
        public static double Brightness { get; set; } = 1.0;
        public static bool Grayscale { get; set; } = false;
        public static System.Collections.Generic.List<System.Windows.Media.Imaging.BitmapImage> CapturedImages { get; set; } = new();
        public static int RetakePhotoIndex { get; set; } = -1; // -1 means normal mode, >= 0 means retake mode
        public static string? TempFramePath { get; set; } // Temporary frame file path for current session
        public static Services.WebcamService? WebcamService { get; set; } // Shared webcam service instance
        public static int NumberOfCopies { get; set; } = 1; // Number of copies selected in payment page
        public static TransactionData? PendingTransactionData { get; set; } // Pending transaction data from API

        private static DispatcherTimer? _maintenanceTimer;
        private static DateTime _applicationStartTime;
        private static int _sessionCount = 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            _applicationStartTime = DateTime.Now;
            
            // Initialize resource management
            ResourceManager.Initialize();
            System.Diagnostics.Debug.WriteLine("[App] Application started with resource management");
            
            // Start periodic maintenance timer for long-running sessions
            StartMaintenanceTimer();
        }

        private static void StartMaintenanceTimer()
        {
            // Run maintenance every 30 minutes
            _maintenanceTimer = new DispatcherTimer();
            _maintenanceTimer.Interval = TimeSpan.FromMinutes(30);
            _maintenanceTimer.Tick += MaintenanceTimer_Tick;
            _maintenanceTimer.Start();
            
            System.Diagnostics.Debug.WriteLine("[App] Maintenance timer started (30 min interval)");
        }

        private static void MaintenanceTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var uptime = DateTime.Now - _applicationStartTime;
                System.Diagnostics.Debug.WriteLine($"[App] === Periodic Maintenance (Uptime: {uptime.TotalHours:F1}h) ===");
                
                // Clean up truly old temporary files (> 2 hours)
                // Session-specific cleanup happens on StartPage
                CleanupOldTempFiles();
                
                // Gentle garbage collection - session cleanup is more aggressive
                ResourceManager.OptimizeMemory(aggressive: false);
                
                // Log memory stats
                ResourceManager.LogMemoryStats($"(After {uptime.TotalHours:F1}h uptime)");
                
                System.Diagnostics.Debug.WriteLine($"[App] Total sessions processed: {_sessionCount}");
                System.Diagnostics.Debug.WriteLine("[App] === Maintenance Complete ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Maintenance error: {ex.Message}");
            }
        }

        private static void CleanupOldTempFiles()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var cutoffTime = DateTime.Now.AddHours(-2); // Only truly old files
                int deletedCount = 0;
                
                // Clean up old frame files (session-specific cleanup happens on StartPage)
                foreach (var file in Directory.GetFiles(tempPath, "frame_*.jpg"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoffTime)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch { /* Ignore locked files */ }
                }
                
                foreach (var file in Directory.GetFiles(tempPath, "frame_composite_*.png"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoffTime)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch { /* Ignore locked files */ }
                }
                
                if (deletedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Cleaned up {deletedCount} old temp files");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Temp file cleanup error: {ex.Message}");
            }
        }

        public static void IncrementSessionCount()
        {
            _sessionCount++;
            System.Diagnostics.Debug.WriteLine($"[App] Session completed. Total: {_sessionCount}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Stop maintenance timer
                _maintenanceTimer?.Stop();
                
                // Log final statistics
                var uptime = DateTime.Now - _applicationStartTime;
                System.Diagnostics.Debug.WriteLine($"[App] === Shutdown Statistics ===");
                System.Diagnostics.Debug.WriteLine($"[App] Total uptime: {uptime.TotalHours:F2} hours");
                System.Diagnostics.Debug.WriteLine($"[App] Total sessions: {_sessionCount}");
                System.Diagnostics.Debug.WriteLine($"[App] Avg session time: {(uptime.TotalMinutes / Math.Max(_sessionCount, 1)):F1} min");
                
                // Cleanup shared webcam service
                if (WebcamService != null)
                {
                    try
                    {
                        WebcamService.StopCapture();
                        WebcamService.Dispose();
                        WebcamService = null;
                        System.Diagnostics.Debug.WriteLine("[App] Webcam service disposed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] Error disposing webcam service: {ex.Message}");
                    }
                }
                
                // Cleanup resources
                ResourceManager.Shutdown();
                System.Diagnostics.Debug.WriteLine("[App] Application exiting cleanly");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Shutdown error: {ex.Message}");
            }
            
            base.OnExit(e);
        }
    }
}
