using System.Linq;

namespace Banguat.ExchangeRates.Common;

/// <summary>
/// <see cref="IRateCalendar"/> backed by the <c>America/Guatemala</c> time zone.
/// </summary>
public sealed class GuatemalaRateCalendar : IRateCalendar
{
    internal const string TimeZoneId = "America/Guatemala";

    /// <summary>
    /// Local wall-clock time at which a new publication day starts. Banguat's exact publication hour is
    /// not documented, so midnight is used as the conservative default. Shifting this constant (for
    /// example to 08:00) moves the boundary everywhere without touching any call site.
    /// </summary>
    private static readonly TimeOnly DayBoundaryLocalTime = TimeOnly.MinValue;

    /// <summary>Upper bound on the minute-by-minute probe used to skip a DST spring-forward gap.</summary>
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
            // Read the clock once so the boundary and the subtraction cannot straddle a tick.
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

        // Today's boundary has already passed (always true for a midnight boundary), so target the next
        // one. Equality also advances, which keeps the returned window strictly positive.
        if (candidate <= localNow.DateTime)
        {
            candidate = candidate.AddDays(1);
        }

        return ToAbsolute(candidate);
    }

    /// <summary>
    /// Resolves a local wall-clock time to an absolute instant. Guatemala observes no DST today, but the
    /// gap and overlap cases are handled so a future reintroduction cannot produce a wrong expiry.
    /// </summary>
    private DateTimeOffset ToAbsolute(DateTime localWallTime)
    {
        DateTime candidate = localWallTime;

        // A spring-forward gap means this wall-clock time never occurs; advance to the first that does.
        for (int probe = 0; probe < InvalidTimeProbeMinutes && _timeZone.IsInvalidTime(candidate); probe++)
        {
            candidate = candidate.AddMinutes(1);
        }

        TimeSpan offset = _timeZone.IsAmbiguousTime(candidate)
            // A fall-back overlap means this wall-clock time occurs twice. The larger offset yields the
            // earlier instant, so caches expire sooner rather than serving a stale rate for an extra hour.
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
            // Minimal container images and InvariantGlobalization builds may ship without tzdata. Falling
            // back to Guatemala's fixed UTC-6 offset keeps the API serving instead of failing at startup.
            return TimeZoneInfo.CreateCustomTimeZone(
                "Banguat/Guatemala", TimeSpan.FromHours(-6), "Guatemala Time", "Guatemala Standard Time");
        }
    }
}