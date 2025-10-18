using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Shared.Models;
using System;

namespace BlazorIdle.Server.Domain.Characters;

public class Character : IEntity
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// 所属用户 ID（外键）
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// 角色在用户 Roster 中的显示顺序（用于多角色管理）
    /// </summary>
    public int RosterOrder { get; set; } = 0;
    
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profession Profession { get; set; } = Profession.Warrior;

    // 主属性（持久化）
    public int Strength { get; set; } = 10;
    public int Agility { get; set; } = 10;
    public int Intellect { get; set; } = 10;
    public int Stamina { get; set; } = 10;

    // 经济属性
    public long Gold { get; set; } = 0;
    public long Experience { get; set; } = 0;

    // 新增：离线相关打点（可选）
    public DateTime? LastSeenAtUtc { get; set; }          // 最近在线心跳/登出时间
    public DateTime? LastOfflineSettledAtUtc { get; set; } // 最近一次离线结算时间
    
    // Navigation property - 所属用户
    public User? User { get; set; }
}