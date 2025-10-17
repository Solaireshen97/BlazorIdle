# 战斗快照恢复修复说明

## 问题描述

当角色设置了战斗计划任务并开始战斗后，如果服务器被突然关闭（如 Ctrl+C、kill 命令、断电等），服务器可能无法正常重启，必须删除数据库才能恢复。

## 根本原因

**StepBattleSnapshotService.RecoverAllAsync** 方法在恢复战斗快照时存在以下问题：

1. **不删除损坏的快照**：当快照恢复失败时（如角色不存在、装备缺失、JSON损坏等），代码只是静默跳过，但不删除损坏的快照记录。
2. **下次启动重复失败**：损坏的快照会在每次服务器启动时重复尝试恢复，可能导致启动过程卡死或异常。
3. **缺少错误日志**：静默失败导致无法诊断问题原因。

## 修复内容

### 1. 改进的错误处理

新的恢复逻辑在每个可能失败的步骤都添加了错误处理：

```csharp
// 1. 验证快照数据完整性
var dto = JsonSerializer.Deserialize<StepBattleSnapshotDto>(row.SnapshotJson);
if (dto is null)
{
    logger?.LogWarning("快照 {SnapshotId} 数据为空，将被删除", row.Id);
    failedSnapshotIds.Add(row.StepBattleId);
    failedCount++;
    continue;
}

// 2. 验证角色存在性
var ch = await characters.GetAsync(dto.CharacterId, ct);
if (ch is null)
{
    logger?.LogWarning(
        "快照 {SnapshotId} 引用的角色 {CharacterId} 不存在，将被删除",
        row.Id, dto.CharacterId);
    failedSnapshotIds.Add(row.StepBattleId);
    failedCount++;
    continue;
}

// 3. 捕获装备加载失败
try
{
    stats = await equipmentStats.BuildStatsWithEquipmentAsync(...);
}
catch (Exception ex)
{
    logger?.LogWarning(ex, "快照 {SnapshotId} 构建角色属性失败（可能装备缺失），将被删除", row.Id);
    failedSnapshotIds.Add(row.StepBattleId);
    failedCount++;
    continue;
}

// 4. 捕获战斗启动失败
try
{
    newId = coord.Start(...);
}
catch (Exception ex)
{
    logger?.LogWarning(ex, "快照 {SnapshotId} 启动战斗失败（可能敌人配置错误），将被删除", row.Id);
    failedSnapshotIds.Add(row.StepBattleId);
    failedCount++;
    continue;
}

// 5. 捕获快进失败
try
{
    rb.FastForwardTo(dto.SimulatedSeconds);
    rb.Segments.Clear();
    rb.Segments.AddRange(dto.Segments);
    recoveredCount++;
}
catch (Exception ex)
{
    logger?.LogWarning(ex, "快照 {SnapshotId} 快进到模拟时间失败，将被删除", row.Id);
    failedSnapshotIds.Add(row.StepBattleId);
    failedCount++;
}
```

### 2. 自动清理损坏快照

恢复完成后，批量删除所有失败的快照：

```csharp
// 删除所有失败的快照
if (failedSnapshotIds.Count > 0)
{
    logger?.LogWarning("清理 {Count} 个损坏的快照", failedSnapshotIds.Count);
    
    foreach (var failedId in failedSnapshotIds)
    {
        try
        {
            await DeleteAsync(failedId, ct);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "删除损坏快照 {SnapshotId} 失败", failedId);
        }
    }
}
```

### 3. 详细的日志记录

添加了完整的日志，便于诊断问题：

- 开始恢复时记录快照总数
- 每个成功恢复的快照记录详情
- 每个失败的快照记录失败原因
- 最后统计成功和失败的数量

```csharp
logger?.LogInformation("开始恢复战斗快照，共 {Count} 个快照", rows.Count);
// ...恢复过程...
logger?.LogInformation(
    "战斗快照恢复完成: 成功 {RecoveredCount} 个，失败 {FailedCount} 个",
    recoveredCount, failedCount);
```

## 修复效果

### 修复前
- ❌ 损坏的快照导致服务器无法启动
- ❌ 必须手动删除数据库才能恢复
- ❌ 无法诊断问题原因

### 修复后
- ✅ 服务器能够正常启动，即使存在损坏的快照
- ✅ 自动清理损坏的快照，无需手动干预
- ✅ 详细的日志帮助诊断问题
- ✅ 成功恢复有效的快照，失败的自动删除

## 测试场景

### 场景 1：正常恢复
1. 角色开始战斗任务
2. 服务器正常保存快照
3. 正常关闭服务器（Ctrl+C）
4. 重启服务器
5. **预期结果**：快照成功恢复，战斗继续

### 场景 2：损坏快照（角色已删除）
1. 角色开始战斗任务
2. 服务器保存快照
3. 手动删除数据库中的角色记录（模拟数据损坏）
4. 重启服务器
5. **预期结果**：日志显示快照恢复失败（角色不存在），快照被自动删除，服务器正常启动

### 场景 3：损坏快照（无效JSON）
1. 手动修改数据库中的快照JSON为无效格式
2. 重启服务器
3. **预期结果**：日志显示快照数据无效，快照被自动删除，服务器正常启动

### 场景 4：突然关闭（前端在线）
1. 角色开始战斗任务
2. **前端保持在线状态**
3. 强制终止服务器进程（kill -9）
4. 重启服务器
5. **预期结果**：服务器正常启动，尝试恢复快照，失败的快照被自动清理

## 日志示例

### 成功恢复
```
开始恢复战斗快照，共 2 个快照
成功恢复快照 xxx-xxx-xxx，战斗ID: yyy-yyy-yyy，角色: zzz-zzz-zzz
成功恢复快照 aaa-aaa-aaa，战斗ID: bbb-bbb-bbb，角色: ccc-ccc-ccc
战斗快照恢复完成: 成功 2 个，失败 0 个
```

### 部分失败
```
开始恢复战斗快照，共 3 个快照
成功恢复快照 xxx-xxx-xxx，战斗ID: yyy-yyy-yyy，角色: zzz-zzz-zzz
快照 aaa-aaa-aaa 引用的角色 bbb-bbb-bbb 不存在，将被删除
快照 ccc-ccc-ccc 构建角色属性失败（可能装备缺失），将被删除
清理 2 个损坏的快照
战斗快照恢复完成: 成功 1 个，失败 2 个
```

## 与其他安全机制的配合

此修复与以下现有机制配合使用，提供多层保护：

1. **DatabaseRetryPolicy**：处理临时性数据库锁定错误
2. **GracefulShutdownCoordinator**：正常关闭时保存所有运行中的快照
3. **WAL Checkpoint**：确保数据库文件一致性
4. **OfflineDetectionService**：检测离线玩家并暂停其任务

## 文件修改

- **修改文件**：`BlazorIdle.Server/Application/Battles/Step/StepBattleSnapshotService.cs`
- **修改方法**：`RecoverAllAsync`
- **修改行数**：约 +100 行（增加了大量错误处理和日志）

## 相关文档

- [DATABASE_SAFETY_IMPROVEMENTS.md](DATABASE_SAFETY_IMPROVEMENTS.md) - 数据库安全性改进总体设计
- [StepBattleHostedService.cs](BlazorIdle.Server/Application/Battles/Step/StepBattleHostedService.cs) - 优雅关闭实现
- [DatabaseRetryPolicy.cs](BlazorIdle.Server/Infrastructure/Persistence/DatabaseRetryPolicy.cs) - 重试策略实现
