using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Commands;
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
        CurrenciesCommand command = new(_client, testConsole) { Output = "plain" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("code", testConsole.Output);
        Assert.Contains("description", testConsole.Output);
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
        CurrenciesCommand command = new(_client, testConsole) { Output = "rich" };

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
        CurrenciesCommand command = new(_client, testConsole) { Output = "json" };

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
        CurrenciesCommand command = new(_client, testConsole);

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
        CurrenciesCommand command = new(_client, testConsole);

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputValueInvalid_WritesErrorNamingValidValues()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        CurrenciesCommand command = new(_client, testConsole) { Output = "xml" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("plain, rich, json", testConsole.Output);
    }
}
