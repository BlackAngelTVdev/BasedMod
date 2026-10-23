using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ItemAdderLauncher.Core;

public static class GameLocator
{
    public const string GameFolderName = "Supermarket Together";
    public const string GameExecutableName = "Supermarket Together.exe";

    private static readonly Regex LibraryPathPattern = new(
        "\"path\"\\s+\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? FindGamePath(string? preferredPath = null)
    {
        string? normalized = NormalizePath(preferredPath);
        if (IsValidGamePath(normalized))
        {
            return normalized;
        }

        foreach (string library in GetSteamLibraries())
        {
            string candidate = Path.Combine(library, "steamapps", "common", GameFolderName);
            if (IsValidGamePath(candidate))
            {
                return NormalizePath(candidate);
            }
        }

        foreach (string candidate in GetFallbackPaths())
        {
            if (IsValidGamePath(candidate))
            {
                return NormalizePath(candidate);
            }
        }

        return null;
    }

    public static bool IsValidGamePath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return false;
        }

        try
        {
            return File.Exists(Path.Combine(gamePath, GameExecutableName));
        }
        catch
        {
            return false;
        }
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string clean = path.Trim().Trim('"');
            string full = Path.GetFullPath(clean);

            // Conserve la racine ("C:\") mais retire les séparateurs de fin.
            return full.Length > 3
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }
        catch
        {
            return null;
        }
    }

    public static string GetPluginsPath(string gamePath)
    {
        return Path.Combine(gamePath, "BepInEx", "plugins");
    }

    public static string GetGameExecutablePath(string gamePath)
    {
        return Path.Combine(gamePath, GameExecutableName);
    }

    public static bool IsGameRunning()
    {
        Process[] processes = Process.GetProcessesByName(GameFolderName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    public static void LaunchGame(string gamePath)
    {
        string executable = GetGameExecutablePath(gamePath);
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                "Supermarket Together.exe est introuvable dans le dossier sélectionné.",
                executable);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = gamePath,
            UseShellExecute = true
        });
    }

    private static IEnumerable<string> GetSteamLibraries()
    {
        var libraries = new List<string>();

        foreach (string root in GetSteamRoots())
        {
            AddUnique(libraries, root);

            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try
            {
                if (!File.Exists(vdf))
                {
                    continue;
                }

                string content = File.ReadAllText(vdf);
                foreach (Match match in LibraryPathPattern.Matches(content))
                {
                    string path = match.Groups[1].Value
                        .Replace("\\\\", "\\")
                        .Replace("/", "\\");
                    AddUnique(libraries, path);
                }
            }
            catch
            {
                // Un VDF illisible ne doit pas empêcher la sélection manuelle.
            }
        }

        return libraries;
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var roots = new List<string>();
        string[] registryKeys =
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
        };

        foreach (string key in registryKeys)
        {
            try
            {
                object? value = Registry.GetValue(key, "SteamPath", null)
                    ?? Registry.GetValue(key, "InstallPath", null);

                if (value is string steamPath)
                {
                    AddUnique(roots, steamPath.Replace("/", "\\"));
                }
            }
            catch
            {
                // Steam n'est pas forcément installé pour cet utilisateur.
            }
        }

        string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            AddUnique(roots, Path.Combine(programFilesX86, "Steam"));
        }

        return roots;
    }

    private static IEnumerable<string> GetFallbackPaths()
    {
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(programFilesX86, "Steam", "steamapps", "common", GameFolderName);

        foreach (string drive in new[] { "C:", "D:", "E:", "F:", "G:" })
        {
            foreach (string library in new[] { "SteamLibrary", "Steam" })
            {
                yield return Path.Combine(drive + "\\", library, "steamapps", "common", GameFolderName);
            }
        }
    }

    private static void AddUnique(ICollection<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string normalized = value.Trim().Trim('"');
        if (normalized.Length == 0)
        {
            return;
        }

        // Ne pas transformer une racine ("C:\") en chemin relatif ("C:").
        if (normalized.Length > 3)
        {
            normalized = normalized.TrimEnd('\\', '/');
        }

        if (!values.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            values.Add(normalized);
        }
    }
}
