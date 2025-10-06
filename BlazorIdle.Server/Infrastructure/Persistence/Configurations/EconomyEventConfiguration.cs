using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class EconomyEventConfiguration : IEntityTypeConfiguration<EconomyEventRecord>
{
    public void Configure(EntityTypeBuilder<EconomyEventRecord> builder)
    {
        builder.ToTable("economy_events");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.CharacterId)
            .IsRequired();
        
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        // 幂等键唯一索引（防止重复发放）
        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique();
        
        // 角色索引（用于查询角色的经济事件历史）
        builder.HasIndex(e => e.CharacterId);
        
        // 战斗索引（用于查询特定战斗的奖励记录）
        builder.HasIndex(e => e.BattleId)
            .HasFilter("BattleId IS NOT NULL");
    }
}
