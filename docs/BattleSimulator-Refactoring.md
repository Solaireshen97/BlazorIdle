# BattleSimulator 重构文档

## 问题描述

原始代码中，战斗模拟逻辑在多个地方重复实现：

1. **BattleRunner** - 用于同步战斗模拟
2. **RunningBattle** - 用于异步/步进式战斗
3. **OfflineSettlementService** - 用于离线时间结算

每次需要修改战斗逻辑时，需要在多个地方同步修改，容易出错且维护成本高。

## 解决方案

创建统一的 **BattleSimulator** 组件，封装所有 BattleEngine 的创建和配置逻辑。

### 核心设计

#### BattleSimulator 类

```csharp
public class BattleSimulator
{
    // 战斗配置参数
    public sealed class BattleConfig { ... }
    
    // 战斗模拟结果
    public sealed class SimulationResult { ... }
    
    // 同步执行战斗（用于 BattleRunner）
    public SimulationResult RunForDuration(BattleConfig config, double durationSeconds)
    
    // 创建异步战斗实例（用于 RunningBattle 和 OfflineSettlement）
    public RunningBattle CreateRunningBattle(BattleConfig config, double targetDurationSeconds)
}
```

### 主要特性

1. **统一配置**: 通过 `BattleConfig` 类统一所有战斗参数
2. **模式支持**: 支持 duration、continuous、dungeon、dungeonloop 等多种模式
3. **Provider 创建**: 自动根据模式创建相应的 IEncounterProvider
4. **元数据管理**: 统一创建和管理 BattleMeta
5. **灵活性**: 支持传入已构造的 RngContext 或 IEncounterProvider

### 修改的文件

#### 新增文件
- `BlazorIdle.Server/Application/Battles/BattleSimulator.cs` - 核心模拟器组件

#### 修改的文件
1. **BattleRunner.cs**
   - 添加 BattleSimulator 依赖注入
   - 使用 BattleSimulator.RunForDuration() 替代内部实现

2. **OfflineSettlementService.cs**
   - 添加 BattleSimulator 依赖注入
   - 使用 BattleSimulator.CreateRunningBattle() 替代直接创建 RunningBattle

3. **StartBattleService.cs**
   - 添加 BattleSimulator 依赖注入
   - 使用 BattleSimulator.CreateRunningBattle() 统一创建战斗实例

4. **DependencyInjection.cs**
   - 注册 BattleSimulator 为 Singleton 服务

5. **测试文件** (所有)
   - 更新所有测试以传递 BattleSimulator 实例到 BattleRunner

### 优势

1. **消除重复**: 战斗创建逻辑只在一个地方维护
2. **易于扩展**: 添加新的战斗模式只需修改 BattleSimulator
3. **一致性**: 确保所有战斗场景使用相同的逻辑
4. **可测试性**: 更容易为战斗模拟编写单元测试
5. **维护性**: 减少了代码重复，降低了维护成本

### 向后兼容

- 所有公共 API 保持不变
- BattleRunner、RunningBattle 和 OfflineSettlementService 的接口未改变
- 现有代码无需修改，只有内部实现发生变化

### 使用示例

#### 同步战斗（BattleRunner）

```csharp
var simulator = new BattleSimulator();
var config = new BattleSimulator.BattleConfig
{
    BattleId = battleId,
    CharacterId = characterId,
    Profession = profession,
    Stats = stats,
    Rng = rng,  // 或使用 Seed = seedValue
    EnemyDef = enemyDef,
    Mode = "duration"
};

var result = simulator.RunForDuration(config, durationSeconds);
```

#### 异步战斗（RunningBattle）

```csharp
var simulator = new BattleSimulator();
var config = new BattleSimulator.BattleConfig
{
    BattleId = battleId,
    CharacterId = characterId,
    Profession = profession,
    Stats = stats,
    Seed = seed,
    EnemyDef = enemyDef,
    Mode = "continuous",
    ContinuousRespawnDelaySeconds = 3.0
};

var runningBattle = simulator.CreateRunningBattle(config, targetSeconds);
runningBattle.FastForwardTo(targetSeconds);
```

### 测试

新增测试文件 `BattleSimulatorTests.cs` 验证：
- RunForDuration 方法正确创建和执行战斗
- CreateRunningBattle 方法正确创建 RunningBattle 实例
- 所有现有测试继续通过

## 总结

通过引入 BattleSimulator 组件，我们成功地：
- 消除了战斗模拟逻辑的重复
- 提高了代码的可维护性和可扩展性
- 保持了完全的向后兼容性
- 为未来的功能扩展奠定了良好的基础
