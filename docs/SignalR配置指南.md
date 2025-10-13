# BlazorIdle SignalR 配置指南

**版本**: 1.0  
**日期**: 2025-10-13  
**适用范围**: Phase 1-2.5

---

## 📋 目录

1. [配置概览](#配置概览)
2. [服务器端配置](#服务器端配置)
3. [客户端配置](#客户端配置)
4. [环境差异化配置](#环境差异化配置)
5. [配置参数详解](#配置参数详解)
6. [配置验证](#配置验证)
7. [常见场景配置](#常见场景配置)
8. [故障排查](#故障排查)

---

## 配置概览

SignalR 系统采用配置文件驱动设计，所有参数从 `appsettings.json` 读取。支持：

- ✅ 开发/生产环境差异化配置
- ✅ 启动时自动验证配置
- ✅ 热更新（部分参数）
- ✅ 向后兼容

### 配置文件位置

| 类型 | 位置 | 用途 |
|------|------|------|
| 服务器端 | `BlazorIdle.Server/appsettings.json` | 服务器 SignalR 配置 |
| 服务器端（开发） | `BlazorIdle.Server/appsettings.Development.json` | 开发环境覆盖 |
| 服务器端（生产） | `BlazorIdle.Server/appsettings.Production.json` | 生产环境覆盖 |
| 客户端 | `BlazorIdle/wwwroot/appsettings.json` | 客户端 SignalR 配置 |
| 客户端（开发） | `BlazorIdle/wwwroot/appsettings.Development.json` | 开发环境覆盖 |
| 客户端（生产） | `BlazorIdle/wwwroot/appsettings.Production.json` | 生产环境覆盖 |

---

## 服务器端配置

### 配置示例

**文件**: `BlazorIdle.Server/appsettings.json`

```json
{
  "SignalR": {
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "EnableDetailedLogging": false,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30
  }
}
```

### 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| HubEndpoint | string | "/hubs/battle" | SignalR Hub 端点路径 |
| EnableSignalR | bool | true | 是否启用 SignalR |
| MaxReconnectAttempts | int | 5 | 最大重连次数 (0-20) |
| ReconnectBaseDelayMs | int | 1000 | 重连基础延迟（毫秒，100-10000） |
| EnableDetailedLogging | bool | false | 是否启用详细日志 |
| ConnectionTimeoutSeconds | int | 30 | 连接超时时间（秒，1-300） |
| KeepAliveIntervalSeconds | int | 15 | 保持连接间隔（秒，1-ServerTimeout） |
| ServerTimeoutSeconds | int | 30 | 服务器超时时间（秒，1-600） |

### 验证规则

服务器启动时会自动验证配置，确保：

1. **HubEndpoint** 不为空且以 '/' 开头
2. **MaxReconnectAttempts** 在 0-20 之间
3. **ReconnectBaseDelayMs** 在 100-10000 之间
4. **ConnectionTimeoutSeconds** 在 1-300 之间
5. **KeepAliveIntervalSeconds** 不超过 ServerTimeoutSeconds
6. **ServerTimeoutSeconds** 至少是 KeepAliveIntervalSeconds 的 2 倍

验证失败会抛出异常，应用无法启动。

---

## 客户端配置

### 配置示例

**文件**: `BlazorIdle/wwwroot/appsettings.json`

```json
{
  "ApiBaseUrl": "https://localhost:7056",
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
    "EnableAutomaticReconnect": true,
    "ReconnectFailedWaitMs": 5000,
    "AutoConnectOnStartup": false,
    "ConnectionCheckIntervalMs": 10000
  }
}
```

### 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| HubEndpoint | string | "/hubs/battle" | SignalR Hub 端点路径（相对路径） |
| EnableSignalR | bool | true | 是否启用 SignalR |
| MaxReconnectAttempts | int | 5 | 最大重连次数 |
| ReconnectBaseDelayMs | int | 1000 | 重连基础延迟（毫秒） |
| MaxReconnectDelayMs | int | 30000 | 最大重连延迟（毫秒） |
| EnableDetailedLogging | bool | false | 是否启用详细日志 |
| ConnectionTimeoutSeconds | int | 30 | 连接超时时间（秒） |
| KeepAliveIntervalSeconds | int | 15 | 保持连接间隔（秒） |
| ServerTimeoutSeconds | int | 30 | 服务器超时时间（秒） |
| EnableAutomaticReconnect | bool | true | 是否自动重连 |
| ReconnectFailedWaitMs | int | 5000 | 重连失败后等待时间（毫秒） |
| AutoConnectOnStartup | bool | false | 是否在启动时自动连接 |
| ConnectionCheckIntervalMs | int | 10000 | 连接状态检查间隔（毫秒） |

### 完整 URL 构建

客户端会自动组合 `ApiBaseUrl` 和 `HubEndpoint`：

```
完整 URL = ApiBaseUrl + HubEndpoint
例如: https://localhost:7056/hubs/battle
```

---

## 环境差异化配置

### 开发环境 (Development)

**特点**: 更详细的日志、更宽松的超时、更多的重连次数

**配置示例** (`appsettings.Development.json`):

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "MaxReconnectAttempts": 10,
    "ReconnectBaseDelayMs": 500,
    "MaxReconnectDelayMs": 15000,
    "ConnectionTimeoutSeconds": 60,
    "KeepAliveIntervalSeconds": 10,
    "ServerTimeoutSeconds": 60,
    "ConnectionCheckIntervalMs": 5000
  }
}
```

**优势**:
- 详细日志便于调试
- 更宽松的超时适应调试场景
- 更频繁的重连快速发现问题

### 生产环境 (Production)

**特点**: 优化性能、减少日志、严格超时

**配置示例** (`appsettings.Production.json`):

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    "ConnectionCheckIntervalMs": 10000
  }
}
```

**优势**:
- 减少日志开销
- 合理的重连策略平衡体验和负载
- 严格超时避免资源浪费

---

## 配置参数详解

### 连接参数

#### HubEndpoint (端点路径)

**用途**: SignalR Hub 的 URL 路径

**格式**: 必须以 '/' 开头的相对路径

**示例**:
```json
"HubEndpoint": "/hubs/battle"
```

**注意事项**:
- 服务器端和客户端必须一致
- 修改后需要重启应用

#### EnableSignalR (启用开关)

**用途**: 全局开关，可用于降级到纯轮询

**取值**: `true` (启用) / `false` (禁用)

**使用场景**:
- 故障排查时临时禁用
- 某些网络环境不支持 WebSocket
- 降级到纯轮询模式

**示例**:
```json
"EnableSignalR": false  // 禁用后，系统自动降级到轮询
```

### 重连参数

#### MaxReconnectAttempts (最大重连次数)

**用途**: 连接断开后的最大重连尝试次数

**范围**: 0-20

**推荐值**:
- 桌面端: 5 次（网络稳定）
- 移动端: 10 次（网络不稳定）
- 开发环境: 10 次（便于调试）

**示例**:
```json
"MaxReconnectAttempts": 5
```

#### ReconnectBaseDelayMs (重连基础延迟)

**用途**: 指数退避算法的基础延迟

**范围**: 100-10000 毫秒

**算法**: 延迟 = ReconnectBaseDelayMs * 2^重试次数

**示例** (基础延迟 1000ms):
- 第 1 次: 1000ms (1s)
- 第 2 次: 2000ms (2s)
- 第 3 次: 4000ms (4s)
- 第 4 次: 8000ms (8s)
- 第 5 次: 16000ms (16s)

#### MaxReconnectDelayMs (最大重连延迟)

**用途**: 限制指数退避的最大延迟

**范围**: 1000-60000 毫秒

**推荐值**: 30000 毫秒 (30 秒)

**作用**: 防止延迟无限增长

**示例**:
```json
"MaxReconnectDelayMs": 30000  // 最多等待 30 秒
```

### 超时参数

#### ConnectionTimeoutSeconds (连接超时)

**用途**: 建立连接的最大等待时间

**范围**: 1-300 秒

**推荐值**:
- 开发环境: 60 秒
- 生产环境: 30 秒
- 移动端: 45 秒

#### KeepAliveIntervalSeconds (保持连接间隔)

**用途**: 发送心跳包的间隔时间

**范围**: 1 秒到 ServerTimeoutSeconds

**推荐值**: ServerTimeoutSeconds 的 1/2

**作用**: 保持连接活跃，及时检测断开

**示例**:
```json
"KeepAliveIntervalSeconds": 15,
"ServerTimeoutSeconds": 30
```

#### ServerTimeoutSeconds (服务器超时)

**用途**: 服务器判定客户端断开的时间

**范围**: 1-600 秒

**约束**: 必须 ≥ 2 * KeepAliveIntervalSeconds

**推荐值**: 2 * KeepAliveIntervalSeconds

### 日志参数

#### EnableDetailedLogging (详细日志)

**用途**: 控制 SignalR 日志的详细程度

**取值**: `true` (详细) / `false` (简略)

**影响**:
- `true`: 输出调试级别日志，包含连接细节
- `false`: 仅输出信息级别日志，减少开销

**推荐**:
- 开发环境: `true`
- 生产环境: `false`

### 客户端特有参数

#### EnableAutomaticReconnect (自动重连)

**用途**: 是否启用自动重连功能

**取值**: `true` (启用) / `false` (禁用)

**推荐**: 保持 `true`

#### AutoConnectOnStartup (启动自动连接)

**用途**: 应用启动时是否自动连接

**取值**: `true` (自动) / `false` (手动)

**推荐**: `false` (根据需要手动连接)

#### ConnectionCheckIntervalMs (连接检查间隔)

**用途**: 定期检查连接状态的间隔

**范围**: 1000-60000 毫秒

**推荐值**: 10000 毫秒 (10 秒)

---

## 配置验证

### 自动验证

服务器启动时会自动验证配置：

```csharp
var validationResult = SignalROptionsValidator.Validate(options);
if (!validationResult.IsValid)
{
    throw new InvalidOperationException(
        $"Invalid SignalR configuration: {validationResult.GetErrorMessage()}"
    );
}
```

### 验证失败示例

**错误配置**:
```json
{
  "SignalR": {
    "HubEndpoint": "hubs/battle",  // 缺少前导 '/'
    "MaxReconnectAttempts": 25,    // 超过上限 20
    "KeepAliveIntervalSeconds": 40,
    "ServerTimeoutSeconds": 30     // KeepAlive 超过 ServerTimeout
  }
}
```

**错误信息**:
```
Invalid SignalR configuration: 
HubEndpoint must start with '/'; 
MaxReconnectAttempts should not exceed 20; 
KeepAliveIntervalSeconds should not exceed ServerTimeoutSeconds; 
ServerTimeoutSeconds should be at least twice KeepAliveIntervalSeconds
```

---

## 常见场景配置

### 场景 1: 桌面网页应用（标准）

**特点**: 网络稳定、延迟低

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30
  }
}
```

### 场景 2: 移动端应用

**特点**: 网络不稳定、可能频繁切换网络

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 10,
    "ReconnectBaseDelayMs": 500,
    "MaxReconnectDelayMs": 20000,
    "ConnectionTimeoutSeconds": 45,
    "KeepAliveIntervalSeconds": 10,
    "ServerTimeoutSeconds": 30
  }
}
```

### 场景 3: 开发调试

**特点**: 需要详细日志、更长的超时

```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "MaxReconnectAttempts": 10,
    "ReconnectBaseDelayMs": 500,
    "ConnectionTimeoutSeconds": 60,
    "KeepAliveIntervalSeconds": 10,
    "ServerTimeoutSeconds": 60
  }
}
```

### 场景 4: 高负载生产环境

**特点**: 减少服务器负担、优化性能

```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "MaxReconnectAttempts": 3,
    "ReconnectBaseDelayMs": 2000,
    "MaxReconnectDelayMs": 30000,
    "ConnectionTimeoutSeconds": 20,
    "KeepAliveIntervalSeconds": 20,
    "ServerTimeoutSeconds": 40
  }
}
```

### 场景 5: 临时禁用 SignalR

**特点**: 故障排查或降级

```json
{
  "SignalR": {
    "EnableSignalR": false
  }
}
```

**效果**: 系统自动降级到纯轮询模式

---

## 故障排查

### 常见问题

#### 1. 启动失败：配置验证错误

**症状**: 应用启动时抛出异常

**原因**: 配置参数不符合验证规则

**解决**:
1. 检查错误消息中的具体问题
2. 参考本文档的参数说明调整配置
3. 确保所有必需参数都已设置

#### 2. 连接不上 SignalR Hub

**症状**: 客户端无法连接

**检查清单**:
- [ ] EnableSignalR 是否为 true
- [ ] ApiBaseUrl 和 HubEndpoint 是否正确
- [ ] 服务器端 Hub 是否正确映射
- [ ] 网络是否可达
- [ ] 防火墙是否允许 WebSocket

**调试**:
```json
"EnableDetailedLogging": true  // 启用详细日志查看详情
```

#### 3. 频繁断开重连

**症状**: 连接不稳定，频繁重连

**可能原因**:
- KeepAliveInterval 太长
- ServerTimeout 太短
- 网络不稳定

**调整建议**:
```json
{
  "KeepAliveIntervalSeconds": 10,  // 减小
  "ServerTimeoutSeconds": 40,      // 增大
  "MaxReconnectAttempts": 10       // 增加重连次数
}
```

#### 4. 重连失败

**症状**: 达到最大重连次数后放弃

**调整建议**:
```json
{
  "MaxReconnectAttempts": 10,     // 增加次数
  "ReconnectBaseDelayMs": 500,    // 减小延迟
  "MaxReconnectDelayMs": 20000    // 减小最大延迟
}
```

---

## 最佳实践

### 1. 环境差异化

始终为不同环境创建专门的配置文件：
- `appsettings.Development.json`: 开发环境
- `appsettings.Production.json`: 生产环境

### 2. 合理的超时设置

```
ServerTimeoutSeconds >= 2 * KeepAliveIntervalSeconds
```

### 3. 分层重连策略

- 移动端: 更多次数、更短延迟
- 桌面端: 适中次数、适中延迟
- 服务器端: 较少次数、较长延迟

### 4. 日志级别控制

- 开发: EnableDetailedLogging = true
- 生产: EnableDetailedLogging = false

### 5. 降级方案

始终保留 `EnableSignalR = false` 的降级选项

---

## 附录

### A. 配置模板

#### 最小配置
```json
{
  "SignalR": {
    "HubEndpoint": "/hubs/battle"
  }
}
```

#### 完整配置
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
    "EnableAutomaticReconnect": true,
    "ReconnectFailedWaitMs": 5000,
    "AutoConnectOnStartup": false,
    "ConnectionCheckIntervalMs": 10000
  }
}
```

### B. 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md)
- [SignalR_Phase2.5_配置增强完成报告.md](./SignalR_Phase2.5_配置增强完成报告.md)
- [SignalR优化进度更新.md](./SignalR优化进度更新.md)

---

**最后更新**: 2025-10-13  
**维护者**: GitHub Copilot Agent
