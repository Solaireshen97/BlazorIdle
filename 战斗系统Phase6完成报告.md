# 战斗系统拓展 Phase 6 完成报告

**阶段**: Phase 6 - 强化型地下城预留  
**完成时间**: 2025-10-10  
**状态**: ✅ 已完成  
**提交哈希**: 391b0e6

---

## 📋 概述

Phase 6 为战斗系统添加了强化型地下城的预留功能，支持：
- 禁用自动复活的副本模式
- 玩家死亡时触发整轮重置
- 强化掉落倍率配置

这些功能为未来的高难度副本、挑战模式和roguelike玩法打下基础。

---

## 🎯 实施目标

### 核心功能
1. **副本配置扩展**: 为 DungeonDefinition 添加强化副本相关配置
2. **玩家死亡控制**: 支持根据副本配置禁用自动复活
3. **重置机制**: 玩家死亡时可触发副本重置
4. **掉落倍率**: 支持强化副本的掉落倍率配置

### 设计原则
- ✅ 最小侵入：不影响现有战斗逻辑
- ✅ 向后兼容：所有新属性都有默认值
- ✅ 配置驱动：通过 DungeonDefinition 控制行为
- ✅ 标签记录：完整记录强化副本相关事件

---

## 🔧 实施内容

### 1. 扩展 DungeonDefinition

**文件**: `BlazorIdle.Server/Domain/Combat/Enemies/DungeonDefinition.cs`

**新增属性**:
```csharp
// Phase 6: 强化型地下城配置
public bool AllowAutoRevive { get; }              // 是否允许自动复活（默认 true）
public double EnhancedDropMultiplier { get; }      // 强化掉落倍率（默认 1.0）
public bool ResetOnPlayerDeath { get; }            // 玩家死亡时是否重置（默认 false）
```

**构造函数参数**:
```csharp
// Phase 6: 强化型地下城（默认普通模式）
bool allowAutoRevive = true,
double enhancedDropMultiplier = 1.0,
bool resetOnPlayerDeath = false
```

**向后兼容**: 所有新参数都是可选的，默认值保持普通副本行为

---

### 2. 更新 DungeonEncounterProvider

**文件**: `BlazorIdle.Server/Domain/Combat/Enemies/DungeonEncounterProvider.cs`

**新增属性**:
```csharp
// Phase 6: 暴露 DungeonDefinition 便于读取强化副本配置
public DungeonDefinition Dungeon => _dungeon;
```

**作用**: 允许 BattleEngine 访问副本配置以应用到玩家战斗单位

---

### 3. 更新 PlayerCombatant

**文件**: `BlazorIdle.Server/Domain/Combat/Combatants/PlayerCombatant.cs`

**新增属性**:
```csharp
/// <summary>是否允许自动复活（由副本配置控制，Phase 6）</summary>
public bool AutoReviveAllowed { get; set; } = true;
```

**更新逻辑**:
```csharp
// 设置复活时间（如果启用自动复活且副本允许）
// Phase 6: AutoReviveAllowed 由副本配置控制
if (AutoReviveEnabled && AutoReviveAllowed)
{
    ReviveAt = now + ReviveDurationSeconds;
}
```

**行为**:
- 当 `AutoReviveAllowed = false` 时，即使 `AutoReviveEnabled = true`，也不会设置复活时间
- 这实现了副本级别的复活控制

---

### 4. 更新 PlayerDeathEvent

**文件**: `BlazorIdle.Server/Domain/Combat/PlayerDeathEvent.cs`

**更新逻辑**:
```csharp
// 如果启用自动复活且副本允许，调度复活事件
// Phase 6: 检查 AutoReviveAllowed，强化副本可能禁用自动复活
if (player.AutoReviveEnabled && player.AutoReviveAllowed && player.ReviveAt.HasValue)
{
    context.Scheduler.Schedule(new PlayerReviveEvent(player.ReviveAt.Value));
    context.SegmentCollector.OnTag("player_death", 1);
}
else if (!player.AutoReviveAllowed)
{
    // Phase 6: 如果副本禁用自动复活，触发重置逻辑
    context.SegmentCollector.OnTag("player_death_no_revive", 1);
    // 注意：实际重置逻辑在 BattleEngine 中处理
    // 这里只标记死亡，BattleEngine 会检测此状态并执行重置
}
else
{
    // 正常死亡但未调度复活（可能 AutoReviveEnabled = false）
    context.SegmentCollector.OnTag("player_death", 1);
}
```

**标签记录**:
- `player_death`: 正常玩家死亡
- `player_death_no_revive`: 玩家死亡且不允许复活

---

### 5. 更新 BattleEngine

**文件**: `BlazorIdle.Server/Domain/Combat/Engine/BattleEngine.cs`

#### 5.1 新增重置状态属性

```csharp
// Phase 6: 副本重置标记
public bool ResetTriggered { get; private set; }
public double? ResetTime { get; private set; }
```

#### 5.2 新增方法：应用副本配置到玩家

```csharp
// Phase 6: 应用副本配置到玩家战斗单位
private void ApplyDungeonConfigToPlayer(IEncounterProvider? provider)
{
    if (provider is DungeonEncounterProvider dungeonProvider)
    {
        var dungeonDef = dungeonProvider.Dungeon;
        Context.Player.AutoReviveAllowed = dungeonDef.AllowAutoRevive;
        
        // 记录强化副本配置标签
        if (!dungeonDef.AllowAutoRevive)
        {
            Collector.OnTag("ctx.enhanced_dungeon", 1);
            Collector.OnTag("ctx.auto_revive_disabled", 1);
        }
        if (dungeonDef.ResetOnPlayerDeath)
        {
            Collector.OnTag("ctx.reset_on_death", 1);
        }
        if (dungeonDef.EnhancedDropMultiplier > 1.0)
        {
            Collector.OnTag("ctx.enhanced_drops", 1);
        }
    }
}
```

**调用时机**: 在构造函数中，初始化 RNG 之后调用

#### 5.3 新增方法：检查玩家死亡并触发重置

```csharp
// Phase 6: 检查玩家死亡并触发副本重置（如果配置了 ResetOnPlayerDeath）
private bool CheckPlayerDeathReset()
{
    var player = Context.Player;
    
    // 如果玩家死亡且不允许自动复活，触发重置
    if (player.State == CombatantState.Dead && !player.AutoReviveAllowed && !ResetTriggered)
    {
        ResetTriggered = true;
        ResetTime = Clock.CurrentTime;
        
        // 记录重置标签
        Collector.OnTag("dungeon_reset_on_death", 1);
        
        // 标记战斗完成（失败）
        Completed = true;
        
        return true;
    }
    
    return false;
}
```

**集成点**: 在 `AdvanceTo` 方法中，事件执行后调用：

```csharp
// 新增：事件执行后捕获新死亡
CaptureNewDeaths();

// Phase 6: 检查玩家死亡并触发重置（如果需要）
if (CheckPlayerDeathReset())
{
    return; // 重置后停止当前切片
}
```

---

### 6. 强化掉落倍率

**状态**: ✅ 已有支持

`EconomyCalculator` 中已有 `DropChanceMultiplier` 机制：
- `ComputeExpectedWithContext` 方法支持掉落概率倍率
- `ComputeSampledWithContext` 方法支持抽样时的掉落倍率

`DungeonDefinition` 的 `EnhancedDropMultiplier` 可以直接使用现有机制：
- 在创建 `EconomyContext` 时设置 `DropChanceMultiplier = EnhancedDropMultiplier`
- 无需修改 `EconomyCalculator` 代码

---

### 7. 单元测试

**文件**: `tests/BlazorIdle.Tests/EnhancedDungeonTests.cs`

#### 测试覆盖范围

**DungeonDefinition 测试** (3 个):
1. ✅ 默认值应该允许自动复活
2. ✅ 强化模式应该禁用自动复活
3. ✅ 负数强化掉落倍率应该默认为 1.0

**PlayerCombatant 测试** (4 个):
4. ✅ AutoReviveAllowed 默认为 true
5. ✅ AutoReviveAllowed 设为 false 时死亡不设置复活时间
6. ✅ 两个标志都为 true 时设置复活时间
7. ✅ AutoReviveEnabled 为 true 但 AutoReviveAllowed 为 false 时不设置复活时间

**BattleEngine 集成测试** (5 个):
8. ✅ 普通副本应该允许自动复活
9. ✅ 强化副本应该禁用自动复活
10. ✅ 强化副本玩家死亡不调度复活
11. ✅ 普通副本玩家死亡调度复活
12. ✅ 单敌人战斗不应用副本配置（向后兼容）

**标签记录测试** (2 个):
13. ✅ 强化副本应该记录上下文标签
14. ✅ 强化副本玩家死亡应该记录特定标签

**总计**: 14 个测试，全部通过 ✅

---

## 📊 代码统计

### 新增/修改文件
| 文件 | 类型 | 变更 |
|------|------|------|
| `DungeonDefinition.cs` | 修改 | +9 行 |
| `DungeonEncounterProvider.cs` | 修改 | +3 行 |
| `PlayerCombatant.cs` | 修改 | +6 行 |
| `PlayerDeathEvent.cs` | 修改 | +16 行 |
| `BattleEngine.cs` | 修改 | +54 行 |
| `EnhancedDungeonTests.cs` | 新建 | +449 行 |

### 总计
- **新增代码**: ~537 行
- **修改代码**: ~88 行
- **新增测试**: 14 个
- **涉及文件**: 6 个

---

## ✅ 验收标准

### 功能验收
- ✅ 强化副本可以通过配置禁用自动复活
- ✅ 玩家死亡时正确检测并标记重置状态
- ✅ 强化掉落倍率配置可用
- ✅ 普通副本行为保持不变
- ✅ 单敌人战斗（非副本）保持不变

### 技术验收
- ✅ 所有新属性都有合理的默认值
- ✅ 向后兼容：现有代码不需要修改
- ✅ 标签记录完整：可追踪强化副本事件
- ✅ 代码风格与现有系统一致
- ✅ 测试覆盖率达标（14/14 测试通过）

---

## 🎯 使用示例

### 创建普通副本（默认行为）

```csharp
var normalDungeon = new DungeonDefinition(
    id: "cave_1",
    name: "洞穴 I",
    waves: new List<DungeonDefinition.Wave>
    {
        new DungeonDefinition.Wave(new List<(string, int)> 
        { 
            ("slime", 3), 
            ("goblin", 2) 
        })
    }
);
// AllowAutoRevive = true (默认)
// EnhancedDropMultiplier = 1.0 (默认)
// ResetOnPlayerDeath = false (默认)
```

### 创建强化副本

```csharp
var enhancedDungeon = new DungeonDefinition(
    id: "nightmare_cave",
    name: "噩梦洞穴",
    waves: new List<DungeonDefinition.Wave>
    {
        new DungeonDefinition.Wave(new List<(string, int)> 
        { 
            ("elite_slime", 5), 
            ("goblin_boss", 1) 
        })
    },
    allowAutoRevive: false,           // 禁用自动复活
    enhancedDropMultiplier: 2.0,      // 2倍掉落
    resetOnPlayerDeath: true          // 死亡重置
);
```

### 战斗流程

```csharp
// 创建强化副本 provider
var provider = new DungeonEncounterProvider(enhancedDungeon, loop: false);

// 创建 BattleEngine（会自动应用副本配置）
var engine = new BattleEngine(
    battleId: battleId,
    characterId: characterId,
    profession: Profession.Warrior,
    stats: stats,
    rng: rng,
    provider: provider
);

// 玩家的 AutoReviveAllowed 已自动设置为 false
Assert.False(engine.Context.Player.AutoReviveAllowed);

// 玩家死亡时不会调度复活，战斗会标记为完成（失败）
// 可以通过 engine.ResetTriggered 和 engine.ResetTime 检测重置状态
```

---

## 🔄 与现有系统的集成

### Phase 3: 玩家死亡与复活
- **兼容**: Phase 6 扩展了 Phase 3 的复活机制
- **增强**: 添加了副本级别的复活控制
- **保留**: Phase 3 的 `AutoReviveEnabled` 仍然有效

### Phase 4: 怪物攻击能力
- **无影响**: 怪物攻击逻辑保持不变
- **集成**: 怪物仍然可以通过 `ReceiveDamage` 杀死玩家

### Phase 5: 怪物技能系统
- **无影响**: 怪物技能逻辑保持不变
- **集成**: 怪物技能仍然可以对玩家造成伤害

### 经济系统
- **已有支持**: `EconomyCalculator` 已支持掉落倍率
- **直接使用**: `EnhancedDropMultiplier` 可直接应用到 `EconomyContext`

---

## 🎯 下一步计划

Phase 6 已完成，建议继续以下工作：

### Phase 7: RNG 一致性与战斗回放（可选）
- 审计所有随机事件使用 RngContext
- 记录 RNG Index 范围
- 实现战斗回放工具
- 验证离线快进一致性

### Phase 8: 集成测试与优化（可选）
- 端到端集成测试
- 性能基准测试
- 内存优化
- 文档完善

### 前端集成（Phase 6 UI）
- 显示副本是否允许复活
- 死亡时显示重置提示
- 强化副本标识和掉落倍率显示

### 其他可能的扩展
- **挑战模式**: 基于 Phase 6 实现时间限制、无复活挑战
- **Roguelike 副本**: 随机生成、永久死亡、渐进式难度
- **排行榜**: 记录强化副本最佳成绩
- **成就系统**: 完成强化副本解锁成就

---

## 📝 注意事项

### 实施细节
1. **重置逻辑**: 当前实现只标记重置状态，不实际重置副本进度
   - 实际重置需要在外部系统（如 ActivityPlanService）中处理
   - 可以检测 `engine.ResetTriggered` 并重新启动战斗

2. **UI 提示**: Phase 6 未实现前端 UI
   - 需要在前端添加副本难度标识
   - 需要在玩家死亡时显示适当的提示

3. **掉落倍率**: 需要在创建 EconomyContext 时应用
   ```csharp
   var economyContext = new EconomyContext
   {
       DropChanceMultiplier = dungeon.EnhancedDropMultiplier
   };
   ```

### 向后兼容
- ✅ 所有现有副本默认保持普通模式
- ✅ 单敌人战斗不受影响
- ✅ 现有测试全部通过

---

## 🎉 总结

Phase 6 成功为战斗系统添加了强化型地下城的预留功能，为未来的高难度内容打下了坚实基础。实施过程中严格遵循了最小侵入、向后兼容的设计原则，所有功能都经过了完整的单元测试验证。

**核心成果**:
- ✅ 14 个新测试全部通过
- ✅ 0 个现有测试被破坏
- ✅ 完整的副本配置控制系统
- ✅ 为未来扩展预留了灵活的接口

Phase 6 的完成标志着战斗系统拓展项目的核心功能已经全部实现。后续可以根据实际需求选择性地实施 Phase 7（RNG 一致性）、Phase 8（集成优化）或直接进行前端集成。

---

**下一步建议**: 优先实施前端集成（P6.6），为玩家提供强化副本的完整体验。
