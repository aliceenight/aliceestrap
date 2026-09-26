using System.Windows;

using Aliceestrap.RobloxInterfaces;

namespace Aliceestrap.UI.Elements.Dialogs
{
    public partial class LaunchOptionsDialog
    {
        public bool Confirmed { get; private set; } = false;

        public string? SelectedChannel { get; private set; }

        public string? SelectedPreset { get; private set; }

        public string? SelectedFlagPreset { get; private set; }

        public bool? LaunchInVR { get; private set; }

        private readonly bool _askChannel;
        private readonly bool _askConfig;
        private readonly bool _askVR;
        private readonly bool _askFlags;

        public LaunchOptionsDialog(bool askChannel, bool askConfig, bool askVR, string? currentChannel, string? defaultPreset, bool vrEnabled, bool askFlags = false)
        {
            InitializeComponent();

            _askFlags = askFlags;

            if (askFlags)
                SetupFlagsBox();
            else
                FlagsRow.Visibility = Visibility.Collapsed;

            _askChannel = askChannel;
            _askConfig = askConfig;
            _askVR = askVR;

            if (askChannel)
                SetupChannelBox(currentChannel);
            else
                ChannelRow.Visibility = Visibility.Collapsed;

            if (askConfig)
                SetupConfigBox(defaultPreset);
            else
                ConfigRow.Visibility = Visibility.Collapsed;

            if (askVR)
                VRToggle.IsChecked = vrEnabled;
            else
                VRRow.Visibility = Visibility.Collapsed;

            Loaded += delegate { LaunchButton.Focus(); };
        }

        private void SetupChannelBox(string? currentChannel)
        {
            foreach (string name in ChannelPresetManager.GetNames())
                ChannelBox.Items.Add(name);

            if (!String.IsNullOrEmpty(currentChannel) && !ChannelBox.Items.Contains(currentChannel))
                ChannelBox.Items.Insert(0, currentChannel);

            if (ChannelBox.Items.Count == 0)
                ChannelBox.Items.Add(Deployment.DefaultChannel);

            int index = currentChannel is null ? -1 : ChannelBox.Items.IndexOf(currentChannel);

            ChannelBox.SelectedIndex = index < 0 ? 0 : index;
        }

        private void SetupConfigBox(string? defaultPreset)
        {
            ConfigBox.Items.Add(Strings.Dialog_LaunchOptions_DontChange);

            foreach (string name in GlobalSettingsPresetManager.GetNames())
                ConfigBox.Items.Add(name);

            int selected = 0;

            if (!String.IsNullOrEmpty(defaultPreset))
            {
                int index = ConfigBox.Items.IndexOf(defaultPreset);

                if (index > 0)
                    selected = index;
            }

            ConfigBox.SelectedIndex = selected;
        }

        private void SetupFlagsBox()
        {
            FlagsBox.Items.Add(Strings.Dialog_LaunchOptions_DontChange);

            foreach (string name in FastFlagPresetManager.GetNames())
                FlagsBox.Items.Add(name);

            FlagsBox.SelectedIndex = 0;
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (_askFlags && FlagsBox.SelectedIndex > 0)
                SelectedFlagPreset = FlagsBox.SelectedItem as string;

            if (_askChannel)
                SelectedChannel = ChannelBox.SelectedItem as string;

            if (_askConfig && ConfigBox.SelectedIndex > 0)
                SelectedPreset = ConfigBox.SelectedItem as string;

            if (_askVR)
                LaunchInVR = VRToggle.IsChecked == true;

            Confirmed = true;

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
