using Microsoft.EntityFrameworkCore;

namespace VisorHR.Data;

public class UnitDbContext : DbContext
{
    public UnitDbContext(DbContextOptions<UnitDbContext> options) : base(options)
    {
    }

    // TODO: Add DbSet<T> properties for your tables.
}
