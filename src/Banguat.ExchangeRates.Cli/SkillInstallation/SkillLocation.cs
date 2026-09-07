namespace Banguat.ExchangeRates.Cli.SkillInstallation;

public sealed class SkillLocation
{
    public static readonly SkillLocation Claude = new("claude", Path.Combine(".claude", "skills"));

    private SkillLocation(string id, string relativeSkillDirectory)
    {
        Id = id;
        RelativeSkillDirectory = relativeSkillDirectory;
    }

    public string Id { get; }

    public string RelativeSkillDirectory { get; }

    public static IReadOnlyList<SkillLocation> All { get; } = [Claude];
}
