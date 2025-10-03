using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// EF Core 上下文：聚合所有实体集合 (DbSet) 与模型配置。
/// 在 Program.cs 中注册生命周期（通常为 Scoped）。
/// 职责：
///   * 暴露 DbSet 让上层通过 LINQ 构建查询
///   * 在 OnModelCreating 中应用实体映射配置
///   * 可在需要时重写 SaveChanges / SaveChangesAsync 做审计、领域事件、并发处理等
/// </summary>
public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
        // 此处通常不做耗时操作；依赖注入已构造好 options（连接串 / 提供程序 / 拦截器等）
    }

    // === 聚合根 / 实体集合 ===
    // DbSet<T> 只是查询/跟踪入口；真正的映射细节在配置类里（Configuration）
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<BattleRecord> Battles => Set<BattleRecord>();
    public DbSet<BattleSegmentRecord> BattleSegments => Set<BattleSegmentRecord>();

    /// <summary>
    /// 模型构建钩子：集中 Fluent 配置。
    /// 当前用 ApplyConfigurationsFromAssembly 自动扫描实现 IEntityTypeConfiguration<> 的配置类。
    /// 调用时机：首次使用该上下文需要模型（例如第一次查询或生成迁移）时。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 自动加载同程序集下的 *Configuration 类（如 CharacterConfiguration、BattleConfiguration）
        // 优点：避免手工逐个调用；新增实体只需添加配置类即可。
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        // 可在此处添加全局约定（如：统一字符串列长度、软删除过滤器、时间转换器等）

        base.OnModelCreating(modelBuilder);
    }
}