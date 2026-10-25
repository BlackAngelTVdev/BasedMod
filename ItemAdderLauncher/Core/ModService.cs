using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace ItemAdderLauncher.Core;

public static class ModService
{
    public const string ModFileName = "ItemAdder.dll";
    public const string LegacyModFileName = "MonSupermarketMod.dll";
    public const string BepInExVersion = "5.4.23.5";

    /// <summary>Version de Unity Doorstop livrée avec le zip BepInEx 5.4.23.5.</summary>
    public const string DoorstopVersion = "4.5.0";

    /// <summary>
    /// Fichiers que Doorstop et BepInEx doivent trouver pour que le mod soit
    /// chargé. Vérifier uniquement <c>BepInEx\core\BepInEx.dll</c> laissait passer
    /// une installation partielle (dossiers recréés, DLL manquantes) : le jeu
    /// démarrait alors en vanilla sans le moindre message.
    /// </summary>
    private static readonly string[] BepInExRequiredFiles =
    {
        "winhttp.dll",
        "doorstop_config.ini",
        ".doorstop_version",
        "BepInEx/core/BepInEx.dll",
        "BepInEx/core/BepInEx.Preloader.dll",
        "BepInEx/core/BepInEx.Harmony.dll",
        "BepInEx/core/0Harmony.dll",
        "BepInEx/core/Mono.Cecil.dll"
    };

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

    /// <summary>
    /// Fichiers manquants pour que BepInEx charge réellement le mod. Liste vide
    /// seulement quand l'installation est complète.
    /// </summary>
    public static IReadOnlyList<string> GetMissingBepInExFiles(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return BepInExRequiredFiles.ToList();
        }

        return BepInExRequiredFiles
            .Where(relative => !File.Exists(Path.Combine(gamePath, relative)))
            .ToList();
    }

    public static bool IsBepInExInstalled(string? gamePath)
    {
        return !string.IsNullOrWhiteSpace(gamePath)
            && GetMissingBepInExFiles(gamePath).Count == 0;
    }

    /// <summary>
    /// Vrai quand le dossier BepInEx existe mais qu'il manque des fichiers :
    /// c'est l'état produit par une extraction interrompue.
    /// </summary>
    public static bool IsBepInExPartial(string? gamePath)
    {
        return !string.IsNullOrWhiteSpace(gamePath)
            && Directory.Exists(Path.Combine(gamePath, "BepInEx"))
            && !IsBepInExInstalled(gamePath);
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

        if (!allowGameRunning && GameLocator.IsGameRunning())
        {
            throw new InvalidOperationException(
                "Ferme Supermarket Together avant d'installer BepInEx.");
        }

        // Dossiers utilisés par BepInEx : les créer évite qu'un plugin échoue
        // faute de dossier, mais ce n'est pas l'installation elle-même.
        Directory.CreateDirectory(Path.Combine(gamePath, "BepInEx"));
        Directory.CreateDirectory(Path.Combine(gamePath, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(gamePath, "BepInEx", "config"));
        Directory.CreateDirectory(GameLocator.GetPluginsPath(gamePath));

        IReadOnlyList<string> missing = GetMissingBepInExFiles(gamePath);
        if (missing.Count == 0)
        {
            EnsureDoorstopEnabled(gamePath, progress);
            return;
        }

        progress?.Report(missing.Count == BepInExRequiredFiles.Length
            ? $"BepInEx absent : installation complète de la version {BepInExVersion}."
            : "BepInEx incomplet : réinstallation complète (manquant : " +
              string.Join(", ", missing) + ").");

        // Des DLL de core à moitié écrites obligent à repartir d'un dossier vide,
        // pour ne pas mélanger deux états. Un fichier absent ailleurs (winhttp.dll,
        // doorstop_config.ini...) laisse au contraire le core intact : l'extraction
        // le réécrit, et un téléchargement qui échoue ne détruit rien d'utile.
        bool coreIncomplete = missing.Any(relative =>
            relative.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase));
        string coreDirectory = Path.Combine(gamePath, "BepInEx", "core");
        if (coreIncomplete && Directory.Exists(coreDirectory))
        {
            TryClearDirectory(coreDirectory);
        }

        string zipName = $"BepInEx_win_x64_{BepInExVersion}.zip";
        string url =
            $"https://github.com/BepInEx/BepInEx/releases/download/v{BepInExVersion}/{zipName}";
        string tempZip = Path.Combine(Path.GetTempPath(), "ItemAdderLauncher", zipName);

        Directory.CreateDirectory(Path.GetDirectoryName(tempZip)!);
        TryDeleteFile(tempZip);

        try
        {
            progress?.Report($"Téléchargement de BepInEx {BepInExVersion}...");
            await DownloadFileAsync(url, tempZip, progress, cancellationToken).ConfigureAwait(false);
            EnsureZipArchive(tempZip);

            progress?.Report("Extraction de BepInEx...");
            ExtractZip(tempZip, gamePath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Impossible d'extraire BepInEx dans le dossier du jeu. " +
                "Ferme le jeu et l'antivirus, vérifie que le dossier n'est pas en lecture seule, " +
                "puis relance. Détail : " + ex.Message,
                ex);
        }
        finally
        {
            TryDeleteFile(tempZip);
        }

        missing = GetMissingBepInExFiles(gamePath);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Installation de BepInEx incomplète après extraction. Fichiers absents : " +
                string.Join(", ", missing) + ". " +
                "Un antivirus a peut-être mis winhttp.dll en quarantaine : autorise le dossier " +
                "du jeu puis clique à nouveau sur « Lancer le jeu ».");
        }

        EnsureDoorstopEnabled(gamePath, progress);
        progress?.Report($"BepInEx {BepInExVersion} installé : {BepInExRequiredFiles.Length} fichiers vérifiés.");
    }

    /// <summary>
    /// Doorstop doit rester actif : un doorstop_config.ini désactivé (ou absent)
    /// démarre le jeu en vanilla même quand toutes les DLL sont en place.
    /// </summary>
    private static void EnsureDoorstopEnabled(string gamePath, IProgress<string>? progress)
    {
        string configPath = Path.Combine(gamePath, "doorstop_config.ini");

        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            string text = File.ReadAllText(configPath);
            string fixedText = System.Text.RegularExpressions.Regex.Replace(
                text,
                "(?im)^\\s*enabled\\s*=\\s*false\\s*$",
                "enabled = true");

            if (!string.Equals(text, fixedText, StringComparison.Ordinal))
            {
                File.WriteAllText(configPath, fixedText);
                progress?.Report("Doorstop était désactivé dans doorstop_config.ini : réactivé.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            progress?.Report("Attention : doorstop_config.ini n'a pas pu être vérifié (" +
                ex.Message + ").");
        }
    }

    private static void EnsureZipArchive(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1024)
        {
            throw new InvalidDataException(
                $"Le téléchargement de BepInEx est incomplet ({info.Length} octets). " +
                "Vérifie la connexion puis relance.");
        }

        using FileStream stream = File.OpenRead(path);
        if (stream.ReadByte() != 'P' || stream.ReadByte() != 'K')
        {
            throw new InvalidDataException(
                "Le fichier téléchargé n'est pas une archive ZIP (GitHub a peut-être " +
                "renvoyé une page d'erreur). Réessaie plus tard.");
        }
    }

    private static void ExtractZip(string archivePath, string destinationRoot)
    {
        string root = Path.GetFullPath(destinationRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                // Entrée hors du dossier du jeu : ignorée (archive piégée).
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static void TryClearDirectory(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fichiers verrouillés (jeu ouvert) : l'extraction signalera l'erreur.
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
