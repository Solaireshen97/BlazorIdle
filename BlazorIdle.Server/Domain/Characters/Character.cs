using BlazorIdle.Shared.Models;
using System;

namespace BlazorIdle.Server.Domain.Characters;

public class Character
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profession Profession { get; set; } = Profession.Warrior; // 新增
}