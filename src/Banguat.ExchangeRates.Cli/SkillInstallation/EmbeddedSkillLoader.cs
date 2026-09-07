using System.Reflection;

namespace Banguat.ExchangeRates.Cli.SkillInstallation;

public static class EmbeddedSkillLoader
{
    public const string SkillName = "banguat-exchange-rates";

    private const string ResourcePrefix = "skills/" + SkillName + "/";

    public static IReadOnlyList<SkillAssetFile> Load()
    {
        Assembly assembly = typeof(EmbeddedSkillLoader).Assembly;
        List<SkillAssetFile> files = [];

        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string relativePath = resourceName[ResourcePrefix.Length..];

            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
            using StreamReader reader = new(stream);
            files.Add(new SkillAssetFile(relativePath, reader.ReadToEnd()));
        }

        return files.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList();
    }
}
