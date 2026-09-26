using System.IO;

namespace Aliceestrap
{
    public static class GlobalSettingsPresetManager
    {
        private const string Extension = ".xml";
        private const int MaxNameLength = 64;

        public static string GetPath(string name) => Path.Combine(Paths.GlobalSettingsPresets, name + Extension);

        public static bool Exists(string name) => IsValidName(name) && File.Exists(GetPath(name));

        public static List<string> GetNames()
        {
            const string LOG_IDENT = "GlobalSettingsPresetManager::GetNames";

            var names = new List<string>();

            if (String.IsNullOrEmpty(Paths.GlobalSettingsPresets) || !Directory.Exists(Paths.GlobalSettingsPresets))
                return names;

            try
            {
                foreach (string file in Directory.GetFiles(Paths.GlobalSettingsPresets, "*" + Extension))
                    names.Add(Path.GetFileNameWithoutExtension(file));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return names;
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return names;
        }

        public static bool IsValidName(string? name)
        {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            if (name.Length > MaxNameLength)
                return false;

            if (name != name.Trim())
                return false;

            return name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
        }

        public static bool Save(string name)
        {
            const string LOG_IDENT = "GlobalSettingsPresetManager::Save";

            if (!IsValidName(name))
                return false;

            try
            {
                Directory.CreateDirectory(Paths.GlobalSettingsPresets);

                if (App.GlobalSettings.Document is not null)
                {
                    App.GlobalSettings.Document.Save(GetPath(name));
                }
                else if (File.Exists(App.GlobalSettings.FileLocation))
                {
                    File.Copy(App.GlobalSettings.FileLocation, GetPath(name), true);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "There are no settings to save");
                    return false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to save config '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Saved config '{name}'");

            return true;
        }

        public static bool Apply(string name)
        {
            const string LOG_IDENT = "GlobalSettingsPresetManager::Apply";

            if (!Exists(name))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Config '{name}' does not exist");
                return false;
            }

            string destination = App.GlobalSettings.FileLocation;

            bool wasReadOnly = App.GlobalSettings.GetReadOnly();

            try
            {
                if (wasReadOnly)
                    App.GlobalSettings.SetReadOnly(false, true);

                string? directory = Path.GetDirectoryName(destination);

                if (!String.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(GetPath(name), destination, true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply config '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
            finally
            {
                if (wasReadOnly)
                    App.GlobalSettings.SetReadOnly(true, true);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Applied config '{name}'");

            App.GlobalSettings.Load();

            return true;
        }

        public static bool Delete(string name)
        {
            const string LOG_IDENT = "GlobalSettingsPresetManager::Delete";

            if (!IsValidName(name))
                return false;

            try
            {
                File.Delete(GetPath(name));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to delete config '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Deleted config '{name}'");

            return true;
        }
    }
}
