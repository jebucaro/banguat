using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Commands;
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
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();
    private readonly ICurrencyResolver _currencyResolver;

    public RateHistoryCommandTests()
    {
        _currencyResolver = new CurrencyResolver(_client, _aliasCatalog);
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
    public async Task ExecuteAsync_WithSince_PlainMode_RendersHistoryTableAndCount()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
            {
                Since = "2026-08-01", Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetRateHistoryAsync(currency, since, null, null);
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
        _client.GetRateHistoryAsync(currency, null, from, to).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(to, 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
            {
                From = "2026-08-01", To = "2026-08-18", Currency = "24", Output = "plain"
            };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetRateHistoryAsync(currency, null, from, to);
        Assert.Contains("2026-08-18", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_RichMode_RendersCaptionAndNoHintsPanel()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
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
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
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
        _client.GetRateHistoryAsync(usd, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = "2026-08-01" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.Received(1).GetRateHistoryAsync(usd, since, null, null);
    }

    // Which combinations are valid is the library's rule (see GetRateHistoryTests); the command only has
    // to forward the parsed dates unchanged and print the library's validation message.
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-08-01", "2026-08-01", "2026-08-18")]
    [InlineData(null, "2026-08-01", null)]
    public async Task ExecuteAsync_WhenHistoryRangeIsInvalid_WritesValidationError(
        string? since, string? from, string? to)
    {
        _client.GetRateHistoryAsync(Arg.Any<CurrencyCode>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(),
                Arg.Any<DateOnly?>())
            .Returns(Result.Failure<GetRateHistory.Response>(BanguatErrors.InvalidHistoryRange()));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = since, From = from, To = to };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Provide either a since date, or both from and to dates", testConsole.Output);
        await _client.Received(1).GetRateHistoryAsync(
            new CurrencyCode(2), ParseOrNull(since), ParseOrNull(from), ParseOrNull(to));
    }

    private static DateOnly? ParseOrNull(string? value)
    {
        return value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinceIsNotValidDate_WritesDateFormatError()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = "08/17/2026" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("yyyy-MM-dd", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPointsReturned_PlainMode_WritesEmptyMessage()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response([])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
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
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 18), 1.1596m, 1.1597m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = "2026-08-01", Currency = "24" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("Showing 1 rate points", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientFails_WritesErrorToOutput()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(24);
        _client.GetRateHistoryAsync(currency, since, null, null).Returns(
            Result.Failure<GetRateHistory.Response>(Error.Failure("Banguat.Transport", "boom")));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = "2026-08-01", Currency = "24" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        Assert.Contains("boom", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsAlias_ResolvesAndEchoesAlias()
    {
        DateOnly since = new(2026, 8, 1);
        CurrencyCode usd = new(2);
        _client.GetRateHistoryAsync(usd, since, null, null).Returns(
            Result.Success(new GetRateHistory.Response(
                [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 18), 7.6215m, 7.6217m)])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
            {
                Since = "2026-08-01", Currency = "USD", Output = "json"
            };

        string stdOut = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        await _client.Received(1).GetRateHistoryAsync(usd, since, null, null);
        Assert.Contains("\"currency\": 2", stdOut);
        Assert.Contains("\"currencyAlias\": \"USD\"", stdOut);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrencyIsUnknownAlias_WritesErrorWithSuggestion()
    {
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog) { Since = "2026-08-01", Currency = "USSD" };

        await command.ExecuteAsync(new FakeInMemoryConsole());

        await _client.DidNotReceive().GetRateHistoryAsync(Arg.Any<CurrencyCode>(), Arg.Any<DateOnly?>(),
            Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>());
        Assert.Contains("Unknown currency 'USSD'", testConsole.Output);
        Assert.Contains("USD", testConsole.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNumericCodeIsNotInCatalog_WritesJsonErrorWithIsoHint()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(
            new GetAvailableCurrencies.Response(
                [new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(2), "Dólares de EE.UU.")])));
        TestConsole testConsole = new TestConsole().Width(200);
        RateHistoryCommand command =
            new(_client, _currencyResolver, testConsole, _aliasCatalog)
            {
                Since = "2026-08-01", Currency = "840", Output = "json"
            };

        string output = await CaptureStdOutAsync(() => command.ExecuteAsync(new FakeInMemoryConsole()));

        Assert.Contains("\"error\"", output);
        Assert.Contains("ISO 4217", output);
        Assert.Contains("Run 'currencies'", output);
    }
}