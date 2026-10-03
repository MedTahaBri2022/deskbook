using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeskBook.Api;
using DeskBook.Api.Domain;

namespace DeskBook.Tests;

[Collection(ApiCollection.Name)]
public class BookingApiTests(ApiFactory api) : IAsyncLifetime
{
    private const int Atlas = 1;
    private const int Rif = 2;
    private const int ClosedRoom = 3;

    public Task InitializeAsync() => api.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset Tuesday(int hour, int minute = 0) => Local.Tuesday(hour, minute);

    private static object Booking(int room, int fromHour, int toHour, int attendees = 4, string title = "Weekly sync") =>
        new { roomId = room, title, attendees, startsAt = Tuesday(fromHour), endsAt = Tuesday(toHour) };

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task A_booking_is_created_and_appears_in_my_list_and_the_day_view()
    {
        var nadia = await api.ClientFor("nadia@example.com");

        var created = await nadia.PostAsJsonAsync("/api/bookings", new
        {
            roomId = Atlas, title = "Weekly sync", attendees = 4,
            startsAt = Tuesday(10).ToUniversalTime(), endsAt = Tuesday(11).ToUniversalTime(),
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>(Json.Options);
        Assert.Equal("Atlas", booking!.RoomName);
        // Sent in UTC by the test, returned in office time: same instant, office offset.
        Assert.Equal(Tuesday(10), booking.StartsAt);
        Assert.Equal(Tuesday(10).Offset, booking.StartsAt.Offset);
        Assert.True(booking.CanCancel);

        var mine = await nadia.GetFromJsonAsync<List<BookingResponse>>("/api/bookings/mine", Json.Options);
        Assert.Single(mine!);

        var day = await nadia.GetFromJsonAsync<List<RoomDayResponse>>("/api/rooms/day?date=2026-10-06", Json.Options);
        var atlas = day!.Single(r => r.Room.Name == "Atlas");
        Assert.Single(atlas.Bookings);
        Assert.Equal([new FreeSlot(Tuesday(7), Tuesday(10)), new FreeSlot(Tuesday(11), Tuesday(21))], atlas.Free);
        Assert.DoesNotContain(day!, r => r.Room.Name == "Closed");
    }

    [Fact]
    public async Task An_overlapping_booking_is_refused_and_says_what_is_in_the_way()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var omar = await api.ClientFor("omar@example.com");
        await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11, title: "Budget review"));

        var clash = await omar.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 12));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var problem = await Body(clash);
        Assert.Equal("Nadia Chraibi", problem.GetProperty("conflict").GetProperty("bookedBy").GetString());
        // Someone else's meeting title is not disclosed.
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("conflict").GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task Back_to_back_bookings_do_not_overlap()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var omar = await api.ClientFor("omar@example.com");
        await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11));

        Assert.Equal(HttpStatusCode.Created, (await omar.PostAsJsonAsync("/api/bookings", Booking(Atlas, 11, 12))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await omar.PostAsJsonAsync("/api/bookings", Booking(Atlas, 9, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await omar.PostAsJsonAsync("/api/bookings", Booking(Rif, 10, 11))).StatusCode);
    }

    [Fact]
    public async Task Two_simultaneous_requests_for_the_same_slot_cannot_both_succeed()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var omar = await api.ClientFor("omar@example.com");

        // Both pass the application check at the same time; the exclusion
        // constraint in PostgreSQL refuses the second insert.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(i =>
                (i % 2 == 0 ? nadia : omar).PostAsJsonAsync("/api/bookings", Booking(Atlas, 14, 15))));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Office_rules_are_returned_as_field_errors()
    {
        var nadia = await api.ClientFor("nadia@example.com");

        var tooMany = await nadia.PostAsJsonAsync("/api/bookings", Booking(Rif, 10, 11, attendees: 5));
        var night = await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 20, 22));
        var closed = await nadia.PostAsJsonAsync("/api/bookings", Booking(ClosedRoom, 10, 11));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMany.StatusCode);
        Assert.Equal("Rif holds 4 people", (await Body(tooMany)).GetProperty("errors").GetProperty("attendees").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, night.StatusCode);
        Assert.True((await Body(night)).GetProperty("errors").TryGetProperty("startsAt", out _));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closed.StatusCode);
    }

    [Fact]
    public async Task Malformed_requests_are_rejected_before_any_rule()
    {
        var nadia = await api.ClientFor("nadia@example.com");

        var response = await nadia.PostAsJsonAsync("/api/bookings",
            new { roomId = Atlas, title = "", attendees = 0, startsAt = Tuesday(10), endsAt = Tuesday(11) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Body(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Title", out _));
        Assert.True(errors.TryGetProperty("Attendees", out _));
    }

    [Fact]
    public async Task A_cancelled_slot_can_be_booked_again()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var omar = await api.ClientFor("omar@example.com");
        var booking = await (await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11)))
            .Content.ReadFromJsonAsync<BookingResponse>(Json.Options);

        var cancelled = await nadia.PostAsync($"/api/bookings/{booking!.Id}/cancel", null);
        Assert.Equal(BookingStatus.Cancelled, (await cancelled.Content.ReadFromJsonAsync<BookingResponse>(Json.Options))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await nadia.PostAsync($"/api/bookings/{booking.Id}/cancel", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await omar.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11))).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_or_an_admin_cancels_and_never_after_the_start()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var omar = await api.ClientFor("omar@example.com");
        var admin = await api.ClientFor("admin@example.com");
        var first = await (await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11)))
            .Content.ReadFromJsonAsync<BookingResponse>(Json.Options);
        var second = await (await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 12, 13)))
            .Content.ReadFromJsonAsync<BookingResponse>(Json.Options);

        Assert.Equal(HttpStatusCode.Forbidden, (await omar.PostAsync($"/api/bookings/{first!.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/bookings/{first.Id}/cancel", null)).StatusCode);

        api.Clock.Now = Tuesday(12, 15);
        nadia = await api.ClientFor("nadia@example.com"); // the token of Monday has expired
        Assert.Equal(HttpStatusCode.Conflict, (await nadia.PostAsync($"/api/bookings/{second!.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task My_list_hides_past_bookings_unless_asked()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        await nadia.PostAsJsonAsync("/api/bookings", Booking(Atlas, 10, 11));
        api.Clock.Now = Tuesday(11, 30);
        nadia = await api.ClientFor("nadia@example.com"); // the token of Monday has expired

        Assert.Empty((await nadia.GetFromJsonAsync<List<BookingResponse>>("/api/bookings/mine", Json.Options))!);
        Assert.Single((await nadia.GetFromJsonAsync<List<BookingResponse>>("/api/bookings/mine?includePast=true", Json.Options))!);
    }
}
