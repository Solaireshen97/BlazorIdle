using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class PurchaseCounterConfiguration : IEntityTypeConfiguration<PurchaseCounter>
{
    public void Configure(EntityTypeBuilder<PurchaseCounter> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CharacterId).IsRequired();
        b.Property(x => x.ShopItemId).IsRequired();
        b.Property(x => x.PurchaseCount).HasDefaultValue(0);
        
        // 索引
        b.HasIndex(x => new { x.CharacterId, x.ShopItemId, x.PeriodStart });
        b.HasIndex(x => x.PeriodEnd);
    }
}
