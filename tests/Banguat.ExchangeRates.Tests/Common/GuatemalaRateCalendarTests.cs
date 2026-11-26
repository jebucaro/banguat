using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class GuatemalaRateCalendarTests
{
    private static GuatemalaRateCalendar CalendarAt(string utcInstant)
    {
        return new GuatemalaRateCalendar(new FixedTimeProvider(DateTimeOffset.Parse(utcInstant,
            System.Globalization.CultureInfo.InvariantCulture)));
    }

    // 01:00 UTC is still the previous day in Guatemala, which is exactly what a UTC-based "today" gets wrong.
    [Fact]
    public void Today_DuringUtcMorningBeforeSix_IsPreviousGuatemalaDay()
    {
        Assert.Equal(new DateOnly(2026, 9, 21), CalendarAt("2026-09-22T01:00:00Z").Today);
    }

    [Fact]
    public void Today_DuringGuatemalaAfternoon_MatchesUtcDate()
    {
        Assert.Equal(new DateOnly(2026, 9, 22), CalendarAt("2026-09-22T19:00:00Z").Today);
    }

    [Fact]
    public void Today_OneSecondBeforeLocalMidnight_IsStillTheOutgoingDay()
    {
        Assert.Equal(new DateOnly(2026, 9, 21), CalendarAt("2026-09-22T05:59:59Z").Today);
    }

    [Fact]
    public void Today_ExactlyAtLocalMidnight_IsTheNewDay()
    {
        Assert.Equal(new DateOnly(2026, 9, 22), CalendarAt("2026-09-22T06:00:00Z").Today);
    }

    [Fact]
    public void NextDayBoundary_IsTheUtcInstantOfLocalMidnight()
    {
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-22T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            CalendarAt("2026-09-22T01:00:00Z").NextDayBoundary.ToUniversalTime());
    }

    [Theory]
    [InlineData("2026-09-22T01:00:00Z", 5 * 60 * 60)]
    [InlineData("2026-09-22T19:00:00Z", 11 * 60 * 60)]
    [InlineData("2026-09-22T05:59:59Z", 1)]
    public void TimeUntilNextDay_CountsDownToLocalMidnight(string utcInstant, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, (int)CalendarAt(utcInstant).TimeUntilNextDay.TotalSeconds);
    }

    // Standing right on the boundary must give a full day, never a zero-length window, or entries
    // would expire instantly.
    [Fact]
    public void TimeUntilNextDay_ExactlyAtBoundary_IsAFullDay()
    {
        Assert.Equal(TimeSpan.FromHours(24), CalendarAt("2026-09-22T06:00:00Z").TimeUntilNextDay);
    }

    [Fact]
    public void TimeUntilNextDay_IsAlwaysPositive()
    {
        for (int minute = 0; minute < 24 * 60; minute += 37)
        {
            DateTimeOffset instant = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
            GuatemalaRateCalendar calendar = new(new FixedTimeProvider(instant.AddMinutes(minute)));
            Assert.True(calendar.TimeUntilNextDay > TimeSpan.Zero, $"non-positive at +{minute}m");
        }
    }

    [Fact]
    public void Constructor_ResolvesGuatemalaTimeZoneWithoutThrowing()
    {
        GuatemalaRateCalendar calendar = new(new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.Equal(new DateOnly(1969, 12, 31), calendar.Today);
    }

    // No DST in Guatemala today. These pin the gap and overlap handling so bringing it back can't produce a
    // boundary in the past or an hour of extra staleness.
    [Fact]
    public void NextDayBoundary_WhenLocalMidnightIsSkippedByDst_AdvancesToTheFirstRealInstant()
    {
        TimeZoneInfo zone = ZoneWithTransitionAtMidnight();
        // 2026-03-14 12:00 local, the day before the spring-forward gap at 2026-03-15 00:00.
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 3, 14, 18, 0, 0, TimeSpan.Zero));
        GuatemalaRateCalendar calendar = new(clock, zone);

        DateTimeOffset boundary = calendar.NextDayBoundary;

        Assert.False(zone.IsInvalidTime(boundary.DateTime));
        Assert.Equal(new TimeSpan(1, 0, 0), boundary.DateTime.TimeOfDay);
        Assert.True(boundary > clock.GetUtcNow());
    }

    [Fact]
    public void NextDayBoundary_WhenLocalMidnightIsAmbiguous_PicksTheEarlierInstant()
    {
        TimeZoneInfo zone = ZoneWithAmbiguousMidnight();
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.Zero));
        GuatemalaRateCalendar calendar = new(clock, zone);

        DateTimeOffset boundary = calendar.NextDayBoundary;

        Assert.True(zone.IsAmbiguousTime(boundary.DateTime));
        Assert.Equal(zone.GetAmbiguousTimeOffsets(boundary.DateTime).Max(), boundary.Offset);
    }

    // Springs forward at 00:00 on 15 March, so 00:00-00:59 never happens.
    private static TimeZoneInfo ZoneWithTransitionAtMidnight()
    {
        return CustomZone(
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 15),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 12, 0, 0), 10, 15));
    }

    // Falls back at 01:00 on 15 October, so 00:00-00:59 happens twice.
    private static TimeZoneInfo ZoneWithAmbiguousMidnight()
    {
        return CustomZone(
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 12, 0, 0), 3, 15),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 15));
    }

    private static TimeZoneInfo CustomZone(
        TimeZoneInfo.TransitionTime daylightStart, TimeZoneInfo.TransitionTime daylightEnd)
    {
        TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/Guatemala",
            TimeSpan.FromHours(-6),
            "Test Guatemala",
            "Test Standard",
            "Test Daylight",
            [rule]);
    }
}