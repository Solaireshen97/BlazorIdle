using BlazorIdle.Server.Domain.Combat;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public interface IProfessionModule
{
    string Id { get; }
    double BaseAttackInterval { get; }
    double BaseSpecialInterval { get; }

    void OnBattleStart(BattleContext context);
    void OnAttackTick(BattleContext context, AttackTickEvent evt);
    void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt);
}