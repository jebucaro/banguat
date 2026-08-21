using System.Globalization;
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
public class RateCommandTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();

    private static GetCurrentRate.Response OnePoint(DateOnly date, decimal buy, decimal sell)
    {
        return new GetCurrentRate.Response([new GetCurrentRate.RatePoint(date, buy, sell)]);
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
    public async Task ExecuteAsync_PlainMode_RendersRateTableAndHint()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrentRateAsync(currency);
        Assert.Contains("2026-08-18", testConsole.Output);
        Assert.Contains("24", testConsole.Output);
        Assert.Contains(1.1596m.ToString(CultureInfo.InvariantCulture), testConsole.Output);
        Assert.Contains("Next steps:", testConsole.Output);
        Assert.Contains("→ rate history --since <date> --currency 24", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_RichMode_RendersDecoratedTableWithHintsPanel()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24", Output = "rich"
            };

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
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"currency\": 24", stdOut);
        Assert.Contains("\"buy\": 1.1596", stdOut);
        Assert.Contains("\"help\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyNotSet_DefaultsToUsd()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 7.6m, 7.6m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(_client, testConsole, new BundledCurrencyAliasCatalog(),
            new StubCurrencyOverrideSource());

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrentRateAsync(usd);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRatesReturned_PlainMode_WritesEmptyMessage()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(Result.Success(new GetCurrentRate.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("No rate data available for today.", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRatesReturned_JsonMode_WritesZeroCount()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(Result.Success(new GetCurrentRate.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"currency\": 24", stdOut);
        Assert.Contains("\"count\": 0", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputNotSet_DefaultsToRich()
    {
        CurrencyCode currency = new(24);
        _client.GetCurrentRateAsync(currency).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24"
            };

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
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "24"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsAlias_ResolvesToNumericCodeAndEchoesAlias()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 7.6215m, 7.6217m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "USD", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        await _client.Received(1).GetCurrentRateAsync(usd);
        Assert.Contains("\"currency\": 2", stdOut);
        Assert.Contains("\"currencyAlias\": \"USD\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsNumeric_EchoesKnownAliasAnyway()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 7.6215m, 7.6217m)));
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "2", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"currencyAlias\": \"USD\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsUnknownAlias_WritesErrorWithSuggestion()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Currency = "USSD"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.DidNotReceive().GetCurrentRateAsync(Arg.Any<CurrencyCode>());
        Assert.Contains("Unknown currency 'USSD'", testConsole.Output);
        Assert.Contains("USD", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverrideIsCaseSensitiveDictionary_LowercaseAliasStillResolves()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrentRateAsync(eur).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 10.5m, 10.6m)));
        TestConsole testConsole = new TestConsole().Width(200);
        Dictionary<string, CurrencyCode> caseSensitiveOverrides = new() { ["EUR"] = new CurrencyCode(24) };
        RateCommand command = new(
            _client, testConsole, new BundledCurrencyAliasCatalog(),
            new StubCurrencyOverrideSource(caseSensitiveOverrides)) { Currency = "eur", Output = "json" };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        await _client.Received(1).GetCurrentRateAsync(eur);
        Assert.Contains("\"currency\": 24", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeOverriddenWithNewAlias_OldBundledAliasStillResolvesAsInput()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd).Returns(
            Result.Success(OnePoint(new DateOnly(2026, 8, 18), 7.6215m, 7.6217m)));
        TestConsole testConsole = new TestConsole().Width(200);
        Dictionary<string, CurrencyCode> overrides = new() { ["DOLLAR"] = new CurrencyCode(2) };
        RateCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource(overrides))
            {
                Currency = "USD", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        await _client.Received(1).GetCurrentRateAsync(usd);
        Assert.Contains("\"currencyAlias\": \"DOLLAR\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverrideFileMalformed_WritesError()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateCommand command = new(
            _client, testConsole, new BundledCurrencyAliasCatalog(),
            new ThrowingCurrencyOverrideSource("Failed to read /fake/path: invalid JSON.")) { Currency = "24" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.DidNotReceive().GetCurrentRateAsync(Arg.Any<CurrencyCode>());
        Assert.Contains("Failed to read /fake/path: invalid JSON.", testConsole.Output);
    }
}