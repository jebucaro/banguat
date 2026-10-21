using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class RateHistoryLimitsTests
{
    // 2026-09-23T01:00Z is still 2026-09-22 in Guatemala, so "today" here is the 22nd, not the UTC date.
    private static readonly IRateCalendar Calendar =
        new GuatemalaRateCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero)));

    private static readonly DateOnly Today = new(2026, 9, 22);

    [Fact]
    public void Validate_Should_Accept_RangeEndingToday()
    {
        Result result = RateHistoryLimits.Validate(Today.AddDays(-30), Today, Calendar);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_Should_Accept_RangeStartingOnEarliestDate()
    {
        Result result = RateHistoryLimits.Validate(
            RateHistoryLimits.EarliestDate, RateHistoryLimits.EarliestDate.AddDays(10), Calendar);

        Assert.True(result.IsSuccess);
    }

    // Year 0001 is the case that made Banguat answer with a SOAP fault, which the resilience pipeline counted
    // toward the shared circuit breaker.
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1900, 1, 1)]
    [InlineData(1989, 11, 5)]
    public void Validate_Should_RejectStart_BeforeEarliestDate(int year, int month, int day)
    {
        DateOnly from = new(year, month, day);

        Result result = RateHistoryLimits.Validate(from, from.AddDays(1), Calendar);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateBeforeEarliest", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Validate_Should_RejectEnd_AfterGuatemalaToday()
    {
        Result result = RateHistoryLimits.Validate(Today.AddDays(-5), Today.AddDays(1), Calendar);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateInFuture", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Validate_Should_RejectStart_AfterGuatemalaToday()
    {
        Result result = RateHistoryLimits.Validate(Today.AddDays(1), Today, Calendar);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateInFuture", result.Error.Code);
    }

    [Fact]
    public void Validate_Should_RejectInvertedRange()
    {
        Result result = RateHistoryLimits.Validate(Today.AddDays(-1), Today.AddDays(-5), Calendar);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidDateRange", result.Error.Code);
    }

    [Fact]
    public void Validate_Should_Accept_RangeOfExactlyMaxDays()
    {
        DateOnly to = Today.AddDays(-10);

        Result result = RateHistoryLimits.Validate(to.AddDays(-(RateHistoryLimits.MaxDays - 1)), to, Calendar);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_Should_RejectRange_LongerThanMaxDays()
    {
        DateOnly to = Today.AddDays(-10);

        Result result = RateHistoryLimits.Validate(to.AddDays(-RateHistoryLimits.MaxDays), to, Calendar);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateRangeTooLong", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}