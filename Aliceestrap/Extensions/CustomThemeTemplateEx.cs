using System.Text;

namespace Aliceestrap.Extensions
{
    static class CustomThemeTemplateEx
    {
        public static string GetFileName(this CustomThemeTemplate template)
        {
            return $"CustomBootstrapperTemplate_{template}.xml";
        } 

        public static string GetFileContents(this CustomThemeTemplate template)
        {
            string contents = Encoding.UTF8.GetString(Resource.Get(template.GetFileName()).Result);

            switch (template)
            {
                case CustomThemeTemplate.Blank:
                    return contents.Replace("{0}", Strings.CustomTheme_Templates_Blank_UIElements);
                case CustomThemeTemplate.Simple:
                    return contents;
                default:
                    Debug.Assert(false);
                    return contents;
            }
        }
    }
}
