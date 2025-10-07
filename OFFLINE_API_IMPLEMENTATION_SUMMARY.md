# 离线战斗 API 端点实现总结

## 概述

本次实现完成了离线战斗系统的第三步：**添加 API 端点**。这是基于已完成的第一步（OfflineFastForwardEngine）和第二步（扩展的 OfflineSettlementService）的集成工作。

## 实现的功能

### 1. API 端点

#### `GET /api/offline/check`
**功能**: 检查离线收益（用户登录时自动调用）

**参数**:
- `characterId` (Guid, Query): 角色 ID

**返回**: `OfflineCheckResult`
```csharp
{
    "hasOfflineTime": true,
    "offlineSeconds": 3600.0,
    "hasRunningPlan": true,
    "settlement": {
        "simulatedSeconds": 1200.0,
        "gold": 500,
        "exp": 1000,
        "totalKills": 10,
        "totalDamage": 5000,
        "planCompleted": false,
        "battleStateJson": "..." // 用于在线继承
    },
    "planCompleted": false,
    "nextPlanStarted": false,
    "nextPlanId": null
}
```

**行为**:
1. 计算角色离线时长（基于 `LastSeenAtUtc`）
2. 查找离线时正在运行的计划
3. 使用 `OfflineFastForwardEngine` 快进模拟离线战斗
4. 更新计划状态（ExecutedSeconds、BattleStateJson）
5. 如果计划完成，自动启动下一个待执行的计划
6. 返回结算结果（不立即发放收益）

#### `POST /api/offline/apply`
**功能**: 应用离线结算，实际发放收益到角色（前端确认后调用）

**请求体**: `ApplySettlementRequest`
```csharp
{
    "characterId": "...",
    "settlement": {
        "gold": 500,
        "exp": 1000,
        // ... 其他结算数据
    }
}
```

**返回**: `200 OK` 或 `404 Not Found`

**行为**:
1. 查找角色
2. 发放金币和经验
3. 持久化角色更新
4. （预留）发放物品到背包

### 2. 依赖注入集成

**文件**: `BlazorIdle.Server/Infrastructure/DependencyInjection.cs`

**改进**:
1. 注册 `OfflineFastForwardEngine` 为 Singleton（无状态，可安全共享）
2. 升级 `OfflineSettlementService` 注册，注入 `TryStartNextPendingPlanAsync` 回调
   - 实现自动计划衔接功能
   - 当离线结算完成一个计划时，自动启动下一个

```csharp
services.AddTransient<OfflineSettlementService>(sp =>
{
    // ... 获取依赖 ...
    
    // 创建回调函数：尝试启动下一个待执行的计划
    async Task<ActivityPlan?> TryStartNextPlan(Guid characterId, CancellationToken ct)
    {
        var planService = sp.GetRequiredService<ActivityPlanService>();
        return await planService.TryStartNextPendingPlanAsync(characterId, ct);
    }
    
    return new OfflineSettlementService(..., TryStartNextPlan);
});
```

### 3. ApplySettlementAsync 优化

**文件**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`

**改进**:
- 改用 `ICharacterRepository.GetAsync()` 查找角色（而非直接使用 DbContext）
- 保持与现有代码风格一致
- 提高可测试性

## 关键设计要点

### 1. 无感继承效果 ✅

**需求**: 副本打到一半进入离线，应该从离线时刻的进度开始继续计算，而不是开一个新的。

**实现**:
- `ActivityPlan.ExecutedSeconds`: 记录已执行的时长
- `ActivityPlan.BattleStateJson`: 保存战斗状态快照（敌人血量、波次等）
- `OfflineFastForwardEngine.FastForward()`: 从 `ExecutedSeconds` 开始继续模拟

```csharp
// 计算剩余时长（考虑已执行时长）
var remainingSeconds = plan.LimitValue.Value - plan.ExecutedSeconds;
var simulateSeconds = Math.Min(offlineSeconds, remainingSeconds);

// 更新累计执行时长
plan.ExecutedSeconds += simulateSeconds;
```

### 2. 自动计划衔接 ✅

**需求**: 如果离线结算完成了一个计划，应该自动启动下一个待执行的计划。

**实现**:
- `OfflineSettlementService` 接收 `TryStartNextPlanAsync` 回调
- 当 `result.PlanCompleted == true` 时，调用回调启动下一个计划
- 返回 `NextPlanStarted` 和 `NextPlanId` 告知前端

```csharp
if (result.PlanCompleted && _tryStartNextPlan is not null)
{
    var nextPlan = await _tryStartNextPlan(characterId, ct);
    if (nextPlan is not null)
    {
        nextPlanId = nextPlan.Id;
        nextPlanStarted = true;
    }
}
```

### 3. 在线继承离线结果 ✅

**需求**: 上线时如果计划没完成，应该让在线的计算能够继承离线计算的结果。

**实现**:
- 离线结算保存 `BattleStateJson` 到计划
- `ActivityPlanService.StartPlanAsync()` 从 `BattleStateJson` 恢复战斗状态
- 传递给 `StepBattleCoordinator.Start()` 的 `battleState` 参数

```csharp
// ActivityPlanService.StartPlanAsync()
Battles.Offline.BattleState? battleState = null;
if (!string.IsNullOrWhiteSpace(plan.BattleStateJson))
{
    battleState = JsonSerializer.Deserialize<BattleState>(plan.BattleStateJson);
}

// 传递给战斗引擎
_coordinator.Start(..., battleState: battleState);
```

## 单元测试

**文件**: `tests/BlazorIdle.Tests/OfflineControllerTests.cs`

**覆盖场景**:
1. ✅ 角色刚刚在线（无离线时间）
2. ✅ 有离线时间但无运行中的计划
3. ✅ 有离线时间和运行中的计划（验证结算计算）
4. ✅ 应用结算并验证收益发放
5. ✅ 错误处理（角色不存在）

**测试结果**: 所有 6 个测试通过 ✅

```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## 代码风格

### 遵循的原则
1. ✅ **最小化修改**: 只修改必要的部分
2. ✅ **保持一致性**: 使用现有的代码模式和命名约定
3. ✅ **单一职责**: API 控制器只负责 HTTP 请求/响应，业务逻辑在 Service 层
4. ✅ **依赖注入**: 使用构造函数注入，遵循 ASP.NET Core 最佳实践
5. ✅ **异步编程**: 所有 I/O 操作使用 async/await

### 命名约定
- 方法名: `CheckOffline`, `ApplySettlement`（动词开头，清晰表达意图）
- 参数名: `characterId`, `settlement`（小驼峰）
- 类型名: `OfflineCheckResult`, `ApplySettlementRequest`（大驼峰）

## API 使用示例

### 前端集成流程

```javascript
// 1. 用户登录或角色选择时，检查离线收益
async function checkOfflineRewards(characterId) {
    const response = await fetch(`/api/offline/check?characterId=${characterId}`);
    const result = await response.json();
    
    if (result.hasOfflineTime && result.hasRunningPlan) {
        // 显示离线结算弹窗
        showOfflineSettlementDialog(result);
    }
}

// 2. 用户确认后，应用离线结算
async function applyOfflineSettlement(characterId, settlement) {
    await fetch('/api/offline/apply', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ characterId, settlement })
    });
    
    // 刷新角色数据
    refreshCharacterData(characterId);
}
```

## 后续工作

### 已完成 ✅
1. OfflineFastForwardEngine（第一步）
2. OfflineSettlementService 扩展（第二步）
3. API 端点（第三步，本次实现）

### 待实现
1. **前端集成**（第四步）
   - 创建离线结算弹窗组件
   - 在 Characters 页面添加离线检查
   - 实现收益展示和领取功能

2. **心跳机制**（可选优化）
   - `POST /api/characters/{id}/heartbeat` 端点
   - 前端定期心跳（如每5分钟）
   - 页面卸载时更新心跳

3. **物品发放**（依赖背包系统）
   - 完善 `ApplySettlementAsync` 中的物品发放逻辑
   - 集成 InventoryService

## 测试验证

### 单元测试
```bash
# 运行离线控制器测试
dotnet test --filter "FullyQualifiedName~OfflineControllerTests"
# 结果: Passed: 6/6

# 运行所有离线相关测试
dotnet test --filter "FullyQualifiedName~Offline"
# 结果: Passed: 10/10 (包括 OfflineFastForwardEngine 和 OfflineSettlementService 测试)
```

### 集成测试（建议）
1. 启动服务器: `dotnet run --project BlazorIdle.Server`
2. 使用 Swagger UI 测试 API: `https://localhost:5001/swagger`
3. 测试场景:
   - 创建角色 → 创建战斗计划 → 等待一段时间 → 调用 `/api/offline/check`
   - 验证返回的结算结果
   - 调用 `/api/offline/apply` 应用结算
   - 验证角色的金币和经验是否正确增加

## 文档更新

相关文档已更新或创建:
- ✅ `OFFLINE_API_IMPLEMENTATION_SUMMARY.md`（本文档）
- ✅ 代码注释和 XML 文档注释
- 📋 待更新: `docs/OfflineBattleImplementationPlan.md`（标记第三步为完成）

## 总结

本次实现严格遵循文档要求和代码规范，成功完成了离线战斗系统的 API 端点集成：

1. ✅ **两个核心 API 端点**: check 和 apply
2. ✅ **自动计划衔接**: 通过依赖注入回调实现
3. ✅ **无感继承和在线恢复**: 利用 BattleStateJson
4. ✅ **完整的单元测试**: 6 个测试用例全部通过
5. ✅ **最小化改动**: 只修改了必要的 4 个文件
6. ✅ **保持代码风格**: 遵循现有的架构和命名约定

系统现在已经准备好进行前端集成（第四步），用户可以在前端看到离线收益弹窗并领取奖励。
