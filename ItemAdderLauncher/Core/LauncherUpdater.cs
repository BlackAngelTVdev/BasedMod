using System.Diagnostics;

namespace ItemAdderLauncher.Core;

/// <summary>
/// Met à jour le launcher depuis l'asset ItemAdderLauncher*.exe de la release.
/// La version est lue dans le nom de l'asset (ex. ItemAdderLauncher-v1.4.0.exe),
/// ce qui évite de télécharger 160 Mo juste pour comparer.
/// </summary>
public static class LauncherUpdater
{
    public const string AssetPrefix = "ItemAdderLauncher";

    public static Version CurrentVersion
    {
        get
        {
            Version? version = typeof(LauncherUpdater).Assembly.GetName().Version;
            return Normalize(version ?? new Version(0, 0, 0, 0));
        }
    }

    public static string CurrentExecutablePath
    {
        get
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            path = Process.GetCurrentProcess().MainModule?.FileName;
            return string.IsNullOrWhiteSpace(path)
                ? Path.Combine(AppContext.BaseDirectory, AssetPrefix + ".exe")
                : path;
        }
    }

    public static Version? GetAssetVersion(ReleaseAsset asset)
    {
        return VersionHelper.Parse(asset.Name);
    }

    public static Version? ReadExecutableVersion(string exePath)
    {
        try
        {
            string? raw = FileVersionInfo.GetVersionInfo(exePath).FileVersion;
            return VersionHelper.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    public static bool CanReplaceExecutable(out string reason)
    {
        string directory = Path.GetDirectoryName(CurrentExecutablePath) ?? string.Empty;
        if (directory.Length == 0)
        {
            reason = "Le dossier du launcher est introuvable.";
            return false;
        }

        string probe = Path.Combine(directory, $".itemadder-write-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason =
                "Impossible d'écrire dans le dossier du launcher " +
                $"({directory}). Déplace l'exécutable dans un dossier utilisateur " +
                $"ou relance-le en administrateur.\n\n{ex.Message}";
            return false;
        }
    }

    public static async Task<string> DownloadAsync(
        ReleaseAsset asset,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string directory = CreateUpdateDirectory();
        string destination = Path.Combine(directory, AssetPrefix + ".exe");

        progress?.Report($"Téléchargement du launcher {asset.Name}...");
        await ModService
            .DownloadFileAsync(asset.DownloadUrl, destination, progress, cancellationToken)
            .ConfigureAwait(false);

        return destination;
    }

    /// <summary>
    /// Lance un script qui attend la fermeture du launcher, remplace l'exécutable
    /// par la version téléchargée puis redémarre. Le launcher doit être fermé
    /// juste après cet appel.
    /// </summary>
    public static void ScheduleReplaceAndRestart(string downloadedExe)
    {
        ScheduleReplaceAndRestart(CurrentExecutablePath, downloadedExe, Environment.ProcessId);
    }

    public static void ScheduleReplaceAndRestart(
        string currentExe,
        string downloadedExe,
        int processId)
    {
        string downloadedDirectory = Path.GetDirectoryName(downloadedExe) ?? Path.GetTempPath();
        string scriptPath = Path.Combine(
            CreateUpdateDirectory(),
            $"apply-{Guid.NewGuid():N}.cmd");

        string[] lines =
        {
            "@echo off",
            "setlocal enabledelayedexpansion",
            "set tries=0",
            ":wait",
            $"tasklist /FI \"PID eq {processId}\" | find \"{processId}\" >nul",
            "if not errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto wait",
            ")",
            ":copy",
            $"copy /y \"{downloadedExe}\" \"{currentExe}\" >nul",
            "if not errorlevel 1 goto restart",
            "set /a tries+=1",
            "if !tries! geq 60 goto end",
            "timeout /t 1 /nobreak >nul",
            "goto copy",
            ":restart",
            $"start \"\" \"{currentExe}\"",
            ":end",
            $"rmdir /s /q \"{downloadedDirectory}\"",
            "del \"%~f0\"",
            string.Empty
        };

        File.WriteAllLines(scriptPath, lines);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        });
    }

    private static string CreateUpdateDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ItemAdderLauncher",
            "update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }
}
