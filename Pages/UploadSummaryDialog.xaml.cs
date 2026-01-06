using System.Windows;

namespace PhotoBooth.Pages
{
    public partial class UploadSummaryDialog : Window
    {
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public bool RetryRequested { get; private set; }
        public bool DeleteFailedRequested { get; private set; }

        public UploadSummaryDialog(int successCount, int failedCount)
        {
            InitializeComponent();
            SuccessCount = successCount;
            FailedCount = failedCount;
            RetryRequested = false;
            DeleteFailedRequested = false;

            UpdateSummaryText();

            // Hide retry and delete buttons if no failures
            if (failedCount == 0)
            {
                RetryButton.Visibility = Visibility.Collapsed;
                DeleteFailedButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSummaryText()
        {
            if (FailedCount == 0)
            {
                SummaryText.Text = $"✓ All frames uploaded successfully!\n\n" +
                                  $"Successfully synced: {SuccessCount} frame(s)";
                SummaryText.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else if (SuccessCount == 0)
            {
                SummaryText.Text = $"✗ All uploads failed!\n\n" +
                                  $"Failed: {FailedCount} frame(s)\n\n" +
                                  $"Options:\n" +
                                  $"• Click 'Retry Failed' to attempt upload again\n" +
                                  $"• Click 'Delete Failed' to remove from database";
                SummaryText.Foreground = System.Windows.Media.Brushes.LightCoral;
            }
            else
            {
                SummaryText.Text = $"Upload completed with some errors:\n\n" +
                                  $"✓ Successfully synced: {SuccessCount} frame(s)\n" +
                                  $"✗ Failed: {FailedCount} frame(s)\n\n" +
                                  $"Options:\n" +
                                  $"• Click 'Retry Failed' to upload failed frames again\n" +
                                  $"• Click 'Delete Failed' to remove from database";
                SummaryText.Foreground = System.Windows.Media.Brushes.LightYellow;
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryRequested = true;
            DialogResult = true;
            Close();
        }

        private void DeleteFailedButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete {FailedCount} failed frame(s) from the database?\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DeleteFailedRequested = true;
                DialogResult = true;
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

