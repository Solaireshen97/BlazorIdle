using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

[ApiController]                       // 启用自动模型绑定、验证与 400 响应行为
[Route("api/[controller]")]           // 路由: /api/characters
public class CharactersController : ControllerBase
{
    private readonly GameDbContext _db;

    // 通过依赖注入获取 EF Core 上下文
    public CharactersController(GameDbContext db) => _db = db;

    /// <summary>
    /// 创建角色（最小 DTO -> 领域实体）
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateCharacterDto dto)
    {
        // 领域实体最小化创建（此处没有业务规则校验，仅 Demo）
        var c = new Character
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Profession = dto.Profession,
            Level = 1          // 默认初始等级
        };

        _db.Characters.Add(c);          // 跟踪实体
        await _db.SaveChangesAsync();   // 生成 INSERT
        // 返回最小投影（避免把所有字段暴露给前端）
        return Ok(new { c.Id, c.Name });
    }

    /// <summary>
    /// 查询单个角色（按 Id）
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        // 使用 FirstOrDefaultAsync 而非 FindAsync：可附加 Include / 条件
        var c = await _db.Characters.FirstOrDefaultAsync(x => x.Id == id);
        return c is null
            ? NotFound()
            : Ok(new { c.Id, c.Name, c.Level });
    }
}

/// <summary>
/// 输入 DTO（防止直接暴露领域实体）
/// </summary>
public record CreateCharacterDto(string Name, Profession Profession);