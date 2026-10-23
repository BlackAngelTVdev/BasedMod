using System.Text.RegularExpressions;

namespace ItemAdderLauncher.Core;

public static class VersionHelper
{
    private static readonly Regex VersionPattern = new(
        @"(\d+(?:\.\d+){1,3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Extrait une version depuis un tag GitHub (ex. "v1.2.3") et la normalise
    /// sur quatre composants pour que "1.1.0" et "1.1.0.0" soient équivalentes.
    /// </summary>
    public static Version? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match match = VersionPattern.Match(text);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out Version? version))
        {
            return null;
        }

        return Normalize(version);
    }

    public static string Format(Version? version)
    {
        return version?.ToString() ?? "inconnue";
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }
}
