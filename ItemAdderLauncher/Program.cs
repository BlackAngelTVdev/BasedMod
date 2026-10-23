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

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
