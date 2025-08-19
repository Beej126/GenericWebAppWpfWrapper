using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GenericWebAppWpfWrapper
{
    public static class BitmapUtils
    {

        /// <summary>
        /// Crops transparent pixels from around a bitmap image
        /// </summary>
        /// <param name="source">The source BitmapImage</param>
        /// <returns>A BitmapSource with transparent pixels cropped</returns>
        public static BitmapSource CropTransparentPixels(this BitmapSource source)
        {
            try
            {
                // Convert BitmapImage to WriteableBitmap for pixel access
                var writeableBitmap = new WriteableBitmap(source);

                int width = writeableBitmap.PixelWidth;
                int height = writeableBitmap.PixelHeight;

                // Get pixel data
                int stride = width * 4; // 4 bytes per pixel (BGRA)
                byte[] pixels = new byte[height * stride];
                writeableBitmap.CopyPixels(pixels, stride, 0);

                // Find the bounds of non-transparent pixels
                int minX = width;
                int minY = height;
                int maxX = 0;
                int maxY = 0;

                bool foundNonTransparentPixel = false;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * stride + x * 4;
                        byte alpha = pixels[index + 3]; // Alpha is at offset 3

                        if (alpha > 0) // Non-transparent pixel
                        {
                            foundNonTransparentPixel = true;
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                // If no non-transparent pixels were found, return the original image
                if (!foundNonTransparentPixel)
                    return source;

                // Ensure we have valid bounds with at least 1 pixel in each dimension
                minX = Math.Max(0, minX);
                minY = Math.Max(0, minY);
                maxX = Math.Min(width - 1, maxX);
                maxY = Math.Min(height - 1, maxY);

                // Calculate the dimensions of the cropped image
                int cropWidth = maxX - minX + 1;
                int cropHeight = maxY - minY + 1;

                // Ensure minimum size (at least 1x1 pixel)
                if (cropWidth < 1 || cropHeight < 1)
                    return source;

                var maxDimension = Math.Max(cropWidth, cropHeight);

                // Create a cropped version of the bitmap
                var croppedBitmap = new CroppedBitmap(writeableBitmap, new Int32Rect(minX, minY, maxDimension, maxDimension));
                return croppedBitmap;
            }
            catch
            {
                // On any error, return the original image
                return source;
            }
        }
    }
}