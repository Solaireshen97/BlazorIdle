using System;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public interface IEncounterProvider
{
    // 当前敌群
    EncounterGroup CurrentGroup { get; }

    // 当前波次（连续模式固定为 1）
    int CurrentWaveIndex { get; }

    // 已完成的地城轮次（连续模式为 0）
    int CompletedRunCount { get; }

    // 当一波敌群被清空时调用，返回是否还有下一波/下一轮
    // nextGroup: 下一波的敌群；runCompleted: 刚刚是否完成了一整轮地城
    bool TryAdvance(out EncounterGroup? nextGroup, out bool runCompleted);
}