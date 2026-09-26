using DiscordRPC;
using System.Collections.ObjectModel;

namespace Aliceestrap.Models.Persistable
{
    public class Settings
    {
        public bool AllowCookieAccess { get; set; } = false;

        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.AliceDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconDefault;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public RobloxIcon RobloxIcon { get; set; } = RobloxIcon.IconDefault;
        public string RobloxTitle { get; set; } = "Roblox";
        public string RobloxIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool UseAcrylicBackground { get; set; } = false;
        public byte AcrylicBackgroundOpacity { get; set; } = 165;
        public bool ForceLocalData { get; set; } = true;
        public bool ConfirmLaunches { get; set; } = true;
        public bool PromptVRModeOnLaunch { get; set; } = false;
        public bool PromptConfigOnLaunch { get; set; } = false;
        public bool PromptFlagPresetOnLaunch { get; set; } = false;
        public bool PromptChannelOnLaunch { get; set; } = false;
        public List<string> ChannelPresets { get; set; } = new List<string> { RobloxInterfaces.Deployment.DefaultChannel };
        public string? GlobalSettingsPreset { get; set; } = null;
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = false;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool StaticDirectory { get; set; } = false;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public string RobloxDomain { get; set; } = RobloxInterfaces.Deployment.DefaultRobloxDomain;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Automatic;
        public string? SelectedCustomTheme { get; set; } = null;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.TwoWeeks;
        public List<string> CleanerDirectories { get; set; } = new List<string> {
            "RobloxCache",
            "RobloxStudioCache",
            "RobloxLogs",
            "AliceestrapLogs"
        };
        public bool EnableWindowManipulation { get; set; } = false;
        public bool FakeBorderlessFullscreen { get; set; } = false;
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public DiscordRPCStatusDisplay RichPresenceStatusDisplayType { get; set; } = DiscordRPCStatusDisplay.Name;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        public bool UseDisableAppPatch { get; set; } = false;
    }
}
