# 使用说明 (Usage Guide)

## 项目概述

这个项目实现了前后端分离的架构：
- **前端**: Blazor WebAssembly 客户端运行在浏览器中
- **后端**: ASP.NET Core Web API 服务器处理数据和业务逻辑
- **数据库**: SQLite 用于数据持久化存储

## 启动步骤

### 1. 启动服务端

打开一个终端窗口，运行：

```bash
cd BlazorIdle.Server
dotnet run
```

服务端将启动并监听：
- HTTP: http://localhost:5056
- HTTPS: https://localhost:7056

你会看到以下输出：
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5056
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 2. 启动客户端

打开另一个终端窗口，运行：

```bash
cd BlazorIdle
dotnet run
```

客户端将启动并监听：
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

你会看到类似输出：
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 3. 访问应用

在浏览器中打开：http://localhost:5000

## 功能演示

### 游戏管理页面 (Game Manager)

1. 在导航栏点击 "Game Manager"
2. 你会看到一个表单用于创建新的游戏数据
3. 填写玩家名称、分数和等级
4. 点击 "Create" 按钮创建数据
5. 创建的数据会显示在下方的表格中
6. 可以点击 "Delete" 按钮删除数据
7. 点击 "Refresh" 按钮刷新数据列表

### 数据持久化测试

1. 在 Game Manager 页面创建一些游戏数据
2. 停止服务端（按 Ctrl+C）
3. 重新启动服务端
4. 刷新浏览器页面
5. 你会发现数据仍然存在，说明数据已经保存到 SQLite 数据库中

## API 测试

### 使用 curl 测试 API

获取所有游戏数据：
```bash
curl http://localhost:5056/api/GameData
```

创建新的游戏数据：
```bash
curl -X POST http://localhost:5056/api/GameData \
  -H "Content-Type: application/json" \
  -d '{"playerName":"玩家1","score":500,"level":3}'
```

获取特定 ID 的游戏数据：
```bash
curl http://localhost:5056/api/GameData/1
```

更新游戏数据：
```bash
curl -X PUT http://localhost:5056/api/GameData/1 \
  -H "Content-Type: application/json" \
  -d '{"id":1,"playerName":"玩家1","score":1500,"level":5}'
```

删除游戏数据：
```bash
curl -X DELETE http://localhost:5056/api/GameData/1
```

## 数据库位置

SQLite 数据库文件位于：
```
BlazorIdle.Server/gamedata.db
```

你可以使用 SQLite 工具（如 DB Browser for SQLite）打开这个文件查看数据。

## 常见问题

### 问题：无法连接到服务端

**解决方案**：
1. 确保服务端正在运行
2. 检查防火墙设置
3. 确认端口没有被其他程序占用
4. 检查客户端的 API 地址配置是否正确（在 `Pages/GameManager.razor` 中）

### 问题：CORS 错误

**解决方案**：
1. 确保服务端的 CORS 策略包含了客户端的地址
2. 检查 `BlazorIdle.Server/Program.cs` 中的 CORS 配置
3. 尝试使用 HTTP 而不是 HTTPS（开发环境）

### 问题：数据库文件被锁定

**解决方案**：
1. 确保只有一个服务端实例在运行
2. 关闭所有访问数据库的工具
3. 删除 `.db-shm` 和 `.db-wal` 文件后重新启动服务端

## 扩展开发

### 添加新的数据模型

1. 在 `BlazorIdle.Server/Models/` 中创建新的模型类
2. 在 `GameDbContext.cs` 中添加对应的 `DbSet`
3. 创建对应的控制器在 `Controllers/` 目录
4. 在客户端的 `Models/` 目录创建相同的模型（用于序列化）
5. 创建或更新 Razor 页面来使用新的 API

### 修改 API 端口

1. 修改 `BlazorIdle.Server/Properties/launchSettings.json`
2. 修改客户端页面中的 `apiBaseUrl` 配置
3. 更新服务端的 CORS 策略（如果需要）

## 生产部署建议

1. 使用环境变量配置 API 地址
2. 启用 HTTPS 并配置正确的证书
3. 使用更安全的数据库（如 PostgreSQL 或 SQL Server）
4. 实施身份验证和授权
5. 添加日志记录和错误处理
6. 配置适当的 CORS 策略
7. 使用 API 版本控制
8. 实施数据验证和清理
