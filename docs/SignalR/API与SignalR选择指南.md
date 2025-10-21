# API 与 SignalR 选择指南

**文档版本**: 1.0  
**生成日期**: 2025年10月21日  
**状态**: 决策参考  
**目标**: 帮助开发人员快速判断何时使用API、何时使用SignalR

---

## 📚 目录

1. [快速决策树](#快速决策树)
2. [详细判断标准](#详细判断标准)
3. [典型场景分类](#典型场景分类)
4. [性能考量](#性能考量)
5. [实施建议](#实施建议)
6. [常见错误](#常见错误)

---

## 快速决策树

使用以下决策树快速判断：

```
需要实现某个功能
        │
        ▼
    需要服务器
    主动推送？
        │
   ┌────┴────┐
   │         │
  是        否
   │         │
   ▼         ▼
实时性要求   使用 API
< 2秒？      ────┐
   │              │
 ┌─┴─┐            │
 是  否           │
 │   │            │
 ▼   ▼            │
SignalR  可选     │
        SignalR   │
        或API     │
                  │
                  ▼
            查询型操作？
                  │
              ┌───┴───┐
              是      否
              │       │
              ▼       ▼
            API    操作型请求？
                       │
                   ┌───┴───┐
                   是      否
                   │       │
                   ▼       ▼
                  API   混合模式
                      (API+SignalR)
```

---

## 详细判断标准

### 标准1: 数据流向

| 特征 | API | SignalR |
|------|-----|---------|
| **客户端请求 → 服务器响应** | ✅ 推荐 | ❌ 不适合 |
| **服务器主动 → 客户端推送** | ❌ 不可能 | ✅ 推荐 |
| **双向实时通信** | ❌ 不适合 | ✅ 推荐 |

**示例**:

```typescript
// ✅ API适用：客户端主动查询
GET /api/character/info

// ✅ SignalR适用：服务器主动通知
connection.on("BattleEnded", (result) => { ... })

// ✅ SignalR适用：实时双向
connection.invoke("CastSkill", skillId);  // 客户端发送
connection.on("SkillCast", (data) => { ... });  // 服务器推送
```

---

### 标准2: 实时性要求

| 延迟容忍度 | 推荐方案 | 说明 |
|----------|---------|------|
| **< 200ms** | SignalR | 高频实时推送必需（战斗） |
| **200ms - 1秒** | SignalR | 实时反馈重要（技能释放） |
| **1秒 - 2秒** | SignalR或API | 根据其他因素判断 |
| **2秒 - 5秒** | API | 轮询或一次性查询即可 |
| **> 5秒** | API | 完全不需要实时性 |

**示例**:

```typescript
// ✅ SignalR：< 200ms（战斗帧更新）
connection.on("FrameTick", (frame) => {
    // 100-200ms 间隔的高频推送
});

// ✅ API：> 2秒（背包查询）
const items = await fetch('/api/inventory/items').then(r => r.json());
```

---

### 标准3: 数据更新频率

| 更新频率 | 推荐方案 | 说明 |
|---------|---------|------|
| **持续高频（> 5次/秒）** | SignalR | 战斗状态、进度条 |
| **定期中频（1-5次/秒）** | SignalR | 活动进度、资源采集 |
| **事件驱动（不定期）** | SignalR | 任务完成、物品获得 |
| **低频（< 1次/分钟）** | API | 背包、角色信息 |
| **几乎不变** | API（缓存） | 配方、技能定义 |

**示例**:

```typescript
// ✅ SignalR：持续高频（战斗）
connection.on("FrameTick", (frame) => {
    // 每秒5-10次
});

// ✅ SignalR：事件驱动（任务完成）
connection.on("QuestCompleted", (quest) => {
    // 不定期，但需要及时通知
});

// ✅ API：低频（查看背包）
button.onClick = async () => {
    const items = await getInventory();  // 用户点击时查询
};
```

---

### 标准4: 数据性质

| 数据类型 | 推荐方案 | 说明 |
|---------|---------|------|
| **状态变化** | SignalR | HP变化、Buff变化 |
| **事件通知** | SignalR | 战斗结束、物品获得 |
| **静态数据** | API | 技能列表、配方列表 |
| **历史数据** | API | 战斗记录、交易历史 |
| **计算结果** | API | 统计数据、排行榜 |

**示例**:

```typescript
// ✅ SignalR：状态变化
connection.on("HealthChanged", (hp) => {
    updateHealthBar(hp);
});

// ✅ API：静态数据（并缓存）
const recipes = await fetch('/api/recipes').then(r => r.json());
localStorage.setItem('recipes', JSON.stringify(recipes));

// ✅ API：历史数据
const history = await fetch('/api/battle/history?page=1').then(r => r.json());
```

---

### 标准5: 影响范围

| 影响范围 | 推荐方案 | 说明 |
|---------|---------|------|
| **单个用户** | API或SignalR | 根据实时性判断 |
| **多个用户（Group）** | SignalR | 组队、副本 |
| **所有用户（广播）** | SignalR | 系统公告 |

**示例**:

```typescript
// ✅ SignalR：多用户同步（队伍）
connection.on("PartyMemberJoined", (member) => {
    // 通知队伍所有成员
});

// ✅ SignalR：全局广播（系统公告）
connection.on("SystemAnnouncement", (message) => {
    showNotification(message);
});

// ✅ API：单用户查询
const myCharacter = await fetch('/api/character').then(r => r.json());
```

---

## 典型场景分类

### 🟢 100% 使用 API 的场景

#### 1. 静态数据查询

```typescript
// ✅ 配方列表
GET /api/crafting/recipes

// ✅ 技能定义
GET /api/skills/definitions

// ✅ 物品数据库
GET /api/items/database

// ✅ 地图信息
GET /api/maps/regions

// 建议：客户端缓存，减少请求
```

#### 2. 历史记录查询

```typescript
// ✅ 战斗历史
GET /api/battle/history?page=1&limit=20

// ✅ 交易记录
GET /api/economy/transactions?startDate=...

// ✅ 制作历史
GET /api/crafting/history

// ✅ 任务完成记录
GET /api/quests/completed
```

#### 3. 背包/仓库查询

```typescript
// ✅ 背包物品
GET /api/inventory/items

// ✅ 仓库物品
GET /api/inventory/storage

// ✅ 当前装备
GET /api/inventory/equipment

// ✅ 材料列表
GET /api/inventory/materials
```

#### 4. 操作型请求

```typescript
// ✅ 装备物品
POST /api/inventory/equip { itemId: "..." }

// ✅ 购买物品
POST /api/shop/buy { itemId: "...", quantity: 1 }

// ✅ 接受任务
POST /api/quests/accept { questId: "..." }

// ✅ 修改设置
PUT /api/settings { ... }

// 响应包含操作结果，不需要额外推送
```

---

### 🔵 100% 使用 SignalR 的场景

#### 1. 高频状态推送

```typescript
// ✅ 战斗帧推送（5-10Hz）
connection.on("FrameTick", (frame) => {
    updateBattleState(frame);
});

// ✅ 组队战斗同步
connection.on("PartyFrameTick", (frame) => {
    updatePartyBattleState(frame);
});
```

#### 2. 关键事件通知

```typescript
// ✅ 战斗结束
connection.on("BattleEnded", (result) => {
    showBattleResult(result);
});

// ✅ 活动完成
connection.on("ActivityCompleted", (activity) => {
    showActivityReward(activity);
});

// ✅ 稀有物品获得
connection.on("RareItemDropped", (item) => {
    playSpecialAnimation(item);
});
```

#### 3. 多用户同步

```typescript
// ✅ 队伍成员加入
connection.on("PartyMemberJoined", (member) => {
    addPartyMember(member);
});

// ✅ 队伍成员离开
connection.on("PartyMemberLeft", (memberId) => {
    removePartyMember(memberId);
});

// ✅ 掉落Roll点
connection.on("LootRolling", (loot) => {
    showRollInterface(loot);
});
```

#### 4. 服务器主动通知

```typescript
// ✅ 系统公告
connection.on("SystemAnnouncement", (message) => {
    showNotification(message);
});

// ✅ 服务器维护通知
connection.on("MaintenanceWarning", (info) => {
    showMaintenanceCountdown(info);
});

// ✅ 异常情况警告
connection.on("AnomalyDetected", (warning) => {
    showWarning(warning);
});
```

---

### 🟡 混合模式场景（API + SignalR）

#### 1. 活动队列

```typescript
// 步骤1: 首次加载用API
const queue = await fetch('/api/activity/queue').then(r => r.json());
displayQueue(queue);

// 步骤2: 后续更新用SignalR
connection.on("ActivityQueueChanged", (changes) => {
    updateQueueDisplay(changes);
});

connection.on("ActivityCompleted", (activity) => {
    removeFromQueue(activity.id);
    showCompletionNotification(activity);
});
```

**原因**: 首次加载完整数据用API效率更高，后续增量更新用SignalR实时性更好。

#### 2. 组队战斗

```typescript
// 步骤1: 创建队伍用API
const party = await fetch('/api/party/create', { method: 'POST' })
    .then(r => r.json());

// 步骤2: 订阅队伍更新
await connection.invoke("SubscribeToParty", party.id);

// 步骤3: 实时同步用SignalR
connection.on("PartyFrameTick", (frame) => {
    updatePartyBattle(frame);
});

// 步骤4: 查看设置用API
button.onClick = async () => {
    const settings = await fetch(`/api/party/${party.id}/settings`)
        .then(r => r.json());
    showSettingsDialog(settings);
};
```

**原因**: 创建操作用API，实时状态用SignalR，静态查询用API。

#### 3. 长时间制作

```typescript
// 步骤1: 开始制作用API
const crafting = await fetch('/api/crafting/start', {
    method: 'POST',
    body: JSON.stringify({ recipeId: 'legendary_sword' })
}).then(r => r.json());

// 步骤2: 进度更新用SignalR（可选）
connection.on("CraftingProgress", (progress) => {
    updateProgressBar(progress.percentage);
});

// 步骤3: 完成通知用SignalR
connection.on("CraftingCompleted", (result) => {
    showCompletionAnimation(result);
    playSound("success");
});
```

**原因**: 
- 快速制作（< 5秒）：API直接返回结果
- 中等制作（5秒-5分钟）：API启动 + SignalR完成通知
- 长时间制作（> 5分钟）：API启动 + SignalR进度 + 完成通知

---

## 性能考量

### API 性能优化

#### 1. 客户端缓存

```typescript
// ✅ 缓存静态数据
class RecipeCache {
    private cache: Recipe[] | null = null;
    private lastFetch: number = 0;
    private readonly TTL = 3600000; // 1小时

    async getRecipes(): Promise<Recipe[]> {
        const now = Date.now();
        if (this.cache && (now - this.lastFetch) < this.TTL) {
            return this.cache;  // 返回缓存
        }

        // 缓存过期，重新获取
        this.cache = await fetch('/api/recipes').then(r => r.json());
        this.lastFetch = now;
        return this.cache;
    }
}
```

#### 2. 分页查询

```typescript
// ✅ 大列表分页
async function getBattleHistory(page: number = 1, limit: number = 20) {
    return await fetch(`/api/battle/history?page=${page}&limit=${limit}`)
        .then(r => r.json());
}

// ❌ 避免一次性获取全部
// const allHistory = await fetch('/api/battle/history/all')  // 可能几万条
```

#### 3. 按需加载

```typescript
// ✅ 用户打开背包时才加载
async function openInventory() {
    showLoadingSpinner();
    const items = await fetch('/api/inventory/items').then(r => r.json());
    hideLoadingSpinner();
    displayItems(items);
}

// ❌ 避免启动时加载所有数据
```

---

### SignalR 性能优化

#### 1. 避免过度推送

```typescript
// ❌ 不推荐：每个金币变化都推送
connection.on("GoldChanged", (gold) => {
    updateGoldDisplay(gold);
});

// ✅ 推荐：在其他事件中包含金币变化
connection.on("BattleEnded", (result) => {
    updateGoldDisplay(result.gold);  // 战斗结束时一并更新
});

// ✅ 推荐：客户端本地计算
function buyItem(price: number) {
    localGold -= price;  // 客户端立即更新
    updateGoldDisplay(localGold);
    // 服务器验证后纠正（如果需要）
}
```

#### 2. 批量推送

```typescript
// ❌ 不推荐：逐个推送普通伤害
for (const damage of damages) {
    await connection.send("Damage", damage);  // 100次调用
}

// ✅ 推荐：聚合到FrameTick中
const frameTick = {
    totalDamage: damages.reduce((sum, d) => sum + d.amount, 0),
    hits: damages.length,
    // ...
};
await connection.send("FrameTick", frameTick);  // 1次调用
```

#### 3. 订阅管理

```typescript
// ✅ 进入战斗时订阅
async function enterBattle(battleId: string) {
    await connection.invoke("SubscribeToBattle", battleId);
}

// ✅ 离开战斗时取消订阅
async function exitBattle(battleId: string) {
    await connection.invoke("UnsubscribeFromBattle", battleId);
}

// 避免订阅不需要的消息
```

---

## 实施建议

### 新功能开发流程

```
1. 需求分析
   └─> 明确数据流向、实时性、更新频率

2. 技术选型
   └─> 使用本文档决策树判断

3. 接口设计
   ├─> API: 设计RESTful接口
   └─> SignalR: 设计消息类型和事件

4. 实现
   ├─> API: Controller + Service + Repository
   └─> SignalR: Event → Broadcaster → Dispatcher → Hub

5. 测试
   ├─> API: 单元测试 + 集成测试
   └─> SignalR: 单元测试 + 网络模拟测试

6. 监控
   ├─> API: 响应时间、错误率
   └─> SignalR: 推送延迟、连接数、消息队列深度
```

### 实施检查清单

#### API实施检查

- [ ] 接口符合RESTful规范
- [ ] 支持分页（大列表）
- [ ] 响应包含必要的元数据
- [ ] 实现了错误处理
- [ ] 添加了日志记录
- [ ] 编写了单元测试
- [ ] 更新了API文档

#### SignalR实施检查

- [ ] 定义了清晰的消息类型
- [ ] 实现了Broadcaster
- [ ] 订阅了领域事件
- [ ] 设置了合理的优先级
- [ ] 实现了错误处理
- [ ] 添加了日志记录
- [ ] 编写了单元测试
- [ ] 更新了消息文档

---

## 常见错误

### 错误1: 静态数据使用SignalR推送

```typescript
// ❌ 错误：技能列表不需要推送
connection.on("SkillListUpdated", (skills) => {
    updateSkillList(skills);
});

// ✅ 正确：技能列表用API查询并缓存
const skills = await fetch('/api/skills').then(r => r.json());
localStorage.setItem('skills', JSON.stringify(skills));
```

**原因**: 静态数据变化极少，推送浪费资源。

---

### 错误2: 高频查询使用API轮询

```typescript
// ❌ 错误：轮询获取战斗状态
setInterval(async () => {
    const battle = await fetch('/api/battle/current').then(r => r.json());
    updateBattleUI(battle);
}, 200);  // 每200ms查询一次

// ✅ 正确：使用SignalR推送
connection.on("FrameTick", (frame) => {
    updateBattleUI(frame);
});
```

**原因**: 
- 轮询延迟高（200ms-1秒）
- 浪费带宽（大量请求）
- 服务器压力大（N个客户端 × 5次/秒）

---

### 错误3: 操作请求使用SignalR

```typescript
// ❌ 错误：装备物品用SignalR
await connection.invoke("EquipItem", itemId);
// 如何获取结果？需要监听响应事件

// ✅ 正确：装备物品用API
const result = await fetch('/api/inventory/equip', {
    method: 'POST',
    body: JSON.stringify({ itemId })
}).then(r => r.json());

if (result.success) {
    updateEquipmentUI(result.equipment);
}
```

**原因**: 操作型请求需要同步响应结果，API更自然。

---

### 错误4: 推送非必要的增量更新

```typescript
// ❌ 错误：推送每次金币变化
connection.on("GoldChanged", (gold) => {
    updateGold(gold);  // 每次+1金币都推送？
});

// ✅ 正确：在关键事件中包含金币变化
connection.on("BattleEnded", (result) => {
    updateGold(result.totalGold);
    updateExp(result.totalExp);
    // ...
});
```

**原因**: 过度推送浪费带宽，客户端本地计算即可。

---

### 错误5: 混淆推送与查询

```typescript
// ❌ 错误：推送后还要查询
connection.on("ItemAcquired", async (notification) => {
    showNotification(notification);
    
    // 为什么还要查询？推送应该包含完整信息
    const item = await fetch(`/api/items/${notification.itemId}`)
        .then(r => r.json());
    displayItem(item);
});

// ✅ 正确：推送包含完整信息
connection.on("ItemAcquired", (item) => {
    showNotification(item.name);
    displayItem(item);  // 不需要额外查询
});
```

**原因**: 推送应该包含客户端需要的完整信息，避免额外查询。

---

## 决策速查表

| 场景 | API | SignalR | 说明 |
|------|:---:|:-------:|------|
| 战斗状态更新 | ❌ | ✅ | 高频实时推送 |
| 查看背包 | ✅ | ❌ | 低频查询 |
| 活动完成通知 | ❌ | ✅ | 事件驱动推送 |
| 查看配方列表 | ✅ | ❌ | 静态数据 |
| 组队成员加入 | ❌ | ✅ | 多用户同步 |
| 购买物品 | ✅ | ❌ | 操作型请求 |
| 制作完成通知 | ❌ | ✅ | 事件通知 |
| 查看历史记录 | ✅ | ❌ | 历史数据 |
| 长时间制作进度 | ❌ | ✅ | 定期推送 |
| 装备物品 | ✅ | ❌ | 操作型请求 |
| 稀有物品掉落 | ❌ | ✅ | 重要通知 |
| 查看角色信息 | ✅ | ❌ | 低频查询 |
| 系统公告 | ❌ | ✅ | 全局广播 |
| 修改设置 | ✅ | ❌ | 操作型请求 |

---

## 总结

### 核心原则

1. **API 用于查询和操作**
   - 客户端主动请求
   - 静态数据
   - 操作型请求

2. **SignalR 用于推送和通知**
   - 服务器主动推送
   - 状态变化
   - 事件通知

3. **混合模式用于复杂场景**
   - API查询初始状态
   - SignalR推送增量更新

### 黄金法则

> **如果客户端需要主动查询 → 使用 API**  
> **如果服务器需要主动通知 → 使用 SignalR**  
> **如果需要高频实时更新 → 使用 SignalR**  
> **如果是低频一次性操作 → 使用 API**

---

**文档状态**: ✅ 完成  
**最后更新**: 2025年10月21日  
**作者**: GitHub Copilot
