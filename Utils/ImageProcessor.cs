using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;

namespace PhotoBooth.Utils
{
    public static class ImageProcessor
    {
        // Thread-safe byte array pool for better memory management
        private static readonly object _poolLock = new object();
        private static byte[]? _cachedPixelArray = null;

        public static BitmapImage? ApplyFilters(BitmapSource? source, double brightness, bool grayscale)
        {
            if (source == null) return null;

            try
            {
                // Convert to WriteableBitmap - FormatConvertedBitmap handles color channel conversion automatically
                WriteableBitmap bitmap;
                if (source is WriteableBitmap wb && wb.Format == PixelFormats.Bgra32)
                {
                    bitmap = new WriteableBitmap(wb);
                }
                else
                {
                    // Use FormatConvertedBitmap to ensure Bgra32 format
                    var formatConverted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                    
                    // Create WriteableBitmap from converted source
                    bitmap = new WriteableBitmap(
                        formatConverted.PixelWidth,
                        formatConverted.PixelHeight,
                        96, 96, // DPI
                        PixelFormats.Bgra32,
                        null);
                    
                    // Copy pixels from converted source
                    int sourceStride = formatConverted.PixelWidth * 4;
                    byte[] sourcePixels = GetOrCreatePixelArray(sourceStride * formatConverted.PixelHeight);
                    formatConverted.CopyPixels(sourcePixels, sourceStride, 0);
                    bitmap.WritePixels(new Int32Rect(0, 0, formatConverted.PixelWidth, formatConverted.PixelHeight), 
                        sourcePixels, sourceStride, 0);
                }
                
                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                int stride = width * 4;
                byte[] pixels = GetOrCreatePixelArray(stride * height);
                bitmap.CopyPixels(pixels, stride, 0);

                // Apply filters
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];
                    
                    if (grayscale)
                    {
                        byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                        pixels[i] = gray;
                        pixels[i + 1] = gray;
                        pixels[i + 2] = gray;
                        pixels[i + 3] = a;
                    }
                    else
                    {
                        pixels[i] = Clamp((int)(b * brightness));
                        pixels[i + 1] = Clamp((int)(g * brightness));
                        pixels[i + 2] = Clamp((int)(r * brightness));
                        pixels[i + 3] = a;
                    }
                }

                bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                bitmap.Freeze(); // Allow cross-thread access and GC optimization
                
                return ConvertToBitmapImage(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] ApplyFilters error: {ex.Message}");
                return null;
            }
        }

        private static byte Clamp(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private static byte[] GetOrCreatePixelArray(int size)
        {
            lock (_poolLock)
            {
                if (_cachedPixelArray == null || _cachedPixelArray.Length < size)
                {
                    _cachedPixelArray = new byte[size];
                }
                return _cachedPixelArray;
            }
        }

        private static BitmapImage ConvertToBitmapImage(WriteableBitmap bitmap, int qualityLevel = 95)
        {
            using var stream = new MemoryStream();
            var encoder = new JpegBitmapEncoder { QualityLevel = qualityLevel }; // High quality for captured images
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze(); // Critical for GC and thread safety
            
            return image;
        }

        /// <summary>
        /// Crops and scales an image to match the target aspect ratio
        /// </summary>
        public static BitmapImage? CropAndScaleToAspectRatio(BitmapSource? source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;

            try
            {
                int sourceWidth = source.PixelWidth;
                int sourceHeight = source.PixelHeight;
                
                // Calculate aspect ratios
                double sourceAspect = (double)sourceWidth / sourceHeight;
                double targetAspect = (double)targetWidth / targetHeight;

                int cropWidth, cropHeight, cropX, cropY;

                // Determine how to crop
                if (sourceAspect > targetAspect)
                {
                    cropHeight = sourceHeight;
                    cropWidth = (int)(sourceHeight * targetAspect);
                    cropX = (sourceWidth - cropWidth) / 2;
                    cropY = 0;
                }
                else
                {
                    cropWidth = sourceWidth;
                    cropHeight = (int)(sourceWidth / targetAspect);
                    cropX = 0;
                    cropY = (sourceHeight - cropHeight) / 2;
                }

                // Create cropped bitmap
                var croppedBitmap = new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropWidth, cropHeight));

                // Scale to target dimensions
                var scaledBitmap = new TransformedBitmap(croppedBitmap, new ScaleTransform(
                    (double)targetWidth / cropWidth,
                    (double)targetHeight / cropHeight));

                // Convert to Bgra32 format
                var formatConverted = new FormatConvertedBitmap(scaledBitmap, PixelFormats.Bgra32, null, 0);
                
                // Create WriteableBitmap
                var writeableBitmap = new WriteableBitmap(formatConverted);
                writeableBitmap.Freeze();

                return ConvertToBitmapImage(writeableBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] CropAndScaleToAspectRatio error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears the cached pixel array to free memory
        /// </summary>
        public static void ClearCache()
        {
            lock (_poolLock)
            {
                _cachedPixelArray = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            }
        }

        /// <summary>
        /// Composites a frame template over a base image
        /// </summary>
        public static BitmapImage? CompositeFrameOverImage(BitmapSource baseImage, BitmapSource frameOverlay)
        {
            try
            {
                int width = baseImage.PixelWidth;
                int height = baseImage.PixelHeight;

                // Create a DrawingVisual to composite the images
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext context = visual.RenderOpen())
                {
                    // Draw base image first
                    context.DrawImage(baseImage, new Rect(0, 0, width, height));
                    
                    // Draw frame overlay on top
                    context.DrawImage(frameOverlay, new Rect(0, 0, width, height));
                }

                // Render to a RenderTargetBitmap
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);

                // Convert to BitmapImage
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;

                    BitmapImage result = new BitmapImage();
                    result.BeginInit();
                    result.CacheOption = BitmapCacheOption.OnLoad;
                    result.StreamSource = stream;
                    result.EndInit();
                    result.Freeze();

                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor] Error compositing frame: {ex.Message}");
                return null;
            }
        }
    }
}
