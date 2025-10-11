using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class GearInstanceConfiguration : IEntityTypeConfiguration<GearInstance>
{
    public void Configure(EntityTypeBuilder<GearInstance> builder)
    {
        builder.ToTable("gear_instances");
        
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.DefinitionId)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(g => g.CharacterId);
        
        builder.Property(g => g.SlotType)
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(g => g.Rarity)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        
        builder.Property(g => g.TierLevel)
            .IsRequired();
        
        builder.Property(g => g.ItemLevel)
            .IsRequired();
        
        // JSON字段存储 - RolledStats
        builder.Property(g => g.RolledStats)
            .HasColumnName("RolledStatsJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<StatType, double>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<StatType, double>()
            )
            .IsRequired();
        
        // JSON字段存储 - Affixes
        builder.Property(g => g.Affixes)
            .HasColumnName("AffixesJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AffixInstance>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<AffixInstance>()
            )
            .IsRequired();
        
        builder.Property(g => g.QualityScore)
            .IsRequired();
        
        builder.Property(g => g.IsEquipped)
            .IsRequired();
        
        builder.Property(g => g.IsBound)
            .IsRequired();
        
        builder.Property(g => g.RerollCount)
            .IsRequired();
        
        builder.Property(g => g.CreatedAt)
            .IsRequired();
        
        builder.Property(g => g.UpdatedAt)
            .IsRequired();
        
        // 外键关系
        builder.HasOne(g => g.Character)
            .WithMany()
            .HasForeignKey(g => g.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        
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
