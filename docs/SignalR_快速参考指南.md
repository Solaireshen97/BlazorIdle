# SignalR 快速参考指南

**目标读者**: 开发人员  
**更新日期**: 2025-10-14  
**状态**: 生产就绪

---

## 🚀 快速开始

### 1. 确认 SignalR 已启用

检查 `appsettings.json`:

```json
{
  "SignalR": {
    "EnableSignalR": true
  }
}
```

### 2. 启动应用

```bash
cd BlazorIdle.Server
dotnet run
```

应用启动时会自动验证配置。成功启动表示配置正确。

### 3. 测试连接

使用浏览器访问：
```
https://localhost:7056/hubs/battle
```

应该看到 SignalR 协议协商响应。

---

## 📝 常见任务

### 启用/禁用 SignalR

在 `appsettings.json` 中：

```json
{
  "SignalR": {
    "EnableSignalR": true  // false 禁用
  }
}
```

### 启用/禁用特定通知类型

```json
{
  "SignalR": {
    "Notification": {
      "EnablePlayerDeathNotification": true,
      "EnablePlayerReviveNotification": true,
      "EnableEnemyKilledNotification": true,
      "EnableTargetSwitchedNotification": true
    }
  }
}
```

### 调整性能设置

```json
{
  "SignalR": {
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 1000
    }
  }
}
```

### 启用详细日志（调试用）

在 `signalr-config.Development.json` 或 `appsettings.Development.json` 中：

```json
{
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

---

## 🔍 调试与故障排查

### 检查配置加载

查看启动日志：

```
开始验证 SignalR 配置...
SignalR 配置验证通过
SignalR 配置: EnableSignalR=True, HubEndpoint=/hubs/battle
```

### 查看通知发送日志

启用详细日志后，每次发送通知都会记录：

```
Sent SignalR notification: Battle={BattleId}, EventType=PlayerDeath
```

### 检查节流效果

启用详细日志后，被节流的通知会记录：

```
Notification throttled: Battle={BattleId}, EventType=EnemyKilled, SuppressedCount=5
```

### 运行测试

```bash
# 运行所有 SignalR 测试
dotnet test --filter "SignalR"

# 运行特定测试
dotnet test --filter "SignalRBattleIntegration"
```

### 常见问题

**问题**: 启动时抛出 "SignalR 配置验证失败"

**解决**: 检查配置文件中的数值是否在有效范围内：
- `MaxReconnectAttempts`: 1-20
- `ReconnectBaseDelayMs`: 100-10000
- `MaxReconnectDelayMs`: 1000-60000
- `ConnectionTimeoutSeconds`: 1-300

**问题**: 客户端无法连接

**解决**: 
1. 确认 `EnableSignalR` 为 true
2. 检查 CORS 设置
3. 确认 JWT 令牌有效
4. 查看浏览器控制台错误

**问题**: 收不到通知

**解决**:
1. 确认客户端已订阅战斗：`SignalRService.SubscribeBattle(battleId)`
2. 检查特定通知类型是否启用
3. 确认 `NotificationService` 已注入到 `BattleContext`
4. 查看服务器日志

---

## 📊 性能监控

### 查看指标

如果注入了 `SignalRMetricsCollector`：

```csharp
var metricsCollector = serviceProvider.GetService<SignalRMetricsCollector>();
var stats = metricsCollector.GetStatistics();

Console.WriteLine($"通知发送总数: {stats.TotalNotificationsSent}");
Console.WriteLine($"通知失败数: {stats.TotalNotificationsFailed}");
Console.WriteLine($"节流抑制数: {stats.ThrottleSuppressedCount}");
```

### 建议的监控指标

1. **通知延迟**: 从事件发生到客户端接收的时间 (目标 <1s)
2. **通知成功率**: 成功发送 / 总尝试 (目标 >95%)
3. **重连频率**: 客户端重连次数 (目标 <1次/小时)
4. **节流率**: 被节流的通知 / 总通知 (可接受 <50%)

---

## 🔧 高级配置

### 自定义重连策略

在 `appsettings.json` 中调整：

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000
  }
}
```

重连延迟使用指数退避：1s, 2s, 4s, 8s, 16s, 最多 30s

### 环境特定配置

开发环境 (`signalr-config.Development.json`):
```json
{
  "EnableDetailedLogging": true,
  "Performance": {
    "EnableThrottling": true,
    "ThrottleWindowMs": 500
  }
}
```

生产环境 (`signalr-config.Production.json`):
```json
{
  "EnableDetailedLogging": false,
  "Performance": {
    "EnableThrottling": true,
    "ThrottleWindowMs": 1000
  }
}
```

### 使用过滤器

在 `Program.cs` 中取消注释：

```csharp
builder.Services.AddSingleton<NotificationFilterPipeline>();
builder.Services.AddTransient<INotificationFilter, EventTypeFilter>();
builder.Services.AddTransient<INotificationFilter, RateLimitFilter>();
```

---

## 📚 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 完整技术设计
- [SignalR配置优化指南.md](./SignalR配置优化指南.md) - 配置详解
- [SignalR性能优化指南.md](./SignalR性能优化指南.md) - 性能优化
- [SignalR_实施完成报告_2025-10-14.md](./SignalR_实施完成报告_2025-10-14.md) - 最新验证报告
- [SignalR系统当前状态与下一步建议.md](./SignalR系统当前状态与下一步建议.md) - 系统状态

---

## 🎯 最佳实践

### DO ✅

1. ✅ 在开发环境启用详细日志
2. ✅ 在生产环境启用节流
3. ✅ 定期查看指标和日志
4. ✅ 为关键事件启用通知
5. ✅ 使用环境特定配置
6. ✅ 运行测试验证更改

### DON'T ❌

1. ❌ 在代码中硬编码配置参数
2. ❌ 在生产环境启用详细日志（性能影响）
3. ❌ 禁用启动验证器
4. ❌ 忽略配置验证错误
5. ❌ 过度使用通知（考虑节流）
6. ❌ 在通知处理器中执行耗时操作

---

## 🐛 已知限制

1. **客户端前端集成**: 尚未完成前端页面集成（计划中）
2. **批量通知**: 功能已预留但未实现
3. **移动端降级**: 功能已预留但未实现
4. **水平扩展**: 需要 Redis 作为 backplane（未配置）

---

## 🚀 下一步

1. **前端集成**: 在战斗页面中使用 `BattleSignalRService`
2. **降级策略**: 实现 SignalR 不可用时的优雅降级
3. **通知 UI**: 设计和实现 Toast 通知组件
4. **进度条优化**: 基于实时通知优化进度条动画

参考 [SignalR系统当前状态与下一步建议.md](./SignalR系统当前状态与下一步建议.md) 获取详细计划。

---

**维护者**: GitHub Copilot Agent  
**最后更新**: 2025-10-14  
**问题反馈**: 请创建 GitHub Issue
