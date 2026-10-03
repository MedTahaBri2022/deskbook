using DeskBook.Api;
using DeskBook.Api.Bookings;
using DeskBook.Api.Domain;

namespace DeskBook.Tests;

/// <summary>Office rules, without database or HTTP.</summary>
public class BookingRulesTests
{
    private static readonly BookingRules Rules = new(new OfficeOptions());
    private static readonly Room Atlas = new() { Id = 1, Name = "Atlas", Capacity = 10 };

    // Monday 5 October 2026, 08:00 in Casablanca.
    private static readonly DateTimeOffset Now = Local.At(2026, 10, 5, 8);

    private static DateTimeOffset Tuesday(int hour, int minute = 0) => Local.Tuesday(hour, minute);

    [Fact]
    public void A_regular_meeting_is_valid() =>
        Assert.Empty(Rules.Check(Tuesday(10), Tuesday(11), 6, Atlas, Now));

    [Fact]
    public void The_end_must_follow_the_start() =>
        Assert.Contains("endsAt", Rules.Check(Tuesday(11), Tuesday(10), 6, Atlas, Now).Keys);

    [Theory]
    [InlineData(10, 5, 10, 30)]   // starts off the quarter
    [InlineData(10, 0, 10, 40)]   // ends off the quarter
    public void Bookings_follow_quarter_hours(int fromHour, int fromMinute, int toHour, int toMinute) =>
        Assert.NotEmpty(Rules.Check(Tuesday(fromHour, fromMinute), Tuesday(toHour, toMinute), 2, Atlas, Now));

    [Fact]
    public void A_booking_lasts_at_most_eight_hours()
    {
        Assert.Empty(Rules.Check(Tuesday(8), Tuesday(16), 2, Atlas, Now));
        Assert.Contains("endsAt", Rules.Check(Tuesday(8), Tuesday(16, 15), 2, Atlas, Now).Keys);
    }

    [Fact]
    public void Opening_hours_are_seven_to_twenty_one_local_time()
    {
        Assert.Empty(Rules.Check(Tuesday(7), Tuesday(8), 2, Atlas, Now));
        Assert.Empty(Rules.Check(Tuesday(20), Tuesday(21), 2, Atlas, Now));
        Assert.Contains("startsAt", Rules.Check(Tuesday(6, 45), Tuesday(7, 30), 2, Atlas, Now).Keys);
        Assert.Contains("startsAt", Rules.Check(Tuesday(20, 30), Tuesday(21, 15), 2, Atlas, Now).Keys);
    }

    [Fact]
    public void The_offset_used_by_the_client_does_not_matter()
    {
        // 07:30 in Casablanca, sent in UTC or with a +05:00 offset: the same instant.
        var start = Tuesday(7, 30);
        Assert.Empty(Rules.Check(start.ToUniversalTime(), start.AddHours(1).ToUniversalTime(), 2, Atlas, Now));
        Assert.Empty(Rules.Check(start.ToOffset(TimeSpan.FromHours(5)), start.AddHours(1).ToOffset(TimeSpan.FromHours(5)), 2, Atlas, Now));
        // 06:30 local, whatever the offset, is before opening time.
        var early = Tuesday(6, 30).ToUniversalTime();
        Assert.Contains("startsAt", Rules.Check(early, early.AddHours(1), 2, Atlas, Now).Keys);
    }

    [Fact]
    public void The_office_is_closed_at_the_weekend()
    {
        var saturday = Local.At(2026, 10, 10, 10);
        Assert.Contains("startsAt", Rules.Check(saturday, saturday.AddHours(1), 2, Atlas, Now).Keys);
    }

    [Fact]
    public void Past_and_far_future_bookings_are_refused()
    {
        var earlierToday = Local.At(2026, 10, 5, 7);
        Assert.Contains("startsAt", Rules.Check(earlierToday, earlierToday.AddHours(1), 2, Atlas, Now).Keys);
        var inSeventyDays = Tuesday(10).AddDays(70);
        Assert.Contains("startsAt", Rules.Check(inSeventyDays, inSeventyDays.AddHours(1), 2, Atlas, Now).Keys);
    }

    [Fact]
    public void Attendees_must_fit_and_the_room_must_be_open()
    {
        Assert.Equal("Atlas holds 10 people", Rules.Check(Tuesday(10), Tuesday(11), 11, Atlas, Now)["attendees"]);
        var closed = new Room { Id = 2, Name = "Old room", Capacity = 10, IsActive = false };
        Assert.Contains("roomId", Rules.Check(Tuesday(10), Tuesday(11), 2, closed, Now).Keys);
    }

    [Fact]
    public void Free_intervals_are_the_gaps_between_bookings()
    {
        var opens = Tuesday(7);
        var closes = Tuesday(21);
        Booking Booked(int from, int to) => new() { Title = "x", StartsAt = Tuesday(from), EndsAt = Tuesday(to) };

        var free = BookingService.FreeIntervals(opens, closes, [Booked(9, 10), Booked(10, 12), Booked(15, 16)]);

        Assert.Equal([
            new FreeSlot(Tuesday(7), Tuesday(9)),
            new FreeSlot(Tuesday(12), Tuesday(15)),
            new FreeSlot(Tuesday(16), Tuesday(21)),
        ], free);
        Assert.Equal([new FreeSlot(opens, closes)], BookingService.FreeIntervals(opens, closes, []));
        Assert.Empty(BookingService.FreeIntervals(opens, closes, [Booked(7, 21)]));
    }
}
