using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Aliceestrap.RobloxInterfaces;

namespace Aliceestrap.UI.ViewModels.Settings
{
    public class PresetEntry : NotifyPropertyChangedViewModel
    {
        private readonly Func<string, bool>? _rename;

        public PresetEntry(string name, string? badge, string primaryLabel, Action primary, Action edit, Action delete, Func<string, bool>? rename = null)
        {
            Name = name;
            Badge = badge;
            PrimaryLabel = primaryLabel;
            Primary = new RelayCommand(primary);
            Delete = new RelayCommand(delete);
            _rename = rename;

            Edit = rename is null ? new RelayCommand(edit) : new RelayCommand(StartRename);
        }

        public string Name { get; }
        public string? Badge { get; }
        public string PrimaryLabel { get; }

        public ICommand Primary { get; }
        public ICommand Edit { get; }
        public ICommand Delete { get; }
        public ICommand SaveRename => new RelayCommand(SaveRenameInternal);
        public ICommand CancelRename => new RelayCommand(() => SetRenaming(false));

        public Visibility BadgeVisibility => Badge is null ? Visibility.Collapsed : Visibility.Visible;

        public bool IsRenaming { get; private set; }
        public Visibility NormalVisibility => IsRenaming ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RenamingVisibility => IsRenaming ? Visibility.Visible : Visibility.Collapsed;

        public string EditName { get; set; } = "";

        private void StartRename()
        {
            EditName = Name;
            OnPropertyChanged(nameof(EditName));
            SetRenaming(true);
        }

        private void SaveRenameInternal()
        {
            string newName = EditName.Trim();

            if (newName == Name)
            {
                SetRenaming(false);
                return;
            }

            if (_rename?.Invoke(newName) == true)
                SetRenaming(false);
        }

        private void SetRenaming(bool renaming)
        {
            IsRenaming = renaming;
            OnPropertyChanged(nameof(IsRenaming));
            OnPropertyChanged(nameof(NormalVisibility));
            OnPropertyChanged(nameof(RenamingVisibility));
        }
    }

    public class PresetsViewModel : NotifyPropertyChangedViewModel
    {
        public PresetsViewModel()
        {
            RefreshChannels();
            RefreshConfigs();
            RefreshFlags();
        }

        private static void Warn(string message) => Frontend.ShowMessageBox(message, MessageBoxImage.Warning);

        private static bool Confirm(string message) =>
            Frontend.ShowMessageBox(message, MessageBoxImage.Question, MessageBoxButton.YesNo) == MessageBoxResult.Yes;

        #region deployment channels

        public ObservableCollection<PresetEntry> Channels { get; } = new();

        public bool HasChannels => Channels.Count > 0;

        public string NewChannelName { get; set; } = "";

        private string _channelStatus = "";
        public string ChannelStatus
        {
            get => _channelStatus;
            private set
            {
                _channelStatus = value;
                OnPropertyChanged(nameof(ChannelStatus));
            }
        }

        public ICommand AddChannelCommand => new RelayCommand(AddChannel);

        private void RefreshChannels()
        {
            Channels.Clear();

            foreach (string name in ChannelPresetManager.GetNames())
            {
                bool inUse = name.Equals(App.Settings.Prop.Channel, StringComparison.OrdinalIgnoreCase);
                Channels.Add(new PresetEntry(name, inUse ? Strings.Menu_Presets_Channels_Current : null,
                    Strings.Menu_Channel_Presets_Use, () => UseChannel(name), () => { }, () => DeleteChannel(name),
                    newName => RenameChannel(name, newName)));
            }

            OnPropertyChanged(nameof(HasChannels));
        }

        private void UseChannel(string? name)
        {
            if (String.IsNullOrEmpty(name))
                return;

            string lower = name.ToLowerInvariant();
            App.Settings.Prop.Channel = lower == "live" || lower == "zlive" ? Deployment.DefaultChannel : name;

            ChannelStatus = String.Format(Strings.Menu_Presets_Channels_Used, App.Settings.Prop.Channel);
            RefreshChannels();
        }

        private bool RenameChannel(string oldName, string newName)
        {
            if (!ChannelPresetManager.IsValidName(newName))
            {
                Warn(Strings.Menu_Channel_Presets_InvalidName);
                return false;
            }

            if (!ChannelPresetManager.Rename(oldName, newName))
            {
                Frontend.ShowMessageBox(String.Format(Strings.Menu_Channel_Presets_Duplicate, newName), MessageBoxImage.Information);
                return false;
            }

            ChannelStatus = "";
            RefreshChannels();
            return true;
        }

        private void DeleteChannel(string? name)
        {
            if (String.IsNullOrEmpty(name) || !Confirm(String.Format(Strings.Menu_Channel_Presets_ConfirmDelete, name)))
                return;

            ChannelPresetManager.Delete(name);
            ChannelStatus = "";
            RefreshChannels();
        }

        private void AddChannel()
        {
            string name = String.IsNullOrWhiteSpace(NewChannelName) ? App.Settings.Prop.Channel : NewChannelName.Trim();

            if (!ChannelPresetManager.IsValidName(name))
            {
                Warn(Strings.Menu_Channel_Presets_InvalidName);
                return;
            }

            if (!ChannelPresetManager.Add(name))
            {
                Frontend.ShowMessageBox(String.Format(Strings.Menu_Channel_Presets_Duplicate, name), MessageBoxImage.Information);
                return;
            }

            NewChannelName = "";
            OnPropertyChanged(nameof(NewChannelName));
            RefreshChannels();
        }

        #endregion

        #region roblox settings configs

        public bool ConfigsAvailable => App.GlobalSettings.Loaded;

        public Visibility ConfigsUnavailableVisibility => ConfigsAvailable ? Visibility.Collapsed : Visibility.Visible;

        public ObservableCollection<PresetEntry> Configs { get; } = new();

        public bool HasConfigs => Configs.Count > 0;

        public ObservableCollection<string> LaunchConfigs { get; } = new();

        public string NewConfigName { get; set; } = "";

        private string _configStatus = "";
        public string ConfigStatus
        {
            get => _configStatus;
            private set
            {
                _configStatus = value;
                OnPropertyChanged(nameof(ConfigStatus));
            }
        }

        public ICommand SaveConfigCommand => new RelayCommand(SaveConfig);

        private static string NoneOption => Strings.Menu_GBSEditor_Configs_None;

        public string LaunchConfig
        {
            get
            {
                string? saved = App.Settings.Prop.GlobalSettingsPreset;

                return String.IsNullOrEmpty(saved) || !GlobalSettingsPresetManager.Exists(saved) ? NoneOption : saved;
            }
            set
            {
                if (value is null)
                    return;

                App.Settings.Prop.GlobalSettingsPreset = value == NoneOption ? null : value;
                RefreshConfigs();
            }
        }

        private void RefreshConfigs()
        {
            Configs.Clear();

            var names = GlobalSettingsPresetManager.GetNames();

            foreach (string name in names)
            {
                bool onLaunch = name == App.Settings.Prop.GlobalSettingsPreset;
                Configs.Add(new PresetEntry(name, onLaunch ? Strings.Menu_Presets_Configs_OnLaunch : null,
                    Strings.Menu_Presets_Configs_Apply, () => ApplyConfig(name), () => EditPreset(PresetKind.RobloxConfig, name), () => DeleteConfig(name)));
            }

            var launch = new List<string> { NoneOption };
            launch.AddRange(names);

            if (!launch.SequenceEqual(LaunchConfigs))
            {
                LaunchConfigs.Clear();

                foreach (string name in launch)
                    LaunchConfigs.Add(name);
            }

            OnPropertyChanged(nameof(HasConfigs));
            OnPropertyChanged(nameof(LaunchConfig));
        }

        private void ApplyConfig(string? name)
        {
            if (String.IsNullOrEmpty(name))
                return;

            if (!GlobalSettingsPresetManager.Apply(name))
            {
                Frontend.ShowMessageBox(Strings.Menu_GBSEditor_Configs_LoadFailed, MessageBoxImage.Error);
                return;
            }

            ConfigStatus = String.Format(Strings.Menu_Presets_Configs_Applied, name);
        }

        private void DeleteConfig(string? name)
        {
            if (String.IsNullOrEmpty(name) || !Confirm(String.Format(Strings.Menu_GBSEditor_Configs_ConfirmDelete, name)))
                return;

            GlobalSettingsPresetManager.Delete(name);

            if (App.Settings.Prop.GlobalSettingsPreset == name)
                App.Settings.Prop.GlobalSettingsPreset = null;

            ConfigStatus = "";
            RefreshConfigs();
        }

        private void SaveConfig()
        {
            string name = NewConfigName.Trim();

            if (!GlobalSettingsPresetManager.IsValidName(name))
            {
                Warn(Strings.Menu_GBSEditor_Configs_InvalidName);
                return;
            }

            if (GlobalSettingsPresetManager.Exists(name) && !Confirm(String.Format(Strings.Menu_GBSEditor_Configs_Overwrite, name)))
                return;

            if (!GlobalSettingsPresetManager.Save(name))
            {
                Frontend.ShowMessageBox(Strings.Menu_GBSEditor_Configs_SaveFailed, MessageBoxImage.Error);
                return;
            }

            NewConfigName = "";
            OnPropertyChanged(nameof(NewConfigName));
            ConfigStatus = "";
            RefreshConfigs();
        }

        #endregion

        #region fast flag presets

        public ObservableCollection<PresetEntry> Flags { get; } = new();

        public bool HasFlags => Flags.Count > 0;

        public string NewFlagPresetName { get; set; } = "";

        private string _flagStatus = "";
        public string FlagStatus
        {
            get => _flagStatus;
            private set
            {
                _flagStatus = value;
                OnPropertyChanged(nameof(FlagStatus));
            }
        }

        public ICommand SaveFlagPresetCommand => new RelayCommand(SaveFlagPreset);

        private void RefreshFlags()
        {
            Flags.Clear();

            foreach (string name in FastFlagPresetManager.GetNames())
                Flags.Add(new PresetEntry(name, null,
                    Strings.Menu_Presets_Flags_Apply, () => ApplyFlagPreset(name), () => EditPreset(PresetKind.FastFlags, name), () => DeleteFlagPreset(name)));

            OnPropertyChanged(nameof(HasFlags));
        }

        private void ApplyFlagPreset(string? name)
        {
            if (String.IsNullOrEmpty(name))
                return;

            if (!FastFlagPresetManager.Apply(name))
            {
                Frontend.ShowMessageBox(Strings.Menu_Presets_Flags_Failed, MessageBoxImage.Error);
                return;
            }

            FlagStatus = String.Format(Strings.Menu_Presets_Flags_Applied, name);
        }

        private void DeleteFlagPreset(string? name)
        {
            if (String.IsNullOrEmpty(name) || !Confirm(String.Format(Strings.Menu_Presets_Flags_ConfirmDelete, name)))
                return;

            FastFlagPresetManager.Delete(name);
            FlagStatus = "";
            RefreshFlags();
        }

        private void SaveFlagPreset()
        {
            string name = NewFlagPresetName.Trim();

            if (!FastFlagPresetManager.IsValidName(name))
            {
                Warn(Strings.Menu_Presets_Flags_InvalidName);
                return;
            }

            if (FastFlagPresetManager.Exists(name) && !Confirm(String.Format(Strings.Menu_Presets_Flags_Overwrite, name)))
                return;

            if (!FastFlagPresetManager.Save(name))
            {
                Frontend.ShowMessageBox(Strings.Menu_Presets_Flags_Failed, MessageBoxImage.Error);
                return;
            }

            NewFlagPresetName = "";
            OnPropertyChanged(nameof(NewFlagPresetName));
            FlagStatus = "";
            RefreshFlags();
        }

        #endregion

        private static void EditPreset(PresetKind kind, string name)
        {
            if (!PresetEditSession.Begin(kind, name))
                Frontend.ShowMessageBox(Strings.Menu_Presets_Flags_Failed, MessageBoxImage.Error);
        }
    }
}
