# SignalR 快速开始测试

**版本**: 1.0  
**日期**: 2025-10-14  
**目的**: 快速验证 SignalR 连接优化是否正常工作

---

## 🚀 快速测试步骤

### 步骤 1: 启动服务器

打开终端，执行：

```bash
cd BlazorIdle.Server
dotnet run --urls "https://localhost:7056"
```

等待服务器启动，看到类似输出：
```
Now listening on: https://localhost:7056
Application started. Press Ctrl+C to shut down.
```

---

### 步骤 2: 打开浏览器

1. 打开 Chrome 或 Edge 浏览器
2. 按 **F12** 打开开发者工具
3. 切换到 **Console** 标签
4. 访问 `https://localhost:7056`

> 💡 提示：如果出现证书警告，点击"高级" → "继续访问"

---

### 步骤 3: 登录并观察

#### 3.1 登录界面

如果看到登录页面：
1. 输入用户名和密码（或注册新账号）
2. 点击"登录"
3. **观察控制台输出**

#### 3.2 预期日志

登录成功后，控制台应显示：

```
[SignalR] 认证成功，正在连接...
[SignalR] 连接成功，实时通知已启用
```

页面右上角应显示绿色通知："✅ 实时通知已启用"

---

### 步骤 4: 验证 WebSocket 连接

在开发者工具中：

1. 切换到 **Network** 标签
2. 过滤器选择 **WS**（WebSocket）
3. 应该看到一个连接到 `/hubs/battle` 的 WebSocket
4. 状态应为 **101 Switching Protocols**

✅ 如果看到这个连接，说明 SignalR 已成功连接！

---

### 步骤 5: 测试战斗事件

#### 5.1 开始战斗

1. 如果没有角色，先创建一个角色
2. 展开 "⚔️ Step战斗测试" 部分
3. 选择一个敌人（如 "dummy"）
4. 设置战斗时间（如 30 秒）
5. 点击 "开始战斗"

#### 5.2 观察事件

**控制台输出**:
```
[SignalR] 收到事件: EnemyKilled, BattleId: xxx
[SignalR] 收到事件: PlayerDeath, BattleId: xxx
[SignalR] 收到事件: PlayerRevive, BattleId: xxx
[SignalR] 收到事件: TargetSwitched, BattleId: xxx
```

**页面通知**:
- 💀 角色死亡 - 5秒后复活
- ✨ 角色已复活
- ⚔️ 击杀敌人
- 🎯 目标切换

✅ 如果看到这些事件，说明实时推送工作正常！

---

### 步骤 6: 测试登出

1. 点击右上角 "登出" 按钮
2. **观察控制台输出**

**预期日志**:
```
[SignalR] 登出，断开连接
```

3. 在 Network 标签中，WebSocket 连接应被关闭

✅ 如果连接正确关闭，说明资源清理正常！

---

## 🎯 成功标准

如果以上步骤都成功，您应该看到：

- ✅ 登录后 SignalR 自动连接
- ✅ WebSocket 连接建立成功
- ✅ 战斗事件实时推送到前端
- ✅ 事件延迟 <1 秒
- ✅ 登出时正确断开连接

---

## ❌ 常见问题

### 问题 1: 连接失败

**症状**:
```
[SignalR] 连接失败，使用轮询模式
```

**可能原因**:
- 服务器未启动
- 端口配置不匹配
- 网络问题

**解决方法**:
1. 确认服务器运行在 `https://localhost:7056`
2. 检查 `BlazorIdle/wwwroot/appsettings.json` 中的 `ApiBaseUrl`
3. 尝试刷新页面

---

### 问题 2: 看不到事件

**症状**:
- WebSocket 已连接
- 但看不到战斗事件

**可能原因**:
- 战斗未正确订阅
- 服务器端未发送事件

**解决方法**:
1. 确认战斗已开始
2. 查看服务器端日志
3. 检查 Network → WS → Messages 标签

---

### 问题 3: 证书错误

**症状**:
```
NET::ERR_CERT_AUTHORITY_INVALID
```

**解决方法**:
1. 点击"高级"
2. 点击"继续访问 localhost (不安全)"
3. 或者安装开发证书：
   ```bash
   dotnet dev-certs https --trust
   ```

---

## 🔍 调试技巧

### 1. 过滤日志

在 Console 中输入过滤器：
```
[SignalR]
```

只显示 SignalR 相关日志。

### 2. 查看消息

在 Network → WS → Messages 标签中：
- 向下箭头 ⬇️ = 接收的消息
- 向上箭头 ⬆️ = 发送的消息

### 3. 启用详细日志

编辑 `BlazorIdle/wwwroot/appsettings.json`:
```json
"SignalR": {
  "EnableDetailedLogging": true,
  ...
}
```

刷新页面，将看到更详细的日志。

---

## 📝 测试检查清单

快速检查表：

- [ ] 服务器启动成功
- [ ] 浏览器打开并登录
- [ ] 控制台显示 "[SignalR] 连接成功"
- [ ] Network 中看到 WebSocket 连接
- [ ] 开始战斗后能看到事件日志
- [ ] 页面显示事件通知
- [ ] 事件延迟 <1 秒
- [ ] 登出后连接断开

---

## 📚 更多信息

详细测试场景请参考：
- [SignalR_连接优化测试指南.md](./SignalR_连接优化测试指南.md)

完整实施报告：
- [SignalR_连接优化完成报告.md](./SignalR_连接优化完成报告.md)

进度跟踪：
- [SignalR优化进度更新.md](./SignalR优化进度更新.md)

---

## ✅ 测试成功！

如果所有步骤都通过，恭喜！SignalR 连接优化已成功实现。

您现在可以：
1. 继续测试其他场景
2. 验证性能指标
3. 体验实时战斗通知的流畅性

---

## 📞 反馈

如果遇到问题，请：
1. 检查控制台错误日志
2. 查看服务器端日志
3. 参考故障排查部分
4. 记录问题并提供日志

---

**创建人**: GitHub Copilot Agent  
**创建日期**: 2025-10-14  
**用途**: 快速验证 SignalR 优化效果
