namespace BlazorIdle.Server.Domain.Combat.Procs;

public enum ProcTriggerType
{
    OnHit = 0,   // 命中（普攻/技能的直接伤害，不含 DoT，除非另行允许）
    OnCrit = 1,  // 暴击命中
    Rppm = 2     // RPPM（按时间评估）
}