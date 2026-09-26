namespace Aliceestrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {

        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (object? _, CookieState state) => CookieLoadingFailed = state != CookieState.Success && state != CookieState.Unknown;
        }

        public bool IsRobloxInstallationMissing => String.IsNullOrEmpty(App.RobloxState.Prop.Player.VersionGuid) && String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool PromptVRModeOnLaunch
        {
            get => App.Settings.Prop.PromptVRModeOnLaunch;
            set => App.Settings.Prop.PromptVRModeOnLaunch = value;
        }

        public bool PromptConfigOnLaunch
        {
            get => App.Settings.Prop.PromptConfigOnLaunch;
            set => App.Settings.Prop.PromptConfigOnLaunch = value;
        }

        public bool PromptFlagPresetOnLaunch
        {
            get => App.Settings.Prop.PromptFlagPresetOnLaunch;
            set => App.Settings.Prop.PromptFlagPresetOnLaunch = value;
        }

        public bool PromptChannelOnLaunch
        {
            get => App.Settings.Prop.PromptChannelOnLaunch;
            set => App.Settings.Prop.PromptChannelOnLaunch = value;
        }

        public bool ForceRobloxLanguage
        {
            get => App.Settings.Prop.ForceRobloxLanguage;
            set => App.Settings.Prop.ForceRobloxLanguage = value;
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public bool CheckForUpdates
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxLogs");
                else
                    CleanerItems.Remove("RobloxLogs");
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxCache");
                else
                    CleanerItems.Remove("RobloxCache");
            }
        }

        public bool CleanerStudioCache
        {
            get => CleanerItems.Contains("RobloxStudioCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxStudioCache");
                else
                    CleanerItems.Remove("RobloxStudioCache");
            }
        }

        public bool CleanerAliceestrap
        {
            get => CleanerItems.Contains("AliceestrapLogs");
            set
            {
                if (value)
                    CleanerItems.Add("AliceestrapLogs");
                else
                    CleanerItems.Remove("AliceestrapLogs");
            }
        }
    }
}
