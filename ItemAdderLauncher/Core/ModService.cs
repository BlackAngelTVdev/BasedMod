using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace ItemAdderLauncher.Core;

public static class ModService
{
    public const string ModFileName = "ItemAdder.dll";
    public const string LegacyModFileName = "MonSupermarketMod.dll";
    public const string BepInExVersion = "5.4.23.5";

    private static readonly HttpClient DownloadClient = CreateDownloadClient();

    public static InstalledMod? GetInstalledMod(string? gamePath)
    {
        if (!GameLocator.IsValidGamePath(gamePath) || gamePath is null)
        {
            return null;
        }

        string plugins = GameLocator.GetPluginsPath(gamePath);
        foreach (string fileName in new[] { ModFileName, LegacyModFileName })
        {
            string path = Path.Combine(plugins, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            string version = "inconnue";
            Version? parsed = null;

            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string? fileVersion = info.FileVersion;
                if (string.IsNullOrWhiteSpace(fileVersion))
                {
                    fileVersion = info.ProductVersion;
                }

                if (!string.IsNullOrWhiteSpace(fileVersion))
                {
                    version = fileVersion.Trim();
                    parsed = VersionHelper.Parse(version);
                }
            }
            catch
            {
                // Un DLL sans métadonnées reste installable : on affiche "inconnue".
            }

            return new InstalledMod
            {
                FileName = fileName,
                FilePath = path,
                Version = version,
                ParsedVersion = parsed
            };
        }

        return null;
    }

    public static async Task EnsureBepInExAsync(
        string gamePath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowGameRunning = false)
    {
        if (!GameLocator.IsValidGamePath(gamePath))
        {
            throw new InvalidOperationException(
                "Le dossier sélectionné ne contient pas Supermarket Together.exe.");
        }

        string corePath = Path.Combine(gamePath, "BepInEx", "core", "BepInEx.dll");
        if (File.Exists(corePath))
        {
            return;
        }

        if (!allowGameRunning && GameLocator.IsGameRunning())
        {
            throw new InvalidOperationException(
                "Ferme Supermarket Together avant d'installer BepInEx.");
        }

        string zipName = $"BepInEx_win_x64_{BepInExVersion}.zip";
        string url =
            $"https://github.com/BepInEx/BepInEx/releases/download/v{BepInExVersion}/{zipName}";
        string tempZip = Path.Combine(Path.GetTempPath(), "ItemAdderLauncher", zipName);

        Directory.CreateDirectory(Path.GetDirectoryName(tempZip)!);

        try
        {
            progress?.Report($"Téléchargement de BepInEx {BepInExVersion}...");
            await DownloadFileAsync(url, tempZip, progress, cancellationToken).ConfigureAwait(false);

            progress?.Report("Extraction de BepInEx...");
            ZipFile.ExtractToDirectory(tempZip, gamePath, overwriteFiles: true);
        }
        finally
        {
            TryDeleteFile(tempZip);
        }

        Directory.CreateDirectory(GameLocator.GetPluginsPath(gamePath));

        if (!File.Exists(corePath))
        {
            throw new InvalidOperationException(
                "Installation de BepInEx incomplète : BepInEx\\core\\BepInEx.dll est absent.");
        }
    }

    public static async Task<InstalledMod> InstallFromReleaseAsync(
        string gamePath,
        ReleaseInfo release,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ReleaseAsset? asset = release.SelectModAsset();
        if (asset is null)
        {
            throw new InvalidOperationException(
                $"La release {release.TagName} ne contient ni ItemAdder.dll ni archive ZIP.");
        }

        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "ItemAdderLauncher",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDirectory);

        try
        {
            string downloadPath = Path.Combine(
                tempDirectory,
                Path.GetFileName(asset.Name));

            progress?.Report($"Téléchargement de {asset.Name}...");
            await DownloadFileAsync(
                asset.DownloadUrl,
                downloadPath,
                progress,
                cancellationToken).ConfigureAwait(false);

            string dllPath;
            if (asset.IsZip)
            {
                progress?.Report("Extraction de l'archive...");
                string extractDirectory = Path.Combine(tempDirectory, "extract");
                ZipFile.ExtractToDirectory(downloadPath, extractDirectory, overwriteFiles: true);

                string? found = FindModDll(extractDirectory);
                if (found is null)
                {
                    throw new InvalidOperationException(
                        "Aucun DLL ItemAdder trouvé dans l'archive de la release.");
                }

                dllPath = found;
            }
            else
            {
                dllPath = downloadPath;
            }

            return await InstallFromDllAsync(
                gamePath,
                dllPath,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    public static async Task<InstalledMod> InstallFromDllAsync(
        string gamePath,
        string dllPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowGameRunning = false)
    {
        if (!GameLocator.IsValidGamePath(gamePath))
        {
            throw new InvalidOperationException(
                "Le dossier sélectionné ne contient pas Supermarket Together.exe.");
        }

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Le DLL du mod est introuvable.", dllPath);
        }

        if (!allowGameRunning && GameLocator.IsGameRunning())
        {
            throw new InvalidOperationException(
                "Ferme Supermarket Together avant d'installer ou de mettre à jour le mod.");
        }

        progress?.Report("Installation du mod...");

        return await Task.Run(() =>
        {
            string plugins = GameLocator.GetPluginsPath(gamePath);
            Directory.CreateDirectory(plugins);

            string targetPath = Path.Combine(plugins, ModFileName);
            string sourceFullPath = Path.GetFullPath(dllPath);
            string targetFullPath = Path.GetFullPath(targetPath);

            if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                // Copie temporaire puis remplacement : le DLL installé n'est jamais à moitié écrit.
                string temporaryTarget = targetFullPath + ".new";
                try
                {
                    File.Copy(sourceFullPath, temporaryTarget, overwrite: true);
                    File.Move(temporaryTarget, targetFullPath, overwrite: true);
                }
                finally
                {
                    TryDeleteFile(temporaryTarget);
                }
            }

            string legacyPath = Path.Combine(plugins, LegacyModFileName);
            if (File.Exists(legacyPath)
                && !string.Equals(
                    Path.GetFullPath(legacyPath),
                    targetFullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(legacyPath);
            }

            InstalledMod installed = GetInstalledMod(gamePath)
                ?? throw new InvalidOperationException(
                    "Le DLL a été copié mais le mod n'est pas détecté dans BepInEx\\plugins.");

            progress?.Report($"Mod installé : {installed.FileName} ({installed.Version}).");
            return installed;
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await DownloadClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        string sizeText = totalBytes.HasValue ? $" ({FormatSize(totalBytes.Value)})" : string.Empty;
        progress?.Report($"Téléchargement{sizeText}...");

        await using Stream input = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        progress?.Report($"Téléchargement terminé : {FormatSize(output.Length)}.");
    }

    private static string? FindModDll(string directory)
    {
        List<string> dlls = Directory
            .EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .ToList();

        foreach (string preferred in new[] { ModFileName, LegacyModFileName })
        {
            string? match = dlls.FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return dlls.FirstOrDefault(path =>
            Path.GetFileName(Path.GetDirectoryName(path))
                ?.Equals("plugins", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static HttpClient CreateDownloadClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ItemAdderLauncher/1.0");
        return client;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} o";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} Ko";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} Mo";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Un fichier temporaire verrouillé ne doit pas masquer l'erreur réelle.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Idem : nettoyage best-effort.
        }
    }
}
