using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class GearDefinitionConfiguration : IEntityTypeConfiguration<GearDefinition>
{
    public void Configure(EntityTypeBuilder<GearDefinition> builder)
    {
        builder.ToTable("gear_definitions");
        
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.Id)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(g => g.Name)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(g => g.Icon)
            .HasMaxLength(50);
        
        builder.Property(g => g.Slot)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        
        builder.Property(g => g.ArmorType)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(g => g.WeaponType)
            .HasConversion<string>()
            .HasMaxLength(30);
        
        // JSON字段存储 - BaseStats
        builder.Property(g => g.BaseStats)
            .HasColumnName("BaseStatsJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<StatType, StatRange>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<StatType, StatRange>()
            );
        
        // JSON字段存储 - AllowedAffixPool
        builder.Property(g => g.AllowedAffixPool)
            .HasColumnName("AllowedAffixPoolJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<string>()
            );
        
        // JSON字段存储 - RarityWeights
        builder.Property(g => g.RarityWeights)
            .HasColumnName("RarityWeightsJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<Rarity, double>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<Rarity, double>()
            );
        
        builder.Property(g => g.SetId)
            .HasMaxLength(100);
        
        builder.Property(g => g.CreatedAt)
            .IsRequired();
        
        builder.Property(g => g.UpdatedAt)
            .IsRequired();
        
        // 索引
        builder.HasIndex(g => g.Slot);
        builder.HasIndex(g => g.SetId);
    }
}
