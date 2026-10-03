using System.ComponentModel.DataAnnotations;
using DeskBook.Api.Domain;

namespace DeskBook.Api;

// Request and response bodies. Data annotations are checked by [ApiController]
// before the action runs (400 with a ValidationProblemDetails).

public record LoginRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, MaxLength(128)] string Password);

public record MeResponse(int Id, string FullName, string Email, Role Role);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, MeResponse User);

public record RoomRequest(
    [Required, MaxLength(80)] string Name,
    [Range(-5, 100)] int Floor,
    [Range(1, 500)] int Capacity,
    [MaxLength(20)] List<string>? Equipment,
    bool IsActive = true);

public record RoomResponse(int Id, string Name, int Floor, int Capacity, IReadOnlyList<string> Equipment, bool IsActive);

public record CreateBookingRequest(
    [Range(1, int.MaxValue)] int RoomId,
    [Required, MaxLength(120)] string Title,
    [Range(1, 500)] int Attendees,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public record BookingResponse(
    int Id, int RoomId, string RoomName, int UserId, string BookedBy, string Title, int Attendees,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, BookingStatus Status, bool CanCancel);

/// <summary>A room's day: its bookings and the free intervals within opening hours.</summary>
public record RoomDayResponse(RoomResponse Room, DateOnly Date, DateTimeOffset Opens, DateTimeOffset Closes,
    IReadOnlyList<BookingResponse> Bookings, IReadOnlyList<FreeSlot> Free);

public record FreeSlot(DateTimeOffset From, DateTimeOffset To);

public static class RoomsMapping
{
    public static RoomResponse ToResponse(Room r) => new(r.Id, r.Name, r.Floor, r.Capacity, r.Equipment, r.IsActive);

    public static void Apply(Room room, RoomRequest request)
    {
        room.Floor = request.Floor;
        room.Capacity = request.Capacity;
        room.Equipment = (request.Equipment ?? []).Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        room.IsActive = request.IsActive;
    }
}
