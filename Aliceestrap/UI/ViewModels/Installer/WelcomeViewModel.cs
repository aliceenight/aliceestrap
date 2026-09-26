namespace Aliceestrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "aliceestrap"
        );

        public bool CanContinue { get; set; } = false;
    }
}
