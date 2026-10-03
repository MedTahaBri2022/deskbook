using DeskBook.Api.Auth;
using DeskBook.Api.Bookings;
using DeskBook.Api.Common;
using DeskBook.Api.Data;
using DeskBook.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeskBook.Api.Controllers;

[ApiController]
[Route("api")]
public class AuthController(DeskBookDbContext db, IPasswordHasher<User> hasher, TokenService tokens, CurrentUser current)
    : ControllerBase
{
    /// <summary>Checked when the e-mail is unknown, so both failures cost the same time.</summary>
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(null!, "no-such-user");

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken token)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, token);
        var result = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Wrong e-mail or password");

        var (jwt, expiresAt) = tokens.Issue(user);
        return new LoginResponse(jwt, expiresAt, Me(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<MeResponse> Get(CancellationToken token) => Me(await current.GetAsync(token));

    private static MeResponse Me(User user) => new(user.Id, user.FullName, user.Email, user.Role);
}

[ApiController]
[Authorize]
[Route("api/rooms")]
public class RoomsController(DeskBookDbContext db, BookingService bookings, CurrentUser current) : ControllerBase
{
    /// <summary>The day view: every active room with its bookings and free intervals.</summary>
    [HttpGet("day")]
    public async Task<IReadOnlyList<RoomDayResponse>> Day([FromQuery] DateOnly date, [FromQuery] int minCapacity = 1,
        CancellationToken token = default) =>
        await bookings.DayAsync(await current.GetAsync(token), date, minCapacity, token);

    [HttpGet]
    public async Task<IReadOnlyList<RoomResponse>> List([FromQuery] bool includeInactive = false,
        CancellationToken token = default) =>
        (await db.Rooms.AsNoTracking().Where(r => includeInactive || r.IsActive)
            .OrderBy(r => r.Floor).ThenBy(r => r.Name).ToListAsync(token))
        .Select(RoomsMapping.ToResponse).ToList();

    [HttpPost]
    [Authorize(Roles = nameof(Role.Admin))]
    public async Task<ActionResult<RoomResponse>> Create(RoomRequest request, CancellationToken token)
    {
        var room = new Room { Name = request.Name.Trim() };
        RoomsMapping.Apply(room, request);
        db.Rooms.Add(room);
        await SaveUnique(token);
        return CreatedAtAction(nameof(List), new { }, RoomsMapping.ToResponse(room));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(Role.Admin))]
    public async Task<RoomResponse> Update(int id, RoomRequest request, CancellationToken token)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == id, token) ?? throw ApiException.NotFound($"Room {id}");
        room.Name = request.Name.Trim();
        RoomsMapping.Apply(room, request);
        await SaveUnique(token);
        return RoomsMapping.ToResponse(room);
    }

    private async Task SaveUnique(CancellationToken token)
    {
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw ApiException.Invalid("A room with this name already exists",
                new Dictionary<string, string> { ["name"] = "already used by another room" });
        }
    }
}

[ApiController]
[Authorize]
[Route("api/bookings")]
public class BookingsController(BookingService bookings, CurrentUser current) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IReadOnlyList<BookingResponse>> Mine([FromQuery] bool includePast = false,
        CancellationToken token = default) =>
        await bookings.MineAsync(await current.GetAsync(token), includePast, token);

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken token)
    {
        var created = await bookings.CreateAsync(await current.GetAsync(token), request, token);
        return Created($"/api/bookings/{created.Id}", created);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<BookingResponse> Cancel(int id, CancellationToken token) =>
        await bookings.CancelAsync(await current.GetAsync(token), id, token);
}
