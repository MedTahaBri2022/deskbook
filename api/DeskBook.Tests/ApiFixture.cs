using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeskBook.Api;
using DeskBook.Api.Data;
using DeskBook.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace DeskBook.Tests;

/// <summary>A clock the tests can set: "now" is part of every booking rule.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>
/// The whole API (WebApplicationFactory) against a real PostgreSQL started by
/// Testcontainers, migrations included: the exclusion constraint is real.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "correct horse battery";

    // Monday 5 October 2026, 08:00 in Casablanca.
    public static readonly DateTimeOffset Monday8am = Local.At(2026, 10, 5, 8);

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestClock Clock { get; } = new() { Now = Monday8am };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DeskBook", postgres.GetConnectionString());
        builder.UseSetting("DemoData", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync() => await postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }

    /// <summary>Empty tables, three users and three rooms; the clock back to Monday 08:00.</summary>
    public async Task ResetAsync()
    {
        Clock.Now = Monday8am;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskBookDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE bookings, rooms, users RESTART IDENTITY CASCADE");

        User NewUser(string email, string name, Role role)
        {
            var user = new User { Email = email, FullName = name, Role = role, PasswordHash = "" };
            user.PasswordHash = hasher.HashPassword(user, Password);
            return user;
        }

        db.Users.AddRange(
            NewUser("nadia@example.com", "Nadia Chraibi", Role.Employee),
            NewUser("omar@example.com", "Omar Fassi", Role.Employee),
            NewUser("admin@example.com", "Salma Admin", Role.Admin));
        db.Rooms.AddRange(
            new Room { Name = "Atlas", Floor = 1, Capacity = 10, Equipment = ["screen"] },
            new Room { Name = "Rif", Floor = 1, Capacity = 4 },
            new Room { Name = "Closed", Floor = 3, Capacity = 8, IsActive = false });
        await db.SaveChangesAsync();
    }

    public async Task<HttpClient> ClientFor(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class Json
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
