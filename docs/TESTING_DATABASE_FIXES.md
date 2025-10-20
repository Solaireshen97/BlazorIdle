# 数据库安全性改进测试指南

## 测试环境准备

### 前置条件
- 已编译的服务器可执行文件
- SQLite 命令行工具（可选，用于数据库检查）
- 能够访问服务器日志

### 启动服务器
```bash
cd BlazorIdle.Server
dotnet run
```

## 测试场景

### ✅ 场景 1：正常关闭时保存战斗快照

**目标**：验证服务器在正常关闭时能够保存所有运行中的战斗快照

**步骤**：
1. 启动服务器
2. 创建一个角色
3. 开始一个战斗任务（例如：连续战斗或副本战斗）
4. 等待 5-10 秒让战斗运行
5. 使用 `Ctrl+C` 关闭服务器
6. 观察服务器日志

**预期结果**：
- 日志中应包含：`"StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照..."`
- 日志中应包含：`"已保存战斗快照: {BattleId}"`
- 日志中应包含：`"战斗快照保存完成: 成功 X 个，失败 0 个"`
- 服务器应在 2-3 秒内完全关闭

**验证**：
```bash
# 检查数据库文件
ls -lh gamedata.db*

# 重新启动服务器
dotnet run

# 服务器应该能够正常启动
# 战斗快照应该被恢复
```

---

### ✅ 场景 2：突然关闭后数据库恢复

**目标**：验证服务器在突然终止后数据库不会损坏

**步骤**：
1. 启动服务器
2. 创建一个角色
3. 开始一个战斗任务
4. 等待 5-10 秒
5. 使用 `kill -9 <pid>` 或任务管理器强制终止服务器进程
   ```bash
   # 查找进程 ID
   ps aux | grep dotnet
   
   # 强制终止
   kill -9 <pid>
   ```
6. 尝试重新启动服务器

**预期结果**：
- 服务器应该能够正常启动（可能会有警告但不应该崩溃）
- 数据库文件应该没有损坏
- WAL 文件可能存在但会被自动恢复

**验证**：
```bash
# 检查数据库完整性
sqlite3 gamedata.db "PRAGMA integrity_check"
# 应该输出: ok

# 检查 WAL 文件
ls -lh gamedata.db-wal
# 可能存在，但不应该很大（< 10MB）

# 重新启动服务器
dotnet run
# 应该能够正常启动
```

**如果服务器无法启动**：
```bash
# 手动执行 WAL 检查点
sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE)"

# 再次检查完整性
sqlite3 gamedata.db "PRAGMA integrity_check"

# 重新启动服务器
dotnet run
```

---

### ✅ 场景 3：并发数据库写入

**目标**：验证多个并发操作不会导致数据库死锁或损坏

**步骤**：
1. 启动服务器
2. 创建 3-5 个角色
3. 为每个角色开始战斗任务
4. 打开多个浏览器标签页/窗口
5. 在每个标签页中切换不同的角色
6. 同时执行以下操作：
   - 发送心跳请求（自动，每 30 秒）
   - 查看战斗状态
   - 停止/启动战斗任务
7. 持续 2-3 分钟
8. 正常关闭服务器

**预期结果**：
- 所有操作应该成功
- 日志中可能出现重试消息（这是正常的）
- 没有死锁错误
- 服务器关闭时保存所有战斗快照

**验证日志**：
```bash
# 查找重试消息
grep "重试" logs/application.log
# 应该看到一些重试，但最终都成功

# 查找死锁或超时错误
grep -i "deadlock\|timeout" logs/application.log
# 不应该有致命错误
```

---

### ✅ 场景 4：长时间运行后关闭

**目标**：验证长时间运行后数据库仍然健康

**步骤**：
1. 启动服务器
2. 创建角色并开始战斗任务
3. 让服务器运行 30 分钟到 1 小时
4. 期间可以偶尔查看战斗状态
5. 检查 WAL 文件大小
   ```bash
   ls -lh gamedata.db-wal
   ```
6. 正常关闭服务器

**预期结果**：
- WAL 文件大小应该 < 10MB（自动检查点工作正常）
- 服务器关闭时执行最终检查点
- 数据库完整性良好

**验证**：
```bash
# 检查数据库大小
ls -lh gamedata.db*

# 关闭后 WAL 文件应该很小或为空
ls -lh gamedata.db-wal

# 验证完整性
sqlite3 gamedata.db "PRAGMA integrity_check"
```

---

### ✅ 场景 5：离线玩家暂停机制

**目标**：验证离线检测服务与数据库安全改进兼容

**步骤**：
1. 启动服务器
2. 创建角色并开始战斗任务
3. 停止发送心跳（关闭浏览器但保持服务器运行）
4. 等待 60-90 秒（超过离线检测阈值）
5. 检查日志，应该看到计划被暂停
6. 重新打开浏览器并发送心跳
7. 正常关闭服务器

**预期结果**：
- 离线检测服务正常工作
- 计划被暂停并保存战斗状态
- 重新连接后可以恢复战斗
- 关闭时没有错误

**日志关键字**：
```
检测到玩家 {CharacterId} 已离线
暂停计划 {PlanId}
```

---

## 性能测试

### 测量重试延迟

**目的**：了解重试策略对性能的影响

**步骤**：
1. 启用详细日志记录
2. 创建高并发场景（5+ 角色战斗）
3. 记录操作响应时间
4. 检查重试日志

**分析**：
```bash
# 统计重试次数
grep -c "正在重试" logs/application.log

# 查看重试延迟
grep "等待.*ms" logs/application.log | tail -20
```

**预期**：
- 大多数操作不需要重试
- 重试的操作最终都应该成功
- 平均延迟增加 < 5%

---

### 测量关闭时间

**步骤**：
1. 记录关闭开始时间
2. 正常关闭服务器
3. 记录完全关闭的时间

**预期**：
- 关闭时间：< 3 秒（2 秒缓冲 + 清理）
- 所有快照成功保存

---

## 故障恢复测试

### 模拟磁盘满

**警告**：仅在测试环境执行

**步骤**：
1. 限制数据库所在分区的可用空间
2. 启动服务器并运行战斗
3. 观察行为

**预期**：
- 应该有清晰的错误消息
- 不应该导致数据损坏
- 释放空间后应该能够恢复

---

### 模拟文件权限问题

**步骤**：
1. 修改数据库文件为只读
   ```bash
   chmod 444 gamedata.db
   ```
2. 尝试启动服务器
3. 观察错误处理

**预期**：
- 清晰的权限错误消息
- 不应该崩溃

**恢复**：
```bash
chmod 644 gamedata.db
```

---

## 自动化测试脚本

### Bash 脚本：测试正常关闭

```bash
#!/bin/bash

echo "测试正常关闭..."

# 启动服务器
cd BlazorIdle.Server
dotnet run &
SERVER_PID=$!

# 等待启动
sleep 10

# 发送关闭信号
kill -SIGTERM $SERVER_PID

# 等待关闭
sleep 5

# 检查进程是否已停止
if ps -p $SERVER_PID > /dev/null; then
   echo "❌ 服务器未正常关闭"
   kill -9 $SERVER_PID
   exit 1
else
   echo "✅ 服务器正常关闭"
fi

# 检查数据库完整性
if sqlite3 gamedata.db "PRAGMA integrity_check" | grep -q "ok"; then
   echo "✅ 数据库完整性良好"
else
   echo "❌ 数据库完整性检查失败"
   exit 1
fi

echo "测试完成"
```

### PowerShell 脚本：测试突然关闭

```powershell
Write-Host "测试突然关闭..."

# 启动服务器
cd BlazorIdle.Server
$process = Start-Process dotnet -ArgumentList "run" -PassThru

# 等待启动
Start-Sleep -Seconds 10

# 强制终止
$process.Kill()
Start-Sleep -Seconds 5

# 检查数据库
$integrityCheck = & sqlite3 gamedata.db "PRAGMA integrity_check"
if ($integrityCheck -eq "ok") {
    Write-Host "✅ 数据库完整性良好"
} else {
    Write-Host "❌ 数据库完整性检查失败"
    exit 1
}

# 尝试重新启动
$process2 = Start-Process dotnet -ArgumentList "run" -PassThru
Start-Sleep -Seconds 10

if ($process2.HasExited) {
    Write-Host "❌ 服务器重启失败"
    exit 1
} else {
    Write-Host "✅ 服务器重启成功"
    $process2.Kill()
}

Write-Host "测试完成"
```

---

## 监控和日志

### 关键日志文件

```bash
# 应用程序日志
tail -f logs/application.log

# 数据库操作日志
grep "SaveChanges\|PRAGMA\|checkpoint" logs/application.log

# 关闭事件日志
grep "优雅关闭\|graceful\|shutdown" logs/application.log
```

### 数据库统计

```bash
# WAL 文件大小趋势
watch -n 5 'ls -lh gamedata.db-wal'

# 数据库页面数
sqlite3 gamedata.db "PRAGMA page_count"

# WAL 模式确认
sqlite3 gamedata.db "PRAGMA journal_mode"
# 应该输出: wal
```

---

## 问题报告模板

如果发现问题，请提供以下信息：

```
### 问题描述
[简要描述问题]

### 复现步骤
1. 
2. 
3. 

### 预期行为
[描述预期应该发生什么]

### 实际行为
[描述实际发生了什么]

### 环境信息
- 操作系统: 
- .NET 版本: 
- 数据库文件大小: 
- WAL 文件大小: 

### 日志片段
```
[粘贴相关日志]
```

### 数据库状态
```bash
sqlite3 gamedata.db "PRAGMA integrity_check"
[粘贴输出]
```

### 额外信息
[任何其他相关信息]
```

---

## 成功标准

### 必须通过的测试

- ✅ 场景 1：正常关闭保存快照
- ✅ 场景 2：突然关闭后恢复
- ✅ 场景 3：并发写入无死锁

### 性能要求

- ✅ 关闭时间 < 5 秒
- ✅ 重试操作成功率 > 95%
- ✅ WAL 文件大小 < 10MB

### 稳定性要求

- ✅ 数据库完整性始终为 "ok"
- ✅ 无数据丢失
- ✅ 崩溃后能够恢复
