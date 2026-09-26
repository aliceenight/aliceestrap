namespace Aliceestrap
{
    public static class FastFlagPresetManager
    {
        private const string Extension = ".json";
        private const int MaxNameLength = 64;

        public static string GetPath(string name) => Path.Combine(Paths.FastFlagPresets, name + Extension);

        public static bool Exists(string name) => IsValidName(name) && File.Exists(GetPath(name));

        public static List<string> GetNames()
        {
            const string LOG_IDENT = "FastFlagPresetManager::GetNames";

            var names = new List<string>();

            if (String.IsNullOrEmpty(Paths.FastFlagPresets) || !Directory.Exists(Paths.FastFlagPresets))
                return names;

            try
            {
                foreach (string file in Directory.GetFiles(Paths.FastFlagPresets, "*" + Extension))
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

            if (name.Length > MaxNameLength || name != name.Trim())
                return false;

            return name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
        }

        public static bool Save(string name)
        {
            const string LOG_IDENT = "FastFlagPresetManager::Save";

            if (!IsValidName(name))
                return false;

            try
            {
                Directory.CreateDirectory(Paths.FastFlagPresets);

                var flags = App.FastFlags.Prop.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? "");

                File.WriteAllText(GetPath(name), JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to save preset '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Saved preset '{name}'");

            return true;
        }

        public static bool Apply(string name)
        {
            const string LOG_IDENT = "FastFlagPresetManager::Apply";

            if (!Exists(name))
                return false;

            try
            {
                var flags = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(GetPath(name)));

                if (flags is null)
                    return false;

                App.FastFlags.Prop = flags.ToDictionary(x => x.Key, x => (object)x.Value);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply preset '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Applied preset '{name}'");

            return true;
        }

        public static bool Delete(string name)
        {
            const string LOG_IDENT = "FastFlagPresetManager::Delete";

            if (!IsValidName(name))
                return false;

            try
            {
                File.Delete(GetPath(name));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to delete preset '{name}'");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Deleted preset '{name}'");

            return true;
        }
    }
}
