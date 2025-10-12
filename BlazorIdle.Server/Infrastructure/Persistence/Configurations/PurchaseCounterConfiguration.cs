using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class PurchaseCounterConfiguration : IEntityTypeConfiguration<PurchaseCounter>
{
    public void Configure(EntityTypeBuilder<PurchaseCounter> b)
    {
        b.ToTable("purchase_counters");
        
        b.HasKey(x => x.Id);
        
        b.Property(x => x.CharacterId).IsRequired();
        b.Property(x => x.ShopItemId).IsRequired();
        b.Property(x => x.PeriodKey).HasMaxLength(50).IsRequired();
        b.Property(x => x.PurchaseCount).HasDefaultValue(0);
        b.Property(x => x.LastPurchaseAt).IsRequired();
        b.Property(x => x.ExpiresAt);
        
        // Unique index for compound key
        b.HasIndex(x => new { x.CharacterId, x.ShopItemId, x.PeriodKey }).IsUnique();
        
        // Indexes for cleanup queries
        b.HasIndex(x => x.ExpiresAt);
    }
}
