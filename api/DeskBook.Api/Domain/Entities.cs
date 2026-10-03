namespace DeskBook.Api.Domain;

public enum Role
{
    Employee,
    Admin,
}

public enum BookingStatus
{
    Active,
    Cancelled,
}

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string PasswordHash { get; set; }
    public Role Role { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public List<string> Equipment { get; set; } = [];

    /// <summary>An inactive room keeps its history but cannot be booked.</summary>
    public bool IsActive { get; set; } = true;
}

public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public int Attendees { get; set; }

    /// <summary>Stored as timestamptz; the interval is [StartsAt, EndsAt).</summary>
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
