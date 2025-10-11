using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class GearSetConfiguration : IEntityTypeConfiguration<GearSet>
{
    public void Configure(EntityTypeBuilder<GearSet> builder)
    {
        builder.ToTable("gear_sets");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Id)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(s => s.Name)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(s => s.Description)
            .HasMaxLength(500);
        
        // JSON字段存储 - Pieces
        builder.Property(s => s.Pieces)
            .HasColumnName("PiecesJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) 
                     ?? new List<string>()
            );
        
        // JSON字段存储 - Bonuses
        builder.Property(s => s.Bonuses)
            .HasColumnName("BonusesJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, List<StatModifier>>>(v, (JsonSerializerOptions?)null) 
                     ?? new Dictionary<int, List<StatModifier>>()
            );
        
        builder.Property(s => s.CreatedAt)
            .IsRequired();
        
        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
