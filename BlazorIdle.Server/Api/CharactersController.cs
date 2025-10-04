using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class CharactersController : ControllerBase
{
    private readonly GameDbContext _db;

    public CharactersController(GameDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateCharacterDto dto)
    {
        var (str, agi, intel, sta) = DefaultAttributesFor(dto.Profession);
        // 覆盖默认值（若 DTO 提供）
        str = dto.Strength ?? str;
        agi = dto.Agility ?? agi;
        intel = dto.Intellect ?? intel;
        sta = dto.Stamina ?? sta;

        var c = new Character
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Profession = dto.Profession,
            Level = 1,
            Strength = str,
            Agility = agi,
            Intellect = intel,
            Stamina = sta
        };

        _db.Characters.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var c = await _db.Characters.FirstOrDefaultAsync(x => x.Id == id);
        return c is null
            ? NotFound()
            : Ok(new { c.Id, c.Name, c.Level, c.Profession, c.Strength, c.Agility, c.Intellect, c.Stamina });
    }

    private static (int str, int agi, int intel, int sta) DefaultAttributesFor(Profession p)
        => p switch
        {
            Profession.Warrior => (20, 10, 5, 15),
            Profession.Ranger => (10, 20, 8, 12),
            _ => (10, 10, 10, 10)
        };
}

// 扩展创建 DTO：支持可选主属性（未传则按职业默认）
public record CreateCharacterDto(
    string Name,
    Profession Profession,
    int? Strength = null,
    int? Agility = null,
    int? Intellect = null,
    int? Stamina = null
);