namespace EchoDrop.Publisher.Mastodon.Configuration;

public sealed class MastodonOptions
{
    public const string SectionName = "Mastodon";

    public Uri BaseUrl { get; set; } = new("https://mastodon.social", UriKind.Absolute);

    public string AccessToken { get; set; } = string.Empty;
}
