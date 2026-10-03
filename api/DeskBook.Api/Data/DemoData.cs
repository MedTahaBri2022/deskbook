using DeskBook.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeskBook.Api.Data;

/// <summary>Demo users, rooms and bookings on an empty database (DemoData=true). Local use only.</summary>
public static class DemoData
{
    public const string Password = "password";

    public static async Task SeedAsync(DeskBookDbContext db, IPasswordHasher<User> hasher, BookingRules rules,
        TimeProvider clock)
    {
        if (await db.Users.AnyAsync())
            return;

        User NewUser(string email, string name, Role role)
        {
            var user = new User { Email = email, FullName = name, Role = role, PasswordHash = "" };
            user.PasswordHash = hasher.HashPassword(user, Password);
            return user;
        }

        var admin = NewUser("admin@example.com", "Salma Admin", Role.Admin);
        var nadia = NewUser("nadia@example.com", "Nadia Chraibi", Role.Employee);
        var omar = NewUser("omar@example.com", "Omar Fassi", Role.Employee);
        db.Users.AddRange(admin, nadia, omar);

        var rooms = new[]
        {
            new Room { Name = "Atlas", Floor = 1, Capacity = 12, Equipment = ["screen", "video conference"] },
            new Room { Name = "Rif", Floor = 1, Capacity = 4, Equipment = ["whiteboard"] },
            new Room { Name = "Sahara", Floor = 2, Capacity = 20, Equipment = ["projector", "video conference"] },
            new Room { Name = "Souss", Floor = 2, Capacity = 6, Equipment = ["screen"] },
        };
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        // A few bookings on the next working day.
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), rules.TimeZone).Date).AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        var (opens, _) = rules.OpeningHours(day);
        Booking At(Room room, User user, string title, int people, double fromHours, double toHours) => new()
        {
            RoomId = room.Id, UserId = user.Id, Title = title, Attendees = people,
            StartsAt = opens.AddHours(fromHours), EndsAt = opens.AddHours(toHours), CreatedAt = clock.GetUtcNow(),
        };
        db.Bookings.AddRange(
            At(rooms[0], nadia, "Sprint planning", 8, 2, 3.5),
            At(rooms[0], omar, "Client demo", 6, 7, 8),
            At(rooms[1], omar, "1:1", 2, 3, 3.5),
            At(rooms[2], admin, "All hands", 18, 9, 10));
        await db.SaveChangesAsync();
    }
}
