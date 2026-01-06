using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoBooth.Models;

namespace PhotoBooth.Utils
{
    public static class FrameGenerator
    {
        public static string GenerateFrame(List<BitmapImage> images, string gridType, string outputPath)
        {
            var frameData = FrameDataProvider.GetFrameData().Find(f => f.Grid == gridType);
            if (frameData == null)
            {
                throw new Exception($"Frame data not found for grid type: {gridType}");
            }

            // Special handling for grid8: 4 photos get duplicated to fill 8 positions
            if (gridType == "grid8")
            {
                if (images.Count != 4)
                {
                    throw new Exception($"Grid8 requires 4 images (will be duplicated to 8 positions), but got {images.Count}");
                }
            }
            else if (images.Count != frameData.Coordinates.Count)
            {
                throw new Exception($"Image count ({images.Count}) doesn't match frame coordinates ({frameData.Coordinates.Count})");
            }

            // Create frame bitmap
            var frameBitmap = new WriteableBitmap(
                frameData.FrameW,
                frameData.FrameH,
                96, 96,
                PixelFormats.Bgr32,
                null);

            // Fill with white background
            byte[] framePixels = new byte[frameData.FrameW * frameData.FrameH * 4];
            for (int i = 0; i < framePixels.Length; i += 4)
            {
                framePixels[i] = 255;     // B
                framePixels[i + 1] = 255; // G
                framePixels[i + 2] = 255; // R
                framePixels[i + 3] = 255; // A
            }
            frameBitmap.WritePixels(new System.Windows.Int32Rect(0, 0, frameData.FrameW, frameData.FrameH), 
                framePixels, frameData.FrameW * 4, 0);

            // Place images on frame
            if (gridType == "grid8")
            {
                // Special handling for grid8: duplicate 4 photos to fill 8 positions
                // Left column positions: 0, 1, 2, 3 (X = 50)
                // Right column positions: 4, 5, 6, 7 (X = 660)
                // Each photo from images[0-3] goes to both left and right at the same Y position
                for (int photoIndex = 0; photoIndex < 4; photoIndex++)
                {
                    var image = images[photoIndex];
                    
                    // Resize image to placeholder size
                    var resized = ResizeImage(image, frameData.PlaceholderW, frameData.PlaceholderH);
                    var imageBitmap = new WriteableBitmap(resized);
                    int imageStride = imageBitmap.PixelWidth * 4;
                    byte[] imagePixels = new byte[imageStride * imageBitmap.PixelHeight];
                    imageBitmap.CopyPixels(imagePixels, imageStride, 0);

                    // Place in left column (position photoIndex)
                    var leftCoord = frameData.Coordinates[photoIndex];
                    for (int y = 0; y < imageBitmap.PixelHeight && leftCoord.Y + y < frameData.FrameH; y++)
                    {
                        for (int x = 0; x < imageBitmap.PixelWidth && leftCoord.X + x < frameData.FrameW; x++)
                        {
                            int srcIndex = y * imageStride + x * 4;
                            int dstIndex = (leftCoord.Y + y) * frameData.FrameW * 4 + (leftCoord.X + x) * 4;

                            if (dstIndex + 3 < framePixels.Length && srcIndex + 3 < imagePixels.Length)
                            {
                                framePixels[dstIndex] = imagePixels[srcIndex];
                                framePixels[dstIndex + 1] = imagePixels[srcIndex + 1];
                                framePixels[dstIndex + 2] = imagePixels[srcIndex + 2];
                                framePixels[dstIndex + 3] = imagePixels[srcIndex + 3];
                            }
                        }
                    }

                    // Place same image in right column (position photoIndex + 4)
                    var rightCoord = frameData.Coordinates[photoIndex + 4];
                    for (int y = 0; y < imageBitmap.PixelHeight && rightCoord.Y + y < frameData.FrameH; y++)
                    {
                        for (int x = 0; x < imageBitmap.PixelWidth && rightCoord.X + x < frameData.FrameW; x++)
                        {
                            int srcIndex = y * imageStride + x * 4;
                            int dstIndex = (rightCoord.Y + y) * frameData.FrameW * 4 + (rightCoord.X + x) * 4;

                            if (dstIndex + 3 < framePixels.Length && srcIndex + 3 < imagePixels.Length)
                            {
                                framePixels[dstIndex] = imagePixels[srcIndex];
                                framePixels[dstIndex + 1] = imagePixels[srcIndex + 1];
                                framePixels[dstIndex + 2] = imagePixels[srcIndex + 2];
                                framePixels[dstIndex + 3] = imagePixels[srcIndex + 3];
                            }
                        }
                    }
                }
            }
            else
            {
                // Normal handling for other grids
                for (int i = 0; i < images.Count && i < frameData.Coordinates.Count; i++)
                {
                    var coord = frameData.Coordinates[i];
                    var image = images[i];

                    // Resize image to placeholder size
                    var resized = ResizeImage(image, frameData.PlaceholderW, frameData.PlaceholderH);

                    // Convert to WriteableBitmap
                    var imageBitmap = new WriteableBitmap(resized);
                    // Calculate stride: width * bytes per pixel (Bgr32 = 4 bytes)
                    int imageStride = imageBitmap.PixelWidth * 4;
                    byte[] imagePixels = new byte[imageStride * imageBitmap.PixelHeight];
                    imageBitmap.CopyPixels(imagePixels, imageStride, 0);

                    // Copy image pixels to frame at coordinates
                    for (int y = 0; y < imageBitmap.PixelHeight && coord.Y + y < frameData.FrameH; y++)
                    {
                        for (int x = 0; x < imageBitmap.PixelWidth && coord.X + x < frameData.FrameW; x++)
                        {
                            int srcIndex = y * imageStride + x * 4;
                            int dstIndex = (coord.Y + y) * frameData.FrameW * 4 + (coord.X + x) * 4;

                            if (dstIndex + 3 < framePixels.Length && srcIndex + 3 < imagePixels.Length)
                            {
                                framePixels[dstIndex] = imagePixels[srcIndex];         // B
                                framePixels[dstIndex + 1] = imagePixels[srcIndex + 1]; // G
                                framePixels[dstIndex + 2] = imagePixels[srcIndex + 2]; // R
                                framePixels[dstIndex + 3] = imagePixels[srcIndex + 3]; // A
                            }
                        }
                    }
                }
            }

            // Write final frame
            frameBitmap.WritePixels(new System.Windows.Int32Rect(0, 0, frameData.FrameW, frameData.FrameH),
                framePixels, frameData.FrameW * 4, 0);

            // Save to file with maximum quality
            var encoder = new JpegBitmapEncoder { QualityLevel = 100 }; // Maximum quality for final frames
            encoder.Frames.Add(BitmapFrame.Create(frameBitmap));

            using var fileStream = new FileStream(outputPath, FileMode.Create);
            encoder.Save(fileStream);

            return outputPath;
        }

        private static BitmapImage ResizeImage(BitmapImage source, int width, int height)
        {
            var resized = new TransformedBitmap(source, new ScaleTransform(
                width / (double)source.PixelWidth,
                height / (double)source.PixelHeight));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(resized));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }
}

