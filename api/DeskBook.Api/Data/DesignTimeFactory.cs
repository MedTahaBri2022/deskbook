using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeskBook.Api.Data;

/// <summary>Lets `dotnet ef migrations add` build the context without running the application.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<DeskBookDbContext>
{
    public DeskBookDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DeskBookDbContext>()
            .UseNpgsql("Host=localhost;Database=deskbook;Username=deskbook;Password=deskbook")
            .Options);
}
