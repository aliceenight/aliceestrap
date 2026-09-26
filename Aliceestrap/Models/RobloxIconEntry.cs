using System.Windows.Media;

namespace Aliceestrap.Models
{
    public class RobloxIconEntry
    {
        public RobloxIcon IconType { get; set; }
        public ImageSource ImageSource => IconType.GetIcon().GetImageSource();
    }
}
