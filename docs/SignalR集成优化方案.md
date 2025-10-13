# BlazorIdle SignalR 实时通知集成优化方案

**版本**: 1.0  
**日期**: 2025-10-13  
**状态**: 需求分析与设计方案  
**目标**: 为前端提供实时战斗事件通知，优化用户体验

---

## 📋 目录

1. [当前系统架构分析](#1-当前系统架构分析)
2. [需求背景与目标](#2-需求背景与目标)
3. [事件分类分析](#3-事件分类分析)
4. [SignalR 集成方案（上篇）](#4-signalr-集成方案上篇)
5. [SignalR 集成方案（中篇）](#5-signalr-集成方案中篇)
6. [SignalR 集成方案（下篇）](#6-signalr-集成方案下篇)
7. [验收标准](#7-验收标准)
8. [附录](#8-附录)

---

## 1. 当前系统架构分析

### 1.1 已完成的核心功能

#### ✅ 后端战斗系统
- **事件驱动架构**: 基于 `IGameEvent` 接口和 `EventScheduler` 优先队列
- **双轨战斗**: `AttackTrack` + `SpecialTrack`
- **玩家死亡与复活**: `PlayerDeathEvent` / `PlayerReviveEvent`
- **怪物击杀**: `Encounter.ApplyDamage()` → 死亡标记 → `kill.{enemyId}` tag
- **目标切换**: `TryRetargetPrimaryIfDead()` → `retarget_primary` tag
- **波次刷新**: `TryScheduleNextWaveIfCleared()` → `spawn_scheduled` tag
- **技能系统**: `SkillCastCompleteEvent` / `SkillCastInterruptEvent`
- **Buff 管理**: `BuffManager` + `BuffInstance`

#### ✅ 前端轮询系统  
- **统一轮询协调器**: `BattlePollingCoordinator`（2025-10-10 已完成）
- **智能轮询间隔**: 根据战斗状态动态调整
- **服务器轮询提示**: `PollingHint` 类（含 `SuggestedIntervalMs` / `NextSignificantEventAt` / `IsStable`）
- **自适应轮询策略**:
  - 战斗完成: 5000ms (稳定)
  - 玩家死亡: 2000ms (稳定)
  - 玩家血量 <50%: 1000ms (激烈，不稳定)
  - 正常战斗: 2000ms (稳定)

#### ✅ 现有 API 端点
- `StepBattlesController`: 战斗状态查询
- `CharactersController`: 角色管理
- `InventoryController`: 背包管理
- `EquipmentController`: 装备管理
- `ShopController`: 商店系统

### 1.2 现有问题识别

| 问题类型 | 具体表现 | 影响 |
|---------|---------|------|
| **延迟感知** | 玩家死亡需等待下次轮询才能感知（最多 2s 延迟） | 进度条动画与实际状态不同步 |
| **资源浪费** | 即使战斗状态稳定，前端仍需固定间隔轮询 | 不必要的网络请求和服务器负载 |
| **用户体验差** | 怪物死亡、目标切换等关键事件无法及时反馈 | 用户感受不到准确的战斗节奏 |
| **进度条中断问题** | 前端进度条基于固定速度模拟，突发事件会导致错位 | 视觉不连贯，用户困惑 |

---

## 2. 需求背景与目标

### 2.1 核心需求

> **目标**: 前端能够自然地模拟进度条进度推进，如果怪物或者玩家死亡会及时打断或者重置当前的进度，让前端用户能够感受到准确的战斗状态。

### 2.2 具体期望

1. **实时突发事件通知**: 玩家死亡、怪物死亡、目标切换等无法预测的事件，通过 SignalR 主动推送
2. **配合自适应轮询**: SignalR 通知前端立即抓取状态，而非等待下次轮询
3. **渐进式细化**: 初期通知"状态变更"触发立即轮询，后期细化为具体事件数据
4. **进度条精准同步**: 突发事件立即打断进度条，重新校准动画

### 2.3 设计原则

- **向后兼容**: 不破坏现有轮询机制，SignalR 作为增强功能
- **渐进式实施**: 分阶段交付，每阶段可独立验证
- **性能优先**: 避免过度通知，仅关键事件使用 SignalR
- **可观测性**: 增加日志和指标，便于调试和监控

---

## 3. 事件分类分析

### 3.1 事件分类决策模型

| 特征维度 | SignalR 通知 | 仅轮询 |
|---------|-------------|--------|
| **时间可预测性** | ❌ 不可预测 | ✅ 可预测 |
| **用户体验影响** | ⚠️ 高（需要立即响应） | ✅ 低（可容忍延迟） |
| **发生频率** | ✅ 低频（秒级/分钟级） | ⚠️ 高频（毫秒级） |
| **状态变化幅度** | ⚠️ 重大状态转换 | ✅ 渐进式变化 |

### 3.2 需要 SignalR 通知的事件 ⚡

#### 🔴 高优先级（Phase 1 必须实现）

| 事件类型 | 服务器端来源 | 前端影响 | 通知频率 |
|---------|------------|---------|---------|
| **玩家死亡** | `PlayerDeathEvent.Execute()` | 立即停止所有进度条，显示死亡状态 | 低频 (分钟级) |
| **玩家复活** | `PlayerReviveEvent.Execute()` | 重置进度条，恢复战斗状态 | 低频 (分钟级) |
| **怪物死亡** | `CaptureNewDeaths()` → `kill.{enemyId}` tag | 当前目标进度条完成，切换新目标 | 中频 (10秒级) |
| **目标切换** | `TryRetargetPrimaryIfDead()` → `retarget_primary` tag | 重置当前攻击进度条到0 | 中频 (10秒级) |

#### 🟡 中优先级（Phase 2 优化体验）

| 事件类型 | 服务器端来源 | 前端影响 | 通知频率 |
|---------|------------|---------|---------|
| **波次刷新** | `TryScheduleNextWaveIfCleared()` → `spawn_scheduled` tag | 清空旧怪物列表，准备刷新倒计时 | 低频 (分钟级) |
| **新波次出现** | `TryPerformPendingSpawn()` → `ResetEncounterGroup()` | 显示新怪物组，重置战斗UI | 低频 (分钟级) |
| **副本完成** | `dungeon_run_complete` tag | 显示奖励面板，停止战斗 | 低频 (分钟级) |
| **战斗结束** | `Battle.Finish()` | 停止所有轮询，显示战斗总结 | 低频 (一次性) |

#### 🟢 低优先级（Phase 3 高级功能）

| 事件类型 | 服务器端来源 | 前端影响 | 通知频率 |
|---------|------------|---------|---------|
| **技能施放** | `SkillCastCompleteEvent` | 播放技能动画，显示技能名称 | 中高频 (秒级) |
| **重要 Buff 获得/失效** | `BuffManager.Add/Remove` | 更新 Buff 图标栏 | 中频 (10秒级) |
| **暴击触发** | `DamageCalculator.ApplyCrit()` | 播放暴击特效 | 高频 (需节流) |

### 3.3 仅需轮询的事件 🔄

| 事件类型 | 原因 | 轮询间隔建议 |
|---------|------|------------|
| **血量渐进式变化** | 高频事件，SignalR 会造成通信开销 | 500-2000ms (根据战斗激烈程度) |
| **资源值变化** (怒气/能量等) | 持续变化，前端可预测插值 | 2000ms |
| **攻击进度推进** | 前端基于 `NextSignificantEventAt` 自行模拟 | 无需轮询（定时器驱动） |
| **经验值增长** | 低优先级信息，容忍延迟 | 5000ms |
| **金币/物品掉落** | 非紧急信息，战斗结束后查询即可 | 战斗结束时 |
| **统计数据** (DPS/伤害总量) | 仅用于展示，非实时需求 | 2000ms |

### 3.4 决策依据总结

#### ✅ 使用 SignalR 的条件（需同时满足）:
1. **不可预测性**: 前端无法通过现有信息预测事件发生时间
2. **低频发生**: 每秒触发次数 ≤ 1次（避免通信开销）
3. **状态突变**: 引起战斗流程重大变化（死亡/复活/目标切换）
4. **体验关键**: 延迟会显著降低用户体验

#### ❌ 不使用 SignalR 的条件（满足任一）:
1. **高频事件**: 每秒触发 >1次（如普通攻击伤害）
2. **可预测性**: 前端可通过 `NextSignificantEventAt` 预测
3. **渐进式变化**: 状态连续变化（如血量持续下降）
4. **非关键信息**: 延迟不影响核心体验（如统计数据）

---

## 4. SignalR 集成方案（上篇）

### 4.1 Phase 1: 基础架构搭建（第 1-2 周）

#### 📦 依赖引入

**服务器端**（`BlazorIdle.Server.csproj`）:
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.0.*" />
```

**客户端**（`BlazorIdle.csproj`）:
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.*" />
```

#### 🏗️ 服务器端架构

##### 1. 创建 `BattleNotificationHub`

**位置**: `BlazorIdle.Server/Hubs/BattleNotificationHub.cs`

**职责**:
- 管理客户端连接（角色 ID → ConnectionId 映射）
- 提供订阅/取消订阅战斗通知的接口
- 向指定连接推送事件通知

**核心方法**:
```csharp
public class BattleNotificationHub : Hub
{
    // 订阅特定战斗的通知
    Task SubscribeBattle(Guid battleId);
    
    // 取消订阅
    Task UnsubscribeBattle(Guid battleId);
    
    // 连接管理
    override OnConnectedAsync();
    override OnDisconnectedAsync(Exception? exception);
}
```

##### 2. 创建 `IBattleNotificationService` 接口

**位置**: `BlazorIdle.Server/Application/Abstractions/IBattleNotificationService.cs`

**职责**:
- 提供业务层调用的通知发送接口
- 解耦战斗逻辑与 SignalR 实现

**核心方法**:
```csharp
public interface IBattleNotificationService
{
    // 发送状态变更通知（初期实现）
    Task NotifyStateChange(Guid battleId, string eventType);
    
    // 发送详细事件数据（Phase 2）
    Task NotifyEvent(Guid battleId, BattleEventDto eventData);
}
```

##### 3. 实现 `BattleNotificationService`

**位置**: `BlazorIdle.Server/Application/Services/BattleNotificationService.cs`

**实现方式**:
```csharp
public class BattleNotificationService : IBattleNotificationService
{
    private readonly IHubContext<BattleNotificationHub> _hubContext;
    private readonly ILogger<BattleNotificationService> _logger;
    
    // 初期实现：发送简单通知
    public async Task NotifyStateChange(Guid battleId, string eventType)
    {
        await _hubContext.Clients
            .Group($"battle_{battleId}")
            .SendAsync("StateChanged", new { eventType, timestamp = DateTime.UtcNow });
    }
}
```

#### 🎯 前端架构

##### 1. 创建 `BattleSignalRService`

**位置**: `BlazorIdle/Services/BattleSignalRService.cs`

**职责**:
- 管理 SignalR 连接生命周期
- 订阅服务器事件并转发到前端组件
- 提供连接状态查询

**核心方法**:
```csharp
public class BattleSignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    
    // 连接到 Hub
    Task ConnectAsync();
    
    // 订阅战斗
    Task SubscribeBattleAsync(Guid battleId);
    
    // 注册事件监听器
    void OnStateChanged(Action<StateChangedEvent> handler);
    
    // 断开连接
    ValueTask DisposeAsync();
}
```

##### 2. 集成到 `BattlePollingCoordinator`

**修改点**:
```csharp
public class BattlePollingCoordinator
{
    private readonly BattleSignalRService _signalR;
    
    public void StartStepBattlePolling(Guid battleId, ...)
    {
        // 订阅 SignalR 通知
        await _signalR.SubscribeBattleAsync(battleId);
        
        // 注册事件处理器
        _signalR.OnStateChanged(async evt => 
        {
            // 收到通知后立即触发一次轮询
            await PollStepBattleStatus();
        });
        
        // 继续正常轮询作为降级方案
        ...
    }
}
```

#### 📝 数据传输对象（DTO）

##### Phase 1: 简化版本

**位置**: `BlazorIdle.Shared/Models/BattleNotifications.cs`

```csharp
// 初期通知：仅事件类型
public record StateChangedEvent(
    string EventType,        // "PlayerDeath", "EnemyKilled", "TargetSwitched"
    DateTime Timestamp
);
```

##### Phase 2: 详细版本（后续扩展）

```csharp
// 玩家死亡事件详情
public record PlayerDeathEventDto(
    Guid BattleId,
    double EventTime,
    double ReviveAt,
    string CauseOfDeath
) : BattleEventDto;

// 怪物击杀事件详情
public record EnemyKilledEventDto(
    Guid BattleId,
    double EventTime,
    string EnemyId,
    int Overkill,
    DropRewardDto[] Drops
) : BattleEventDto;
```

#### ⚙️ 依赖注入配置

**服务器端** (`Program.cs`):
```csharp
// 注册 SignalR
builder.Services.AddSignalR();

// 注册通知服务
builder.Services.AddSingleton<IBattleNotificationService, BattleNotificationService>();

// 映射 Hub 端点
app.MapHub<BattleNotificationHub>("/hubs/battle");
```

**客户端** (`Program.cs`):
```csharp
builder.Services.AddScoped<BattleSignalRService>();
```

#### 🔧 事件埋点位置

在现有事件类的 `Execute()` 方法中添加通知调用：

**示例 1: `PlayerDeathEvent.Execute()`**
```csharp
public void Execute(BattleContext context)
{
    // 原有死亡逻辑
    ...
    
    // ✨ 新增：发送 SignalR 通知
    var notifier = context.GetService<IBattleNotificationService>();
    await notifier?.NotifyStateChange(context.Battle.Id, "PlayerDeath");
}
```

**示例 2: `BattleEngine.CaptureNewDeaths()`**
```csharp
private void CaptureNewDeaths()
{
    foreach (var e in grp.All)
    {
        if (e.IsDead && !_markedDead.Contains(e))
        {
            Collector.OnTag($"kill.{e.Enemy.Id}", 1);
            _markedDead.Add(e);
            
            // ✨ 新增：发送 SignalR 通知
            _notifier?.NotifyStateChange(Battle.Id, "EnemyKilled");
        }
    }
}
```

#### ✅ Phase 1 验收标准

1. **连接管理**:
   - [ ] SignalR 连接成功建立
   - [ ] 断线后自动重连（5 次指数退避）
   - [ ] 连接状态可查询（Connected/Connecting/Disconnected）

2. **通知触发**:
   - [ ] 玩家死亡时前端收到 `StateChanged` 事件（类型: `PlayerDeath`）
   - [ ] 怪物死亡时前端收到 `StateChanged` 事件（类型: `EnemyKilled`）
   - [ ] 目标切换时前端收到 `StateChanged` 事件（类型: `TargetSwitched`）

3. **前端响应**:
   - [ ] 收到通知后立即触发一次战斗状态轮询
   - [ ] 正常轮询机制不受影响（降级保障）
   - [ ] 控制台可见 SignalR 日志（开发模式）

4. **性能指标**:
   - [ ] 通知延迟 <500ms（测试环境）
   - [ ] 不影响现有轮询性能
   - [ ] 无内存泄漏（连接/断开 100 次测试）

---

## 5. SignalR 集成方案（中篇）

### 5.1 Phase 2: 进度条精准同步（第 3-4 周）

#### 🎯 核心目标

让前端进度条能够：
1. **准确预测**: 基于 `PollingHint.NextSignificantEventAt` 平滑推进
2. **及时中断**: 收到 SignalR 通知时立即打断并重置
3. **自然过渡**: 状态变更时不会产生视觉跳变

#### 📊 前端进度条状态机

```
States:
┌──────────────┐
│   Idle       │ ← 战斗未开始/已结束
└──────┬───────┘
       │ StartBattle
       ▼
┌──────────────┐
│  Simulating  │ ← 基于 NextSignificantEventAt 模拟推进
└──┬───────┬───┘
   │       │
   │       │ SignalR: StateChanged
   │       ▼
   │  ┌──────────────┐
   │  │ Interrupted  │ ← 立即停止，等待新状态
   │  └──────┬───────┘
   │         │ Poll completed
   │         ▼
   └─────► (重新计算进度)
```

#### 🔄 进度条更新逻辑

**当前实现** (`BattlePollingCoordinator`):
```csharp
// 固定速度推进，可能与实际状态脱节
_progressAnimationTimer = new Timer(_ => 
{
    _attackProgress += 0.1 / _expectedAttackInterval;  // 固定增量
    InvokeAsync(StateHasChanged);
}, null, 0, 100);
```

**优化后实现**:
```csharp
public class ProgressBarState
{
    public double StartTime { get; set; }
    public double CurrentProgress { get; set; }  // 0.0 - 1.0
    public double? TargetEventTime { get; set; } // 来自 NextSignificantEventAt
    
    // 计算当前应显示的进度
    public double GetCurrentProgress()
    {
        if (TargetEventTime == null) return CurrentProgress;
        
        var elapsed = (DateTime.UtcNow - StartTime).TotalSeconds;
        var totalDuration = TargetEventTime.Value - _battleCurrentTime;
        
        return Math.Min(1.0, elapsed / totalDuration);
    }
    
    // 收到 SignalR 通知后重置
    public void Reset(double newTargetTime)
    {
        StartTime = DateTime.UtcNow;
        CurrentProgress = 0;
        TargetEventTime = newTargetTime;
    }
}
```

#### 🎬 动画中断处理流程

```
1. 前端正在模拟攻击进度条（基于上次 NextSignificantEventAt = 10.5s）
   当前进度: 60%，本地时间推进到 9.2s

2. 服务器端：怪物死亡（实际发生在 9.0s）
   → 触发 TryRetargetPrimaryIfDead()
   → 重置攻击进度：ResetAttackProgress()
   → 发送 SignalR 通知: "TargetSwitched"

3. 前端收到通知（延迟 ~200ms，实际时间 9.2s）
   → 立即停止进度条动画
   → 设置状态为 "Interrupted"
   → 触发立即轮询

4. 轮询返回新状态
   → 新 NextSignificantEventAt = 9.2s + 2.5s = 11.7s
   → 重置进度条从 0% 开始
   → 恢复动画，目标时间 11.7s
```

#### 📡 详细事件数据传输（Phase 2 扩展）

**扩展 DTO 定义**:
```csharp
// 基类
public abstract record BattleEventDto(
    Guid BattleId,
    double EventTime,
    string EventType
);

// 目标切换事件
public record TargetSwitchedEventDto(
    Guid BattleId,
    double EventTime,
    string NewTargetId,
    string NewTargetName,
    int NewTargetHp,
    int NewTargetMaxHp,
    double NextAttackAt
) : BattleEventDto(BattleId, EventTime, "TargetSwitched");

// 玩家死亡事件
public record PlayerDeathEventDto(
    Guid BattleId,
    double EventTime,
    double ReviveAt,
    bool AutoReviveEnabled
) : BattleEventDto(BattleId, EventTime, "PlayerDeath");
```

**服务器端发送详细数据**:
```csharp
// BattleEngine.ResetAttackProgress() 中
private void ResetAttackProgress()
{
    var attackTrack = Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
    if (attackTrack is not null)
    {
        attackTrack.NextTriggerAt = Clock.CurrentTime + attackTrack.CurrentInterval;
        Collector.OnTag("attack_progress_reset", 1);
        
        // ✨ 发送详细通知
        var newTarget = Context.Encounter;
        if (newTarget != null)
        {
            _notifier?.NotifyEvent(Battle.Id, new TargetSwitchedEventDto(
                BattleId: Battle.Id,
                EventTime: Clock.CurrentTime,
                NewTargetId: newTarget.Enemy.Id,
                NewTargetName: newTarget.Enemy.Name,
                NewTargetHp: newTarget.CurrentHp,
                NewTargetMaxHp: newTarget.Enemy.MaxHp,
                NextAttackAt: attackTrack.NextTriggerAt
            ));
        }
    }
}
```

**前端处理详细事件**:
```csharp
_signalR.OnTargetSwitched(evt => 
{
    // 立即更新 UI
    _currentTarget = evt.NewTargetName;
    _currentTargetHp = evt.NewTargetHp;
    _currentTargetMaxHp = evt.NewTargetMaxHp;
    
    // 重置进度条
    _attackProgressState.Reset(evt.NextAttackAt);
    
    // 可选：播放切换动画
    await PlayTargetSwitchAnimation();
    
    // 仍然触发一次完整轮询，确保所有状态同步
    await PollStepBattleStatus();
});
```

#### 🎨 UI 反馈增强

**1. 进度条状态指示器**:
```razor
<div class="progress-container @GetProgressStateClass()">
    <div class="progress-bar" style="width: @(_attackProgressState.GetCurrentProgress() * 100)%">
        @if (_progressState == ProgressState.Interrupted)
        {
            <span class="interrupted-indicator">⚠️</span>
        }
    </div>
</div>

@code {
    private string GetProgressStateClass() => _progressState switch
    {
        ProgressState.Simulating => "progress-normal",
        ProgressState.Interrupted => "progress-interrupted",
        _ => ""
    };
}
```

**2. 事件通知 Toast**（可选）:
```csharp
_signalR.OnPlayerDeath(evt => 
{
    _toastNotification.ShowWarning(
        title: "角色死亡",
        message: evt.AutoReviveEnabled 
            ? $"将在 {evt.ReviveAt - evt.EventTime:F1}秒后复活" 
            : "请手动复活"
    );
});
```

#### ✅ Phase 2 验收标准

1. **进度条精度**:
   - [ ] 基于 `NextSignificantEventAt` 推进，误差 <5%
   - [ ] SignalR 通知后立即中断，无视觉跳变
   - [ ] 目标切换时进度条重置到 0%

2. **事件数据完整性**:
   - [ ] `TargetSwitchedEventDto` 包含新目标完整信息
   - [ ] `PlayerDeathEventDto` 包含复活时间
   - [ ] `EnemyKilledEventDto` 包含击杀信息

3. **用户体验**:
   - [ ] 怪物死亡到前端更新延迟 <1s
   - [ ] 玩家死亡立即显示死亡状态
   - [ ] 进度条动画流畅（60 FPS）

### 5.2 Phase 2.5: 错误处理与降级策略（第 4-5 周）

#### 🛡️ SignalR 连接异常处理

**场景 1: 连接失败**
```csharp
public async Task ConnectAsync()
{
    var retryCount = 0;
    var maxRetries = 5;
    
    while (retryCount < maxRetries)
    {
        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR connected successfully");
            return;
        }
        catch (Exception ex)
        {
            retryCount++;
            _logger.LogWarning(ex, $"SignalR connection failed, retry {retryCount}/{maxRetries}");
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // 指数退避
        }
    }
    
    // 降级：仅使用轮询
    _logger.LogError("SignalR connection failed after {maxRetries} retries, falling back to polling only");
    _isFallbackMode = true;
}
```

**场景 2: 断线重连**
```csharp
_connection.Closed += async (error) =>
{
    _logger.LogWarning(error, "SignalR connection closed");
    await Task.Delay(TimeSpan.FromSeconds(5));
    await ConnectAsync(); // 自动重连
};
```

**场景 3: 通知丢失检测**
```csharp
// 在轮询时检测事件时间戳不连续，说明可能漏掉了 SignalR 通知
if (_lastKnownEventTime.HasValue && 
    status.CurrentTime - _lastKnownEventTime.Value > _pollInterval * 2)
{
    _logger.LogWarning("Potential SignalR notification missed, gap detected");
    _metrics.IncrementMissedNotifications();
}
```

#### 📊 可观测性增强

**指标收集**:
```csharp
public class SignalRMetrics
{
    public long TotalNotificationsReceived { get; set; }
    public long TotalNotificationsSent { get; set; }
    public long MissedNotifications { get; set; }
    public TimeSpan AverageNotificationLatency { get; set; }
    public int CurrentConnections { get; set; }
}
```

**日志记录**（服务器端）:
```csharp
_logger.LogInformation(
    "Battle notification sent: BattleId={BattleId}, EventType={EventType}, Latency={Latency}ms",
    battleId, eventType, latency
);
```

**日志记录**（客户端）:
```csharp
_logger.LogDebug(
    "Received SignalR notification: EventType={EventType}, ProcessingTime={Time}ms",
    evt.EventType, processingTime
);
```

---

## 6. SignalR 集成方案（下篇）

### 6.1 Phase 3: 高级功能与优化（第 5-6 周）

#### 🎯 技能与 Buff 通知

**场景**: 玩家施放重要技能或关键 Buff 生效时通知前端播放动画

**筛选策略**:
```csharp
// 仅通知"重要"技能（避免高频通知）
public bool IsImportantSkill(SkillDefinition skill)
{
    return skill.Tags.Contains("Ultimate")      // 大招
        || skill.Tags.Contains("Defensive")     // 防御技能
        || skill.Cooldown >= 10.0;              // 冷却 ≥10s
}
```

**通知实现**:
```csharp
// SkillCastCompleteEvent.Execute()
if (IsImportantSkill(def) && _notifier != null)
{
    await _notifier.NotifyEvent(context.Battle.Id, new SkillCastEventDto(
        BattleId: context.Battle.Id,
        EventTime: context.Clock.CurrentTime,
        SkillId: def.Id,
        SkillName: def.Name,
        TargetCount: targets.Length
    ));
}
```

**前端处理**:
```csharp
_signalR.OnSkillCast(evt => 
{
    // 播放技能动画
    await _animationService.PlaySkillAnimation(evt.SkillId);
    
    // 显示浮动文字
    ShowFloatingText($"施放 {evt.SkillName}");
    
    // 可选：触发技能音效
    await _audioService.PlaySkillSound(evt.SkillId);
});
```

#### 🎭 Buff 状态变化通知

**筛选策略**:
```csharp
// 仅通知关键 Buff（如层数 ≥5 或持续时间 ≥30s）
public bool IsImportantBuff(BuffDefinition buff, int stacks)
{
    return buff.Tags.Contains("Important")
        || stacks >= 5
        || buff.Duration >= 30.0;
}
```

**DTO 定义**:
```csharp
public record BuffChangedEventDto(
    Guid BattleId,
    double EventTime,
    string BuffId,
    string BuffName,
    BuffChangeType ChangeType,  // Added, Removed, StacksChanged
    int CurrentStacks,
    double RemainingDuration
) : BattleEventDto(BattleId, EventTime, "BuffChanged");
```

#### ⚡ 性能优化：通知节流

**问题**: 高频事件（如暴击）可能导致 SignalR 通信开销过大

**解决方案**: 服务器端节流器
```csharp
public class NotificationThrottler
{
    private readonly Dictionary<string, DateTime> _lastSent = new();
    private readonly TimeSpan _minInterval = TimeSpan.FromSeconds(1);
    
    public bool ShouldSend(string eventKey)
    {
        if (_lastSent.TryGetValue(eventKey, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < _minInterval)
                return false; // 节流
        }
        
        _lastSent[eventKey] = DateTime.UtcNow;
        return true;
    }
}
```

**使用示例**:
```csharp
// 暴击通知（节流到每秒最多 1 次）
if (isCritical && _throttler.ShouldSend($"crit_{battleId}"))
{
    await _notifier.NotifyStateChange(battleId, "CriticalHit");
}
```

#### 🔐 安全性增强

**1. 验证客户端身份**:
```csharp
public class BattleNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            Context.Abort(); // 未认证用户拒绝连接
            return;
        }
        
        // 记录用户 → ConnectionId 映射
        await _connectionManager.AddConnection(userId, Context.ConnectionId);
    }
}
```

**2. 权限校验**（订阅战斗前检查角色所有权）:
```csharp
public async Task SubscribeBattle(Guid battleId)
{
    var userId = Context.User.GetUserId();
    var characterId = await _battleService.GetBattleCharacterId(battleId);
    
    if (!await _characterService.IsCharacterOwnedBy(characterId, userId))
    {
        throw new HubException("Unauthorized access to battle");
    }
    
    await Groups.AddToGroupAsync(Context.ConnectionId, $"battle_{battleId}");
}
```

#### 📱 移动端优化

**问题**: 移动网络不稳定，SignalR 连接频繁断开

**解决方案**:
1. **更长的重连间隔**:
   ```csharp
   _connection = new HubConnectionBuilder()
       .WithUrl("/hubs/battle")
       .WithAutomaticReconnect(new[] { 
           TimeSpan.Zero,        // 立即重连
           TimeSpan.FromSeconds(2), 
           TimeSpan.FromSeconds(10),
           TimeSpan.FromSeconds(30) 
       })
       .Build();
   ```

2. **轮询降级更激进**:
   ```csharp
   if (_reconnectAttempts > 3)
   {
       // 移动端 3 次失败后直接降级到纯轮询
       _isFallbackMode = true;
       _logger.LogWarning("Mobile device fallback to polling after 3 reconnect failures");
   }
   ```

#### 📊 完整的监控面板

**后端指标 API** (`/api/admin/signalr/metrics`):
```csharp
public class SignalRMetricsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetMetrics()
    {
        return Ok(new
        {
            TotalConnections = _hubContext.GetConnectionCount(),
            NotificationsSent = _metrics.TotalNotificationsSent,
            AverageLatency = _metrics.AverageNotificationLatency,
            ErrorRate = _metrics.ErrorCount / (double)_metrics.TotalNotificationsSent
        });
    }
}
```

**前端监控面板** (`Debug.razor` 扩展):
```razor
<h5>SignalR 状态</h5>
<table>
    <tr><td>连接状态:</td><td>@_signalR.ConnectionState</td></tr>
    <tr><td>收到通知数:</td><td>@_signalR.Metrics.TotalReceived</td></tr>
    <tr><td>平均延迟:</td><td>@_signalR.Metrics.AverageLatency ms</td></tr>
    <tr><td>漏通知数:</td><td>@_signalR.Metrics.MissedCount</td></tr>
</table>
```

### 6.2 Phase 4: 进阶场景支持（第 7-8 周）

#### 🌟 多角色战斗通知

**场景**: 用户同时进行多个角色的战斗，需要接收所有战斗通知

**实现**:
```csharp
// 客户端订阅多个战斗
await _signalR.SubscribeBattleAsync(battle1Id);
await _signalR.SubscribeBattleAsync(battle2Id);

// 区分不同战斗的通知
_signalR.OnStateChanged((battleId, evt) => 
{
    if (battleId == _currentDisplayedBattleId)
    {
        // 当前显示的战斗，立即更新 UI
        await UpdateBattleUI(evt);
    }
    else
    {
        // 后台战斗，仅标记"有更新"
        MarkBattleAsUpdated(battleId);
    }
});
```

#### 🔄 离线战斗通知支持

**挑战**: 离线战斗（`OfflineFastForwardEngine`）在单独线程模拟，无法实时发送通知

**解决方案**: 记录关键事件，登录时批量通知
```csharp
public class OfflineEventBuffer
{
    private readonly List<BattleEventDto> _bufferedEvents = new();
    
    public void RecordEvent(BattleEventDto evt)
    {
        _bufferedEvents.Add(evt);
    }
    
    public async Task FlushOnLogin(Guid userId)
    {
        var connectionId = await _connectionManager.GetConnectionId(userId);
        if (connectionId != null)
        {
            foreach (var evt in _bufferedEvents)
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("OfflineEventReplay", evt);
            }
        }
        _bufferedEvents.Clear();
    }
}
```

**前端处理**:
```csharp
_signalR.OnOfflineEventReplay(evt => 
{
    // 显示离线期间的重要事件摘要
    ShowOfflineEventSummary(evt);
});
```

#### 🎮 副本进度广播

**场景**: 组队副本中，队友的状态变化需要广播给所有成员

**实现**（预留接口）:
```csharp
// 服务器端
public async Task NotifyPartyMembers(Guid partyId, PartyEventDto evt)
{
    await _hubContext.Clients.Group($"party_{partyId}")
        .SendAsync("PartyEvent", evt);
}

// 前端
_signalR.OnPartyEvent(evt => 
{
    switch (evt)
    {
        case MemberJoinedEventDto join:
            ShowNotification($"{join.MemberName} 加入了队伍");
            break;
        case MemberDiedEventDto death:
            ShowWarning($"{death.MemberName} 已倒下");
            break;
    }
});
```

### 6.3 Phase 5: 文档与运维（第 8 周）

#### 📖 开发者文档

**1. 添加新事件通知指南** (`docs/SignalR_Event_Guide.md`):
```markdown
## 如何添加新的 SignalR 通知

1. 判断是否需要 SignalR（参考决策模型）
2. 定义 DTO（继承 BattleEventDto）
3. 在事件类的 Execute() 中调用 NotifyEvent()
4. 前端注册事件处理器
5. 编写集成测试
```

**2. 故障排查手册** (`docs/SignalR_Troubleshooting.md`):
```markdown
## 常见问题

### Q: 前端收不到通知
A: 检查以下几点：
   1. SignalR 连接状态（查看浏览器控制台）
   2. 是否正确订阅战斗（调用 SubscribeBattleAsync）
   3. 服务器日志是否显示通知已发送
   4. 检查防火墙/代理配置
```

#### 🔧 运维工具

**1. SignalR 连接监控脚本**:
```bash
#!/bin/bash
# 检查 SignalR Hub 健康状态
curl -f https://api.blazoridle.com/hubs/battle/health || exit 1
```

**2. 性能告警规则**（Prometheus 示例）:
```yaml
- alert: SignalRHighLatency
  expr: signalr_notification_latency_ms > 1000
  for: 5m
  annotations:
    summary: "SignalR notification latency is high"
    description: "Average latency is {{ $value }}ms"
```

#### ✅ Phase 3-5 验收标准

1. **高级功能**:
   - [ ] 重要技能施放通知（冷却 ≥10s 的技能）
   - [ ] 关键 Buff 变化通知（层数 ≥5）
   - [ ] 暴击通知节流（每秒最多 1 次）

2. **性能与稳定性**:
   - [ ] 1000 并发连接压力测试通过
   - [ ] 通知延迟 P99 <1s
   - [ ] 连接断开后自动重连成功率 >95%
   - [ ] 24 小时稳定性测试无内存泄漏

3. **可观测性**:
   - [ ] 监控面板实时显示连接数、通知数
   - [ ] 日志完整记录通知发送/接收
   - [ ] 告警规则覆盖异常场景

4. **文档完整性**:
   - [ ] 开发者指南完整
   - [ ] API 文档自动生成（Swagger）
   - [ ] 故障排查手册经过验证

---

## 7. 验收标准

### 7.1 功能验收清单

#### ✅ Phase 1: 基础架构（必须）

| 验收项 | 验收方法 | 通过标准 |
|-------|---------|---------|
| SignalR Hub 启动 | 访问 `/hubs/battle` | 返回 101 Switching Protocols |
| 客户端连接成功 | 浏览器控制台查看 | 显示 "SignalR connected" 日志 |
| 玩家死亡通知 | 触发玩家死亡 | 前端收到 `PlayerDeath` 事件 <1s |
| 怪物击杀通知 | 击杀怪物 | 前端收到 `EnemyKilled` 事件 <1s |
| 目标切换通知 | 多怪战斗 | 前端收到 `TargetSwitched` 事件 <1s |
| 自动重连 | 手动断开连接 | 5s 内自动重连成功 |
| 降级到轮询 | 禁用 SignalR 服务 | 前端仍正常工作（纯轮询） |

#### ✅ Phase 2: 进度条同步（重要）

| 验收项 | 验收方法 | 通过标准 |
|-------|---------|---------|
| 进度条精准模拟 | 对比 NextSignificantEventAt | 误差 <5% |
| SignalR 中断进度条 | 目标切换 | 立即重置到 0%，无视觉跳变 |
| 详细事件数据 | 检查 TargetSwitchedEventDto | 包含新目标完整信息 |
| 玩家死亡 UI 响应 | 触发死亡 | 立即停止进度条，显示死亡状态 |
| 波次刷新延迟显示 | 清空一波怪物 | 显示刷新倒计时，进度条暂停 |

#### ✅ Phase 3: 高级功能（可选）

| 验收项 | 验收方法 | 通过标准 |
|-------|---------|---------|
| 技能通知 | 施放大招 | 播放技能动画 |
| Buff 通知 | 获得 5 层 Buff | 前端更新 Buff 图标 |
| 通知节流 | 连续暴击 10 次 | 每秒最多 1 次通知 |
| 权限校验 | 尝试订阅他人战斗 | 返回 403 Forbidden |
| 多战斗通知 | 同时进行 2 个战斗 | 两个战斗都能收到通知 |

### 7.2 性能验收指标

| 指标 | 目标值 | 测试方法 |
|-----|--------|---------|
| 通知延迟（P50） | <300ms | 发送 1000 次通知，计算中位数 |
| 通知延迟（P99） | <1s | 发送 1000 次通知，计算 P99 |
| 并发连接数 | ≥1000 | 使用 SignalR 压测工具 |
| 内存占用 | <50MB（1000 连接） | 监控服务器内存 |
| CPU 占用 | <10%（1000 连接） | 监控服务器 CPU |
| 通知丢失率 | <0.1% | 对比发送数和接收数 |
| 重连成功率 | >95% | 断开连接 100 次，统计成功次数 |

### 7.3 兼容性验收

| 环境 | 验收内容 | 通过标准 |
|------|---------|---------|
| 纯轮询模式 | 禁用 SignalR | 功能完全正常，无错误日志 |
| 旧版客户端 | 不支持 SignalR | 降级到轮询，不影响使用 |
| 移动端 | 网络不稳定 | 自动重连，或降级到轮询 |
| 多标签页 | 打开多个页面 | 每个页面独立接收通知 |

### 7.4 代码质量验收

| 验收项 | 标准 |
|-------|------|
| 单元测试覆盖率 | ≥80%（新增代码） |
| 集成测试 | 覆盖所有通知场景 |
| 代码审查 | 无严重代码异味（SonarQube） |
| 文档完整性 | 所有公共 API 有 XML 注释 |
| 日志规范 | 使用结构化日志（Serilog） |

### 7.5 用户体验验收

| 场景 | 期望结果 |
|------|---------|
| 怪物死亡 | 立即显示击杀提示，进度条切换新目标 |
| 玩家死亡 | 立即停止动画，显示死亡画面 + 复活倒计时 |
| 目标切换 | 进度条平滑重置，新目标信息立即显示 |
| 网络断开 | 显示"连接中断"提示，自动重连后提示"已重连" |
| 离线登录 | 显示离线期间重要事件摘要 |

---

## 8. 附录

### 8.1 技术栈总结

| 层级 | 技术 | 版本 |
|------|------|------|
| 服务器端 | ASP.NET Core SignalR | 8.0+ |
| 客户端 | Microsoft.AspNetCore.SignalR.Client | 8.0+ |
| 传输协议 | WebSocket（降级到 Server-Sent Events / Long Polling） | - |
| 序列化 | System.Text.Json | - |
| 日志 | Serilog | 3.0+ |
| 监控 | Application Insights / Prometheus | - |

### 8.2 关键设计决策

| 决策 | 理由 |
|------|------|
| **初期仅通知事件类型** | 减少开发复杂度，快速验证可行性 |
| **保留轮询机制** | 作为降级方案，确保可用性 |
| **使用 Group 而非直接 ConnectionId** | 便于未来扩展（组队副本、公会战） |
| **服务器端节流** | 避免高频事件导致通信开销 |
| **前端立即轮询而非直接使用通知数据** | 确保状态完整性，避免漏掉其他变化 |

### 8.3 风险与应对

| 风险 | 影响 | 应对措施 |
|------|------|---------|
| SignalR 连接不稳定 | 用户频繁看到"连接中断" | 自动重连 + 降级到轮询 |
| 通知延迟过高（>2s） | 进度条不同步 | 监控告警 + 优化服务器性能 |
| 内存泄漏 | 服务器崩溃 | 定期重启 + 内存监控告警 |
| 权限漏洞 | 用户窃听他人战斗 | 严格权限校验 + 安全审计 |
| 通知风暴（短时间大量通知） | 服务器压力过大 | 节流 + 批量合并通知 |

### 8.4 参考资料

- [ASP.NET Core SignalR 官方文档](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [SignalR 性能最佳实践](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale)
- [WebSocket 协议规范 RFC 6455](https://tools.ietf.org/html/rfc6455)
- 项目现有文档：
  - `docs/前端UI优化设计方案.md`
  - `docs/POLLING_UNIFICATION_SUMMARY.md`
  - `docs/POLLING_HINT_IMPLEMENTATION.md`

### 8.5 时间线与里程碑

```
Week 1-2: Phase 1 - 基础架构搭建
├── Day 1-3:   SignalR Hub + Service 实现
├── Day 4-5:   客户端连接管理
├── Day 6-7:   玩家死亡/怪物击杀通知
├── Day 8-10:  目标切换通知
└── Day 11-14: 集成测试 + Bug 修复

Week 3-4: Phase 2 - 进度条同步
├── Day 15-17: 前端进度条状态机重构
├── Day 18-20: SignalR 中断逻辑实现
├── Day 21-23: 详细事件 DTO 设计与传输
├── Day 24-26: UI 反馈增强（Toast/动画）
└── Day 27-28: 错误处理与降级策略

Week 5-6: Phase 3 - 高级功能
├── Day 29-31: 技能与 Buff 通知
├── Day 32-34: 通知节流与性能优化
├── Day 35-36: 权限校验与安全加固
├── Day 37-38: 移动端优化
└── Day 39-42: 监控面板与指标收集

Week 7-8: Phase 4-5 - 进阶与运维
├── Day 43-45: 多角色/离线战斗支持
├── Day 46-47: 组队副本预留接口
├── Day 48-50: 文档编写
├── Day 51-53: 压力测试与优化
└── Day 54-56: 最终验收与上线准备
```

### 8.6 成本估算

| 类型 | 说明 | 估算 |
|------|------|------|
| 开发工时 | 8 周 * 5 天/周 * 8 小时/天 | 320 小时 |
| 测试工时 | 包含在开发周期内 | - |
| 基础设施 | SignalR 无额外许可成本（ASP.NET Core 内置） | $0 |
| 云服务成本 | 可能需要更多 CPU/内存（估算 +20%） | 按现有基础设施计算 |
| 监控成本 | Application Insights / Prometheus | 现有服务 |

### 8.7 后续演进路线

#### 短期（3 个月内）
- 细化通知内容（包含完整伤害数据）
- 前端播放技能/Buff 动画
- 移动端推送通知（PWA）

#### 中期（6 个月内）
- 组队副本实时协作
- 战斗回放功能（基于通知流）
- PvP 对战实时同步

#### 长期（1 年内）
- 服务器集群 SignalR 扩展（Redis Backplane）
- 全球多区域部署（延迟优化）
- AI 驱动的战斗推荐（基于实时数据）

---

## 📝 文档变更记录

| 版本 | 日期 | 作者 | 变更内容 |
|------|------|------|---------|
| 1.0 | 2025-10-13 | AI Assistant | 初始版本：完整需求分析与设计方案 |

---

## ✅ 签字确认

| 角色 | 姓名 | 签名 | 日期 |
|------|------|------|------|
| 产品负责人 | - | - | - |
| 技术负责人 | - | - | - |
| 测试负责人 | - | - | - |

---

**文档结束**
