using DeskBook.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DeskBook.Api.Data;

public class DeskBookDbContext(DbContextOptions<DeskBookDbContext> options) : DbContext(options)
{
    /// <summary>Name of the PostgreSQL exclusion constraint that forbids overlapping bookings.</summary>
    public const string NoOverlapConstraint = "ex_bookings_no_overlap";

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Email).HasMaxLength(254);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.FullName).HasMaxLength(120);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        model.Entity<Room>(room =>
        {
            room.ToTable("rooms", t => t.HasCheckConstraint("ck_rooms_capacity", "\"Capacity\" BETWEEN 1 AND 500"));
            room.Property(r => r.Name).HasMaxLength(80);
            room.HasIndex(r => r.Name).IsUnique();
        });

        model.Entity<Booking>(booking =>
        {
            booking.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("ck_bookings_interval", "\"EndsAt\" > \"StartsAt\"");
                t.HasCheckConstraint("ck_bookings_attendees", "\"Attendees\" >= 1");
            });
            booking.Property(b => b.Title).HasMaxLength(120);
            booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
            booking.HasOne(b => b.Room).WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
            booking.HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
            booking.HasIndex(b => new { b.RoomId, b.StartsAt });
            booking.HasIndex(b => new { b.UserId, b.StartsAt });
        });
    }
}
