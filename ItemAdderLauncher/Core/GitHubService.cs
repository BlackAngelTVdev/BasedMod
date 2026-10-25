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

    public const string ReleasesApiUrl =
        "https://api.github.com/repos/BlackAngelTVdev/BasedMod/releases";

    private readonly HttpClient _httpClient;

    public GitHubService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        return await GetSingleReleaseAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Dernières releases publiées (brouillons exclus), de la plus récente à la plus ancienne.
    /// Alimente la page « Actualités » du launcher.
    /// </summary>
    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        int count = 6,
        CancellationToken cancellationToken = default)
    {
        int bounded = Math.Clamp(count, 1, 20);
        string url = $"{ReleasesApiUrl}?per_page={bounded}";

        using HttpResponseMessage response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        EnsureSuccess(response);

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var releases = new List<ReleaseInfo>();
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            releases.Add(ParseRelease(item));
        }

        return releases;
    }

    private async Task<ReleaseInfo?> GetSingleReleaseAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string detail = response.StatusCode switch
        {
            HttpStatusCode.NotFound => "dépôt ou release introuvable",
            HttpStatusCode.Forbidden => "limite de requêtes GitHub atteinte",
            HttpStatusCode.Unauthorized => "accès GitHub refusé",
            _ => $"réponse HTTP {(int)response.StatusCode}"
        };

        throw new InvalidOperationException($"GitHub est indisponible ({detail}).");
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

        string author = string.Empty;
        if (root.TryGetProperty("author", out JsonElement authorElement))
        {
            author = GetString(authorElement, "login") ?? string.Empty;
        }

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out JsonElement publishedElement)
            && publishedElement.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                publishedElement.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsed))
        {
            publishedAt = parsed;
        }

        bool prerelease = root.TryGetProperty("prerelease", out JsonElement preElement)
            && preElement.ValueKind == JsonValueKind.True;

        return new ReleaseInfo
        {
            TagName = displayName,
            DisplayName = string.IsNullOrWhiteSpace(name) ? displayName : name,
            Assets = assets,
            Body = GetString(root, "body") ?? string.Empty,
            Author = author,
            HtmlUrl = GetString(root, "html_url") ?? string.Empty,
            PublishedAt = publishedAt,
            Prerelease = prerelease
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
