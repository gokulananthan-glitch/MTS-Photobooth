using System.Windows;
using System.Windows.Controls;
using PhotoBooth.Pages;
using PhotoBooth.Services;

namespace PhotoBooth
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize navigation service
            App.NavigationService.Initialize(MainFrame);
            
            // Navigate to start page
            MainFrame.Navigate(new StartPage());
        }

        private void MainFrame_ContentRendered(object? sender, System.EventArgs e)
        {
            // Remove navigation history to prevent back button
            if (MainFrame.CanGoBack)
            {
                MainFrame.RemoveBackEntry();
            }
        }
    }
}
