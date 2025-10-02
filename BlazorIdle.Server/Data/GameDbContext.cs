using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Models;

namespace BlazorIdle.Server.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<GameData> GameData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<GameData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Score).IsRequired();
            entity.Property(e => e.Level).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();
        });
    }
}
