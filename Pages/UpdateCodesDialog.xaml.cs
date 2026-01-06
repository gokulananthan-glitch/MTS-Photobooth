using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoBooth.Pages
{
    public partial class UpdateCodesDialog : Window
    {
        public string MachineCode { get; private set; } = string.Empty;
        public string SiteCode { get; private set; } = string.Empty;

        // Windows API for backdrop blur
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        public UpdateCodesDialog(string? currentMachineCode, string? currentSiteCode)
        {
            InitializeComponent();
            MachineCodeTextBox.Text = currentMachineCode ?? string.Empty;
            SiteCodeTextBox.Text = currentSiteCode ?? string.Empty;
            
            // Enable backdrop blur and set focus when loaded
            Loaded += (s, e) =>
            {
                EnableBlur();
                MachineCodeTextBox.Focus();
            };
        }

        private void EnableBlur()
        {
            var windowHelper = new System.Windows.Interop.WindowInteropHelper(this);
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags = 2,
                GradientColor = unchecked((int)0x99000000) // Semi-transparent black
            };

            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Move focus to next textbox or save
                if (sender == MachineCodeTextBox)
                {
                    SiteCodeTextBox.Focus();
                    SiteCodeTextBox.SelectAll();
                }
                else if (sender == SiteCodeTextBox)
                {
                    SaveButton_Click(SaveButton, new RoutedEventArgs());
                }
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(CancelButton, new RoutedEventArgs());
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            MachineCode = MachineCodeTextBox.Text.Trim();
            SiteCode = SiteCodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(MachineCode))
            {
                MessageBox.Show("Please enter a machine code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                MachineCodeTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(SiteCode))
            {
                MessageBox.Show("Please enter a site code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                SiteCodeTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
