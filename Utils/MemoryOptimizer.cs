using System;
using System.Diagnostics;
using System.Windows;

namespace PhotoBooth.Utils
{
    /// <summary>
    /// Utility class for memory optimization and cleanup
    /// </summary>
    public static class MemoryOptimizer
    {
        private static long _lastMemoryCheck = 0;
        private static readonly long MEMORY_CHECK_INTERVAL_MS = 30000; // 30 seconds

        /// <summary>
        /// Force garbage collection with aggressive settings
        /// </summary>
        public static void ForceGarbageCollection()
        {
            try
            {
                Debug.WriteLine("[MemoryOptimizer] Forcing garbage collection...");
                
                // Clear image cache if available
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Clear any cached BitmapImage objects
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                        GC.WaitForPendingFinalizers();
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    });
                }
                else
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                }
                
                Debug.WriteLine("[MemoryOptimizer] Garbage collection completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] GC error: {ex.Message}");
            }
        }

        /// <summary>
        /// Optimized garbage collection (less aggressive)
        /// </summary>
        public static void OptimizedGarbageCollection()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                Debug.WriteLine("[MemoryOptimizer] Optimized GC completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] Optimized GC error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check memory usage and trigger GC if threshold exceeded
        /// </summary>
        public static void CheckAndOptimizeMemory(bool force = false)
        {
            try
            {
                long currentTicks = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                
                if (!force && (currentTicks - _lastMemoryCheck) < MEMORY_CHECK_INTERVAL_MS)
                {
                    return; // Too soon since last check
                }
                
                _lastMemoryCheck = currentTicks;
                
                // Get current memory usage
                long memoryUsed = GC.GetTotalMemory(false);
                double memoryMB = memoryUsed / (1024.0 * 1024.0);
                
                Debug.WriteLine($"[MemoryOptimizer] Current memory usage: {memoryMB:F2} MB");
                
                // If memory usage is high (over 500MB), trigger GC
                if (memoryMB > 500 || force)
                {
                    Debug.WriteLine("[MemoryOptimizer] High memory usage detected, triggering GC");
                    OptimizedGarbageCollection();
                    
                    long memoryAfter = GC.GetTotalMemory(true);
                    double memoryAfterMB = memoryAfter / (1024.0 * 1024.0);
                    Debug.WriteLine($"[MemoryOptimizer] Memory after GC: {memoryAfterMB:F2} MB (freed: {(memoryMB - memoryAfterMB):F2} MB)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] Memory check error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all cached images and force GC
        /// </summary>
        public static void ClearImageCache()
        {
            try
            {
                Debug.WriteLine("[MemoryOptimizer] Clearing image cache...");
                
                // Trigger aggressive GC to clear image cache
                ForceGarbageCollection();
                
                Debug.WriteLine("[MemoryOptimizer] Image cache cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] Clear cache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Log current memory statistics
        /// </summary>
        public static void LogMemoryStats()
        {
            try
            {
                long totalMemory = GC.GetTotalMemory(false);
                double totalMB = totalMemory / (1024.0 * 1024.0);
                
                Process currentProcess = Process.GetCurrentProcess();
                double workingSetMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
                double privateMemoryMB = currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0);
                
                Debug.WriteLine("=== Memory Statistics ===");
                Debug.WriteLine($"GC Total Memory: {totalMB:F2} MB");
                Debug.WriteLine($"Working Set: {workingSetMB:F2} MB");
                Debug.WriteLine($"Private Memory: {privateMemoryMB:F2} MB");
                Debug.WriteLine($"GC Generation 0 Collections: {GC.CollectionCount(0)}");
                Debug.WriteLine($"GC Generation 1 Collections: {GC.CollectionCount(1)}");
                Debug.WriteLine($"GC Generation 2 Collections: {GC.CollectionCount(2)}");
                Debug.WriteLine("========================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] Stats error: {ex.Message}");
            }
        }
    }
}


