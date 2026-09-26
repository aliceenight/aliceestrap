using Wpf.Ui.Controls;

namespace Aliceestrap.Models.APIs.Config
{
    public class RemoteDataBase
    {
        [JsonPropertyName("deeplinkUrl")]
        public string DeeplinkUrl { get; set; } = "roblox://experiences/start";

        [JsonPropertyName("packageMaps")]
        public PackageMaps PackageMaps { get; set; } = new();

        [JsonPropertyName("ignoredPackages")]
        public List<string> IgnoredPackages { get; set; } = new()
        {
            "RobloxPlayerInstaller.exe"
        };
    }
}