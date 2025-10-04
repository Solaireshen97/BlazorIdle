namespace BlazorIdle.Server.Domain.Combat.Skills;

public enum InterruptReason
{
    Manual = 0,   // 玩家/逻辑主动取消
    Stun = 1,     // 眩晕
    Silence = 2,  // 沉默
    Move = 3,     // 移动/位移导致中断（若你的设计限制站桩施法）
    Other = 9
}