using BlazorIdle.Server.Application.Battles.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly BatchSimulator _sim;

    public SimulationController(BatchSimulator sim)
    {
        _sim = sim;
    }

    // POST /api/simulation
    // body: { characterId, enemyId, enemyCount, mode: "Kills"|"Hours", value, sampleSeconds, seed? }
    [HttpPost]
    public async Task<ActionResult<SimulateResponse>> Simulate([FromBody] SimulateRequest req, CancellationToken ct)
    {
        if (req is null) return BadRequest();
        if (string.IsNullOrWhiteSpace(req.EnemyId)) req.EnemyId = "dummy";
        if (req.Value <= 0) return BadRequest("Value must be positive.");
        var result = await _sim.SimulateAsync(req, ct);
        return Ok(result);
    }
}