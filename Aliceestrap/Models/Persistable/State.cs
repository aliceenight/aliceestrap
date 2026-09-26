using System.Windows.Forms;

namespace Aliceestrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool WatcherRunning { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage {  get; set; } = null!;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        #region Deprecated properties
        public AppState? Player { private get; set; }
        public AppState? GetDeprecatedPlayer() => Player;

        public AppState? Studio { private get; set; }
        public AppState? GetDeprecatedStudio() => Studio;

        public List<string>? ModManifest { private get; set; }
        public List<string>? GetDeprecatedModManifest() => ModManifest;
        #endregion
    }
}
