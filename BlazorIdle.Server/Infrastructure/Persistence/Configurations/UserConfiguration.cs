using BlazorIdle.Server.Domain.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

/// <summary>
/// User 实体的 EF Core 配置，定义表结构、索引和关系映射。
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        
        builder.HasKey(u => u.Id);
        
        // 用户名：必填，唯一，最大长度 64
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(64);
        
        builder.HasIndex(u => u.Username)
            .IsUnique();
        
        // 电子邮箱：必填，唯一，最大长度 256
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);
        
        builder.HasIndex(u => u.Email)
            .IsUnique();
        
        // 密码哈希：必填，最大长度 256（BCrypt 哈希通常为 60 字符，预留空间）
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);
        
        // 时间戳字段
        builder.Property(u => u.CreatedAt)
            .IsRequired();
        
        builder.Property(u => u.UpdatedAt)
            .IsRequired();
        
        builder.Property(u => u.LastLoginAt);
        
        // 配置与 Character 的一对多关系
        builder.HasMany(u => u.Characters)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull); // 删除用户时，角色的 UserId 设为 NULL（保留孤立角色）
    }
}
