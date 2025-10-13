# SignalR 配置指南

本文档提供 BlazorIdle SignalR 系统的配置说明和最佳实践。

---

## 📋 目录

1. [配置概览](#配置概览)
2. [基础配置](#基础配置)
3. [增强配置（Phase 2.5）](#增强配置phase-25)
4. [环境配置](#环境配置)
5. [配置验证](#配置验证)
6. [故障排查](#故障排查)
7. [最佳实践](#最佳实践)

---

## 配置概览

SignalR 配置位于 `appsettings.json` 的 `SignalR` 节点，包含 21 个配置项：

| 类别 | 配置项数量 | 说明 |
|------|----------|------|
| 基础配置 | 8项 | Hub端点、连接、重连、日志 |
| 组名和方法 | 3项 | 组名前缀、方法名 |
| 节流配置 | 4项 | 通知节流（预留） |
| 监控配置 | 4项 | 性能监控、日志控制 |

---

## 基础配置

### HubEndpoint (Hub 端点路径)

**类型**: `string`  
**默认值**: `"/hubs/battle"`  
**说明**: SignalR Hub 的 URL 路径

```json
{
  "SignalR": {
    "HubEndpoint": "/hubs/battle"
  }
}
```

**使用场景**:
- 自定义 Hub 路径
- 区分不同版本的 API
- 多 Hub 部署

### EnableSignalR (全局开关)

**类型**: `bool`  
**默认值**: `true`  
**说明**: 是否启用 SignalR 功能，可用于降级到纯轮询

```json
{
  "SignalR": {
    "EnableSignalR": false  // 禁用 SignalR，使用纯轮询
  }
}
```

**使用场景**:
- 临时禁用 SignalR
- 测试降级策略
- 排查 SignalR 相关问题

### MaxReconnectAttempts (最大重连次数)

**类型**: `int`  
**默认值**: `5`  
**说明**: 断线后自动重连的最大尝试次数

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 5
  }
}
```

**建议值**:
- 开发环境: `3-5` (快速失败)
- 生产环境: `5-10` (提高可用性)
- 移动端: `8-12` (网络不稳定)

### ReconnectBaseDelayMs (重连基础延迟)

**类型**: `int` (毫秒)  
**默认值**: `1000`  
**说明**: 重连的基础延迟，采用指数退避策略

```json
{
  "SignalR": {
    "ReconnectBaseDelayMs": 1000  // 1s → 2s → 4s → 8s → 16s
  }
}
```

**延迟计算**: `delay = baseDelay * 2^(attempt - 1)`

### EnableDetailedLogging (详细日志)

**类型**: `bool`  
**默认值**: `false`  
**说明**: 是否启用详细的调试日志

```json
{
  "SignalR": {
    "EnableDetailedLogging": true  // 开发环境建议启用
  }
}
```

**影响范围**:
- 连接建立和断开
- 订阅和取消订阅
- 通知发送详情

### ConnectionTimeoutSeconds (连接超时)

**类型**: `int` (秒)  
**默认值**: `30`  
**说明**: 建立连接的超时时间

```json
{
  "SignalR": {
    "ConnectionTimeoutSeconds": 30
  }
}
```

### KeepAliveIntervalSeconds (保持连接间隔)

**类型**: `int` (秒)  
**默认值**: `15`  
**说明**: 发送保持连接包的间隔

```json
{
  "SignalR": {
    "KeepAliveIntervalSeconds": 15
  }
}
```

**建议值**:
- 高带宽: `10-15` 秒
- 移动网络: `20-30` 秒

### ServerTimeoutSeconds (服务器超时)

**类型**: `int` (秒)  
**默认值**: `30`  
**说明**: 服务器端认为连接断开的超时时间

```json
{
  "SignalR": {
    "ServerTimeoutSeconds": 30
  }
}
```

---

## 增强配置（Phase 2.5）

### GroupNamePrefix (组名前缀)

**类型**: `string`  
**默认值**: `"battle_"`  
**说明**: SignalR 组名的前缀，用于战斗订阅分组

```json
{
  "SignalR": {
    "GroupNamePrefix": "battle_"  // 生成组名: battle_{battleId}
  }
}
```

**使用场景**:
- 区分不同类型的订阅组
- 多 Hub 场景下的命名隔离
- 便于日志过滤和监控

**示例**:
```csharp
// 默认配置
GroupName = "battle_" + battleId;  // "battle_12345678-1234-..."

// 自定义配置
GroupName = "combat_v2_" + battleId;  // "combat_v2_12345678-1234-..."
```

### Methods (方法名配置)

#### Methods.StateChanged

**类型**: `string`  
**默认值**: `"StateChanged"`  
**说明**: 状态变更通知的方法名

```json
{
  "SignalR": {
    "Methods": {
      "StateChanged": "StateChanged"
    }
  }
}
```

**使用场景**:
- API 版本演进（如 "StateChangedV2"）
- A/B 测试不同通知策略
- 支持多客户端版本

#### Methods.BattleEvent

**类型**: `string`  
**默认值**: `"BattleEvent"`  
**说明**: 详细事件通知的方法名

```json
{
  "SignalR": {
    "Methods": {
      "BattleEvent": "BattleEvent"
    }
  }
}
```

### Throttling (节流配置)

> **注意**: 节流功能计划在 Phase 4 实施，当前配置为预留接口。

#### Throttling.EnableThrottling

**类型**: `bool`  
**默认值**: `false`  
**说明**: 是否启用通知节流

```json
{
  "SignalR": {
    "Throttling": {
      "EnableThrottling": true
    }
  }
}
```

#### Throttling.MinNotificationIntervalMs

**类型**: `int` (毫秒)  
**默认值**: `100`  
**说明**: 最小通知间隔，防止高频通知

```json
{
  "SignalR": {
    "Throttling": {
      "MinNotificationIntervalMs": 100  // 每100ms最多发送一次
    }
  }
}
```

#### Throttling.MaxBatchDelayMs

**类型**: `int` (毫秒)  
**默认值**: `500`  
**说明**: 批量通知的最大延迟

```json
{
  "SignalR": {
    "Throttling": {
      "MaxBatchDelayMs": 500  // 最多延迟500ms批量发送
    }
  }
}
```

#### Throttling.MaxEventsPerBatch

**类型**: `int`  
**默认值**: `10`  
**说明**: 每批最多包含的事件数

```json
{
  "SignalR": {
    "Throttling": {
      "MaxEventsPerBatch": 10
    }
  }
}
```

### Monitoring (监控配置)

#### Monitoring.EnableMetrics

**类型**: `bool`  
**默认值**: `false`  
**说明**: 是否启用性能指标收集

```json
{
  "SignalR": {
    "Monitoring": {
      "EnableMetrics": true  // 生产环境建议启用
    }
  }
}
```

**收集的指标**:
- 通知发送耗时
- 慢通知检测
- 通知失败率

#### Monitoring.LogConnectionEvents

**类型**: `bool`  
**默认值**: `true`  
**说明**: 是否记录连接和断开事件

```json
{
  "SignalR": {
    "Monitoring": {
      "LogConnectionEvents": false  // 生产环境可关闭以减少日志
    }
  }
}
```

#### Monitoring.LogNotificationDetails

**类型**: `bool`  
**默认值**: `false`  
**说明**: 是否记录每次通知的详细信息

```json
{
  "SignalR": {
    "Monitoring": {
      "LogNotificationDetails": true  // 调试时启用
    }
  }
}
```

#### Monitoring.SlowNotificationThresholdMs

**类型**: `int` (毫秒)  
**默认值**: `1000`  
**说明**: 慢通知的阈值，超过此时间记录警告

```json
{
  "SignalR": {
    "Monitoring": {
      "SlowNotificationThresholdMs": 500  // 超过500ms记录警告
    }
  }
}
```

---

## 环境配置

### 开发环境

**文件**: `appsettings.Development.json`

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "MaxReconnectAttempts": 3,
    "Monitoring": {
      "EnableMetrics": true,
      "LogConnectionEvents": true,
      "LogNotificationDetails": true,
      "SlowNotificationThresholdMs": 500
    }
  }
}
```

**特点**:
- 详细日志，便于调试
- 较少重连次数，快速失败
- 启用所有监控选项
- 较低的慢通知阈值

### 生产环境

**文件**: `appsettings.Production.json`

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "MaxReconnectAttempts": 8,
    "Monitoring": {
      "EnableMetrics": true,
      "LogConnectionEvents": false,
      "LogNotificationDetails": false,
      "SlowNotificationThresholdMs": 1000
    },
    "Throttling": {
      "EnableThrottling": true,
      "MinNotificationIntervalMs": 100
    }
  }
}
```

**特点**:
- 关闭详细日志，减少存储
- 较多重连次数，提高可用性
- 启用性能指标，关闭详细日志
- 启用节流，保护服务器

### 测试环境

**文件**: `appsettings.Staging.json`

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "Monitoring": {
      "EnableMetrics": true,
      "LogConnectionEvents": true,
      "LogNotificationDetails": false,
      "SlowNotificationThresholdMs": 800
    }
  }
}
```

---

## 配置验证

### 单元测试验证

运行以下命令验证配置加载：

```bash
dotnet test --filter "FullyQualifiedName~SignalR"
```

**预期结果**: 11/11 测试通过

### 配置校验清单

- [ ] HubEndpoint 路径以 "/" 开头
- [ ] MaxReconnectAttempts > 0
- [ ] ReconnectBaseDelayMs >= 100
- [ ] ConnectionTimeoutSeconds > 0
- [ ] KeepAliveIntervalSeconds < ServerTimeoutSeconds
- [ ] GroupNamePrefix 不为空
- [ ] Methods.StateChanged 和 Methods.BattleEvent 不为空
- [ ] MinNotificationIntervalMs >= 10
- [ ] SlowNotificationThresholdMs >= 100

### 运行时验证

启动应用后检查日志：

```
info: BlazorIdle.Server.Hubs.BattleNotificationHub[0]
      Client connected: {ConnectionId}
```

---

## 故障排查

### 问题：SignalR 连接失败

**可能原因**:
1. HubEndpoint 配置错误
2. 服务器未启用 SignalR
3. 防火墙或网络问题

**解决方案**:
```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "ConnectionTimeoutSeconds": 60
  }
}
```

### 问题：频繁断线重连

**可能原因**:
1. KeepAliveInterval 设置过短
2. 网络不稳定
3. 服务器负载过高

**解决方案**:
```json
{
  "SignalR": {
    "KeepAliveIntervalSeconds": 30,
    "ServerTimeoutSeconds": 60,
    "MaxReconnectAttempts": 10
  }
}
```

### 问题：通知延迟高

**可能原因**:
1. 通知发送耗时过长
2. 网络延迟
3. 服务器性能问题

**排查方法**:
```json
{
  "SignalR": {
    "Monitoring": {
      "EnableMetrics": true,
      "SlowNotificationThresholdMs": 500
    }
  }
}
```

查看日志中的慢通知警告：
```
warn: BlazorIdle.Server.Services.BattleNotificationService[0]
      Slow SignalR notification detected: Battle={BattleId}, EventType={EventType}, ElapsedMs={ElapsedMs}
```

---

## 最佳实践

### 1. 环境差异化配置

- **开发**: 详细日志，快速失败
- **测试**: 基本监控，中等超时
- **生产**: 精简日志，高可用性

### 2. 监控策略

生产环境建议：
```json
{
  "SignalR": {
    "Monitoring": {
      "EnableMetrics": true,
      "LogConnectionEvents": false,
      "LogNotificationDetails": false,
      "SlowNotificationThresholdMs": 1000
    }
  }
}
```

### 3. 性能优化

高并发场景：
```json
{
  "SignalR": {
    "Throttling": {
      "EnableThrottling": true,
      "MinNotificationIntervalMs": 100,
      "MaxBatchDelayMs": 500
    }
  }
}
```

### 4. 降级策略

紧急情况下快速降级：
```json
{
  "SignalR": {
    "EnableSignalR": false  // 立即切换到纯轮询
  }
}
```

### 5. 自定义组名

多 Hub 场景：
```json
{
  "SignalR": {
    "GroupNamePrefix": "app_v2_battle_"  // 区分版本和类型
  }
}
```

### 6. API 版本演进

支持多版本客户端：
```json
{
  "SignalR": {
    "Methods": {
      "StateChanged": "StateChangedV2",  // 新版本
      "BattleEvent": "BattleEvent"        // 保持兼容
    }
  }
}
```

---

## 配置示例

### 完整配置示例

```json
{
  "SignalR": {
    // 基础配置
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "EnableDetailedLogging": false,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    
    // 组名和方法名
    "GroupNamePrefix": "battle_",
    "Methods": {
      "StateChanged": "StateChanged",
      "BattleEvent": "BattleEvent"
    },
    
    // 节流配置（预留）
    "Throttling": {
      "EnableThrottling": false,
      "MinNotificationIntervalMs": 100,
      "MaxBatchDelayMs": 500,
      "MaxEventsPerBatch": 10
    },
    
    // 监控配置
    "Monitoring": {
      "EnableMetrics": false,
      "LogConnectionEvents": true,
      "LogNotificationDetails": false,
      "SlowNotificationThresholdMs": 1000
    }
  }
}
```

---

## 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 技术设计
- [SignalR_Phase2.5_配置增强报告.md](./SignalR_Phase2.5_配置增强报告.md) - 配置详解
- [SignalR优化进度更新.md](./SignalR优化进度更新.md) - 进度跟踪

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2025-10-13 | Phase 1 基础配置（8项） |
| 1.5 | 2025-10-13 | Phase 2.5 增强配置（21项） |

---

**最后更新**: 2025-10-13  
**维护者**: BlazorIdle 开发团队
