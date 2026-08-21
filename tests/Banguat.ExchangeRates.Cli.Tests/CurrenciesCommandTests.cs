using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Aliases;
using Banguat.ExchangeRates.Cli.Commands;
using Banguat.ExchangeRates.Cli.Tests.Aliases;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using CliFx.Infrastructure;
using NSubstitute;
using Spectre.Console.Testing;

namespace Banguat.ExchangeRates.Cli.Tests;

[Collection("ConsoleOutRedirect")]
public class CurrenciesCommandTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();

    private static GetAvailableCurrencies.Response OneCurrency()
    {
        return new GetAvailableCurrencies.Response(
            [new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(24), "Euro")]);
    }

    /// <summary>
    /// JSON-mode output is written via System.Console.Out (bypassing the injected IAnsiConsole/TestConsole
    /// to avoid Spectre's console-width line wrapping), so JSON-mode assertions must capture real stdout.
    /// </summary>
    private static async Task<string> CaptureStdOutAsync(Func<ValueTask> action)
    {
        TextWriter original = Console.Out;
        StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_RendersBorderlessTableWithCountAndHints()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(OneCurrency()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("code", testConsole.Output);
        Assert.Contains("description", testConsole.Output);
        Assert.Contains("alias", testConsole.Output);
        Assert.Contains("24", testConsole.Output);
        Assert.Contains("Euro", testConsole.Output);
        Assert.Contains("# count: 1", testConsole.Output);
        Assert.Contains("Next steps:", testConsole.Output);
        Assert.Contains("→ rate --currency <id|alias>", testConsole.Output);
        Assert.Contains("→ rate history --since <date> --currency <id|alias>", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_RichMode_RendersDecoratedTableWithCaptionAndHintsPanel()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(OneCurrency()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Output = "rich"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("24", testConsole.Output);
        Assert.Contains("Euro", testConsole.Output);
        Assert.Contains("Showing 1 currencies", testConsole.Output);
        Assert.Contains("Next steps", testConsole.Output);
        Assert.Contains("rate --currency <id|alias>", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_JsonMode_WritesStructuredPayload()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(OneCurrency()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"count\": 1", stdOut);
        Assert.Contains("\"code\": 24", stdOut);
        Assert.Contains("\"description\": \"Euro\"", stdOut);
        Assert.Contains("\"help\"", stdOut);
        Assert.Contains("rate --currency <id|alias>", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputNotSet_DefaultsToRich()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(OneCurrency()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource());

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("24", testConsole.Output);
        Assert.Contains("Euro", testConsole.Output);
        Assert.Contains("Showing 1 currencies", testConsole.Output);
        Assert.Contains("Next steps", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientFails_WritesErrorToOutput()
    {
        _client.GetAvailableCurrenciesAsync().Returns(
            Result.Failure<GetAvailableCurrencies.Response>(Error.Failure("Banguat.Transport", "boom")));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource());

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputValueInvalid_WritesErrorNamingValidValues()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Output = "xml"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("plain, rich, json", testConsole.Output);
    }

    private static GetAvailableCurrencies.Response TwoCurrencies()
    {
        return new GetAvailableCurrencies.Response(
        [
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(1), "Quetzales"),
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(2), "Dólares de EE.UU.")
        ]);
    }

    [Fact]
    public async Task ExecuteAsync_JsonMode_WhenCurrencyHasKnownAlias_IncludesAlias()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(TwoCurrencies()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"alias\": \"GTQ\"", stdOut);
        Assert.Contains("\"alias\": \"USD\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WhenCurrencyHasOverride_ShowsOverrideAliasOnly()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(TwoCurrencies()));
        TestConsole testConsole = new TestConsole().Width(200);
        Dictionary<string, CurrencyCode> overrides = new() { ["DOLLAR"] = new CurrencyCode(2) };
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource(overrides))
            {
                Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("DOLLAR", testConsole.Output);
        Assert.DoesNotContain("USD", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverrideFileMalformed_WritesError()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(OneCurrency()));
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command = new(
            _client, testConsole, new BundledCurrencyAliasCatalog(),
            new ThrowingCurrencyOverrideSource("Failed to read /fake/path: invalid JSON."));

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Failed to read /fake/path: invalid JSON.", testConsole.Output);
    }
}