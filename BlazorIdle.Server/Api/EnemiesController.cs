using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class EnemiesController : ControllerBase
{
    // 最小可用：当前枚举常用敌人 ID。后续可改为 EnemyRegistry 暴露全量列表。
    private static readonly string[] KnownIds = new[] { "dummy", "tank", "magebane", "paper" };

    [HttpGet]
    public ActionResult<IEnumerable<EnemyDto>> GetAll()
    {
        var list = new List<EnemyDto>();
        foreach (var id in KnownIds)
        {
            var def = EnemyRegistry.Resolve(id);
            list.Add(new EnemyDto
            {
                Id = def.Id,
                Name = def.Name,
                Level = def.Level,
                MaxHp = def.MaxHp,
                Armor = def.Armor,
                MagicResist = def.MagicResist
            });
        }
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