using System.Text.Json;

namespace ItemAdderLauncher.Core;

public sealed class LauncherSettings
{
    public string? GamePath { get; set; }

    // null = pas encore choisi : on reprend alors la valeur du fichier .cfg.
    public bool? Tobacco { get; set; }

    public bool? Underground { get; set; }

    public bool? Restock { get; set; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "ItemAdderLauncher", "settings.json");
        }
    }

    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new LauncherSettings();
            }

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions)
                ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public static void SaveGamePath(string gamePath)
    {
        LauncherSettings settings = Load();
        settings.GamePath = gamePath;
        Save(settings);
    }

    public static void SaveModules(bool tobacco, bool underground, bool restock)
    {
        LauncherSettings settings = Load();
        settings.Tobacco = tobacco;
        settings.Underground = underground;
        settings.Restock = restock;
        Save(settings);
    }

    public static void Save(LauncherSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Une préférence non enregistrée ne doit pas empêcher le launcher de fonctionner.
        }
    }
}
