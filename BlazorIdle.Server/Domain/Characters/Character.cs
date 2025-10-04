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

    // 新增：主属性（持久化）
    public int Strength { get; set; } = 10;
    public int Agility { get; set; } = 10;
    public int Intellect { get; set; } = 10;
    public int Stamina { get; set; } = 10;
}