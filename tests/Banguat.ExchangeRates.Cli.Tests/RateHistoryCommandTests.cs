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
public class RateHistoryCommandTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();

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
    public async Task ExecuteAsync_WithSince_PlainMode_RendersHistoryTableAndCount()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response(
                [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrencyRateHistorySinceAsync(since, currency);
        Assert.Contains("2026-08-18", testConsole.Output);
        Assert.Contains(1.1596m.ToString(CultureInfo.InvariantCulture), testConsole.Output);
        Assert.Contains("# count: 1", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithFromTo_PlainMode_RendersHistoryTable()
    {
        DateOnly from = new(2026, 8, 1);
        DateOnly to = new(2026, 8, 18);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistoryAsync(from, to, currency).Returns(
            Result.Success(new GetCurrencyRateHistory.Response(
                [new GetCurrencyRateHistory.RatePoint(to, 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                From = "2026-08-01", To = "2026-08-18", Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrencyRateHistoryAsync(from, to, currency);
        Assert.Contains("2026-08-18", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_RichMode_RendersCaptionAndNoHintsPanel()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response(
                [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24", Output = "rich"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Showing 1 rate points", testConsole.Output);
        Assert.DoesNotContain("Next steps", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_JsonMode_WritesStructuredPayloadWithNoHelpKey()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response(
                [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"currency\": 24", stdOut);
        Assert.Contains("\"count\": 1", stdOut);
        Assert.DoesNotContain("\"help\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyNotSet_DefaultsToUsd()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode usd = new(2);
        _client.GetCurrencyRateHistorySinceAsync(since, usd).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetCurrencyRateHistorySinceAsync(since, usd);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinceAndRangeBothGiven_WritesValidationError()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", From = "2026-08-01", To = "2026-08-18"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Provide either --since, or both --from and --to", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNeitherSinceNorRangeGiven_WritesValidationError()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource());

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Provide either --since, or both --from and --to", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinceIsNotValidDate_WritesDateFormatError()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "08/17/2026"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("yyyy-MM-dd", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPointsReturned_PlainMode_WritesEmptyMessage()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("No rate data found for the given range.", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputNotSet_DefaultsToRich()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response(
                [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Showing 1 rate points", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientFails_WritesErrorToOutput()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetCurrencyRateHistorySinceAsync(since, currency).Returns(
            Result.Failure<GetCurrencyRateHistorySince.Response>(Error.Failure("Banguat.Transport", "boom")));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "24"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsAlias_ResolvesAndEchoesAlias()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode usd = new(2);
        _client.GetCurrencyRateHistorySinceAsync(since, usd).Returns(
            Result.Success(new GetCurrencyRateHistorySince.Response(
                [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 18), 7.6215m, 7.6217m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "USD", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        await _client.Received(1).GetCurrencyRateHistorySinceAsync(since, usd);
        Assert.Contains("\"currency\": 2", stdOut);
        Assert.Contains("\"currencyAlias\": \"USD\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsUnknownAlias_WritesErrorWithSuggestion()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, testConsole, new BundledCurrencyAliasCatalog(), new StubCurrencyOverrideSource())
            {
                Since = "2026-08-01", Currency = "USSD"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.DidNotReceive().GetCurrencyRateHistorySinceAsync(Arg.Any<DateOnly>(), Arg.Any<CurrencyCode>());
        Assert.Contains("Unknown currency 'USSD'", testConsole.Output);
        Assert.Contains("USD", testConsole.Output);
    }
}