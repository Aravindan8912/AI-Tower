using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AAT.API.Persistence;

public sealed class AatDbContextFactory : IDesignTimeDbContextFactory<AatDbContext>
{
    public AatDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AatDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=aat;Username=aat;Password=design-time-placeholder")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AatDbContext(options);
    }
}
