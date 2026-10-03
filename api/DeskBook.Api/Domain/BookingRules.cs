namespace DeskBook.Api.Domain;

/// <summary>Office rules for a booking. Plain code, no database: unit tested on its own.</summary>
public sealed class BookingRules(OfficeOptions office)
{
    public static readonly TimeSpan Slot = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);
    public static readonly TimeSpan MaxAdvance = TimeSpan.FromDays(60);

    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(office.TimeZone);

    /// <summary>Every rule the requested interval breaks, keyed by field; empty when it is valid.</summary>
    public Dictionary<string, string> Check(DateTimeOffset startsAt, DateTimeOffset endsAt, int attendees,
        Room room, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string>();
        if (endsAt <= startsAt)
        {
            errors["endsAt"] = "must be after the start";
            return errors;
        }

        var duration = endsAt - startsAt;
        if (duration < MinDuration || duration > MaxDuration)
            errors["endsAt"] = $"a booking lasts between {MinDuration.TotalMinutes:0} minutes and {MaxDuration.TotalHours:0} hours";
        if (!OnSlot(startsAt))
            errors["startsAt"] = "must start on a quarter of an hour";
        else if (!OnSlot(endsAt))
            errors["endsAt"] = "must end on a quarter of an hour";

        if (startsAt <= now)
            errors["startsAt"] = "must be in the future";
        else if (startsAt - now > MaxAdvance)
            errors["startsAt"] = $"cannot be more than {MaxAdvance.TotalDays:0} days ahead";

        var localStart = TimeZoneInfo.ConvertTime(startsAt, TimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(endsAt, TimeZone);
        if (localStart.Date != localEnd.Date && localEnd.TimeOfDay != TimeSpan.Zero)
            errors["endsAt"] = "must be on the same day";
        else if (localStart.TimeOfDay < office.Opens || EndTime(localEnd) > office.Closes)
            errors["startsAt"] = $"the office is open from {office.Opens:hh\\:mm} to {office.Closes:hh\\:mm}";
        if (localStart.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            errors["startsAt"] = "the office is closed at the weekend";

        if (!room.IsActive)
            errors["roomId"] = "this room cannot be booked any more";
        if (attendees > room.Capacity)
            errors["attendees"] = $"{room.Name} holds {room.Capacity} people";

        return errors;
    }

    /// <summary>Opening hours of a local date, as instants.</summary>
    public (DateTimeOffset Opens, DateTimeOffset Closes) OpeningHours(DateOnly day)
    {
        DateTimeOffset At(TimeSpan time)
        {
            var local = day.ToDateTime(TimeOnly.FromTimeSpan(time));
            return new DateTimeOffset(local, TimeZone.GetUtcOffset(local));
        }
        return (At(office.Opens), At(office.Closes));
    }

    private static TimeSpan EndTime(DateTimeOffset localEnd) =>
        localEnd.TimeOfDay == TimeSpan.Zero ? TimeSpan.FromHours(24) : localEnd.TimeOfDay;

    private static bool OnSlot(DateTimeOffset instant) =>
        instant.Second == 0 && instant.Millisecond == 0 && instant.Minute % (int)Slot.TotalMinutes == 0;
}

public sealed class OfficeOptions
{
    public string TimeZone { get; set; } = "Africa/Casablanca";
    public TimeSpan Opens { get; set; } = TimeSpan.FromHours(7);
    public TimeSpan Closes { get; set; } = TimeSpan.FromHours(21);
}
