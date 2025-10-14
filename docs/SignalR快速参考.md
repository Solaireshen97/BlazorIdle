# SignalR 快速参考

**最后更新**: 2025-10-14  
**状态**: 最新

---

## 🚀 快速开始

### 1. 检查配置

**服务端** (`appsettings.json`):
```json
{
  "SignalR": {
    "EnableSignalR": true
  }
}
```

**客户端** (`wwwroot/appsettings.json`):
```json
{
  "SignalR": {
    "EnableSignalR": true
  }
}
```

### 2. 启动应用

```bash
# 启动服务端
cd BlazorIdle.Server
dotnet run

# 启动客户端（如果分离）
cd BlazorIdle
dotnet run
```

### 3. 验证连接

1. 登录应用
2. 查看 Toast 通知：`✅ 实时通知已启用`
3. 打开浏览器控制台（F12）
4. 查看日志：`Connected to SignalR Hub`

---

## 🔧 常用配置

### 开发环境配置

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "ConnectionStatusNotifications": true
  }
}
```

### 生产环境配置

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "MaxReconnectAttempts": 10,
    "MaxReconnectDelayMs": 60000
  }
}
```

### 禁用 SignalR（测试降级）

```json
{
  "SignalR": {
    "EnableSignalR": false
  }
}
```

---

## 🐛 故障排查

### 连接失败（401）

**检查清单**:
- [ ] CORS 配置包含 `AllowCredentials()`
- [ ] JWT Token 正确传递
- [ ] Hub 有 `[Authorize]` 属性

**快速修复**:
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins("https://localhost:5001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ⚠️ 必需
    });
});
```

### 收不到事件

**检查清单**:
- [ ] 战斗已订阅（调用 `SubscribeBattleAsync`）
- [ ] 服务端通知已启用
- [ ] WebSocket 连接正常

**快速调试**:
```javascript
// 浏览器控制台
// 查看 WebSocket 连接
console.log('WebSocket:', performance.getEntriesByType('resource').filter(r => r.name.includes('hubs')));
```

### 重连失败

**检查清单**:
- [ ] Token 未过期
- [ ] 重连次数未达上限
- [ ] 服务端正常运行

**快速修复**:
```json
{
  "SignalR": {
    "MaxReconnectAttempts": 10,
    "MaxReconnectDelayMs": 60000
  }
}
```

---

## 📊 性能优化

### 启用节流

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

### 启用批量发送

```json
{
  "SignalR": {
    "Performance": {
      "EnableBatching": true,
      "BatchDelayMs": 100
    }
  }
}
```

---

## 📋 代码示例

### 订阅战斗

```csharp
if (_isSignalRConnected)
{
    await SignalRService.SubscribeBattleAsync(battleId);
}
```

### 取消订阅

```csharp
if (_isSignalRConnected && battleId.HasValue)
{
    await SignalRService.UnsubscribeBattleAsync(battleId.Value);
}
```

### 监听连接状态

```csharp
SignalRService.OnConnectionStateChanged(state =>
{
    Console.WriteLine($"SignalR 状态: {state}");
});
```

### 处理事件

```csharp
SignalRService.OnStateChanged(evt =>
{
    Console.WriteLine($"收到事件: {evt.EventType}");
    // 触发轮询更新
    await TriggerImmediatePollAsync(evt.BattleId);
});
```

---

## 🔑 重要配置项

| 配置项 | 开发环境 | 生产环境 | 说明 |
|--------|---------|---------|------|
| `EnableSignalR` | true | true | 是否启用 |
| `EnableDetailedLogging` | true | false | 详细日志 |
| `MaxReconnectAttempts` | 5 | 10 | 重连次数 |
| `ConnectionStatusNotifications` | true | false | 状态通知 |

---

## 📚 完整文档

- [配置完整指南](./SignalR配置完整指南.md) - 详细配置说明
- [端到端测试指南](./SignalR端到端测试指南.md) - 测试用例
- [前端集成方案](./SignalR前端集成方案.md) - 集成步骤
- [优化完成总结](./SignalR优化完成总结.md) - 完整总结

---

## 🆘 获取帮助

1. 查看完整文档
2. 检查浏览器控制台日志
3. 查看服务端日志
4. 提交 Issue 或 Pull Request

---

**维护**: GitHub Copilot Agent
