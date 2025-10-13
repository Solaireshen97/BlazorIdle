using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 购买冷却记录的 EF Core 配置
/// </summary>
public class PurchaseCooldownConfiguration : IEntityTypeConfiguration<PurchaseCooldown>
{
    public void Configure(EntityTypeBuilder<PurchaseCooldown> builder)
    {
        builder.ToTable("purchase_cooldowns");
        
        // 主键
        builder.HasKey(pc => pc.Id);
        builder.Property(pc => pc.Id)
            .HasMaxLength(200)
            .IsRequired();
        
        // 角色 ID
        builder.Property(pc => pc.CharacterId)
            .HasMaxLength(50)
            .IsRequired();
        
        // 商品 ID（可选）
        builder.Property(pc => pc.ShopItemId)
            .HasMaxLength(100);
        
        // 冷却结束时间
        builder.Property(pc => pc.CooldownUntil)
            .IsRequired();
        
        // 创建时间
        builder.Property(pc => pc.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // 索引
        builder.HasIndex(pc => pc.CharacterId)
            .HasDatabaseName("idx_purchase_cooldowns_character_id");
        
        builder.HasIndex(pc => pc.CooldownUntil)
            .HasDatabaseName("idx_purchase_cooldowns_cooldown_until");
        
        builder.HasIndex(pc => new { pc.CharacterId, pc.ShopItemId })
            .HasDatabaseName("idx_purchase_cooldowns_character_item");
    }
}
