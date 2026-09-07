using Banguat.ExchangeRates.Cli.SkillInstallation;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

[Command("skill install", Description = "Install the Banguat exchange rates skill for Claude Code.")]
public sealed partial class SkillInstallCommand(IAnsiConsole console, string? localRoot = null, string? userRoot = null)
    : ICommand
{
    private readonly string _localRoot = localRoot ?? Directory.GetCurrentDirectory();
    private readonly string _userRoot = userRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [CommandOption(
        "scope",
        Description = "Where to install: local (this repo's .claude/skills), user (~/.claude/skills), or both.")]
    public string Scope { get; set; } = "local";

    public ValueTask ExecuteAsync(IConsole cliConsole)
    {
        if (!TryParseScope(out InstallScope scope))
        {
            return default;
        }

        IReadOnlyList<SkillAssetFile> files = EmbeddedSkillLoader.Load();

        if (files.Count == 0)
        {
            console.MarkupLine("[red]No embedded skill files found. This build of the CLI is missing its skill content.[/]");
            Environment.ExitCode = 1;
            return default;
        }

        foreach ((string root, string label) in ResolveRoots(scope))
        {
            InstallResult result =
                SkillInstaller.Install(root, SkillLocation.Claude, EmbeddedSkillLoader.SkillName, files);
            WriteResult(label, result);

            if (result.HasFailures)
            {
                Environment.ExitCode = 1;
            }
        }

        return default;
    }

    private bool TryParseScope(out InstallScope scope)
    {
        switch (Scope.ToLowerInvariant())
        {
            case "local":
                scope = InstallScope.Local;
                return true;
            case "user":
                scope = InstallScope.User;
                return true;
            case "both":
                scope = InstallScope.Both;
                return true;
            default:
                scope = InstallScope.Local;
                console.MarkupLine(
                    $"[red]Invalid value for --scope: '{Markup.Escape(Scope)}'. Expected one of: local, user, both.[/]");
                Environment.ExitCode = 1;
                return false;
        }
    }

    private (string Root, string Label)[] ResolveRoots(InstallScope scope)
    {
        (string Root, string Label) local = (_localRoot, $"./.claude/skills/{EmbeddedSkillLoader.SkillName}");
        (string Root, string Label) user = (_userRoot, $"~/.claude/skills/{EmbeddedSkillLoader.SkillName}");

        return scope switch
        {
            InstallScope.Local => [local],
            InstallScope.User => [user],
            _ => [local, user]
        };
    }

    private void WriteResult(string label, InstallResult result)
    {
        if (result.HasChanges)
        {
            console.WriteLine(
                $"Installed skill to {label} ({result.Written.Count} written, {result.Updated.Count} updated).");
        }
        else if (!result.HasFailures)
        {
            console.WriteLine($"{label} is already up to date.");
        }

        foreach (SkillFileFailure failure in result.Failed)
        {
            console.MarkupLine(
                $"[red]Failed to write {Markup.Escape(failure.RelativePath)} in {Markup.Escape(label)}: " +
                $"{Markup.Escape(failure.Message)}[/]");
        }
    }
}
