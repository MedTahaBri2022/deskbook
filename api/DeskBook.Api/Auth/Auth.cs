using System.Security.Claims;
using System.Text;
using DeskBook.Api.Common;
using DeskBook.Api.Data;
using DeskBook.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeskBook.Api.Auth;

public sealed class JwtOptions
{
    public const string Issuer = "deskbook";

    /// <summary>HMAC-SHA256 key, at least 32 characters (checked at startup).</summary>
    public string Secret { get; set; } = "";
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(Secret));
}

public sealed class TokenService(JwtOptions options, TimeProvider clock)
{
    /// <summary>Short claim names in the token; the validation reads them as is (no inbound mapping).</summary>
    public const string RoleClaim = "role";

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now + options.Lifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Issuer,
            Audience = JwtOptions.Issuer,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(RoleClaim, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(options.Key(), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }
}

/// <summary>
/// The user of the current request, reloaded from the database on every call:
/// a deleted account or a changed role takes effect immediately.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor http, DeskBookDbContext db)
{
    private User? user;

    public async Task<User> GetAsync(CancellationToken token = default)
    {
        if (user is not null)
            return user;
        var subject = http.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(subject, out var id))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Not signed in");
        user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, token)
               ?? throw new ApiException(StatusCodes.Status401Unauthorized, "This account no longer exists");
        return user;
    }
}
