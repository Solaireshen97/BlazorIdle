# SignalR 客户端配置说明

## 配置文件
- `signalr.json` - 基础配置（所有环境通用）
- `signalr.Development.json` - 开发环境特定配置
- `signalr.Production.json` - 生产环境特定配置

## 配置项说明

### 连接配置
| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `HubEndpoint` | string | "/hubs/battle" | SignalR Hub 端点路径 |
| `EnableSignalR` | bool | true | 是否启用 SignalR |
| `ConnectionTimeoutSeconds` | int | 30 | 连接超时时间（秒） |
| `KeepAliveIntervalSeconds` | int | 15 | 保活间隔（秒） |
| `ServerTimeoutSeconds` | int | 30 | 服务器超时时间（秒） |

### 重连配置
| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `AutoReconnect` | bool | true | 是否自动重连 |
| `MaxReconnectAttempts` | int | 5 | 最大重连次数 |
| `ReconnectBaseDelayMs` | int | 1000 | 基础重连延迟（毫秒） |
| `MaxReconnectDelayMs` | int | 30000 | 最大重连延迟（毫秒） |

### 日志和通知
| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `EnableDetailedLogging` | bool | false | 是否启用详细日志 |
| `ConnectionStatusNotifications` | bool | true | 是否显示连接状态通知 |

## 重连策略
采用指数退避策略：
1. 第1次重连：1秒后
2. 第2次重连：2秒后
3. 第3次重连：4秒后
4. 第4次重连：8秒后
5. 第5次重连：16秒后

最大延迟受 `MaxReconnectDelayMs` 限制。

## 环境配置
- **开发环境**：启用详细日志，显示所有连接状态通知
- **生产环境**：禁用详细日志，增加重连次数和最大延迟

## 使用示例
```json
{
  "SignalR": {
    "EnableSignalR": true,
    "MaxReconnectAttempts": 10,
    "ConnectionStatusNotifications": false
  }
}
```

## 注意事项
1. 配置文件按以下顺序加载（后加载的会覆盖先加载的）：
   - `appsettings.json`
   - `signalr.json`
   - `signalr.{Environment}.json`

2. 修改配置后需要重新构建应用才能生效

3. 连接失败时会自动降级到纯轮询模式，不影响核心功能
