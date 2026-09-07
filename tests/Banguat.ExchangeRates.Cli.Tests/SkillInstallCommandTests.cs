using Banguat.ExchangeRates.Cli.Commands;
using Banguat.ExchangeRates.Common;
using CliFx.Infrastructure;
using Spectre.Console.Testing;

namespace Banguat.ExchangeRates.Cli.Tests;

public class SkillInstallCommandTests
{
    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"skill-install-cmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string SkillMdPath(string root) =>
        Path.Combine(root, ".claude", "skills", "banguat-exchange-rates", "SKILL.md");

    private static string KnownCurrenciesPath(string root) =>
        Path.Combine(root, ".claude", "skills", "banguat-exchange-rates", "known-currencies.md");

    [Fact]
    public async Task ExecuteAsync_ScopeLocal_InstallsOnlyToLocalRoot()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole testConsole = new TestConsole().Width(200);
            SkillInstallCommand command =
                new(testConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "local" };

            await command.ExecuteAsync(new FakeInMemoryConsole());

            Assert.True(File.Exists(SkillMdPath(localRoot)));
            Assert.False(File.Exists(SkillMdPath(userRoot)));
            Assert.Contains("./.claude/skills/banguat-exchange-rates", testConsole.Output);
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InstallsGeneratedKnownCurrenciesFileWithBundledAliases()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole testConsole = new TestConsole().Width(200);
            SkillInstallCommand command =
                new(testConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "local" };

            await command.ExecuteAsync(new FakeInMemoryConsole());

            string content = File.ReadAllText(KnownCurrenciesPath(localRoot));
            Assert.Contains("| USD | 2 |", content);
            Assert.Contains("| VES | 41 |", content);
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScopeUser_InstallsOnlyToUserRoot()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole testConsole = new TestConsole().Width(200);
            SkillInstallCommand command =
                new(testConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "user" };

            await command.ExecuteAsync(new FakeInMemoryConsole());

            Assert.False(File.Exists(SkillMdPath(localRoot)));
            Assert.True(File.Exists(SkillMdPath(userRoot)));
            Assert.Contains("~/.claude/skills/banguat-exchange-rates", testConsole.Output);
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScopeBoth_InstallsToBothRoots()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole testConsole = new TestConsole().Width(200);
            SkillInstallCommand command =
                new(testConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "both" };

            await command.ExecuteAsync(new FakeInMemoryConsole());

            Assert.True(File.Exists(SkillMdPath(localRoot)));
            Assert.True(File.Exists(SkillMdPath(userRoot)));
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SecondRunWithNoChanges_ReportsAlreadyUpToDate()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole firstConsole = new TestConsole().Width(200);
            SkillInstallCommand first =
                new(firstConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "local" };
            await first.ExecuteAsync(new FakeInMemoryConsole());

            TestConsole secondConsole = new TestConsole().Width(200);
            SkillInstallCommand second =
                new(secondConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "local" };
            await second.ExecuteAsync(new FakeInMemoryConsole());

            Assert.Contains("already up to date", secondConsole.Output);
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeInvalid_WritesErrorAndSetsExitCode()
    {
        string localRoot = CreateTempDir();
        string userRoot = CreateTempDir();
        try
        {
            TestConsole testConsole = new TestConsole().Width(200);
            SkillInstallCommand command =
                new(testConsole, new BundledCurrencyAliasCatalog(), localRoot, userRoot) { Scope = "nowhere" };

            await command.ExecuteAsync(new FakeInMemoryConsole());

            Assert.Contains("local, user, both", testConsole.Output);
            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Directory.Delete(localRoot, recursive: true);
            Directory.Delete(userRoot, recursive: true);
            Environment.ExitCode = 0;
        }
    }
}
