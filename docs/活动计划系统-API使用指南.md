# 活动计划系统 API 使用指南

## 快速开始

活动计划系统通过 RESTful API 提供完整的活动管理功能。本指南将帮助你快速上手。

## API 端点概览

| 端点 | 方法 | 功能 | 说明 |
|------|------|------|------|
| `/api/activities/plans` | POST | 创建活动计划 | 将活动添加到指定槽位 |
| `/api/activities/plans/{planId}` | GET | 获取计划详情 | 查询单个计划的状态和进度 |
| `/api/activities/characters/{characterId}/slots` | GET | 获取槽位信息 | 查询角色所有槽位和队列 |
| `/api/activities/plans/{planId}/cancel` | POST | 取消活动计划 | 停止正在执行或待定的计划 |

## 1. 创建活动计划

### 端点
```
POST /api/activities/plans
```

### 请求体

```json
{
  "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "slotIndex": 0,
  "type": "combat",
  "limitType": "duration",
  "limitValue": 3600,
  "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"duration\"}"
}
```

### 参数说明

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| characterId | string (guid) | ✅ | 角色ID |
| slotIndex | int | ✅ | 槽位索引（0-2，默认3个槽位） |
| type | string | ✅ | 活动类型："combat"/"gather"/"craft" |
| limitType | string | ✅ | 限制类型："duration"/"count"/"infinite" |
| limitValue | number | ❌ | 限制值（duration:秒数，count:数量） |
| payloadJson | string | ✅ | 活动特定参数（JSON字符串） |

### 活动类型说明

#### combat - 战斗活动

**payloadJson 格式**：

```json
{
  "enemyId": "dummy",           // 敌人ID（必填）
  "enemyCount": 1,              // 敌人数量（可选，默认1）
  "mode": "duration",           // 模式：duration/continuous/dungeon/dungeonloop
  "dungeonId": null,            // 副本ID（dungeon模式必填）
  "respawnDelay": null,         // 刷新延迟（continuous模式，可选）
  "waveDelay": null,            // 波次延迟（dungeon模式，可选）
  "runDelay": null,             // 轮次延迟（dungeonloop模式，可选）
  "seed": null                  // 随机种子（可选，自动生成）
}
```

**战斗模式说明**：

| 模式 | 说明 | 适用场景 |
|------|------|---------|
| duration | 固定时长战斗 | 限时挂机 |
| continuous | 持续战斗（敌人死亡后自动刷新） | 无限挂机、击杀计数 |
| dungeon | 单次副本 | 通关副本一次 |
| dungeonloop | 循环副本 | 刷副本 |

#### gather - 采集活动（未来扩展）

```json
{
  "nodeId": "iron_ore",         // 采集节点ID
  "gatherSpeed": 1.0            // 采集速度倍率
}
```

#### craft - 制作活动（未来扩展）

```json
{
  "recipeId": "iron_sword",     // 配方ID
  "quantity": 10                // 制作数量
}
```

### 限制类型说明

#### duration - 时长限制

按模拟时间（秒）限制活动执行时长。

```json
{
  "limitType": "duration",
  "limitValue": 3600  // 3600秒 = 1小时
}
```

适用场景：
- 挂机1小时
- 定时采集
- 限时战斗

#### count - 计数限制

按完成次数限制（击杀数、采集次数、制作数量等）。

```json
{
  "limitType": "count",
  "limitValue": 100  // 100次
}
```

适用场景：
- 击杀100个敌人
- 采集50个矿石
- 制作20把武器

#### infinite - 无限制

需要手动停止的活动。

```json
{
  "limitType": "infinite"
  // limitValue 会被忽略
}
```

适用场景：
- 长期挂机
- 直到手动停止

### 响应示例

```json
{
  "id": "a1b2c3d4-e5f6-4789-a012-3456789abcde",
  "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "slotIndex": 0,
  "type": "combat",
  "state": "pending",
  "limitType": "duration",
  "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"duration\"}",
  "createdAtUtc": "2025-01-09T10:30:00Z",
  "startedAtUtc": null,
  "endedAtUtc": null,
  "progress": {
    "simulatedSeconds": 0,
    "completedCount": 0
  }
}
```

### 状态说明

| State | 说明 | 何时出现 |
|-------|------|---------|
| pending | 待定 | 刚创建或在队列中等待 |
| running | 运行中 | 正在执行 |
| completed | 已完成 | 达到限制条件 |
| cancelled | 已取消 | 被用户或系统取消 |

## 2. 获取计划详情

### 端点
```
GET /api/activities/plans/{planId}
```

### 路径参数

| 参数 | 类型 | 说明 |
|------|------|------|
| planId | string (guid) | 活动计划ID |

### 响应示例

```json
{
  "id": "a1b2c3d4-e5f6-4789-a012-3456789abcde",
  "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "slotIndex": 0,
  "type": "combat",
  "state": "running",
  "limitType": "duration",
  "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"duration\"}",
  "createdAtUtc": "2025-01-09T10:30:00Z",
  "startedAtUtc": "2025-01-09T10:30:05Z",
  "endedAtUtc": null,
  "progress": {
    "simulatedSeconds": 150.5,
    "completedCount": 25
  }
}
```

## 3. 获取角色槽位信息

### 端点
```
GET /api/activities/characters/{characterId}/slots
```

### 路径参数

| 参数 | 类型 | 说明 |
|------|------|------|
| characterId | string (guid) | 角色ID |

### 响应示例

```json
[
  {
    "slotIndex": 0,
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "currentPlan": {
      "id": "a1b2c3d4-e5f6-4789-a012-3456789abcde",
      "state": "running",
      "type": "combat",
      "limitType": "duration",
      "progress": {
        "simulatedSeconds": 150.5,
        "completedCount": 25
      }
    },
    "queuedPlans": [
      {
        "id": "b2c3d4e5-f6a7-4890-b123-456789abcdef",
        "state": "pending",
        "type": "combat",
        "limitType": "count"
      }
    ]
  },
  {
    "slotIndex": 1,
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "currentPlan": null,
    "queuedPlans": []
  },
  {
    "slotIndex": 2,
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "currentPlan": null,
    "queuedPlans": []
  }
]
```

## 4. 取消活动计划

### 端点
```
POST /api/activities/plans/{planId}/cancel
```

### 路径参数

| 参数 | 类型 | 说明 |
|------|------|------|
| planId | string (guid) | 活动计划ID |

### 响应示例

```json
{
  "success": true
}
```

### 注意事项

1. 取消正在运行的计划会立即停止底层执行（如战斗）
2. 取消队列中的计划会从队列移除
3. 已完成的计划无法取消（会返回错误）
4. 取消当前计划后，队列中的下一个计划会自动开始

## 实战示例

### 示例1：先打1小时A怪，再打1小时B怪

```bash
# 1. 创建第一个计划：打1小时dummy
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "duration",
    "limitValue": 3600,
    "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"duration\"}"
  }'

# 2. 创建第二个计划：打1小时tank（会自动进入队列）
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "duration",
    "limitValue": 3600,
    "payloadJson": "{\"enemyId\":\"tank\",\"enemyCount\":1,\"mode\":\"duration\"}"
  }'

# 3. 查询槽位状态
curl http://localhost:5000/api/activities/characters/3fa85f64-5717-4562-b3fc-2c963f66afa6/slots
```

### 示例2：击杀100个敌人

```bash
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "count",
    "limitValue": 100,
    "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"continuous\",\"respawnDelay\":3.0}"
  }'
```

### 示例3：刷副本10次

```bash
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "count",
    "limitValue": 10,
    "payloadJson": "{\"mode\":\"dungeonloop\",\"dungeonId\":\"intro_cave\"}"
  }'
```

### 示例4：多槽位并行

```bash
# 槽位0：打dummy
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "duration",
    "limitValue": 3600,
    "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1}"
  }'

# 槽位1：采集（未来功能）
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 1,
    "type": "gather",
    "limitType": "count",
    "limitValue": 50,
    "payloadJson": "{\"nodeId\":\"iron_ore\"}"
  }'

# 槽位2：制作（未来功能）
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 2,
    "type": "craft",
    "limitType": "count",
    "limitValue": 10,
    "payloadJson": "{\"recipeId\":\"iron_sword\"}"
  }'
```

## 错误处理

### 常见错误

#### 1. 槽位索引无效

```json
{
  "error": "Invalid slot index: 5. Must be 0-2"
}
```

**解决方案**：使用有效的槽位索引（0-2，默认3个槽位）

#### 2. 角色不存在

```json
{
  "error": "Character {characterId} not found"
}
```

**解决方案**：确认角色ID正确

#### 3. 计划不存在

```json
{
  "title": "Not Found",
  "status": 404
}
```

**解决方案**：确认计划ID正确

#### 4. 无法取消已完成的计划

```json
{
  "error": "Cannot cancel completed activity"
}
```

**解决方案**：只能取消 pending 或 running 状态的计划

## 前端集成建议

### 1. 轮询更新

```javascript
// 每2秒查询一次槽位状态
setInterval(async () => {
  const response = await fetch(`/api/activities/characters/${characterId}/slots`);
  const slots = await response.json();
  updateUI(slots);
}, 2000);
```

### 2. 进度展示

```javascript
function calculateProgress(plan) {
  if (plan.limitType === 'duration') {
    const progress = plan.progress.simulatedSeconds / plan.limitValue;
    return Math.min(100, progress * 100);
  } else if (plan.limitType === 'count') {
    const progress = plan.progress.completedCount / plan.limitValue;
    return Math.min(100, progress * 100);
  }
  return 0; // infinite 类型无法计算进度
}
```

### 3. 状态图标

```javascript
const stateIcons = {
  pending: '⏳',
  running: '▶️',
  completed: '✅',
  cancelled: '❌'
};
```

## 性能优化建议

1. **批量查询**：使用槽位查询接口一次获取所有信息，而不是逐个查询计划
2. **缓存**：对于不变的信息（如 payloadJson）可以在客户端缓存
3. **增量更新**：只查询和更新变化的槽位
4. **WebSocket**：未来可以考虑使用 WebSocket 推送状态变更

## 配置项

在 `appsettings.json` 中可以配置：

```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 1.0,    // 活动推进间隔
    "PruneIntervalMinutes": 10.0,     // 清理间隔
    "SlotsPerCharacter": 3            // 每个角色的槽位数量
  }
}
```

## 总结

活动计划系统提供了完整的 RESTful API，支持：

✅ 灵活的活动类型（战斗、采集、制作）  
✅ 多种限制条件（时长、计数、无限）  
✅ 队列管理和自动衔接  
✅ 多槽位并行执行  
✅ 实时状态查询和取消  

通过这些 API，你可以轻松构建复杂的挂机和自动化玩法。
