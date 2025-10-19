# SignalR 连接测试指南 (SignalR Connection Testing Guide)

## 快速测试步骤 (Quick Test Steps)

### 1. 启动服务器 (Start Server)

```bash
cd BlazorIdle.Server
dotnet run
```

服务器应该在以下端口启动 (Server should start on):
- HTTPS: `https://localhost:7056`
- HTTP: `http://localhost:5056`

### 2. 启动客户端 (Start Client)

在新的终端窗口中 (In a new terminal window):

```bash
cd BlazorIdle
dotnet run
```

客户端应该在以下端口启动 (Client should start on):
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

### 3. 登录并测试 SignalR (Login and Test SignalR)

1. 打开浏览器访问 `https://localhost:5001`
2. 注册或登录账号
3. 进入角色页面
4. 开始战斗

### 4. 检查连接状态 (Check Connection Status)

打开浏览器开发者工具 (按 F12)，查看：

#### Console (控制台)
应该看到类似的日志 (You should see logs like):
```
Connected to SignalR Hub: https://localhost:7056/hubs/battle
```

**不应该看到的错误 (You should NOT see errors like)**:
- ❌ `401 Unauthorized` - JWT 认证错误
- ❌ `CORS policy` - 跨域错误
- ❌ `Failed to connect` - 连接失败

#### Network (网络)
1. 切换到 "WS"（WebSocket）标签
2. 应该看到一个连接到 `/hubs/battle` 的 WebSocket 连接
3. 状态应该是 "101 Switching Protocols"（成功）

#### 请求详情 (Request Details)
查看 WebSocket 连接请求：
- URL: `wss://localhost:7056/hubs/battle?access_token=eyJ...`
- Headers 应该包含 `Origin: https://localhost:5001`
- Response status: `101`

### 5. 测试实时通信 (Test Real-time Communication)

启动战斗后，应该能看到：

1. **实时战斗日志**：
   - 攻击消息立即显示
   - 伤害数值实时更新
   - 敌人死亡/玩家复活通知

2. **Console 日志**：
   ```
   Received StateChanged event: BattleId=..., EventType=PlayerDeath
   Received StateChanged event: BattleId=..., EventType=EnemyKilled
   ```

3. **没有延迟**：
   - 事件应该在发生后立即显示
   - 不应该需要手动刷新页面

## 常见问题排查 (Troubleshooting)

### 问题 1: 401 Unauthorized

**症状 (Symptoms)**:
- Console 显示 "401 Unauthorized"
- SignalR 连接失败

**原因 (Cause)**:
- JWT token 未正确传递
- JWT 配置未处理查询字符串中的 token

**解决方案 (Solution)**:
- 确认 `Program.cs` 中包含 `OnMessageReceived` 事件处理器
- 检查 JWT token 是否有效（未过期）

### 问题 2: CORS Error

**症状 (Symptoms)**:
- Console 显示 CORS 相关错误
- 提示 "Origin https://localhost:5001 is not allowed"

**原因 (Cause)**:
- CORS 策略未包含客户端 URL
- 缺少 `.AllowCredentials()`

**解决方案 (Solution)**:
- 确认 CORS 策略包含客户端端口
- 确认启用了 `.AllowCredentials()`

### 问题 3: Connection Failed

**症状 (Symptoms)**:
- Console 显示 "Failed to connect to SignalR Hub"
- 网络请求失败

**原因 (Cause)**:
- 服务器未启动
- 端口配置错误
- 防火墙阻止

**解决方案 (Solution)**:
- 确认服务器正在运行
- 检查 `appsettings.json` 中的 `ApiBaseUrl` 配置
- 检查防火墙设置

### 问题 4: Token 过期

**症状 (Symptoms)**:
- 一段时间后连接断开
- 重连失败

**原因 (Cause)**:
- JWT token 过期（默认 24 小时）

**解决方案 (Solution)**:
- 重新登录获取新 token
- 考虑实现 token 自动刷新机制

## 验证清单 (Verification Checklist)

使用此清单确保 SignalR 正常工作：

- [ ] 服务器成功启动在 7056 端口
- [ ] 客户端成功启动在 5001 端口
- [ ] 用户可以登录
- [ ] Console 显示 "Connected to SignalR Hub"
- [ ] Network 显示 WebSocket 连接（101 状态）
- [ ] 没有 CORS 错误
- [ ] 没有 401 错误
- [ ] 战斗开始时收到实时通知
- [ ] 战斗日志实时更新
- [ ] 断开网络后自动重连

## 调试技巧 (Debug Tips)

### 启用详细日志 (Enable Detailed Logging)

1. 在 `appsettings.Development.json` 中：
```json
{
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

2. 在客户端 `appsettings.json` 中：
```json
{
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

### 使用浏览器开发者工具

1. **Network Tab**：
   - 过滤 WS (WebSocket)
   - 检查请求/响应头
   - 查看消息内容

2. **Console Tab**：
   - 查看 SignalR 日志
   - 检查错误消息
   - 监控事件接收

3. **Application Tab**：
   - 检查 Local Storage 中的 token
   - 验证 token 格式和内容

## 性能测试 (Performance Testing)

### 测试延迟 (Test Latency)

1. 记录客户端接收事件的时间戳
2. 与服务器发送时间比较
3. 延迟应该 < 100ms

### 测试重连 (Test Reconnection)

1. 断开网络连接
2. 等待 5-10 秒
3. 恢复网络连接
4. 应该自动重连成功

### 测试并发 (Test Concurrency)

1. 打开多个浏览器标签
2. 每个标签登录不同账号
3. 同时启动战斗
4. 所有标签都应该收到各自的实时通知

## 监控和日志 (Monitoring and Logs)

### 服务器日志位置

查看服务器控制台输出，应该看到：
```
[Information] Client connected: {ConnectionId}
[Debug] Client {ConnectionId} subscribed to battle {BattleId}
[Debug] Notifying StateChanged event to battle {BattleId}
```

### 客户端日志位置

查看浏览器 Console，应该看到：
```
Connected to SignalR Hub: https://localhost:7056/hubs/battle
Subscribed to battle {BattleId}
Received StateChanged event: ...
```

## 相关配置文件 (Related Configuration Files)

- `BlazorIdle.Server/appsettings.json` - 服务器 SignalR 配置
- `BlazorIdle.Server/appsettings.Development.json` - 开发环境配置
- `BlazorIdle/wwwroot/appsettings.json` - 客户端配置
- `BlazorIdle.Server/Program.cs` - JWT 和 CORS 配置
- `BlazorIdle/Services/BattleSignalRService.cs` - 客户端服务

## 联系支持 (Contact Support)

如果问题仍然存在，请提供以下信息：
- 浏览器类型和版本
- Console 错误日志
- Network 请求详情
- 服务器日志输出
