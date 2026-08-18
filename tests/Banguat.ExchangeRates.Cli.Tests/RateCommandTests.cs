using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Commands;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using CliFx.Infrastructure;
using NSubstitute;
using Spectre.Console.Testing;

namespace Banguat.ExchangeRates.Cli.Tests;

public class RateCommandTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();

    private static GetCurrentRate.Response OnePoint(DateOnly date, decimal buy, decimal sell)
    {
        return new GetCurrentRate.Response([new GetCurrentRate.RatePoint(date, buy, sell)]);
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_RendersRateTableAndHint()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24, Output = "plain" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrentRateAsync(currency);
        Assert.Contains("2026-08-18", testConsole.Output);
        Assert.Contains("24", testConsole.Output);
        Assert.Contains(1.1596m.ToString(CultureInfo.InvariantCulture), testConsole.Output);
        Assert.Contains("# hint: rate history --since <date> --currency 24", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_RichMode_RendersDecoratedTableWithHintsPanel()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24, Output = "rich" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("2026-08-18", testConsole.Output);
        Assert.Contains("Next steps", testConsole.Output);
        Assert.Contains("rate history --since <date> --currency 24", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_JsonMode_WritesStructuredPayload()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24, Output = "json" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("\"currency\": 24", testConsole.Output);
        Assert.Contains("\"buy\": 1.1596", testConsole.Output);
        Assert.Contains("\"help\"", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyNotSet_DefaultsToUsd()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 7.6m, 7.6m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole);

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrentRateAsync(usd);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRatesReturned_PlainMode_WritesEmptyMessage()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(Result.Success(new GetCurrentRate.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24, Output = "plain" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("No rate data available for today.", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRatesReturned_JsonMode_WritesZeroCount()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(Result.Success(new GetCurrentRate.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24, Output = "json" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("\"currency\": 24", testConsole.Output);
        Assert.Contains("\"count\": 0", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputNotSet_DefaultsToRich()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24 };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("2026-08-18", testConsole.Output);
        Assert.Contains("Next steps", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientFails_WritesErrorToOutput()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Failure<GetCurrentRate.Response>(Error.Failure("Banguat.Transport", "boom")));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole) { Currency = 24 };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }
}
