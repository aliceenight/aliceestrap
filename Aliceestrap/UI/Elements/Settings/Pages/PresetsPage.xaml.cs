using Aliceestrap.UI.ViewModels.Settings;

namespace Aliceestrap.UI.Elements.Settings.Pages
{
    public partial class PresetsPage
    {
        public PresetsPage()
        {
            DataContext = new PresetsViewModel();
            InitializeComponent();
        }
    }
}
