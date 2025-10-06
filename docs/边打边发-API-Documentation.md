# 边打边发 - API 文档

## 概述

本文档说明边打边发系统提供的核心 API 接口。

## IRewardGrantService

奖励发放服务的核心接口。

### 命名空间
```csharp
BlazorIdle.Server.Application.Abstractions
```

### 依赖注入
```csharp
// 注册方式（已在 ApplicationDI 中自动注册）
services.AddScoped<IRewardGrantService, RewardGrantService>();

// 使用方式
public class MyService
{
    private readonly IRewardGrantService _rewardService;
    
    public MyService(IRewardGrantService rewardService)
    {
        _rewardService = rewardService;
    }
}
```

## 方法详解

### GrantRewardsAsync

发放奖励给指定角色，带幂等性保护。

#### 签名
```csharp
Task<bool> GrantRewardsAsync(
    Guid characterId,
    long gold,
    long exp,
    Dictionary<string, int> items,
    string idempotencyKey,
    string eventType,
    Guid? battleId = null,
    CancellationToken ct = default)
```

#### 参数

| 参数名 | 类型 | 必需 | 说明 |
|--------|------|------|------|
| characterId | Guid | 是 | 角色ID |
| gold | long | 是 | 金币数量（可以为0） |
| exp | long | 是 | 经验数量（可以为0） |
| items | Dictionary<string, int> | 是 | 物品字典，key=物品ID, value=数量 |
| idempotencyKey | string | 是 | 幂等键，确保不重复发放 |
| eventType | string | 是 | 事件类型，如 "battle_periodic_reward" |
| battleId | Guid? | 否 | 关联的战斗ID |
| ct | CancellationToken | 否 | 取消令牌 |

#### 返回值
- `true`: 奖励成功发放
- `false`: 奖励已经发放过（幂等性拦截）

#### 异常
- `InvalidOperationException`: 角色不存在
- `DbUpdateException`: 数据库更新失败
- `OperationCanceledException`: 操作被取消

#### 示例

**基本用法**:
```csharp
var items = new Dictionary<string, int>
{
    ["item_sword_1"] = 1,
    ["item_potion_hp"] = 5
};

var granted = await _rewardService.GrantRewardsAsync(
    characterId: characterId,
    gold: 100,
    exp: 50,
    items: items,
    idempotencyKey: "battle:123:periodic:sim10.0:seg0-2",
    eventType: "battle_periodic_reward",
    battleId: battleId
);

if (granted)
{
    Console.WriteLine("Rewards granted successfully");
}
else
{
    Console.WriteLine("Rewards already granted (idempotent)");
}
```

**空奖励**:
```csharp
// 只发放金币，不发放物品
var granted = await _rewardService.GrantRewardsAsync(
    characterId: characterId,
    gold: 100,
    exp: 0,
    items: new Dictionary<string, int>(),
    idempotencyKey: "battle:123:gold_only",
    eventType: "battle_reward"
);
```

### IsAlreadyGrantedAsync

检查指定的幂等键是否已经发放过奖励。

#### 签名
```csharp
Task<bool> IsAlreadyGrantedAsync(
    string idempotencyKey,
    CancellationToken ct = default)
```

#### 参数
| 参数名 | 类型 | 必需 | 说明 |
|--------|------|------|------|
| idempotencyKey | string | 是 | 幂等键 |
| ct | CancellationToken | 否 | 取消令牌 |

#### 返回值
- `true`: 已经发放过
- `false`: 尚未发放

#### 示例
```csharp
var key = "battle:123:periodic:sim10.0:seg0-2";
var alreadyGranted = await _rewardService.IsAlreadyGrantedAsync(key);

if (alreadyGranted)
{
    Console.WriteLine("This reward has already been granted");
}
```

## 幂等键规范

### 格式约定
```
{prefix}:{resource}:{type}:{identifier}
```

### 示例

**战斗周期性奖励**:
```
battle:{battleId}:periodic:sim{simTime}:seg{fromIndex}-{toIndex}
```

**战斗最终奖励**:
```
battle:{battleId}:final
```

**离线奖励**:
```
offline:{characterId}:{timestamp}
```

### 最佳实践
1. **唯一性**: 确保幂等键在全局范围内唯一
2. **可读性**: 幂等键应该包含足够的信息用于调试
3. **长度限制**: 不超过 200 字符
4. **特殊字符**: 避免使用空格和特殊字符，使用 `:` 分隔各部分

## 事件类型规范

### 标准事件类型
| 事件类型 | 说明 | 使用场景 |
|---------|------|---------|
| battle_periodic_reward | 战斗周期性奖励 | Step战斗中周期发放 |
| battle_final_reward | 战斗最终奖励 | 战斗结束时发放 |
| dungeon_completion_reward | 副本完成奖励 | 完成副本时发放 |
| offline_reward | 离线奖励 | 玩家离线期间的奖励 |
| quest_reward | 任务奖励 | 完成任务时发放 |
| achievement_reward | 成就奖励 | 达成成就时发放 |

### 自定义事件类型
可以定义自己的事件类型，建议遵循以下命名规范：
- 使用小写字母
- 使用下划线分隔单词
- 格式: `{系统}_{动作}_reward`

## 错误处理

### 常见错误场景

#### 1. 角色不存在
```csharp
try
{
    await _rewardService.GrantRewardsAsync(...);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
{
    // 角色不存在
    _logger.LogWarning("Character {CharacterId} not found", characterId);
}
```

#### 2. 数据库并发冲突
```csharp
try
{
    await _rewardService.GrantRewardsAsync(...);
}
catch (DbUpdateConcurrencyException)
{
    // 并发冲突，可以重试
    _logger.LogWarning("Concurrency conflict, retrying...");
    await Task.Delay(100);
    // 重试逻辑
}
```

#### 3. 幂等键冲突
```csharp
var granted = await _rewardService.GrantRewardsAsync(...);
if (!granted)
{
    // 幂等性拦截，这是正常情况
    _logger.LogDebug("Reward already granted, idempotent");
}
```

## 性能优化建议

### 1. 批量发放
如果需要给多个角色发放奖励，考虑并行处理：

```csharp
var tasks = characterIds.Select(async cid =>
{
    await _rewardService.GrantRewardsAsync(
        characterId: cid,
        gold: 100,
        exp: 50,
        items: items,
        idempotencyKey: $"batch:{batchId}:char:{cid}",
        eventType: "batch_reward"
    );
});

await Task.WhenAll(tasks);
```

### 2. 预检查幂等性
对于已知可能重复的场景，先检查幂等性：

```csharp
if (!await _rewardService.IsAlreadyGrantedAsync(key))
{
    // 只在未发放时才调用完整的发放逻辑
    await _rewardService.GrantRewardsAsync(...);
}
```

### 3. 使用取消令牌
支持优雅取消：

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try
{
    await _rewardService.GrantRewardsAsync(..., ct: cts.Token);
}
catch (OperationCanceledException)
{
    _logger.LogWarning("Reward grant timed out");
}
```

## 集成示例

### 在 StepBattleCoordinator 中使用
```csharp
private void TryFlushPeriodicRewards(RunningBattle rb, CancellationToken ct)
{
    // ... 计算奖励
    
    var idempotencyKey = $"battle:{rb.Id}:periodic:sim{currentSimTime:F2}:seg{fromIndex}-{toIndex}";
    
    var granted = _rewardService.GrantRewardsAsync(
        rb.CharacterId,
        reward.Gold,
        reward.Exp,
        reward.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value)),
        idempotencyKey,
        "battle_periodic_reward",
        rb.Id,
        ct
    ).GetAwaiter().GetResult();
    
    if (granted)
    {
        // 更新发放状态
        rb.LastRewardFlushSimTime = currentSimTime;
    }
}
```

## 测试建议

### 单元测试
```csharp
[Fact]
public async Task GrantRewardsAsync_ShouldBeIdempotent()
{
    // Arrange
    var service = CreateService();
    var items = new Dictionary<string, int> { ["item1"] = 1 };
    
    // Act
    var first = await service.GrantRewardsAsync(
        characterId, 100, 50, items, "test:key1", "test");
    var second = await service.GrantRewardsAsync(
        characterId, 100, 50, items, "test:key1", "test");
    
    // Assert
    Assert.True(first);
    Assert.False(second); // 第二次应该被幂等性拦截
}
```

### 集成测试
```csharp
[Fact]
public async Task GrantRewards_ShouldUpdateDatabase()
{
    // Arrange
    using var context = CreateDbContext();
    var service = new RewardGrantService(context, logger);
    
    // Act
    await service.GrantRewardsAsync(...);
    
    // Assert
    var character = await context.Characters.FindAsync(characterId);
    Assert.Equal(100, character.Gold);
    
    var economyEvent = await context.EconomyEvents
        .FirstOrDefaultAsync(e => e.IdempotencyKey == "test:key");
    Assert.NotNull(economyEvent);
}
```

## 总结

IRewardGrantService 提供了一个健壮、幂等的奖励发放机制。关键要点：
1. 始终提供唯一的幂等键
2. 正确处理返回值和异常
3. 使用合适的事件类型
4. 考虑性能和并发场景
