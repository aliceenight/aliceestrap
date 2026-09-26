using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;

namespace Aliceestrap
{
    public class JsonManager<T> where T : class, new()
    {
        public T OriginalProp { get; set; } = new();

        public T Prop { get; set; } = new();

        public string? LastFileHash { get; private set; }

        public bool Loaded { get; set; } = false;

        public virtual string ClassName => typeof(T).Name;
        
        public virtual string ProfilesLocation => Path.Combine(Paths.Base, $"Profiles.json");

        public virtual string FileLocation => Path.Combine(Paths.Base, $"{ClassName}.json");

        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public virtual void Load(bool alertFailure = true)
        {
            
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                string contents = File.ReadAllText(FileLocation);

                T? settings = JsonSerializer.Deserialize<T>(contents);

                if (settings is null)
                    throw new ArgumentNullException("Deserialization returned null");

                Prop = settings;
                Loaded = true;
                LastFileHash = MD5Hash.FromString(contents);

                App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                {
                    string message = "";

                    if (ClassName == nameof(Settings))
                        message = Strings.JsonManager_SettingsLoadFailed;
                    else if (ClassName == nameof(FastFlagManager))
                        message = Strings.JsonManager_FastFlagsLoadFailed;

                    if (!String.IsNullOrEmpty(message))
                        Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", System.Windows.MessageBoxImage.Warning);

                    try
                    {
                        File.Copy(FileLocation, FileLocation + ".bak", true);
                    }
                    catch (Exception copyEx)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to create backup file: {FileLocation}.bak");
                        App.Logger.WriteException(LOG_IDENT, copyEx);
                    }
                }

                Save();
            }
        }

        public virtual void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";
            
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                string contents = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(FileLocation, contents);

                LastFileHash = MD5Hash.FromString(contents);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger.WriteException(LOG_IDENT, ex);

                string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                Frontend.ShowMessageBox(errorMessage, System.Windows.MessageBoxImage.Warning);

                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Save complete!");
        }

        public bool HasFileOnDiskChanged()
        {
            return LastFileHash != MD5Hash.FromFile(FileLocation);
        }
    }
}
