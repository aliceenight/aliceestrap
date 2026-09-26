using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Aliceestrap.Enums.GBSPresets;

namespace Aliceestrap.UI.ViewModels.Settings
{
    public class GlobalSettingsViewModel : NotifyPropertyChangedViewModel
    {
        public event EventHandler? OpenConfigsEvent;

        public ICommand OpenConfigsCommand => new RelayCommand(() => OpenConfigsEvent?.Invoke(this, EventArgs.Empty));

        public bool ReadOnly
        {
            get => App.GlobalSettings.GetReadOnly();
            set => App.GlobalSettings.SetReadOnly(value);
        }

        public int FramerateCap
        {
            get
            {
                if (int.TryParse(App.GlobalSettings.GetPreset("Rendering.FramerateCap"), out int framerate))
                {
                    if (framerate < 1)
                        return 60;
                    else
                        return framerate;
                }
                else
                    return 60;
            }
            set
            {
                if (value < 1)
                    value = -1;

                App.GlobalSettings.SetPreset("Rendering.FramerateCap", value);
            }
        }

        public string UITransparency
        {
            get => App.GlobalSettings.GetPreset("UI.Transparency")!;
            set
            {
                App.GlobalSettings.SetPreset("UI.Transparency", value.Length >= 3 ? value[..3] : value);

                OnPropertyChanged(nameof(UITransparency));
            }
        }

        public string GraphicsQuality
        {
            get => App.GlobalSettings.GetPreset("Rendering.SavedQualityLevel")!;
            set
            {
                App.GlobalSettings.SetPreset("Rendering.SavedQualityLevel", value);

                OnPropertyChanged(nameof(GraphicsQuality));
            }
        }

        public bool ReducedMotion
        {
            get => App.GlobalSettings.GetPreset("UI.ReducedMotion")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("UI.ReducedMotion", value);
        }

        public IReadOnlyDictionary<FontSize, string?> FontSizes => GlobalSettingsManager.FontSizes;
        public FontSize SelectedFontSize
        {
            get => FontSizes.FirstOrDefault(x => x.Value == App.GlobalSettings.GetPreset("UI.FontSize")).Key;
            set => App.GlobalSettings.SetPreset("UI.FontSize", FontSizes[value]);
        }

        public int MasterVolume
        {
            get
            {
                string? raw = App.GlobalSettings.GetPreset("Audio.MasterVolume");

                if (raw is null || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float volume))
                    return 10;

                return Math.Clamp((int)Math.Round(volume * 10), 0, 10);
            }
            set
            {
                int steps = Math.Clamp(value, 0, 10);

                App.GlobalSettings.SetPresetOrCreate(
                    "Audio.MasterVolume",
                    "float",
                    "MasterVolume",
                    (steps / 10f).ToString("0.0########", CultureInfo.InvariantCulture)
                );

                OnPropertyChanged(nameof(MasterVolume));
            }
        }

        public string MouseSensitivity
        {
            get => App.GlobalSettings.GetPreset("User.MouseSensitivity")!;
            set => App.GlobalSettings.SetPreset("User.MouseSensitivity", value);
        }

        public string VREnabled
        {
            get => App.GlobalSettings.GetPreset("User.VREnabled")!;
            set => App.GlobalSettings.SetPreset("User.VREnabled", value);
        }

    }
}
