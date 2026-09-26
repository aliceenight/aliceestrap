using System.Windows;

using Aliceestrap.RobloxInterfaces;

namespace Aliceestrap.UI.ViewModels.Bootstrapper
{
    public class AliceDialogViewModel : BootstrapperDialogViewModel
    {
        public string Subtitle { get; private set; } = "";
        public string Channel { get; private set; } = Deployment.Channel;
        public string Footer { get; } = $"v{App.DisplayVersion}";

        public string ProgressText => ProgressMaximum > 0
            ? $"{Math.Clamp((long)ProgressValue * 100 / ProgressMaximum, 0, 100)}%"
            : "";

        public Visibility ProgressTextVisibility => !ProgressIndeterminate && ProgressMaximum > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public AliceDialogViewModel(IBootstrapperDialog dialog) : base(dialog)
        {
            if (Title != App.ProjectName)
                Footer = $"{App.ProjectName} v{App.DisplayVersion}";

            SetTarget(false);
        }

        public void SetTarget(bool studio)
        {
            string app = studio ? "Roblox Studio" : "Roblox";
            string version = Utilities.GetRobloxVersionStr(studio);

            Subtitle = String.IsNullOrEmpty(version) ? app : $"{app} {version}";
            OnPropertyChanged(nameof(Subtitle));
        }

        public void SetChannel(string channel)
        {
            Channel = channel;
            OnPropertyChanged(nameof(Channel));
        }

        public void RefreshProgress()
        {
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ProgressTextVisibility));
        }
    }
}
