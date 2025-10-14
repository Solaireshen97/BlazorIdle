# SignalR 前端集成测试指南

**创建日期**: 2025-10-14  
**版本**: 1.0  
**适用阶段**: Phase 2 前端集成完成后

---

## 📋 概述

本指南提供 SignalR 前端集成的测试步骤和验证方法，帮助确认实时通知功能正常工作。

---

## 🎯 测试目标

1. ✅ 验证 SignalR 连接成功建立
2. ✅ 验证战斗事件实时通知
3. ✅ 验证降级策略（SignalR 不可用时）
4. ✅ 验证通知延迟（目标 <1s）
5. ✅ 验证资源清理

---

## 🚀 测试环境准备

### 1. 启动后端服务

```bash
cd BlazorIdle.Server
dotnet run
```

**预期输出**:
```
info: BlazorIdle.Server.Config.SignalRStartupValidator[0]
      ✅ SignalR 配置验证通过
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7056
```

### 2. 启动前端应用

```bash
cd BlazorIdle
dotnet run
```

**预期输出**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
```

### 3. 打开浏览器开发者工具

- 按 `F12` 打开开发者工具
- 切换到 **Console** 标签页
- 切换到 **Network** 标签页 → 过滤 **WS**（WebSocket）

---

## 🧪 测试场景

### 场景 1: SignalR 连接建立

**步骤**:
1. 打开浏览器访问 `https://localhost:5001`
2. 登录账号
3. 观察页面通知和控制台输出

**预期结果**:
- ✅ 页面显示绿色通知："实时通知已启用"（2秒后消失）
- ✅ 控制台无错误信息
- ✅ Network 标签页显示 WebSocket 连接（状态 101 Switching Protocols）

**失败情况**:
- ⚠️ 页面显示黄色通知："实时通知不可用，使用轮询模式"
  - 原因：后端 SignalR 未启动或配置错误
  - 解决：检查后端 appsettings.json 中 `"EnableSignalR": true`

---

### 场景 2: Step 战斗事件通知

**步骤**:
1. 创建或选择一个角色
2. 滚动到 **2. Step 战斗** 部分
3. 配置战斗参数：
   - 模式：duration
   - 时长：30 秒
   - 敌人数量：1
   - 轮询间隔：500ms
4. 点击 **Start** 按钮
5. 观察战斗过程中的通知

**预期结果**:
- ✅ 控制台输出: `[SignalR] 收到事件: EnemyKilled, BattleId: ...`
- ✅ 页面显示通知: "⚔️ 击杀敌人"（2秒后消失）
- ✅ 战斗状态立即更新（无需等待下次轮询）
- ✅ 敌人血量和击杀计数实时同步

**如果角色死亡**:
- ✅ 控制台输出: `[SignalR] 收到事件: PlayerDeath, BattleId: ...`
- ✅ 页面显示通知: "💀 角色死亡，5秒后复活"（3秒）
- ✅ 5秒后显示通知: "✨ 角色已复活"（2秒）

**如果目标切换**:
- ✅ 控制台输出: `[SignalR] 收到事件: TargetSwitched, BattleId: ...`
- ✅ 页面显示通知: "🎯 目标切换"（2秒）

---

### 场景 3: 活动计划战斗事件通知

**步骤**:
1. 滚动到 **3. 活动计划** 部分
2. 配置活动参数：
   - 活动类型：combat
   - 限制类型：duration
   - 限制值：60 秒
   - 槽位索引：0
   - 敌人数量：1
3. 点击 **创建活动计划** 按钮
4. 点击计划旁边的 **查看战斗** 按钮
5. 观察战斗过程中的通知

**预期结果**:
- ✅ 收到 EnemyKilled、PlayerDeath、TargetSwitched 等事件通知
- ✅ 战斗状态实时更新
- ✅ 通知与 Step 战斗表现一致

---

### 场景 4: 降级策略测试

**步骤**:
1. 停止后端服务（Ctrl+C）
2. 刷新浏览器页面
3. 登录账号
4. 观察通知和功能

**预期结果**:
- ⚠️ 页面显示黄色通知："实时通知不可用，使用轮询模式"（3秒）
- ✅ 战斗功能正常（使用轮询模式）
- ✅ 无 JavaScript 错误
- ✅ 页面不卡顿

**恢复测试**:
1. 重新启动后端服务
2. 刷新浏览器页面
3. 登录账号

**预期结果**:
- ✅ 页面显示绿色通知："实时通知已启用"
- ✅ SignalR 功能恢复

---

### 场景 5: 通知延迟测试

**目标**: 验证通知延迟 <1 秒

**步骤**:
1. 启动 Step 战斗（30秒，1个敌人）
2. 打开浏览器开发者工具的 Console
3. 观察敌人死亡时的时间戳

**验证方法**:
```javascript
// 在控制台输入以下代码以查看时间戳
console.log = (function(oldLog) {
    return function() {
        var args = Array.from(arguments);
        args.unshift(new Date().toISOString() + ' -');
        oldLog.apply(console, args);
    };
})(console.log);
```

**预期结果**:
- ✅ 敌人死亡时间戳 与 SignalR 事件接收时间戳 差异 <1s
- ✅ SignalR 事件接收时间戳 与 页面通知显示时间戳 差异 <0.1s

**示例输出**:
```
2025-10-14T02:10:00.123Z - [SignalR] 收到事件: EnemyKilled, BattleId: ...
2025-10-14T02:10:00.125Z - ⚔️ 击杀敌人
```

---

### 场景 6: 资源清理测试

**步骤**:
1. 启动 Step 战斗
2. 观察 Network 标签页的 WebSocket 连接
3. 点击浏览器的刷新按钮（或导航到其他页面）
4. 观察 WebSocket 连接状态

**预期结果**:
- ✅ WebSocket 连接正常关闭（状态变为 Closed）
- ✅ 无连接泄漏
- ✅ 无控制台错误

---

## 📊 验收标准

| 测试项 | 期望结果 | 通过标准 |
|-------|---------|---------|
| **连接建立** | 显示成功通知 | ✅ 必须 |
| **EnemyKilled 通知** | <1s 延迟 | ✅ 必须 |
| **PlayerDeath 通知** | <1s 延迟 | ✅ 必须 |
| **TargetSwitched 通知** | <1s 延迟 | ✅ 必须 |
| **降级策略** | 无错误，功能正常 | ✅ 必须 |
| **资源清理** | 无泄漏 | ✅ 必须 |
| **通知样式** | 与设计一致 | ⚠️ 可选 |
| **性能影响** | FPS >30 | ⚠️ 可选 |

---

## 🐛 常见问题排查

### 问题 1: SignalR 连接失败

**现象**: 页面显示 "实时通知不可用"

**排查步骤**:
1. 检查后端是否运行：访问 `https://localhost:7056/swagger`
2. 检查 appsettings.json 配置：`"EnableSignalR": true`
3. 检查控制台错误信息
4. 检查 CORS 配置（后端 Program.cs）

**解决方案**:
```csharp
// 后端 Program.cs 确保 CORS 包含 SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins("https://localhost:5001", "http://localhost:5001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR 需要
    });
});
```

---

### 问题 2: 收不到事件通知

**现象**: 战斗进行但无 SignalR 通知

**排查步骤**:
1. 检查控制台是否有 `[SignalR]` 输出
2. 检查 Network 标签页的 WebSocket 连接状态
3. 检查后端日志是否有 NotifyStateChangeAsync 调用
4. 检查 signalr-config.json 中事件类型是否启用

**解决方案**:
```json
// signalr-config.json 确保事件启用
{
  "Notification": {
    "EnablePlayerDeathNotification": true,
    "EnablePlayerReviveNotification": true,
    "EnableEnemyKilledNotification": true,
    "EnableTargetSwitchedNotification": true
  }
}
```

---

### 问题 3: 通知延迟过高

**现象**: 延迟 >2 秒

**排查步骤**:
1. 检查网络延迟（ping localhost）
2. 检查后端性能（CPU/内存）
3. 检查轮询间隔设置

**解决方案**:
- 减少轮询间隔（不推荐 <200ms）
- 优化后端性能
- 检查是否有大量并发战斗

---

## 📝 测试报告模板

```markdown
# SignalR 前端集成测试报告

**测试日期**: YYYY-MM-DD  
**测试人**: XXX  
**环境**: Development / Production  

## 测试结果

| 场景 | 状态 | 备注 |
|-----|------|------|
| SignalR 连接建立 | ✅ / ❌ | |
| Step 战斗事件通知 | ✅ / ❌ | |
| 活动计划战斗通知 | ✅ / ❌ | |
| 降级策略 | ✅ / ❌ | |
| 通知延迟 | XXms | 目标 <1s |
| 资源清理 | ✅ / ❌ | |

## 发现的问题

1. [问题描述]
   - 重现步骤：
   - 预期结果：
   - 实际结果：
   - 截图：

## 改进建议

1. [建议内容]

## 总体评价

- 功能完整性: XX%
- 性能表现: 优/良/中/差
- 用户体验: 优/良/中/差
- 推荐上线: 是/否
```

---

## 🔗 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 技术设计
- [SignalR系统当前状态与下一步建议.md](./SignalR系统当前状态与下一步建议.md) - 当前状态
- [SignalR优化进度更新.md](./SignalR优化进度更新.md) - 实施进度
- [SignalR配置优化指南.md](./SignalR配置优化指南.md) - 配置说明

---

**创建人**: GitHub Copilot Agent  
**创建日期**: 2025-10-14  
**适用版本**: Phase 2 前端集成完成
