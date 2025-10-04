using BlazorIdle.Server.Domain.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();

        // 主属性列（简单限制与默认）
        b.Property(x => x.Strength).HasDefaultValue(10);
        b.Property(x => x.Agility).HasDefaultValue(10);
        b.Property(x => x.Intellect).HasDefaultValue(10);
        b.Property(x => x.Stamina).HasDefaultValue(10);
    }
}