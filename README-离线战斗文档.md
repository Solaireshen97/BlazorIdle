# 离线战斗系统实施文档

> 📖 完整的离线战斗系统实施方案文档包

---

## 📋 文档清单

本次提交包含6份完整的实施文档：

### 1️⃣ 主入口文档
📄 **离线战斗实施方案.md** （根目录）
- 完整概述和快速导航
- 当前状态分析
- 缺失组件清单
- 实施路径指引
- 关键技术要点

👉 **建议先阅读此文档获取全局认识**

---

### 2️⃣ 快速开始
📄 **docs/离线战斗快速开始.md**
- 5分钟快速了解
- 代码框架和模板
- 实施步骤清单
- 测试方法
- 常见问题解答

⏱️ 阅读时间：10分钟
👨‍💻 适合：开发者快速上手

---

### 3️⃣ 实施总结（中文）
📄 **docs/离线战斗系统实施总结.md**
- 核心需求详解
- 已有vs缺失组件对比
- 详细实施步骤（含代码）
- 完整流程示例
- 数据模型定义
- 配置说明
- 测试计划

⏱️ 阅读时间：20-30分钟
👨‍💼 适合：开发者和技术负责人

---

### 4️⃣ 流程图文档
📄 **docs/离线战斗流程图.md**
- 用户上线流程图
- 后端计算流程图
- 快进引擎流程图
- 计划衔接流程图
- 前端交互流程图
- 时间轴示例
- 边界情况处理

⏱️ 阅读时间：15分钟
🎨 适合：视觉学习者

---

### 5️⃣ 详细实施方案（英文）
📄 **docs/OfflineBattleImplementationPlan.md**
- 完整需求分析
- 详细状态分析
- 缺失组件清单（含优先级）
- 分阶段实施方案
- 数据库设计
- API设计（含示例）
- 前端集成方案
- 测试验证计划
- 风险分析

⏱️ 阅读时间：45-60分钟
🏗️ 适合：架构师、技术负责人

---

### 6️⃣ 文档索引
📄 **docs/离线战斗文档索引.md**
- 所有文档索引
- 推荐阅读顺序
- 核心概念速查
- 数据模型速查
- 配置速查
- 测试检查清单

⏱️ 阅读时间：随时查阅
🔍 适合：查找特定信息

---

## 🎯 推荐阅读路径

### 场景1：快速开始实施（开发者）
```
1. 离线战斗实施方案.md (10分钟) - 获取全局认识
2. docs/离线战斗快速开始.md (10分钟) - 学习具体步骤
3. docs/离线战斗流程图.md (15分钟) - 理解系统流程
4. 开始编码 💻
```
**总时间**：35分钟阅读

---

### 场景2：技术评估（技术负责人）
```
1. 离线战斗实施方案.md (10分钟) - 快速概览
2. docs/离线战斗系统实施总结.md (30分钟) - 详细了解
3. docs/OfflineBattleImplementationPlan.md (60分钟) - 全面评估
4. 制定开发计划 📊
```
**总时间**：100分钟

---

### 场景3：查找特定信息
```
1. docs/离线战斗文档索引.md - 查找需要的文档
2. 直接跳转到相应章节
```
**总时间**：5分钟

---

## 💡 核心内容速览

### 实施概要
- **目标**：实现离线战斗收益计算与自动衔接活动计划
- **工作量**：4-7天
- **优先级**：高（放置游戏核心功能）

### 关键组件
| 组件 | 状态 | 说明 |
|------|------|------|
| OfflineFastForwardEngine | ❌ 需新增 | 离线快进引擎 |
| 自动离线检测 | ❌ 需新增 | 登录时触发结算 |
| 离线API端点 | ❌ 需新增 | check/apply接口 |
| 前端弹窗组件 | ❌ 需新增 | 收益展示 |

### 实施阶段
**Phase 1**: 后端核心 (2-3天)
- OfflineFastForwardEngine
- OfflineSettlementService扩展
- API端点

**Phase 2**: 前端集成 (1-2天)
- 离线弹窗组件
- ApiClient扩展
- Characters页面集成

**Phase 3**: 测试优化 (1-2天)
- 单元测试
- 集成测试
- 手动验证

---

## 🔧 关键技术点

### 1. 离线时长计算
```csharp
var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc).TotalSeconds;
var cappedSeconds = Math.Min(offlineSeconds, 43200); // 12小时上限
```

### 2. 活动计划剩余时长
```csharp
if (plan.LimitType == LimitType.Duration)
{
    var remaining = plan.LimitValue - plan.ExecutedSeconds;
    var simulated = Math.Min(remaining, cappedSeconds);
}
```

### 3. 自动衔接下一个计划
```csharp
if (plan.IsLimitReached())
{
    plan.State = ActivityState.Completed;
    await TryStartNextPendingPlanAsync(characterId);
}
```

---

## 📊 数据结构

### OfflineFastForwardResult
```json
{
  "characterId": "guid",
  "planId": "guid",
  "simulatedSeconds": 3600,
  "planCompleted": true,
  "gold": 5000,
  "exp": 8000,
  "loot": {
    "iron_ore": 25.5,
    "health_potion": 3.2
  }
}
```

### OfflineCheckResult
```json
{
  "hasOfflineTime": true,
  "offlineSeconds": 3600,
  "hasRunningPlan": true,
  "settlement": { /* OfflineFastForwardResult */ },
  "planCompleted": true,
  "nextPlanStarted": true,
  "nextPlanId": "guid"
}
```

---

## 🧪 测试验证

### 快速测试步骤
1. 创建角色和1小时战斗计划
2. 修改数据库 `LastSeenAtUtc` 为1小时前
3. 重新登录验证弹窗显示
4. 点击领取验证数据更新

详细测试方案见各文档的测试章节。

---

## ⚙️ 配置
```json
{
  "Offline": {
    "MaxOfflineSeconds": 43200,  // 12小时
    "EnableAutoSettlement": true
  }
}
```

---

## 📈 里程碑

- [ ] Phase 1: 后端核心完成
- [ ] Phase 2: 前端集成完成
- [ ] Phase 3: 测试验证通过
- [ ] 功能上线

---

## ✅ 完成标志

- ✅ 离线后上线能看到收益弹窗
- ✅ 收益数据准确
- ✅ 领取后数据正确更新
- ✅ 计划状态正确
- ✅ 自动衔接正常工作
- ✅ 所有测试通过

---

## 📞 获取帮助

遇到问题时查阅：

| 问题类型 | 参考文档 |
|---------|---------|
| 代码实现 | `docs/离线战斗快速开始.md` |
| 流程理解 | `docs/离线战斗流程图.md` |
| 详细设计 | `docs/OfflineBattleImplementationPlan.md` |
| 查找信息 | `docs/离线战斗文档索引.md` |

---

## 🚀 开始实施

1. ✅ 阅读 `离线战斗实施方案.md` 获取概览
2. ✅ 阅读 `docs/离线战斗快速开始.md` 学习步骤
3. ✅ 参考 `docs/离线战斗流程图.md` 理解流程
4. 💻 开始编码实施
5. 🧪 完成后进行测试验证

---

**祝实施顺利！** 🎉

所有文档已准备完毕，可以立即开始开发工作。
