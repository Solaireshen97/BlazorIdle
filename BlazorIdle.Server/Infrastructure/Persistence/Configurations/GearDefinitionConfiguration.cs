using System.Text.Json;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 装备定义实体配置
/// </summary>
public class GearDefinitionConfiguration : IEntityTypeConfiguration<GearDefinition>
{
    public void Configure(EntityTypeBuilder<GearDefinition> builder)
    {
        builder.ToTable("gear_definitions");
        
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.Id)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(g => g.Icon)
            .HasMaxLength(50);
        
        builder.Property(g => g.Slot)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(g => g.ArmorType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(g => g.WeaponType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);
        
        builder.Property(g => g.RequiredLevel)
            .IsRequired();
        
        // JSON 字段存储复杂对象
        builder.Property(g => g.BaseStats)
            .HasColumnName("base_stats_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<StatType, StatRange>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<StatType, StatRange>()
            )
            .HasColumnType("TEXT");
        
        builder.Property(g => g.AllowedAffixPool)
            .HasColumnName("allowed_affix_pool_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<string>()
            )
            .HasColumnType("TEXT");
        
        builder.Property(g => g.RarityWeights)
            .HasColumnName("rarity_weights_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<Rarity, double>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<Rarity, double>()
            )
            .HasColumnType("TEXT");
        
        builder.Property(g => g.TierMultipliers)
            .HasColumnName("tier_multipliers_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, double>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<int, double> { { 1, 0.8 }, { 2, 1.0 }, { 3, 1.2 } }
            )
            .HasColumnType("TEXT");
        
        builder.Property(g => g.SetId)
            .HasMaxLength(100);
        
        builder.Property(g => g.CreatedAt)
            .IsRequired();
        
        builder.Property(g => g.UpdatedAt)
            .IsRequired();
        
        // 索引
        builder.HasIndex(g => g.Slot);
        builder.HasIndex(g => g.SetId);
        builder.HasIndex(g => g.RequiredLevel);
    }
}
