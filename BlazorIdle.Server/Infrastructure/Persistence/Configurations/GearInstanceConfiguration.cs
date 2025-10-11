using System.Text.Json;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 装备实例实体配置
/// </summary>
public class GearInstanceConfiguration : IEntityTypeConfiguration<GearInstance>
{
    public void Configure(EntityTypeBuilder<GearInstance> builder)
    {
        builder.ToTable("gear_instances");
        
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.DefinitionId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(g => g.CharacterId);
        
        builder.Property(g => g.SlotType)
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(g => g.Rarity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(g => g.TierLevel)
            .IsRequired();
        
        builder.Property(g => g.ItemLevel)
            .IsRequired();
        
        // JSON 字段存储复杂对象
        builder.Property(g => g.RolledStats)
            .HasColumnName("rolled_stats_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<StatType, double>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<StatType, double>()
            )
            .HasColumnType("TEXT")
            .IsRequired();
        
        builder.Property(g => g.Affixes)
            .HasColumnName("affixes_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AffixInstance>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<AffixInstance>()
            )
            .HasColumnType("TEXT")
            .IsRequired();
        
        builder.Property(g => g.QualityScore)
            .IsRequired();
        
        builder.Property(g => g.SetId)
            .HasMaxLength(100);
        
        builder.Property(g => g.IsEquipped)
            .IsRequired();
        
        builder.Property(g => g.IsBound)
            .IsRequired();
        
        builder.Property(g => g.CreatedAt)
            .IsRequired();
        
        builder.Property(g => g.UpdatedAt)
            .IsRequired();
        
        // 外键关系
        builder.HasOne(g => g.Definition)
            .WithMany()
            .HasForeignKey(g => g.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // 索引
        builder.HasIndex(g => g.CharacterId);
        builder.HasIndex(g => g.IsEquipped);
        builder.HasIndex(g => g.Rarity);
        builder.HasIndex(g => g.DefinitionId);
        builder.HasIndex(g => new { g.CharacterId, g.SlotType });
    }
}
