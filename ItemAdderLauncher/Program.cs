using ItemAdderLauncher.Core;

namespace ItemAdderLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--diagnose", StringComparison.OrdinalIgnoreCase)))
        {
            return Diagnostics.RunDiagnose();
        }

        if (args.Any(arg => string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            return Diagnostics.RunSelfTest(args);
        }

        if (args.Any(arg => string.Equals(arg, "--bepinextest", StringComparison.OrdinalIgnoreCase)))
        {
            return Diagnostics.RunBepInExTest();
        }

        LauncherUpdater.CleanupUpdateArtifacts();

        bool silent = args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));

        if (args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            return RunInstallCommand(silent, uninstall: true);
        }

        if (args.Any(arg => string.Equals(arg, "--install", StringComparison.OrdinalIgnoreCase)))
        {
            return RunInstallCommand(silent, uninstall: false);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    /// <summary>
    /// Installation / désinstallation déclenchées en ligne de commande, notamment par
    /// l'entrée « Applications installées » de Windows (« ... --uninstall »).
    /// </summary>
    private static int RunInstallCommand(bool silent, bool uninstall)
    {
        ApplicationConfiguration.Initialize();
        var progress = new Progress<string>(_ => { });

        try
        {
            if (uninstall)
            {
                LauncherInstaller.Uninstall(progress);
                if (!silent)
                {
                    MessageBox.Show(
                        "ItemAdder Launcher a été désinstallé.\n\n" +
                        "Les raccourcis et le dossier d'installation sont supprimés. " +
                        "Le mod dans le jeu et tes réglages ne sont pas touchés : " +
                        "tu peux réinstaller le launcher à tout moment depuis la release GitHub.",
                        "ItemAdder Launcher",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return 0;
            }

            LauncherInstaller.Install(progress);
            if (!silent)
            {
                MessageBox.Show(
                    $"ItemAdder Launcher est installé.\n\n{LauncherInstaller.InstalledExecutablePath}\n\n" +
                    "Une icône t'attend sur le Bureau et dans le menu Démarrer. " +
                    "Les mises à jour remplaceront cette installation.",
                    "ItemAdder Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(
                    (uninstall ? "La désinstallation a échoué :\n\n" : "L'installation a échoué :\n\n") +
                    ex.Message,
                    "ItemAdder Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 1;
        }
    }
}
