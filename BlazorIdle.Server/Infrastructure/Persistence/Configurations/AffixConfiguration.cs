using System.Text.Json;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 词条定义实体配置
/// </summary>
public class AffixConfiguration : IEntityTypeConfiguration<Affix>
{
    public void Configure(EntityTypeBuilder<Affix> builder)
    {
        builder.ToTable("affixes");
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Id)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(a => a.StatType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(a => a.ModifierType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(a => a.ValueMin)
            .IsRequired();
        
        builder.Property(a => a.ValueMax)
            .IsRequired();
        
        builder.Property(a => a.RarityWeight)
            .IsRequired();
        
        builder.Property(a => a.AllowedSlots)
            .HasColumnName("allowed_slots_json")
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                v => v != null ? JsonSerializer.Deserialize<List<EquipmentSlot>>(v, (JsonSerializerOptions?)null) : null
            )
            .HasColumnType("TEXT");
        
        builder.Property(a => a.CreatedAt)
            .IsRequired();
        
        builder.Property(a => a.UpdatedAt)
            .IsRequired();
        
        // 索引
        builder.HasIndex(a => a.StatType);
        builder.HasIndex(a => a.ModifierType);
    }
}
