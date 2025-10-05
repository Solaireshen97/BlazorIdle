using BlazorIdle.Shared.Models;
using System;

namespace BlazorIdle.Server.Domain.Characters;

public class Character
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profession Profession { get; set; } = Profession.Warrior;

    // 主属性（持久化）
    public int Strength { get; set; } = 10;
    public int Agility { get; set; } = 10;
    public int Intellect { get; set; } = 10;
    public int Stamina { get; set; } = 10;

    // 新增：离线相关打点（可选）
    public DateTime? LastSeenAtUtc { get; set; }          // 最近在线心跳/登出时间
    public DateTime? LastOfflineSettledAtUtc { get; set; } // 最近一次离线结算时间
}