namespace BlazorIdle.Models;

/// <summary>
/// 前端进度条配置模型
/// </summary>
public class ProgressBarConfig
{
    public ProgressBarSettings ProgressBar { get; set; } = new();
    public JITPollingSettings JITPolling { get; set; } = new();
    public HPAnimationSettings HPAnimation { get; set; } = new();
    public DebugSettings Debug { get; set; } = new();
}

/// <summary>
/// 进度条显示和计算设置
/// </summary>
public class ProgressBarSettings
{
    /// <summary>启用进度条循环滚动（达到100%后继续基于interval循环）</summary>
    public bool EnableLoopingProgress { get; set; } = true;
    
    /// <summary>动画刷新间隔（毫秒）</summary>
    public int AnimationIntervalMs { get; set; } = 100;
    
    /// <summary>循环进度的最小有效间隔（秒）</summary>
    public double MinIntervalForLooping { get; set; } = 0.1;
    
    /// <summary>循环进度的最大有效间隔（秒）</summary>
    public double MaxIntervalForLooping { get; set; } = 100.0;
    
    /// <summary>启用基于 AttackTick 事件的进度条同步</summary>
    public bool EnableSyncOnAttackTick { get; set; } = true;
    
    /// <summary>启用基于 SkillCast 事件的进度条同步</summary>
    public bool EnableSyncOnSkillCast { get; set; } = true;
    
    /// <summary>启用基于 DamageApplied 事件的即时血量更新</summary>
    public bool EnableSyncOnDamageApplied { get; set; } = true;
}

/// <summary>
/// JIT即时轮询配置
/// </summary>
public class JITPollingSettings
{
    /// <summary>启用JIT即时轮询机制</summary>
    public bool EnableJITPolling { get; set; } = true;
    
    /// <summary>触发点前的时间窗口（毫秒）</summary>
    public int TriggerWindowMs { get; set; } = 150;
    
    /// <summary>最小预测时间（毫秒）</summary>
    public int MinPredictionTimeMs { get; set; } = 100;
    
    /// <summary>每个攻击周期最多尝试JIT轮询次数</summary>
    public int MaxJITAttemptsPerCycle { get; set; } = 1;
    
    /// <summary>启用自适应轮询（根据战斗状态动态调整）</summary>
    public bool AdaptivePollingEnabled { get; set; } = true;
    
    /// <summary>最小轮询间隔（毫秒）</summary>
    public int MinPollingIntervalMs { get; set; } = 200;
    
    /// <summary>最大轮询间隔（毫秒）</summary>
    public int MaxPollingIntervalMs { get; set; } = 2000;
    
    /// <summary>血量危急阈值</summary>
    public double HealthCriticalThreshold { get; set; } = 0.3;
    
    /// <summary>血量偏低阈值</summary>
    public double HealthLowThreshold { get; set; } = 0.5;
    
    /// <summary>血量危急时的轮询间隔（毫秒）</summary>
    public int CriticalHealthPollingMs { get; set; } = 500;
    
    /// <summary>血量偏低时的轮询间隔（毫秒）</summary>
    public int LowHealthPollingMs { get; set; } = 1000;
    
    /// <summary>正常状态下的轮询间隔（毫秒）</summary>
    public int NormalPollingMs { get; set; } = 2000;
}

/// <summary>
/// HP条动画设置
/// </summary>
public class HPAnimationSettings
{
    /// <summary>默认过渡动画时长（毫秒）</summary>
    public int TransitionDurationMs { get; set; } = 120;
    
    /// <summary>CSS过渡函数</summary>
    public string TransitionTimingFunction { get; set; } = "linear";
    
    /// <summary>启用平滑过渡效果</summary>
    public bool EnableSmoothTransition { get; set; } = true;
    
    /// <summary>玩家HP条过渡时长（毫秒）</summary>
    public int PlayerHPTransitionMs { get; set; } = 120;
    
    /// <summary>敌人HP条过渡时长（毫秒）</summary>
    public int EnemyHPTransitionMs { get; set; } = 120;
}

/// <summary>
/// 调试设置
/// </summary>
public class DebugSettings
{
    /// <summary>记录进度计算详情</summary>
    public bool LogProgressCalculations { get; set; } = false;
    
    /// <summary>记录JIT轮询触发事件</summary>
    public bool LogJITPollingEvents { get; set; } = false;
    
    /// <summary>在UI中显示调试信息</summary>
    public bool ShowProgressDebugInfo { get; set; } = false;
}
