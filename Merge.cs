#region " Imports definitions "

using System.Diagnostics;
using System.Runtime.InteropServices;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;

#endregion

namespace ImageMerger
{
    // Struct to quickly extract the 4 bytes from a 32-bit integer
    [StructLayout(LayoutKind.Explicit)]
    struct GetBytes
    {
        [FieldOffset(0)]
        public byte cyan;
        [FieldOffset(1)]
        public byte magenta;
        [FieldOffset(2)]
        public byte yellow;
        [FieldOffset(3)]
        public byte black;

        [FieldOffset(0)]
        public int pixelToConvert;
    }

    internal class Merge
    {
        /// <summary>
        /// Merge source images with merge images into target images
        /// </summary>
        /// <param name="images">List of images to process</param>
        public static List<Image> Images(List<Image> images)
        {
            // Get "source images" and "merge image" in BitmapSource format
            BitmapSource source0Image = (BitmapSource)images[0].Source;
            BitmapSource source1Image = (BitmapSource)images[1].Source;
            BitmapSource source2Image = (BitmapSource)images[2].Source;
            BitmapSource merge0Image = (BitmapSource)images[3].Source;
            BitmapSource merge1Image = (BitmapSource)images[4].Source;
            WriteableBitmap[] mergedImages;

            // Merge "source images" and "merge images" into "target images"
            mergedImages = MergeImages(source0Image, source1Image, source2Image, merge0Image, merge1Image);

            // Save "target images"
            images[6].Source = mergedImages[0];
            images[7].Source = mergedImages[1];
            images[8].Source = mergedImages[2];

            return images;
        }

        /// <summary>
        /// Merge three source images and two merge images together into three target images
        /// </summary>
        /// <param name="source0Image">First source image as BitmapSource</param>
        /// <param name="source1Image">Second source image as BitmapSource</param>
        /// <param name="source2Image">Third source image as BitmapSource</param>
        /// <param name="merge0Image">First merge image as BitmapSource</param>
        /// <param name="merge1Image">Second merge image as BitmapSource</param>
        /// <returns></returns>
        public static WriteableBitmap[] MergeImages(BitmapSource source0Image, BitmapSource source1Image, BitmapSource source2Image, BitmapSource merge0Image, BitmapSource merge1Image)
        {
            // All image widths and heights are identical
            int width = source0Image.PixelWidth;
            int height = source0Image.PixelHeight;

            // Copy "source0 image" pixels to array
            int[] source0Array = new int[width * height];
            source0Image.CopyPixels(source0Array, width * 4, 0);

            // Copy "source1 image" pixels to array
            int[] source1Array = new int[width * height];
            source1Image.CopyPixels(source1Array, width * 4, 0);

            // Copy "source2 image" pixels to array
            int[] source2Array = new int[width * height];
            source2Image.CopyPixels(source2Array, width * 4, 0);

            // Copy "merge0 image" pixels to array
            int[] merge0Array = new int[width * height];
            merge0Image.CopyPixels(merge0Array, width * 4, 0);

            // Copy "merge1 image" pixels to array
            int[] merge1Array = new int[width * height];
            merge1Image.CopyPixels(merge1Array, width * 4, 0);

            // Define rotated source arrays
            int[] source0Array270 = new int[width * height];
            int[] source1Array180 = new int[width * height];
            int[] source2Array90 = new int[width * height];

            // Copy pixels in rotated source arrays
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    source0Array270[row * width + column] = source0Array[width * (height - row - 1) + width - column - 1];
                    source1Array180[row * width + column] = source1Array[width * column + width - row - 1];
                    source2Array90[row * width + column] = source2Array[width * (height - column - 1) + row];
                }
            }

            // Target images are in WriteableBitmap format
            WriteableBitmap target0Image = new(width, height, 96, 96, PixelFormats.Cmyk32, null);
            WriteableBitmap target1Image = new(width, height, 96, 96, PixelFormats.Cmyk32, null);
            WriteableBitmap target2Image = new(width, height, 96, 96, PixelFormats.Cmyk32, null);

            try
            {
                // Reserve the back buffers for updates
                target0Image.Lock();
                target1Image.Lock();
                target2Image.Lock();

                // Get pointers to the back buffers
                IntPtr p0BackBuffer = target0Image.BackBuffer;
                IntPtr p1BackBuffer = target1Image.BackBuffer;
                IntPtr p2BackBuffer = target2Image.BackBuffer;

                // Define rotated target arrays
                int[] target1Array90 = new int[width * height];
                int[] target1Array180 = new int[width * height];
                int[] target1Array270 = new int[width * height];

                // Pixels to manipulate
                int[] targetPixels;
                int pixelS0, pixelS1, pixelS2, pixelM;
                byte pixelsCyan0, pixelsMagenta0, pixelsYellow0, pixelsBlack0, pixelsCyan1, pixelsMagenta1, pixelsYellow1, pixelsBlack1, pixelsCyan2, pixelsMagenta2, pixelsYellow2, pixelsBlack2;

                // Here is where the magic happens, pixel by pixel ;-)
                // Calculate target pixels of merge1 image
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        // Get source pixels from rotated arrays
                        pixelS0 = source0Array270[row * width + column];
                        pixelS1 = source1Array180[row * width + column];
                        pixelS2 = source2Array90[row * width + column];

                        // Get merge pixel from merge1 array
                        pixelM = merge1Array[row * width + column];

                        // Merge source pixels and merge pixel into target pixels
                        targetPixels = MergePixels(pixelS0, pixelS1, pixelS2, pixelM);

                        // Store target pixels in arrays with 90°, 180° and 270° rotations
                        target1Array270[width * column + width - row - 1] = targetPixels[0];
                        target1Array180[width * (height - row - 1) + width - column - 1] = targetPixels[1];
                        target1Array90[width * (height - column - 1) + row] = targetPixels[2];
                    }
                }

                // Calculate target pixels of merge0 image, and merge them with target pixels of merge1 image
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        unsafe
                        {
                            // Get source pixels
                            pixelS0 = source0Array[row * width + column];
                            pixelS1 = source1Array[row * width + column];
                            pixelS2 = source2Array[row * width + column];

                            // Get merge pixel from merge0 array
                            pixelM = merge0Array[row * width + column];

                            // Merge source pixels and merge pixel into target pixels
                            targetPixels = MergePixels(pixelS0, pixelS1, pixelS2, pixelM);

                            // Merge target pixels: Get pixel colors as byte structs
                            GetBytes cmyk00 = new() { pixelToConvert = targetPixels[0] };
                            GetBytes cmyk01 = new() { pixelToConvert = targetPixels[1] };
                            GetBytes cmyk02 = new() { pixelToConvert = targetPixels[2] };
                            GetBytes cmyk10 = new() { pixelToConvert = target1Array90[row * width + column] };
                            GetBytes cmyk11 = new() { pixelToConvert = target1Array180[row * width + column] };
                            GetBytes cmyk12 = new() { pixelToConvert = target1Array270[row * width + column] };

                            // Merge the pixel colors of target0 image by averaging them
                            pixelsCyan0 = (byte)((cmyk00.cyan + cmyk10.cyan) / 2);
                            pixelsMagenta0 = (byte)((cmyk00.magenta + cmyk10.magenta) / 2);
                            pixelsYellow0 = (byte)((cmyk00.yellow + cmyk10.yellow) / 2);
                            pixelsBlack0 = (byte)((cmyk00.black + cmyk10.black) / 2);

                            // Merge the pixel colors of target1 image by averaging them
                            pixelsCyan1 = (byte)((cmyk01.cyan + cmyk11.cyan) / 2);
                            pixelsMagenta1 = (byte)((cmyk01.magenta + cmyk11.magenta) / 2);
                            pixelsYellow1 = (byte)((cmyk01.yellow + cmyk11.yellow) / 2);
                            pixelsBlack1 = (byte)((cmyk01.black + cmyk11.black) / 2);

                            // Merge the pixel colors of target2 image by averaging them
                            pixelsCyan2 = (byte)((cmyk02.cyan + cmyk12.cyan) / 2);
                            pixelsMagenta2 = (byte)((cmyk02.magenta + cmyk12.magenta) / 2);
                            pixelsYellow2 = (byte)((cmyk02.yellow + cmyk12.yellow) / 2);
                            pixelsBlack2 = (byte)((cmyk02.black + cmyk12.black) / 2);

                            // Merge target pixel colors and save the pixels to target images
                            *(int*)p0BackBuffer = pixelsCyan0 | pixelsMagenta0 << 8 | pixelsYellow0 << 16 | pixelsBlack0 << 24;
                            *(int*)p1BackBuffer = pixelsCyan1 | pixelsMagenta1 << 8 | pixelsYellow1 << 16 | pixelsBlack1 << 24;
                            *(int*)p2BackBuffer = pixelsCyan2 | pixelsMagenta2 << 8 | pixelsYellow2 << 16 | pixelsBlack2 << 24;

                            // Move the back buffer pointers 1 pixel = 4 bytes further
                            p0BackBuffer += 4;
                            p1BackBuffer += 4;
                            p2BackBuffer += 4;
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Debug.Write(err.ToString());
            }
            finally
            {
                // Specify the area of the bitmaps that changed, in our case the entire bitmap
                target0Image.AddDirtyRect(new Int32Rect(0, 0, width, height));
                target1Image.AddDirtyRect(new Int32Rect(0, 0, width, height));
                target2Image.AddDirtyRect(new Int32Rect(0, 0, width, height));

                // Release the back buffers and make it available for display
                target0Image.Unlock();
                target1Image.Unlock();
                target2Image.Unlock();
            }

            WriteableBitmap[] targetImages = [target0Image, target1Image, target2Image];
            return targetImages;
        }

        /// <summary>
        /// Merge three source pixels and one merge pixel into three target pixels
        /// </summary>
        /// <param name="pixelS0">First source pixel</param>
        /// <param name="pixelS1">Second source pixel</param>
        /// <param name="pixelS2">Third source pixel</param>
        /// <param name="pixelM0">Merge pixel</param>
        /// <returns></returns>
        public static int[] MergePixels(int pixelS0, int pixelS1, int pixelS2, int pixelM0)
        {
            // Pixels to manipulate
            int[] pixels = new int[3];
            byte[] pixelsCyan, pixelsMagenta, pixelsYellow, pixelsBlack;

            // Get pixel colors as byte structs
            GetBytes cmykS0 = new() { pixelToConvert = pixelS0 };
            GetBytes cmykS1 = new() { pixelToConvert = pixelS1 };
            GetBytes cmykS2 = new() { pixelToConvert = pixelS2 };
            GetBytes cmykM0 = new() { pixelToConvert = pixelM0 };

            // Adjust the CYAN target pixel colors
            pixelsCyan = AdjustPixels(cmykS0.cyan, cmykS1.cyan, cmykS2.cyan, cmykM0.cyan);
            // Adjust the MAGENTA target pixel colors
            pixelsMagenta = AdjustPixels(cmykS0.magenta, cmykS1.magenta, cmykS2.magenta, cmykM0.magenta);
            // Adjust the YELLOW target pixel colors
            pixelsYellow = AdjustPixels(cmykS0.yellow, cmykS1.yellow, cmykS2.yellow, cmykM0.yellow);
            // Adjust the BLACK target pixel colors
            pixelsBlack = AdjustPixels(cmykS0.black, cmykS1.black, cmykS2.black, cmykM0.black);

            // Merge target pixel colors
            pixels[0] = pixelsCyan[0] | pixelsMagenta[0] << 8 | pixelsYellow[0] << 16 | pixelsBlack[0] << 24;
            pixels[1] = pixelsCyan[1] | pixelsMagenta[1] << 8 | pixelsYellow[1] << 16 | pixelsBlack[1] << 24;
            pixels[2] = pixelsCyan[2] | pixelsMagenta[2] << 8 | pixelsYellow[2] << 16 | pixelsBlack[2] << 24;

            return pixels;
        }

        /// <summary>
        /// Adjust three "source pixel" colors to match one "merge pixel" color
        /// </summary>
        /// <param name="cmykS0">First source pixel color</param>
        /// <param name="cmykS1">Second source pixel color</param>
        /// <param name="cmykS2">Third source pixel color</param>
        /// <param name="cmyk">Merge pixel color</param>
        /// <returns></returns>
        public static byte[] AdjustPixels(byte cmykS0, byte cmykS1, byte cmykS2, byte cmyk)
        {
            float c;
            byte[] pixels;

            if (cmyk != 0)
            {
                // Increase contrast
                cmyk = (byte)Math.Min(cmyk * 1.5, 255);

                if (cmykS0 == 0) cmykS0 = 1;
                if (cmykS1 == 0) cmykS1 = 1;
                if (cmykS2 == 0) cmykS2 = 1;

                if (cmyk >= (cmykS0 + cmykS1 + cmykS2))
                    // Source pixel colors should be DARKER or EQUAL compared to merge pixel color
                    c = (float)(cmykS0 + cmykS1 + cmykS2) / cmyk;
                else
                    // Source pixel colors should be LIGHTER than merge pixel color
                    c = (float)cmyk / (cmykS0 + cmykS1 + cmykS2);

                // Adjust source pixel colors
                cmykS0 = (byte)Math.Min(cmykS0 * c, 255);
                cmykS1 = (byte)Math.Min(cmykS1 * c, 255);
                cmykS2 = (byte)Math.Min(cmykS2 * c, 255);

                // Correct source pixel colors if necessary
                if (cmykS0 + cmykS1 + cmykS2 < cmyk)
                {
                    while (cmykS0 + cmykS1 + cmykS2 < cmyk)
                    {
                        cmykS0 = (byte)Math.Min(cmykS0 + 1, 255);
                        cmykS1 = (byte)Math.Min(cmykS1 + 1, 255);
                        cmykS2 = (byte)Math.Min(cmykS2 + 1, 255);
                    }
                }
                else if (cmykS0 + cmykS1 + cmykS2 > cmyk)
                {
                    while (cmykS0 + cmykS1 + cmykS2 > cmyk)
                    {
                        cmykS0 = (byte)Math.Max(cmykS0 - 1, 0);
                        cmykS1 = (byte)Math.Max(cmykS1 - 1, 0);
                        cmykS2 = (byte)Math.Max(cmykS2 - 1, 0);
                    }
                }

                pixels = [cmykS0, cmykS1, cmykS2];
            }
            else
                pixels = [0, 0, 0];

            return pixels;
        }
    }
}
