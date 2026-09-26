using System.Windows;
using System.Windows.Input;

using Aliceestrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Aliceestrap.UI.Elements.Settings.Pages
{
    public partial class GlobalSettingsPage
    {
        private GlobalSettingsViewModel _viewModel = null!;

        public GlobalSettingsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private bool _initialLoad = false;

        private void SetupViewModel()
        {
            _viewModel = new GlobalSettingsViewModel();

            _viewModel.OpenConfigsEvent += OpenConfigs;

            DataContext = _viewModel;
        }

        private void OpenConfigs(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                window.Navigate(typeof(PresetsPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);
        private void ValidateFloat(object sender, TextCompositionEventArgs e) => e.Handled = !Regex.IsMatch(e.Text, @"^\d*\.?\d*$");
    }
}
