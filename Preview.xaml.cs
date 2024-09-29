#region " Imports definitions "

using System.Diagnostics;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

# endregion

namespace ImageMerger
{
    /// <summary>
    /// Display all merged images in an animation
    /// </summary>
    public partial class Preview : Window
    {
        #region " Variables definitions "

        int sceneNumber = 0;                // Counter for switching from one images view to the next
        bool lastScene = false;             // = true if last scene was reached

        #endregion

        public Preview()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Display stacked images in an animation
        /// </summary>
        /// <param name="images"></param>
        public static void Images(List<Image> images)
        {
            Preview dp = new();

            // Reduce the brightness of the images, because their opacity is only 33%
            dp.Image00.Source = DarkenImage((BitmapSource)images[0].Source);
            dp.Image01.Source = DarkenImage((BitmapSource)images[1].Source);
            dp.Image02.Source = DarkenImage((BitmapSource)images[2].Source);

            dp.Show();
        }

        /// <summary>
        /// Reduce the brightness of an image
        /// </summary>
        /// <param name="image">Image to darken</param>
        /// <returns>Darkened image</returns>
        private static WriteableBitmap DarkenImage(BitmapSource image)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            WriteableBitmap darkImage = new(width, height, 96, 96, PixelFormats.Cmyk32, null);
            int pixel;
            byte pixelCyan, pixelMagenta, pixelYellow, pixelBlack;

            try
            {
                // Copy image pixels to array
                int[] darkImageArray = new int[width * height];
                image.CopyPixels(darkImageArray, width * 4, 0);

                // Reserve the back buffers for updates
                darkImage.Lock();

                // Get pointer to the back buffer
                IntPtr pBackBuffer = darkImage.BackBuffer;

                // Darken each pixel
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        unsafe
                        {
                            pixel = darkImageArray[row * width + column];
                            pixelCyan = (byte)(pixel & 0xFF);
                            pixelMagenta = (byte)(pixel >> 8 & 0xFF);
                            pixelYellow = (byte)(pixel >> 16 & 0xFF);
                            pixelBlack = (byte)(pixel >> 24);
                            // Darken pixel
                            if (pixelCyan > 85) pixelCyan = 255; else pixelCyan *= 3;
                            if (pixelMagenta > 85) pixelMagenta = 255; else pixelMagenta *= 3;
                            if (pixelYellow > 85) pixelYellow = 255; else pixelYellow *= 3;
                            if (pixelBlack > 85) pixelBlack = 255; else pixelBlack *= 3;
                            pixel = pixelCyan | pixelMagenta << 8 | pixelYellow << 16 | pixelBlack << 24;

                            // Save  pixel to image
                            *(int*)pBackBuffer = pixel;

                            // Move the back buffer pointers 1 pixel = 4 bytes further
                            pBackBuffer += 4;
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
                // Specify the area of the bitmap that changed, in our case the entire bitmap area
                darkImage.AddDirtyRect(new Int32Rect(0, 0, width, height));

                // Release the back buffer and make it available for display
                darkImage.Unlock();
            }

            return darkImage;
        }

        /// <summary>
        /// Change image orientations
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        public void Continue_Click(object sender, RoutedEventArgs e)
        {
            // Get origin X-coordinates of the images
            double OffsetImage00 = VisualTreeHelper.GetOffset(Image00).X;
            double OffsetImage01 = VisualTreeHelper.GetOffset(Image01).X;
            double OffsetImage02 = VisualTreeHelper.GetOffset(Image02).X;

            switch (sceneNumber)
            {
                case 0:
                    if (lastScene == true)
                    {
                        // Rotate Image00 finally by -90°
                        Image00.Source = RotateImageFinally(Image00, -90);

                        // Rotate Image02 finally by 90°
                        Image02.Source = RotateImageFinally(Image02, 90);
                    }

                    // Disable 'Continue'-button for 2 seconds = duration of this scene
                    DisableButton(2);

                    // Move Image00 over Image01
                    TranslateImage(Image00, 0, OffsetImage01, null);

                    // Move Image02 over Image01
                    TranslateImage(Image02, 0, -OffsetImage01, null);

                    sceneNumber += 1;
                    break;

                case 1:
                    if (lastScene == true)
                    {
                        // Rotate Image01 finally by -180°
                        Image01.Source = RotateImageFinally(Image01, -180);

                        lastScene = false;
                    }

                    // Disable 'Continue'-button for 5 seconds = duration of this scene
                    DisableButton(5);

                    // Move Image00 back to its original position
                    TranslateImage(Image00, OffsetImage01, -OffsetImage00, Image00_Finished01);

                    // Rotate Image01 temporarily by 180°
                    RotateImage(Image01, 180, 1, 2, null);

                    // Move Image02 back to its original position
                    TranslateImage(Image02, -OffsetImage01, 0, Image02_Finished01);

                    sceneNumber += 1;
                    break;

                case 2:
                    // Disable 'Continue'-button for 3 seconds = duration of this scene
                    DisableButton(3);

                    // Move Image00 back to its original position
                    TranslateImage(Image00, OffsetImage01, 0, Image00_Finished03);

                    // Rotate Image01 finally by 180°
                    Image01.Source = RotateImageFinally(Image01, 180);

                    // Rotate Image01 temporarily by -180°
                    RotateImage(Image01, -180, 1, 2, null);

                    // Move Image02 back to its original position
                    TranslateImage(Image02, -OffsetImage01, 0, Image02_Finished03);

                    lastScene = true;
                    sceneNumber = 0;
                    break;
            }
        }

        /// <summary>
        /// Image00 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image00_Finished01(object? sender, EventArgs e)
        {
            // Rotate Image00 temporarily by 90°
            RotateImage(Image00, 90, 1, 0, Image00_Finished02);
        }

        /// <summary>
        /// Image00 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image00_Finished02(object? sender, EventArgs e)
        {
            // Rotate Image00 finally by 90°
            Image00.Source = RotateImageFinally(Image00, 90);

            // Move Image00 over Image01
            Vector offset1 = VisualTreeHelper.GetOffset(Image00);
            Vector offset2 = VisualTreeHelper.GetOffset(Image01);
            TranslateImage(Image00, 0, offset2.X - offset1.X, null);
        }

        /// <summary>
        /// Image00 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image00_Finished03(object? sender, EventArgs e)
        {
            // Rotate Image00 temporarily by -90°
            RotateImage(Image00, -90, 1, 0, null);
        }

        /// <summary>
        /// Flag that Image02 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image02_Finished01(object? sender, EventArgs e)
        {
            // Rotate Image02 temporarily by -90°
            RotateImage(Image02, -90, 1, 0, Image02_Finished02);
        }

        /// <summary>
        /// Image02 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image02_Finished02(object? sender, EventArgs e)
        {
            // Rotate Image02 finally by -90°
            Image02.Source = RotateImageFinally(Image02, -90);

            // Move Image02 over Image01
            Vector offset1 = VisualTreeHelper.GetOffset(Image02);
            Vector offset2 = VisualTreeHelper.GetOffset(Image01);
            TranslateImage(Image02, 0, offset2.X - offset1.X, null);
        }

        /// <summary>
        /// Image02 animation has finished
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        private void Image02_Finished03(object? sender, EventArgs e)
        {
            // Rotate Image02 temporarily by 90°
            RotateImage(Image02, 90, 1, 0, null);
        }

        /// <summary>
        /// Move image horizontally
        /// </summary>
        /// <param name="image">Image to move</param>
        /// <param name="fromX">Source X-coordinate</param>
        /// <param name="toX">Target X-coordinate</param>
        private static void TranslateImage(Image image, double fromX, double toX, EventHandler? eventHandler)
        {
            TranslateTransform translate = new();
            image.RenderTransform = translate;
            DoubleAnimation animateImage = new(fromX, toX, TimeSpan.FromSeconds(2))
            {
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut }
            };
            if (eventHandler != null)
                animateImage.Completed += new EventHandler(eventHandler);
            translate.BeginAnimation(TranslateTransform.XProperty, animateImage);
        }

        /// <summary>
        /// Rotate image temporarily
        /// </summary>
        /// <param name="image">Image to rotate</param>
        /// <param name="angle">Rotation angle in degrees</param>
        /// <param name="duration">Animation duration in seconds</param>
        /// <param name="beginTime">Animation start delay in seconds</param>
        private static void RotateImage(Image image, int angle, int duration, int beginTime, EventHandler? eventHandler)
        {
            RotateTransform rotate = new();
            image.RenderTransform = rotate;
            DoubleAnimation animateImage = new(0, angle, TimeSpan.FromSeconds(duration))
            {
                BeginTime = TimeSpan.FromSeconds(beginTime),
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut }
            };
            if (eventHandler != null)
                animateImage.Completed += new EventHandler(eventHandler);
            rotate.BeginAnimation(RotateTransform.AngleProperty, animateImage);
        }

        /// <summary>
        ///  Rotate image finally
        /// </summary>
        /// <param name="image">Image to rotate</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns></returns>
        private static WriteableBitmap RotateImageFinally(Image image, int angle)
        {
            WriteableBitmap source = (WriteableBitmap)image.Source;
            TransformedBitmap bitmap = new();
            bitmap.BeginInit();
            bitmap.Source = source;     // Chain the objects together
            bitmap.Transform = new RotateTransform(angle);
            bitmap.EndInit();
            WriteableBitmap writableBitmap = new(bitmap);
            return writableBitmap;
        }

        /// <summary>
        /// Temporarily disable the 'Continue'-button
        /// </summary>
        /// <param name="seconds">Duration of the timer</param>
        private void DisableButton(double seconds)
        {
            ContinueButton.IsEnabled = false;

            DispatcherTimer timer = new()
            {
                Interval = TimeSpan.FromSeconds(seconds)
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                ContinueButton.IsEnabled = true;
            };

            timer.Start();
        }
    }
}