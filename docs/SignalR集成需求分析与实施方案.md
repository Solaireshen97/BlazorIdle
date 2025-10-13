# SignalR 集成需求分析与实施方案

## 文档信息
- **创建时间**: 2025-10-13
- **版本**: v1.0
- **状态**: 需求分析与方案设计
- **目标**: 为 BlazorIdle 项目增加 SignalR 实时通知功能

---

## 一、项目现状分析

### 1.1 当前架构概述

#### 战斗系统架构
- **事件驱动引擎**: `BattleEngine` 使用 `IEventScheduler` 管理所有战斗事件
- **事件类型**: 包括玩家死亡、复活、攻击、技能施放、怪物死亡等
- **轮询机制**: 前端通过 `BattlePollingCoordinator` 统一管理轮询
- **自适应轮询**: 服务端通过 `PollingHint` 提供动态轮询间隔建议

#### 前端轮询系统
```
BattlePollingCoordinator 管理：
├── Step战斗状态轮询 (500ms 基准)
├── 活动计划轮询 (2000ms)
├── Debug信息轮询 (1000ms)
└── 进度条动画定时器 (100ms)
```

#### 自适应轮询策略
服务端根据战斗状态动态调整建议轮询间隔：
- **战斗完成/空闲**: 5000ms (稳定)
- **正常战斗**: 2000ms (稳定)
- **玩家死亡**: 根据复活时间动态调整
- **玩家血量 < 50%**: 1000ms (激烈)
- **玩家血量 < 20%**: 500ms (危急)

#### 现有事件系统
服务端已实现完整的事件驱动架构：
- `IGameEvent` 接口定义所有战斗事件
- `PlayerDeathEvent`: 玩家死亡事件
- `PlayerReviveEvent`: 玩家复活事件
- `EnemyAttackEvent`: 怪物攻击事件
- `AttackTickEvent`: 玩家攻击事件
- `SpecialPulseEvent`: 特殊技能脉冲
- 其他技能、Buff 相关事件

### 1.2 当前问题识别

#### 轮询延迟问题
1. **玩家死亡延迟**: 玩家实际死亡到前端感知有 0.5-5 秒延迟
2. **怪物死亡延迟**: 怪物被击杀到前端更新有延迟，影响进度条体验
3. **目标切换延迟**: 多怪战斗中目标切换不够及时
4. **进度条不准确**: 依赖客户端模拟的进度条在突发事件时会出现错位

#### 带宽浪费问题
1. **无变化轮询**: 稳定战斗时大量轮询返回相同数据
2. **高频轮询**: 危急状态 500ms 轮询在死亡后仍持续
3. **重复数据传输**: 每次轮询都返回完整状态

#### 用户体验问题
1. **反馈延迟**: 关键事件（死亡、击杀）反馈不够即时
2. **进度条跳跃**: 轮询更新导致进度条突然跳跃或重置
3. **战斗感不足**: 缺乏即时打击反馈

---

## 二、SignalR 与轮询事件分类分析

### 2.1 事件分类原则

#### 需要 SignalR 实时通知的事件（高优先级）
**判断标准**：
- 不可预测的突发事件
- 需要立即中断前端当前状态
- 影响用户核心体验
- 发生频率相对较低

#### 适合继续轮询的事件（保持现状）
**判断标准**：
- 可预测的周期性事件
- 状态渐进变化
- 高频更新（每秒多次）
- 数据量较大的聚合信息

### 2.2 详细事件分类

#### 🔴 必须使用 SignalR 通知（Phase 1 核心）

| 事件类型 | 优先级 | 理由 | 前端响应 |
|---------|--------|------|----------|
| **玩家死亡** | ⭐⭐⭐⭐⭐ | 不可预测，需立即暂停所有进度条 | 立即停止进度、显示死亡状态、触发复活倒计时 |
| **玩家复活** | ⭐⭐⭐⭐⭐ | 需立即恢复战斗显示 | 恢复进度条、更新状态、显示复活效果 |
| **怪物死亡** | ⭐⭐⭐⭐⭐ | 不可预测，影响进度条和目标显示 | 立即重置进度条、触发击杀效果、更新目标列表 |
| **主要目标切换** | ⭐⭐⭐⭐ | 影响当前战斗焦点 | 更新焦点显示、重置相关进度 |
| **波次切换** | ⭐⭐⭐⭐ | 副本关键节点 | 显示波次变化、重置战场、触发过场效果 |

#### 🟡 建议使用 SignalR 通知（Phase 2 增强）

| 事件类型 | 优先级 | 理由 | 前端响应 |
|---------|--------|------|----------|
| **战斗完成** | ⭐⭐⭐⭐ | 重要里程碑 | 停止轮询、显示结算、触发奖励动画 |
| **危急状态变化** | ⭐⭐⭐ | 玩家血量 < 20% | 触发预警效果、调整轮询策略 |
| **重要Buff添加/移除** | ⭐⭐⭐ | 影响战斗策略 | 更新Buff显示、触发特效 |
| **特殊技能就绪** | ⭐⭐⭐ | 引导玩家注意 | 高亮技能、播放就绪音效 |
| **掉落获得** | ⭐⭐⭐ | 即时奖励反馈 | 飘字效果、物品通知 |

#### 🟢 继续使用轮询（保持现状）

| 事件类型 | 轮询间隔 | 理由 |
|---------|---------|------|
| **HP/资源缓慢变化** | 2000ms | 渐进变化，可预测 |
| **普通攻击伤害** | 2000ms | 高频事件，聚合后轮询 |
| **冷却时间更新** | 自适应 | 可通过客户端倒计时模拟 |
| **经验/金币累积** | 2000ms | 聚合数据，不需要实时 |
| **背包更新** | 按需 | 用户主动查询 |
| **战斗统计信息** | 2000-5000ms | 分析数据，延迟可接受 |

### 2.3 混合策略设计

#### SignalR 触发 + 立即轮询
**场景**：重大事件发生，需要完整状态更新
```
SignalR 通知 → 前端立即发起一次状态轮询 → 获取完整状态
```

**优势**：
- 保持现有 API 结构，最小化改动
- SignalR 只传递轻量级通知
- 完整状态通过成熟的轮询 API 获取
- 避免 SignalR 消息体积过大

#### 渐进增强策略
**Phase 1**: SignalR 仅发送事件通知，触发立即轮询
**Phase 2**: SignalR 携带关键数据（如死亡时间、目标ID）
**Phase 3**: SignalR 携带完整事件上下文，减少轮询依赖

---

## 三、SignalR 与自适应轮询协同方案

### 3.1 协同工作原理

#### 轮询间隔动态调整
```
SignalR 事件触发 → 前端判断事件类型 → 动态调整轮询间隔

示例：
1. 正常战斗：2000ms 轮询
2. 收到"玩家血量低"通知：缩短到 1000ms
3. 收到"玩家死亡"通知：延长到 5000ms（等待复活）
4. 收到"战斗完成"通知：停止战斗轮询
```

#### 智能轮询暂停
```
关键事件通知 → 立即触发一次轮询 → 临时调整轮询策略

示例：
1. 收到"怪物死亡"通知
2. 立即发起一次状态查询
3. 获取最新状态（新目标、奖励等）
4. 根据新状态调整后续轮询间隔
```

### 3.2 前端轮询协调器增强

#### BattlePollingCoordinator 扩展
```csharp
private class BattlePollingCoordinator
{
    // 现有字段...
    
    // 新增：SignalR 连接管理
    private HubConnection? _battleHub;
    
    // 新增：动态轮询控制
    private bool _eventDrivenMode = false;
    private DateTime _lastSignalREvent = DateTime.MinValue;
    
    // 新增：建立 SignalR 连接
    public async Task ConnectSignalRAsync(Guid characterId)
    {
        _battleHub = new HubConnectionBuilder()
            .WithUrl($"https://api.example.com/battleHub?characterId={characterId}")
            .WithAutomaticReconnect()
            .Build();
        
        // 订阅关键事件
        _battleHub.On<string>("PlayerDeath", OnPlayerDeathNotification);
        _battleHub.On<string>("PlayerRevive", OnPlayerReviveNotification);
        _battleHub.On<string, Guid>("EnemyKilled", OnEnemyKilledNotification);
        _battleHub.On<string>("BattleComplete", OnBattleCompleteNotification);
        _battleHub.On<string>("CriticalStateChange", OnCriticalStateNotification);
        
        await _battleHub.StartAsync();
    }
    
    // 新增：处理玩家死亡通知
    private async void OnPlayerDeathNotification(string message)
    {
        _lastSignalREvent = DateTime.UtcNow;
        
        // 立即触发一次状态更新
        await TriggerImmediateRefresh();
        
        // 调整轮询策略：延长轮询间隔（等待复活）
        _stepPollInterval = 5000;
    }
    
    // 新增：处理怪物死亡通知
    private async void OnEnemyKilledNotification(string message, Guid enemyId)
    {
        _lastSignalREvent = DateTime.UtcNow;
        
        // 立即触发一次状态更新
        await TriggerImmediateRefresh();
        
        // 可能需要重置进度条
        ResetProgressBars();
    }
    
    // 新增：立即触发刷新
    private async Task TriggerImmediateRefresh()
    {
        if (_stepBattleActive)
        {
            await _parent.PollStepBattleStatusAsync();
        }
    }
}
```

### 3.3 轮询优化策略

#### 降级策略
```
SignalR 连接失败/断开 → 自动回退到纯轮询模式
- 使用更短的轮询间隔补偿
- 记录降级事件用于监控
- 自动尝试重连
```

#### 混合监控
```
同时运行 SignalR + 轮询：
- SignalR：关键事件实时通知
- 轮询：定期同步完整状态（2-5秒）
- 交叉验证：检测数据一致性

优势：
- 冗余保障可靠性
- 检测 SignalR 消息丢失
- 自动修正不一致状态
```

---

## 四、分阶段实施方案

### 【上】Phase 1 - 基础架构搭建（第 1-2 周）

#### 目标
建立 SignalR 基础设施，实现核心死亡/复活事件通知

#### 实施步骤

##### Step 1.1: 服务端基础架构（3天）
**任务**：
- [ ] 添加 `Microsoft.AspNetCore.SignalR` NuGet 包
- [ ] 创建 `BattleHub` 类（继承自 `Hub`）
- [ ] 在 `Program.cs` 配置 SignalR 服务和端点
- [ ] 实现连接管理（用户ID到ConnectionId映射）
- [ ] 配置 CORS 支持 SignalR

**产出**：
```csharp
// BlazorIdle.Server/Hubs/BattleHub.cs
public class BattleHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    
    public override async Task OnConnectedAsync()
    {
        var characterId = Context.GetHttpContext()?.Request.Query["characterId"];
        if (Guid.TryParse(characterId, out var id))
        {
            await _connectionManager.RegisterConnectionAsync(id, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionManager.UnregisterConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// BlazorIdle.Server/Services/IConnectionManager.cs
public interface IConnectionManager
{
    Task RegisterConnectionAsync(Guid characterId, string connectionId);
    Task UnregisterConnectionAsync(string connectionId);
    Task<string?> GetConnectionIdAsync(Guid characterId);
}
```

##### Step 1.2: 事件通知服务（3天）
**任务**：
- [ ] 创建 `IBattleNotificationService` 接口
- [ ] 实现 `BattleNotificationService`
- [ ] 在 `BattleEngine` 中集成事件通知
- [ ] 实现玩家死亡/复活事件推送

**产出**：
```csharp
// BlazorIdle.Server/Services/IBattleNotificationService.cs
public interface IBattleNotificationService
{
    Task NotifyPlayerDeathAsync(Guid characterId, double deathTime, double? reviveAt);
    Task NotifyPlayerReviveAsync(Guid characterId, double reviveTime);
    Task NotifyEnemyKilledAsync(Guid characterId, Guid enemyId, string enemyName);
    Task NotifyBattleCompleteAsync(Guid characterId, Guid battleId);
}

// BlazorIdle.Server/Services/BattleNotificationService.cs
public class BattleNotificationService : IBattleNotificationService
{
    private readonly IHubContext<BattleHub> _hubContext;
    private readonly IConnectionManager _connectionManager;
    
    public async Task NotifyPlayerDeathAsync(Guid characterId, double deathTime, double? reviveAt)
    {
        var connectionId = await _connectionManager.GetConnectionIdAsync(characterId);
        if (connectionId != null)
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(
                "PlayerDeath",
                new PlayerDeathNotification
                {
                    DeathTime = deathTime,
                    ReviveAt = reviveAt,
                    Timestamp = DateTime.UtcNow
                });
        }
    }
}
```

##### Step 1.3: 在战斗引擎中集成（3天）
**任务**：
- [ ] 修改 `PlayerDeathEvent.Execute()` 调用通知服务
- [ ] 修改 `PlayerReviveEvent.Execute()` 调用通知服务
- [ ] 确保事件通知不阻塞战斗逻辑（异步）
- [ ] 添加异常处理和日志

**产出**：
```csharp
// 在 PlayerDeathEvent.Execute() 中添加
public void Execute(BattleContext context)
{
    // 现有死亡处理逻辑...
    
    // 新增：触发 SignalR 通知（Fire-and-forget）
    _ = context.NotificationService?.NotifyPlayerDeathAsync(
        context.CharacterId,
        ExecuteAt,
        player.ReviveAt
    );
}
```

##### Step 1.4: 前端 SignalR 客户端（4天）
**任务**：
- [ ] 添加 `Microsoft.AspNetCore.SignalR.Client` NuGet 包到前端项目
- [ ] 在 `BattlePollingCoordinator` 中添加 SignalR 连接管理
- [ ] 实现自动重连机制
- [ ] 订阅玩家死亡/复活事件
- [ ] 实现事件触发立即轮询逻辑

**产出**：
```csharp
// Characters.razor - BattlePollingCoordinator 扩展
private HubConnection? _battleHub;

private async Task ConnectBattleHubAsync(Guid characterId)
{
    _battleHub = new HubConnectionBuilder()
        .WithUrl($"{_parent.Api.BaseUrl}/battleHub?characterId={characterId}")
        .WithAutomaticReconnect()
        .Build();
    
    _battleHub.On<PlayerDeathNotification>("PlayerDeath", async notification =>
    {
        Console.WriteLine($"[SignalR] Player death at {notification.DeathTime}");
        await TriggerImmediateStatusRefresh();
        AdjustPollingForDeath(notification.ReviveAt);
    });
    
    _battleHub.On<PlayerReviveNotification>("PlayerRevive", async notification =>
    {
        Console.WriteLine($"[SignalR] Player revived at {notification.ReviveTime}");
        await TriggerImmediateStatusRefresh();
        ResumeNormalPolling();
    });
    
    await _battleHub.StartAsync();
}
```

##### Step 1.5: 测试与验证（2天）
**任务**：
- [ ] 手动测试玩家死亡通知
- [ ] 手动测试玩家复活通知
- [ ] 验证立即轮询触发
- [ ] 验证轮询间隔调整
- [ ] 测试 SignalR 断线重连
- [ ] 测试降级到纯轮询

**验收标准**：
- ✅ 玩家死亡后 100ms 内收到 SignalR 通知
- ✅ 收到通知后立即触发状态轮询
- ✅ 前端正确显示死亡状态和复活倒计时
- ✅ SignalR 断线后自动回退到轮询模式
- ✅ 无报错或异常日志

---

### 【中】Phase 2 - 怪物死亡与目标切换（第 3-4 周）

#### 目标
实现怪物死亡和目标切换的实时通知，改善进度条体验

#### 实施步骤

##### Step 2.1: 怪物死亡事件捕获（3天）
**任务**：
- [ ] 在 `BattleEngine.CaptureNewDeaths()` 中集成通知
- [ ] 传递怪物详细信息（ID、名称、是否为主目标）
- [ ] 处理多怪同时死亡的场景
- [ ] 添加击杀计数和连击统计

**产出**：
```csharp
private void CaptureNewDeaths()
{
    var grp = Context.EncounterGroup;
    if (grp is not null)
    {
        foreach (var e in grp.All)
        {
            if (e.IsDead && !_markedDead.Contains(e))
            {
                Collector.OnTag($"kill.{e.Enemy.Id}", 1);
                _markedDead.Add(e);
                
                // 新增：SignalR 通知
                _ = Context.NotificationService?.NotifyEnemyKilledAsync(
                    Context.CharacterId,
                    e.Enemy.Id,
                    e.Enemy.Name,
                    isPrimaryTarget: e == grp.Primary
                );
            }
        }
    }
}
```

##### Step 2.2: 目标切换通知（2天）
**任务**：
- [ ] 在 `TryRetargetPrimaryIfDead()` 中添加通知
- [ ] 传递新目标信息
- [ ] 区分自动切换和手动切换

**产出**：
```csharp
private void TryRetargetPrimaryIfDead()
{
    var grp = Context.EncounterGroup;
    if (grp?.Primary.IsDead == true && grp.Alive.Count > 0)
    {
        var oldTargetId = grp.Primary.Enemy.Id;
        grp.RetargetToNextAlive();
        var newTargetId = grp.Primary.Enemy.Id;
        
        // 新增：SignalR 通知
        _ = Context.NotificationService?.NotifyTargetSwitchedAsync(
            Context.CharacterId,
            oldTargetId,
            newTargetId,
            grp.Primary.Enemy.Name
        );
    }
}
```

##### Step 2.3: 前端进度条优化（4天）
**任务**：
- [ ] 订阅怪物死亡和目标切换事件
- [ ] 实现进度条立即重置逻辑
- [ ] 添加击杀特效动画
- [ ] 优化目标高亮显示
- [ ] 实现连击计数器

**产出**：
```csharp
_battleHub.On<EnemyKilledNotification>("EnemyKilled", async notification =>
{
    Console.WriteLine($"[SignalR] Enemy killed: {notification.EnemyName}");
    
    // 立即重置进度条
    ResetAttackProgress();
    
    // 触发击杀特效
    await ShowKillEffect(notification.EnemyId);
    
    // 更新连击计数
    UpdateComboCounter();
    
    // 立即获取最新状态（包括新目标）
    await TriggerImmediateStatusRefresh();
});

_battleHub.On<TargetSwitchedNotification>("TargetSwitched", notification =>
{
    Console.WriteLine($"[SignalR] Target switched to: {notification.NewTargetName}");
    
    // 更新目标高亮
    HighlightTarget(notification.NewTargetId);
    
    // 重置进度条针对新目标
    ResetProgressForNewTarget();
});
```

##### Step 2.4: 测试与优化（2天）
**任务**：
- [ ] 测试单怪击杀流程
- [ ] 测试多怪连续击杀
- [ ] 测试目标自动切换
- [ ] 验证进度条不再跳跃
- [ ] 性能测试（高频击杀场景）

**验收标准**：
- ✅ 怪物死亡后 100ms 内收到通知
- ✅ 进度条立即重置，不再出现跳跃
- ✅ 目标切换即时高亮
- ✅ 击杀特效流畅播放
- ✅ 高频击杀不造成通知延迟或丢失

---

### 【下】Phase 3 - 波次切换与战斗完成（第 5-6 周）

#### 目标
实现副本波次切换和战斗完成的通知，完善整体体验

#### 实施步骤

##### Step 3.1: 波次切换通知（3天）
**任务**：
- [ ] 在 `TryScheduleNextWaveIfCleared()` 中添加通知
- [ ] 传递波次信息（当前波次、总波次、下一波怪物预览）
- [ ] 处理波次间隔等待

**产出**：
```csharp
private void TryScheduleNextWaveIfCleared()
{
    // 现有波次切换逻辑...
    
    if (_pendingNextGroup != null)
    {
        // 新增：波次切换通知
        _ = Context.NotificationService?.NotifyWaveCompletedAsync(
            Context.CharacterId,
            WaveIndex - 1,
            WaveIndex,
            _provider?.TotalWaves ?? WaveIndex,
            _pendingSpawnAt ?? Context.Clock.CurrentTime
        );
    }
}
```

##### Step 3.2: 战斗完成通知（2天）
**任务**：
- [ ] 在战斗完成时发送通知
- [ ] 传递战斗结果摘要（时长、击杀数、经验金币）
- [ ] 触发前端停止轮询

**产出**：
```csharp
public async Task<(bool ok, Guid persistedId)> StopAndFinalizeAsync(Guid id, CancellationToken ct = default)
{
    // 现有结算逻辑...
    
    // 新增：战斗完成通知
    await _notificationService.NotifyBattleCompleteAsync(
        rb.CharacterId,
        id,
        new BattleCompleteSummary
        {
            Duration = rb.Clock.CurrentTime,
            TotalKills = rb.Segments.Sum(s => s.Tags.Count(t => t.Key.StartsWith("kill."))),
            GoldEarned = totalGold,
            ExpEarned = totalExp
        }
    );
    
    return (true, persistedBattleId);
}
```

##### Step 3.3: 前端完整流程优化（4天）
**任务**：
- [ ] 订阅波次和完成事件
- [ ] 实现波次过渡动画
- [ ] 优化战斗完成UI
- [ ] 实现奖励展示动画
- [ ] 完善轮询停止逻辑

**产出**：
```csharp
_battleHub.On<WaveCompletedNotification>("WaveCompleted", async notification =>
{
    Console.WriteLine($"[SignalR] Wave {notification.CompletedWave} cleared");
    
    // 显示波次完成UI
    await ShowWaveCompleteUI(notification);
    
    // 如果有等待时间，显示倒计时
    if (notification.NextWaveAt > notification.CompletedAt)
    {
        ShowWaveTransitionCountdown(notification.NextWaveAt - notification.CompletedAt);
    }
    
    // 刷新状态准备下一波
    await TriggerImmediateStatusRefresh();
});

_battleHub.On<BattleCompleteNotification>("BattleComplete", async notification =>
{
    Console.WriteLine($"[SignalR] Battle completed");
    
    // 停止所有轮询
    StopAll();
    
    // 显示战斗完成UI
    await ShowBattleCompleteUI(notification.Summary);
    
    // 播放奖励动画
    await AnimateRewards(notification.Summary);
    
    // 最后一次状态刷新获取完整结算
    await _parent.RefreshSummary();
});
```

##### Step 3.4: 性能优化与监控（3天）
**任务**：
- [ ] 添加 SignalR 消息发送计数和延迟监控
- [ ] 优化消息体积（只传递必要数据）
- [ ] 实现消息队列防止雪崩
- [ ] 添加 SignalR 连接状态监控面板
- [ ] 配置日志和告警

**产出**：
```csharp
// 消息发送监控
public class BattleNotificationService : IBattleNotificationService
{
    private readonly ILogger<BattleNotificationService> _logger;
    private readonly IMetricsCollector _metrics;
    
    public async Task NotifyEnemyKilledAsync(...)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(...);
            _metrics.RecordSignalRMessageSent("EnemyKilled", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send EnemyKilled notification");
            _metrics.RecordSignalRMessageFailed("EnemyKilled");
        }
    }
}
```

##### Step 3.5: 完整测试与文档（2天）
**任务**：
- [ ] 端到端完整战斗流程测试
- [ ] 多玩家并发测试
- [ ] 长时间稳定性测试
- [ ] 编写 SignalR 使用文档
- [ ] 更新前端集成指南

**验收标准**：
- ✅ 完整战斗流程所有关键事件都有实时通知
- ✅ 进度条体验流畅，无跳跃或错位
- ✅ SignalR 消息延迟 < 200ms (P95)
- ✅ 消息送达率 > 99.9%
- ✅ 支持 100+ 并发连接
- ✅ 降级机制工作正常

---

## 五、技术实现细节

### 5.1 服务端架构

#### Hub 设计
```csharp
public class BattleHub : Hub
{
    // 连接管理
    public override async Task OnConnectedAsync()
    public override async Task OnDisconnectedAsync(Exception? exception)
    
    // 客户端可调用的方法
    public async Task JoinBattle(Guid battleId)
    public async Task LeaveBattle(Guid battleId)
    public async Task RequestStateSync()
}
```

#### 通知消息格式
```csharp
// 基础通知接口
public interface IBattleNotification
{
    DateTime Timestamp { get; }
    Guid CharacterId { get; }
}

// 玩家死亡通知
public record PlayerDeathNotification : IBattleNotification
{
    public DateTime Timestamp { get; init; }
    public Guid CharacterId { get; init; }
    public double DeathTime { get; init; }
    public double? ReviveAt { get; init; }
}

// 怪物死亡通知
public record EnemyKilledNotification : IBattleNotification
{
    public DateTime Timestamp { get; init; }
    public Guid CharacterId { get; init; }
    public Guid EnemyId { get; init; }
    public string EnemyName { get; init; }
    public bool IsPrimaryTarget { get; init; }
}
```

#### 连接管理服务
```csharp
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<Guid, string> _characterToConnection = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToCharacter = new();
    
    public Task RegisterConnectionAsync(Guid characterId, string connectionId)
    {
        _characterToConnection[characterId] = connectionId;
        _connectionToCharacter[connectionId] = characterId;
        return Task.CompletedTask;
    }
    
    public Task<string?> GetConnectionIdAsync(Guid characterId)
    {
        _characterToConnection.TryGetValue(characterId, out var connectionId);
        return Task.FromResult(connectionId);
    }
}
```

### 5.2 前端架构

#### SignalR 连接生命周期
```
1. 战斗开始 → 建立 SignalR 连接
2. 订阅所有事件处理器
3. 连接成功 → 发送 JoinBattle 消息
4. 接收事件 → 触发相应处理
5. 战斗结束 → 发送 LeaveBattle 消息
6. 断开连接 → 清理资源
```

#### 降级策略
```csharp
private async Task EnsureHubConnectionAsync()
{
    if (_battleHub?.State == HubConnectionState.Connected)
        return;
    
    try
    {
        await ConnectBattleHubAsync(_parent.lastCreated.Id);
        _signalRMode = true;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "SignalR connection failed, falling back to polling");
        _signalRMode = false;
        // 使用更短的轮询间隔补偿
        _stepPollInterval = 500;
    }
}
```

### 5.3 性能考虑

#### 消息频率控制
```csharp
// 防止短时间内重复发送相同通知
public class BattleNotificationService
{
    private readonly MemoryCache _recentNotifications = new();
    
    private bool ShouldSendNotification(string key)
    {
        if (_recentNotifications.TryGetValue(key, out _))
        {
            return false; // 最近已发送
        }
        
        _recentNotifications.Set(key, true, TimeSpan.FromMilliseconds(100));
        return true;
    }
}
```

#### 批量通知优化
```csharp
// 对于多怪同时死亡的情况，合并通知
public async Task NotifyMultipleEnemiesKilledAsync(
    Guid characterId, 
    List<EnemyKillInfo> kills)
{
    // 单次 SignalR 消息发送多个击杀信息
    await _hubContext.Clients.Client(connectionId).SendAsync(
        "BatchEnemyKilled",
        new BatchEnemyKilledNotification
        {
            Kills = kills,
            Timestamp = DateTime.UtcNow
        });
}
```

---

## 六、验收标准文档

### 6.1 功能验收

#### Phase 1 验收清单
- [ ] **连接建立**
  - [ ] 前端成功连接到 BattleHub
  - [ ] 连接带有正确的角色ID参数
  - [ ] 连接管理器正确映射 characterId ↔ connectionId

- [ ] **玩家死亡通知**
  - [ ] 玩家死亡后 100ms 内收到 SignalR 通知
  - [ ] 通知包含正确的死亡时间和复活时间
  - [ ] 前端立即触发状态轮询
  - [ ] 前端显示死亡状态和复活倒计时
  - [ ] 进度条正确暂停

- [ ] **玩家复活通知**
  - [ ] 玩家复活时收到 SignalR 通知
  - [ ] 前端恢复进度条和战斗显示
  - [ ] 轮询间隔恢复正常

- [ ] **降级处理**
  - [ ] SignalR 连接失败自动回退轮询
  - [ ] 断线后自动尝试重连
  - [ ] 重连成功后恢复 SignalR 模式

#### Phase 2 验收清单
- [ ] **怪物死亡通知**
  - [ ] 怪物死亡后 100ms 内收到通知
  - [ ] 通知包含怪物ID、名称、是否主目标
  - [ ] 前端进度条立即重置
  - [ ] 显示击杀特效
  - [ ] 连击计数器正确更新

- [ ] **目标切换通知**
  - [ ] 主目标死亡后立即收到切换通知
  - [ ] 新目标正确高亮
  - [ ] 进度条针对新目标重置

- [ ] **多怪击杀处理**
  - [ ] 多怪同时死亡时正确接收所有通知
  - [ ] 不出现通知丢失或重复
  - [ ] 性能无明显下降

#### Phase 3 验收清单
- [ ] **波次切换通知**
  - [ ] 波次清除后收到通知
  - [ ] 显示波次完成UI
  - [ ] 波次间隔倒计时正确
  - [ ] 新波次刷新后状态同步

- [ ] **战斗完成通知**
  - [ ] 战斗完成时收到通知
  - [ ] 轮询正确停止
  - [ ] 显示结算UI和奖励动画
  - [ ] 最终状态数据完整

### 6.2 性能验收

#### 延迟指标
| 指标 | 目标值 | 测量方法 |
|-----|--------|---------|
| SignalR 消息延迟 (P50) | < 50ms | 服务端发送到客户端接收的时间差 |
| SignalR 消息延迟 (P95) | < 200ms | 同上 |
| SignalR 消息延迟 (P99) | < 500ms | 同上 |
| 事件触发到前端更新 | < 300ms | 事件发生到UI更新完成 |

#### 可靠性指标
| 指标 | 目标值 | 测量方法 |
|-----|--------|---------|
| 消息送达率 | > 99.9% | 发送消息数 / 成功接收消息数 |
| 连接成功率 | > 99% | 成功连接数 / 尝试连接数 |
| 自动重连成功率 | > 95% | 重连成功数 / 断线次数 |
| 降级触发准确性 | 100% | 连接失败后是否正确回退轮询 |

#### 并发性能
| 场景 | 目标 | 测试方法 |
|-----|------|---------|
| 并发连接数 | ≥ 100 | 模拟100个客户端同时连接 |
| 消息吞吐量 | ≥ 1000条/秒 | 压力测试高频事件场景 |
| 服务器CPU使用率 | < 30% | 在50并发连接下监控 |
| 服务器内存使用 | < 500MB增量 | 在100并发连接下监控 |

### 6.3 兼容性验收

#### 浏览器兼容性
- [ ] Chrome (最新版)
- [ ] Firefox (最新版)
- [ ] Safari (最新版)
- [ ] Edge (最新版)

#### 网络环境测试
- [ ] 正常网络 (< 50ms 延迟)
- [ ] 慢速网络 (200-500ms 延迟)
- [ ] 不稳定网络 (间歇性丢包)
- [ ] 移动网络 (4G/5G)

#### 降级场景测试
- [ ] SignalR 服务不可用
- [ ] 连接过程中断
- [ ] 消息发送失败
- [ ] 客户端离线后重新上线

### 6.4 用户体验验收

#### 视觉反馈
- [ ] 死亡瞬间有明显视觉反馈（屏幕效果、音效）
- [ ] 击杀特效流畅且不遮挡重要信息
- [ ] 波次切换过渡自然
- [ ] 进度条移动平滑，无跳跃

#### 响应性
- [ ] 所有关键事件反馈在 300ms 内完成
- [ ] 无明显卡顿或延迟感
- [ ] UI 更新不影响其他交互

#### 稳定性
- [ ] 长时间战斗（30分钟+）无内存泄漏
- [ ] 频繁连接/断开不导致错误
- [ ] 多次战斗开始/结束状态正确

---

## 七、监控与维护

### 7.1 关键监控指标

#### SignalR 连接指标
```
- signalr_active_connections: 当前活跃连接数
- signalr_connection_duration_seconds: 连接持续时间分布
- signalr_connection_errors_total: 连接错误总数
- signalr_reconnect_attempts_total: 重连尝试次数
```

#### 消息指标
```
- signalr_messages_sent_total{event_type}: 各类型消息发送总数
- signalr_messages_failed_total{event_type}: 消息发送失败总数
- signalr_message_latency_seconds{event_type}: 消息延迟分布
- signalr_message_size_bytes{event_type}: 消息体积分布
```

#### 业务指标
```
- battle_death_notification_delay_ms: 死亡通知延迟
- battle_kill_notification_delay_ms: 击杀通知延迟
- battle_events_per_second: 每秒战斗事件数
- polling_triggered_by_signalr_total: SignalR 触发的轮询次数
```

### 7.2 告警规则

#### 高优先级告警
```
- SignalR 消息送达率 < 95% (持续 5 分钟)
- 消息平均延迟 > 1秒 (持续 5 分钟)
- 连接失败率 > 10% (持续 5 分钟)
- 活跃连接数突然下降 > 50%
```

#### 中优先级告警
```
- 重连失败率 > 20% (持续 10 分钟)
- 消息发送失败率 > 5% (持续 10 分钟)
- SignalR Hub 异常 (任何错误)
```

### 7.3 日志规范

#### 结构化日志
```csharp
_logger.LogInformation(
    "SignalR notification sent: {EventType}, CharacterId: {CharacterId}, Latency: {LatencyMs}ms",
    eventType,
    characterId,
    latencyMs
);
```

#### 关键事件日志
- 连接建立/断开
- 消息发送成功/失败
- 降级触发
- 异常和错误

---

## 八、风险与缓解措施

### 8.1 技术风险

#### 风险1: SignalR 连接不稳定
**影响**: 用户体验下降，消息丢失
**缓解措施**:
- 实现自动重连机制（指数退避）
- 完善的降级到轮询策略
- 消息确认机制（可选）
- 定期心跳检测

#### 风险2: 服务器负载增加
**影响**: 系统性能下降
**缓解措施**:
- 消息批量发送
- 消息频率限制
- 连接数限制
- 水平扩展准备（Redis backplane）

#### 风险3: 消息时序问题
**影响**: 前端状态不一致
**缓解措施**:
- 消息携带时间戳
- 前端验证消息顺序
- 定期轮询同步修正
- 事件幂等性设计

### 8.2 兼容性风险

#### 风险4: 旧客户端不支持 SignalR
**影响**: 部分用户无法使用新功能
**缓解措施**:
- SignalR 作为增强功能，非强制
- 纯轮询模式始终可用
- 客户端版本检测
- 渐进式推出

#### 风险5: 防火墙/代理阻止 WebSocket
**影响**: SignalR 连接失败
**缓解措施**:
- SignalR 自动降级到 Long Polling
- 服务端支持多种传输协议
- 提供网络诊断工具

### 8.3 运维风险

#### 风险6: 部署复杂度增加
**影响**: 部署失败或配置错误
**缓解措施**:
- 详细的部署文档
- 配置验证脚本
- 灰度发布策略
- 快速回滚方案

#### 风险7: 监控盲区
**影响**: 问题发现延迟
**缓解措施**:
- 完善的监控指标
- 实时告警机制
- 日志聚合和分析
- 定期健康检查

---

## 九、后续优化方向

### 9.1 Phase 4 - 高级特性（第 7-8 周）

#### 消息压缩
- 实现 MessagePack 序列化减少消息体积
- 差量更新而非全量数据

#### 离线消息队列
- 玩家离线时缓存重要事件
- 上线后批量推送

#### 跨服务器支持
- 配置 Redis backplane
- 支持多实例部署
- 负载均衡

### 9.2 Phase 5 - 扩展应用（第 9-10 周）

#### 更多事件类型
- 技能释放通知
- 重要Buff触发
- 稀有掉落通知
- 成就解锁通知

#### 社交功能
- 队友状态同步
- 公会聊天
- 实时排行榜

#### 管理功能
- 实时玩家监控
- GM 工具集成
- 系统广播

---

## 十、总结

### 10.1 核心价值

#### 用户体验提升
- **即时反馈**: 关键事件延迟从 0.5-5 秒降低到 < 0.3 秒
- **流畅进度条**: 消除进度条跳跃和错位
- **战斗感增强**: 即时打击反馈和视觉效果

#### 技术优化
- **带宽节省**: 稳定状态减少 70% 轮询请求
- **服务器负载**: 降低无效轮询造成的CPU开销
- **可扩展性**: 为未来社交和实时功能奠定基础

### 10.2 实施原则

#### 渐进式实施
- 分阶段推进，每阶段都有独立价值
- 先核心事件，再扩展功能
- 充分测试后再进入下一阶段

#### 兼容性优先
- SignalR 作为增强而非替代
- 轮询机制完整保留
- 降级策略自动生效

#### 监控驱动
- 全面的性能指标
- 实时告警机制
- 持续优化迭代

### 10.3 成功标准

#### 短期目标（Phase 1-2）
- ✅ 玩家死亡/复活实时通知
- ✅ 怪物击杀即时反馈
- ✅ 进度条体验流畅

#### 中期目标（Phase 3）
- ✅ 完整战斗流程覆盖
- ✅ 性能指标达标
- ✅ 稳定性验证

#### 长期目标（Phase 4-5）
- ✅ 支持更多事件类型
- ✅ 扩展到社交功能
- ✅ 多服务器部署

---

## 附录

### A. 参考文档
- [ASP.NET Core SignalR 官方文档](https://docs.microsoft.com/aspnet/core/signalr/)
- [SignalR 性能优化指南](https://docs.microsoft.com/aspnet/core/signalr/scale)
- [整合设计总结.txt](../整合设计总结.txt)
- [前端UI优化设计方案.md](./前端UI优化设计方案.md)
- [POLLING_HINT_IMPLEMENTATION.md](./POLLING_HINT_IMPLEMENTATION.md)

### B. 相关代码文件
- `BlazorIdle.Server/Domain/Combat/Engine/BattleEngine.cs`
- `BlazorIdle.Server/Domain/Combat/PlayerDeathEvent.cs`
- `BlazorIdle.Server/Application/Battles/Step/StepBattleCoordinator.cs`
- `BlazorIdle/Pages/Characters.razor` - BattlePollingCoordinator

### C. 技术栈
- **服务端**: ASP.NET Core 9.0, SignalR
- **前端**: Blazor WebAssembly, SignalR Client
- **传输协议**: WebSocket (优先), Long Polling (降级)
- **序列化**: JSON (初期), MessagePack (优化)

---

**文档结束**
