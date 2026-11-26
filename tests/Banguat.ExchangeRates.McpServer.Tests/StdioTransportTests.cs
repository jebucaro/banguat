using System.Diagnostics;
using Banguat.ExchangeRates.McpServer.Hosting;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol.Client;

namespace Banguat.ExchangeRates.McpServer.Tests;

public class StdioTransportTests
{
    private static readonly string ServerAssembly = typeof(GetRateTool).Assembly.Location;

    [Fact]
    public async Task Stdio_ListsTheThreeTools()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        StdioClientTransport transport = new(new StdioClientTransportOptions
        {
            Name = "banguat", Command = "dotnet", Arguments = [ServerAssembly, "--stdio"]
        });

        await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

        Assert.Equal(
            ["get_currencies", "get_rate", "get_rate_history"],
            tools.Select(tool => tool.Name).Order().ToArray());
    }

    // The host logs at Information on startup and shutdown. Anything of that on stdout would corrupt the
    // protocol stream, so a server that never receives a message must leave stdout empty.
    [Fact]
    public async Task Stdio_WhenStdinCloses_ExitsCleanlyWithNothingOnStdout()
    {
        using Process process = Process.Start(new ProcessStartInfo("dotnet")
        {
            ArgumentList = { ServerAssembly, "--stdio" },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, await stdout);
        _ = await stderr;
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--other" }, false)]
    [InlineData(new[] { "--stdio" }, true)]
    [InlineData(new[] { "--urls", "http://localhost:1", "--stdio" }, true)]
    public void IsRequested_OnlyForTheStdioFlag(string[] args, bool expected)
    {
        Assert.Equal(expected, StdioHost.IsRequested(args));
    }
}