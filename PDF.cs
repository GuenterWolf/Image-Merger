#region " Imports definitions "

using Microsoft.Win32;

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;

using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Quality;
using System.Windows.Shapes;

#endregion

namespace ImageMerger
{
    internal class PDF
    {
        /// <summary>
        /// Save all merged images as PDF file
        /// </summary>
        public static void Save(List<Image> images)
        {
            try
            {
                // Create a new PDF document
                PdfDocument document = new();
                document.Info.Title = "Image Merger";
                document.Info.Subject = "Print all images in this document on transparencies and stack them to reveal the hidden images";
                document.Options.ColorMode = PdfColorMode.Cmyk;
                document.PageLayout = PdfPageLayout.SinglePage;     // Zoom pages to view as entire pages in PDF viewer
                int pageNumber = 0;

                // Render all images into PDF document
                foreach (Image image in images)
                {
                    // Create an empty, square page in this document
                    PdfPage page = document.AddPage();
                    page.Size = PageSize.A0;
                    page.Height = page.Width;

                    // Get an XGraphics object for drawing on this page
                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    // Scale image to fit A0 page width
                    BitmapSource bitmap = (BitmapSource)image.Source;
                    bitmap = new TransformedBitmap(bitmap, new ScaleTransform(4.0, 4.0));
                    // Create a RenderTargetBitmap for fit into a memory stream
                    int width = (int)page.Width.Point;
                    RenderTargetBitmap rtb = new(width - width / 20, width - width / 20, 1094, 1094, PixelFormats.Default);     // dpi = 1094 fills the whole bitmap area
                    DrawingVisual dv = new();
                    using (DrawingContext dc = dv.RenderOpen())
                        dc.DrawImage(bitmap, new Rect(0, 0, image.ActualWidth, image.ActualHeight));
                    rtb.Render(dv);
                    // Create memory stream
                    JpegBitmapEncoder jpg = new();
                    jpg.Frames.Add(BitmapFrame.Create(rtb));
                    Stream ms = new MemoryStream();
                    jpg.Save(ms);
                    ms.Position = 0;
                    // Load memory stream into XImage
                    XImage ximage = XImage.FromStream(ms);
                    // Draw image into PDF page
                    gfx.DrawImage(ximage, width / 40, width / 40, ximage.PixelWidth, ximage.PixelHeight);

                    // Draw line frame around image
                    width -= 3;
                    gfx.DrawLine(XPens.Black, 3, 3, width, 3);
                    gfx.DrawLine(XPens.Black, 3, 3, 3, width);
                    gfx.DrawLine(XPens.Black, 3, width, width, width);
                    gfx.DrawLine(XPens.Black, width, 3, width, width);

                    // Draw four circle markers
                    width -= 20;
                    int x = (int)(width / 100.0);
                    gfx.DrawEllipse(new XPen(XColors.Black, 1.5), x * 5, x - 5, 30, 30);
                    gfx.DrawEllipse(new XPen(XColors.Black, 1.5), x - 5, width - x * 5 - 8, 30, 30);
                    gfx.DrawEllipse(new XPen(XColors.Black, 1.5), width - x * 5 - 8, width - x - 2, 30, 30);
                    gfx.DrawEllipse(new XPen(XColors.Black, 1.5), width - x - 2, x * 5 - 2, 30, 30);

                    // Create a font
                    XFont font = new("Arial", 50, XFontStyleEx.Regular);
                    // Rotation point = center of page
                    XPoint rotationPoint = new(page.Width.Point / 2, page.Height.Point / 2);

                    switch (pageNumber)
                    {
                        case 0:
                            // Left top text
                            gfx.DrawString("0", font, XBrushes.Black, x * 10, x * 2);
                            // Left bottom text
                            gfx.RotateAtTransform(-90, rotationPoint);
                            gfx.DrawString("A", font, XBrushes.Black, x * 9, x * 2);
                            pageNumber += 1;
                            break;

                        case 1:
                            // Left top text
                            gfx.DrawString("   1", font, XBrushes.Black, x * 10, x * 2);
                            // Right bottom text
                            gfx.RotateAtTransform(180, rotationPoint);
                            gfx.DrawString("  B", font, XBrushes.Black, x * 10, x * 2);
                            pageNumber += 1;
                            break;

                        case 2:
                            // Left top text
                            gfx.DrawString("      2", font, XBrushes.Black, x * 10, x * 2);
                            // Right top text
                            gfx.RotateAtTransform(90, rotationPoint);
                            gfx.DrawString("        C", font, XBrushes.Black, x * 9, x * 2);
                            pageNumber += 1;
                            break;
                    }
                }

                // Show save file dialog
                SaveFileDialog saveFileDialog = new()
                {
                    FileName = "Merged Images",
                    DefaultExt = ".pdf",
                    Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    // Save the document
                    string filename = saveFileDialog.FileName;
                    document.Save(filename);
                }

                // Start a PDF viewer
                PdfFileUtility.ShowDocument(saveFileDialog.FileName);
            }
            catch (Exception err)
            {
                Debug.Write(err.ToString());
            }
        }
    }
}
