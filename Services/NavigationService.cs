using System;
using System.Windows.Controls;
using PhotoBooth.Utils;

namespace PhotoBooth.Services
{
    public class NavigationService
    {
        private Frame? _frame;

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public void NavigateTo(Page page)
        {
            if (_frame != null)
            {
                // Clean up resources before navigation
                ResourceManager.ClearCaches();
                ResourceManager.OptimizeMemory(aggressive: false);
                
                _frame.Navigate(page);
            }
        }

        public void NavigateTo(Type pageType)
        {
            if (_frame == null)
            {
                System.Diagnostics.Debug.WriteLine("[NavigationService] Frame not initialized! Cannot navigate.");
                return;
            }

            try
            {
                // Clean up resources before navigation
                ResourceManager.ClearCaches();
                ResourceManager.OptimizeMemory(aggressive: false);
                
                var page = Activator.CreateInstance(pageType) as Page;
                if (page != null)
                {
                    _frame.Navigate(page);
                    
                    // Log memory after navigation
                    ResourceManager.LogMemoryStats($"(After {pageType.Name})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] Failed to create instance of {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Error navigating to {pageType.Name}: {ex.Message}");
                throw;
            }
        }
    }
}

