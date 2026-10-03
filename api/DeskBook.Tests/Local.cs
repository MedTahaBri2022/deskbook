namespace DeskBook.Tests;

/// <summary>
/// Local office times as instants, with the offset taken from the time zone
/// database: no hard-coded UTC offset (Morocco's has changed more than once).
/// </summary>
public static class Local
{
    private static readonly TimeZoneInfo Casablanca = TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca");

    public static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Casablanca.GetUtcOffset(local));
    }

    /// <summary>Tuesday 6 October 2026, the day most tests book.</summary>
    public static DateTimeOffset Tuesday(int hour, int minute = 0) => At(2026, 10, 6, hour, minute);
}
