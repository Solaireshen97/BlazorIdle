using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// EF Core 上下文：聚合所有实体集合 (DbSet) 与模型配置。
/// 在 Program.cs 中注册生命周期（通常为 Scoped）。
/// </summary>
public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    // === 聚合根 / 实体集合 ===
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<BattleRecord> Battles => Set<BattleRecord>();
    public DbSet<BattleSegmentRecord> BattleSegments => Set<BattleSegmentRecord>();

    /// <summary>
    /// 模型构建钩子：集中 Fluent 配置。
    /// 当前用 ApplyConfigurationsFromAssembly 自动扫描实现 IEntityTypeConfiguration<> 的配置类。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 自动加载同程序集下的 *Configuration 类（如 CharacterConfiguration 等）
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}