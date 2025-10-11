using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class AffixConfiguration : IEntityTypeConfiguration<Affix>
{
    public void Configure(EntityTypeBuilder<Affix> builder)
    {
        builder.ToTable("affixes");
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Id)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(a => a.StatType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        
        builder.Property(a => a.ModifierType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        
        builder.Property(a => a.ValueMin)
            .IsRequired();
        
        builder.Property(a => a.ValueMax)
            .IsRequired();
        
        builder.Property(a => a.RarityWeight)
            .IsRequired();
        
        // JSON字段存储 - AllowedSlots
        builder.Property(a => a.AllowedSlots)
            .HasColumnName("AllowedSlotsJson")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<List<EquipmentSlot>>(v, (JsonSerializerOptions?)null)
            );
        
        builder.Property(a => a.CreatedAt)
            .IsRequired();
        
        builder.Property(a => a.UpdatedAt)
            .IsRequired();
        
        // 索引
        builder.HasIndex(a => a.StatType);
    }
}
