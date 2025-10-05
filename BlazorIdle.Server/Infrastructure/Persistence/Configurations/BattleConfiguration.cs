using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class BattleConfiguration : IEntityTypeConfiguration<BattleRecord>, IEntityTypeConfiguration<BattleSegmentRecord>
{
    public void Configure(EntityTypeBuilder<BattleRecord> b)
    {
        b.HasKey(x => x.Id);

        b.HasMany(x => x.Segments)
            .WithOne()
            .HasForeignKey(s => s.BattleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.CharacterId);
    }

    public void Configure(EntityTypeBuilder<BattleSegmentRecord> s)
    {
        s.HasKey(x => x.Id);

        s.Property(x => x.DamageBySourceJson).HasMaxLength(4000);

        // 段级 RNG 区间可加索引用于调试检索（可选）
        // s.HasIndex(x => new { x.BattleId, x.RngIndexStart, x.RngIndexEnd });

        s.HasIndex(x => x.BattleId);
    }
}