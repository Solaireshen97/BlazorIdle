# OfflineFastForwardEngine 实施文档

## 概述

本文档说明 `OfflineFastForwardEngine` 的实现细节，这是离线战斗系统的核心组件。该引擎专门处理离线期间的战斗计算，支持从离线时刻的进度无感继承。

## 核心特性

### 1. 无感继承

引擎支持从离线时刻的进度无感继承，意味着：
- 如果一个副本打到一半进入离线，会从离线这个时间点的进度开始继续计算
- 使用 `ActivityPlan.ExecutedSeconds` 记录已执行时长
- 计算剩余时长时自动考虑已执行部分

### 2. 离线时长上限

- 默认上限：12 小时（43200 秒）
- 可通过参数 `maxCapSeconds` 配置
- 如果离线时间超过上限，只计算上限时长内的收益

### 3. 支持多种计划类型

#### Duration 类型计划
- 有明确的时长限制（例如：2 小时战斗计划）
- 剩余时长计算：`remaining = LimitValue - ExecutedSeconds`
- 实际模拟时长：`min(remaining, cappedOfflineSeconds)`
- 完成判断：当 `ExecutedSeconds >= LimitValue` 时标记为完成

#### Infinite 类型计划
- 无时长限制，持续执行直到手动停止
- 模拟全部离线时长（受上限约束）
- 永远不会自动标记为完成

### 4. 战斗模拟

- 复用 `BattleSimulator` 组件进行战斗模拟
- 支持战斗类型：
  - **Combat**：连续战斗模式
  - **Dungeon**：地下城模式（单次/循环）

## 文件结构

```
BlazorIdle.Server/Application/Battles/Offline/
├── Offline.cs                        # 原有的离线结算服务（基础版）
└── OfflineFastForwardEngine.cs       # 新增的离线快进引擎
```

## 数据模型

### OfflineFastForwardResult

离线快进结果数据模型：

```csharp
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }           // 角色ID
    public Guid PlanId { get; init; }                // 计划ID
    public double SimulatedSeconds { get; init; }     // 实际模拟时长
    public bool PlanCompleted { get; init; }          // 计划是否完成
    public long TotalDamage { get; init; }            // 总伤害
    public int TotalKills { get; init; }              // 总击杀数
    public long Gold { get; init; }                   // 金币收益
    public long Exp { get; init; }                    // 经验收益
    public Dictionary<string, double> LootExpected { get; init; } // 期望掉落
    public Dictionary<string, int> LootSampled { get; init; }     // 采样掉落
    public double UpdatedExecutedSeconds { get; init; } // 更新后的已执行时长
    public string DropMode { get; init; }             // 掉落模式
}
```

## 核心方法

### FastForward

快进模拟离线战斗的主方法。

**参数：**
- `character`: 角色数据
- `plan`: 活动计划（必须为 Running 状态）
- `offlineSeconds`: 离线时长（秒）
- `maxCapSeconds`: 离线时长上限，默认 43200（12小时）
- `dropMode`: 掉落模式，"expected" 或 "sampled"

**返回：**
`OfflineFastForwardResult` 对象，包含模拟结果和收益数据

**处理流程：**

1. **验证计划状态**
   - 只能快进 `Running` 状态的计划
   - 其他状态会抛出异常

2. **应用离线时长上限**
   ```csharp
   var cappedOfflineSeconds = Math.Min(offlineSeconds, maxCapSeconds);
   ```

3. **计算计划剩余时长**
   - Duration 计划：`remaining = LimitValue - ExecutedSeconds`
   - Infinite 计划：`remaining = cappedOfflineSeconds`

4. **执行战斗模拟**
   - 构建 `BattleConfig` 配置
   - 使用 `BattleSimulator.CreateRunningBattle()` 创建战斗实例
   - 调用 `FastForwardTo()` 快进到目标时间

5. **更新计划状态**
   - 更新 `ExecutedSeconds`
   - 如果达到限制，标记为 `Completed`

6. **计算经济收益**
   - 统计击杀数据
   - 使用 `EconomyCalculator` 计算金币、经验、掉落

## 使用示例

### 示例 1：Duration 计划从中途继承

```csharp
// 场景：2小时战斗计划，已执行0.5小时，离线1小时
var character = GetCharacter();
var plan = new ActivityPlan
{
    Type = ActivityType.Combat,
    LimitType = LimitType.Duration,
    LimitValue = 7200,      // 2小时
    ExecutedSeconds = 1800, // 已执行0.5小时
    State = ActivityState.Running,
    // ... 其他字段
};

var engine = new OfflineFastForwardEngine(simulator);
var result = engine.FastForward(character, plan, offlineSeconds: 3600);

// 结果：
// - SimulatedSeconds = 3600 (离线1小时)
// - UpdatedExecutedSeconds = 5400 (1800 + 3600)
// - PlanCompleted = false (还剩0.5小时)
```

### 示例 2：Duration 计划完成

```csharp
// 场景：1小时战斗计划，已执行50分钟，离线30分钟
var plan = new ActivityPlan
{
    Type = ActivityType.Combat,
    LimitType = LimitType.Duration,
    LimitValue = 3600,      // 1小时
    ExecutedSeconds = 3000, // 已执行50分钟
    State = ActivityState.Running,
    // ... 其他字段
};

var result = engine.FastForward(character, plan, offlineSeconds: 1800);

// 结果：
// - SimulatedSeconds = 600 (只模拟剩余的10分钟)
// - UpdatedExecutedSeconds = 3600 (完整1小时)
// - PlanCompleted = true
// - plan.State = ActivityState.Completed
```

### 示例 3：Infinite 计划

```csharp
// 场景：无限战斗计划，已执行2小时，离线3小时
var plan = new ActivityPlan
{
    Type = ActivityType.Combat,
    LimitType = LimitType.Infinite,
    ExecutedSeconds = 7200, // 已执行2小时
    State = ActivityState.Running,
    // ... 其他字段
};

var result = engine.FastForward(character, plan, offlineSeconds: 10800);

// 结果：
// - SimulatedSeconds = 10800 (模拟全部3小时)
// - UpdatedExecutedSeconds = 18000 (2小时 + 3小时)
// - PlanCompleted = false (无限计划不会完成)
```

### 示例 4：超过12小时上限

```csharp
// 场景：离线24小时，但只计算12小时
var plan = new ActivityPlan
{
    Type = ActivityType.Combat,
    LimitType = LimitType.Infinite,
    ExecutedSeconds = 0,
    State = ActivityState.Running,
    // ... 其他字段
};

var result = engine.FastForward(character, plan, offlineSeconds: 86400);

// 结果：
// - SimulatedSeconds = 43200 (12小时上限)
// - UpdatedExecutedSeconds = 43200
```

## 单元测试

完整的单元测试套件位于：
```
tests/BlazorIdle.Tests/OfflineFastForwardEngineTests.cs
```

### 测试覆盖

1. **离线时长上限测试**
   - `FastForward_WithOfflineTimeExceeding12Hours_ShouldCapAt12Hours`

2. **Duration 计划测试**
   - `FastForward_DurationPlan_ShouldCalculateRemainingTime`
   - `FastForward_DurationPlan_PartialCompletion_ShouldNotComplete`
   - `FastForward_DurationPlan_AlreadyCompleted_ShouldReturnZeroSimulation`
   - `FastForward_DurationPlan_NearCompletion_ShouldCompleteExactly`

3. **Infinite 计划测试**
   - `FastForward_InfinitePlan_ShouldSimulateFullOfflineTime`
   - `FastForward_InfinitePlan_WithLongOfflineTime_ShouldCapAt12Hours`

4. **状态更新测试**
   - `FastForward_ShouldUpdatePlanState_WhenCompleted`

5. **无感继承测试**
   - `FastForward_InheritProgress_FromMidBattle_ShouldContinueSeamlessly`

6. **多种计划类型测试**
   - `FastForward_CombatPlan_ShouldReturnRewards`
   - `FastForward_DungeonPlan_ShouldWork`

7. **异常处理测试**
   - `FastForward_WithNonRunningPlan_ShouldThrowException`

## 设计考虑

### 1. 为什么使用 BattleSimulator？

- **代码复用**：避免重复实现战斗逻辑
- **一致性**：保证在线和离线战斗结果一致
- **维护性**：战斗逻辑只在一处维护

### 2. 为什么需要 ExecutedSeconds？

- **无感继承**：记录计划的执行进度
- **精确计算**：准确计算剩余时长
- **状态恢复**：支持从任意时间点恢复

### 3. 为什么有12小时上限？

- **平衡性**：防止长时间离线获得过多收益
- **性能考虑**：限制单次模拟的计算量
- **游戏设计**：鼓励玩家定期上线

### 4. 掉落模式说明

- **expected**：期望值计算，结果稳定可预测
- **sampled**：随机采样，更真实但有波动

## 后续集成

此引擎是离线战斗系统的第一步，后续需要：

1. **扩展 OfflineSettlementService**
   - 添加 `CheckAndSettleAsync()` 方法
   - 集成 `OfflineFastForwardEngine`
   - 处理计划完成后的自动衔接

2. **添加 API 端点**
   - `GET /api/offline/check` - 检查离线收益
   - `POST /api/offline/apply` - 应用离线结算

3. **前端集成**
   - 创建离线结算弹窗组件
   - 在 Characters 页面添加离线检查
   - 实现收益展示和领取功能

## 版本历史

- **v1.0.0** (2025-01-07)
  - 初始实现
  - 支持 Combat 和 Dungeon 类型计划
  - 实现无感继承功能
  - 完整的单元测试覆盖

## 参考文档

- [离线战斗系统实施总结](./离线战斗系统实施总结.md)
- [离线战斗快速开始](./离线战斗快速开始.md)
- [离线战斗流程图](./离线战斗流程图.md)
- [Activity Plan System](./ActivityPlanSystem.md)
- [BattleSimulator Refactoring](./BattleSimulator-Refactoring.md)
