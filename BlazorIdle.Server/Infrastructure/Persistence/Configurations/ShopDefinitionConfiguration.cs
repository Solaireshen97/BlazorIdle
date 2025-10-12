using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class ShopDefinitionConfiguration : IEntityTypeConfiguration<ShopDefinition>
{
    public void Configure(EntityTypeBuilder<ShopDefinition> b)
    {
        b.ToTable("shop_definitions");
        
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(100).IsRequired();
        
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.Icon).HasMaxLength(50).HasDefaultValue("🏪");
        b.Property(x => x.Description).HasMaxLength(1000).HasDefaultValue("");
        b.Property(x => x.UnlockCondition).HasMaxLength(500);
        b.Property(x => x.IsEnabled).HasDefaultValue(true);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        
        // Navigation
        b.HasMany(x => x.Items)
            .WithOne(x => x.Shop)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        b.HasIndex(x => x.Type);
        b.HasIndex(x => x.IsEnabled);
        b.HasIndex(x => x.SortOrder);
    }
}
