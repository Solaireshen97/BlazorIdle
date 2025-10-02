using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }
    //测试用
    public DbSet<GameData> GameData { get; set; }
    //一阶段功能
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<BattleRecord> Battles => Set<BattleRecord>();
    public DbSet<BattleSegmentRecord> BattleSegments => Set<BattleSegmentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
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
