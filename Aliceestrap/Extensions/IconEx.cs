using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Aliceestrap.Extensions
{
    public static class IconEx
    {
        public static Icon GetSized(this Icon icon, int width, int height) => new(icon, new Size(width, height));

        public static ImageSource GetImageSource(this Icon icon, bool handleException = true)
        {
            using MemoryStream stream = new();
            icon.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);

            if (handleException)
            {
                try
                {
                    return GetLargestFrame(stream);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("IconEx::GetImageSource", ex);
                    Frontend.ShowMessageBox(string.Format(Strings.Dialog_IconLoadFailed, ex.Message));
                    return BootstrapperIcon.IconDefault.GetIcon().GetImageSource(false);
                }
            }
            else
            {
                return GetLargestFrame(stream);
            }
        }

        private static ImageSource GetLargestFrame(Stream stream)
        {
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            BitmapFrame? largest = null;

            foreach (var frame in decoder.Frames)
            {
                if (largest is null || frame.PixelWidth * frame.PixelHeight > largest.PixelWidth * largest.PixelHeight)
                    largest = frame;
            }

            return largest ?? decoder.Frames[0];
        }
    }
}
