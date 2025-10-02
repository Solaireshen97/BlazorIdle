# 架构设计文档 (Architecture Documentation)

## 项目架构概览

本项目采用前后端分离的架构设计，包含以下主要组件：

```
┌─────────────────────────────────────────────────────────────┐
│                         浏览器 (Browser)                      │
├─────────────────────────────────────────────────────────────┤
│           Blazor WebAssembly 客户端 (Client)                 │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Home.razor │  │Counter.razor │  │Weather.razor │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                              │
│  ┌───────────────────────────────────────────────────┐      │
│  │          GameManager.razor (新增)                  │      │
│  │    - 创建游戏数据                                   │      │
│  │    - 查看游戏数据列表                               │      │
│  │    - 删除游戏数据                                   │      │
│  └───────────────────────────────────────────────────┘      │
│                         ↓ HTTP/JSON                          │
└─────────────────────────────────────────────────────────────┘

                              ↓
                      CORS 允许跨域请求
                              ↓

┌─────────────────────────────────────────────────────────────┐
│        ASP.NET Core Web API 服务端 (Server)                  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌───────────────────────────────────────────────────┐      │
│  │          GameDataController (API控制器)            │      │
│  │                                                     │      │
│  │  GET    /api/GameData         - 获取所有数据       │      │
│  │  GET    /api/GameData/{id}    - 获取单条数据       │      │
│  │  POST   /api/GameData         - 创建新数据         │      │
│  │  PUT    /api/GameData/{id}    - 更新数据          │      │
│  │  DELETE /api/GameData/{id}    - 删除数据          │      │
│  └───────────────────────────────────────────────────┘      │
│                         ↓                                    │
│  ┌───────────────────────────────────────────────────┐      │
│  │        GameDbContext (数据库上下文)                │      │
│  │        - Entity Framework Core                     │      │
│  │        - SQLite Provider                           │      │
│  └───────────────────────────────────────────────────┘      │
│                         ↓                                    │
└─────────────────────────────────────────────────────────────┘

                              ↓

┌─────────────────────────────────────────────────────────────┐
│              SQLite 数据库 (gamedata.db)                     │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Table: GameData                                             │
│  ┌──────────┬──────────────┬─────────┬─────────┬───────────┐│
│  │ Id (PK)  │ PlayerName   │ Score   │ Level   │LastUpdated││
│  ├──────────┼──────────────┼─────────┼─────────┼───────────┤│
│  │ 1        │ 玩家1        │ 1000    │ 5       │2025-10-02 ││
│  │ 2        │ 玩家2        │ 500     │ 3       │2025-10-02 ││
│  └──────────┴──────────────┴─────────┴─────────┴───────────┘│
└─────────────────────────────────────────────────────────────┘
```

## 技术栈详细说明

### 客户端 (BlazorIdle)

**框架和库：**
- .NET 8.0
- Microsoft.AspNetCore.Components.WebAssembly 8.0.20
- Microsoft.AspNetCore.Components.WebAssembly.DevServer 8.0.20

**主要特性：**
- 单页应用 (SPA)
- 在浏览器中运行 .NET 代码
- 使用 HttpClient 进行 API 调用
- 组件化开发

**项目结构：**
```
BlazorIdle/
├── Pages/
│   ├── Home.razor          - 首页
│   ├── Counter.razor       - 计数器示例
│   ├── Weather.razor       - 天气示例
│   └── GameManager.razor   - 游戏数据管理 (新增)
├── Layout/
│   ├── MainLayout.razor    - 主布局
│   └── NavMenu.razor       - 导航菜单
├── Models/
│   └── GameData.cs         - 游戏数据模型 (新增)
├── wwwroot/                - 静态资源
├── Program.cs              - 应用入口
└── App.razor               - 根组件
```

### 服务端 (BlazorIdle.Server)

**框架和库：**
- .NET 9.0
- ASP.NET Core Web API
- Microsoft.EntityFrameworkCore.Sqlite 9.0.9
- Microsoft.EntityFrameworkCore.Design 9.0.9

**主要特性：**
- RESTful API 设计
- Entity Framework Core ORM
- SQLite 数据库
- CORS 跨域支持
- 依赖注入
- 结构化日志

**项目结构：**
```
BlazorIdle.Server/
├── Controllers/
│   └── GameDataController.cs  - API 控制器
├── Data/
│   └── GameDbContext.cs       - 数据库上下文
├── Models/
│   └── GameData.cs            - 数据模型
├── Properties/
│   └── launchSettings.json    - 启动配置
├── appsettings.json           - 应用配置
├── appsettings.Development.json
└── Program.cs                 - 应用入口
```

## 数据流

### 1. 客户端请求数据流程

```
用户操作 → Blazor组件 → HttpClient → API端点 → 控制器 → 
DbContext → SQLite → 返回数据 → JSON序列化 → HTTP响应 → 
客户端反序列化 → 更新UI
```

### 2. 数据保存流程

```
用户输入 → 表单提交 → HttpClient.PostAsJsonAsync → 
API端点 → 控制器验证 → DbContext.Add → SaveChangesAsync → 
SQLite写入 → 返回创建的对象 → 更新客户端列表
```

## 安全考虑

当前实现包含以下安全措施：

1. **CORS 配置**: 限制允许访问 API 的源
2. **参数验证**: 控制器中的基本验证
3. **异常处理**: 统一的错误处理
4. **数据验证**: EF Core 模型验证

**生产环境建议增加：**
- 身份验证 (Authentication)
- 授权 (Authorization)
- HTTPS 强制
- API 密钥或 JWT 令牌
- 输入消毒 (Input Sanitization)
- SQL 注入防护（EF Core 已提供）
- 速率限制 (Rate Limiting)
- 日志审计

## 扩展性

### 水平扩展

当前架构支持以下扩展：

1. **客户端**: 可以部署到 CDN，无状态设计
2. **服务端**: 可以部署多个实例，使用负载均衡
3. **数据库**: 可以升级到支持并发的数据库（PostgreSQL, SQL Server）

### 垂直扩展

可以添加的功能：

1. **缓存层**: Redis 用于会话和数据缓存
2. **消息队列**: RabbitMQ/Azure Service Bus 用于异步处理
3. **文件存储**: Azure Blob Storage/AWS S3
4. **身份认证**: Azure AD B2C/Auth0
5. **实时通信**: SignalR 用于实时更新
6. **搜索引擎**: Elasticsearch 用于全文搜索

## 开发最佳实践

本项目遵循以下最佳实践：

1. **关注点分离**: 客户端、服务端、数据访问分离
2. **RESTful API**: 遵循 REST 设计原则
3. **依赖注入**: 使用内置 DI 容器
4. **异步编程**: 使用 async/await 模式
5. **错误处理**: 统一的异常处理策略
6. **配置管理**: 使用配置文件和环境变量
7. **代码组织**: 清晰的项目结构和命名约定

## 性能考虑

### 当前实现

- **客户端**: Blazor WebAssembly 首次加载较慢，但后续交互快速
- **服务端**: 轻量级 API，响应时间 < 100ms
- **数据库**: SQLite 适合小规模应用，单文件存储

### 优化建议

1. **客户端优化**:
   - 使用懒加载 (Lazy Loading)
   - 压缩 Blazor 资源
   - 使用 AOT 编译
   
2. **服务端优化**:
   - 实施缓存策略
   - 数据库查询优化
   - 启用响应压缩
   - 使用异步 I/O

3. **数据库优化**:
   - 添加索引
   - 使用连接池
   - 实施查询优化
   - 考虑读写分离

## 监控和日志

当前实现：
- 内置 ASP.NET Core 日志
- 控制器级别的错误日志

生产环境建议：
- Application Insights 或 ELK Stack
- 性能监控
- 健康检查端点
- 分布式追踪

## 总结

本项目成功实现了一个前后端分离的架构，具有以下特点：

✅ **完整的 CRUD 操作**
✅ **SQLite 数据持久化**
✅ **跨域支持**
✅ **RESTful API 设计**
✅ **清晰的代码结构**
✅ **易于扩展和维护**

这个架构为开发更复杂的应用程序提供了坚实的基础。
