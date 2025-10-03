using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 同时配置 BattleRecord 与 BattleSegmentRecord 两个实体。
/// 实现两个 IEntityTypeConfiguration<T> 接口是合法的；若后期配置膨胀可拆分。
/// </summary>
public class BattleConfiguration : IEntityTypeConfiguration<BattleRecord>, IEntityTypeConfiguration<BattleSegmentRecord>
{
    /// <summary>
    /// 针对 BattleRecord 的实体配置。
    /// </summary>
    public void Configure(EntityTypeBuilder<BattleRecord> b)
    {
        // 主键
        b.HasKey(x => x.Id);

        // 一对多：BattleRecord (1) -> BattleSegmentRecord (多)
        // Segments 为 BattleRecord 的导航集合；分段实体没有反向导航，所以这里用 WithOne() 不指定导航。
        b.HasMany(x => x.Segments)
            .WithOne()
            .HasForeignKey(s => s.BattleId)          // 外键列：BattleSegmentRecord.BattleId
            .OnDelete(DeleteBehavior.Cascade);       // 级联删除：删除 Battle 时同时删除其所有分段

        // 为 CharacterId 建索引：便于按角色查询战斗历史
        b.HasIndex(x => x.CharacterId);
    }

    /// <summary>
    /// 针对 BattleSegmentRecord 的实体配置。
    /// </summary>
    public void Configure(EntityTypeBuilder<BattleSegmentRecord> s)
    {
        // 主键
        s.HasKey(x => x.Id);

        // DamageBySourceJson 文本列最大长度限制（当前简单存 JSON 字符串）
        s.Property(x => x.DamageBySourceJson).HasMaxLength(4000);

        // 为 BattleId 建索引：常见按战斗主键加载全部分段的查询
        s.HasIndex(x => x.BattleId);
    }
}