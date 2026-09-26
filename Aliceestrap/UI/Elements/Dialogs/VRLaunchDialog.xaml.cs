using System.Windows;

namespace Aliceestrap.UI.Elements.Dialogs
{
    public partial class VRLaunchDialog
    {
        public VRLaunchChoice Choice { get; private set; } = VRLaunchChoice.Cancelled;

        public VRLaunchDialog(bool vrCurrentlyEnabled)
        {
            InitializeComponent();

            Loaded += delegate
            {
                if (vrCurrentlyEnabled)
                    VRButton.Focus();
                else
                    DesktopButton.Focus();
            };
        }

        private void VR_Click(object sender, RoutedEventArgs e)
        {
            Choice = VRLaunchChoice.VR;
            Close();
        }

        private void Desktop_Click(object sender, RoutedEventArgs e)
        {
            Choice = VRLaunchChoice.Desktop;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = VRLaunchChoice.Cancelled;
            Close();
        }
    }
}
