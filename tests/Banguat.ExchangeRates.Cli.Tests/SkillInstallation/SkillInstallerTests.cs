using Banguat.ExchangeRates.Cli.SkillInstallation;

namespace Banguat.ExchangeRates.Cli.Tests.SkillInstallation;

public class SkillInstallerTests
{
    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"skill-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Install_WhenSkillNotPresent_WritesEveryFile()
    {
        string root = CreateTempDir();
        try
        {
            SkillAssetFile[] files = [new("SKILL.md", "one"), new("reference.md", "two")];

            InstallResult result = SkillInstaller.Install(root, SkillLocation.Claude, "banguat-exchange-rates", files);

            Assert.Equal(2, result.Written.Count);
            Assert.Empty(result.Updated);
            Assert.Empty(result.Skipped);
            Assert.True(result.HasChanges);
            Assert.False(result.HasFailures);
            string skillMdPath = Path.Combine(root, ".claude", "skills", "banguat-exchange-rates", "SKILL.md");
            Assert.Equal("one", File.ReadAllText(skillMdPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Install_WhenContentUnchanged_SkipsFileAndReportsNoChanges()
    {
        string root = CreateTempDir();
        try
        {
            SkillAssetFile[] files = [new("SKILL.md", "one")];
            SkillInstaller.Install(root, SkillLocation.Claude, "banguat-exchange-rates", files);

            InstallResult result = SkillInstaller.Install(root, SkillLocation.Claude, "banguat-exchange-rates", files);

            Assert.Empty(result.Written);
            Assert.Empty(result.Updated);
            Assert.Equal(["SKILL.md"], result.Skipped);
            Assert.False(result.HasChanges);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Install_WhenContentChanged_OverwritesFile()
    {
        string root = CreateTempDir();
        try
        {
            SkillInstaller.Install(
                root, SkillLocation.Claude, "banguat-exchange-rates", [new SkillAssetFile("SKILL.md", "one")]);

            InstallResult result = SkillInstaller.Install(
                root, SkillLocation.Claude, "banguat-exchange-rates", [new SkillAssetFile("SKILL.md", "two")]);

            Assert.Equal(["SKILL.md"], result.Updated);
            string skillMdPath = Path.Combine(root, ".claude", "skills", "banguat-exchange-rates", "SKILL.md");
            Assert.Equal("two", File.ReadAllText(skillMdPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Install_WhenContentDiffersOnlyByLineEndings_SkipsFile()
    {
        string root = CreateTempDir();
        try
        {
            SkillInstaller.Install(
                root, SkillLocation.Claude, "banguat-exchange-rates",
                [new SkillAssetFile("SKILL.md", "line one\nline two")]);

            InstallResult result = SkillInstaller.Install(
                root, SkillLocation.Claude, "banguat-exchange-rates",
                [new SkillAssetFile("SKILL.md", "line one\r\nline two")]);

            Assert.Equal(["SKILL.md"], result.Skipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Install_WritesNestedRelativePaths()
    {
        string root = CreateTempDir();
        try
        {
            SkillAssetFile[] files = [new("docs/extra.md", "nested")];

            SkillInstaller.Install(root, SkillLocation.Claude, "banguat-exchange-rates", files);

            string path = Path.Combine(root, ".claude", "skills", "banguat-exchange-rates", "docs", "extra.md");
            Assert.Equal("nested", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Install_WhenTargetPathIsADirectory_RecordsFailureAndContinuesWithOtherFiles()
    {
        string root = CreateTempDir();
        try
        {
            string skillDir = Path.Combine(root, ".claude", "skills", "banguat-exchange-rates");
            Directory.CreateDirectory(Path.Combine(skillDir, "SKILL.md"));

            SkillAssetFile[] files = [new("SKILL.md", "one"), new("reference.md", "two")];

            InstallResult result = SkillInstaller.Install(root, SkillLocation.Claude, "banguat-exchange-rates", files);

            Assert.Single(result.Failed);
            Assert.Equal("SKILL.md", result.Failed[0].RelativePath);
            Assert.Equal(["reference.md"], result.Written);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
