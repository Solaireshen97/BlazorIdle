# BlazorIdle - 前后端分离项目

这是一个前后端分离的 Blazor 项目，包含 Blazor WebAssembly 客户端和 ASP.NET Core Web API 服务端。

## 项目结构

- **BlazorIdle** - Blazor WebAssembly 客户端项目
- **BlazorIdle.Server** - ASP.NET Core Web API 服务端项目，集成了 SQLite 数据库

## 技术栈

### 客户端 (BlazorIdle)
- .NET 8.0
- Blazor WebAssembly
- HttpClient 用于 API 通信

### 服务端 (BlazorIdle.Server)
- .NET 9.0
- ASP.NET Core Web API
- Entity Framework Core 9.0
- SQLite 数据库

## 功能特性

1. **前后端分离架构** - 客户端和服务端独立运行
2. **RESTful API** - 完整的 CRUD API 端点
3. **SQLite 数据库集成** - 轻量级本地数据库存储
4. **CORS 配置** - 支持跨域请求
5. **游戏数据管理** - 示例页面展示数据库操作

## 快速开始

### 前置要求

- .NET 8.0 SDK 或更高版本
- .NET 9.0 SDK

### 运行服务端

1. 进入服务端目录：
```bash
cd BlazorIdle.Server
```

2. 运行服务端：
```bash
dotnet run
```

服务端将在以下地址启动：
- HTTPS: https://localhost:7056
- HTTP: http://localhost:5056

### 运行客户端

1. 在另一个终端窗口，进入客户端目录：
```bash
cd BlazorIdle
```

2. 运行客户端：
```bash
dotnet run
```

客户端将在以下地址启动：
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

### 访问应用

在浏览器中打开 https://localhost:5001 或 http://localhost:5000

## API 端点

服务端提供以下 API 端点：

- `GET /api/GameData` - 获取所有游戏数据
- `GET /api/GameData/{id}` - 获取指定 ID 的游戏数据
- `POST /api/GameData` - 创建新的游戏数据
- `PUT /api/GameData/{id}` - 更新指定 ID 的游戏数据
- `DELETE /api/GameData/{id}` - 删除指定 ID 的游戏数据

## 数据库

项目使用 SQLite 作为数据库，数据库文件 `gamedata.db` 会在服务端首次运行时自动创建在服务端项目根目录。

### 数据模型

**GameData** 表结构：
- `Id` (int, Primary Key) - 游戏数据 ID
- `PlayerName` (string) - 玩家名称
- `Score` (int) - 分数
- `Level` (int) - 等级
- `LastUpdated` (DateTime) - 最后更新时间

## 开发说明

### 修改 API 地址

如果需要修改服务端的运行端口，请同时更新客户端的 API 地址配置：

在 `BlazorIdle/Pages/GameManager.razor` 中修改：
```csharp
private string apiBaseUrl = "https://localhost:7056/api";
```

### CORS 配置

服务端的 CORS 配置在 `BlazorIdle.Server/Program.cs` 中，默认允许以下源：
- https://localhost:5001
- http://localhost:5000

如需添加其他源，请修改 CORS 策略配置。

## 构建发布

### 构建整个解决方案
```bash
dotnet build
```

### 发布服务端
```bash
cd BlazorIdle.Server
dotnet publish -c Release -o ./publish
```

### 发布客户端
```bash
cd BlazorIdle
dotnet publish -c Release -o ./publish
```

## 故障排除

### 跨域问题
如果遇到跨域请求被阻止的问题，请确保：
1. 服务端的 CORS 策略包含客户端的地址
2. 客户端的 API 地址配置正确

### 数据库连接问题
如果遇到数据库连接问题：
1. 确保服务端项目目录有写入权限
2. 检查 `appsettings.json` 中的连接字符串配置

## 许可证

此项目仅供学习和参考使用。
