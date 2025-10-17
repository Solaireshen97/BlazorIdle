# 战斗计划任务数据库异常修复文档

## 问题描述

当角色设置了战斗的计划任务并开始战斗后，如果重启服务器，数据库会出现异常，导致：
- 服务器无法正常启动
- 前端无法连接服务器
- 数据库文件可能损坏或处于不一致状态

而没有设置任务时，可以正常启动数据库和前端连接。

## 根本原因分析

问题出在 `StepBattleHostedService.cs` 文件的以下几个方面：

### 1. DbContext 作用域冲突

在 `RecoverPausedPlansAsync` 方法中：
- 创建了一个 DbContext 作用域
- 在该作用域内查询所有暂停的计划
- 然后调用 `ActivityPlanService.StartPlanAsync`
- `StartPlanAsync` 会创建新的战斗并尝试访问数据库
- 但此时使用的仍然是外层作用域的 DbContext
- 这导致同一个 DbContext 实例被多个操作并发使用，违反了 DbContext 的使用规范

### 2. SQLite WAL 文件未正确检查点

SQLite 使用 Write-Ahead Logging (WAL) 模式时：
- 写操作先写入 WAL 文件（gamedata.db-wal）
- 定期将 WAL 文件合并到主数据库（gamedata.db）
- 如果服务器崩溃，WAL 文件可能包含未合并的更改
- 下次启动时，如果 WAL 文件损坏或过大，可能导致启动失败

### 3. 错误处理不足

- 单个计划恢复失败会影响整个启动过程
- 没有适当的延迟确保数据库完全初始化

## 修复方案

### 1. DbContext 作用域隔离

**修复前：**
```csharp
private async Task RecoverPausedPlansAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    var pausedPlans = await db.ActivityPlans
        .Where(p => p.State == ActivityState.Paused)
        .ToListAsync(ct);
    
    foreach (var plan in pausedPlans)
    {
        // 这里使用的是外层作用域的 DbContext
        await activityPlanService.StartPlanAsync(plan.Id, ct);
    }
}
```

**修复后：**
```csharp
private async Task RecoverPausedPlansAsync(CancellationToken ct)
{
    List<Guid> pausedPlanIds;
    
    // 第一步：在独立作用域中只查询ID
    using (var scope = _scopeFactory.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        pausedPlanIds = await db.ActivityPlans
            .Where(p => p.State == ActivityState.Paused)
            .Select(p => p.Id)
            .ToListAsync(ct);
    }
    
    // 第二步：为每个计划创建独立作用域
    foreach (var planId in pausedPlanIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
        // 每个计划使用自己的 DbContext
        await activityPlanService.StartPlanAsync(planId, ct);
    }
}
```

### 2. 添加 SQLite WAL 检查点

在 `Program.cs` 中添加启动时 WAL 检查点：

```csharp
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    
    try
    {
        // 执行 WAL 检查点，将 WAL 文件合并到主数据库
        logger.LogInformation("Executing SQLite WAL checkpoint...");
        await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
        logger.LogInformation("SQLite WAL checkpoint completed.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQLite WAL checkpoint failed, but continuing startup.");
    }
}
```

### 3. 改进错误处理和日志

- 添加更详细的日志记录
- 单个计划恢复失败不影响其他计划
- 添加 1 秒启动延迟确保数据库完全就绪

## 验证方法

### 方法 1：手动测试

1. 创建一个战斗计划任务并启动
2. 停止服务器（Ctrl+C）
3. 重新启动服务器
4. 检查服务器日志，应该看到：
   ```
   info: Program[0]
         Executing SQLite WAL checkpoint...
   info: Program[0]
         SQLite WAL checkpoint completed.
   info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
         StepBattleHostedService starting; recovering snapshots...
   info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
         Recovering paused plans...
   ```
5. 服务器应该成功启动，前端应该能够正常连接

### 方法 2：使用 SQLite 直接测试

1. 检查数据库状态：
   ```bash
   cd BlazorIdle.Server
   sqlite3 gamedata.db "SELECT Id, CharacterId, State, Type FROM ActivityPlans;"
   ```

2. 手动设置一个计划为暂停状态（State=4）：
   ```bash
   sqlite3 gamedata.db "UPDATE ActivityPlans SET State = 4 WHERE State = 1 LIMIT 1;"
   ```

3. 启动服务器：
   ```bash
   dotnet run
   ```

4. 观察服务器日志，确认暂停计划被正确恢复

### 方法 3：检查 WAL 文件大小

正常情况下，WAL 文件应该在检查点后变小：

```bash
ls -lh BlazorIdle.Server/gamedata.db*
```

启动前 WAL 文件可能较大（如 200KB），启动后应该减小（如 8KB）。

## 技术细节

### SQLite WAL 模式

- **优点**：提高并发性能，允许读写同时进行
- **缺点**：需要定期检查点，否则 WAL 文件会无限增长
- **TRUNCATE 模式**：检查点后删除 WAL 文件，确保干净的启动状态

### EF Core DbContext 生命周期

- **Scoped**：每个请求一个实例
- **不应共享**：不应在多个并发操作中共享同一个 DbContext
- **正确使用**：为每个独立操作创建新的作用域

### 后台服务（BackgroundService）

- **Singleton**：作为单例服务运行
- **作用域工厂**：使用 IServiceScopeFactory 创建临时作用域
- **DbContext 访问**：必须在作用域内获取 DbContext

## 影响分析

### 修复的影响

- ✅ 服务器重启不再因为暂停的计划而失败
- ✅ 数据库 WAL 文件得到正确维护
- ✅ DbContext 使用符合最佳实践
- ✅ 更好的错误处理和日志记录

### 性能影响

- 启动时多 1 秒延迟（可接受）
- WAL 检查点可能需要几毫秒到几百毫秒（取决于 WAL 大小）
- 每个计划恢复使用独立作用域，内存开销略微增加（可忽略）

## 相关文件

- `BlazorIdle.Server/Application/Battles/Step/StepBattleHostedService.cs` - 主要修复文件
- `BlazorIdle.Server/Program.cs` - WAL 检查点添加位置
- `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs` - 活动计划服务
- `BlazorIdle.Server/Infrastructure/Persistence/GameDbContext.cs` - 数据库上下文

## 未来改进建议

1. **监控 WAL 文件大小**：添加定期检查点任务，防止 WAL 文件过大
2. **计划恢复策略**：考虑添加配置选项，允许禁用自动恢复
3. **数据库健康检查**：添加启动时数据库完整性检查
4. **更细粒度的日志**：为调试添加更详细的日志级别控制
