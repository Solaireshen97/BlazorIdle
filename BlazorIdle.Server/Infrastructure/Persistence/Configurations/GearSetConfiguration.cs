using System.Text.Json;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// 装备套装实体配置
/// </summary>
public class GearSetConfiguration : IEntityTypeConfiguration<GearSet>
{
    public void Configure(EntityTypeBuilder<GearSet> builder)
    {
        builder.ToTable("gear_sets");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Id)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(s => s.Pieces)
            .HasColumnName("pieces_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<string>()
            )
            .HasColumnType("TEXT")
            .IsRequired();
        
        builder.Property(s => s.Bonuses)
            .HasColumnName("bonuses_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, List<StatModifier>>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<int, List<StatModifier>>()
            )
            .HasColumnType("TEXT")
            .IsRequired();
        
        builder.Property(s => s.CreatedAt)
            .IsRequired();
        
        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
