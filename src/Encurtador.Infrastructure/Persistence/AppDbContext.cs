using Encurtador.Application.Links;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LinkConfiguration());
    }
}
