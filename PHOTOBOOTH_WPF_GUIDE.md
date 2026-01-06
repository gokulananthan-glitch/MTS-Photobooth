# Photobooth Application Development Guide - WPF

## Overview
This guide explains how to create a WPF-based photobooth application using Canon EDSDK for live preview and photo capture, based on the existing Windows Forms camera control application.

## Current Application Architecture

### Key Components:
1. **EDSDK.cs** - Canon EDSDK wrapper library
2. **CameraController.cs** - Main controller handling camera operations
3. **CameraModel.cs** - Camera state and properties model
4. **CommandProcessor.cs** - Command queue processor for camera operationsa
5. **EVF (Electronic View Finder)** - Live preview functionality
   - `StartEvfCommand.cs` - Starts live view
   - `DownloadEvfCommand.cs` - Downloads live view frames
   - `EndEvfCommand.cs` - Stops live view
6. **TakePictureCommand.cs** - Captures photos

### Current Flow:
1. Initialize EDSDK → Detect camera → Open session
2. Start EVF → Download frames continuously → Display in PictureBox
3. Capture photo → Download image → Save to disk

---

## Steps to Create WPF Photobooth Application

### Step 1: Create New WPF Project

1. **Create WPF Application Project**
   - Target Framework: .NET Framework 4.8 (or .NET 6+)
   - Project Type: WPF App (.NET Framework)
   - Name: `PhotoBoothWPF`

2. **Add Required References**
   - Keep existing EDSDK references
   - Add WPF-specific references:
     - `System.Windows.Media.Imaging` (for image handling)
     - `System.Windows.Threading` (for UI thread operations)

### Step 2: Reuse Existing Camera Control Logic

**Option A: Reference Existing Project**
- Add the existing `CameraControl` project as a reference
- Reuse `CameraController`, `CameraModel`, `CommandProcessor`, and command classes

**Option B: Copy and Adapt**
- Copy camera control classes to new WPF project
- Adapt WinForms-specific code to WPF

### Step 3: Create WPF Main Window

**MainWindow.xaml Structure:**
```xml
<Window x:Class="PhotoBoothWPF.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PhotoBooth" WindowState="Maximized" WindowStyle="None">
    
    <Grid>
        <!-- Live Preview Area -->
        <Image x:Name="LivePreviewImage" 
               Stretch="Uniform" 
               HorizontalAlignment="Center" 
               VerticalAlignment="Center"/>
        
        <!-- Capture Button -->
        <Button x:Name="CaptureButton" 
                Content="Capture Photo" 
                Width="200" Height="80"
                FontSize="24"
                HorizontalAlignment="Center"
                VerticalAlignment="Bottom"
                Margin="0,0,0,50"
                Click="CaptureButton_Click"/>
        
        <!-- Countdown Timer (optional) -->
        <TextBlock x:Name="CountdownText" 
                   FontSize="120" 
                   Foreground="White"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   Visibility="Collapsed"/>
    </Grid>
</Window>
```

### Step 4: Create WPF EVF Image Display Component

**Key Differences from WinForms:**
- WinForms uses `PictureBox` → WPF uses `Image` control
- WinForms uses `System.Drawing.Bitmap` → WPF uses `System.Windows.Media.Imaging.BitmapImage`
- Thread marshalling: `Control.Invoke()` → `Dispatcher.Invoke()`

**Create EvfImageControl.cs:**
```csharp
using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace PhotoBoothWPF
{
    public class EvfImageControl : Image, IObserver
    {
        private CameraModel _model;
        private ActionSource _actionSource;
        private bool _active = false;

        public void SetActionSource(ref ActionSource actionSource)
        {
            _actionSource = actionSource;
        }

        public void Update(Observable from, CameraEvent e)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateInternal(from, e);
            }
            else
            {
                Dispatcher.Invoke(new Action(() => UpdateInternal(from, e)));
            }
        }

        private void UpdateInternal(Observable from, CameraEvent e)
        {
            CameraEvent.Type eventType = e.GetEventType();
            _model = (CameraModel)from;

            switch (eventType)
            {
                case CameraEvent.Type.EVFDATA_CHANGED:
                    IntPtr evfDataSetPtr = e.GetArg();
                    EVFDataSet evfDataSet = (EVFDataSet)Marshal.PtrToStructure(evfDataSetPtr, typeof(EVFDataSet));
                    OnDrawImage(evfDataSet);
                    
                    uint propertyID = EDSDKLib.EDSDK.PropID_FocusInfo;
                    _actionSource.FireEvent(ActionEvent.Command.GET_PROPERTY, (IntPtr)propertyID);
                    _actionSource.FireEvent(ActionEvent.Command.DOWNLOAD_EVF, IntPtr.Zero);
                    break;

                case CameraEvent.Type.PROPERTY_CHANGED:
                    uint propID = (uint)e.GetArg();
                    if (propID == EDSDKLib.EDSDK.PropID_Evf_OutputDevice)
                    {
                        uint device = _model.EvfOutputDevice;
                        if (!_active && (device & EDSDKLib.EDSDK.EvfOutputDevice_PC) != 0)
                        {
                            _active = true;
                            _actionSource.FireEvent(ActionEvent.Command.DOWNLOAD_EVF, IntPtr.Zero);
                        }
                        if (_active && (device & EDSDKLib.EDSDK.EvfOutputDevice_PC) == 0)
                        {
                            _active = false;
                        }
                    }
                    break;
            }
        }

        private void OnDrawImage(EVFDataSet evfDataSet)
        {
            IntPtr evfStream;
            UInt64 streamLength;

            EDSDKLib.EDSDK.EdsGetPointer(evfDataSet.stream, out evfStream);
            EDSDKLib.EDSDK.EdsGetLength(evfDataSet.stream, out streamLength);

            byte[] data = new byte[(int)streamLength];
            Marshal.Copy(evfStream, data, 0, (int)streamLength);

            // Convert to WPF BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = new MemoryStream(data);
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // Make thread-safe

            if (_model.isEvfEnable)
            {
                Source = bitmapImage;
            }
            else
            {
                Source = null;
            }
        }
    }
}
```

### Step 5: Implement Main Window Code-Behind

**MainWindow.xaml.cs:**
```csharp
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace PhotoBoothWPF
{
    public partial class MainWindow : Window, IObserver
    {
        private CameraController _controller;
        private ActionSource _actionSource = new ActionSource();
        private CameraModel _model;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCamera();
        }

        private void InitializeCamera()
        {
            // Initialize EDSDK (similar to Program.cs)
            uint err = EDSDKLib.EDSDK.EdsInitializeSDK();
            if (err != EDSDKLib.EDSDK.EDS_ERR_OK)
            {
                MessageBox.Show("Failed to initialize EDSDK");
                return;
            }

            // Get camera list
            IntPtr cameraList = IntPtr.Zero;
            err = EDSDKLib.EDSDK.EdsGetCameraList(out cameraList);
            if (err != EDSDKLib.EDSDK.EDS_ERR_OK)
            {
                MessageBox.Show("No camera found");
                return;
            }

            // Get first camera
            IntPtr camera = IntPtr.Zero;
            err = EDSDKLib.EDSDK.EdsGetChildAtIndex(cameraList, 0, out camera);
            EDSDKLib.EDSDK.EdsRelease(cameraList);

            // Create model and controller
            _model = new CameraModel(camera);
            _controller = new CameraController(ref _model);

            // Set up event handlers
            SetupEventHandlers();

            // Start controller
            _controller.Run();

            // Register as observer
            _model.Add(ref this);

            // Start live view
            System.Threading.Thread.Sleep(1000);
            _actionSource.FireEvent(ActionEvent.Command.START_EVF, IntPtr.Zero);
        }

        private void SetupEventHandlers()
        {
            // Set up EDSDK event handlers (similar to Program.cs)
            EDSDKLib.EDSDK.EdsPropertyEventHandler handlePropertyEvent = 
                new EDSDKLib.EDSDK.EdsPropertyEventHandler(CameraEventListener.HandlePropertyEvent);
            EDSDKLib.EDSDK.EdsObjectEventHandler handleObjectEvent = 
                new EDSDKLib.EDSDK.EdsObjectEventHandler(CameraEventListener.HandleObjectEvent);
            EDSDKLib.EDSDK.EdsStateEventHandler handleStateEvent = 
                new EDSDKLib.EDSDK.EdsStateEventHandler(CameraEventListener.HandleStateEvent);

            GCHandle handle = GCHandle.Alloc(_controller);
            IntPtr ptr = GCHandle.ToIntPtr(handle);

            EDSDKLib.EDSDK.EdsSetPropertyEventHandler(_model.Camera, 
                EDSDKLib.EDSDK.PropertyEvent_All, handlePropertyEvent, ptr);
            EDSDKLib.EDSDK.EdsSetObjectEventHandler(_model.Camera, 
                EDSDKLib.EDSDK.ObjectEvent_All, handleObjectEvent, ptr);
            EDSDKLib.EDSDK.EdsSetCameraStateEventHandler(_model.Camera, 
                EDSDKLib.EDSDK.StateEvent_All, handleStateEvent, ptr);
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // Start countdown (optional)
            StartCountdown(() =>
            {
                // Capture photo
                _actionSource.FireEvent(ActionEvent.Command.TAKE_PICTURE, IntPtr.Zero);
            });
        }

        private void StartCountdown(Action onComplete)
        {
            // Implement countdown timer (3, 2, 1)
            // Then call onComplete
            onComplete();
        }

        public void Update(Observable from, CameraEvent e)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateInternal(from, e);
            }
            else
            {
                Dispatcher.Invoke(new Action(() => UpdateInternal(from, e)));
            }
        }

        private void UpdateInternal(Observable from, CameraEvent e)
        {
            CameraEvent.Type eventType = e.GetEventType();

            switch (eventType)
            {
                case CameraEvent.Type.SHUT_DOWN:
                    MessageBox.Show("Camera disconnected");
                    Close();
                    break;

                case CameraEvent.Type.EVFDATA_CHANGED:
                    // Handle EVF data (if not using EvfImageControl)
                    break;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _actionSource.FireEvent(ActionEvent.Command.END_EVF, IntPtr.Zero);
            _actionSource.FireEvent(ActionEvent.Command.CLOSING, IntPtr.Zero);
            base.OnClosing(e);
        }
    }
}
```

### Step 6: Handle Image Download After Capture

**Add to MainWindow.xaml.cs:**
```csharp
private void HandleImageDownload(IntPtr objectRef)
{
    // Download captured image
    _actionSource.FireEvent(ActionEvent.Command.DOWNLOAD, objectRef);
}

// In Update method, handle ObjectEvent_DirItemCreated
case CameraEvent.Type.OBJECT_CREATED:
    IntPtr objectRef = e.GetArg();
    HandleImageDownload(objectRef);
    break;
```

### Step 7: Add Photobooth-Specific Features

**Features to Add:**
1. **Countdown Timer**
   - 3-2-1 countdown before capture
   - Visual/audio feedback

2. **Photo Preview**
   - Show captured photo after capture
   - Allow retake option

3. **Photo Saving**
   - Save to specified directory
   - Generate unique filenames (timestamp-based)

4. **Fullscreen Mode**
   - Maximize window
   - Hide window decorations

5. **Touch-Friendly UI**
   - Large buttons for touch screens
   - Simple, clean interface

### Step 8: Project Configuration

**Update .csproj file:**
```xml
<PropertyGroup>
  <TargetFramework>net48</TargetFramework>
  <UseWPF>true</UseWPF>
</PropertyGroup>

<ItemGroup>
  <!-- Reference existing CameraControl project or add files -->
  <ProjectReference Include="..\CameraControl\CameraControl.csproj" />
</ItemGroup>
```

---

## Key Differences: WinForms vs WPF

| Aspect | WinForms | WPF |
|--------|----------|-----|
| Image Display | `PictureBox` | `Image` control |
| Bitmap Type | `System.Drawing.Bitmap` | `BitmapImage` |
| Thread Marshalling | `Control.Invoke()` | `Dispatcher.Invoke()` |
| Drawing | `Graphics` object | `DrawingContext` or `Image` |
| Layout | Absolute positioning | Layout panels (Grid, StackPanel) |

---

## Implementation Checklist

- [ ] Create WPF project
- [ ] Add EDSDK references
- [ ] Copy/adapt camera control classes
- [ ] Create WPF EVF image display component
- [ ] Implement main window with live preview
- [ ] Add capture button functionality
- [ ] Implement image download handler
- [ ] Add countdown timer
- [ ] Add photo preview after capture
- [ ] Implement photo saving
- [ ] Test live preview
- [ ] Test photo capture
- [ ] Add error handling
- [ ] Add UI polish (styling, animations)

---

## Additional Resources

1. **EDSDK Documentation**: Canon EDSDK API Reference
2. **WPF Image Handling**: MSDN - BitmapImage Class
3. **WPF Threading**: MSDN - Threading Model

---

## Notes

- Ensure EDSDK DLLs are in the output directory
- Handle camera disconnection gracefully
- Consider using MVVM pattern for better separation of concerns
- Add logging for debugging camera operations
- Test with different Canon camera models

/******************************************************************************
*                                                                             *
*   PhotoBooth WPF Application - Main Window Code-Behind                      *
*                                                                             *
*   Description: WPF implementation of photobooth with live preview          *
*                                                                             *
*******************************************************************************/

using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PhotoBoothWPF
{
    public partial class PhotoBoothWindow : Window, IObserver
    {
        private CameraController _controller;
        private ActionSource _actionSource = new ActionSource();
        private CameraModel _model;
        private bool _isCapturing = false;
        private BitmapImage _capturedPhoto = null;
        private string _saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoBooth");

        public PhotoBoothWindow()
        {
            InitializeComponent();
            
            // Create save directory if it doesn't exist
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            InitializeCamera();
        }

        private void InitializeCamera()
        {
            try
            {
                // Initialize EDSDK
                uint err = EDSDKLib.EDSDK.EdsInitializeSDK();
                if (err != EDSDKLib.EDSDK.EDS_ERR_OK)
                {
                    CameraStatusText.Text = "Camera: Failed to initialize SDK";
                    MessageBox.Show("Failed to initialize EDSDK. Error: " + err);
                    return;
                }

                // Get camera list
                IntPtr cameraList = IntPtr.Zero;
                err = EDSDKLib.EDSDK.EdsGetCameraList(out cameraList);
                if (err != EDSDKLib.EDSDK.EDS_ERR_OK)
                {
                    CameraStatusText.Text = "Camera: No camera found";
                    MessageBox.Show("No camera detected. Please connect a Canon camera.");
                    return;
                }

                // Get number of cameras
                int count = 0;
                err = EDSDKLib.EDSDK.EdsGetChildCount(cameraList, out count);
                if (count == 0)
                {
                    CameraStatusText.Text = "Camera: No camera found";
                    EDSDKLib.EDSDK.EdsRelease(cameraList);
                    return;
                }

                // Get first camera
                IntPtr camera = IntPtr.Zero;
                err = EDSDKLib.EDSDK.EdsGetChildAtIndex(cameraList, 0, out camera);
                EDSDKLib.EDSDK.EdsRelease(cameraList);

                if (err != EDSDKLib.EDSDK.EDS_ERR_OK || camera == IntPtr.Zero)
                {
                    CameraStatusText.Text = "Camera: Failed to connect";
                    return;
                }

                // Get device info
                EDSDKLib.EDSDK.EdsDeviceInfo deviceInfo;
                err = EDSDKLib.EDSDK.EdsGetDeviceInfo(camera, out deviceInfo);
                if (err == EDSDKLib.EDSDK.EDS_ERR_OK)
                {
                    CameraStatusText.Text = "Camera: " + deviceInfo.szDeviceDescription;
                }

                // Create model and controller
                _model = new CameraModel(camera);
                _controller = new CameraController(ref _model);

                // Set up event handlers
                SetupEventHandlers();

                // Register as observer
                _model.Add(ref this);

                // Start controller
                _controller.Run();

                // Wait a bit then start live view
                Task.Delay(1000).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _actionSource.FireEvent(ActionEvent.Command.START_EVF, IntPtr.Zero);
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing camera: " + ex.Message);
                CameraStatusText.Text = "Camera: Error - " + ex.Message;
            }
        }

        private void SetupEventHandlers()
        {
            try
            {
                EDSDKLib.EDSDK.EdsPropertyEventHandler handlePropertyEvent =
                    new EDSDKLib.EDSDK.EdsPropertyEventHandler(CameraEventListener.HandlePropertyEvent);
                EDSDKLib.EDSDK.EdsObjectEventHandler handleObjectEvent =
                    new EDSDKLib.EDSDK.EdsObjectEventHandler(CameraEventListener.HandleObjectEvent);
                EDSDKLib.EDSDK.EdsStateEventHandler handleStateEvent =
                    new EDSDKLib.EDSDK.EdsStateEventHandler(CameraEventListener.HandleStateEvent);

                GCHandle handle = GCHandle.Alloc(_controller);
                IntPtr ptr = GCHandle.ToIntPtr(handle);

                uint err = EDSDKLib.EDSDK.EdsSetPropertyEventHandler(_model.Camera,
                    EDSDKLib.EDSDK.PropertyEvent_All, handlePropertyEvent, ptr);
                err = EDSDKLib.EDSDK.EdsSetObjectEventHandler(_model.Camera,
                    EDSDKLib.EDSDK.ObjectEvent_All, handleObjectEvent, ptr);
                err = EDSDKLib.EDSDK.EdsSetCameraStateEventHandler(_model.Camera,
                    EDSDKLib.EDSDK.StateEvent_All, handleStateEvent, ptr);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error setting up event handlers: " + ex.Message);
            }
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturing) return;

            _isCapturing = true;
            CaptureButton.IsEnabled = false;

            // Start countdown
            await StartCountdownAsync();

            // Capture photo
            _actionSource.FireEvent(ActionEvent.Command.TAKE_PICTURE, IntPtr.Zero);
        }

        private async Task StartCountdownAsync()
        {
            CountdownOverlay.Visibility = Visibility.Visible;

            for (int i = 3; i > 0; i--)
            {
                CountdownText.Text = i.ToString();
                await Task.Delay(1000);
            }

            CountdownText.Text = "SMILE!";
            await Task.Delay(500);

            CountdownOverlay.Visibility = Visibility.Collapsed;
        }

        private void HandleImageDownload(IntPtr objectRef)
        {
            try
            {
                // Download the image
                _actionSource.FireEvent(ActionEvent.Command.DOWNLOAD, objectRef);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error downloading image: " + ex.Message);
                _isCapturing = false;
                CaptureButton.IsEnabled = true;
            }
        }

        private void ShowCapturedPhoto(BitmapImage photo)
        {
            _capturedPhoto = photo;
            CapturedPhotoImage.Source = photo;
            PreviewOverlay.Visibility = Visibility.Visible;
            _isCapturing = false;
            CaptureButton.IsEnabled = true;
        }

        private void RetakeButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewOverlay.Visibility = Visibility.Collapsed;
            _capturedPhoto = null;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedPhoto == null) return;

            try
            {
                // Generate filename with timestamp
                string filename = "PhotoBooth_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                string filepath = Path.Combine(_saveDirectory, filename);

                // Save image
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 95;
                encoder.Frames.Add(BitmapFrame.Create(_capturedPhoto));

                using (FileStream fs = new FileStream(filepath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                MessageBox.Show("Photo saved to:\n" + filepath, "Photo Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                
                PreviewOverlay.Visibility = Visibility.Collapsed;
                _capturedPhoto = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving photo: " + ex.Message);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open settings window
            MessageBox.Show("Settings feature coming soon!");
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit?", "Exit PhotoBooth", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        public void Update(Observable from, CameraEvent e)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateInternal(from, e);
            }
            else
            {
                Dispatcher.Invoke(new Action(() => UpdateInternal(from, e)));
            }
        }

        private void UpdateInternal(Observable from, CameraEvent e)
        {
            CameraEvent.Type eventType = e.GetEventType();

            switch (eventType)
            {
                case CameraEvent.Type.SHUT_DOWN:
                    MessageBox.Show("Camera disconnected");
                    Close();
                    break;

                case CameraEvent.Type.PROPERTY_CHANGED:
                    uint propertyID = (uint)e.GetArg();
                    if (propertyID == EDSDKLib.EDSDK.PropID_BatteryLevel)
                    {
                        BatteryLevelText.Text = "Battery: " + _model.BatteryLebel + "%";
                    }
                    else if (propertyID == EDSDKLib.EDSDK.PropID_Evf_OutputDevice)
                    {
                        // Handle EVF output device change
                        uint device = _model.EvfOutputDevice;
                        if ((device & EDSDKLib.EDSDK.EvfOutputDevice_PC) != 0)
                        {
                            // Start downloading EVF frames
                            _actionSource.FireEvent(ActionEvent.Command.DOWNLOAD_EVF, IntPtr.Zero);
                        }
                    }
                    break;

                case CameraEvent.Type.EVFDATA_CHANGED:
                    IntPtr evfDataSetPtr = e.GetArg();
                    if (evfDataSetPtr != IntPtr.Zero)
                    {
                        EVFDataSet evfDataSet = (EVFDataSet)Marshal.PtrToStructure(evfDataSetPtr, typeof(EVFDataSet));
                        UpdateLivePreview(evfDataSet);
                    }
                    break;

                case CameraEvent.Type.OBJECT_CREATED:
                    IntPtr objectRef = e.GetArg();
                    HandleImageDownload(objectRef);
                    break;

                case CameraEvent.Type.DOWNLOAD_COMPLETE:
                    // Handle download complete - convert to BitmapImage and show
                    // This would need to be implemented based on your DownloadCommand
                    break;
            }
        }

        private void UpdateLivePreview(EVFDataSet evfDataSet)
        {
            try
            {
                IntPtr evfStream;
                UInt64 streamLength;

                EDSDKLib.EDSDK.EdsGetPointer(evfDataSet.stream, out evfStream);
                EDSDKLib.EDSDK.EdsGetLength(evfDataSet.stream, out streamLength);

                byte[] data = new byte[(int)streamLength];
                Marshal.Copy(evfStream, data, 0, (int)streamLength);

                // Convert to WPF BitmapImage
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = new MemoryStream(data);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Make thread-safe

                if (_model.isEvfEnable)
                {
                    LivePreviewImage.Source = bitmapImage;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show message box for every frame
                System.Diagnostics.Debug.WriteLine("Error updating live preview: " + ex.Message);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_actionSource != null)
                {
                    _actionSource.FireEvent(ActionEvent.Command.END_EVF, IntPtr.Zero);
                    _actionSource.FireEvent(ActionEvent.Command.CLOSING, IntPtr.Zero);
                }

                if (_model != null && _model.Camera != IntPtr.Zero)
                {
                    EDSDKLib.EDSDK.EdsRelease(_model.Camera);
                }

                EDSDKLib.EDSDK.EdsTerminateSDK();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error during cleanup: " + ex.Message);
            }

            base.OnClosing(e);
        }
    }
}

