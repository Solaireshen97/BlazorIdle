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
    }
}