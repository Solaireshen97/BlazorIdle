# 数据库安全性改进文档

## 问题描述

当角色设置了战斗计划任务并开始战斗后，如果服务器被突然关闭（如 Ctrl+C、kill 命令、断电等），数据库会出现损坏，导致：
- 服务器无法正常启动
- 前端无法连接服务器
- SQLite 数据库文件进入不一致状态

### 问题根源

1. **并发写入冲突**：多个后台服务同时写入数据库
   - `StepBattleHostedService`：每 500ms 保存战斗快照
   - `OfflineDetectionService`：每 30s 检查并暂停离线玩家的计划
   - `ActivityPlanService`：每 1s 更新运行中的计划进度
   - 心跳 API 调用：更新玩家的 LastSeenAtUtc

2. **WAL 模式未提交的事务**：SQLite 使用 Write-Ahead Logging (WAL) 模式时，突然关闭会导致 WAL 文件中的更改未合并到主数据库文件

3. **缺少重试机制**：临时性的数据库锁定错误（SQLITE_BUSY）会直接导致操作失败

4. **缺少优雅关闭**：服务器关闭时没有等待所有数据库操作完成

## 实施的改进

### 1. 数据库重试策略（DatabaseRetryPolicy）

**文件**：`BlazorIdle.Server/Infrastructure/Persistence/DatabaseRetryPolicy.cs`

使用 Polly 库实现了智能重试策略：
- 最多重试 5 次
- 指数退避延迟（100ms, 200ms, 400ms, 800ms, 1600ms）
- 仅对可重试的错误进行重试（SQLITE_BUSY, SQLITE_LOCKED, SQLITE_IOERR）
- 提供日志记录重试尝试

**使用方法**：
```csharp
await DatabaseRetryPolicy.SaveChangesWithRetryAsync(dbContext, cancellationToken, logger);
```

**应用位置**：
- 所有 Repository 的 `AddAsync`, `UpdateAsync`, `DeleteAsync` 方法
- `CharactersController` 的所有数据库保存操作
- `StepBattleSnapshotService` 的快照保存和删除操作

### 2. 优雅关闭协调器（GracefulShutdownCoordinator）

**文件**：`BlazorIdle.Server/Services/GracefulShutdownCoordinator.cs`

在服务器关闭时：
1. 触发关闭信号
2. 等待 2 秒让其他服务完成操作
3. 记录关闭过程

**注册**：在 `Program.cs` 中作为第一个 HostedService 注册，确保最先接收关闭信号。

### 3. 战斗快照优雅保存

**修改**：`StepBattleHostedService.ExecuteAsync`

在服务停止时：
- 保存所有运行中的战斗快照
- 记录成功/失败的数量
- 即使个别快照保存失败也继续处理其他快照

```csharp
private async Task SaveAllRunningBattleSnapshotsAsync(CancellationToken ct)
{
    // 遍历所有运行中的战斗并保存快照
    // 记录成功和失败的数量
}
```

### 4. SQLite 连接配置优化

**文件**：`BlazorIdle.Server/Infrastructure/DependencyInjection.cs`

改进的连接字符串配置：
```csharp
var connectionStringBuilder = new SqliteConnectionStringBuilder(conn)
{
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared,  // 共享缓存以提高并发性
    DefaultTimeout = 30               // 30秒繁忙超时
};
```

**优点**：
- `Shared Cache`：允许多个连接共享缓存，减少锁定冲突
- `30s Timeout`：给予足够时间等待其他事务完成，而不是立即失败

### 5. WAL 检查点处理

**文件**：`BlazorIdle.Server/Infrastructure/Persistence/GameDbContext.cs`

重写 `Dispose` 和 `DisposeAsync` 方法：
```csharp
private async Task ExecuteWalCheckpointAsync()
{
    // 执行 PRAGMA wal_checkpoint(TRUNCATE)
    // 将所有 WAL 更改写入主数据库文件
    // 截断 WAL 文件
}
```

**TRUNCATE 模式的作用**：
- 将 WAL 文件中的所有更改写入主数据库
- 截断 WAL 文件，防止无限增长
- 确保数据库文件处于一致状态

## 测试和验证

### 已完成

1. ✅ 代码编译成功
2. ✅ 添加了 Polly 包（v8.5.0），无安全漏洞
3. ✅ 所有数据库操作已更新为使用重试策略
4. ✅ 优雅关闭协调器已注册

### 待验证

1. ⏳ 手动测试：创建战斗任务后突然关闭服务器
2. ⏳ 验证服务器能够正常重启
3. ⏳ 验证战斗快照能够正确恢复
4. ⏳ 模拟高并发数据库写入场景

### 测试步骤

#### 场景 1：正常关闭
1. 启动服务器
2. 创建角色并开始战斗任务
3. 使用 `Ctrl+C` 或 `SIGTERM` 正常关闭服务器
4. 检查日志：应看到"优雅关闭"和"保存战斗快照"消息
5. 重新启动服务器
6. 验证战斗任务能够恢复

#### 场景 2：突然关闭（模拟崩溃）
1. 启动服务器
2. 创建角色并开始战斗任务
3. 使用 `kill -9` 强制终止服务器进程
4. 检查数据库文件：应该没有损坏
5. 重新启动服务器
6. 验证数据库能够正常打开
7. 验证战斗任务状态

#### 场景 3：并发压力测试
1. 启动服务器
2. 创建多个角色（5-10个）
3. 为每个角色创建战斗任务
4. 同时发送多个心跳请求
5. 等待数分钟让快照频繁保存
6. 正常关闭服务器
7. 验证所有数据都正确保存

## 技术细节

### SQLite WAL 模式

WAL（Write-Ahead Logging）模式的工作原理：
- 写入操作首先写入 WAL 文件
- 定期将 WAL 合并到主数据库文件（检查点）
- 允许并发读取，提高性能

**优点**：
- 支持并发读取
- 写入性能更好
- 崩溃恢复更可靠

**缺点**：
- 需要额外的磁盘空间（WAL 文件）
- 必须正确处理检查点

### Polly 重试策略

**指数退避算法**：
```
延迟 = 初始延迟 × 2^(重试次数-1)
100ms, 200ms, 400ms, 800ms, 1600ms
```

**为什么使用指数退避**：
- 给予其他事务更多时间完成
- 避免雪崩效应（所有重试同时发生）
- 平衡延迟和成功率

### 优雅关闭的重要性

**关闭顺序**：
1. 停止接受新请求
2. 完成正在处理的请求
3. 保存所有持久化数据
4. 关闭数据库连接
5. 释放资源

**2 秒缓冲时间的选择**：
- 足够让快照保存完成（通常 < 1 秒）
- 足够让心跳更新完成
- 不会让用户等待太久

## 配置选项

在 `appsettings.json` 中可以配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=gamedata.db"
  },
  "Offline": {
    "OfflineDetectionSeconds": 60
  },
  "Logging": {
    "EnableSensitiveDataLogging": false
  }
}
```

## 监控和日志

### 关键日志消息

**优雅关闭**：
```
服务器正在关闭，触发优雅关闭流程...
StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照...
已保存战斗快照: {BattleId}
战斗快照保存完成: 成功 {SavedCount} 个，失败 {FailedCount} 个
```

**重试操作**：
```
数据库操作失败，正在重试 ({RetryCount}/{MaxRetries})，等待 {Delay}ms
```

**错误**：
```
保存战斗快照失败: {BattleId}
保存所有战斗快照时发生错误
```

## 性能影响

### 预期影响

1. **重试策略**：
   - 正常情况：无影响（不需要重试）
   - 高负载：增加 < 5% 延迟（由于重试）
   - 提高成功率：95% → 99%+

2. **优雅关闭**：
   - 关闭时间增加：< 2 秒
   - 避免数据丢失：无价

3. **WAL 检查点**：
   - 每次连接关闭：< 100ms
   - 减少崩溃恢复时间：秒级 → 毫秒级

### 优化建议

如果遇到性能问题：
1. 增加 `wal_autocheckpoint` 阈值（默认 1000 页）
2. 调整重试次数和延迟
3. 使用更快的磁盘（SSD）
4. 监控数据库锁定时间

## 故障排除

### 数据库仍然损坏

1. 检查磁盘空间
2. 检查文件权限
3. 使用 `sqlite3 gamedata.db "PRAGMA integrity_check"`
4. 查看 WAL 文件大小（`gamedata.db-wal`）

### 恢复损坏的数据库

```bash
# 1. 备份现有文件
cp gamedata.db gamedata.db.backup
cp gamedata.db-wal gamedata.db-wal.backup

# 2. 尝试恢复
sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE)"

# 3. 验证完整性
sqlite3 gamedata.db "PRAGMA integrity_check"

# 4. 如果仍然损坏，从备份恢复
# 找到最近的有效备份并恢复
```

### 日志分析

查找重试相关的日志：
```bash
grep -i "重试" logs/application.log
grep -i "retry" logs/application.log
```

查找关闭相关的日志：
```bash
grep -i "优雅关闭" logs/application.log
grep -i "graceful" logs/application.log
```

## 未来改进

1. **数据库备份**：
   - 定期自动备份
   - 基于 WAL 的增量备份

2. **健康检查**：
   - 定期验证数据库完整性
   - 监控 WAL 文件大小

3. **更精细的锁定**：
   - 使用行级锁而不是表级锁
   - 考虑使用 `BEGIN IMMEDIATE` 事务

4. **分离读写**：
   - 读操作使用只读连接
   - 写操作使用专用连接

## 参考资料

- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Entity Framework Core Connection Resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)
- [Graceful Shutdown in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host)
