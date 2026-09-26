using Aliceestrap.UI.ViewModels.Settings;

using System.Windows.Controls;

namespace Aliceestrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
        }

        public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            AppearanceViewModel viewModel = (AppearanceViewModel)DataContext;

            viewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
            viewModel.SelectedCustomThemeName = viewModel.SelectedCustomTheme;

            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomTheme));
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomThemeName));
        }
    }
}