using System.Net;
using System.Net.Http.Json;
using DeskBook.Api;

namespace DeskBook.Tests;

[Collection(ApiCollection.Name)]
public class AuthAndRoomsApiTests(ApiFactory api) : IAsyncLifetime
{
    public Task InitializeAsync() => api.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_returns_a_token_and_the_profile()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "Nadia@Example.com", password = ApiFactory.Password });

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options);
        Assert.Equal("Nadia Chraibi", login!.User.FullName);
        Assert.False(string.IsNullOrEmpty(login.Token));
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_get_the_same_answer()
    {
        var client = api.CreateClient();
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = "nadia@example.com", password = "nope" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<Problem>())!.Detail,
            (await unknownEmail.Content.ReadFromJsonAsync<Problem>())!.Detail);
    }

    [Fact]
    public async Task The_api_needs_a_valid_token()
    {
        var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/bookings/mine")).StatusCode);

        anonymous.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/bookings/mine")).StatusCode);
    }

    [Fact]
    public async Task Only_admins_manage_rooms()
    {
        var nadia = await api.ClientFor("nadia@example.com");
        var admin = await api.ClientFor("admin@example.com");
        var room = new { name = "Toubkal", floor = 4, capacity = 30, equipment = new[] { "projector", " projector", "" } };

        Assert.Equal(HttpStatusCode.Forbidden, (await nadia.PostAsJsonAsync("/api/rooms", room)).StatusCode);

        var created = await admin.PostAsJsonAsync("/api/rooms", room);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(["projector"], (await created.Content.ReadFromJsonAsync<RoomResponse>(Json.Options))!.Equipment);

        var duplicate = await admin.PostAsJsonAsync("/api/rooms", room);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_room_hides_it_from_the_day_view()
    {
        var admin = await api.ClientFor("admin@example.com");

        var updated = await admin.PutAsJsonAsync("/api/rooms/2", new { name = "Rif", floor = 1, capacity = 4, isActive = false });

        updated.EnsureSuccessStatusCode();
        var day = await admin.GetFromJsonAsync<List<RoomDayResponse>>("/api/rooms/day?date=2026-10-06", Json.Options);
        Assert.Equal(["Atlas"], day!.Select(r => r.Room.Name));
        var all = await admin.GetFromJsonAsync<List<RoomResponse>>("/api/rooms?includeInactive=true", Json.Options);
        Assert.Equal(3, all!.Count);
    }

    [Fact]
    public async Task Health_is_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync("/health")).StatusCode);
    }

    private sealed record Problem(string? Detail);
}
