using System.Windows;

namespace Aliceestrap.UI.ViewModels.About
{
    public record LicenseEntry(string Name, string License, string Url);

    public class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.DisplayVersion);

        public BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public string BuildTimestamp => BuildMetadata.Timestamp.ToFriendlyString();

        public Visibility BuildInformationVisibility => App.IsProductionBuild ? Visibility.Collapsed : Visibility.Visible;
        public Visibility BuildCommitVisibility => App.IsActionBuild ? Visibility.Visible : Visibility.Collapsed;

        public IReadOnlyList<LicenseEntry> Licenses { get; } = new LicenseEntry[]
        {
            new("Bloxstrap", "MIT, © 2022 pizzaboxer", "https://github.com/bloxstraplabs/bloxstrap/blob/main/LICENSE"),
            new("Fishstrap", "MIT, © 2025 returnrqt", "https://github.com/returnrqt/fishstrap/blob/main/LICENSE"),
            new("WPF-UI", "MIT", "https://github.com/lepoco/wpfui/blob/main/LICENSE"),
            new("AvalonEdit", "MIT", "https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE"),
            new("DiscordRPC", "MIT", "https://github.com/Lachee/discord-rpc-csharp/blob/master/LICENSE"),
            new("Markdig", "BSD 2-Clause", "https://github.com/xoofx/markdig/blob/master/license.txt"),
            new("Roblox Studio Mod Manager", "MIT", "https://github.com/MaximumADHD/Roblox-Studio-Mod-Manager/blob/main/LICENSE"),
            new("RoValra", "GPL-3.0", "https://github.com/NotValra/RoValra/blob/main/LICENSE"),
            new("SharpVectors", "BSD 3-Clause", "https://github.com/ElinamLLC/SharpVectors/blob/master/License.md"),
            new("SharpZipLib", "MIT", "https://github.com/icsharpcode/SharpZipLib/blob/master/LICENSE.txt"),
            new("ShellLink", "MIT", "https://github.com/securifybv/ShellLink/blob/master/LICENSE.txt"),
        };
    }
}
