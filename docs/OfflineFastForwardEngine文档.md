# OfflineFastForwardEngine 文档

## 📌 概述

`OfflineFastForwardEngine` 是离线战斗系统的核心组件，负责模拟角色离线期间的活动计划执行。该引擎复用 `BattleSimulator` 进行战斗模拟，并根据活动计划的限制类型（Duration/Infinite）计算实际模拟时长。

## 🏗️ 架构设计

### 核心职责

1. **离线时长限制**：确保离线模拟不超过配置的上限（默认12小时）
2. **计划剩余时长计算**：根据计划类型和已执行时长，计算实际需要模拟的时长
3. **战斗模拟**：调用 `BattleSimulator` 执行快进模拟
4. **收益计算**：统计金币、经验和物品掉落
5. **状态判断**：判断计划是否已完成

### 依赖关系

```
OfflineFastForwardEngine
├── BattleSimulator         (战斗模拟器)
├── ActivityPlan            (活动计划实体)
├── Character               (角色实体)
└── EconomyCalculator       (经济收益计算器)
```

## 📊 数据模型

### OfflineFastForwardResult

离线快进结果，包含模拟时长、收益统计和计划状态信息。

```csharp
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }           // 角色ID
    public Guid PlanId { get; init; }                // 计划ID
    public double SimulatedSeconds { get; init; }    // 实际模拟时长（秒）
    public bool PlanCompleted { get; init; }         // 计划是否已完成
    public long TotalDamage { get; init; }           // 总伤害
    public int TotalKills { get; init; }             // 总击杀数
    public long Gold { get; init; }                  // 金币收益
    public long Exp { get; init; }                   // 经验收益
    public Dictionary<string, double> Loot { get; init; } // 物品掉落（预期值）
    public double UpdatedExecutedSeconds { get; init; }   // 更新后的已执行时长（秒）
}
```

### OfflineCheckResult

离线检查结果，用于前端展示离线收益预览。

```csharp
public sealed class OfflineCheckResult
{
    public bool HasOfflineTime { get; init; }        // 是否有离线时间
    public double OfflineSeconds { get; init; }      // 离线总时长（秒）
    public bool HasRunningPlan { get; init; }        // 离线时是否有运行中的计划
    public OfflineFastForwardResult? Settlement { get; init; } // 结算结果
    public bool PlanCompleted { get; init; }         // 计划是否完成
    public bool NextPlanStarted { get; init; }       // 是否启动了下一个计划
    public Guid? NextPlanId { get; init; }           // 下一个计划ID
}
```

## 🔧 核心方法

### FastForward

快进模拟离线期间的活动计划执行。

```csharp
public OfflineFastForwardResult FastForward(
    Character character,          // 角色实体
    ActivityPlan plan,            // 活动计划
    double offlineSeconds,        // 离线时长（秒）
    double maxCapSeconds = 43200.0) // 最大离线时长上限（默认12小时）
```

**执行流程**：

```
1. 验证参数有效性
   ├── 检查 character 不为 null
   ├── 检查 plan 不为 null
   └── 检查 offlineSeconds >= 0

2. 限制离线时长
   └── cappedOfflineSeconds = Min(offlineSeconds, maxCapSeconds)

3. 计算计划剩余时长
   ├── 如果是 Infinite 类型
   │   └── 返回全部离线时长
   └── 如果是 Duration 类型
       ├── remaining = LimitValue - ExecutedSeconds
       └── 返回 Min(remaining, cappedOfflineSeconds)

4. 快进模拟战斗
   ├── 解析活动配置 (PayloadJson)
   ├── 构建角色属性 (Stats)
   ├── 创建战斗配置 (BattleConfig)
   ├── 执行战斗模拟 (BattleSimulator)
   └── 统计收益 (EconomyCalculator)

5. 更新计划状态
   ├── updatedExecutedSeconds = ExecutedSeconds + SimulatedSeconds
   └── planCompleted = IsLimitReached()

6. 返回结果
   └── OfflineFastForwardResult
```

## 💡 使用示例

### 基本用法

```csharp
// 创建引擎
var simulator = new BattleSimulator();
var engine = new OfflineFastForwardEngine(simulator);

// 获取角色和计划
var character = await _characterRepo.GetAsync(characterId);
var plan = await _planRepo.GetRunningPlanAsync(characterId);

// 计算离线时长
var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc).TotalSeconds;

// 执行快进模拟
var result = engine.FastForward(character, plan, offlineSeconds);

// 应用结果
plan.ExecutedSeconds = result.UpdatedExecutedSeconds;
if (result.PlanCompleted)
{
    plan.State = ActivityState.Completed;
    plan.CompletedAt = DateTime.UtcNow;
}

character.Gold += result.Gold;
character.Experience += result.Exp;
```

### 场景1：Duration 计划未完成

```csharp
// 初始状态
// - 计划限制：2小时（7200秒）
// - 已执行：30分钟（1800秒）
// - 离线时长：1小时（3600秒）

var plan = new ActivityPlan
{
    LimitType = LimitType.Duration,
    LimitValue = 7200.0,
    ExecutedSeconds = 1800.0,
    // ...
};

var result = engine.FastForward(character, plan, 3600.0);

// 结果
// - SimulatedSeconds = 3600（模拟1小时）
// - UpdatedExecutedSeconds = 5400（1800 + 3600）
// - PlanCompleted = false（5400 < 7200）
```

### 场景2：Duration 计划完成

```csharp
// 初始状态
// - 计划限制：1小时（3600秒）
// - 已执行：45分钟（2700秒）
// - 离线时长：30分钟（1800秒）

var plan = new ActivityPlan
{
    LimitType = LimitType.Duration,
    LimitValue = 3600.0,
    ExecutedSeconds = 2700.0,
    // ...
};

var result = engine.FastForward(character, plan, 1800.0);

// 结果
// - SimulatedSeconds = 900（只模拟剩余的15分钟）
// - UpdatedExecutedSeconds = 3600（2700 + 900）
// - PlanCompleted = true（3600 >= 3600）
```

### 场景3：Infinite 计划

```csharp
// 初始状态
// - 计划限制：无限制
// - 已执行：1.4小时（5000秒）
// - 离线时长：1小时（3600秒）

var plan = new ActivityPlan
{
    LimitType = LimitType.Infinite,
    LimitValue = null,
    ExecutedSeconds = 5000.0,
    // ...
};

var result = engine.FastForward(character, plan, 3600.0);

// 结果
// - SimulatedSeconds = 3600（模拟全部离线时长）
// - UpdatedExecutedSeconds = 8600（5000 + 3600）
// - PlanCompleted = false（无限制计划永远不完成）
```

### 场景4：超过12小时离线上限

```csharp
// 初始状态
// - 计划限制：无限制
// - 离线时长：27.8小时（100000秒）
// - 最大上限：12小时（43200秒）

var plan = new ActivityPlan
{
    LimitType = LimitType.Infinite,
    // ...
};

var result = engine.FastForward(character, plan, 100000.0, 43200.0);

// 结果
// - SimulatedSeconds = 43200（被限制在12小时）
// - UpdatedExecutedSeconds = 43200
// - PlanCompleted = false
```

## 🧪 测试覆盖

### 测试矩阵

| 测试场景 | 计划类型 | 离线时长 | 已执行 | 预期结果 |
|---------|---------|---------|--------|---------|
| 参数验证 | - | - | - | 抛出异常 |
| 离线上限 | Infinite | 13.9h | 0 | 限制在12h |
| Duration未完成 | Duration(2h) | 1h | 0.5h | 模拟1h，未完成 |
| Duration完成 | Duration(1h) | 0.5h | 0.75h | 模拟0.25h，完成 |
| Duration已完成 | Duration(1h) | 0.5h | 1h | 模拟0，已完成 |
| Infinite全时长 | Infinite | 1h | 1.4h | 模拟1h，未完成 |
| Infinite超上限 | Infinite | 27.8h | 0 | 限制在12h |
| 短时间离线 | Duration(1h) | 1min | 0 | 模拟1min |
| 多次调用 | Duration(1h) | 0.5h×2 | 0→0.5h | 累积1h，完成 |

### 单元测试

所有测试均位于 `BlazorIdle.Tests/OfflineFastForwardEngineTests.cs`

```csharp
✓ FastForward_WithNullCharacter_ThrowsArgumentNullException
✓ FastForward_WithNullPlan_ThrowsArgumentNullException
✓ FastForward_WithNegativeOfflineSeconds_ThrowsArgumentOutOfRangeException
✓ FastForward_WithOfflineTimeExceeding12Hours_CapAt12Hours
✓ FastForward_DurationPlan_CalculatesRemainingTimeCorrectly
✓ FastForward_DurationPlan_CompletesWhenRemainingTimeIsLessThanOfflineTime
✓ FastForward_DurationPlan_AlreadyCompleted_SimulatesZeroSeconds
✓ FastForward_InfinitePlan_SimulatesFullOfflineTime
✓ FastForward_InfinitePlan_WithLongOfflineTime_CapsAt12Hours
✓ FastForward_GeneratesValidRewards
✓ FastForward_WithShortOfflineTime_WorksCorrectly
✓ FastForward_MultipleInvocations_AccumulateExecutedSeconds
```

## ⚙️ 配置选项

### 离线时长上限

默认值：12小时（43200秒）

```csharp
// 使用默认值
var result = engine.FastForward(character, plan, offlineSeconds);

// 自定义上限（例如：6小时）
var result = engine.FastForward(character, plan, offlineSeconds, maxCapSeconds: 21600.0);
```

### 活动配置（PayloadJson）

活动计划的 `PayloadJson` 字段包含战斗配置：

```json
{
    "EnemyId": "dummy",
    "EnemyCount": 1,
    "Mode": "continuous",
    "DungeonId": null
}
```

**字段说明**：
- `EnemyId`: 敌人ID（从 EnemyRegistry 获取）
- `EnemyCount`: 敌人数量
- `Mode`: 战斗模式（"continuous", "dungeon", "dungeonloop"）
- `DungeonId`: 副本ID（仅在副本模式下使用）

## 🔍 内部实现细节

### 剩余时长计算逻辑

```csharp
private double CalculateRemainingSeconds(ActivityPlan plan, double cappedOfflineSeconds)
{
    if (plan.LimitType == LimitType.Infinite)
    {
        // 无限制计划：使用全部离线时长
        return cappedOfflineSeconds;
    }

    if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
    {
        // 时长限制计划：计算剩余时长
        var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
        
        // 确保不超过实际离线时长
        return Math.Min(Math.Max(0, remaining), cappedOfflineSeconds);
    }

    // 其他情况默认返回0
    return 0;
}
```

### 完成状态判断逻辑

```csharp
private bool CheckPlanCompleted(ActivityPlan plan, double updatedExecutedSeconds)
{
    if (plan.LimitType == LimitType.Infinite)
        return false; // 无限制计划永远不完成

    if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        return updatedExecutedSeconds >= plan.LimitValue.Value;

    return false;
}
```

### 随机种子生成

```csharp
private static ulong DeriveSeed(Guid characterId, Guid planId)
{
    var charRng = RngContext.FromGuid(characterId);
    var planRng = RngContext.FromGuid(planId);
    charRng.Skip(2);
    planRng.Skip(3);
    ulong salt = (ulong)DateTime.UtcNow.Ticks;
    return RngContext.Hash64(charRng.NextUInt64() ^ planRng.NextUInt64() ^ salt);
}
```

## 📝 设计原则

1. **最小修改原则**：复用现有的 `BattleSimulator` 和 `EconomyCalculator`，不重复实现战斗逻辑
2. **清晰的职责分离**：引擎只负责快进模拟，不负责数据库更新和计划自动衔接
3. **可测试性**：所有核心逻辑都有单元测试覆盖
4. **可配置性**：离线时长上限可以通过参数配置
5. **代码风格一致**：遵循项目现有的代码风格和命名规范

## 🚀 后续扩展

### Step 2: 扩展 OfflineSettlementService

下一步需要在 `OfflineSettlementService` 中添加 `CheckAndSettleAsync` 方法：

```csharp
public async Task<OfflineCheckResult> CheckAndSettleAsync(
    Guid characterId, 
    CancellationToken ct)
{
    // 1. 获取角色，计算离线时长
    // 2. 查找运行中的计划
    // 3. 调用 OfflineFastForwardEngine.FastForward()
    // 4. 更新计划状态
    // 5. 如果计划完成，尝试启动下一个计划
    // 6. 返回 OfflineCheckResult
}
```

### Step 3: 添加 API 端点

- `GET /api/offline/check?characterId={id}` - 检查离线收益
- `POST /api/offline/apply` - 应用离线结算

### Step 4: 前端集成

- 创建 `OfflineSettlementDialog.razor` 组件
- 扩展 `ApiClient.cs` 添加离线 API 方法
- 修改 `Characters.razor` 集成离线检查

## 📚 相关文档

- [离线战斗系统实施总结](./离线战斗系统实施总结.md)
- [离线战斗快速开始](./离线战斗快速开始.md)
- [OfflineBattleImplementationPlan](./OfflineBattleImplementationPlan.md)

---

**文档版本**：1.0  
**创建日期**：2025-01-08  
**最后更新**：2025-01-08
