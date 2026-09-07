using Banguat.ExchangeRates.Cli.SkillInstallation;

namespace Banguat.ExchangeRates.Cli.Tests.SkillInstallation;

public class EmbeddedSkillLoaderTests
{
    [Fact]
    public void Load_ReturnsSkillMdAndReferenceMd()
    {
        IReadOnlyList<SkillAssetFile> files = EmbeddedSkillLoader.Load();

        Assert.Contains(files, f => f.RelativePath == "SKILL.md");
        Assert.Contains(files, f => f.RelativePath == "reference.md");
    }

    [Fact]
    public void Load_EveryFileHasNonEmptyContent()
    {
        IReadOnlyList<SkillAssetFile> files = EmbeddedSkillLoader.Load();

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.False(string.IsNullOrWhiteSpace(f.Content)));
    }

    [Fact]
    public void Load_SkillMdHasFrontmatterName()
    {
        IReadOnlyList<SkillAssetFile> files = EmbeddedSkillLoader.Load();

        SkillAssetFile skillMd = files.Single(f => f.RelativePath == "SKILL.md");
        Assert.Contains("name: banguat-exchange-rates-cli", skillMd.Content);
    }
}
