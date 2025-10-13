# BlazorIdle SignalR 实时通知集成方案

**文档版本**: 1.0  
**创建日期**: 2025-10-13  
**状态**: 需求分析与设计方案  
**作者**: 系统分析

---

## 📋 目录

1. [项目背景分析](#1-项目背景分析)
2. [当前系统架构评估](#2-当前系统架构评估)
3. [事件分类与通知策略](#3-事件分类与通知策略)
4. [SignalR集成方案（上）- 架构设计](#4-signalr集成方案上---架构设计)
5. [SignalR集成方案（中）- 实施细节](#5-signalr集成方案中---实施细节)
6. [SignalR集成方案（下）- 优化与扩展](#6-signalr集成方案下---优化与扩展)
7. [实施路线图](#7-实施路线图)
8. [验收标准](#8-验收标准)

---

## 1. 项目背景分析

### 1.1 需求概述

**核心目标**：将部分无法预测的突发战斗事件从轮询机制改为SignalR主动推送，提升用户体验的实时性和流畅度。

**用户体验期望**：
- ✅ 前端自然地模拟进度条进度推进
- ✅ 怪物或玩家死亡时立即打断/重置进度
- ✅ 用户能感受到准确的战斗状态
- ✅ 配合现有自适应轮询系统

**设计原则**：
- 维持现有代码风格
- 向后兼容现有轮询机制
- SignalR作为轮询的增强，而非替代
- 渐进式增强，可逐步细化通知内容

### 1.2 当前系统评估

#### 已完成的基础设施

**✅ 自适应轮询系统** (已实现)
- `PollingHint` 机制：服务器根据战斗状态返回建议轮询间隔
- 智能轮询策略：
  - 战斗已完成: 5000ms (稳定)
  - 玩家死亡: 2000ms (稳定)
  - 玩家血量<50%: 1000ms (激烈，不稳定)
  - 正常战斗: 2000ms (稳定)
- `StepBattleCoordinator.GetStatus()` 中实现的 `CalculatePollingHint()` 方法

**✅ 事件驱动架构** (已完成)
- `IGameEvent` 接口和 `EventScheduler` 事件调度系统
- 现有事件类型：
  - `AttackTickEvent` - 普通攻击事件
  - `SpecialPulseEvent` - 特殊轨道脉冲
  - `PlayerDeathEvent` - 玩家死亡事件
  - `PlayerReviveEvent` - 玩家复活事件
  - `EnemyAttackEvent` - 怪物攻击事件
- `SegmentCollector` - 战斗事件聚合收集器
- `BattleContext` - 战斗上下文管理

**✅ 前端进度条系统** (已完成)
- 攻击进度条（普通攻击 & 特殊攻击）
- 基于轮询数据的进度条模拟
- 100ms 动画定时器（客户端预测）

#### 当前问题识别

| 问题类型 | 描述 | 影响 |
|---------|------|------|
| **延迟感知** | 死亡事件需等待下次轮询才能获知 | 进度条继续推进，显示不准确 |
| **状态不同步** | 目标切换、怪物死亡等事件有延迟 | 用户看到错误的战斗状态 |
| **轮询浪费** | 激烈战斗时1000ms轮询仍可能错过关键时刻 | 需要更短轮询间隔，增加服务器负载 |
| **用户体验** | 无法立即响应突发事件 | 战斗感受不够即时和流畅 |

---

## 2. 当前系统架构评估

### 2.1 服务端架构

```
BlazorIdle.Server/
├── Application/
│   └── Battles/
│       ├── Step/
│       │   └── StepBattleCoordinator.cs       # 战斗协调器
│       └── BattleRunner.cs                     # 战斗运行器
├── Domain/
│   └── Combat/
│       ├── IGameEvent.cs                       # 事件接口
│       ├── EventScheduler.cs                   # 事件调度器
│       ├── PlayerDeathEvent.cs                 # ★ 需SignalR
│       ├── PlayerReviveEvent.cs                # ★ 需SignalR
│       ├── EnemyAttackEvent.cs                 # ★ 可能需要
│       ├── AttackTickEvent.cs                  # 不需要
│       ├── SpecialPulseEvent.cs                # 不需要
│       ├── SegmentCollector.cs                 # 事件聚合
│       └── BattleContext.cs                    # 战斗上下文
└── Api/
    └── Controllers/                            # REST API控制器
```

### 2.2 前端架构

```
BlazorIdle/
├── Pages/
│   └── Characters.razor                        # 主战斗界面
├── Components/
│   ├── PlayerStatusPanel.razor                 # 玩家状态面板
│   ├── MonsterStatusPanel.razor                # 怪物状态面板
│   └── DungeonProgressPanel.razor              # 地下城进度面板
└── Services/
    ├── ApiClient.cs                            # HTTP客户端
    └── ApiModels.cs                            # 数据模型
```

### 2.3 关键集成点

| 集成位置 | 现有机制 | SignalR增强点 |
|---------|---------|--------------|
| **StepBattleCoordinator** | 管理战斗实例，提供GetStatus | 注入SignalR Hub，关键事件时推送 |
| **EventScheduler** | 调度事件执行 | 事件执行后判断是否需要推送 |
| **PlayerDeathEvent.Execute()** | 记录死亡标记 | 推送死亡通知到前端 |
| **Characters.razor** | 轮询GetStatus | 监听SignalR，立即更新UI |

---

## 3. 事件分类与通知策略

### 3.1 事件分类矩阵

基于事件的**可预测性**、**重要性**、**频率**三个维度，将事件分为四类：

#### 🔴 **A类：必须SignalR通知**
**特征**：不可预测 + 高重要性 + 需立即响应

| 事件 | 重要性 | 频率 | 可预测性 | 当前实现 | 影响 |
|-----|--------|------|---------|---------|------|
| **玩家死亡** | ⭐⭐⭐ | 低-中 | 不可预测 | `PlayerDeathEvent` | 立即打断所有进度条，显示死亡状态 |
| **玩家复活** | ⭐⭐⭐ | 低-中 | 可预测时间但用户关注 | `PlayerReviveEvent` | 立即恢复战斗，重新启动进度条 |
| **怪物全灭** | ⭐⭐⭐ | 中 | 不可预测 | `TagCounter: kill.*` | 切换波次，可能进入Boss战或完成战斗 |
| **Boss出现** | ⭐⭐⭐ | 低 | 可预测波次但需强调 | 地下城波次切换 | 特殊UI提示，用户调整策略 |
| **副本完成** | ⭐⭐⭐ | 低 | 可预测但需确认 | `dungeon_run_complete` | 显示奖励，停止战斗 |

**推送策略**：
- 推送简化通知：`{ "eventType": "PlayerDeath", "battleId": "...", "timestamp": ... }`
- 前端收到后**立即触发一次完整状态抓取**
- 后期可细化通知内容（如死亡原因、复活倒计时等）

#### 🟡 **B类：可选SignalR通知**
**特征**：中等重要性 + 用户关注 + 轮询延迟可接受

| 事件 | 重要性 | 频率 | 可预测性 | 当前实现 | 建议 |
|-----|--------|------|---------|---------|------|
| **单个怪物死亡** | ⭐⭐ | 高 | 不可预测 | `TagCounter: kill.*` | Phase 2：批量推送（每5秒汇总） |
| **攻击目标切换** | ⭐⭐ | 中 | 不可预测 | 隐式在Status中 | Phase 2：目标变更通知 |
| **Buff获得/失效** | ⭐⭐ | 中-高 | 可预测时间 | `BuffManager` | Phase 3：重要Buff通知 |
| **资源溢出** | ⭐ | 低 | 可预测 | `ResourceBucket` | 轮询足够 |

**推送策略**：
- Phase 1：不推送，依赖轮询
- Phase 2-3：批量聚合推送，避免消息风暴

#### 🟢 **C类：仅轮询**
**特征**：可预测 + 低重要性 + 高频率

| 事件 | 重要性 | 频率 | 可预测性 | 说明 |
|-----|--------|------|---------|------|
| **普通攻击** | ⭐ | 极高 | 高度可预测 | 前端基于AttackInterval预测进度 |
| **特殊攻击** | ⭐ | 高 | 高度可预测 | 前端基于SpecialInterval预测 |
| **资源变化** | ⭐ | 极高 | 可预测 | 轮询获取当前值 |
| **伤害数值** | ⭐ | 极高 | 可预测 | 通过SegmentCollector聚合，轮询获取 |
| **经验/金币** | ⭐ | 高 | 可预测 | 批量结算，轮询显示 |

**处理策略**：
- 维持现有轮询机制
- 前端基于已知的攻击间隔做客户端预测
- 轮询同步修正预测偏差

#### 🔵 **D类：后台事件（不需通知）**
**特征**：内部逻辑 + 用户无感知

| 事件 | 说明 |
|-----|------|
| **SegmentFlush** | 内部数据聚合，用户不关心 |
| **RNG索引记录** | 调试用，用户不可见 |
| **内部状态机转换** | 底层逻辑，无需展示 |

### 3.2 进度条打断场景分析

**场景1：玩家死亡**
```
[当前] 进度条继续推进 → 下次轮询(最多1-2秒) → 显示死亡
[优化] 死亡瞬间SignalR推送 → 前端立即打断所有进度条 → 显示死亡倒计时
```

**场景2：怪物全灭（波次切换）**
```
[当前] 进度条继续 → 轮询发现新波次 → 重置进度条
[优化] 全灭瞬间SignalR推送 → 立即切换UI → 显示新波次信息
```

**场景3：Boss出现**
```
[当前] 轮询发现Boss → 调整UI
[优化] Boss出现时SignalR推送 → 立即显示Boss警告 → 突出展示
```

**场景4：正常战斗（保持现状）**
```
[保持] 前端基于AttackInterval预测进度 → 轮询同步修正 → 流畅体验
```

### 3.3 决策总结表

| 决策点 | 结论 | 理由 |
|-------|------|------|
| **哪些事件用SignalR** | A类（必须）+ B类Phase 2+ | 平衡实时性与复杂度 |
| **哪些事件仅轮询** | C类（高频可预测）+ D类（后台） | 避免消息风暴，轮询足够 |
| **SignalR通知内容** | Phase 1: 简化通知+触发抓取 | 最小化变更，快速上线 |
|  | Phase 2-3: 细化内容 | 减少抓取次数，提升性能 |
| **轮询间隔调整** | 维持PollingHint机制 | SignalR覆盖关键事件后，可适当延长稳定态轮询 |
| **兼容性** | SignalR可选，轮询保底 | 网络问题或不支持WebSocket时降级 |

---

## 4. SignalR集成方案（上）- 架构设计

### 4.1 总体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                         前端 (Blazor WASM)                      │
├─────────────────────────────────────────────────────────────────┤
│  Characters.razor                                                │
│  ├─ 轮询任务（PollingHint自适应）                               │
│  └─ SignalR连接                                                  │
│     ├─ OnBattleEvent(notification) ──┐                          │
│     └─ OnConnectionError() ──────────┼─> 降级到纯轮询          │
│                                       │                          │
│  收到通知 ─> 立即触发状态抓取 ───────┘                          │
│             ↓                                                    │
│  1. 打断进度条动画                                               │
│  2. 调用 GetStatus()                                             │
│  3. 更新UI状态                                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↕ WebSocket / HTTP
┌─────────────────────────────────────────────────────────────────┐
│                      服务端 (ASP.NET Core)                      │
├─────────────────────────────────────────────────────────────────┤
│  BattleHub (SignalR Hub)                                         │
│  ├─ SubscribeToBattle(battleId)                                 │
│  ├─ UnsubscribeFromBattle(battleId)                             │
│  └─ [内部] SendBattleEvent(battleId, notification)              │
│                                                                  │
│  IBattleNotificationService (接口)                               │
│  └─ NotifyBattleEvent(battleId, eventType, metadata)            │
│                                                                  │
│  StepBattleCoordinator (协调器)                                  │
│  ├─ 注入 IBattleNotificationService                              │
│  └─ 关键事件触发时调用 NotifyBattleEvent()                      │
│                                                                  │
│  PlayerDeathEvent.Execute(context)                               │
│  ├─ 执行死亡逻辑                                                 │
│  └─ context.NotificationService.NotifyBattleEvent(              │
│         context.BattleId,                                        │
│         "PlayerDeath",                                           │
│         new { timestamp = context.Clock.CurrentTime }           │
│     )                                                            │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 核心设计原则

1. **最小侵入性**：SignalR作为现有系统的增强层，不改变核心战斗逻辑
2. **可选依赖**：`BattleContext.NotificationService` 为可选，不注入时系统正常运行
3. **降级兼容**：SignalR连接失败时自动降级到纯轮询模式
4. **异步非阻塞**：通知推送不阻塞战斗逻辑执行（使用 `_ = Task.Run()`）
5. **事件分组**：使用SignalR Groups功能，仅向订阅特定战斗的客户端推送
6. **向后兼容**：现有轮询机制完全保留，作为数据同步的保底方案

### 4.3 关键技术决策

| 决策点 | 选择 | 理由 |
|-------|------|------|
| **通知模式** | Server-to-Client单向推送 | 简化实现，前端仅接收不回复 |
| **订阅机制** | 基于BattleId的Groups | 精准推送，避免广播浪费 |
| **通知时机** | Event.Execute()内部 | 最及时，与事件发生同步 |
| **通知内容** | Phase 1简化+触发抓取 | 最小化变更，降低风险 |
| **连接管理** | 单例HubConnection | 复用连接，减少开销 |
| **错误处理** | 自动重连+降级 | 提升健壮性 |
| **依赖注入** | 接口+实现分离 | 便于测试和扩展 |


---

## 5. SignalR集成方案（中）- 实施细节

### 5.1 服务端实施清单

#### 5.1.1 新增文件列表

| 文件路径 | 用途 | 代码行数估算 |
|---------|------|------------|
| `BlazorIdle.Server/Application/Battles/IBattleNotificationService.cs` | 通知服务接口 | ~30行 |
| `BlazorIdle.Server/Application/Battles/BattleNotificationService.cs` | 通知服务实现 | ~80行 |
| `BlazorIdle.Server/Hubs/BattleHub.cs` | SignalR Hub | ~50行 |
| `BlazorIdle.Shared/Models/BattleEventNotification.cs` | 通知数据模型 | ~40行 |

#### 5.1.2 修改文件列表

| 文件路径 | 修改内容 | 复杂度 |
|---------|---------|--------|
| `BlazorIdle.Server/Program.cs` | 添加SignalR服务注册和端点映射 | 🟢 简单（5行） |
| `BlazorIdle.Server/Domain/Combat/BattleContext.cs` | 添加NotificationService和BattleId字段 | 🟢 简单（10行） |
| `BlazorIdle.Server/Domain/Combat/PlayerDeathEvent.cs` | 添加SignalR通知调用 | 🟢 简单（10行） |
| `BlazorIdle.Server/Domain/Combat/PlayerReviveEvent.cs` | 添加SignalR通知调用 | 🟢 简单（10行） |
| `BlazorIdle.Server/Application/Battles/Step/StepBattleCoordinator.cs` | 注入服务+波次通知逻辑 | 🟡 中等（40行） |

### 5.2 前端实施清单

#### 5.2.1 新增文件列表

| 文件路径 | 用途 | 代码行数估算 |
|---------|------|------------|
| `BlazorIdle/Services/BattleHubConnection.cs` | SignalR连接管理服务 | ~150行 |

#### 5.2.2 修改文件列表

| 文件路径 | 修改内容 | 复杂度 |
|---------|---------|--------|
| `BlazorIdle/Program.cs` | 注册BattleHubConnection服务 | 🟢 简单（2行） |
| `BlazorIdle/Pages/Characters.razor` | 集成SignalR+事件处理逻辑 | 🔴 复杂（100行） |
| `BlazorIdle/BlazorIdle.csproj` | 添加SignalR客户端包引用 | 🟢 简单（3行） |

### 5.3 关键代码片段

#### 5.3.1 PlayerDeathEvent通知集成

```csharp
// BlazorIdle.Server/Domain/Combat/PlayerDeathEvent.cs

public void Execute(BattleContext context)
{
    var player = context.Player;
    
    if (player.State != CombatantState.Dead || !player.DeathTime.HasValue)
        return;
    
    // [现有逻辑] 暂停轨道、取消技能施放
    const double FAR_FUTURE = 1e10;
    foreach (var track in context.Tracks)
    {
        track.NextTriggerAt = FAR_FUTURE;
    }
    
    if (context.AutoCaster.IsCasting)
    {
        context.AutoCaster.ClearCasting();
    }
    
    // [现有逻辑] 调度复活事件
    if (player.AutoReviveEnabled && player.ReviveAt.HasValue)
    {
        context.Scheduler.Schedule(new PlayerReviveEvent(player.ReviveAt.Value));
    }
    
    // [现有逻辑] 记录统计
    context.SegmentCollector.OnTag("player_death", 1);
    
    // ✨ 新增：SignalR实时通知
    if (context.NotificationService != null && context.BattleId != Guid.Empty)
    {
        // 异步推送，不阻塞战斗逻辑
        _ = Task.Run(async () =>
        {
            try
            {
                await context.NotificationService.NotifyBattleEventAsync(
                    context.BattleId,
                    "PlayerDeath",
                    new
                    {
                        deathTime = ExecuteAt,
                        reviveAt = player.ReviveAt,
                        autoRevive = player.AutoReviveEnabled
                    }
                );
            }
            catch (Exception ex)
            {
                // 静默失败，不影响战斗逻辑
                Console.WriteLine($"SignalR notification failed: {ex.Message}");
            }
        });
    }
}
```

#### 5.3.2 前端事件处理逻辑

```csharp
// BlazorIdle/Pages/Characters.razor

@inject BattleHubConnection BattleHub

private Guid? _currentBattleId = null;
private bool _signalRConnected = false;

protected override async Task OnInitializedAsync()
{
    try
    {
        await BattleHub.InitializeAsync();
        BattleHub.OnBattleEventReceived += HandleBattleEvent;
        BattleHub.OnConnectionError += HandleConnectionError;
        _signalRConnected = BattleHub.IsConnected;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "SignalR初始化失败，使用纯轮询模式");
        _signalRConnected = false;
    }
    
    await base.OnInitializedAsync();
}

private void HandleBattleEvent(BattleEventNotification notification)
{
    InvokeAsync(async () =>
    {
        _logger.LogInformation("收到战斗事件: {EventType}", notification.EventType);
        
        switch (notification.EventType)
        {
            case "PlayerDeath":
                // 立即停止进度条动画
                StopProgressBars();
                // 显示死亡状态（可选：从通知元数据解析）
                await RefreshBattleStatusAsync();
                ShowToast("💀 角色已死亡", "error");
                break;
                
            case "PlayerRevive":
                await RefreshBattleStatusAsync();
                ShowToast("✨ 角色已复活", "success");
                break;
                
            case "WaveComplete":
                await RefreshBattleStatusAsync();
                ShowToast("✅ 波次完成", "success");
                break;
                
            case "BossAppear":
                await RefreshBattleStatusAsync();
                ShowToast("⚠️ Boss出现！", "warning");
                break;
                
            case "DungeonComplete":
                await RefreshBattleStatusAsync();
                ShowToast("🎉 副本完成！", "success");
                break;
        }
        
        StateHasChanged();
    });
}

private async Task StartBattleAsync()
{
    // ... 现有战斗启动逻辑 ...
    
    // 订阅SignalR通知
    if (_signalRConnected && _currentBattleId.HasValue)
    {
        try
        {
            await BattleHub.SubscribeToBattleAsync(_currentBattleId.Value);
            _logger.LogInformation("已订阅战斗 {BattleId} 的实时通知", _currentBattleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "订阅SignalR失败，仅使用轮询");
        }
    }
}

private async Task StopBattleAsync()
{
    // 取消订阅
    if (_currentBattleId.HasValue)
    {
        try
        {
            await BattleHub.UnsubscribeFromBattleAsync(_currentBattleId.Value);
        }
        catch { /* 静默失败 */ }
    }
    
    // ... 现有停止逻辑 ...
}

private void StopProgressBars()
{
    _attackProgress = 0;
    _specialProgress = 0;
    // 可以添加视觉效果，如进度条闪烁红色
}
```

### 5.4 配置与依赖注入

#### Program.cs完整配置
```csharp
// BlazorIdle.Server/Program.cs

var builder = WebApplication.CreateBuilder(args);

// ... 现有服务 ...

// ✨ 添加SignalR服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// ✨ 注册战斗通知服务
builder.Services.AddSingleton<IBattleNotificationService, BattleNotificationService>();

var app = builder.Build();

// ... 现有中间件 ...

// ✨ 映射SignalR Hub端点
app.MapHub<BattleHub>("/battleHub");

app.Run();
```

#### 前端服务注册
```csharp
// BlazorIdle/Program.cs

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ... 现有服务 ...

// ✨ 注册SignalR连接服务（单例）
builder.Services.AddSingleton<BattleHubConnection>();

await builder.Build().RunAsync();
```

---

## 6. SignalR集成方案（下）- 优化与扩展

### 6.1 性能优化策略

#### 6.1.1 连接池管理

**问题**：大量并发战斗时，每个客户端一个持久连接可能导致服务器资源消耗过高

**优化方案**：
1. **连接复用**：一个客户端使用单个SignalR连接，可订阅多个战斗（当前设计已实现）
2. **连接限流**：配置最大并发连接数
   ```csharp
   // Program.cs
   builder.Services.AddSignalR(options =>
   {
       options.MaximumParallelInvocationsPerClient = 1;
       options.StreamBufferCapacity = 10;
   });
   ```
3. **心跳优化**：根据服务器负载动态调整心跳间隔
4. **闲置断开**：超过一定时间无战斗的连接自动断开

#### 6.1.2 消息聚合

**问题**：高频事件（如单个怪物死亡）可能产生消息风暴

**解决方案**：批量通知服务（Phase 2实现）
```csharp
// 示例：5秒内的多个击杀事件聚合为一条消息
{
  "eventType": "BatchKills",
  "battleId": "xxx",
  "data": {
    "kills": [
      { "enemyId": "goblin_1", "time": 12.5 },
      { "enemyId": "goblin_2", "time": 14.2 }
    ],
    "totalCount": 2
  }
}
```

### 6.2 监控与诊断

#### 6.2.1 关键指标

| 指标名称 | 用途 | 告警阈值建议 |
|---------|------|------------|
| `signalr_active_connections` | 当前活跃连接数 | > 5000 |
| `signalr_messages_sent_per_sec` | 每秒发送消息数 | > 1000 |
| `signalr_connection_failures` | 连接失败次数 | > 100/分钟 |
| `battle_events_without_notification` | 未发送通知的关键事件 | > 0 |
| `client_signalr_reconnect_count` | 前端重连次数 | > 10/会话 |

#### 6.2.2 日志记录

```csharp
// BattleNotificationService.cs

public async Task NotifyBattleEventAsync(Guid battleId, string eventType, object? metadata = null)
{
    var sw = Stopwatch.StartNew();
    try
    {
        await _hubContext.Clients
            .Group($"Battle_{battleId}")
            .SendAsync("ReceiveBattleEvent", notification);
        
        _logger.LogDebug("战斗事件已发送: {EventType} for {BattleId} in {ElapsedMs}ms",
            eventType, battleId, sw.ElapsedMilliseconds);
        
        // 监控指标
        _metrics.IncrementCounter("signalr_messages_sent", new[] { ("event_type", eventType) });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "发送战斗事件失败: {EventType} for {BattleId}", eventType, battleId);
        _metrics.IncrementCounter("signalr_send_failures", new[] { ("event_type", eventType) });
    }
    finally
    {
        sw.Stop();
        _metrics.RecordHistogram("signalr_send_duration_ms", sw.ElapsedMilliseconds);
    }
}
```

### 6.3 安全性考虑

#### 6.3.1 认证与授权

**Phase 1实现**：基于已有的JWT认证
```csharp
// Program.cs

app.MapHub<BattleHub>("/battleHub")
   .RequireAuthorization(); // 要求认证

// BattleHub.cs

public override async Task OnConnectedAsync()
{
    // 从上下文获取用户信息
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        Context.Abort();
        return;
    }
    
    _logger.LogInformation("用户 {UserId} 建立SignalR连接: {ConnectionId}", 
        userId, Context.ConnectionId);
    
    await base.OnConnectedAsync();
}
```

#### 6.3.2 订阅验证

**问题**：恶意客户端订阅他人的战斗ID

**解决方案**：在订阅时验证战斗所有权
```csharp
// BattleHub.cs

public async Task SubscribeToBattle(Guid battleId)
{
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        throw new HubException("未认证");
    }
    
    // 验证战斗是否属于该用户
    var battle = await _battleService.GetBattleAsync(battleId);
    if (battle == null || battle.UserId != Guid.Parse(userId))
    {
        _logger.LogWarning("用户 {UserId} 尝试订阅非本人战斗 {BattleId}", userId, battleId);
        throw new HubException("无权订阅该战斗");
    }
    
    await Groups.AddToGroupAsync(Context.ConnectionId, $"Battle_{battleId}");
    _logger.LogDebug("用户 {UserId} 订阅战斗 {BattleId}", userId, battleId);
}
```

### 6.4 降级与容错

#### 6.4.1 自动降级策略

```csharp
// BattleHubConnection.cs

public class BattleHubConnection : IAsyncDisposable
{
    private int _consecutiveFailures = 0;
    private const int MaxFailuresBeforeFallback = 3;
    
    public bool IsHealthy => _consecutiveFailures < MaxFailuresBeforeFallback;
    
    private void OnConnectionError(Exception error)
    {
        _consecutiveFailures++;
        
        if (_consecutiveFailures >= MaxFailuresBeforeFallback)
        {
            _logger.LogWarning("SignalR连续失败 {Count} 次，建议使用纯轮询模式", _consecutiveFailures);
            OnSuggestFallbackToPolling?.Invoke();
        }
    }
    
    private void OnReconnected()
    {
        _consecutiveFailures = 0;
        _logger.LogInformation("SignalR重连成功");
    }
}
```

#### 6.4.2 前端降级逻辑

```csharp
// Characters.razor

private async Task MonitorSignalRHealth()
{
    if (!BattleHub.IsHealthy)
    {
        _logger.LogWarning("SignalR不健康，增加轮询频率作为补偿");
        _pollingInterval = Math.Min(_pollingInterval, 1000); // 最短1秒轮询
        ShowToast("实时通知不稳定，已增强轮询同步", "warning");
    }
}
```

---

## 7. 实施路线图

### 7.1 Phase 1: 核心事件推送（基础版）

**目标**：实现A类事件的SignalR通知，覆盖玩家死亡/复活、波次完成等关键场景

**工期**：5-7个工作日

**实施步骤**：

#### Step 1.1: 服务端基础设施（2天）

| 任务 | 负责模块 | 验收标准 |
|-----|---------|---------|
| 添加SignalR服务配置 | `Program.cs` | SignalR服务注册成功，/battleHub端点可访问 |
| 创建IBattleNotificationService接口 | `Application/Battles/` | 接口定义清晰，包含NotifyBattleEventAsync方法 |
| 实现BattleNotificationService | `Application/Battles/` | 服务能够向指定Group推送消息 |
| 创建BattleHub | `Hubs/` | Hub支持订阅/取消订阅操作 |
| 创建通知数据模型 | `Shared/Models/` | BattleEventNotification模型定义完整 |

#### Step 1.2: 战斗事件集成（2天）

| 任务 | 文件 | 验收标准 |
|-----|------|---------|
| BattleContext添加通知服务字段 | `BattleContext.cs` | NotificationService可选注入 |
| PlayerDeathEvent集成 | `PlayerDeathEvent.cs` | 死亡时成功推送通知 |
| PlayerReviveEvent集成 | `PlayerReviveEvent.cs` | 复活时成功推送通知 |
| StepBattleCoordinator注入服务 | `StepBattleCoordinator.cs` | 战斗启动时传入通知服务 |
| 波次完成通知 | `StepBattleCoordinator.cs` | 检测波次完成并推送 |

#### Step 1.3: 前端集成（2天）

| 任务 | 文件 | 验收标准 |
|-----|------|---------|
| 添加SignalR客户端依赖 | `BlazorIdle.csproj` | 包引用成功 |
| 创建BattleHubConnection服务 | `Services/BattleHubConnection.cs` | 连接管理、自动重连、事件订阅功能完整 |
| Characters.razor集成 | `Characters.razor` | 接收通知并立即刷新状态 |
| 进度条打断逻辑 | `Characters.razor` | 死亡事件立即停止进度条 |
| 降级容错处理 | `Characters.razor` | SignalR失败时自动降级到轮询 |

#### Step 1.4: 测试与优化（1天）

| 测试场景 | 验收标准 |
|---------|---------|
| 玩家死亡通知 | 死亡瞬间前端收到通知，进度条立即停止，UI显示死亡状态 |
| 玩家复活通知 | 复活时前端立即收到通知，进度条恢复，UI更新 |
| 波次完成通知 | 最后一个怪物死亡后，前端立即收到波次完成通知 |
| SignalR断线重连 | 断开连接后自动重连，重连后恢复订阅 |
| 降级到轮询 | SignalR连接失败时，轮询机制正常工作 |
| 多战斗并发 | 同时进行多个战斗，通知不混乱 |

**Phase 1 产出**：
- ✅ A类事件的SignalR实时推送
- ✅ 前端立即响应关键事件
- ✅ 向后兼容现有轮询机制
- ✅ 基本的降级容错能力
- ✅ 完整的单元测试和集成测试


### 7.2 Phase 2: 批量事件聚合与优化（可选）

**目标**：优化B类事件，实现批量推送机制，减少消息频率

**工期**：3-4个工作日

**实施步骤**：

#### Step 2.1: 批量通知服务（2天）

| 任务 | 验收标准 |
|-----|---------|
| 创建BatchNotificationService | 支持事件缓冲和定时flush |
| 单个怪物死亡批量推送 | 5秒内多次击杀聚合为一条消息 |
| 目标切换通知 | 攻击目标变更时通知前端 |

#### Step 2.2: 前端批量处理（1天）

| 任务 | 验收标准 |
|-----|---------|
| 处理BatchEvents消息 | 前端能解析并批量更新UI |
| 击杀统计实时更新 | 收到批量击杀通知后更新计数器 |

**Phase 2 产出**：
- ✅ B类事件的批量聚合推送
- ✅ 消息频率降低50%以上
- ✅ 更丰富的战斗反馈

### 7.3 Phase 3: 细化通知内容与深度集成（可选）

**目标**：减少前端抓取次数，直接推送完整状态数据

**工期**：4-5个工作日

**实施步骤**：

#### Step 3.1: 富通知模型（2天）

| 任务 | 验收标准 |
|-----|---------|
| 扩展通知数据模型 | 包含PlayerDeathData、WaveCompleteData等 |
| 死亡通知包含复活倒计时 | 前端无需抓取即可显示倒计时 |
| 波次通知包含下一波信息 | 前端提前知晓Boss波次 |

#### Step 3.2: 智能抓取策略（2天）

| 任务 | 验收标准 |
|-----|---------|
| 根据通知内容判断是否需要抓取 | 关键事件才触发完整状态抓取 |
| 轮询间隔动态调整 | SignalR活跃时延长轮询间隔 |

**Phase 3 产出**：
- ✅ 通知内容丰富化
- ✅ 前端抓取次数减少60%
- ✅ 网络流量优化
- ✅ 用户体验提升到最佳状态

### 7.4 整体时间线

```
Week 1-2: Phase 1 核心功能
  ├─ Day 1-2: 服务端基础设施
  ├─ Day 3-4: 战斗事件集成
  ├─ Day 5-6: 前端集成
  └─ Day 7: 测试与优化

Week 3 (可选): Phase 2 批量优化
  ├─ Day 1-2: 批量通知服务
  └─ Day 3: 前端批量处理

Week 4 (可选): Phase 3 深度集成
  ├─ Day 1-2: 富通知模型
  └─ Day 3-4: 智能抓取策略
```

---

## 8. 验收标准

### 8.1 功能验收

#### 8.1.1 核心功能（Phase 1 必须）

| 编号 | 验收项 | 验收标准 | 优先级 |
|-----|-------|---------|--------|
| F-1 | 玩家死亡实时通知 | 死亡瞬间（<100ms）前端收到通知，进度条立即停止 | ⭐⭐⭐ |
| F-2 | 玩家复活实时通知 | 复活时前端立即收到通知并更新UI | ⭐⭐⭐ |
| F-3 | 波次完成实时通知 | 最后一个怪物死亡后立即收到通知 | ⭐⭐⭐ |
| F-4 | Boss出现实时通知 | Boss波次开始时前端立即收到通知 | ⭐⭐⭐ |
| F-5 | 副本完成实时通知 | 副本完成时立即通知前端 | ⭐⭐⭐ |
| F-6 | SignalR连接建立 | 页面加载后1秒内建立SignalR连接 | ⭐⭐⭐ |
| F-7 | 战斗订阅 | 战斗启动后成功订阅对应的战斗通知 | ⭐⭐⭐ |
| F-8 | 降级兼容性 | SignalR连接失败时，系统自动降级到轮询模式 | ⭐⭐⭐ |
| F-9 | 自动重连 | 连接断开后在30秒内自动重连成功 | ⭐⭐ |
| F-10 | 多战斗支持 | 可同时订阅和接收多个战斗的通知 | ⭐⭐ |

#### 8.1.2 扩展功能（Phase 2-3 可选）

| 编号 | 验收项 | 验收标准 | Phase |
|-----|-------|---------|-------|
| E-1 | 批量击杀通知 | 5秒内多次击杀聚合为一条消息 | Phase 2 |
| E-2 | 目标切换通知 | 攻击目标变更时前端收到通知 | Phase 2 |
| E-3 | 死亡通知包含详情 | 通知中包含死亡原因、复活倒计时 | Phase 3 |
| E-4 | 波次通知包含详情 | 通知中包含下一波信息、是否Boss波次 | Phase 3 |
| E-5 | 智能抓取策略 | 根据通知内容决定是否需要完整状态抓取 | Phase 3 |

### 8.2 性能验收

| 编号 | 指标 | 目标值 | 测试方法 |
|-----|------|--------|---------|
| P-1 | 通知延迟 | 事件发生到前端收到 < 200ms | 使用时间戳对比 |
| P-2 | 连接建立时间 | < 1秒 | 测量InitializeAsync耗时 |
| P-3 | 重连时间 | < 30秒 | 模拟断网后测量 |
| P-4 | 服务端CPU增量 | < 5% | 压力测试前后对比 |
| P-5 | 服务端内存增量 | < 100MB (1000并发连接) | 压力测试监控 |
| P-6 | 消息丢失率 | < 0.1% | 发送1000条消息，统计接收数 |
| P-7 | 前端UI响应时间 | 收到通知到UI更新 < 50ms | 使用Performance API测量 |

### 8.3 兼容性验收

| 编号 | 验收项 | 验收标准 |
|-----|-------|---------|
| C-1 | 现有轮询机制不受影响 | SignalR未启用时，轮询功能完全正常 |
| C-2 | 向后兼容性 | 旧客户端（不支持SignalR）仍可正常使用 |
| C-3 | 数据库兼容性 | 无需修改数据库结构 |
| C-4 | API兼容性 | 现有REST API接口不变 |
| C-5 | WebSocket降级 | 不支持WebSocket环境自动降级到LongPolling |

### 8.4 健壮性验收

| 编号 | 场景 | 预期行为 | 验收标准 |
|-----|------|---------|---------|
| R-1 | SignalR服务崩溃 | 前端自动降级到轮询，不影响用户 | 用户无感知，仅日志记录 |
| R-2 | 网络间歇性中断 | 自动重连，重连后恢复订阅 | 重连后继续接收通知 |
| R-3 | 大量并发连接 | 服务器正常响应，不崩溃 | 1000并发连接测试通过 |
| R-4 | 消息风暴 | 批量聚合机制生效，服务器稳定 | Phase 2实现后测试 |
| R-5 | 无效战斗ID订阅 | 返回错误但不崩溃 | 抛出HubException，客户端处理 |
| R-6 | 未认证用户连接 | 拒绝连接 | 连接被中止 |

### 8.5 用户体验验收

| 编号 | 场景 | 预期体验 | 验收标准 |
|-----|------|---------|---------|
| UX-1 | 玩家死亡 | 进度条立即停止，显示死亡动画，无延迟感 | 用户感知延迟<500ms |
| UX-2 | 波次切换 | 立即显示新波次信息，提示音效（可选） | 切换流畅无卡顿 |
| UX-3 | Boss出现 | 立即显示警告提示，UI突出显示 | 视觉反馈明显 |
| UX-4 | 网络不稳定 | 提示"实时通知连接中断，使用轮询模式" | 用户知晓状态变化 |
| UX-5 | 正常战斗 | 进度条流畅推进，轮询+SignalR配合无缝 | 无异常感知 |

### 8.6 测试用例清单

#### 单元测试（必须）

```
服务端：
├─ IBattleNotificationService接口测试
├─ BattleNotificationService推送逻辑测试
├─ BattleHub订阅/取消订阅测试
├─ PlayerDeathEvent通知调用测试
└─ PlayerReviveEvent通知调用测试

前端：
├─ BattleHubConnection连接管理测试
├─ 事件处理逻辑测试
├─ 降级机制测试
└─ 重连逻辑测试
```

#### 集成测试（必须）

```
端到端测试：
├─ 玩家死亡通知端到端流程
├─ 玩家复活通知端到端流程
├─ 波次完成通知端到端流程
├─ SignalR断线重连流程
└─ 降级到轮询流程
```

#### 压力测试（可选）

```
性能测试：
├─ 1000并发连接测试
├─ 高频事件推送测试（100事件/秒）
├─ 长时间运行稳定性测试（24小时）
└─ 消息丢失率测试
```

---

## 9. 风险评估与缓解

### 9.1 技术风险

| 风险 | 影响 | 概率 | 缓解措施 |
|-----|------|------|---------|
| SignalR性能瓶颈 | 高 | 中 | Phase 2实现批量聚合；监控连接数；必要时扩容 |
| WebSocket不支持 | 中 | 低 | 自动降级到LongPolling或纯轮询 |
| 消息丢失 | 高 | 低 | 轮询作为保底同步机制；关键事件重试 |
| 内存泄漏 | 高 | 中 | 严格的连接生命周期管理；定期审查资源释放 |
| 并发竞态条件 | 中 | 中 | 事件通知使用Task.Run异步化，不阻塞战斗逻辑 |

### 9.2 业务风险

| 风险 | 影响 | 概率 | 缓解措施 |
|-----|------|------|---------|
| 用户体验不如预期 | 中 | 低 | 充分测试；灰度发布；收集用户反馈 |
| 现有功能回归 | 高 | 低 | 完整的回归测试；保持向后兼容 |
| 开发延期 | 中 | 中 | 采用分阶段交付；Phase 1为最小可行方案 |
| 维护成本增加 | 中 | 中 | 充分的代码注释和文档；模块化设计 |

### 9.3 缓解策略总结

1. **渐进式实施**：Phase 1先实现核心功能，验证可行性后再扩展
2. **保底机制**：轮询机制完全保留，SignalR仅作为增强
3. **充分测试**：单元测试、集成测试、压力测试全覆盖
4. **监控预警**：部署后持续监控关键指标，及时发现问题
5. **快速回滚**：保持代码可回滚性，必要时可禁用SignalR功能

---

## 10. 附录

### 10.1 代码仓库结构变更

```
新增文件：
├─ BlazorIdle.Server/
│  ├─ Application/Battles/
│  │  ├─ IBattleNotificationService.cs
│  │  └─ BattleNotificationService.cs
│  └─ Hubs/
│     └─ BattleHub.cs
├─ BlazorIdle.Shared/
│  └─ Models/
│     └─ BattleEventNotification.cs
└─ BlazorIdle/
   └─ Services/
      └─ BattleHubConnection.cs

修改文件：
├─ BlazorIdle.Server/
│  ├─ Program.cs
│  ├─ Domain/Combat/BattleContext.cs
│  ├─ Domain/Combat/PlayerDeathEvent.cs
│  ├─ Domain/Combat/PlayerReviveEvent.cs
│  └─ Application/Battles/Step/StepBattleCoordinator.cs
└─ BlazorIdle/
   ├─ Program.cs
   ├─ Pages/Characters.razor
   └─ BlazorIdle.csproj
```

### 10.2 配置示例

```json
// appsettings.json (服务端)
{
  "SignalR": {
    "Enabled": true,
    "ClientTimeoutSeconds": 60,
    "KeepAliveIntervalSeconds": 15,
    "MaxParallelInvocationsPerClient": 1,
    "EnableDetailedErrors": false
  },
  "BattleNotifications": {
    "EnableBatchAggregation": false,  // Phase 2启用
    "BatchFlushIntervalSeconds": 5
  }
}
```

### 10.3 参考资料

- [ASP.NET Core SignalR文档](https://learn.microsoft.com/zh-cn/aspnet/core/signalr/introduction)
- [SignalR Blazor WebAssembly集成](https://learn.microsoft.com/zh-cn/aspnet/core/blazor/tutorials/signalr-blazor)
- [SignalR性能最佳实践](https://learn.microsoft.com/zh-cn/aspnet/core/signalr/scale)
- [WebSocket协议规范](https://datatracker.ietf.org/doc/html/rfc6455)

---

## 文档变更历史

| 版本 | 日期 | 变更内容 | 作者 |
|-----|------|---------|------|
| 1.0 | 2025-10-13 | 初始版本，完整设计方案 | 系统分析 |

---

## 总结

本文档提供了BlazorIdle项目集成SignalR实时通知的完整方案，包括：

✅ **需求分析**：明确了SignalR的必要性和适用场景  
✅ **事件分类**：科学地划分了A/B/C/D四类事件，明确哪些需要SignalR  
✅ **架构设计**：给出了详细的组件设计和数据流图  
✅ **实施细节**：Phase 1-3分阶段实施，代码清单和修改点明确  
✅ **验收标准**：功能、性能、兼容性、健壮性、用户体验全方位验收  
✅ **风险管理**：识别主要风险并提供缓解措施  

**关键设计原则**：
1. 🎯 **渐进式增强**：SignalR作为轮询的增强而非替代
2. 🛡️ **向后兼容**：不破坏现有功能，保持系统稳定
3. 🔄 **降级容错**：SignalR失败时自动降级到轮询
4. ⚡ **最小侵入**：对现有代码的修改最小化
5. 📊 **可监控**：完善的监控指标和日志记录

**预期效果**：
- 用户体验：死亡/复活等关键事件的感知延迟从1-2秒降低到<200ms
- 系统性能：关键事件及时通知后，可适当延长稳定态轮询间隔，降低服务器负载
- 可维护性：模块化设计，代码清晰，易于扩展和维护

该方案可作为开发团队的实施蓝图，按照Phase 1 → Phase 2 → Phase 3逐步推进，每个阶段都有明确的交付物和验收标准。

