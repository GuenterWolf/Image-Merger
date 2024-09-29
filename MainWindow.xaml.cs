#region 3rd party software

// http://artof01.com/vrellis/works/AllAndOne.html https://www.youtube.com/watch?v=LnXhjzS_i48 Artwork of Petros Vrellis
// https://docs.pdfsharp.net https://github.com/empira/PDFsharp .NET library for processing PDF files

#endregion

#region " Imports definitions "

using Microsoft.Win32;

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

#endregion

namespace ImageMerger
{
    /// <summary>
    /// Routines for hiding one or two images within three slides
    /// </summary>
    public partial class MainWindow : Window
    {
        #region " Variables definitions "

        List<Image> images = [];        // List of all images to process
        int maxWidth = 0;               // Width of the largest image in pixels
        int maxHeight = 0;              // Height of the largest image in pixels
        Point point;                    // Point used to position the SpinningWheel control
        CancellationToken token;        // Token used to cancel the SpinningWheel thread

        #endregion

        /// <summary>
        /// Program start routine
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Add all images to list
            images = [Image00, Image01, Image02, Image03, Image04, Image05, Image06, Image07, Image08];

            // Store source paths needed for possible images reset
            foreach (Image image in images)
                image.Tag = ((BitmapFrame)image.Source).Decoder.ToString();
        }

        /// <summary>
        /// Merge images together
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        public void MergeImages(object sender, RoutedEventArgs e)
        {
            // Show spinning wheel in a new thread
            point = Image04.PointToScreen(new Point(0, 0));
            CancellationTokenSource cts = new();
            token = cts.Token;
            Thread popupThread = new(new ThreadStart(ShowSpinningWheel));
            popupThread.SetApartmentState(ApartmentState.STA);
            popupThread.IsBackground = false;
            popupThread.Start();
            Mouse.OverrideCursor = Cursors.Wait;

            // Scale all images to the largest width and height found, and convert them to CMYK32 format
            FormatImages();

            // Merge source images with merge images into target images
            images = Merge.Images(images);

            // Enable "Show Images" and "Save Images" buttons
            ShowImagesButton.IsEnabled = true;
            SaveImagesButton.IsEnabled = true;

            // Cancel spinning wheel thread
            cts.Cancel();
            popupThread.Join();
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        /// <summary>
        /// Display the spinning wheel in a popup
        /// </summary>
        public void ShowSpinningWheel()
        {
            try
            {
                // Display popup containing spinning wheel
                Popup popup = new()
                {
                    Child = new SpinningWheel(),
                    AllowsTransparency = true,
                    Placement = PlacementMode.Absolute,
                    HorizontalOffset = point.X * 0.68,  // HACK: PointToScreen X-value is too high
                    VerticalOffset = point.Y * 0.72,    // HACK: PointToScreen Y-value is too high
                    IsOpen = true
                };

                while (true)
                {
                    popup.Refresh();

                    if (token.IsCancellationRequested)
                    {
                        // Request for cancellation received, cancel thread
                        popup.IsOpen = false;
                        throw new TaskCanceledException();
                    }
                }
            }
            catch (OperationCanceledException err)
            {
                Debug.Write(err.ToString());
            }
        }

        /// <summary>
        /// Scale all images to the largest width and height found, and convert it to CMYK32 format
        /// </summary>
        public void FormatImages()
        {
            BitmapSource bitmap;

            // Find the largest width and height
            foreach (Image image in images)
            {
                bitmap = (BitmapSource)image.Source;
                if (bitmap.PixelWidth > maxWidth)
                    maxWidth = bitmap.PixelWidth;
                if (bitmap.PixelHeight > maxHeight)
                    maxHeight = bitmap.PixelHeight;
            }

            // Scale all images to the largest width and height found, and convert it to CMYK32 format
            foreach (Image image in images)
            {
                // Scale image
                bitmap = (BitmapSource)image.Source;
                bitmap = new TransformedBitmap(bitmap,
                    new ScaleTransform(
                        (double)maxWidth / bitmap.PixelWidth,
                        (double)maxHeight / bitmap.PixelHeight));
                image.Source = bitmap;

                // Convert image to CMYK32 format
                FormatConvertedBitmap newFormat = new();
                newFormat.BeginInit();
                newFormat.Source = (BitmapSource)image.Source;      // Chain the objects together                
                newFormat.DestinationFormat = PixelFormats.Cmyk32;  // Set the new format to CMYK32
                newFormat.EndInit();
                image.Source = newFormat;
            }
        }

        /// <summary>
        /// Display all merged images in an animation
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        public void ShowImages(object sender, RoutedEventArgs e)
        {
            List<Image> imageList = [Image06, Image07, Image08];
            Preview.Images(imageList);
        }

        /// <summary>
        /// Save all merged images as PDF file
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        public void SaveImages(object sender, RoutedEventArgs e)
        {
            List<Image> imageList = [Image06, Image07, Image08];
            PDF.Save(imageList);
        }

        /// <summary>
        /// Reset all images to their original contents
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ResetImages(object sender, RoutedEventArgs e)
        {
            foreach (Image image in images)
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri((string)image.Tag);      // Uri strings have been saved during program start
                bitmap.EndInit();
                image.Source = bitmap;

                // Disable "Show Images" and "Save Images" buttons
                ShowImagesButton.IsEnabled = false;
                SaveImagesButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Load a new image and display it in control
        /// </summary>
        /// <param name="sender">Interrupt <c>sender</c> object</param>
        /// <param name="e">Interrupt event arguments</param>
        public void Image_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Image image)
            {
                try
                {
                    // Show open file dialog
                    OpenFileDialog openFileDialog = new()
                    {
                        Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.tiff)|*.png;*.jpg;*.jpeg;*.gif;*.tiff|All files (*.*)|*.*",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        // Load image
                        BitmapImage bitmap = new();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(openFileDialog.FileName);
                        bitmap.EndInit();

                        // Ensure image is square
                        if (bitmap.PixelWidth > bitmap.PixelHeight)
                            image.Source = new CroppedBitmap(bitmap, new Int32Rect((bitmap.PixelWidth - bitmap.PixelHeight) / 2, 0, bitmap.PixelHeight, bitmap.PixelHeight));
                        else if (bitmap.PixelHeight > bitmap.PixelWidth)
                            image.Source = new CroppedBitmap(bitmap, new Int32Rect(0, (bitmap.PixelHeight - bitmap.PixelWidth) / 2, bitmap.PixelWidth, bitmap.PixelWidth));
                        else
                            image.Source = bitmap;

                        // Disable "Show Images" and "Save Images" buttons
                        ShowImagesButton.IsEnabled = false;
                        SaveImagesButton.IsEnabled = false;
                    }
                    else
                    {
                        // File dialog was cancelled, load blank image
                        //image.Source = Image05.Source;
                    }
                }
                catch (Exception err)
                {
                    Debug.Write(err.ToString());
                }
            }
        }
    }

    /// <summary>
    /// Extension method used to refresh the SpinningWheel, which keeps it spinning
    /// </summary>
    public static class ExtensionMethods
    {
        private static readonly Action EmptyDelegate = delegate { };
        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
    }
}