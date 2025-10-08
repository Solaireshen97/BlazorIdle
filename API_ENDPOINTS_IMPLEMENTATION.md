# 离线战斗API端点实现总结

## 概述

本次实现了离线战斗系统的第三步：**添加API端点**。实现了三个关键端点来支持离线战斗的检查、应用和心跳更新功能。

## 🆕 最新更新：离线暂停恢复功能

### 核心变更
- **新增 Paused 状态**: 任务可以被暂停而不是直接完成
- **新增暂停/恢复端点**: `/api/activity-plans/{id}/pause` 和 `/api/activity-plans/{id}/resume`
- **增强启动端点**: `/api/activity-plans/{id}/start` 现在支持恢复暂停的任务
- **自动暂停**: 玩家离线超过阈值时，任务自动暂停并保存进度
- **自动恢复**: 服务器重启或玩家上线时，暂停的任务可以自动恢复

详细说明请参考：[离线暂停恢复功能说明.md](./docs/离线暂停恢复功能说明.md)

## 实现的端点

### 1. GET /api/offline/check
**功能**: 检查角色离线时间并返回结算预览（不立即发放收益）

**请求参数**:
- `characterId` (query, Guid): 角色ID

**响应**:
```json
{
  "hasOfflineTime": true,
  "offlineSeconds": 7200.0,
  "hasRunningPlan": true,
  "settlement": {
    "characterId": "xxx",
    "planId": "xxx",
    "simulatedSeconds": 1200.0,
    "planCompleted": false,
    "gold": 500,
    "exp": 1000,
    "lootExpected": {},
    "lootSampled": {},
    "updatedExecutedSeconds": 1800.0,
    "dropMode": "expected"
  },
  "planCompleted": false,
  "nextPlanStarted": false,
  "nextPlanId": null
}
```

**特性**:
- 自动检测离线时长（基于LastSeenAtUtc）
- 使用OfflineFastForwardEngine进行快进模拟
- **实现无感继承效果**：从离线时刻的进度继续计算，而不是重新开始
- 如果计划在离线期间完成，自动尝试启动下一个待执行计划
- 仅返回预览结果，不实际发放收益

### 2. POST /api/offline/apply
**功能**: 应用离线结算，实际发放收益到角色

**请求体**:
```json
{
  "characterId": "xxx",
  "settlement": {
    "characterId": "xxx",
    "planId": "xxx",
    "simulatedSeconds": 1200.0,
    "planCompleted": false,
    "gold": 500,
    "exp": 1000,
    "lootExpected": {},
    "lootSampled": {},
    "updatedExecutedSeconds": 1800.0,
    "dropMode": "expected"
  }
}
```

**响应**:
```json
{
  "success": true
}
```

**特性**:
- 接收前端确认后调用
- 实际将金币和经验添加到角色
- 预留了物品发放接口（待背包系统完善）

### 3. POST /api/characters/{id}/heartbeat
**功能**: 更新角色心跳时间，标记角色在线

**路径参数**:
- `id` (Guid): 角色ID

**响应**:
```json
{
  "message": "心跳更新成功",
  "timestamp": "2025-01-17T10:30:00.000Z"
}
```

**特性**:
- 更新LastSeenAtUtc为当前时间
- 用于前端定期调用（建议每1-2分钟）
- 确保离线时间计算的准确性

## 服务注册改进

### DependencyInjection.cs 变化

1. **注册OfflineFastForwardEngine**:
```csharp
services.AddTransient<OfflineFastForwardEngine>();
```

2. **改进OfflineSettlementService注册**:
```csharp
services.AddTransient<OfflineSettlementService>(sp =>
{
    var characters = sp.GetRequiredService<ICharacterRepository>();
    var simulator = sp.GetRequiredService<BattleSimulator>();
    var plans = sp.GetRequiredService<IActivityPlanRepository>();
    var engine = sp.GetRequiredService<OfflineFastForwardEngine>();
    var db = sp.GetRequiredService<GameDbContext>();
    var planService = sp.GetRequiredService<ActivityPlanService>();
    
    // 传递 ActivityPlanService 的 TryStartNextPendingPlanAsync 方法作为委托
    return new OfflineSettlementService(
        characters,
        simulator,
        plans,
        engine,
        db,
        planService.TryStartNextPendingPlanAsync
    );
});
```

**关键点**:
- 注入ActivityPlanService的`TryStartNextPendingPlanAsync`方法
- 实现自动计划衔接：当离线期间完成一个计划时，自动启动下一个

## 无感继承效果实现

### 核心逻辑流程

1. **用户离线**：
   - 假设正在打副本，已经执行了30分钟（ExecutedSeconds = 1800）
   - 离线时LastSeenAtUtc被记录

2. **用户上线并调用check端点**：
   - 计算离线时长：`offlineSeconds = DateTime.UtcNow - LastSeenAtUtc`
   - 获取正在运行的计划（包含ExecutedSeconds和BattleStateJson快照）
   - 调用OfflineFastForwardEngine.FastForward()

3. **快进引擎处理**：
   - 计算剩余时长：`remaining = plan.LimitValue - plan.ExecutedSeconds`
   - 从BattleStateJson恢复之前的战斗状态（敌人血量、波次等）
   - 模拟剩余时长的战斗
   - 累加ExecutedSeconds：`plan.ExecutedSeconds += simulatedSeconds`
   - 保存新的BattleStateJson快照

4. **在线继承**：
   - 如果用户在计划未完成时回到在线状态
   - ActivityPlanService的在线战斗会从保存的BattleStateJson恢复
   - 继续从离线结算的进度开始执行

### 示例场景

**场景1: 打到一半离线**
- 计划: 1小时 (3600秒)
- 在线执行: 30分钟 (1800秒)
- 离线时长: 20分钟 (1200秒)
- **结果**: 离线模拟20分钟，累计进度50分钟，计划未完成
- 上线后: 继续从50分钟进度开始，还剩10分钟完成

**场景2: 离线期间完成**
- 计划: 40分钟 (2400秒)
- 在线执行: 20分钟 (1200秒)
- 离线时长: 30分钟 (1800秒)
- **结果**: 仅模拟剩余20分钟，计划标记为完成，自动启动下一个计划
- 上线后: 新计划已在运行中

## 代码修改清单

### 修改的文件

1. **BlazorIdle.Server/Api/OfflineController.cs**
   - 新增 `CheckOffline` 方法 (GET /api/offline/check)
   - 新增 `ApplySettlement` 方法 (POST /api/offline/apply)
   - 新增 `ApplySettlementRequest` 记录类型

2. **BlazorIdle.Server/Api/CharactersController.cs**
   - 新增 `Heartbeat` 方法 (POST /api/characters/{id}/heartbeat)

3. **BlazorIdle.Server/Infrastructure/DependencyInjection.cs**
   - 注册 `OfflineFastForwardEngine`
   - 改进 `OfflineSettlementService` 注册（注入计划服务委托）
   - 添加必要的using指令

### 代码特点

- **最小化修改**: 仅添加必要的端点和注册逻辑
- **遵循现有风格**: 使用相同的命名约定和代码结构
- **错误处理**: 所有端点都包含适当的异常处理
- **文档注释**: 添加了XML文档注释说明端点功能

## 测试验证

### 现有测试
- ✅ OfflineSettlementServiceTests (4个测试全部通过)
- ✅ OfflineFastForwardEngineTests (所有测试通过)

### 编译验证
- ✅ 解决方案编译无错误
- ✅ 所有警告已处理或确认无影响

## 使用示例

### 前端集成流程

```typescript
// 1. 用户登录后检查离线收益
async function checkOfflineRewards(characterId: string) {
  const response = await fetch(`/api/offline/check?characterId=${characterId}`);
  const result = await response.json();
  
  if (result.hasOfflineTime && result.settlement) {
    // 显示离线收益弹窗
    showOfflineRewardsDialog(result);
  }
}

// 2. 用户确认后应用收益
async function applyOfflineRewards(characterId: string, settlement: any) {
  const response = await fetch('/api/offline/apply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ characterId, settlement })
  });
  
  if (response.ok) {
    console.log('离线收益已发放');
  }
}

// 3. 定期发送心跳
setInterval(async () => {
  await fetch(`/api/characters/${characterId}/heartbeat`, {
    method: 'POST'
  });
}, 60000); // 每分钟一次
```

## 🆕 活动计划暂停/恢复端点

### 4. POST /api/activity-plans/{id}/pause
**功能**: 暂停正在运行的活动计划（保存进度以便恢复）

**路径参数**:
- `id` (Guid): 活动计划ID

**响应**:
```json
{
  "planId": "xxx",
  "paused": true
}
```

**特性**:
- 保存当前战斗状态快照（`BattleStateJson`）
- 更新已执行时长（`ExecutedSeconds`）
- 停止战斗引擎释放内存
- 设置状态为 `Paused`（区别于 `Completed`）
- 可以稍后恢复执行

**使用场景**:
- 手动暂停任务（虽然通常由后台服务自动处理）
- 临时停止任务但不想标记为完成

### 5. POST /api/activity-plans/{id}/resume
**功能**: 恢复暂停的活动计划

**路径参数**:
- `id` (Guid): 活动计划ID

**响应**:
```json
{
  "planId": "xxx",
  "battleId": "xxx",
  "resumed": true
}
```

**特性**:
- 从保存的战斗状态快照恢复
- 继承之前的执行进度
- 无缝继续战斗（不重新开始）
- 内部调用 `StartPlanAsync`，支持 `Paused` 状态

**注意**: `/api/activity-plans/{id}/start` 端点现在也支持恢复暂停的任务，`/resume` 端点提供更清晰的语义。

### 6. 增强的 POST /api/activity-plans/{id}/start
**功能**: 启动活动计划（支持新任务和恢复暂停的任务）

**路径参数**:
- `id` (Guid): 活动计划ID

**响应**:
```json
{
  "planId": "xxx",
  "battleId": "xxx"
}
```

**特性**:
- 支持启动 `Pending` 状态的新任务
- 支持恢复 `Paused` 状态的暂停任务
- 从 `BattleStateJson` 恢复战斗进度
- 继承 `ExecutedSeconds` 和战斗状态

**状态转换**:
- `Pending` → `Running` (新任务)
- `Paused` → `Running` (恢复任务)

## 后续工作

- [x] 实现暂停/恢复机制 ✅
- [x] 添加服务器重启后的任务恢复 ✅
- [ ] 前端集成（创建离线结算弹窗组件）
- [ ] 添加离线历史记录端点（可选）
- [ ] 完善物品发放逻辑（待背包系统完善）
- [ ] 性能优化（如有需要）

## 技术亮点

1. **无感继承**: 离线和在线战斗完全连续，用户体验流畅
2. **暂停而非停止**: 离线时暂停任务而不是标记完成，允许恢复
3. **自动计划衔接**: 计划完成后自动启动下一个，无需手动操作
4. **依赖注入优化**: 通过委托传递实现松耦合
5. **状态快照**: 使用BattleStateJson保存战斗状态，支持精确继承
6. **服务器重启恢复**: 自动恢复服务器重启前暂停的任务
7. **错误处理**: 完善的异常处理和错误响应

## 总结

本次实现完成了离线战斗系统的关键API层，实现了文档中要求的所有核心功能：

✅ 检查离线时间和收益预览
✅ 应用离线结算发放收益
✅ 心跳更新标记在线状态
✅ 无感继承效果（从离线进度继续）
✅ 自动计划衔接
✅ 服务正确注册和依赖注入
✅ **新增：离线任务暂停/恢复机制**
✅ **新增：服务器重启后任务恢复**
✅ **新增：手动暂停/恢复API端点**

代码风格与现有项目保持一致，修改最小化，测试验证通过。
