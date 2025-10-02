using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Data;
using BlazorIdle.Server.Models;

namespace BlazorIdle.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameDataController : ControllerBase
{
    private readonly GameDbContext _context;
    private readonly ILogger<GameDataController> _logger;

    public GameDataController(GameDbContext context, ILogger<GameDataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameData>>> GetAllGameData()
    {
        try
        {
            return await _context.GameData.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GameData>> GetGameData(int id)
    {
        try
        {
            var gameData = await _context.GameData.FindAsync(id);

            if (gameData == null)
            {
                return NotFound();
            }

            return gameData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game data with id {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<GameData>> CreateGameData(GameData gameData)
    {
        try
        {
            gameData.LastUpdated = DateTime.UtcNow;
            _context.GameData.Add(gameData);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetGameData), new { id = gameData.Id }, gameData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating game data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGameData(int id, GameData gameData)
    {
        if (id != gameData.Id)
        {
            return BadRequest();
        }

        try
        {
            gameData.LastUpdated = DateTime.UtcNow;
            _context.Entry(gameData).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await GameDataExists(id))
            {
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game data with id {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGameData(int id)
    {
        try
        {
            var gameData = await _context.GameData.FindAsync(id);
            if (gameData == null)
            {
                return NotFound();
            }

            _context.GameData.Remove(gameData);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting game data with id {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<bool> GameDataExists(int id)
    {
        return await _context.GameData.AnyAsync(e => e.Id == id);
    }
}
