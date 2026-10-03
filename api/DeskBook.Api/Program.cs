using DeskBook.Api.Auth;
using DeskBook.Api.Bookings;
using DeskBook.Api.Common;
using DeskBook.Api.Data;
using DeskBook.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ------------------------------------------------------------------ options
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters (set Jwt__Secret).");
var office = config.GetSection("Office").Get<OfficeOptions>() ?? new OfficeOptions();

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(office);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BookingRules>();

// ----------------------------------------------------------------- services
builder.Services.AddDbContext<DeskBookDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("DeskBook")));
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = JwtOptions.Issuer,
            ValidAudience = JwtOptions.Issuer,
            IssuerSigningKey = jwt.Key(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = TokenService.RoleClaim,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// Token lifetimes are checked against the application's TimeProvider, the same
// clock that issues them, instead of the machine clock.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((options, clock) =>
        options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, parameters) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var skew = parameters.ClockSkew;
            return (notBefore is null || notBefore.Value <= now + skew) && (expires is null || expires.Value > now - skew);
        });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<DeskBookDbContext>();

var app = builder.Build();

// ----------------------------------------------------------------- pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();
app.MapHealthChecks("/health");

// Migrations at startup keep the demo one command away; a production deployment
// would run them as a separate step (Database:MigrateOnStartup=false).
if (config.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DeskBookDbContext>();
    await db.Database.MigrateAsync();
    if (config.GetValue("DemoData", false))
        await DemoData.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
            scope.ServiceProvider.GetRequiredService<BookingRules>(), TimeProvider.System);
}

app.Run();

/// <summary>Visible to the integration tests (WebApplicationFactory).</summary>
public partial class Program;
