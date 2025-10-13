# SignalR 配置优化指南

**更新日期**: 2025-10-13  
**版本**: 1.0  
**状态**: Stage 1 配置优化完成

---

## 📋 概述

本指南介绍了 SignalR 配置系统的优化和扩展，包括配置结构、环境特定设置、以及未来功能的预留选项。

---

## 🎯 优化目标

1. **配置参数化**: 所有参数从配置文件读取，避免硬编码
2. **环境特定配置**: 支持 Development/Production 环境差异化配置
3. **可扩展性**: 为 Phase 3/4 的高级功能预留配置接口
4. **细粒度控制**: 支持单独启用/禁用特定事件类型的通知
5. **性能优化预留**: 为未来的性能优化功能预留配置选项

---

## 🏗️ 配置结构

### 1. SignalROptions 类

**位置**: `BlazorIdle.Server/Config/SignalROptions.cs`

```csharp
public sealed class SignalROptions
{
    // 配置节名称常量
    public const string SectionName = "SignalR";
    
    // 基础配置
    public string HubEndpoint { get; set; }
    public bool EnableSignalR { get; set; }
    public int MaxReconnectAttempts { get; set; }
    public int ReconnectBaseDelayMs { get; set; }
    public int MaxReconnectDelayMs { get; set; }
    public bool EnableDetailedLogging { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }
    public int KeepAliveIntervalSeconds { get; set; }
    public int ServerTimeoutSeconds { get; set; }
    
    // 嵌套配置对象
    public NotificationOptions Notification { get; set; }
    public PerformanceOptions Performance { get; set; }
}
```

### 2. NotificationOptions 类

控制各类事件通知的启用/禁用：

```csharp
public sealed class NotificationOptions
{
    // Phase 2 已实现
    public bool EnablePlayerDeathNotification { get; set; }      // 玩家死亡
    public bool EnablePlayerReviveNotification { get; set; }     // 玩家复活
    public bool EnableEnemyKilledNotification { get; set; }      // 敌人击杀
    public bool EnableTargetSwitchedNotification { get; set; }   // 目标切换
    
    // Phase 3 预留
    public bool EnableWaveSpawnNotification { get; set; }        // 波次刷新
    public bool EnableSkillCastNotification { get; set; }        // 技能施放
    public bool EnableBuffChangeNotification { get; set; }       // Buff 变化
}
```

### 3. PerformanceOptions 类

为 Phase 4 性能优化预留：

```csharp
public sealed class PerformanceOptions
{
    public bool EnableThrottling { get; set; }          // 通知节流
    public int ThrottleWindowMs { get; set; }           // 节流窗口
    public bool EnableBatching { get; set; }            // 批量通知
    public int BatchDelayMs { get; set; }               // 批量延迟
    public bool AutoDegradeOnMobile { get; set; }       // 移动端降级
}
```

### 4. MonitoringOptions 类

**新增** (2025-10-13) - 监控和指标收集：

```csharp
public sealed class MonitoringOptions
{
    public bool EnableMetrics { get; set; }                    // 启用性能指标
    public int MetricsIntervalSeconds { get; set; }            // 指标收集间隔
    public bool EnableConnectionTracking { get; set; }         // 启用连接追踪
    public bool EnableLatencyMeasurement { get; set; }         // 启用延迟测量
    public int SlowNotificationThresholdMs { get; set; }       // 慢通知阈值
}
```

### 5. 新增配置项

```csharp
public string BattleGroupPrefix { get; set; } = "battle_";              // SignalR 分组前缀
public int MaxConcurrentConnections { get; set; } = 0;                 // 最大并发连接数(0=不限制)
public int ConnectionIdleTimeoutSeconds { get; set; } = 300;           // 连接空闲超时
```

---

## ⚙️ 配置文件

### 1. 服务器端基础配置 (appsettings.json)

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
      "EnableSkillCastNotification": false,
      "EnableBuffChangeNotification": false
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

### 2. 开发环境配置 (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Services.BattleNotificationService": "Debug",
      "BlazorIdle.Server.Hubs.BattleNotificationHub": "Debug"
    }
  },
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

**说明**:
- 开发环境启用详细日志
- 对 SignalR 相关组件使用 Debug 级别日志
- 便于开发调试和问题排查

### 3. 生产环境配置 (appsettings.Production.example.json)

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 3,
    "ReconnectBaseDelayMs": 2000,
    "MaxReconnectDelayMs": 60000,
    "EnableDetailedLogging": false,
    "ConnectionTimeoutSeconds": 60,
    "KeepAliveIntervalSeconds": 30,
    "ServerTimeoutSeconds": 60,
    "Notification": {
      "EnableWaveSpawnNotification": true,
      "EnableSkillCastNotification": true
    },
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 500,
      "EnableBatching": true,
      "BatchDelayMs": 50,
      "AutoDegradeOnMobile": true
    }
  }
}
```

**说明**:
- 生产环境更保守的重连策略（3次 vs 5次）
- 更长的超时时间（适应网络波动）
- 启用性能优化功能
- 启用移动端自动降级

### 4. 客户端配置 (wwwroot/appsettings.json)

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
  }
}
```

---

## 🔧 配置项说明

### 基础配置

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| `HubEndpoint` | string | "/hubs/battle" | SignalR Hub 端点路径 |
| `EnableSignalR` | bool | true | 全局开关，可用于降级到纯轮询 |
| `MaxReconnectAttempts` | int | 5 | 最大重连尝试次数 |
| `ReconnectBaseDelayMs` | int | 1000 | 重连基础延迟（毫秒） |
| `MaxReconnectDelayMs` | int | 30000 | 最大重连延迟（毫秒） |
| `EnableDetailedLogging` | bool | false | 详细日志开关（开发环境建议启用） |
| `ConnectionTimeoutSeconds` | int | 30 | 连接超时时间（秒） |
| `KeepAliveIntervalSeconds` | int | 15 | 保持连接间隔（秒） |
| `ServerTimeoutSeconds` | int | 30 | 服务器超时时间（秒） |

### 通知配置

| 配置项 | 类型 | 默认值 | 状态 | 说明 |
|-------|------|--------|------|------|
| `EnablePlayerDeathNotification` | bool | true | ✅ 已实现 | 玩家死亡通知 |
| `EnablePlayerReviveNotification` | bool | true | ✅ 已实现 | 玩家复活通知 |
| `EnableEnemyKilledNotification` | bool | true | ✅ 已实现 | 敌人击杀通知 |
| `EnableTargetSwitchedNotification` | bool | true | ✅ 已实现 | 目标切换通知 |
| `EnableWaveSpawnNotification` | bool | false | 📅 Phase 3 | 波次刷新通知 |
| `EnableSkillCastNotification` | bool | false | 📅 Phase 3 | 技能施放通知 |
| `EnableBuffChangeNotification` | bool | false | 📅 Phase 3 | Buff 变化通知 |

### 性能配置

| 配置项 | 类型 | 默认值 | 状态 | 说明 |
|-------|------|--------|------|------|
| `EnableThrottling` | bool | false | 📅 Phase 4 | 启用通知节流 |
| `ThrottleWindowMs` | int | 1000 | 📅 Phase 4 | 节流窗口大小（毫秒） |
| `EnableBatching` | bool | false | 📅 Phase 4 | 启用批量通知 |
| `BatchDelayMs` | int | 100 | 📅 Phase 4 | 批量通知延迟（毫秒） |
| `AutoDegradeOnMobile` | bool | false | 📅 Phase 4 | 移动端自动降级 |

---

## 🎯 使用场景

### 场景 1: 禁用特定事件通知

如果某类事件通知导致性能问题，可以单独禁用：

```json
{
  "SignalR": {
    "Notification": {
      "EnableEnemyKilledNotification": false  // 禁用敌人击杀通知
    }
  }
}
```

### 场景 2: 开发环境调试

开发环境启用详细日志：

```json
{
  "SignalR": {
    "EnableDetailedLogging": true
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Services.BattleNotificationService": "Debug"
    }
  }
}
```

### 场景 3: 生产环境降级

如果 SignalR 服务异常，可以快速降级到纯轮询：

```json
{
  "SignalR": {
    "EnableSignalR": false  // 全局禁用 SignalR
  }
}
```

### 场景 4: 网络不稳定环境

增加重连次数和延迟：

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 10,
    "ReconnectBaseDelayMs": 2000,
    "MaxReconnectDelayMs": 60000,
    "ConnectionTimeoutSeconds": 60,
    "ServerTimeoutSeconds": 60
  }
}
```

---

## 🧪 测试验证

### 测试覆盖

- ✅ 配置默认值验证
- ✅ 嵌套配置对象验证
- ✅ 事件类型启用/禁用验证
- ✅ 配置节名称常量验证

### 运行测试

```bash
dotnet test --filter "FullyQualifiedName~SignalR"
```

**结果**: 11/11 测试通过 ✅

---

## 📚 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 完整技术设计
- [SignalR_Phase1_实施总结.md](./SignalR_Phase1_实施总结.md) - Phase 1 总结
- [SignalR_Phase2_服务端集成完成报告.md](./SignalR_Phase2_服务端集成完成报告.md) - Phase 2 总结
- [SignalR优化进度更新.md](./SignalR优化进度更新.md) - 进度跟踪

---

## 🔄 后续计划

### Phase 3: 高级功能实现

当实现波次刷新、技能施放、Buff 变化等通知时：

1. 在服务中实现相应的通知逻辑
2. 在配置中启用对应的开关
3. 更新测试用例

### Phase 4: 性能优化实现

当实现性能优化功能时：

1. 实现通知节流逻辑
2. 实现批量通知机制
3. 实现移动端检测和降级
4. 在配置中启用对应的开关

---

## 🔧 配置验证

新增了 `SignalROptionsValidator` 类，在应用启动时自动验证配置：

```csharp
public sealed class SignalROptionsValidator : IValidateOptions<SignalROptions>
{
    public ValidateOptionsResult Validate(string? name, SignalROptions options)
    {
        // 验证逻辑
        // - HubEndpoint 必须以 '/' 开头
        // - 延迟配置必须合理
        // - 超时时间必须大于0
        // - BattleGroupPrefix 不能为空
        // - 等等...
    }
}
```

**使用方式**：

```csharp
// Program.cs 中自动注册
builder.Services.Configure<SignalROptions>(builder.Configuration.GetSection(SignalROptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<SignalROptions>, SignalROptionsValidator>();
```

---

## 📊 监控功能

### 延迟测量

当 `Monitoring.EnableLatencyMeasurement = true` 时，服务会自动测量每次通知的延迟：

```csharp
// 自动记录通知发送时间
var startTime = DateTime.UtcNow;
await _hubContext.Clients.Group(groupName).SendAsync("StateChanged", notification);

// 测量延迟
var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

// 慢通知警告
if (latencyMs > _options.Monitoring.SlowNotificationThresholdMs)
{
    _logger.LogWarning("Slow SignalR notification detected: {Latency}ms", latencyMs);
}
```

### 开发环境建议配置

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "Monitoring": {
      "EnableMetrics": true,
      "EnableConnectionTracking": true,
      "EnableLatencyMeasurement": true,
      "SlowNotificationThresholdMs": 500
    }
  }
}
```

### 生产环境建议配置

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "Monitoring": {
      "EnableMetrics": true,
      "EnableConnectionTracking": false,
      "EnableLatencyMeasurement": true,
      "SlowNotificationThresholdMs": 1000
    }
  }
}
```

---

## ✅ 验收标准

- [x] 所有配置参数从配置文件读取
- [x] 支持环境特定配置（Development/Production）
- [x] 预留未来功能配置选项
- [x] 支持细粒度事件控制
- [x] 配置验证和测试通过
- [x] 文档完整清晰
- [x] **新增**: 配置验证器自动检查配置有效性
- [x] **新增**: 监控选项支持性能追踪
- [x] **新增**: 可配置 SignalR 分组前缀
- [x] **新增**: 17个单元测试全部通过

---

## 📝 变更日志

### 2025-10-13 - Stage 1.5 配置增强

**新增功能**:
- 添加 `MonitoringOptions` 配置类
- 添加 `BattleGroupPrefix` 配置项
- 添加 `MaxConcurrentConnections` 配置项
- 添加 `ConnectionIdleTimeoutSeconds` 配置项
- 创建 `SignalROptionsValidator` 配置验证器
- 服务中实现延迟测量和慢通知警告

**测试**:
- 新增 6 个单元测试
- 总计 17 个测试，全部通过

**文档**:
- 更新配置优化指南
- 添加监控功能说明
- 添加配置验证说明

---

**更新人**: GitHub Copilot Agent  
**更新日期**: 2025-10-13  
**下次更新**: Stage 2 前端集成准备完成后
