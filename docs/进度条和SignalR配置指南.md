# 进度条和 SignalR 配置指南

**目标读者**: 开发者、运维人员  
**更新日期**: 2025-10-14  
**相关版本**: Phase 2.5+

---

## 📋 目录

1. [后端 SignalR 配置](#后端-signalr-配置)
2. [前端进度条配置](#前端进度条配置)
3. [配置场景示例](#配置场景示例)
4. [性能调优建议](#性能调优建议)
5. [故障排查](#故障排查)

---

## 后端 SignalR 配置

### 配置文件位置

`BlazorIdle.Server/appsettings.json`

### 完整配置示例

```json
{
  "SignalR": {
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "EnableDetailedLogging": false,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    "Notification": {
      "EnablePlayerDeathNotification": true,
      "EnablePlayerReviveNotification": true,
      "EnableEnemyKilledNotification": true,
      "EnableTargetSwitchedNotification": true,
      "EnableWaveSpawnNotification": false,
      "EnableSkillCastNotification": true,
      "EnableBuffChangeNotification": false,
      "EnableAttackTickNotification": true,
      "EnableDamageAppliedNotification": true
    },
    "Performance": {
      "EnableThrottling": false,
      "ThrottleWindowMs": 1000,
      "EnableBatching": false,
      "BatchDelayMs": 100,
      "AutoDegradeOnMobile": false
    }
  }
}
```

### 参数说明

#### 基础连接设置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `HubEndpoint` | string | "/hubs/battle" | SignalR Hub 端点路径 |
| `EnableSignalR` | bool | true | 总开关，false 时完全禁用 SignalR |
| `MaxReconnectAttempts` | int | 5 | 断线后最大重连次数 |
| `ReconnectBaseDelayMs` | int | 1000 | 重连基础延迟（毫秒） |
| `MaxReconnectDelayMs` | int | 30000 | 重连最大延迟（毫秒） |
| `EnableDetailedLogging` | bool | false | 启用详细日志（开发环境） |
| `ConnectionTimeoutSeconds` | int | 30 | 连接超时时间 |
| `KeepAliveIntervalSeconds` | int | 15 | 保持连接心跳间隔 |
| `ServerTimeoutSeconds` | int | 30 | 服务器超时时间 |

#### 通知事件设置（Notification 节）

| 参数 | 默认值 | 触发时机 | 建议场景 |
|------|--------|---------|---------|
| `EnablePlayerDeathNotification` | true | 玩家死亡时 | 总是启用 |
| `EnablePlayerReviveNotification` | true | 玩家复活时 | 总是启用 |
| `EnableEnemyKilledNotification` | true | 击杀敌人时 | 总是启用 |
| `EnableTargetSwitchedNotification` | true | 切换目标时 | 总是启用 |
| `EnableWaveSpawnNotification` | false | 波次刷新时 | 副本/地城场景 |
| `EnableSkillCastNotification` | true | 技能施放时 | 体验优先场景 |
| `EnableBuffChangeNotification` | false | Buff 变化时 | 预留（未实现） |
| **`EnableAttackTickNotification`** | **true** | **每次攻击后** | **进度条同步** |
| **`EnableDamageAppliedNotification`** | **true** | **伤害应用时** | **血量即时更新** |

#### 性能优化设置（Performance 节）

| 参数 | 默认值 | 说明 | 使用场景 |
|------|--------|------|---------|
| `EnableThrottling` | false | 启用事件节流 | 高并发场景 |
| `ThrottleWindowMs` | 1000 | 节流窗口大小 | 配合节流使用 |
| `EnableBatching` | false | 启用批量发送 | 预留（未实现） |
| `BatchDelayMs` | 100 | 批量延迟时间 | 预留（未实现） |
| `AutoDegradeOnMobile` | false | 移动端自动降级 | 预留（未实现） |

---

## 前端进度条配置

### 配置文件位置

`BlazorIdle/wwwroot/appsettings.json`

### 完整配置示例

```json
{
  "ApiBaseUrl": "https://localhost:7056",
  "SignalR": {
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "ConnectionTimeoutSeconds": 30,
    "EnableDetailedLogging": false
  },
  "ProgressBar": {
    "EnableLoopingProgress": true,
    "AnimationIntervalMs": 100,
    "MinIntervalForLooping": 0.1,
    "MaxIntervalForLooping": 100.0,
    "EnableSyncOnAttackTick": true,
    "EnableSyncOnSkillCast": true,
    "EnableSyncOnDamageApplied": true
  },
  "JITPolling": {
    "EnableJITPolling": true,
    "TriggerWindowMs": 150,
    "MinPredictionTimeMs": 100,
    "MaxJITAttemptsPerCycle": 1,
    "AdaptivePollingEnabled": true,
    "MinPollingIntervalMs": 200,
    "MaxPollingIntervalMs": 2000,
    "HealthCriticalThreshold": 0.3,
    "HealthLowThreshold": 0.5,
    "CriticalHealthPollingMs": 500,
    "LowHealthPollingMs": 1000,
    "NormalPollingMs": 2000
  },
  "HPAnimation": {
    "TransitionDurationMs": 120,
    "TransitionTimingFunction": "linear",
    "EnableSmoothTransition": true,
    "PlayerHPTransitionMs": 120,
    "EnemyHPTransitionMs": 120
  },
  "Debug": {
    "LogProgressCalculations": false,
    "LogJITPollingEvents": false,
    "ShowProgressDebugInfo": false
  }
}
```

### 参数说明

#### 进度条设置（ProgressBar 节）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `EnableLoopingProgress` | bool | true | 启用循环进度条（>100% 时继续滚动） |
| `AnimationIntervalMs` | int | 100 | 动画刷新间隔（毫秒） |
| `MinIntervalForLooping` | double | 0.1 | 循环有效的最小攻击间隔（秒） |
| `MaxIntervalForLooping` | double | 100.0 | 循环有效的最大攻击间隔（秒） |
| **`EnableSyncOnAttackTick`** | **bool** | **true** | **响应 AttackTick 事件同步进度** |
| **`EnableSyncOnSkillCast`** | **bool** | **true** | **响应 SkillCast 事件更新技能** |
| **`EnableSyncOnDamageApplied`** | **bool** | **true** | **响应 DamageApplied 事件更新血量** |

#### JIT 轮询设置（JITPolling 节）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `EnableJITPolling` | bool | true | 启用即时轮询机制 |
| `TriggerWindowMs` | int | 150 | 触发窗口（攻击前多久轮询） |
| `MinPredictionTimeMs` | int | 100 | 最小预测时间 |
| `MaxJITAttemptsPerCycle` | int | 1 | 每个攻击周期最多 JIT 次数 |
| `AdaptivePollingEnabled` | bool | true | 启用自适应轮询 |
| `MinPollingIntervalMs` | int | 200 | 最小轮询间隔 |
| `MaxPollingIntervalMs` | int | 2000 | 最大轮询间隔 |
| `HealthCriticalThreshold` | double | 0.3 | 危急血量阈值（30%） |
| `HealthLowThreshold` | double | 0.5 | 偏低血量阈值（50%） |
| `CriticalHealthPollingMs` | int | 500 | 危急时轮询间隔 |
| `LowHealthPollingMs` | int | 1000 | 偏低时轮询间隔 |
| `NormalPollingMs` | int | 2000 | 正常时轮询间隔 |

#### HP 动画设置（HPAnimation 节）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `TransitionDurationMs` | int | 120 | 默认过渡时长（毫秒） |
| `TransitionTimingFunction` | string | "linear" | CSS 过渡函数 |
| `EnableSmoothTransition` | bool | true | 启用平滑过渡效果 |
| `PlayerHPTransitionMs` | int | 120 | 玩家 HP 条过渡时长 |
| `EnemyHPTransitionMs` | int | 120 | 敌人 HP 条过渡时长 |

#### 调试设置（Debug 节）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `LogProgressCalculations` | bool | false | 记录进度计算详情 |
| `LogJITPollingEvents` | bool | false | 记录 JIT 轮询触发 |
| `ShowProgressDebugInfo` | bool | false | 在 UI 显示调试信息 |

---

## 配置场景示例

### 场景 1: 开发调试模式

**目标**: 查看所有事件和日志，方便调试

**后端配置**:
```json
{
  "SignalR": {
    "EnableSignalR": true,
    "EnableDetailedLogging": true,
    "Notification": {
      "EnableAttackTickNotification": true,
      "EnableSkillCastNotification": true,
      "EnableDamageAppliedNotification": true
    }
  }
}
```

**前端配置**:
```json
{
  "ProgressBar": {
    "EnableSyncOnAttackTick": true,
    "EnableSyncOnSkillCast": true,
    "EnableSyncOnDamageApplied": true
  },
  "Debug": {
    "LogProgressCalculations": true,
    "LogJITPollingEvents": true,
    "ShowProgressDebugInfo": true
  }
}
```

---

### 场景 2: 生产环境（体验优先）

**目标**: 最佳用户体验，延迟最低

**后端配置**:
```json
{
  "SignalR": {
    "EnableSignalR": true,
    "EnableDetailedLogging": false,
    "Notification": {
      "EnableAttackTickNotification": true,
      "EnableSkillCastNotification": true,
      "EnableDamageAppliedNotification": true
    },
    "Performance": {
      "EnableThrottling": false
    }
  }
}
```

**前端配置**:
```json
{
  "ProgressBar": {
    "EnableLoopingProgress": true,
    "EnableSyncOnAttackTick": true,
    "EnableSyncOnSkillCast": true,
    "EnableSyncOnDamageApplied": true
  },
  "JITPolling": {
    "EnableJITPolling": true,
    "AdaptivePollingEnabled": true
  }
}
```

---

### 场景 3: 高并发场景（性能优先）

**目标**: 降低服务器负载，牺牲部分即时性

**后端配置**:
```json
{
  "SignalR": {
    "EnableSignalR": true,
    "Notification": {
      "EnableAttackTickNotification": false,
      "EnableSkillCastNotification": true,
      "EnableDamageAppliedNotification": false
    },
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 1000
    }
  }
}
```

**前端配置**:
```json
{
  "ProgressBar": {
    "EnableSyncOnAttackTick": false,
    "EnableSyncOnSkillCast": true,
    "EnableSyncOnDamageApplied": false
  },
  "JITPolling": {
    "EnableJITPolling": true,
    "MinPollingIntervalMs": 500,
    "MaxPollingIntervalMs": 3000
  }
}
```

---

### 场景 4: 移动端/弱网络

**目标**: 降低流量消耗，保证核心功能

**后端配置**:
```json
{
  "SignalR": {
    "EnableSignalR": true,
    "Notification": {
      "EnableAttackTickNotification": false,
      "EnableSkillCastNotification": true,
      "EnableDamageAppliedNotification": false
    }
  }
}
```

**前端配置**:
```json
{
  "ProgressBar": {
    "EnableLoopingProgress": true,
    "EnableSyncOnAttackTick": false,
    "EnableSyncOnSkillCast": true,
    "EnableSyncOnDamageApplied": false
  },
  "JITPolling": {
    "EnableJITPolling": true,
    "MinPollingIntervalMs": 1000,
    "MaxPollingIntervalMs": 5000,
    "AdaptivePollingEnabled": false
  }
}
```

---

## 性能调优建议

### 1. 事件频率控制

**问题**: 高攻速下 AttackTick 和 DamageApplied 事件过于频繁

**解决方案**:
- 启用 `Performance.EnableThrottling = true`
- 调整 `ThrottleWindowMs` 为 500-1000ms
- 或直接禁用 `EnableAttackTickNotification` 和 `EnableDamageAppliedNotification`

### 2. 轮询间隔优化

**问题**: 轮询过于频繁导致服务器压力

**解决方案**:
- 增加 `JITPolling.MinPollingIntervalMs` 到 500ms
- 增加 `JITPolling.MaxPollingIntervalMs` 到 3000ms
- 禁用 `AdaptivePollingEnabled`，使用固定轮询间隔

### 3. 动画性能优化

**问题**: 低端设备动画卡顿

**解决方案**:
- 增加 `ProgressBar.AnimationIntervalMs` 到 200ms
- 增加 `HPAnimation.TransitionDurationMs` 到 200ms
- 禁用 `HPAnimation.EnableSmoothTransition`

---

## 故障排查

### 问题 1: SignalR 连接失败

**症状**: 前端无法接收实时事件

**检查步骤**:
1. 确认后端 `EnableSignalR = true`
2. 确认前端 `ApiBaseUrl` 和 `HubEndpoint` 正确
3. 检查认证 Token 是否有效
4. 查看浏览器控制台 SignalR 连接日志

**解决方案**:
```json
// 前端启用详细日志
{
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

### 问题 2: 事件接收但不生效

**症状**: SignalR 连接正常，但前端没有反应

**检查步骤**:
1. 确认对应事件的 `EnableSync*` 开关已启用
2. 确认前端已注册事件处理器
3. 检查浏览器控制台是否有事件接收日志

**解决方案**:
```javascript
// 检查是否注册了处理器
SignalRService.OnAttackTick(evt => {
    console.log("AttackTick received:", evt);
});
```

### 问题 3: 进度条不同步

**症状**: 进度条显示与实际战斗状态不一致

**检查步骤**:
1. 确认 `EnableSyncOnAttackTick = true`
2. 确认后端 `EnableAttackTickNotification = true`
3. 检查攻击间隔是否在有效范围内

**解决方案**:
```json
// 调整有效间隔范围
{
  "ProgressBar": {
    "MinIntervalForLooping": 0.05,
    "MaxIntervalForLooping": 200.0
  }
}
```

### 问题 4: 高频事件导致性能问题

**症状**: 页面卡顿，CPU 占用高

**检查步骤**:
1. 确认事件频率（查看控制台日志）
2. 检查是否为快速攻击导致

**解决方案**:
```json
// 后端启用节流
{
  "SignalR": {
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 500
    }
  }
}

// 或前端禁用高频事件
{
  "ProgressBar": {
    "EnableSyncOnAttackTick": false,
    "EnableSyncOnDamageApplied": false
  }
}
```

---

## 总结

本配置指南涵盖了：
- ✅ 完整的配置参数说明
- ✅ 4 种典型场景的配置示例
- ✅ 性能调优建议
- ✅ 常见问题故障排查

**建议配置流程**:
1. 从默认配置开始
2. 根据实际场景选择对应的配置模板
3. 观察性能指标（延迟、带宽、CPU）
4. 根据监控数据微调参数

**配置原则**:
- **开发环境**: 启用所有日志和调试功能
- **生产环境**: 根据用户群体和硬件条件选择合适的配置
- **监控驱动**: 基于监控数据持续优化配置

---

**文档版本**: 1.0  
**最后更新**: 2025-10-14
