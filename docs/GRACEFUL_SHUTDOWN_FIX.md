# 服务器优雅关闭修复文档

## 问题描述

在之前的版本中，当服务器运行战斗计划任务时被关闭，会导致数据库损坏，无法正常重启。具体表现为：

1. 如果角色设置了战斗计划任务并开始战斗
2. 直接关闭服务器（Ctrl+C 或终止进程）
3. 数据库出现异常（SQLite WAL 文件未正确同步）
4. 服务器无法正常启动，前端无法连接

如果在关闭服务器前先退出登录，等待一段时间让离线检测服务暂停任务，则可以正常关闭和重启。

## 根本原因分析

1. **SQLite WAL 模式未正确关闭**
   - SQLite 使用 Write-Ahead Logging (WAL) 模式提高并发性能
   - 服务器突然关闭时，WAL 文件中的更改未写入主数据库文件
   - 导致数据库处于不一致状态

2. **运行中的战斗状态未保存**
   - `StepBattleHostedService` 没有实现优雅关闭逻辑
   - 服务器关闭时，运行中的战斗状态丢失
   - 活动计划仍然标记为 Running 状态，但战斗已停止

3. **活动计划未正确暂停**
   - `OfflineDetectionService` 没有实现关闭时的清理逻辑
   - 运行中的计划在服务器关闭时未暂停
   - 重启后这些计划处于不一致状态

4. **数据库事务未提交**
   - 频繁的状态更新可能有未提交的事务
   - 服务器关闭时这些事务被丢弃

## 解决方案

### 1. StepBattleHostedService 优雅关闭

添加 `GracefulShutdownAsync()` 方法：

```csharp
private async Task GracefulShutdownAsync()
{
    // 1. 保存所有运行中的战斗快照
    foreach (var id in _coordinator.InternalIdsSnapshot())
    {
        if (_coordinator.TryGet(id, out var rb) && rb is not null && !rb.Completed)
        {
            await _snapshot.SaveAsync(rb, CancellationToken.None);
        }
    }
    
    // 2. 暂停所有运行中的活动计划（保存战斗状态到计划）
    await PauseAllRunningPlansAsync();
}
```

**工作原理：**
- 在 `ExecuteAsync` 退出前调用优雅关闭逻辑
- 保存所有运行中战斗的快照到数据库
- 遍历所有运行中的活动计划并暂停它们
- 暂停时会保存战斗状态到 `ActivityPlan.BattleStateJson`
- 服务器重启后可以从保存的状态恢复

### 2. OfflineDetectionService 优雅关闭

添加 `ShutdownCheckAsync()` 方法：

```csharp
private async Task ShutdownCheckAsync()
{
    var runningPlans = await planRepository.GetAllRunningPlansAsync(CancellationToken.None);
    
    foreach (var plan in runningPlans)
    {
        await planService.PausePlanAsync(plan.Id, CancellationToken.None);
    }
}
```

**工作原理：**
- 在服务停止时执行最后一次检查
- 暂停所有运行中的计划（不管玩家是否在线）
- 确保服务器重启后能够恢复所有计划

### 3. DatabaseCleanupService

新增专门的数据库清理服务：

```csharp
public class DatabaseCleanupService : IHostedService
{
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. 保存待处理的更改
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        
        // 2. 执行 SQLite WAL 检查点
        await dbContext.Database.ExecuteSqlRawAsync(
            "PRAGMA wal_checkpoint(TRUNCATE);", 
            cancellationToken);
        
        // 3. 关闭数据库连接
        await dbContext.Database.CloseConnectionAsync();
    }
}
```

**工作原理：**
- `PRAGMA wal_checkpoint(TRUNCATE)` 强制将 WAL 文件内容写入主数据库
- 保存所有待处理的数据库更改
- 正确关闭数据库连接
- 防止数据库文件损坏

### 4. SQLite 连接配置优化

更新 `DependencyInjection.cs` 中的数据库连接字符串：

```csharp
var connectionString = conn + 
    ";Journal Mode=WAL" +           // 使用 WAL 模式
    ";Synchronous=NORMAL" +         // 平衡性能和安全性
    ";Cache=Shared" +               // 允许连接共享缓存
    ";Foreign Keys=True" +          // 启用外键约束
    ";Pooling=True";                // 启用连接池
```

**配置说明：**
- `Journal Mode=WAL`: 使用 Write-Ahead Logging，提高并发性能
- `Synchronous=NORMAL`: 在大多数情况下足够安全，性能更好
- `Cache=Shared`: 多个连接可以共享缓存，减少内存使用
- `Foreign Keys=True`: 确保数据一致性
- `Pooling=True`: 重用数据库连接，提高性能

## 测试验证

### 单元测试

创建了 `GracefulShutdownTests.cs` 包含以下测试：

1. **PausePlan_ShouldSaveBattleState_WhenPlanIsRunning**
   - 验证暂停计划时保存战斗状态
   - 验证 `BattleStateJson` 不为空
   - 验证 `BattleId` 被清除（战斗已停止）

2. **StartPlan_ShouldRestoreBattleState_AfterPause**
   - 验证暂停后可以恢复计划
   - 验证战斗状态从 `BattleStateJson` 恢复
   - 验证计划状态正确切换

3. **MultipleRunningPlans_ShouldAllBePaused_OnShutdown**
   - 验证多个运行中的计划都能被暂停
   - 验证所有计划的战斗状态都被保存

所有测试通过，确认功能正确实现。

### 手动测试步骤

#### 测试场景 1：正常关闭和重启

1. 启动服务器
2. 创建角色并设置战斗计划任务
3. 等待战斗开始运行
4. 按 Ctrl+C 优雅关闭服务器
5. 重新启动服务器
6. 验证数据库正常加载
7. 验证战斗计划恢复为暂停状态
8. 登录后验证可以继续战斗

**预期结果：**
- 服务器正常关闭，日志显示优雅关闭消息
- 数据库文件完整无损
- 服务器可以正常重启
- 战斗计划状态为 Paused
- 玩家登录后可以继续战斗

#### 测试场景 2：多个角色同时战斗

1. 启动服务器
2. 创建 3 个角色
3. 为每个角色设置战斗计划任务
4. 等待所有战斗开始运行
5. 按 Ctrl+C 关闭服务器
6. 重新启动服务器
7. 验证所有角色的战斗计划都恢复为暂停状态

**预期结果：**
- 所有 3 个战斗计划都被暂停
- 所有战斗状态都被保存
- 服务器重启后可以恢复所有计划

#### 测试场景 3：数据库完整性验证

使用 SQLite 命令行工具验证数据库完整性：

```bash
# 在服务器关闭后执行
sqlite3 gamedata.db "PRAGMA integrity_check;"
```

**预期结果：**
- 输出 "ok"，表示数据库完整无损

## 关键改进点总结

### 1. 优雅关闭流程
- ✅ StepBattleHostedService 在退出前保存所有战斗快照
- ✅ OfflineDetectionService 在退出前暂停所有运行中的计划
- ✅ DatabaseCleanupService 确保数据库正确关闭

### 2. 状态保存
- ✅ 战斗状态保存到 `RunningBattleSnapshotRecord` 表
- ✅ 活动计划状态保存到 `ActivityPlan.BattleStateJson`
- ✅ 玩家进度保存到 `ActivityPlan.ExecutedSeconds`

### 3. 数据库安全
- ✅ WAL 检查点确保所有更改写入主数据库
- ✅ 连接池和共享缓存提高性能
- ✅ 同步模式平衡性能和安全性

### 4. 恢复机制
- ✅ 服务器重启时自动恢复战斗快照
- ✅ 服务器重启时恢复暂停的计划
- ✅ 玩家登录时通过离线结算恢复战斗

## 注意事项

### 关闭时间
- 优雅关闭需要时间完成（通常 < 5 秒）
- 不要强制终止进程（kill -9），应使用 Ctrl+C 或 SIGTERM
- 如果关闭时间过长，检查是否有大量运行中的战斗

### 日志监控
关闭时应该看到以下日志：

```
离线检测服务正在关闭，执行最后一次检查...
服务器关闭：发现 X 个运行中的计划，正在暂停...
StepBattleHostedService shutting down gracefully...
Starting graceful shutdown - saving running battles and pausing activity plans...
Saved X running battle snapshots during shutdown
数据库清理服务正在关闭，执行数据库清理...
已执行 SQLite WAL 检查点
```

### 异常处理
- 所有关闭逻辑都有 try-catch 保护
- 即使某个步骤失败，其他步骤仍会继续
- 错误会记录到日志，但不会阻止服务器关闭

## 性能影响

- **关闭时间增加**: 约 1-3 秒（取决于运行中的战斗数量）
- **内存使用**: 无明显增加
- **运行时性能**: 无影响（WAL 模式实际上提高了性能）
- **磁盘 I/O**: 关闭时有一次额外的写入（WAL 检查点）

## 未来改进方向

1. **增量检查点**: 在运行时定期执行 WAL 检查点，减少关闭时的工作量
2. **异步暂停**: 使用后台任务并行暂停多个计划，加快关闭速度
3. **状态压缩**: 压缩 `BattleStateJson` 减少数据库大小
4. **监控指标**: 添加 Prometheus 指标监控关闭时间和状态保存成功率

## 相关文件

- `BlazorIdle.Server/Application/Battles/Step/StepBattleHostedService.cs`
- `BlazorIdle.Server/Services/OfflineDetectionService.cs`
- `BlazorIdle.Server/Services/DatabaseCleanupService.cs`
- `BlazorIdle.Server/Infrastructure/DependencyInjection.cs`
- `BlazorIdle.Server/Program.cs`
- `tests/BlazorIdle.Tests/GracefulShutdownTests.cs`
