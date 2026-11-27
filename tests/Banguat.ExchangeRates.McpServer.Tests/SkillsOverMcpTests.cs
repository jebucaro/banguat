using System.Text;
using System.Text.RegularExpressions;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Protocol;

#pragma warning disable MCPEXP005 // The Skills extension is experimental.

namespace Banguat.ExchangeRates.McpServer.Tests;

public partial class SkillsOverMcpTests
{
    private const string SkillName = "banguat-exchange-rates-mcp";
    private const string SkillUri = $"skill://{SkillName}/SKILL.md";

    private static readonly string ServerAssembly = typeof(GetRateTool).Assembly.Location;

    // The server is started from the test's working directory, not its own, the way Claude Code launches it from
    // wherever the user is. The skills path is built from AppContext.BaseDirectory, so this still has to find them.
    // If the Skills folder is missing from the output, the server dies at startup with DirectoryNotFoundException,
    // tools included. Loud on purpose: a server that quietly drops its skill is worse.
    [Fact]
    public async Task Stdio_ListsTheBanguatSkill()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await using McpClient client = await ConnectAsync(timeout.Token);

        IList<Skill> skills = await client.ListSkillsAsync(cancellationToken: timeout.Token);

        Skill skill = Assert.Single(skills);
        Assert.Equal(SkillName, skill.Name);
        Assert.Equal(SkillUri, skill.Uri);
    }

    [Fact]
    public async Task Stdio_SkillManifest_MatchesTheServedFile()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await using McpClient client = await ConnectAsync(timeout.Token);
        Skill skill = Assert.Single(await client.ListSkillsAsync(cancellationToken: timeout.Token));

        string text = await ReadSkillMdAsync(client, skill, timeout.Token);

        SkillResource entry = Assert.Single(skill.Resources.Entries, r => r.Uri == SkillUri);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), entry.Size);
    }

    [Fact]
    public async Task Skill_OnlyNamesToolsTheServerHas()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await using McpClient client = await ConnectAsync(timeout.Token);
        Skill skill = Assert.Single(await client.ListSkillsAsync(cancellationToken: timeout.Token));
        string text = await ReadSkillMdAsync(client, skill, timeout.Token);
        string[] toolNames = (await client.ListToolsAsync(cancellationToken: timeout.Token))
            .Select(tool => tool.Name).ToArray();

        string[] mentioned = ToolNamePattern().Matches(text).Select(m => m.Value).Distinct().ToArray();

        Assert.NotEmpty(mentioned);
        Assert.All(mentioned, name => Assert.Contains(name, toolNames));
    }

    [Theory]
    [InlineData("SPIKE")]
    [InlineData("SKILL-OVER-MCP-LOADED")]
    [InlineData("CLI")]
    [InlineData("banguat-exchangerates-cli")]
    public async Task Skill_HasNoSpikeLeftoversAndNoCli(string forbidden)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await using McpClient client = await ConnectAsync(timeout.Token);
        Skill skill = Assert.Single(await client.ListSkillsAsync(cancellationToken: timeout.Token));

        string text = await ReadSkillMdAsync(client, skill, timeout.Token);

        Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    private static Task<McpClient> ConnectAsync(CancellationToken cancellationToken)
    {
        StdioClientTransport transport = new(new StdioClientTransportOptions
        {
            Name = "banguat", Command = "dotnet", Arguments = [ServerAssembly, "--stdio"]
        });

        return McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    // ReadSkillResourceAsync checks the URI, digest and size against the manifest before returning.
    private static async Task<string> ReadSkillMdAsync(McpClient client, Skill skill, CancellationToken cancellationToken)
    {
        ReadResourceResult read = await client.ReadSkillResourceAsync(skill, SkillUri, cancellationToken: cancellationToken);

        return Assert.IsType<TextResourceContents>(Assert.Single(read.Contents)).Text;
    }

    [GeneratedRegex("get_[a-z_]+")]
    private static partial Regex ToolNamePattern();
}
