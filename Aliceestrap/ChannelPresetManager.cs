using Aliceestrap.RobloxInterfaces;

namespace Aliceestrap
{
    public static class ChannelPresetManager
    {
        private const int MaxNameLength = 64;

        private static readonly Regex NamePattern = new(@"^[A-Za-z0-9_\-\.]+$");

        public static bool IsValidName(string? name) =>
            !String.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength && NamePattern.IsMatch(name);

        public static bool Contains(string? name) =>
            !String.IsNullOrEmpty(name) && App.Settings.Prop.ChannelPresets.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static List<string> GetNames()
        {
            var names = new List<string>();

            foreach (string name in App.Settings.Prop.ChannelPresets)
            {
                if (!IsValidName(name))
                    continue;

                if (names.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return names;
        }

        public static bool Add(string name)
        {
            const string LOG_IDENT = "ChannelPresetManager::Add";

            if (!IsValidName(name) || Contains(name))
                return false;

            App.Settings.Prop.ChannelPresets.Add(name);

            App.Logger.WriteLine(LOG_IDENT, $"Saved channel '{name}'");

            return true;
        }

        public static bool Delete(string name)
        {
            const string LOG_IDENT = "ChannelPresetManager::Delete";

            if (String.IsNullOrEmpty(name))
                return false;

            int removed = App.Settings.Prop.ChannelPresets.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
                return false;

            App.Logger.WriteLine(LOG_IDENT, $"Removed channel '{name}'");

            return true;
        }

        public static bool Rename(string oldName, string newName)
        {
            const string LOG_IDENT = "ChannelPresetManager::Rename";

            if (!IsValidName(newName))
                return false;

            var presets = App.Settings.Prop.ChannelPresets;
            int index = presets.FindIndex(x => x.Equals(oldName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                return false;

            if (!oldName.Equals(newName, StringComparison.OrdinalIgnoreCase) && Contains(newName))
                return false;

            presets[index] = newName;

            if (App.Settings.Prop.Channel.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                App.Settings.Prop.Channel = newName;

            App.Logger.WriteLine(LOG_IDENT, $"Renamed channel '{oldName}' to '{newName}'");

            return true;
        }

        public static string GetDefault()
        {
            string channel = App.Settings.Prop.Channel;

            return IsValidName(channel) ? channel : Deployment.DefaultChannel;
        }
    }
}
