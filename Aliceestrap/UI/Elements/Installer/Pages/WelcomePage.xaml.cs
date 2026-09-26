using System.Windows;
using Aliceestrap.UI.ViewModels.Installer;

namespace Aliceestrap.UI.Elements.Installer.Pages
{
    public partial class WelcomePage
    {
        private readonly WelcomeViewModel _viewModel = new();

        public WelcomePage()
        {
                if (Window.GetWindow(this) is MainWindow window)
                    window.SetButtonEnabled("next", true);

            DataContext = _viewModel;
            InitializeComponent();
        }

        private void UiPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
                window.SetNextButtonText(Strings.Common_Navigation_Next);
        }
    }
}
