using System.Net;
using System.Text.Json;

namespace ItemAdderLauncher.Core;

public sealed class GitHubService
{
    public const string RepositoryOwner = "BlackAngelTVdev";
    public const string RepositoryName = "BasedMod";
    public const string RepositoryUrl = "https://github.com/BlackAngelTVdev/BasedMod";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/BlackAngelTVdev/BasedMod/releases/latest";

    private readonly HttpClient _httpClient;

    public GitHubService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync(LatestReleaseApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            string detail = response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "limite de requêtes GitHub atteinte",
                HttpStatusCode.Unauthorized => "accès GitHub refusé",
                _ => $"réponse HTTP {(int)response.StatusCode}"
            };

            throw new InvalidOperationException($"GitHub est indisponible ({detail}).");
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ParseRelease(document.RootElement);
    }

    private static ReleaseInfo ParseRelease(JsonElement root)
    {
        string? tag = GetString(root, "tag_name");
        string? name = GetString(root, "name");
        string displayName = string.IsNullOrWhiteSpace(tag)
            ? (string.IsNullOrWhiteSpace(name) ? "release inconnue" : name)
            : tag;

        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out JsonElement assetsElement)
            && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in assetsElement.EnumerateArray())
            {
                string? assetName = GetString(item, "name");
                string? downloadUrl = GetString(item, "browser_download_url");

                if (!string.IsNullOrWhiteSpace(assetName)
                    && !string.IsNullOrWhiteSpace(downloadUrl))
                {
                    assets.Add(new ReleaseAsset
                    {
                        Name = assetName,
                        DownloadUrl = downloadUrl
                    });
                }
            }
        }

        return new ReleaseInfo
        {
            TagName = displayName,
            DisplayName = string.IsNullOrWhiteSpace(name) ? displayName : name,
            Assets = assets
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("ItemAdderLauncher/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
