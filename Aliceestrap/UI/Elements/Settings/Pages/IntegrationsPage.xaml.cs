using System.Windows.Controls;

using Aliceestrap.UI.ViewModels.Settings;

namespace Aliceestrap.UI.Elements.Settings.Pages
{
    public partial class IntegrationsPage
    {
        public IntegrationsPage()
        {
            DataContext = new IntegrationsViewModel();
            InitializeComponent();
        }

        public void CustomIntegrationSelection(object sender, SelectionChangedEventArgs e)
        {
            IntegrationsViewModel viewModel = (IntegrationsViewModel)DataContext;
            viewModel.SelectedCustomIntegration = (CustomIntegration)((ListBox)sender).SelectedItem;
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomIntegration));
        }
    }
}
