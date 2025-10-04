namespace BlazorIdle.Server.Domain.Combat.Skills;

public sealed class SkillRuntime
{
    public SkillDefinition Definition { get; }
    // 单充能（传统冷却）使用
    public double NextAvailableTime { get; private set; } = 0;

    // 多充能使用
    public int Charges { get; private set; }
    public double? NextChargeReadyAt { get; private set; }
    private double _rechargeStepSeconds = 0; // 本轮充能链的步长（快照）

    public SkillRuntime(SkillDefinition def)
    {
        Definition = def;
        if (def.MaxCharges <= 1)
        {
            Charges = 1;
            NextChargeReadyAt = null;
        }
        else
        {
            Charges = def.MaxCharges; // 初始满充能
            NextChargeReadyAt = null; // 满充能不在恢复
        }
    }

    public bool IsReady(double now)
    {
        if (Definition.MaxCharges > 1) return Charges > 0;
        return now >= NextAvailableTime;
    }

    public void TickRecharge(double now)
    {
        if (Definition.MaxCharges <= 1) return;

        while (NextChargeReadyAt.HasValue && now >= NextChargeReadyAt.Value)
        {
            Charges = System.Math.Min(Definition.MaxCharges, Charges + 1);
            if (Charges < Definition.MaxCharges)
            {
                // 继续下一跳
                NextChargeReadyAt = NextChargeReadyAt.Value + _rechargeStepSeconds;
            }
            else
            {
                // 满了停止
                NextChargeReadyAt = null;
                _rechargeStepSeconds = 0;
            }
        }
    }

    // 在“开始施法”或“即时施放”时消耗（ConsumeChargeOnCast=true 的路径）
    public void ConsumeAtStart(double now, double effRechargeSeconds)
    {
        if (Definition.MaxCharges <= 1)
        {
            NextAvailableTime = now + Definition.CooldownSeconds;
            return;
        }

        Charges = System.Math.Max(0, Charges - 1);
        if (Charges == Definition.MaxCharges - 1)
        {
            // 刚从满 → 不满：启动恢复
            _rechargeStepSeconds = System.Math.Max(0.01, effRechargeSeconds);
            NextChargeReadyAt ??= now + _rechargeStepSeconds;
        }
        else if (NextChargeReadyAt is null && Charges < Definition.MaxCharges)
        {
            _rechargeStepSeconds = System.Math.Max(0.01, effRechargeSeconds);
            NextChargeReadyAt = now + _rechargeStepSeconds;
        }
    }

    // 在“施法完成”时消耗（ConsumeChargeOnCast=false 的路径）
    public void ConsumeAtComplete(double now, double effRechargeSeconds)
    {
        if (Definition.MaxCharges <= 1)
        {
            NextAvailableTime = now + Definition.CooldownSeconds;
            return;
        }

        Charges = System.Math.Max(0, Charges - 1);
        if (Charges == Definition.MaxCharges - 1)
        {
            _rechargeStepSeconds = System.Math.Max(0.01, effRechargeSeconds);
            NextChargeReadyAt ??= now + _rechargeStepSeconds;
        }
        else if (NextChargeReadyAt is null && Charges < Definition.MaxCharges)
        {
            _rechargeStepSeconds = System.Math.Max(0.01, effRechargeSeconds);
            NextChargeReadyAt = now + _rechargeStepSeconds;
        }
    }

    // 打断时返还（仅当开始时消耗过才调用）
    public void RefundOnInterrupt(double now, double effRechargeSeconds)
    {
        if (Definition.MaxCharges <= 1) return;

        Charges = System.Math.Min(Definition.MaxCharges, Charges + 1);
        if (Charges == Definition.MaxCharges)
        {
            NextChargeReadyAt = null;
            _rechargeStepSeconds = 0;
        }
        else if (NextChargeReadyAt is null)
        {
            _rechargeStepSeconds = System.Math.Max(0.01, effRechargeSeconds);
            NextChargeReadyAt = now + _rechargeStepSeconds;
        }
    }

    // 单充能在完成或即时施放后进入冷却
    public void MarkCast(double when)
    {
        if (Definition.MaxCharges <= 1)
            NextAvailableTime = when + Definition.CooldownSeconds;
        // 多充能：不处理，这由 Consume* 来管理
    }
}