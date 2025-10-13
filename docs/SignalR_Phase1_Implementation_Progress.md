# SignalR Phase 1 实施进度文档

**版本**: 1.0  
**日期**: 2025-10-13  
**状态**: Phase 1 基础架构完成，等待测试验证

---

## 📊 完成进度

### ✅ Phase 1.1: 配置系统（100%）
- [x] 创建 `SignalROptions` 配置类
- [x] 更新服务器端配置文件（appsettings.json）
- [x] 更新客户端配置文件（appsettings.json）
- [x] 支持开发/生产环境差异化配置

### ✅ Phase 1.2: 服务器端基础架构（100%）
- [x] 添加 `Microsoft.AspNetCore.SignalR` NuGet 包
- [x] 创建 `BattleNotificationHub`
- [x] 创建 `IBattleNotificationService` 接口
- [x] 实现 `BattleNotificationService`
- [x] 更新 Program.cs 注册服务和端点
- [x] 构建验证通过

### ✅ Phase 1.3: 客户端基础架构（100%）
- [x] 添加 `Microsoft.AspNetCore.SignalR.Client` NuGet 包
- [x] 创建 `BattleSignalRService`
- [x] 实现连接管理和事件处理
- [x] 实现自动重连机制
- [x] 更新 Program.cs 注册服务
- [x] 构建验证通过

### ✅ Phase 1.4: 核心事件通知（100%）
- [x] 玩家死亡事件通知（PlayerDeath）
- [x] 玩家复活事件通知（PlayerRevive）
- [x] 怪物击杀事件通知（EnemyKilled）
- [x] 目标切换事件通知（TargetSwitched）
- [x] 集成到 `StepBattleCoordinator.AdvanceAll()`
- [x] 创建 `MockBattleNotificationService` 用于测试
- [x] 更新所有相关单元测试

### 🔄 Phase 1.5: 测试与验证（进行中）
- [x] 单元测试更新（已完成）
- [ ] 集成测试（待执行）
- [ ] 手动功能测试（待执行）
- [ ] 文档更新（本文档）

---

## 🏗️ 架构概览

### 服务器端组件

```
BlazorIdle.Server/
├── Config/
│   └── SignalROptions.cs          # SignalR 配置类
├── Hubs/
│   └── BattleNotificationHub.cs    # SignalR Hub
├── Services/
│   └── BattleNotificationService.cs # 通知服务实现
├── Application/Abstractions/
│   └── IBattleNotificationService.cs # 通知服务接口
└── Application/Battles/Step/
    └── StepBattleCoordinator.cs    # 集成事件检测逻辑
```

### 客户端组件

```
BlazorIdle/
├── Services/
│   └── BattleSignalRService.cs     # 客户端 SignalR 服务
├── wwwroot/
│   └── appsettings.json            # 客户端配置
└── Program.cs                       # 服务注册
```

---

## 🔧 配置说明

### 服务器端配置（appsettings.json）

```json
{
  "SignalR": {
    "Enabled": true,                        // 是否启用 SignalR
    "HubPath": "/hubs/battle",              // Hub 端点路径
    "ReconnectDelaySeconds": 5,             // 重连延迟（秒）
    "MaxReconnectAttempts": 5,              // 最大重连尝试次数
    "ConnectionTimeoutSeconds": 30,         // 连接超时时间（秒）
    "KeepAliveIntervalSeconds": 15,         // 心跳间隔（秒）
    "EnableDetailedErrors": false           // 是否启用详细错误信息（生产环境建议关闭）
  }
}
```

### 客户端配置（wwwroot/appsettings.json）

```json
{
  "ApiBaseUrl": "https://localhost:7056",
  "SignalR": {
    "Enabled": true,                        // 是否启用 SignalR
    "HubPath": "/hubs/battle",              // Hub 端点路径
    "ReconnectDelaySeconds": 5,             // 重连延迟（秒）
    "MaxReconnectAttempts": 5               // 最大重连尝试次数
  }
}
```

---

## 📡 事件通知流程

### 1. 服务器端事件检测

```csharp
// StepBattleCoordinator.cs
private void DetectAndNotifyBattleEvents(RunningBattle rb, int previousLastSegmentIndex)
{
    // 检查新生成的战斗段
    for (int i = previousLastSegmentIndex + 1; i < currentSegmentCount; i++)
    {
        var segment = rb.Segments[i];
        
        // 检查标签并发送通知
        if (segment.TagCounters.TryGetValue("player_death", out var count) && count > 0)
        {
            _ = _notificationService.NotifyStateChangeAsync(rb.Id, "PlayerDeath");
        }
        // ... 其他事件检测
    }
}
```

### 2. 通知服务发送

```csharp
// BattleNotificationService.cs
public async Task NotifyStateChangeAsync(Guid battleId, string eventType)
{
    var groupName = $"battle_{battleId}";
    var notification = new
    {
        eventType,
        timestamp = DateTime.UtcNow
    };

    await _hubContext.Clients
        .Group(groupName)
        .SendAsync("StateChanged", notification);
}
```

### 3. 客户端接收

```csharp
// BattleSignalRService.cs
_connection.On<StateChangedEvent>("StateChanged", evt =>
{
    // 触发注册的事件处理器
    foreach (var handler in _stateChangedHandlers)
    {
        handler(evt);
    }
});
```

---

## 🧪 测试指南

### 单元测试

所有现有测试已更新以支持新的 SignalR 依赖：

```csharp
// 使用 MockBattleNotificationService
var coordinator = TestHelpers.CreateCoordinator();
```

### 手动测试步骤（Phase 1.5）

#### 前置条件
1. 服务器配置中 `SignalR.Enabled = true`
2. 客户端配置中 `SignalR.Enabled = true`
3. 服务器和客户端均已构建

#### 测试用例 TC-001: SignalR 连接建立

**步骤**:
1. 启动服务器: `cd BlazorIdle.Server && dotnet run`
2. 启动客户端（浏览器访问）
3. 打开浏览器开发者工具 → 控制台

**预期结果**:
- 服务器日志显示: `Client {ConnectionId} connected to BattleNotificationHub`
- 浏览器控制台显示: `Connected to SignalR Hub at https://localhost:7056/hubs/battle`

#### 测试用例 TC-002: 玩家死亡通知

**步骤**:
1. 开始一场战斗（使用难度较高的敌人）
2. 等待玩家血量降为 0 并死亡
3. 观察浏览器控制台

**预期结果**:
- 控制台显示: `Received StateChanged event: PlayerDeath`
- 前端立即触发轮询，获取最新状态（如已集成）

#### 测试用例 TC-003: 怪物击杀通知

**步骤**:
1. 开始一场战斗（使用血量较低的敌人）
2. 等待击杀第一个敌人
3. 观察浏览器控制台

**预期结果**:
- 控制台显示: `Received StateChanged event: EnemyKilled`

#### 测试用例 TC-004: SignalR 降级

**步骤**:
1. 修改服务器配置: `SignalR.Enabled = false`
2. 重启服务器
3. 启动客户端并开始战斗

**预期结果**:
- 服务器日志显示: `SignalR is disabled, skipping notification for battle {BattleId}`
- 战斗功能正常，通过轮询更新状态

---

## 🚀 部署建议

### 开发环境
- 启用 `EnableDetailedErrors = true`
- 较短的重连延迟（2-5秒）
- 较多的重连尝试次数（10次）

### 生产环境
- 关闭 `EnableDetailedErrors = false`
- 标准重连延迟（5秒）
- 适中的重连尝试次数（5次）
- 监控连接失败和降级情况

---

## 📝 代码审查要点

### 1. 配置管理
- ✅ 所有 SignalR 参数均从配置文件读取
- ✅ 支持通过配置开关完全禁用 SignalR
- ✅ 开发/生产环境配置分离

### 2. 异常处理
- ✅ 通知发送失败不影响战斗主流程
- ✅ 使用 try-catch 包装所有事件处理器
- ✅ 记录错误日志但不抛出异常

### 3. 性能考虑
- ✅ 只检测新生成的战斗段，避免重复处理
- ✅ 使用异步通知（`_` 丢弃模式），不阻塞主循环
- ✅ 对于同一类型事件（如多次击杀），只通知一次

### 4. 测试覆盖
- ✅ 所有现有测试已更新
- ✅ 提供 Mock 实现用于隔离测试
- ✅ 测试辅助方法统一管理

---

## 🔮 后续演进（Phase 2）

### 进度条精准同步
- 添加 `NextSignificantEventAt` 到通知数据
- 前端根据服务器时间推进进度条
- 突发事件立即中断并重置进度条

### 详细事件数据
```csharp
// 从简化版本
NotifyStateChangeAsync(battleId, "EnemyKilled")

// 升级到详细版本
NotifyEventAsync(battleId, new EnemyKilledEventDto {
    BattleId = battleId,
    EventTime = currentTime,
    EnemyId = enemyId,
    Overkill = overkillDamage,
    Drops = dropRewards
})
```

### UI 反馈增强
- 死亡动画立即播放
- 击杀特效即时显示
- 进度条中断视觉反馈

---

## 📚 参考文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 完整设计文档
- [SignalR需求分析总结.md](./SignalR需求分析总结.md) - 需求分析
- [SignalR验收文档.md](./SignalR验收文档.md) - 验收标准
- [Microsoft SignalR 官方文档](https://learn.microsoft.com/en-us/aspnet/core/signalr/)

---

## 📞 联系与支持

如有问题或建议，请在 GitHub Issues 中提出。
