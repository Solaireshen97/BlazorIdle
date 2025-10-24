# 前端项目修复说明 / Frontend Project Fix Documentation

## 问题描述 / Problem Description

前端项目无法打开，主要原因是 .NET 版本不匹配导致的兼容性问题。

The frontend project couldn't open due to .NET version mismatch causing compatibility issues.

## 问题根源 / Root Cause

项目中存在 .NET 版本不一致：
- **BlazorIdle** (前端): 使用 .NET 8.0 ✓
- **BlazorIdle.Server** (后端): 原本使用 .NET 9.0 ❌
- **BlazorIdle.Shared** (共享库): 使用 .NET 8.0 ✓
- **BlazorIdle.Tests** (测试): 原本使用 .NET 9.0 ❌

Multiple .NET versions across the project:
- **BlazorIdle** (Frontend): Using .NET 8.0 ✓
- **BlazorIdle.Server** (Backend): Was using .NET 9.0 ❌
- **BlazorIdle.Shared** (Shared): Using .NET 8.0 ✓
- **BlazorIdle.Tests** (Tests): Was using .NET 9.0 ❌

## 修复内容 / Fix Applied

### 1. 统一 .NET 版本到 8.0 / Unified .NET Version to 8.0

更新了以下项目文件：
Updated the following project files:

#### BlazorIdle.Server/BlazorIdle.Server.csproj
- 目标框架: `net9.0` → `net8.0`
- Target Framework: `net9.0` → `net8.0`

#### tests/BlazorIdle.Tests/BlazorIdle.Tests.csproj
- 目标框架: `net9.0` → `net8.0`
- Target Framework: `net9.0` → `net8.0`

### 2. 更新依赖包版本 / Updated Package Versions

#### BlazorIdle.Server
```xml
Microsoft.AspNetCore.Authentication.JwtBearer: 9.0.0 → 8.0.20
Microsoft.AspNetCore.SignalR.Protocols.MessagePack: 9.0.9 → 8.0.20
Microsoft.EntityFrameworkCore.Design: 9.0.9 → 8.0.20
Microsoft.EntityFrameworkCore.Sqlite: 9.0.9 → 8.0.20
Microsoft.EntityFrameworkCore.Tools: 9.0.9 → 8.0.20
```

#### BlazorIdle.Tests
```xml
Microsoft.AspNetCore.SignalR.Client: 9.0.0 → 8.0.20
```

## 验证结果 / Verification Results

✅ **编译成功** / Build Successful
- 所有项目成功编译，无错误
- All projects build successfully with no errors

✅ **测试通过** / Tests Passing
- 253/259 测试通过 (6个失败为预存在问题)
- 253/259 tests passing (6 failures are pre-existing issues)

✅ **服务器启动** / Server Started
- 后端服务器成功启动并监听 https://localhost:7056
- Backend server starts successfully and listens on https://localhost:7056

✅ **前端启动** / Frontend Started
- 前端应用成功启动并监听 http://localhost:5000
- Frontend app starts successfully and listens on http://localhost:5000

## 如何运行 / How to Run

### 方式一：使用启动脚本 / Method 1: Using Startup Script

```bash
# Linux/Mac
./start-dev.sh

# Windows (PowerShell)
# 需要手动启动两个项目 / Need to start both projects manually
```

### 方式二：手动启动 / Method 2: Manual Start

#### 1. 启动后端 / Start Backend
```bash
cd BlazorIdle.Server
dotnet run --launch-profile https
```

后端将监听 / Backend listens on:
- HTTPS: https://localhost:7056
- HTTP: http://localhost:5056
- Swagger: https://localhost:7056/swagger

#### 2. 启动前端 / Start Frontend
```bash
cd BlazorIdle
dotnet run
```

前端将监听 / Frontend listens on:
- http://localhost:5000

## 项目配置 / Project Configuration

### 端口配置 / Port Configuration

- **后端 Backend**: 
  - HTTPS: `https://localhost:7056`
  - HTTP: `http://localhost:5056`
  
- **前端 Frontend**: 
  - HTTP: `http://localhost:5000`

### 前端 API 配置 / Frontend API Configuration

前端通过以下配置文件连接后端：
Frontend connects to backend via:

**BlazorIdle/wwwroot/appsettings.json**
```json
{
  "ApiBaseUrl": "https://localhost:7056"
}
```

## 技术细节 / Technical Details

### 为什么选择 .NET 8.0 而不是 9.0？ / Why .NET 8.0 Instead of 9.0?

1. **一致性** / Consistency: 前端和共享库已经使用 .NET 8.0
2. **稳定性** / Stability: .NET 8.0 是 LTS (长期支持) 版本
3. **兼容性** / Compatibility: 确保所有组件使用相同版本避免冲突

### 影响的组件 / Affected Components

- ✅ Blazor WebAssembly (前端)
- ✅ ASP.NET Core Web API (后端)
- ✅ Entity Framework Core (数据库)
- ✅ SignalR (实时通信)
- ✅ xUnit (测试框架)

## 问题排查 / Troubleshooting

### 如果前端无法连接后端 / If Frontend Can't Connect to Backend

1. 确认后端正在运行 / Verify backend is running
   ```bash
   curl -k https://localhost:7056/swagger/index.html
   ```

2. 检查防火墙设置 / Check firewall settings

3. 确认端口未被占用 / Verify ports are not in use
   ```bash
   # Linux/Mac
   lsof -i :7056
   lsof -i :5000
   
   # Windows
   netstat -ano | findstr :7056
   netstat -ano | findstr :5000
   ```

### 如果编译失败 / If Build Fails

1. 清理并重新构建 / Clean and rebuild
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

2. 确认 .NET SDK 版本 / Verify .NET SDK version
   ```bash
   dotnet --version
   # 应该显示 8.0.x / Should show 8.0.x
   ```

## 总结 / Summary

此修复将整个项目统一到 .NET 8.0，解决了版本不匹配导致的兼容性问题，使前端和后端能够正常通信和运行。

This fix unified the entire project to .NET 8.0, resolving compatibility issues caused by version mismatch, enabling proper communication and operation between frontend and backend.
