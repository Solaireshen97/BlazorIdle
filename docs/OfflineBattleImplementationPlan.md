# 离线战斗系统实施方案

**文档版本**: 1.0  
**创建日期**: 2025年1月  
**目标**: 实现完整的离线战斗收益计算与自动衔接活动计划功能

---

## 📋 目录

1. [需求概述](#需求概述)
2. [当前实现状态分析](#当前实现状态分析)
3. [缺失组件清单](#缺失组件清单)
4. [详细实施方案](#详细实施方案)
5. [数据库变更](#数据库变更)
6. [API设计](#api设计)
7. [前端集成方案](#前端集成方案)
8. [测试验证计划](#测试验证计划)
9. [风险与注意事项](#风险与注意事项)

---

## 需求概述

### 核心功能

用户在线时：
1. 创建活动计划（如1小时战斗任务）
2. 启动活动计划，开始战斗
3. 可随时下线或关闭网页

用户离线期间：
1. 系统记录离线时间点
2. 保存当前活动计划状态

用户再次上线时：
1. 检测离线时长（上限12小时）
2. **自动触发离线快进模拟**，计算离线期间收益
3. 展示离线结算界面（时长、金币、经验、物品）
4. 将收益发放到角色
5. 如果活动计划未完成，自动继续执行剩余任务
6. 如果活动计划已完成，自动启动下一个待执行的计划

### 设计原则

根据`整合设计总结.txt`第9章节：
- **一致性**：离线使用与在线相同的事件调度算法
- **上限控制**：离线收益最多计算12小时（可配置）
- **快进模拟**：使用`OfflineFastForwardEngine`快速推进时间
- **聚合优化**：生成`CombatSegment`而非逐事件存储
- **自动衔接**：当前计划完成→查找下一个Pending→启动

---

## 当前实现状态分析

### ✅ 已实现的组件

#### 1. ActivityPlan 模型 (完整)
- **位置**: `BlazorIdle.Server/Domain/Activities/ActivityPlan.cs`
- **功能**:
  - 活动计划实体（Combat、Dungeon）
  - 限制类型（Duration、Infinite）
  - 状态机（Pending → Running → Completed/Cancelled）
  - 槽位索引（0-4，预留多槽位支持）
  - 执行时长追踪（`ExecutedSeconds`）
- **状态**: ✅ 完整实现

#### 2. ActivityPlanService (完整)
- **位置**: `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs`
- **功能**:
  - 创建活动计划
  - 启动/停止/取消计划
  - 与`StepBattleCoordinator`集成
  - 自动启动逻辑（首个计划自动启动）
  - 进度更新与限制检查
- **状态**: ✅ 完整实现

#### 3. OfflineSettlementService (基础版本)
- **位置**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`
- **功能**:
  - 基础离线模拟（SimulateAsync）
  - 使用`BattleSimulator`进行快进
  - 经济计算（金币、经验、掉落）
  - 支持多种模式（continuous、dungeon）
- **限制**: 
  - ⚠️ **未集成到用户登录流程**
  - ⚠️ **未记录角色离线时间**
  - ⚠️ **未自动触发离线结算**
  - ⚠️ **未恢复活动计划状态**
- **状态**: 🟡 部分实现

#### 4. OfflineController (基础API)
- **位置**: `BlazorIdle.Server/Api/OfflineController.cs`
- **功能**:
  - 提供手动离线结算接口
  - `POST /api/offline/settle` 端点
- **限制**: 
  - ⚠️ 仅支持手动调用
  - ⚠️ 未自动在登录时触发
- **状态**: 🟡 部分实现

#### 5. Character 实体（已扩展）
- **位置**: `BlazorIdle.Server/Domain/Characters/Character.cs`
- **功能**:
  - 包含离线追踪字段：
    - `LastSeenAtUtc`: 最近在线时间
    - `LastOfflineSettledAtUtc`: 最近离线结算时间
- **状态**: ✅ 字段已添加

### ❌ 缺失的核心组件

#### 1. OfflineFastForwardEngine (完全缺失)
根据设计文档，需要一个专门的引擎用于离线快进：
- 复用`EventScheduler`进行事件调度
- 支持时间跳跃（无需逐帧计算）
- 生成聚合的`CombatSegment`
- 与`ActivityPlan`状态同步

#### 2. 自动离线检测与结算机制 (完全缺失)
- 登录时自动检测离线时长
- 触发离线结算流程
- 恢复活动计划状态

#### 3. 活动计划自动衔接 (部分实现)
- 当前有`TryStartNextPendingPlanAsync`方法
- ⚠️ 但未与离线结算集成
- ⚠️ 未处理离线期间计划完成的情况

#### 4. 离线结算结果持久化 (完全缺失)
- 离线收益记录（用于审计和回顾）
- 历史离线结算记录

#### 5. 前端离线结算界面 (完全缺失)
- 离线时长展示
- 收益详情（金币、经验、物品）
- 段摘要查看

---

## 缺失组件清单

### 后端组件

| 组件 | 优先级 | 状态 | 描述 |
|------|--------|------|------|
| `OfflineFastForwardEngine` | 🔴 高 | ❌ 缺失 | 专用离线快进引擎，复用EventScheduler |
| `OfflineSettlementRecord` 实体 | 🟡 中 | ❌ 缺失 | 离线结算记录表（审计用） |
| `IOfflineSettlementRepository` | 🟡 中 | ❌ 缺失 | 离线结算记录仓储接口 |
| `LoginOfflineCheckService` | 🔴 高 | ❌ 缺失 | 登录时自动检测并触发离线结算 |
| 活动计划快照恢复逻辑 | 🔴 高 | ❌ 缺失 | 恢复离线前的活动计划状态 |
| 活动计划自动衔接完善 | 🔴 高 | 🟡 部分 | 处理离线期间计划完成的情况 |
| 离线时长上限配置 | 🟢 低 | ❌ 缺失 | 12小时上限（可配置） |

### 前端组件

| 组件 | 优先级 | 状态 | 描述 |
|------|--------|------|------|
| `OfflineSettlementDialog` 组件 | 🔴 高 | ❌ 缺失 | 离线结算弹窗 |
| 离线收益展示界面 | 🔴 高 | ❌ 缺失 | 金币、经验、物品详情 |
| 离线段摘要查看 | 🟡 中 | ❌ 缺失 | 查看离线战斗详细数据 |
| ApiClient 离线API集成 | 🔴 高 | ❌ 缺失 | 调用离线结算API |

### API端点

| 端点 | 优先级 | 状态 | 描述 |
|------|--------|------|------|
| `GET /api/offline/check` | 🔴 高 | ❌ 缺失 | 检查是否有待结算的离线时间 |
| `POST /api/offline/apply-settlement` | 🔴 高 | ❌ 缺失 | 应用离线结算并发放收益 |
| `GET /api/offline/history` | 🟡 中 | ❌ 缺失 | 获取历史离线结算记录 |

---

## 详细实施方案

### Phase 1: 核心离线引擎（最关键）

#### Step 1.1: 创建 OfflineFastForwardEngine

**位置**: `BlazorIdle.Server/Application/Battles/Offline/OfflineFastForwardEngine.cs`

**功能设计**:
```csharp
public class OfflineFastForwardEngine
{
    private readonly BattleSimulator _simulator;
    
    public OfflineFastForwardEngine(BattleSimulator simulator)
    {
        _simulator = simulator;
    }
    
    /// <summary>
    /// 快进模拟离线期间的活动计划执行
    /// </summary>
    public OfflineFastForwardResult FastForward(
        Character character,
        ActivityPlan plan,
        double offlineSeconds,
        double maxCapSeconds = 43200) // 12小时默认上限
    {
        // 1. 计算实际模拟时长
        var cappedSeconds = Math.Min(offlineSeconds, maxCapSeconds);
        
        // 2. 计算计划剩余时长
        var remainingSeconds = CalculateRemainingSeconds(plan, cappedSeconds);
        
        // 3. 使用 BattleSimulator 快进
        var result = SimulatePlan(character, plan, remainingSeconds);
        
        // 4. 更新计划状态
        var updatedPlan = UpdatePlanState(plan, remainingSeconds);
        
        // 5. 返回结果
        return new OfflineFastForwardResult
        {
            CharacterId = character.Id,
            PlanId = plan.Id,
            SimulatedSeconds = remainingSeconds,
            PlanCompleted = updatedPlan.IsLimitReached(),
            TotalDamage = result.TotalDamage,
            TotalKills = result.TotalKills,
            Gold = result.Gold,
            Exp = result.Exp,
            Loot = result.Loot,
            Segments = result.Segments,
            UpdatedExecutedSeconds = updatedPlan.ExecutedSeconds
        };
    }
    
    private double CalculateRemainingSeconds(ActivityPlan plan, double availableSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
        {
            return availableSeconds;
        }
        
        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        {
            var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
            return Math.Min(remaining, availableSeconds);
        }
        
        return availableSeconds;
    }
    
    private SimulationResult SimulatePlan(
        Character character, 
        ActivityPlan plan, 
        double seconds)
    {
        // 根据计划类型构建配置
        var config = BuildBattleConfig(character, plan, seconds);
        
        // 使用 BattleSimulator 执行快进
        var rb = _simulator.CreateRunningBattle(config, seconds);
        rb.FastForwardTo(seconds);
        
        // 聚合结果
        return AggregateResults(rb);
    }
}

public class OfflineFastForwardResult
{
    public Guid CharacterId { get; set; }
    public Guid PlanId { get; set; }
    public double SimulatedSeconds { get; set; }
    public bool PlanCompleted { get; set; }
    public long TotalDamage { get; set; }
    public int TotalKills { get; set; }
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> Loot { get; set; } = new();
    public List<CombatSegment> Segments { get; set; } = new();
    public double UpdatedExecutedSeconds { get; set; }
}
```

**实现要点**:
1. 复用现有的`BattleSimulator`和`RunningBattle`
2. 支持计划限制计算（Duration vs Infinite）
3. 返回详细的模拟结果和更新后的计划状态
4. 生成聚合的`CombatSegment`用于回顾

---

#### Step 1.2: 完善 OfflineSettlementService

**位置**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`（扩展）

**新增方法**:
```csharp
public class OfflineSettlementService
{
    private readonly ICharacterRepository _characters;
    private readonly IActivityPlanRepository _plans;
    private readonly BattleSimulator _simulator;
    private readonly OfflineFastForwardEngine _engine;
    private readonly IRewardGrantService _rewards;
    
    /// <summary>
    /// 用户登录时自动检测并结算离线收益
    /// </summary>
    public async Task<OfflineCheckResult> CheckAndSettleAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");
        
        // 1. 计算离线时长
        var offlineSeconds = CalculateOfflineDuration(character);
        if (offlineSeconds <= 0)
        {
            return new OfflineCheckResult
            {
                HasOfflineTime = false,
                OfflineSeconds = 0
            };
        }
        
        // 2. 查找离线时正在运行的计划
        var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
        if (runningPlan is null)
        {
            // 没有活动计划，仅更新LastSeenAt
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _characters.UpdateAsync(character, ct);
            
            return new OfflineCheckResult
            {
                HasOfflineTime = true,
                OfflineSeconds = offlineSeconds,
                HasRunningPlan = false
            };
        }
        
        // 3. 使用 OfflineFastForwardEngine 快进模拟
        var result = _engine.FastForward(character, runningPlan, offlineSeconds);
        
        // 4. 更新计划执行时长和状态
        runningPlan.ExecutedSeconds = result.UpdatedExecutedSeconds;
        if (result.PlanCompleted)
        {
            runningPlan.State = ActivityState.Completed;
            runningPlan.CompletedAt = DateTime.UtcNow;
        }
        await _plans.UpdateAsync(runningPlan, ct);
        
        // 5. 发放奖励（暂不实际修改角色Gold/Exp，等前端确认后调用apply接口）
        
        // 6. 更新角色时间戳
        character.LastSeenAtUtc = DateTime.UtcNow;
        character.LastOfflineSettledAtUtc = DateTime.UtcNow;
        await _characters.UpdateAsync(character, ct);
        
        // 7. 如果计划完成，尝试启动下一个
        if (result.PlanCompleted)
        {
            var nextPlan = await TryStartNextPendingPlanAsync(characterId, ct);
            return new OfflineCheckResult
            {
                HasOfflineTime = true,
                OfflineSeconds = offlineSeconds,
                HasRunningPlan = true,
                Settlement = result,
                PlanCompleted = true,
                NextPlanStarted = nextPlan is not null,
                NextPlanId = nextPlan?.Id
            };
        }
        
        return new OfflineCheckResult
        {
            HasOfflineTime = true,
            OfflineSeconds = offlineSeconds,
            HasRunningPlan = true,
            Settlement = result,
            PlanCompleted = false
        };
    }
    
    /// <summary>
    /// 应用离线结算，实际发放收益
    /// </summary>
    public async Task ApplySettlementAsync(
        Guid characterId,
        OfflineFastForwardResult settlement,
        CancellationToken ct = default)
    {
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");
        
        // 发放金币和经验
        character.Gold += settlement.Gold;
        character.Experience += settlement.Exp;
        await _characters.UpdateAsync(character, ct);
        
        // 发放物品（如果有背包系统）
        if (settlement.Loot.Any())
        {
            await _rewards.GrantItemsAsync(characterId, settlement.Loot, ct);
        }
    }
    
    private double CalculateOfflineDuration(Character character)
    {
        if (!character.LastSeenAtUtc.HasValue)
            return 0;
        
        var now = DateTime.UtcNow;
        var lastSeen = character.LastSeenAtUtc.Value;
        return (now - lastSeen).TotalSeconds;
    }
    
    private async Task<ActivityPlan?> TryStartNextPendingPlanAsync(
        Guid characterId,
        CancellationToken ct)
    {
        // 查找下一个Pending状态的计划
        var pendingPlans = await _plans.GetPendingPlansAsync(characterId, ct);
        var nextPlan = pendingPlans
            .OrderBy(p => p.SlotIndex)
            .ThenBy(p => p.CreatedAt)
            .FirstOrDefault();
        
        if (nextPlan is null)
            return null;
        
        // 启动计划（需要调用ActivityPlanService）
        // 注意：这里需要注入ActivityPlanService或重构代码避免循环依赖
        
        return nextPlan;
    }
}

public class OfflineCheckResult
{
    public bool HasOfflineTime { get; set; }
    public double OfflineSeconds { get; set; }
    public bool HasRunningPlan { get; set; }
    public OfflineFastForwardResult? Settlement { get; set; }
    public bool PlanCompleted { get; set; }
    public bool NextPlanStarted { get; set; }
    public Guid? NextPlanId { get; set; }
}
```

**实现要点**:
1. 自动检测离线时长（基于`LastSeenAtUtc`）
2. 查找离线时的运行计划
3. 调用`OfflineFastForwardEngine`进行快进
4. 更新计划状态（ExecutedSeconds、Completed）
5. 支持两阶段提交（先返回结算结果，前端确认后再发放）
6. 自动衔接下一个计划

---

### Phase 2: 心跳与离线追踪

#### Step 2.1: 添加心跳更新机制

**位置**: `BlazorIdle.Server/Api/CharactersController.cs`（新增端点）

**新增端点**:
```csharp
[HttpPost("{characterId}/heartbeat")]
public async Task<ActionResult> UpdateHeartbeat(
    Guid characterId,
    CancellationToken ct = default)
{
    var character = await _characters.GetAsync(characterId, ct);
    if (character is null)
        return NotFound();
    
    character.LastSeenAtUtc = DateTime.UtcNow;
    await _characters.UpdateAsync(character, ct);
    
    return Ok();
}
```

**前端集成**:
- 在`Characters.razor`页面加载时调用
- 定期心跳（可选，如每5分钟）
- 页面卸载时调用

---

#### Step 2.2: 登录时离线检测

**位置**: `BlazorIdle.Server/Api/OfflineController.cs`（扩展）

**新增端点**:
```csharp
[HttpGet("check")]
public async Task<ActionResult<OfflineCheckResult>> CheckOffline(
    [FromQuery] Guid characterId,
    CancellationToken ct = default)
{
    try
    {
        var result = await _offline.CheckAndSettleAsync(characterId, ct);
        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new { error = ex.Message });
    }
}

[HttpPost("apply")]
public async Task<ActionResult> ApplySettlement(
    [FromBody] ApplySettlementRequest request,
    CancellationToken ct = default)
{
    try
    {
        await _offline.ApplySettlementAsync(
            request.CharacterId,
            request.Settlement,
            ct);
        return Ok();
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new { error = ex.Message });
    }
}

public record ApplySettlementRequest(
    Guid CharacterId,
    OfflineFastForwardResult Settlement
);
```

---

### Phase 3: 活动计划自动衔接完善

#### Step 3.1: 优化 ActivityPlanService

**位置**: `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs`

**新增/修改方法**:
```csharp
/// <summary>
/// 尝试启动下一个待执行的计划（公开方法，供离线结算调用）
/// </summary>
public async Task<ActivityPlan?> TryStartNextPendingPlanAsync(
    Guid characterId,
    CancellationToken ct = default)
{
    // 检查是否有正在运行的计划
    var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
    if (runningPlan is not null)
        return null; // 已有运行中的计划
    
    // 查找下一个Pending计划
    var pendingPlans = await _plans.GetByCharacterIdAsync(characterId, ct);
    var nextPlan = pendingPlans
        .Where(p => p.State == ActivityState.Pending)
        .OrderBy(p => p.SlotIndex)
        .ThenBy(p => p.CreatedAt)
        .FirstOrDefault();
    
    if (nextPlan is null)
        return null;
    
    // 启动计划
    try
    {
        await StartPlanAsync(nextPlan.Id, ct);
        return nextPlan;
    }
    catch (Exception)
    {
        return null;
    }
}
```

---

### Phase 4: 可选增强功能

#### Step 4.1: 离线结算记录（审计用）

**位置**: `BlazorIdle.Server/Domain/Offline/OfflineSettlementRecord.cs`（新增）

**实体设计**:
```csharp
public class OfflineSettlementRecord
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? ActivityPlanId { get; set; }
    public DateTime OfflineStartAt { get; set; }
    public DateTime OfflineEndAt { get; set; }
    public double OfflineSeconds { get; set; }
    public double SimulatedSeconds { get; set; }
    public long GoldEarned { get; set; }
    public long ExpEarned { get; set; }
    public string LootJson { get; set; } = "{}";
    public DateTime SettledAt { get; set; }
}
```

**用途**:
- 记录每次离线结算
- 用于玩家查询历史收益
- 防作弊审计

---

#### Step 4.2: 离线时长上限配置

**位置**: `appsettings.json`

**配置项**:
```json
{
  "Offline": {
    "MaxOfflineSeconds": 43200,  // 12小时
    "EnableAutoSettlement": true,
    "RequireManualConfirm": true  // 是否需要前端确认才发放
  }
}
```

---

## 数据库变更

### Migration: 添加离线结算记录表（可选）

**迁移名称**: `AddOfflineSettlementRecords`

**SQL**:
```sql
CREATE TABLE OfflineSettlementRecords (
    Id TEXT PRIMARY KEY,
    CharacterId TEXT NOT NULL,
    ActivityPlanId TEXT,
    OfflineStartAt TEXT NOT NULL,
    OfflineEndAt TEXT NOT NULL,
    OfflineSeconds REAL NOT NULL,
    SimulatedSeconds REAL NOT NULL,
    GoldEarned INTEGER NOT NULL,
    ExpEarned INTEGER NOT NULL,
    LootJson TEXT NOT NULL,
    SettledAt TEXT NOT NULL,
    FOREIGN KEY (CharacterId) REFERENCES Characters(Id)
);

CREATE INDEX IX_OfflineSettlementRecords_CharacterId 
    ON OfflineSettlementRecords(CharacterId);
```

---

## API设计

### 新增端点汇总

| 端点 | 方法 | 描述 |
|------|------|------|
| `/api/offline/check` | GET | 检查角色是否有离线时间，返回结算预览 |
| `/api/offline/apply` | POST | 应用离线结算，实际发放收益 |
| `/api/characters/{id}/heartbeat` | POST | 更新角色心跳，记录在线时间 |
| `/api/offline/history` | GET | 获取历史离线结算记录（可选） |

### 请求/响应示例

#### GET /api/offline/check?characterId={id}

**响应**:
```json
{
  "hasOfflineTime": true,
  "offlineSeconds": 7200,
  "hasRunningPlan": true,
  "settlement": {
    "characterId": "xxx",
    "planId": "yyy",
    "simulatedSeconds": 3600,
    "planCompleted": true,
    "totalDamage": 1500000,
    "totalKills": 150,
    "gold": 5000,
    "exp": 8000,
    "loot": {
      "iron_ore": 25.5,
      "health_potion": 3.2
    },
    "updatedExecutedSeconds": 3600
  },
  "planCompleted": true,
  "nextPlanStarted": true,
  "nextPlanId": "zzz"
}
```

#### POST /api/offline/apply

**请求体**:
```json
{
  "characterId": "xxx",
  "settlement": { /* OfflineFastForwardResult */ }
}
```

**响应**:
```json
{
  "success": true
}
```

---

## 前端集成方案

### Step 1: ApiClient 扩展

**位置**: `BlazorIdle/Services/ApiClient.cs`

**新增方法**:
```csharp
// 检查离线收益
public Task<OfflineCheckResult?> CheckOfflineAsync(
    Guid characterId, 
    CancellationToken ct = default)
{
    SetAuthHeader();
    return _http.GetFromJsonAsync<OfflineCheckResult>(
        $"/api/offline/check?characterId={characterId}", 
        ct);
}

// 应用离线结算
public async Task ApplyOfflineSettlementAsync(
    Guid characterId,
    OfflineFastForwardResult settlement,
    CancellationToken ct = default)
{
    SetAuthHeader();
    var request = new ApplySettlementRequest(characterId, settlement);
    var resp = await _http.PostAsJsonAsync("/api/offline/apply", request, ct);
    resp.EnsureSuccessStatusCode();
}

// 更新心跳
public async Task UpdateHeartbeatAsync(
    Guid characterId,
    CancellationToken ct = default)
{
    SetAuthHeader();
    var resp = await _http.PostAsync(
        $"/api/characters/{characterId}/heartbeat", 
        null, 
        ct);
    resp.EnsureSuccessStatusCode();
}
```

---

### Step 2: 创建离线结算弹窗组件

**位置**: `BlazorIdle/Components/OfflineSettlementDialog.razor`（新建）

**组件功能**:
1. 显示离线时长（如"离线2小时15分钟"）
2. 展示收益统计：
   - 金币: +5000
   - 经验: +8000
   - 击败敌人: 150
3. 物品掉落列表
4. "确认领取"按钮
5. 可选："查看详情"展开段摘要

**伪代码**:
```razor
@if (showDialog && result != null)
{
    <div class="offline-dialog">
        <h2>欢迎回来！</h2>
        <p>离线期间获得了以下收益：</p>
        
        <div class="offline-duration">
            离线时长: @FormatDuration(result.OfflineSeconds)
        </div>
        
        <div class="offline-rewards">
            <div>金币: +@result.Settlement.Gold</div>
            <div>经验: +@result.Settlement.Exp</div>
            <div>击杀: @result.Settlement.TotalKills</div>
        </div>
        
        @if (result.Settlement.Loot.Any())
        {
            <div class="offline-loot">
                <h3>物品掉落</h3>
                @foreach (var item in result.Settlement.Loot)
                {
                    <div>@item.Key: @item.Value.ToString("F1")</div>
                }
            </div>
        }
        
        <button @onclick="ClaimRewards">确认领取</button>
        
        @if (result.PlanCompleted && result.NextPlanStarted)
        {
            <p class="auto-continue">
                活动计划已完成，已自动开始下一个计划
            </p>
        }
    </div>
}

@code {
    [Parameter] public OfflineCheckResult? Result { get; set; }
    [Parameter] public EventCallback OnClaimed { get; set; }
    
    private bool showDialog = false;
    
    protected override void OnParametersSet()
    {
        showDialog = Result?.HasOfflineTime == true;
    }
    
    private async Task ClaimRewards()
    {
        // 调用API应用结算
        await OnClaimed.InvokeAsync();
        showDialog = false;
    }
    
    private string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours}小时{ts.Minutes}分钟";
    }
}
```

---

### Step 3: 在 Characters.razor 中集成

**位置**: `BlazorIdle/Pages/Characters.razor`

**修改要点**:
```csharp
protected override async Task OnInitializedAsync()
{
    await base.OnInitializedAsync();
    
    // 加载角色列表...
    
    // 如果有选中的角色，检查离线收益
    if (selectedCharacter != null)
    {
        await CheckOfflineRewardsAsync(selectedCharacter.Id);
    }
}

private async Task CheckOfflineRewardsAsync(Guid characterId)
{
    try
    {
        // 1. 更新心跳
        await apiClient.UpdateHeartbeatAsync(characterId);
        
        // 2. 检查离线收益
        var offlineResult = await apiClient.CheckOfflineAsync(characterId);
        
        if (offlineResult?.HasOfflineTime == true && 
            offlineResult.Settlement != null)
        {
            // 3. 显示离线结算弹窗
            showOfflineDialog = true;
            offlineCheckResult = offlineResult;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"离线检查失败: {ex.Message}");
    }
}

private async Task ApplyOfflineSettlement()
{
    if (offlineCheckResult?.Settlement == null)
        return;
    
    try
    {
        // 应用离线结算
        await apiClient.ApplyOfflineSettlementAsync(
            selectedCharacter.Id,
            offlineCheckResult.Settlement
        );
        
        // 刷新角色数据
        await RefreshCharacterAsync();
        
        showOfflineDialog = false;
    }
    catch (Exception ex)
    {
        errorMessage = $"领取失败: {ex.Message}";
    }
}
```

---

## 测试验证计划

### 单元测试

#### 1. OfflineFastForwardEngine 测试
- ✅ 测试离线时长上限（12小时）
- ✅ 测试计划剩余时长计算（Duration vs Infinite）
- ✅ 测试快进模拟结果聚合
- ✅ 测试计划完成状态判断

#### 2. OfflineSettlementService 测试
- ✅ 测试无离线时间场景
- ✅ 测试有离线时间但无运行计划
- ✅ 测试有离线时间且有运行计划
- ✅ 测试计划完成后自动衔接
- ✅ 测试收益应用逻辑

#### 3. ActivityPlanService 测试
- ✅ 测试自动衔接逻辑
- ✅ 测试多个Pending计划的优先级

### 集成测试

#### 场景1: 基础离线流程
1. 创建角色
2. 创建1小时战斗计划
3. 启动计划
4. 模拟离线2小时（修改LastSeenAtUtc）
5. 调用离线检查API
6. 验证返回结果（应该只计算1小时，因为计划已完成）
7. 应用离线结算
8. 验证角色金币、经验增加

#### 场景2: 计划未完成继续执行
1. 创建角色
2. 创建3小时战斗计划
3. 启动计划（已执行0.5小时）
4. 模拟离线1小时
5. 调用离线检查API
6. 验证：ExecutedSeconds = 1.5小时，计划仍Running

#### 场景3: 自动衔接下一个计划
1. 创建角色
2. 创建两个计划：
   - Plan1: 1小时战斗（Slot 0）
   - Plan2: 2小时战斗（Slot 1，Pending）
3. 启动Plan1
4. 模拟离线2小时
5. 调用离线检查API
6. 验证：
   - Plan1已完成
   - Plan2已自动启动
   - 返回结果包含NextPlanStarted=true

### 手动测试

#### 前端集成测试
1. 登录游戏
2. 创建角色和战斗计划
3. 关闭浏览器
4. 等待5分钟
5. 重新登录
6. 验证：
   - 显示离线结算弹窗
   - 收益数据正确
   - 点击"确认领取"后角色数据更新

---

## 风险与注意事项

### 高风险项

#### 1. 时间同步问题
**风险**: 服务器时间与客户端时间不一致可能导致计算错误
**缓解**:
- 所有时间戳使用UTC
- 服务端权威（不信任客户端时间）

#### 2. 并发问题
**风险**: 用户快速多次调用离线检查API可能导致重复发放
**缓解**:
- 使用事务保证原子性
- 添加分布式锁（如果多实例部署）
- 检查`LastOfflineSettledAtUtc`防止短时间内重复结算

#### 3. 性能问题
**风险**: 12小时的快进模拟可能耗时较长
**缓解**:
- 使用`BattleSimulator`的快进功能（已优化）
- 设置合理的超时时间
- 考虑异步处理（复杂场景）

### 中风险项

#### 4. 数据一致性
**风险**: 离线结算过程中角色数据被其他操作修改
**缓解**:
- 使用数据库事务
- 乐观锁或版本号机制

#### 5. 游戏平衡
**风险**: 离线收益过高可能导致玩家不在线也能快速升级
**缓解**:
- 12小时上限限制
- 可配置的收益衰减系数（未来扩展）

### 低风险项

#### 6. UI/UX 体验
**风险**: 离线弹窗可能打断用户操作
**缓解**:
- 允许用户关闭弹窗稍后查看
- 提供"不再提示"选项

---

## 实施优先级建议

### Phase 1 (核心功能，必须实现)
1. ✅ `OfflineFastForwardEngine` 实现
2. ✅ `OfflineSettlementService.CheckAndSettleAsync` 实现
3. ✅ `GET /api/offline/check` 端点
4. ✅ `POST /api/offline/apply` 端点
5. ✅ 前端离线结算弹窗组件
6. ✅ Characters.razor 集成离线检查逻辑

### Phase 2 (增强功能，推荐实现)
7. ✅ 心跳机制（`POST /api/characters/{id}/heartbeat`）
8. ✅ 活动计划自动衔接完善
9. ✅ 离线时长上限配置

### Phase 3 (可选功能，后续扩展)
10. ⭕ 离线结算记录表（审计用）
11. ⭕ 历史结算记录查询API
12. ⭕ 离线段摘要详细查看
13. ⭕ 收益衰减机制（防止过度挂机）

---

## 总结

本实施方案基于现有代码（`ActivityPlan`、`BattleSimulator`、`OfflineSettlementService`）提供了完整的离线战斗系统设计。

### 核心实现路径：
1. **新增** `OfflineFastForwardEngine` 封装离线快进逻辑
2. **扩展** `OfflineSettlementService` 支持登录时自动结算
3. **新增** 离线检查与应用API端点
4. **完善** 活动计划自动衔接机制
5. **新建** 前端离线结算弹窗组件

### 关键设计原则：
- ✅ 复用现有的事件调度与战斗模拟系统
- ✅ 保持在线/离线逻辑一致性
- ✅ 支持活动计划的状态恢复与自动衔接
- ✅ 两阶段提交模式（预览→确认→发放）
- ✅ 离线时长上限保护游戏平衡

### 预估工作量：
- **后端核心功能**: 2-3天
- **前端集成**: 1-2天
- **测试与调优**: 1-2天
- **总计**: 4-7天

---

**文档结束**
