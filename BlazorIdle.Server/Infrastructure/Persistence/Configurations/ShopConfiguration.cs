using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// ShopDefinition 实体配置
/// </summary>
public class ShopDefinitionConfiguration : IEntityTypeConfiguration<ShopDefinition>
{
    public void Configure(EntityTypeBuilder<ShopDefinition> builder)
    {
        builder.ToTable("shop_definitions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.Icon)
            .HasMaxLength(50);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.UnlockCondition)
            .HasMaxLength(500);

        builder.Property(s => s.IsEnabled)
            .IsRequired();

        builder.Property(s => s.SortOrder)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // 索引
        builder.HasIndex(s => s.Type);
        builder.HasIndex(s => s.IsEnabled);
        builder.HasIndex(s => s.SortOrder);

        // 关系
        builder.HasMany(s => s.Items)
            .WithOne(i => i.Shop)
            .HasForeignKey(i => i.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// ShopItem 实体配置
/// </summary>
public class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem>
{
    public void Configure(EntityTypeBuilder<ShopItem> builder)
    {
        builder.ToTable("shop_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ShopId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.ItemDefinitionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.ItemName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.ItemIcon)
            .HasMaxLength(50);

        builder.Property(i => i.PriceJson)
            .IsRequired();

        builder.Property(i => i.PurchaseLimitJson)
            .IsRequired();

        builder.Property(i => i.StockQuantity)
            .IsRequired();

        builder.Property(i => i.MinLevel)
            .IsRequired();

        builder.Property(i => i.IsEnabled)
            .IsRequired();

        builder.Property(i => i.SortOrder)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .IsRequired();

        // 索引
        builder.HasIndex(i => i.ShopId);
        builder.HasIndex(i => i.ItemDefinitionId);
        builder.HasIndex(i => i.IsEnabled);
        builder.HasIndex(i => i.SortOrder);
    }
}

/// <summary>
/// PurchaseRecord 实体配置
/// </summary>
public class PurchaseRecordConfiguration : IEntityTypeConfiguration<PurchaseRecord>
{
    public void Configure(EntityTypeBuilder<PurchaseRecord> builder)
    {
        builder.ToTable("purchase_records");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CharacterId)
            .IsRequired();

        builder.Property(p => p.ShopId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.ShopItemId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.ItemDefinitionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Quantity)
            .IsRequired();

        builder.Property(p => p.PriceJson)
            .IsRequired();

        builder.Property(p => p.PurchasedAt)
            .IsRequired();

        builder.Property(p => p.EconomyEventId)
            .HasMaxLength(100);

        // 索引
        builder.HasIndex(p => p.CharacterId);
        builder.HasIndex(p => p.ShopId);
        builder.HasIndex(p => p.ShopItemId);
        builder.HasIndex(p => p.PurchasedAt);
    }
}

/// <summary>
/// PurchaseCounter 实体配置
/// </summary>
public class PurchaseCounterConfiguration : IEntityTypeConfiguration<PurchaseCounter>
{
    public void Configure(EntityTypeBuilder<PurchaseCounter> builder)
    {
        builder.ToTable("purchase_counters");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CharacterId)
            .IsRequired();

        builder.Property(c => c.ShopItemId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.PurchaseCount)
            .IsRequired();

        builder.Property(c => c.PeriodStartAt)
            .IsRequired();

        builder.Property(c => c.LastPurchasedAt)
            .IsRequired();

        // 索引
        builder.HasIndex(c => c.CharacterId);
        builder.HasIndex(c => c.ShopItemId);
        builder.HasIndex(c => new { c.CharacterId, c.ShopItemId });
    }
}
