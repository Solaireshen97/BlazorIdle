using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem>
{
    public void Configure(EntityTypeBuilder<ShopItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ShopId).HasMaxLength(100).IsRequired();
        b.Property(x => x.ItemDefinitionId).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.Icon).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.PriceJson).HasMaxLength(1000).IsRequired();
        b.Property(x => x.PurchaseLimitJson).HasMaxLength(500);
        b.Property(x => x.UnlockCondition).HasMaxLength(500);
        b.Property(x => x.IsEnabled).HasDefaultValue(true);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.StockLimit).HasDefaultValue(-1);
        b.Property(x => x.CurrentStock).HasDefaultValue(-1);
        
        // 索引
        b.HasIndex(x => x.ShopId);
        b.HasIndex(x => x.ItemType);
        b.HasIndex(x => x.IsEnabled);
        b.HasIndex(x => x.SortOrder);
        b.HasIndex(x => new { x.ShopId, x.SortOrder });
    }
}
