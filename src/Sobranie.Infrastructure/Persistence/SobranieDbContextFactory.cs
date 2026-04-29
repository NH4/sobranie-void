using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sobranie.Infrastructure.Persistence;

public sealed class SobranieDbContextFactory : IDesignTimeDbContextFactory<SobranieDbContext>
{
    public SobranieDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SobranieDbContext>();
        optionsBuilder.UseSqlite("Data Source=sobranie.db");
        return new SobranieDbContext(optionsBuilder.Options);
    }
}