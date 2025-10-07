# OfflineFastForwardEngine 实施文档

## 概述

OfflineFastForwardEngine 是离线战斗系统的核心引擎，负责快速模拟离线期间的战斗并计算收益。

## 核心设计理念

### 无感切换设计

本实现的核心特点是**无感的离线-在线切换**：

1. **从离线点继续**：如果副本打到一半进入离线，离线计算会从该时间点继续，而不是重新开始
2. **状态继承**：通过 `ExecutedSeconds` 字段记录已执行时间，实现状态的无缝继承
3. **自然衔接**：如果离线期间计划完成，系统会标记完成；如果未完成，上线后可以继续执行剩余部分

### 关键实现点

```
离线前：计划执行了 20 分钟（ExecutedSeconds = 1200）
离线：10 分钟
离线计算：从 20 分钟的状态继续模拟 10 分钟
结果：ExecutedSeconds = 1800（30分钟）
上线后：如果计划是 1 小时，则还剩 30 分钟可以继续在线执行
```

## 文件位置

- **引擎实现**: `BlazorIdle.Server/Application/Battles/Offline/OfflineFastForwardEngine.cs`
- **单元测试**: `tests/BlazorIdle.Tests/OfflineFastForwardEngineTests.cs`

## API 说明

### OfflineFastForwardEngine.FastForward

```csharp
public OfflineFastForwardResult FastForward(
    Character character,
    ActivityPlan plan,
    double offlineSeconds,
    double maxCapSeconds = 43200.0) // 12小时默认上限
```

**参数**：
- `character`: 角色信息（用于构建战斗配置）
- `plan`: 活动计划（包含战斗配置、执行状态等）
- `offlineSeconds`: 离线时长（秒）
- `maxCapSeconds`: 离线时长上限（默认12小时 = 43200秒）

**返回值** `OfflineFastForwardResult`：
- `CharacterId`: 角色ID
- `PlanId`: 计划ID
- `SimulatedSeconds`: 实际模拟的时长
- `PlanCompleted`: 计划是否完成
- `TotalDamage`: 总伤害
- `TotalKills`: 总击杀数
- `Gold`: 金币收益
- `Exp`: 经验收益
- `Loot`: 物品掉落（字典：物品ID -> 数量）
- `Segments`: 战斗段数据（用于回顾）
- `UpdatedExecutedSeconds`: 更新后的执行时长（关键字段，用于状态继承）

## 核心逻辑流程

### 1. 离线时长限制

```csharp
var cappedSeconds = Math.Min(offlineSeconds, maxCapSeconds);
```

离线收益最多计算12小时，超过部分不计算。

### 2. 计算剩余时长

根据计划类型计算实际模拟时长：

**无限模式（Infinite）**：
```csharp
if (plan.LimitType == LimitType.Infinite)
{
    return availableSeconds; // 使用全部离线时间
}
```

**限时模式（Duration）**：
```csharp
if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
{
    var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
    return Math.Max(0, Math.Min(remaining, availableSeconds));
}
```

示例：
- 计划限制：1小时（3600秒）
- 已执行：30分钟（1800秒）
- 离线：2小时（7200秒）
- 实际模拟：30分钟（1800秒）—— 只模拟剩余部分

### 3. 战斗模拟

使用 `BattleSimulator` 进行快进模拟：

```csharp
var rb = _simulator.CreateRunningBattle(config, seconds);
rb.FastForwardTo(seconds);
```

支持两种活动类型：
- **Combat**（普通战斗）：连续刷怪模式
- **Dungeon**（地下城）：波次/轮次模式

### 4. 状态更新

```csharp
var updatedExecutedSeconds = plan.ExecutedSeconds + remainingSeconds;
plan.ExecutedSeconds = updatedExecutedSeconds; // 更新计划状态
var planCompleted = plan.IsLimitReached();
```

**关键点**：直接更新 `ActivityPlan` 对象的 `ExecutedSeconds`，确保状态继承。

### 5. 收益计算

使用 `EconomyCalculator.ComputeExpectedWithContext` 计算经济收益：
- 金币
- 经验
- 物品掉落（期望值模式）

## 测试覆盖

### 核心功能测试

1. **离线时长上限测试**
   - 验证超过12小时只计算12小时

2. **无限模式测试**
   - 验证使用全部离线时间
   - 验证永不完成

3. **限时模式测试**
   - 验证计算剩余时长
   - 验证完成状态判断
   - 验证已完成计划返回零时间

4. **无感继承测试**
   - 验证 `ExecutedSeconds` 正确累加
   - 验证从中途继续
   - 验证多次离线累加正确

5. **收益测试**
   - 验证生成战斗段
   - 验证计算收益

6. **地下城测试**
   - 验证地下城模式工作正常

### 测试结果

所有12个测试用例全部通过 ✅

## 使用示例

### 基本用法

```csharp
var engine = new OfflineFastForwardEngine(battleSimulator);

// 用户离线2小时后上线
var offlineSeconds = 2 * 3600.0;

// 快进模拟
var result = engine.FastForward(character, runningPlan, offlineSeconds);

// 检查结果
if (result.PlanCompleted)
{
    // 计划完成，可以启动下一个
    Console.WriteLine($"计划完成！获得 {result.Gold} 金币，{result.Exp} 经验");
}
else
{
    // 计划未完成，可以继续在线执行
    Console.WriteLine($"模拟了 {result.SimulatedSeconds} 秒，剩余 {plan.LimitValue - result.UpdatedExecutedSeconds} 秒");
}

// 更新计划状态（已在 FastForward 中更新）
await planRepository.UpdateAsync(plan, ct);
```

### 多次离线场景

```csharp
// 第一次离线
var result1 = engine.FastForward(character, plan, 1200.0); // 20分钟
// plan.ExecutedSeconds 现在是 1200

// 第二次离线
var result2 = engine.FastForward(character, plan, 1800.0); // 30分钟
// plan.ExecutedSeconds 现在是 3000（50分钟）

// 状态会自动累加，实现无感继承
```

## 与现有系统集成

### 依赖关系

```
OfflineFastForwardEngine
  ↓ 使用
BattleSimulator (已有)
  ↓ 创建
RunningBattle (已有)
  ↓ 使用
BattleEngine (已有)
```

### 数据流

```
ActivityPlan (持久化)
  → OfflineFastForwardEngine.FastForward()
    → BattleSimulator.CreateRunningBattle()
      → 快进模拟
      → 收益计算
    ← OfflineFastForwardResult
  ← 更新 ExecutedSeconds
```

## 设计亮点

### 1. 完全复用现有代码

- 使用 `BattleSimulator` 进行战斗模拟
- 使用 `EconomyCalculator` 计算收益
- 使用 `ActivityPlan.IsLimitReached()` 判断完成
- **零重复代码**，最小化维护成本

### 2. 状态继承机制

通过 `ExecutedSeconds` 实现：
- **离线前状态保存**：记录已执行时长
- **离线计算继续**：从保存点继续
- **上线后衔接**：如果未完成，可继续执行

### 3. 类型安全

- 使用强类型 `OfflineFastForwardResult`
- 使用枚举 `ActivityType`、`LimitType`
- JSON 序列化/反序列化有类型检查

### 4. 可扩展性

- 支持新的活动类型（通过 `ActivityType` 扩展）
- 支持自定义离线上限（`maxCapSeconds` 参数）
- 支持不同的收益计算模式（当前使用期望值模式）

## 后续步骤

1. ✅ **Step 1: 创建 OfflineFastForwardEngine** （已完成）
2. ⏭️ **Step 2: 扩展 OfflineSettlementService** 添加自动检测
3. ⏭️ **Step 3: 添加 API 端点** `/api/offline/check` 和 `/api/offline/apply`
4. ⏭️ **Step 4: 前端集成** 创建离线结算弹窗

## 相关文档

- [离线战斗系统实施总结](./离线战斗系统实施总结.md)
- [离线战斗实施方案](./OfflineBattleImplementationPlan.md)
- [离线战斗快速开始](./离线战斗快速开始.md)
- [整合设计总结](../整合设计总结.txt) - 第9章节

## 更新日志

- **2025-01**: 初始实现
  - 实现 `OfflineFastForwardEngine`
  - 实现无感切换机制
  - 完成12个单元测试
  - 编写实施文档
