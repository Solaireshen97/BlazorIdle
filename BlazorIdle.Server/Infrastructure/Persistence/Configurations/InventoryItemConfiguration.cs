using BlazorIdle.Server.Domain.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.ItemId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(i => i.Quantity)
            .IsRequired();
        
        builder.Property(i => i.CreatedAt)
            .IsRequired();
        
        builder.Property(i => i.UpdatedAt)
            .IsRequired();
        
        // 每个角色 + 物品 ID 唯一索引（用于快速查找和防止重复）
        builder.HasIndex(i => new { i.CharacterId, i.ItemId })
            .IsUnique();
        
        // 角色外键关系
        builder.HasOne(i => i.Character)
            .WithMany()
            .HasForeignKey(i => i.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
