using ItemAdderLauncher.Core;

namespace ItemAdderLauncher;

internal static class Diagnostics
{
    public static int RunDiagnose()
    {
        var lines = new List<string>();
        int exitCode = 0;

        try
        {
            string? gamePath = GameLocator.FindGamePath();
            lines.Add($"Jeu : {gamePath ?? "non détecté"}");
            lines.Add($"Exe valide : {GameLocator.IsValidGamePath(gamePath)}");

            InstalledMod? installed = ModService.GetInstalledMod(gamePath);
            lines.Add(installed is null
                ? "Mod installé : aucun"
                : $"Mod installé : {installed.FileName} {installed.Version}");

            string core = gamePath is null
                ? "BepInEx : jeu non détecté"
                : $"BepInEx : {File.Exists(Path.Combine(gamePath, "BepInEx", "core", "BepInEx.dll"))}";
            lines.Add(core);

            try
            {
                ReleaseInfo? release = new GitHubService()
                    .GetLatestReleaseAsync()
                    .GetAwaiter()
                    .GetResult();
                lines.Add(release is null
                    ? "Dernière release : aucune"
                    : $"Dernière release : {release.TagName}");
            }
            catch (Exception ex)
            {
                lines.Add($"Dernière release : erreur ({ex.Message})");
                exitCode = 2;
            }
        }
        catch (Exception ex)
        {
            lines.Add($"ERREUR : {ex}");
            exitCode = 1;
        }

        return WriteResult("launcher-diagnose.txt", lines, exitCode);
    }

    public static int RunBepInExTest()
    {
        var lines = new List<string>();
        int exitCode = 0;
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ItemAdderLauncherBepInExTest",
            Guid.NewGuid().ToString("N"));

        try
        {
            string gamePath = Path.Combine(tempRoot, "Supermarket Together");
            Directory.CreateDirectory(gamePath);
            File.WriteAllText(GameLocator.GetGameExecutablePath(gamePath), "fake");

            ModService
                .EnsureBepInExAsync(
                    gamePath,
                    cancellationToken: CancellationToken.None,
                    allowGameRunning: true)
                .GetAwaiter()
                .GetResult();

            string core = Path.Combine(gamePath, "BepInEx", "core", "BepInEx.dll");
            bool coreExists = File.Exists(core);
            bool pluginsExists = Directory.Exists(GameLocator.GetPluginsPath(gamePath));

            lines.Add($"BepInEx.dll : {coreExists}");
            lines.Add($"Dossier plugins : {pluginsExists}");

            if (!coreExists || !pluginsExists)
            {
                exitCode = 1;
            }
        }
        catch (Exception ex)
        {
            lines.Add($"ERREUR : {ex}");
            exitCode = 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Nettoyage best-effort.
            }
        }

        return WriteResult("launcher-bepinextest.txt", lines, exitCode);
    }

    public static int RunSelfTest(string[] args)
    {
        var lines = new List<string>();
        int exitCode = 0;
        string? sourceDll = args
            .SkipWhile(arg => !string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(sourceDll))
        {
            sourceDll = Path.Combine(
                Environment.CurrentDirectory,
                "ItemAdder",
                "bin",
                "Release",
                "ItemAdder.dll");
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ItemAdderLauncherSelfTest",
            Guid.NewGuid().ToString("N"));

        try
        {
            if (!File.Exists(sourceDll))
            {
                throw new FileNotFoundException("DLL source introuvable.", sourceDll);
            }

            string gamePath = Path.Combine(tempRoot, "Supermarket Together");
            string plugins = GameLocator.GetPluginsPath(gamePath);
            Directory.CreateDirectory(plugins);
            File.WriteAllText(GameLocator.GetGameExecutablePath(gamePath), "fake");
            File.Copy(
                sourceDll,
                Path.Combine(plugins, ModService.LegacyModFileName),
                overwrite: true);

            InstalledMod installed = ModService
                .InstallFromDllAsync(
                    gamePath,
                    sourceDll,
                    cancellationToken: CancellationToken.None,
                    allowGameRunning: true)
                .GetAwaiter()
                .GetResult();

            string target = Path.Combine(plugins, ModService.ModFileName);
            lines.Add($"DLL installé : {File.Exists(target)}");
            lines.Add($"Ancien DLL supprimé : {!File.Exists(Path.Combine(plugins, ModService.LegacyModFileName))}");
            lines.Add($"Fichier retourné : {installed.FileName}");
            lines.Add($"Version retournée : {installed.Version}");

            Version? tagVersion = VersionHelper.Parse("v1.1.0");
            Version? fileVersion = VersionHelper.Parse("1.1.0.0");
            bool versionsMatch = tagVersion is not null
                && fileVersion is not null
                && tagVersion == fileVersion;
            lines.Add($"Comparaison des versions : {versionsMatch}");

            var fakeRelease = new ReleaseInfo
            {
                TagName = "v9.9.9",
                DisplayName = "test",
                Assets = new[]
                {
                    new ReleaseAsset { Name = "notes.txt", DownloadUrl = "https://example.invalid/notes.txt" },
                    new ReleaseAsset { Name = "ItemAdder.dll", DownloadUrl = "https://example.invalid/ItemAdder.dll" },
                    new ReleaseAsset { Name = "mod.zip", DownloadUrl = "https://example.invalid/mod.zip" }
                }
            };
            bool selectsDll = string.Equals(
                fakeRelease.SelectModAsset()?.Name,
                "ItemAdder.dll",
                StringComparison.OrdinalIgnoreCase);
            lines.Add($"Sélection de l'asset DLL : {selectsDll}");

            if (!File.Exists(target)
                || File.Exists(Path.Combine(plugins, ModService.LegacyModFileName))
                || !string.Equals(installed.FileName, ModService.ModFileName, StringComparison.OrdinalIgnoreCase)
                || !versionsMatch
                || !selectsDll)
            {
                exitCode = 1;
            }
        }
        catch (Exception ex)
        {
            lines.Add($"ERREUR : {ex}");
            exitCode = 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Nettoyage best-effort.
            }
        }

        return WriteResult("launcher-selftest.txt", lines, exitCode);
    }

    private static int WriteResult(string fileName, IEnumerable<string> lines, int exitCode)
    {
        string outputPath = Path.Combine(Environment.CurrentDirectory, fileName);
        string content = string.Join(Environment.NewLine, lines) + Environment.NewLine;

        try
        {
            File.WriteAllText(outputPath, content);
        }
        catch
        {
            // Le résultat reste visible dans la console si le fichier ne peut pas être écrit.
        }

        Console.WriteLine(content);
        return exitCode;
    }
}
