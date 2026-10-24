namespace ItemAdderLauncher.Core;

/// <summary>
/// Lit et écrit les options du mod dans le fichier .cfg généré par BepInEx,
/// pour que le launcher puisse choisir les modules actifs avant le lancement.
/// </summary>
public static class ModConfig
{
    public const string FileName = "com.username.supermarkettogethermod.cfg";

    private const string TobaccoSection = "Tabac";
    private const string UndergroundSection = "Souterrain";
    private const string RestockSection = "Employés";
    private const string TobaccoKey = "ActiverRayonTabac";
    private const string UndergroundKey = "ActiverSousSol";
    private const string RestockKey = "ReassortIntelligent";

    public static string GetConfigPath(string gamePath)
    {
        return Path.Combine(gamePath, "BepInEx", "config", FileName);
    }

    public static (bool Tobacco, bool Underground, bool Restock) Read(string gamePath)
    {
        string path = GetConfigPath(gamePath);
        if (!File.Exists(path))
        {
            return (true, true, true);
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            return (
                ReadBool(lines, TobaccoSection, TobaccoKey, true),
                ReadBool(lines, UndergroundSection, UndergroundKey, true),
                ReadBool(lines, RestockSection, RestockKey, true));
        }
        catch
        {
            // Un fichier illisible ne doit pas bloquer le launcher.
            return (true, true, true);
        }
    }

    public static void Apply(string gamePath, bool tobacco, bool underground, bool restock)
    {
        string path = GetConfigPath(gamePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var lines = File.Exists(path)
            ? new List<string>(File.ReadAllLines(path))
            : new List<string>();

        SetValue(lines, TobaccoSection, TobaccoKey, tobacco);
        SetValue(lines, UndergroundSection, UndergroundKey, underground);
        SetValue(lines, RestockSection, RestockKey, restock);

        File.WriteAllLines(path, lines);
    }

    private static bool ReadBool(
        IReadOnlyList<string> lines,
        string section,
        string key,
        bool fallback)
    {
        int start = IndexOfSection(lines, section);
        if (start < 0)
        {
            return fallback;
        }

        for (int i = start + 1; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            if (TryParseKey(line, out string lineKey) &&
                string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
            {
                string raw = line[(line.IndexOf('=') + 1)..].Trim();
                return bool.TryParse(raw, out bool parsed) ? parsed : fallback;
            }
        }

        return fallback;
    }

    private static void SetValue(List<string> lines, string section, string key, bool value)
    {
        string formatted = $"{key} = {(value ? "true" : "false")}";
        int sectionStart = IndexOfSection(lines, section);

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length != 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{section}]");
            lines.Add(string.Empty);
            lines.Add(formatted);
            return;
        }

        int sectionEnd = lines.Count;
        for (int i = sectionStart + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                sectionEnd = i;
                break;
            }
        }

        for (int i = sectionStart + 1; i < sectionEnd; i++)
        {
            if (TryParseKey(lines[i], out string lineKey) &&
                string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = formatted;
                return;
            }
        }

        // Clé absente : on l'ajoute à la fin du bloc, avant les lignes vides
        // qui précèdent la section suivante.
        int insertAt = sectionEnd;
        while (insertAt > sectionStart + 1 && lines[insertAt - 1].Trim().Length == 0)
        {
            insertAt--;
        }

        lines.Insert(insertAt, formatted);
    }

    private static int IndexOfSection(IReadOnlyList<string> lines, string section)
    {
        string header = $"[{section}]";
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseKey(string line, out string key)
    {
        key = string.Empty;
        string trimmed = line.TrimStart();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith("#", StringComparison.Ordinal) ||
            trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        int separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = trimmed[..separator].Trim();
        return key.Length > 0;
    }
}
