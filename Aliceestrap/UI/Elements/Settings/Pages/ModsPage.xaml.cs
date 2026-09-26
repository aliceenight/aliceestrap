using Aliceestrap.UI.ViewModels.Settings;

namespace Aliceestrap.UI.Elements.Settings.Pages
{
    public partial class ModsPage
    {
        public ModsPage()
        {
            DataContext = new ModsViewModel();
            InitializeComponent();
        }
    }
}
