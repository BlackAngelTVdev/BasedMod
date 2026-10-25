using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ItemAdderLauncher.Core;

/// <summary>
/// Installe le launcher dans le profil de l'utilisateur, sans droit administrateur :
/// copie de l'exécutable, raccourcis sur le Bureau et dans le menu Démarrer, entrée
/// dans « Applications installées ». Les mises à jour remplacent ensuite l'exécutable
/// installé : les raccourcis continuent de pointer dessus, rien à recréer.
/// </summary>
public static class LauncherInstaller
{
    public const string ProductName = "ItemAdder Launcher";
    public const string ExecutableName = "ItemAdderLauncher.exe";
    public const string ShortcutFileName = "ItemAdder Launcher.lnk";
    public const string UninstallKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ItemAdderLauncher";
    public const string SettingsKeyPath = @"Software\ItemAdderLauncher";

    private const string Publisher = "BlackAngelTVdev";
    private const string ProjectUrl = "https://github.com/BlackAngelTVdev/BasedMod";

    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "ItemAdder");

    public static string InstalledExecutablePath => Path.Combine(InstallDirectory, ExecutableName);

    public static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        ShortcutFileName);

    public static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        "ItemAdder",
        ShortcutFileName);

    public static bool IsInstalled => File.Exists(InstalledExecutablePath);

    public static bool IsRunningInstalledCopy
    {
        get
        {
            string? path = Environment.ProcessPath;
            return path is not null && SamePath(path, InstalledExecutablePath);
        }
    }

    /// <summary>
    /// Cible d'une mise à jour du launcher : l'exécutable installé quand il existe,
    /// sinon la copie en cours d'exécution.
    /// </summary>
    public static string UpdateTargetPath =>
        IsInstalled ? InstalledExecutablePath : LauncherUpdater.CurrentExecutablePath;

    public static void Install(IProgress<string>? progress = null)
    {
        string source = LauncherUpdater.CurrentExecutablePath;
        string target = InstalledExecutablePath;

        progress?.Report($"Dossier d'installation : {InstallDirectory}");
        Directory.CreateDirectory(InstallDirectory);

        if (SamePath(source, target))
        {
            progress?.Report("Le launcher tourne déjà depuis le dossier d'installation.");
        }
        else
        {
            progress?.Report($"Copie de {Path.GetFileName(source)} vers l'installation...");
            CopyWithRetry(source, target);
        }

        if (CopyCompanionFiles(source, progress))
        {
            progress?.Report(
                "Ce build multi-fichiers a besoin du .NET Desktop Runtime sur le PC " +
                "(l'exécutable publié en single-file n'en a pas besoin).");
        }

        CreateShortcut(DesktopShortcutPath, target, InstallDirectory);
        progress?.Report($"Raccourci créé sur le Bureau : {DesktopShortcutPath}");

        CreateShortcut(StartMenuShortcutPath, target, InstallDirectory);
        progress?.Report($"Raccourci créé dans le menu Démarrer : {StartMenuShortcutPath}");

        WriteRegistration();
        progress?.Report(
            "Entrée créée dans « Applications installées » (désinstallation depuis Windows " +
            "ou depuis ce launcher).");
    }

    public static void Uninstall(IProgress<string>? progress = null)
    {
        DeleteShortcut(DesktopShortcutPath, progress);
        DeleteShortcut(StartMenuShortcutPath, progress);

        string startMenuFolder = Path.GetDirectoryName(StartMenuShortcutPath) ?? string.Empty;
        try
        {
            if (startMenuFolder.Length > 0
                && Directory.Exists(startMenuFolder)
                && !Directory.EnumerateFileSystemEntries(startMenuFolder).Any())
            {
                Directory.Delete(startMenuFolder);
            }
        }
        catch
        {
            // Un dossier de raccourcis vide restant n'empêche pas la désinstallation.
        }

        DeleteRegistryKey(UninstallKeyPath);
        DeleteRegistryKey(SettingsKeyPath);
        progress?.Report("Entrée « Applications installées » supprimée.");

        if (!Directory.Exists(InstallDirectory))
        {
            progress?.Report("Le dossier d'installation est déjà absent.");
            return;
        }

        if (IsRunningInstalledCopy)
        {
            progress?.Report("Suppression du dossier d'installation à la fermeture du launcher...");
            ScheduleDeleteDirectory(InstallDirectory, Environment.ProcessId);
            return;
        }

        DeleteDirectoryBestEffort(InstallDirectory, progress);
    }

    /// <summary>
    /// Réaligne la version affichée dans « Applications installées » sur l'exécutable
    /// réellement présent (par exemple après une mise à jour automatique).
    /// </summary>
    public static void RefreshRegistration()
    {
        try
        {
            if (IsInstalled && IsRunningInstalledCopy)
            {
                WriteRegistration();
            }
        }
        catch
        {
            // Un registre inaccessible ne doit pas empêcher le launcher de démarrer.
        }
    }

    /// <summary>
    /// Copie les fichiers sans lesquels l'exécutable copié ne démarrerait pas :
    /// un build classique (bin\...) a besoin de son .dll, de son .deps.json et de son
    /// .runtimeconfig.json à côté de l'exe. Un exe publié en single-file n'en a pas,
    /// et les restes d'un ancien build multi-fichiers sont alors retirés.
    /// </summary>
    private static bool CopyCompanionFiles(
        string sourceExecutable,
        IProgress<string>? progress)
    {
        string sourceDirectory = Path.GetDirectoryName(sourceExecutable) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(sourceExecutable);
        bool copied = false;

        foreach (string suffix in new[] { ".dll", ".deps.json", ".runtimeconfig.json" })
        {
            string sourceFile = Path.Combine(sourceDirectory, stem + suffix);
            string targetFile = Path.Combine(InstallDirectory, stem + suffix);

            if (File.Exists(sourceFile))
            {
                if (!SamePath(sourceFile, targetFile))
                {
                    CopyWithRetry(sourceFile, targetFile);
                    progress?.Report($"Fichier d'exécution copié : {Path.GetFileName(sourceFile)}");
                    copied = true;
                }

                continue;
            }

            if (File.Exists(targetFile))
            {
                try
                {
                    File.Delete(targetFile);
                    progress?.Report(
                        $"Ancien fichier d'exécution retiré : {Path.GetFileName(targetFile)}");
                }
                catch (Exception ex)
                {
                    progress?.Report(
                        $"Fichier d'exécution non supprimé ({Path.GetFileName(targetFile)}) : {ex.Message}");
                }
            }
        }

        return copied;
    }

    private static void WriteRegistration()
    {
        Version version = LauncherUpdater.CurrentVersion;
        long sizeInKb = 0;
        try
        {
            sizeInKb = new FileInfo(InstalledExecutablePath).Length / 1024;
        }
        catch
        {
            // La taille affichée est indicative.
        }

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, writable: true))
        {
            key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
            key.SetValue(
                "DisplayVersion",
                $"{version.Major}.{version.Minor}.{version.Build}",
                RegistryValueKind.String);
            key.SetValue("Publisher", Publisher, RegistryValueKind.String);
            key.SetValue("DisplayIcon", InstalledExecutablePath + ",0", RegistryValueKind.String);
            key.SetValue("InstallLocation", InstallDirectory, RegistryValueKind.String);
            key.SetValue(
                "UninstallString",
                $"\"{InstalledExecutablePath}\" --uninstall",
                RegistryValueKind.String);
            key.SetValue(
                "QuietUninstallString",
                $"\"{InstalledExecutablePath}\" --uninstall --silent",
                RegistryValueKind.String);
            key.SetValue("URLInfoAbout", ProjectUrl, RegistryValueKind.String);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            if (sizeInKb > 0)
            {
                key.SetValue(
                    "EstimatedSize",
                    (int)Math.Min(sizeInKb, int.MaxValue),
                    RegistryValueKind.DWord);
            }
        }

        using RegistryKey settingsKey = Registry.CurrentUser.CreateSubKey(
            SettingsKeyPath,
            writable: true);
        settingsKey.SetValue("InstallPath", InstallDirectory, RegistryValueKind.String);
        settingsKey.SetValue(
            "Version",
            $"{version.Major}.{version.Minor}.{version.Build}",
            RegistryValueKind.String);
    }

    private static void CreateShortcut(string shortcutPath, string target, string workingDirectory)
    {
        string directory = Path.GetDirectoryName(shortcutPath)
            ?? throw new InvalidOperationException(
                $"Dossier du raccourci introuvable : {shortcutPath}");
        Directory.CreateDirectory(directory);

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            throw new InvalidOperationException(
                "Le service de raccourcis de Windows (WScript.Shell) est indisponible, " +
                "le raccourci n'a pas pu être créé.");
        }

        object? shell = null;
        object? link = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                throw new InvalidOperationException("WScript.Shell n'a pas pu être démarré.");
            }

            dynamic shellObject = shell;
            link = shellObject.CreateShortcut(shortcutPath);
            dynamic linkObject = link;
            linkObject.TargetPath = target;
            linkObject.WorkingDirectory = workingDirectory;
            linkObject.Description = ProductName + " — mods Supermarket Together";
            linkObject.IconLocation = target + ",0";
            linkObject.WindowStyle = 1;
            linkObject.Save();
        }
        finally
        {
            ReleaseComObject(link);
            ReleaseComObject(shell);
        }
    }

    private static void DeleteShortcut(string shortcutPath, IProgress<string>? progress)
    {
        try
        {
            if (!File.Exists(shortcutPath))
            {
                return;
            }

            File.Delete(shortcutPath);
            progress?.Report($"Raccourci supprimé : {shortcutPath}");
        }
        catch (Exception ex)
        {
            progress?.Report($"Raccourci non supprimé ({shortcutPath}) : {ex.Message}");
        }
    }

    private static void DeleteRegistryKey(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Une clé protégée ne bloque pas le reste de la désinstallation.
        }
    }

    private static void DeleteDirectoryBestEffort(string directory, IProgress<string>? progress)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    progress?.Report("Dossier d'installation supprimé.");
                    return;
                }

                Directory.Delete(directory, recursive: true);
                progress?.Report($"Dossier d'installation supprimé : {directory}");
                return;
            }
            catch (Exception ex)
            {
                if (attempt == 4)
                {
                    progress?.Report($"Dossier d'installation non supprimé : {ex.Message}");
                    return;
                }

                Thread.Sleep(400);
            }
        }
    }

    private static void CopyWithRetry(string source, string destination)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(400);
            }
        }

        throw new InvalidOperationException(
            $"Impossible de copier le launcher dans {destination}. {lastError?.Message}",
            lastError);
    }

    /// <summary>
    /// Lance un script qui attend la fin du processus puis supprime le dossier
    /// d'installation : impossible de supprimer l'exécutable en cours d'exécution.
    /// </summary>
    private static void ScheduleDeleteDirectory(string directory, int processId)
    {
        string scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"itemadder-uninstall-{Guid.NewGuid():N}.cmd");

        string[] lines =
        {
            "@echo off",
            "setlocal enabledelayedexpansion",
            "set tries=0",
            ":wait",
            $"tasklist /FI \"PID eq {processId}\" | find \"{processId}\" >nul",
            "if not errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  set /a tries+=1",
            "  if !tries! lss 30 goto wait",
            ")",
            "set tries=0",
            ":remove",
            $"rmdir /s /q \"{directory}\" >nul 2>&1",
            $"if exist \"{directory}\" (",
            "  timeout /t 1 /nobreak >nul",
            "  set /a tries+=1",
            "  if !tries! lss 60 goto remove",
            ")",
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

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            try
            {
                Marshal.FinalReleaseComObject(instance);
            }
            catch
            {
                // Libération best effort.
            }
        }
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd('\\'),
                Path.GetFullPath(right).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
