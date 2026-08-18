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

public class CurrenciesCommandTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();

    private static GetAvailableCurrencies.Response OneCurrency()
    {
        return new GetAvailableCurrencies.Response(
            [new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(24), "Euro")]);
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
        Assert.Contains("# hint: rate --currency <id>", testConsole.Output);
        Assert.Contains("# hint: rate history --since <date> --currency <id>", testConsole.Output);
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
        Assert.Contains("rate --currency <id>", testConsole.Output);
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

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("\"count\": 1", testConsole.Output);
        Assert.Contains("\"code\": 24", testConsole.Output);
        Assert.Contains("\"description\": \"Euro\"", testConsole.Output);
        Assert.Contains("\"help\"", testConsole.Output);
        Assert.Contains("rate --currency <id>", testConsole.Output);
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

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("\"alias\": \"GTQ\"", testConsole.Output);
        Assert.Contains("\"alias\": \"USD\"", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WhenCurrencyHasMultipleAliases_ListsBoth()
    {
        _client.GetAvailableCurrenciesAsync().Returns(Result.Success(TwoCurrencies()));
        TestConsole testConsole = new TestConsole().Width(200);
        var overrides = new Dictionary<string, CurrencyCode> { ["DOLLAR"] = new(2) };
        CurrenciesCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource(overrides))
            {
                Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("DOLLAR", testConsole.Output);
        Assert.Contains("USD", testConsole.Output);
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
