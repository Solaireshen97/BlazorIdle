# SignalR 性能监控指南

**更新日期**: 2025-10-13  
**版本**: 1.0  
**状态**: Stage 3 性能监控完成

---

## 📋 概述

本指南介绍如何使用 SignalR 性能监控系统来跟踪、分析和优化实时通知的性能。

---

## 🎯 监控目标

1. **延迟监控**: 跟踪通知从服务器发送到客户端的延迟
2. **成功率监控**: 监控通知发送的成功率
3. **负载监控**: 了解系统通知负载和跳过率
4. **性能优化**: 基于指标数据进行系统调优

---

## 📊 核心指标

### 1. 计数器指标

| 指标 | 描述 | 用途 |
|------|------|------|
| `TotalNotificationsSent` | 成功发送的通知总数 | 了解系统负载 |
| `TotalNotificationsFailed` | 发送失败的通知总数 | 识别问题 |
| `TotalNotificationsSkipped` | 因配置禁用而跳过的通知数 | 优化配置 |

### 2. 延迟指标

| 指标 | 描述 | 目标值 |
|------|------|--------|
| `AverageLatencyMs` | 平均通知延迟（毫秒） | < 300ms |
| `P95LatencyMs` | 95百分位延迟（毫秒） | < 500ms |
| `P99LatencyMs` | 99百分位延迟（毫秒） | < 1000ms |

### 3. 派生指标

| 指标 | 计算方式 | 目标值 |
|------|----------|--------|
| `SuccessRate` | `TotalSent / (TotalSent + TotalFailed) * 100` | > 99% |

---

## 🔧 使用方法

### 1. 获取指标摘要

通过 `IBattleNotificationService` 接口获取指标：

```csharp
// 注入服务
private readonly IBattleNotificationService _notificationService;

// 获取指标
var metrics = _notificationService.GetMetrics();

Console.WriteLine($"总发送: {metrics.TotalSent}");
Console.WriteLine($"成功率: {metrics.SuccessRate:F2}%");
Console.WriteLine($"平均延迟: {metrics.AverageLatencyMs:F2}ms");
Console.WriteLine($"P95延迟: {metrics.P95LatencyMs:F2}ms");
Console.WriteLine($"P99延迟: {metrics.P99LatencyMs:F2}ms");
```

### 2. 指标自动收集

系统会自动收集以下数据：

```csharp
// 成功发送通知时
await NotifyStateChangeAsync(battleId, eventType);
// ✅ 自动记录: TotalSent++, 延迟计算

// 发送失败时
// ✅ 自动记录: TotalFailed++

// 配置禁用时
// ✅ 自动记录: TotalSkipped++
```

### 3. 指标重置

```csharp
// 注意：SignalRMetrics 是单例服务，不建议在生产环境重置
// 仅用于测试或调试场景
var metrics = serviceProvider.GetService<SignalRMetrics>();
metrics.Reset();
```

---

## 📈 性能分析

### 1. 识别延迟问题

```csharp
var metrics = _notificationService.GetMetrics();

if (metrics.P99LatencyMs > 1000)
{
    // 99%的通知延迟超过1秒，需要优化
    _logger.LogWarning("SignalR P99 延迟过高: {P99}ms", metrics.P99LatencyMs);
    
    // 建议措施:
    // 1. 检查网络状况
    // 2. 增加服务器资源
    // 3. 启用通知节流 (Performance.EnableThrottling)
    // 4. 启用批量通知 (Performance.EnableBatching)
}
```

### 2. 识别成功率问题

```csharp
if (metrics.SuccessRate < 95)
{
    _logger.LogError(
        "SignalR 成功率过低: {SuccessRate}%, 失败: {Failed}, 成功: {Sent}",
        metrics.SuccessRate,
        metrics.TotalFailed,
        metrics.TotalSent
    );
    
    // 建议措施:
    // 1. 检查客户端连接状态
    // 2. 增加重连次数 (MaxReconnectAttempts)
    // 3. 调整超时时间 (ConnectionTimeoutSeconds)
}
```

### 3. 优化跳过率

```csharp
var skipRate = (double)metrics.TotalSkipped / 
    (metrics.TotalSent + metrics.TotalSkipped + metrics.TotalFailed) * 100;

if (skipRate > 50)
{
    _logger.LogWarning("通知跳过率过高: {SkipRate}%", skipRate);
    
    // 建议措施:
    // 1. 检查 Notification 配置
    // 2. 确认是否有不必要的事件类型被禁用
    // 3. 考虑启用更多事件通知以改善用户体验
}
```

---

## 🎨 性能优化建议

### 1. 开发环境配置

**目标**: 详细日志 + 所有事件启用

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "Notification": {
      "EnablePlayerDeathNotification": true,
      "EnablePlayerReviveNotification": true,
      "EnableEnemyKilledNotification": true,
      "EnableTargetSwitchedNotification": true,
      "EnableWaveSpawnNotification": true,
      "EnableSkillCastNotification": true,
      "EnableBuffChangeNotification": true
    },
    "Performance": {
      "EnableThrottling": false,
      "EnableBatching": false
    }
  }
}
```

### 2. 生产环境配置

**目标**: 高性能 + 关键事件优先

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
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
      "EnableThrottling": true,
      "ThrottleWindowMs": 1000,
      "EnableBatching": true,
      "BatchDelayMs": 100,
      "AutoDegradeOnMobile": true
    }
  }
}
```

### 3. 高负载场景优化

当服务器负载高时：

1. **启用节流**: `EnableThrottling = true`
   - 限制通知频率，防止客户端过载
   
2. **启用批量**: `EnableBatching = true`
   - 将多个通知合并发送，减少网络开销

3. **增加节流窗口**: `ThrottleWindowMs = 2000`
   - 在更长的时间窗口内限制通知数量

4. **禁用非关键通知**:
   ```json
   {
     "EnableWaveSpawnNotification": false,
     "EnableSkillCastNotification": false,
     "EnableBuffChangeNotification": false
   }
   ```

---

## 🔍 故障排查

### 问题 1: 平均延迟突然升高

**症状**: `AverageLatencyMs` 从 100ms 升到 500ms+

**可能原因**:
1. 服务器 CPU 使用率高
2. 网络带宽不足
3. 数据库查询慢

**排查步骤**:
```bash
# 1. 检查服务器资源
top
netstat -an | grep ESTABLISHED | wc -l

# 2. 检查 SignalR 连接数
# 查看日志中的连接数指标

# 3. 启用详细日志
"EnableDetailedLogging": true

# 4. 分析日志找到慢操作
grep "SignalR" logs/app.log | grep "Latency"
```

### 问题 2: 成功率下降

**症状**: `SuccessRate` < 95%

**可能原因**:
1. 客户端频繁断线
2. 服务器重启
3. 负载均衡器配置问题

**排查步骤**:
```csharp
// 1. 检查错误日志
_logger.LogError("SignalR notification failed");

// 2. 增加重连次数
"MaxReconnectAttempts": 10

// 3. 调整超时时间
"ConnectionTimeoutSeconds": 60
"ServerTimeoutSeconds": 60
```

### 问题 3: 高跳过率

**症状**: `TotalSkipped` 远大于 `TotalSent`

**可能原因**:
1. 配置过于保守，禁用了过多事件
2. `EnableSignalR = false`
3. 特定事件类型被误禁

**排查步骤**:
```csharp
// 1. 检查配置
var metrics = _notificationService.GetMetrics();
var config = _configuration.GetSection("SignalR").Get<SignalROptions>();

if (!config.EnableSignalR)
{
    _logger.LogWarning("SignalR is disabled globally");
}

// 2. 审查事件配置
if (!config.Notification.EnablePlayerDeathNotification)
{
    _logger.LogWarning("Player death notifications are disabled");
}
```

---

## 📊 监控仪表板（未来计划）

### 建议监控面板指标

1. **实时指标**
   - 当前 TPS（每秒通知数）
   - 实时延迟图表
   - 连接数

2. **历史趋势**
   - 24小时延迟趋势
   - 成功率趋势
   - 跳过率趋势

3. **告警阈值**
   - P99延迟 > 1000ms
   - 成功率 < 95%
   - 失败数 > 100/分钟

---

## ✅ 验收标准

- [x] 指标自动收集（成功/失败/跳过）
- [x] 延迟百分位计算（P95/P99）
- [x] 成功率计算
- [x] GetMetrics() API 可用
- [x] 单元测试覆盖（5个测试）
- [x] 文档完整

---

## 📚 相关文档

- [SignalR配置优化指南.md](./SignalR配置优化指南.md) - 配置参数说明
- [SignalR优化进度更新.md](./SignalR优化进度更新.md) - 进度跟踪
- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 技术设计

---

**更新人**: GitHub Copilot Agent  
**更新日期**: 2025-10-13  
**下次更新**: 监控端点实现后
