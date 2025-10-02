using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
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
        var c = new Character { Id = Guid.NewGuid(), Name = dto.Name, Level = 1 };
        _db.Characters.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var c = await _db.Characters.FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : Ok(new { c.Id, c.Name, c.Level });
    }
}

public record CreateCharacterDto(string Name);