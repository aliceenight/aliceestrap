using System.Windows;

using Aliceestrap.UI.ViewModels.About;

namespace Aliceestrap.UI.Elements.About
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            DataContext = new AboutViewModel();

            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing about window");

            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive && x != this);

            if (Owner is null)
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}
