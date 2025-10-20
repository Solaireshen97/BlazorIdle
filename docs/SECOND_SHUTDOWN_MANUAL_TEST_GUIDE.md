# 第二次关闭问题 - 手动测试指南

## 测试目标

验证服务器在多次关闭和重启后能够正常工作，特别是：
1. 第二次重启不会失败
2. 快照和计划状态保持同步
3. 不需要手动删除数据库

## 前提条件

### 环境准备

```bash
# 确保已安装依赖
cd /home/runner/work/BlazorIdle/BlazorIdle
dotnet restore

# 构建项目
dotnet build

# 备份现有数据库（如果有）
cd BlazorIdle.Server
cp gamedata.db gamedata.db.backup 2>/dev/null || true
```

### 数据库清理（可选）

如果要从干净状态开始测试：

```bash
cd BlazorIdle.Server
rm -f gamedata.db gamedata.db-shm gamedata.db-wal
```

## 测试场景

### 场景 1：正常的两次关闭和重启

**目标**：验证正常关闭流程下，第二次重启能够成功

#### 步骤 1：第一次启动

```bash
cd BlazorIdle.Server
dotnet run
```

**预期日志**：
```
StepBattleHostedService starting; cleaning up orphaned running plans...
没有发现孤立的运行中计划
没有发现孤立的战斗快照
Recovering battle snapshots...
Recovering paused plans...
StepBattleHostedService started.
```

**验证点**：
- [ ] 服务器成功启动
- [ ] Swagger 可访问（http://localhost:5000/swagger 或配置的端口）
- [ ] 没有错误日志

#### 步骤 2：创建角色和战斗任务

使用前端或 Swagger：

1. 创建用户和角色
2. 创建战斗计划（Combat Activity Plan）
   - SlotIndex: 0
   - Type: Combat
   - LimitType: Duration
   - LimitValue: 3600 (1小时)
   - PayloadJson: `{"EnemyId":"goblin","EnemyCount":1,"RespawnDelay":5}`
3. 开始战斗任务

**验证点**：
- [ ] 计划创建成功
- [ ] 战斗开始运行
- [ ] 可以看到战斗日志更新

#### 步骤 3：等待战斗运行

```bash
# 等待至少 10 秒，让战斗快照保存几次
sleep 10
```

**预期日志**：
```
已保存战斗快照: {BattleId}
```

**验证点**：
- [ ] 看到快照保存日志（每 ~500ms 一次）
- [ ] 战斗进度在推进

#### 步骤 4：第一次关闭

```bash
# 在服务器终端按 Ctrl+C
```

**预期日志**：
```
服务器正在关闭，触发优雅关闭流程...
StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照并暂停计划...
已保存战斗快照: {BattleId}
战斗快照保存完成: 成功 1 个，失败 0 个
已删除战斗快照 {BattleId} (计划 {PlanId})
已暂停计划 {PlanId} (角色 {CharacterId})
计划暂停完成: 成功 1 个，失败 0 个
StepBattleHostedService stopped.
```

**验证点**：
- [ ] 看到"已删除战斗快照"日志
- [ ] 看到"已暂停计划"日志
- [ ] 服务器正常退出（没有异常）

#### 步骤 5：检查数据库状态

```bash
# 检查数据库完整性
sqlite3 gamedata.db "PRAGMA integrity_check;"

# 检查快照表（应该为空）
sqlite3 gamedata.db "SELECT COUNT(*) FROM RunningBattleSnapshots;"

# 检查计划状态（应该是 Paused）
sqlite3 gamedata.db "SELECT Id, State, BattleId FROM ActivityPlans;"
```

**预期输出**：
```
ok
0
{PlanId}|Paused|
```

**验证点**：
- [ ] 数据库完整性检查通过
- [ ] 快照表为空
- [ ] 计划状态为 Paused，BattleId 为 NULL

#### 步骤 6：第一次重启

```bash
dotnet run
```

**预期日志**：
```
StepBattleHostedService starting; cleaning up orphaned running plans...
没有发现孤立的运行中计划
没有发现孤立的战斗快照
Recovering battle snapshots...
Recovering paused plans...
服务器重启后恢复暂停的计划 {PlanId} (玩家 {CharacterId} 在线)
StepBattleHostedService started.
```

**验证点**：
- [ ] 服务器成功启动 ✅
- [ ] 没有发现孤立数据
- [ ] 如果玩家仍在线，计划自动恢复运行

#### 步骤 7：等待战斗运行

```bash
sleep 10
```

**验证点**：
- [ ] 战斗正常运行
- [ ] 快照正常保存

#### 步骤 8：第二次关闭

```bash
# 再次按 Ctrl+C
```

**预期日志**：
```
StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照并暂停计划...
已删除战斗快照 {BattleId} (计划 {PlanId})
已暂停计划 {PlanId} (角色 {CharacterId})
计划暂停完成: 成功 1 个，失败 0 个
```

**验证点**：
- [ ] 快照被删除
- [ ] 计划被暂停
- [ ] 正常退出

#### 步骤 9：第二次重启（关键测试）

```bash
dotnet run
```

**预期日志**：
```
StepBattleHostedService starting; cleaning up orphaned running plans...
没有发现孤立的运行中计划
没有发现孤立的战斗快照
Recovering battle snapshots...
Recovering paused plans...
StepBattleHostedService started.
```

**验证点**：
- [ ] 服务器成功启动 ✅✅（关键！）
- [ ] Swagger 可访问
- [ ] 没有错误日志
- [ ] 计划可以再次恢复

**成功标准**：
- ✅ 服务器正常启动
- ✅ 没有数据库错误
- ✅ 不需要删除数据库
- ✅ 可以继续使用

---

### 场景 2：强制终止后的恢复

**目标**：验证即使强制终止，重启时也能自动恢复

#### 步骤 1：启动并创建战斗

```bash
cd BlazorIdle.Server
dotnet run
```

按照场景 1 的步骤 2 创建战斗任务

#### 步骤 2：强制终止服务器

```bash
# 找到进程 ID
ps aux | grep "dotnet run"

# 强制终止（替换 <PID> 为实际进程 ID）
kill -9 <PID>
```

**预期结果**：
- 服务器立即停止
- 没有优雅关闭日志
- 可能有孤立的快照和计划

#### 步骤 3：检查数据库状态

```bash
# 快照表可能有残留
sqlite3 gamedata.db "SELECT COUNT(*) FROM RunningBattleSnapshots;"

# 计划可能仍是 Running 状态
sqlite3 gamedata.db "SELECT State FROM ActivityPlans;"
```

**预期输出**：
```
1  （可能有快照残留）
Running  （计划仍是运行状态）
```

#### 步骤 4：重新启动

```bash
dotnet run
```

**预期日志**：
```
StepBattleHostedService starting; cleaning up orphaned running plans...
发现 1 个孤立的运行中计划，将它们标记为暂停状态
已将孤立的计划 {PlanId} (角色 {CharacterId}) 标记为暂停状态
孤立计划清理完成，已处理 1 个计划
发现 1 个孤立的战斗快照，将它们删除
已删除孤立的战斗快照 {BattleId} (角色 {CharacterId})
孤立快照清理完成，已删除 1 个快照
Recovering battle snapshots...
Recovering paused plans...
StepBattleHostedService started.
```

**验证点**：
- [ ] 服务器成功启动 ✅
- [ ] 自动清理孤立计划
- [ ] 自动清理孤立快照
- [ ] 没有错误

#### 步骤 5：再次强制终止和重启

```bash
# 创建新战斗，等待 10 秒
sleep 10

# 再次强制终止
kill -9 <PID>

# 再次启动
dotnet run
```

**验证点**：
- [ ] 第二次重启也能成功 ✅✅
- [ ] 自动清理机制正常工作

---

### 场景 3：多个角色和任务

**目标**：验证多个角色和任务的情况下仍能正常工作

#### 步骤 1：创建多个角色和任务

1. 创建 3 个不同的角色
2. 为每个角色创建战斗任务
3. 确保所有任务都在运行

#### 步骤 2：等待并关闭

```bash
sleep 10
# Ctrl+C
```

**预期日志**：
```
战斗快照保存完成: 成功 3 个，失败 0 个
计划暂停完成: 成功 3 个，失败 0 个
```

**验证点**：
- [ ] 所有快照都被删除
- [ ] 所有计划都被暂停

#### 步骤 3：重启

```bash
dotnet run
```

**验证点**：
- [ ] 服务器成功启动
- [ ] 所有计划可以恢复

#### 步骤 4：再次关闭和重启

```bash
# Ctrl+C
dotnet run
```

**验证点**：
- [ ] 第二次重启成功 ✅✅
- [ ] 所有角色的任务都正常

---

### 场景 4：压力测试

**目标**：测试快速连续的关闭和重启

#### 步骤：快速循环测试

```bash
cd BlazorIdle.Server

# 创建测试脚本
cat > test_multiple_restarts.sh << 'EOF'
#!/bin/bash
for i in {1..5}; do
    echo "=== 第 $i 次启动 ==="
    
    # 启动服务器
    dotnet run &
    PID=$!
    
    # 等待启动完成
    sleep 10
    
    # 正常关闭
    kill -SIGTERM $PID
    wait $PID
    
    # 检查数据库
    sqlite3 gamedata.db "PRAGMA integrity_check;" | grep -q "ok" || {
        echo "错误：数据库损坏！"
        exit 1
    }
    
    echo "第 $i 次测试通过"
    sleep 2
done

echo "所有测试通过！✅"
EOF

chmod +x test_multiple_restarts.sh
./test_multiple_restarts.sh
```

**验证点**：
- [ ] 所有 5 次重启都成功
- [ ] 数据库始终完整
- [ ] 没有错误日志

---

## 问题排查

### 问题 1：启动失败

**症状**：
```
An error occurred while updating the database
Unable to open database file
```

**排查步骤**：

```bash
# 1. 检查文件权限
ls -l gamedata.db*

# 2. 检查数据库完整性
sqlite3 gamedata.db "PRAGMA integrity_check;"

# 3. 如果损坏，尝试恢复
sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE);"

# 4. 查看详细日志
tail -n 100 logs/application.log
```

### 问题 2：快照未被删除

**症状**：日志中看到"孤立快照"警告

**排查步骤**：

```bash
# 检查快照表
sqlite3 gamedata.db "SELECT * FROM RunningBattleSnapshots;"

# 手动清理
sqlite3 gamedata.db "DELETE FROM RunningBattleSnapshots;"

# 重新启动
dotnet run
```

**原因**：可能是上次关闭时删除失败

**解决方案**：自动清理机制会在下次启动时处理

### 问题 3：计划状态不一致

**症状**：计划显示 Running 但没有对应的战斗

**排查步骤**：

```bash
# 检查计划状态
sqlite3 gamedata.db << EOF
SELECT p.Id, p.State, p.BattleId, s.StepBattleId
FROM ActivityPlans p
LEFT JOIN RunningBattleSnapshots s ON p.BattleId = s.StepBattleId;
EOF

# 手动修复
sqlite3 gamedata.db << EOF
UPDATE ActivityPlans 
SET State = 'Paused', BattleId = NULL 
WHERE State = 'Running';
EOF
```

### 问题 4：性能问题

**症状**：启动或关闭时间过长

**排查步骤**：

```bash
# 检查 WAL 文件大小
ls -lh gamedata.db-wal

# 如果过大（>10MB），执行检查点
sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE);"

# 检查表大小
sqlite3 gamedata.db << EOF
SELECT name, COUNT(*) 
FROM sqlite_master 
WHERE type='table' 
GROUP BY name;
EOF
```

---

## 测试检查清单

### 基本功能
- [ ] 第一次启动成功
- [ ] 创建战斗任务成功
- [ ] 第一次关闭成功
- [ ] 第一次重启成功
- [ ] 第二次关闭成功
- [ ] 第二次重启成功 ✅✅（关键）

### 数据一致性
- [ ] 关闭时快照被删除
- [ ] 关闭时计划被暂停
- [ ] 启动时清理孤立计划
- [ ] 启动时清理孤立快照
- [ ] 数据库始终完整

### 容错性
- [ ] 强制终止后能恢复
- [ ] 多次重启都能成功
- [ ] 多个任务都能正常处理
- [ ] 快速重启也能工作

### 性能
- [ ] 启动时间 < 3 秒
- [ ] 关闭时间 < 5 秒
- [ ] 没有内存泄漏
- [ ] CPU 使用正常

---

## 成功标准

### 必须满足（P0）
- ✅ 第二次重启能够成功启动
- ✅ 不需要手动删除数据库
- ✅ 数据库不会损坏
- ✅ 快照和计划状态一致

### 应该满足（P1）
- ✅ 启动时自动清理孤立数据
- ✅ 关闭时正确删除快照
- ✅ 有清晰的日志输出
- ✅ 错误能够自动恢复

### 最好满足（P2）
- ✅ 性能影响 < 1 秒
- ✅ 可以多次快速重启
- ✅ 支持多个并发任务
- ✅ 日志易于理解

---

## 报告问题

如果测试失败，请收集以下信息：

1. **错误日志**：
   ```bash
   cat logs/application.log | grep -A 10 -B 10 "Error\|Exception"
   ```

2. **数据库状态**：
   ```bash
   sqlite3 gamedata.db << EOF
   .schema ActivityPlans
   .schema RunningBattleSnapshots
   SELECT * FROM ActivityPlans;
   SELECT * FROM RunningBattleSnapshots;
   PRAGMA integrity_check;
   EOF
   ```

3. **重现步骤**：详细描述操作步骤

4. **环境信息**：
   ```bash
   dotnet --version
   sqlite3 --version
   uname -a
   ```

---

**测试人员**：_________________  
**测试日期**：_________________  
**测试结果**：[ ] 通过 / [ ] 失败  
**备注**：_________________
