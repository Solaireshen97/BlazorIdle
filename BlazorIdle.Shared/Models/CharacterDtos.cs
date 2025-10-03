using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorIdle.Shared.Models
{
    public record CreateCharacterRequest(string Name);
    public record CharacterCreated(Guid Id, string Name);

    public record BattleStartResponse(Guid BattleId);

    public record BattleSummaryDto(
        Guid Id,
        Guid CharacterId,
        int TotalDamage,
        double DurationSeconds,
        double Dps,
        int SegmentCount,
        double AttackIntervalSeconds,
        double SpecialIntervalSeconds,
        string ResourceFlow
    );

    public record BattleSegmentDto(
        double StartTime,
        double EndTime,
        int EventCount,
        int TotalDamage,
        Dictionary<string, int> DamageBySource,
        Dictionary<string, int> ResourceFlow
    );
}
