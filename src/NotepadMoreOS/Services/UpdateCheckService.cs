using System.Net.Http.Headers;
using System.Text.Json;

namespace NotepadMoreOS.Services;

public sealed class UpdateCheckService
{
    private static readonly HttpClient Client = CreateClient();
    private const string ReleasesApi = "https://api.github.com/repos/Agarni/NotepadMoreOS/releases/latest";

    public async Task<Version> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(ReleasesApi, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String ||
            !TryParseVersion(tag.GetString(), out var version))
            throw new InvalidDataException("Invalid release version.");
        return version;
    }

    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (tag.StartsWith('v')) tag = tag[1..];
        if (!Version.TryParse(tag, out var parsed) || parsed.Build < 0 || parsed.Revision >= 0) return false;
        version = parsed;
        return true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NotepadMoreOS", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
