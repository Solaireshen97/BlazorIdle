using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class PurchaseRecordConfiguration : IEntityTypeConfiguration<PurchaseRecord>
{
    public void Configure(EntityTypeBuilder<PurchaseRecord> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CharacterId).IsRequired();
        b.Property(x => x.ShopId).HasMaxLength(100).IsRequired();
        b.Property(x => x.ShopItemId).IsRequired();
        b.Property(x => x.ItemDefinitionId).HasMaxLength(100).IsRequired();
        b.Property(x => x.ItemPaidId).HasMaxLength(100);
        
        // 索引
        b.HasIndex(x => x.CharacterId);
        b.HasIndex(x => x.ShopItemId);
        b.HasIndex(x => x.PurchasedAt);
        b.HasIndex(x => new { x.CharacterId, x.PurchasedAt });
    }
}
