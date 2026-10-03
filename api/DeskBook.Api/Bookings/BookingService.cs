using DeskBook.Api.Common;
using DeskBook.Api.Data;
using DeskBook.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeskBook.Api.Bookings;

public sealed class BookingService(DeskBookDbContext db, BookingRules rules, TimeProvider clock)
{
    public async Task<BookingResponse> CreateAsync(User user, CreateBookingRequest request, CancellationToken token)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == request.RoomId, token)
                   ?? throw ApiException.NotFound($"Room {request.RoomId}");
        var startsAt = request.StartsAt.ToUniversalTime();
        var endsAt = request.EndsAt.ToUniversalTime();

        var errors = rules.Check(startsAt, endsAt, request.Attendees, room, clock.GetUtcNow());
        if (errors.Count > 0)
            throw ApiException.Invalid("The booking breaks the office rules", errors);

        // Friendly answer for the common case: say which booking is in the way.
        var clash = await ActiveOverlapping(room.Id, startsAt, endsAt).Include(b => b.User).FirstOrDefaultAsync(token);
        if (clash is not null)
            throw Conflict(clash, user);

        var booking = new Booking
        {
            RoomId = room.Id,
            UserId = user.Id,
            Title = request.Title.Trim(),
            Attendees = request.Attendees,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
            ConstraintName: DeskBookDbContext.NoOverlapConstraint,
        })
        {
            // Another request booked the slot between our check and our insert:
            // the exclusion constraint refused the second one.
            throw new ApiException(StatusCodes.Status409Conflict, "Someone booked this room for that time a moment ago");
        }

        booking.Room = room;
        booking.User = user;
        return ToResponse(booking, user);
    }

    public async Task<BookingResponse> CancelAsync(User user, int id, CancellationToken token)
    {
        var booking = await db.Bookings.Include(b => b.Room).Include(b => b.User)
                          .SingleOrDefaultAsync(b => b.Id == id, token)
                      ?? throw ApiException.NotFound($"Booking {id}");
        if (booking.UserId != user.Id && user.Role != Role.Admin)
            throw ApiException.Forbidden("Only the person who booked, or an administrator, can cancel");
        if (booking.Status == BookingStatus.Cancelled)
            throw new ApiException(StatusCodes.Status409Conflict, "This booking is already cancelled");
        if (booking.StartsAt <= clock.GetUtcNow())
            throw new ApiException(StatusCodes.Status409Conflict, "A booking that has started cannot be cancelled");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = clock.GetUtcNow();
        await db.SaveChangesAsync(token);
        return ToResponse(booking, user);
    }

    public async Task<IReadOnlyList<BookingResponse>> MineAsync(User user, bool includePast, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var query = db.Bookings.AsNoTracking().Include(b => b.Room).Include(b => b.User)
            .Where(b => b.UserId == user.Id && b.Status == BookingStatus.Active);
        if (!includePast)
            query = query.Where(b => b.EndsAt > now);
        var bookings = await query.OrderBy(b => b.StartsAt).Take(200).ToListAsync(token);
        return bookings.Select(b => ToResponse(b, user)).ToList();
    }

    /// <summary>Every active room for a local date, with its bookings and free intervals.</summary>
    public async Task<IReadOnlyList<RoomDayResponse>> DayAsync(User user, DateOnly day, int minCapacity,
        CancellationToken token)
    {
        var (opens, closes) = rules.OpeningHours(day);
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.IsActive && r.Capacity >= minCapacity)
            .OrderBy(r => r.Floor).ThenBy(r => r.Name)
            .ToListAsync(token);
        var roomIds = rooms.Select(r => r.Id).ToList();
        // One query for every room of the day, not one per room.
        var bookings = await db.Bookings.AsNoTracking().Include(b => b.User).Include(b => b.Room)
            .Where(b => roomIds.Contains(b.RoomId) && b.Status == BookingStatus.Active
                        && b.StartsAt < closes && b.EndsAt > opens)
            .OrderBy(b => b.StartsAt)
            .ToListAsync(token);
        var byRoom = bookings.ToLookup(b => b.RoomId);

        return rooms.Select(room =>
        {
            var taken = byRoom[room.Id].ToList();
            return new RoomDayResponse(RoomsMapping.ToResponse(room), day, opens, closes,
                taken.Select(b => ToResponse(b, user)).ToList(), FreeIntervals(opens, closes, taken, rules.TimeZone));
        }).ToList();
    }

    /// <summary>The gaps between bookings, within opening hours; in office time when a zone is given.</summary>
    public static IReadOnlyList<FreeSlot> FreeIntervals(DateTimeOffset opens, DateTimeOffset closes,
        IEnumerable<Booking> bookings, TimeZoneInfo? zone = null)
    {
        DateTimeOffset Office(DateTimeOffset instant) => zone is null ? instant : TimeZoneInfo.ConvertTime(instant, zone);

        var free = new List<FreeSlot>();
        var cursor = opens;
        foreach (var booking in bookings.OrderBy(b => b.StartsAt))
        {
            if (booking.StartsAt > cursor)
                free.Add(new FreeSlot(Office(cursor), Office(booking.StartsAt < closes ? booking.StartsAt : closes)));
            if (booking.EndsAt > cursor)
                cursor = booking.EndsAt;
            if (cursor >= closes)
                break;
        }
        if (cursor < closes)
            free.Add(new FreeSlot(Office(cursor), Office(closes)));
        return free;
    }

    private IQueryable<Booking> ActiveOverlapping(int roomId, DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        db.Bookings.Where(b => b.RoomId == roomId && b.Status == BookingStatus.Active
                               && b.StartsAt < endsAt && b.EndsAt > startsAt);

    private ApiException Conflict(Booking clash, User user) =>
        new(StatusCodes.Status409Conflict, "The room is already booked at that time",
            conflict: new
            {
                clash.Id,
                StartsAt = Office(clash.StartsAt),
                EndsAt = Office(clash.EndsAt),
                // Other people's meeting titles stay private.
                Title = clash.UserId == user.Id ? clash.Title : null,
                BookedBy = clash.User.FullName,
            });

    /// <summary>
    /// Instants leave the API in office time (Africa/Casablanca, from the server's
    /// time zone database): clients read the wall-clock time and its offset from
    /// the value itself instead of trusting their own, possibly older, zone data.
    /// </summary>
    private DateTimeOffset Office(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, rules.TimeZone);

    private BookingResponse ToResponse(Booking b, User viewer) =>
        new(b.Id, b.RoomId, b.Room.Name, b.UserId, b.User.FullName, b.Title, b.Attendees, Office(b.StartsAt), Office(b.EndsAt),
            b.Status, b.Status == BookingStatus.Active && (b.UserId == viewer.Id || viewer.Role == Role.Admin));
}
