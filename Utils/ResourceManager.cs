using System;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoBooth.Utils
{
    /// <summary>
    /// Centralized resource management for memory optimization
    /// </summary>
    public static class ResourceManager
    {
        private static long _initialMemory = 0;
        private static int _gcCollectionCount = 0;

        /// <summary>
        /// Initialize resource tracking
        /// </summary>
        public static void Initialize()
        {
            _initialMemory = GC.GetTotalMemory(false);
            Debug.WriteLine($"[ResourceManager] Initial memory: {_initialMemory / 1024 / 1024:F2} MB");
        }

        /// <summary>
        /// Perform optimized garbage collection
        /// </summary>
        public static void OptimizeMemory(bool aggressive = false)
        {
            try
            {
                _gcCollectionCount++;
                
                var beforeGC = GC.GetTotalMemory(false);
                
                if (aggressive)
                {
                    // Aggressive cleanup for page transitions
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                }
                else
                {
                    // Gentle cleanup for regular operations
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                }
                
                var afterGC = GC.GetTotalMemory(false);
                var freed = (beforeGC - afterGC) / 1024 / 1024;
                
                Debug.WriteLine($"[ResourceManager] GC #{_gcCollectionCount} freed: {freed:F2} MB (Current: {afterGC / 1024 / 1024:F2} MB)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceManager] OptimizeMemory error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear cached resources
        /// </summary>
        public static void ClearCaches()
        {
            try
            {
                // Clear image processor cache
                ImageProcessor.ClearCache();
                
                Debug.WriteLine("[ResourceManager] Caches cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceManager] ClearCaches error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current memory usage statistics
        /// </summary>
        public static MemoryStats GetMemoryStats()
        {
            return new MemoryStats
            {
                TotalMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                InitialMemoryMB = _initialMemory / 1024.0 / 1024.0
            };
        }

        /// <summary>
        /// Log memory statistics
        /// </summary>
        public static void LogMemoryStats(string context = "")
        {
            var stats = GetMemoryStats();
            Debug.WriteLine($"[ResourceManager] Memory Stats {context}:");
            Debug.WriteLine($"  Total Memory: {stats.TotalMemoryMB:F2} MB");
            Debug.WriteLine($"  Since Start: {(stats.TotalMemoryMB - stats.InitialMemoryMB):F2} MB");
            Debug.WriteLine($"  Gen0/Gen1/Gen2: {stats.Gen0Collections}/{stats.Gen1Collections}/{stats.Gen2Collections}");
        }

        /// <summary>
        /// Cleanup resources on application exit
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                Debug.WriteLine("[ResourceManager] Shutdown initiated");
                ClearCaches();
                OptimizeMemory(aggressive: true);
                LogMemoryStats("(Final)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceManager] Shutdown error: {ex.Message}");
            }
        }
    }

    public class MemoryStats
    {
        public double TotalMemoryMB { get; set; }
        public double InitialMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }
}

