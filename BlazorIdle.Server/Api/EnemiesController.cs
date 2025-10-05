using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class EnemiesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<EnemyDto>> GetAll()
    {
        // 标准化：直接枚举 EnemyRegistry 全量定义
        var list = EnemyRegistry.All()
            .Select(def => new EnemyDto
            {
                Id = def.Id,
                Name = def.Name,
                Level = def.Level,
                MaxHp = def.MaxHp,
                Armor = def.Armor,
                MagicResist = def.MagicResist
            })
            .ToList();
        return Ok(list);
    }

    [HttpGet("{id}")]
    public ActionResult<EnemyDto> GetOne(string id)
    {
        var def = EnemyRegistry.Resolve(id);
        return Ok(new EnemyDto
        {
            Id = def.Id,
            Name = def.Name,
            Level = def.Level,
            MaxHp = def.MaxHp,
            Armor = def.Armor,
            MagicResist = def.MagicResist
        });
    }

    public sealed class EnemyDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public double Armor { get; set; }
        public double MagicResist { get; set; }
    }
}