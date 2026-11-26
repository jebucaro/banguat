using System.Linq;

namespace Banguat.ExchangeRates.Common;

public sealed class GuatemalaRateCalendar : IRateCalendar
{
    internal const string TimeZoneId = "America/Guatemala";

    // Banguat doesn't document its publication hour, so midnight is the safe default. Change it here to move
    // the boundary everywhere.
    private static readonly TimeOnly DayBoundaryLocalTime = TimeOnly.MinValue;

    private const int InvalidTimeProbeMinutes = 24 * 60;

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public GuatemalaRateCalendar(TimeProvider timeProvider)
        : this(timeProvider, ResolveTimeZone())
    {
    }

    internal GuatemalaRateCalendar(TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(timeZone);

        _timeProvider = timeProvider;
        _timeZone = timeZone;
    }

    public DateOnly Today => DateOnly.FromDateTime(LocalTime(_timeProvider.GetUtcNow()).DateTime);

    public DateTimeOffset NextDayBoundary => NextBoundaryFrom(_timeProvider.GetUtcNow());

    public TimeSpan TimeUntilNextDay
    {
        get
        {
            // One clock read, so the boundary and the subtraction can't disagree.
            DateTimeOffset utcNow = _timeProvider.GetUtcNow();
            TimeSpan remaining = NextBoundaryFrom(utcNow) - utcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    private DateTimeOffset LocalTime(DateTimeOffset utcNow)
    {
        return TimeZoneInfo.ConvertTime(utcNow, _timeZone);
    }

    private DateTimeOffset NextBoundaryFrom(DateTimeOffset utcNow)
    {
        DateTimeOffset localNow = LocalTime(utcNow);
        DateTime candidate = localNow.Date.Add(DayBoundaryLocalTime.ToTimeSpan());

        // Today's boundary already passed (always, for midnight), so aim for the next one. The <= keeps the
        // window above zero.
        if (candidate <= localNow.DateTime)
        {
            candidate = candidate.AddDays(1);
        }

        return ToAbsolute(candidate);
    }

    // Guatemala has no DST today, but gaps and overlaps are handled so bringing it back can't break expiries.
    private DateTimeOffset ToAbsolute(DateTime localWallTime)
    {
        DateTime candidate = localWallTime;

        // Spring-forward gap: that wall-clock time never happens, so step ahead to one that does.
        for (int probe = 0; probe < InvalidTimeProbeMinutes && _timeZone.IsInvalidTime(candidate); probe++)
        {
            candidate = candidate.AddMinutes(1);
        }

        TimeSpan offset = _timeZone.IsAmbiguousTime(candidate)
            // Fall-back overlap: the time happens twice. The larger offset is the earlier instant, so caches
            // expire early instead of serving a stale rate for an extra hour.
            ? _timeZone.GetAmbiguousTimeOffsets(candidate).Max()
            : _timeZone.GetUtcOffset(candidate);

        return new DateTimeOffset(candidate, offset);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // No tzdata in minimal images or InvariantGlobalization builds, so fall back to Guatemala's fixed
            // UTC-6 instead of failing at startup.
            return TimeZoneInfo.CreateCustomTimeZone(
                "Banguat/Guatemala", TimeSpan.FromHours(-6), "Guatemala Time", "Guatemala Standard Time");
        }
    }
}