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
        
        // 用户外键（可空，支持未关联用户的角色）
        b.Property(x => x.UserId);
        b.HasIndex(x => x.UserId); // 为外键添加索引，提升查询性能
        
        // 角色在用户 Roster 中的显示顺序（默认为 0）
        b.Property(x => x.RosterOrder)
            .HasDefaultValue(0);
        b.HasIndex(x => new { x.UserId, x.RosterOrder }); // 复合索引，提升按用户和顺序查询的性能

        // 主属性列（默认）
        b.Property(x => x.Strength).HasDefaultValue(10);
        b.Property(x => x.Agility).HasDefaultValue(10);
        b.Property(x => x.Intellect).HasDefaultValue(10);
        b.Property(x => x.Stamina).HasDefaultValue(10);

        // 离线字段（可空）
        b.Property(x => x.LastSeenAtUtc);
        b.Property(x => x.LastOfflineSettledAtUtc);
    }
}