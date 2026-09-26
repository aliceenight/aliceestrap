namespace Aliceestrap.Models.APIs.GitHub
{
    public class GithubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("assets")]
        public List<GithubReleaseAsset>? Assets { get; set; }
    }
}
