using System.Xml.Linq;

namespace Aliceestrap
{
    public enum PresetKind
    {
        RobloxConfig,
        FastFlags
    }

    public static class PresetEditSession
    {
        public static PresetKind? Kind { get; private set; }

        public static string? Name { get; private set; }

        public static bool Active => Name is not null;

        public static event EventHandler? Changed;

        private static XDocument? _configBackup;
        private static bool _configSwapped;
        private static Dictionary<string, object>? _flagsBackup;

        public static bool Begin(PresetKind kind, string name)
        {
            const string LOG_IDENT = "PresetEditSession::Begin";

            if (Active)
                Cancel();

            try
            {
                if (kind == PresetKind.RobloxConfig)
                {
                    if (!GlobalSettingsPresetManager.Exists(name))
                        return false;

                    _configBackup = App.GlobalSettings.Document is null ? null : new XDocument(App.GlobalSettings.Document);
                    _configSwapped = true;
                    App.GlobalSettings.Document = XDocument.Load(GlobalSettingsPresetManager.GetPath(name));
                }
                else
                {
                    if (!FastFlagPresetManager.Exists(name))
                        return false;

                    _flagsBackup = new Dictionary<string, object>(App.FastFlags.Prop);

                    if (!FastFlagPresetManager.Apply(name))
                    {
                        _flagsBackup = null;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Couldn't open '{name}' for editing");
                App.Logger.WriteException(LOG_IDENT, ex);
                Restore();
                return false;
            }

            Kind = kind;
            Name = name;

            App.Logger.WriteLine(LOG_IDENT, $"Editing {kind} preset '{name}'");
            Changed?.Invoke(null, EventArgs.Empty);

            return true;
        }

        public static bool Commit()
        {
            if (!Active)
                return false;

            bool saved = Kind == PresetKind.RobloxConfig
                ? GlobalSettingsPresetManager.Save(Name!)
                : FastFlagPresetManager.Save(Name!);

            if (!saved)
                return false;

            End();
            return true;
        }

        public static void Cancel()
        {
            if (Active)
                End();
        }

        private static void End()
        {
            Restore();

            App.Logger.WriteLine("PresetEditSession::End", $"Finished editing '{Name}'");

            Kind = null;
            Name = null;

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void Restore()
        {
            if (_flagsBackup is not null)
                App.FastFlags.Prop = _flagsBackup;

            if (_configSwapped)
                App.GlobalSettings.Document = _configBackup;

            _configSwapped = false;
            _configBackup = null;
            _flagsBackup = null;
        }
    }
}
