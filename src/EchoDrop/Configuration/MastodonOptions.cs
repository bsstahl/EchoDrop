namespace EchoDrop.Configuration;

public sealed class MastodonOptions
{
    public const string SectionName = "Mastodon";

    public string BaseUrl { get; set; } = "https://mastodon.social";

    public string AccessToken { get; set; } = string.Empty;
}
