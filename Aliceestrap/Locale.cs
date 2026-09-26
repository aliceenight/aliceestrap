using System.Windows;

namespace Aliceestrap
{
    internal static class Locale
    {
        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static bool RightToLeft { get; private set; } = false;

        private static readonly List<string> _rtlLocales = new() { "ar", "he", "fa" };

        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            { "nil", Strings.Common_SystemDefault },
            { "en", "English" },
            { "en-US", "English (United States)" },
#if QA_BUILD
            { "sq", "Albanian" },
#endif
            { "ar", "العربية" },
            { "bg", "Български" },
#if QA_BUILD
            { "bn", "বাংলা" },
#endif
            { "bs", "Bosanski" },
            { "cs", "Čeština" },
            { "de", "Deutsch" },
            { "da", "Dansk" },
            { "es-ES", "Español" },
#if QA_BUILD
            { "el", "Ελληνικά" },
#endif
            { "fa", "فارسی" },
            { "fi", "Suomi" },
            { "fil", "Filipino" },
            { "fr", "Français" },
#if QA_BUILD
            { "he", "עברית‎" },
            { "hi", "Hindi (Latin)" },
#endif
            { "hr", "Hrvatski" },
            { "hu", "Magyar" },
#if QA_BUILD
            { "is", "Íslenska" },
#endif
            { "id", "Bahasa Indonesia" },
            { "it", "Italiano" },
            { "ja", "日本語" },
            { "ko", "한국어" },
            { "lv", "Latviešu" },
            { "lt", "Lietuvių" },
            { "ms", "Malay" },
            { "nl", "Nederlands" },
#if QA_BUILD
            { "et", "Eesti Keel" },
            { "no", "Bokmål" },
#endif
            { "pl", "Polski" },
#if QA_BUILD
            { "pt-PT", "Portugese (European)" },
#endif
            { "pt-BR", "Português (Brasil)" },
            { "ro", "Română" },
            { "ru", "Русский" },
#if QA_BUILD
            { "sr-CS", "Serbian (Latin)" },
#endif
            { "sv-SE", "Svenska" },
            { "th", "ภาษาไทย" },
            { "tr", "Türkçe" },
            { "uk", "Українська" },
            { "vi", "Tiếng Việt" },
            { "zh-Hans-CN", "中文 (简体)" },
            { "zh-Hant-HK", "中文 (香港)" },
            { "zh-Hant-TW", "中文 (繁體)" }
        };

        public static string GetIdentifierFromName(string language) => SupportedLocales.FirstOrDefault(x => x.Value == language).Key ?? "nil";

        public static List<string> GetLanguages()
        {
            var languages = new List<string>();
            
            languages.AddRange(SupportedLocales.Values.Take(3));
            languages.AddRange(SupportedLocales.Values.Where(x => !languages.Contains(x)).OrderBy(x => x));
            languages[0] = Strings.Common_SystemDefault;

            return languages;
        }

        public static void Set(string identifier)
        {
            if (!SupportedLocales.ContainsKey(identifier))
                identifier = "nil";

            if (identifier == "nil")
            {
                CurrentCulture = Thread.CurrentThread.CurrentUICulture;
            }
            else
            {
                CurrentCulture = new CultureInfo(identifier);

                CultureInfo.DefaultThreadCurrentUICulture = CurrentCulture;
                Thread.CurrentThread.CurrentUICulture = CurrentCulture;
            }

            RightToLeft = _rtlLocales.Any(CurrentCulture.Name.StartsWith);
        }

        public static void Initialize()
        {
            Set("nil");

            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
            {
                var window = (Window)sender;

                if (RightToLeft)
                {
                    window.FlowDirection = FlowDirection.RightToLeft;

                    if (window.ContextMenu is not null)
                        window.ContextMenu.FlowDirection = FlowDirection.RightToLeft;
                }
                else if (CurrentCulture.Name.StartsWith("th"))
                {
                    window.FontFamily = new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/Resources/Fonts/"), "./#Noto Sans Thai");
                }

#if QA_BUILD
                window.BorderBrush = System.Windows.Media.Brushes.Red;
                window.BorderThickness = new Thickness(4);
#endif
            }));
        }
    }
}
