# 战斗计划任务数据库异常修复 - 测试指南

## 测试目的

验证修复后的服务器能够在有活动计划（特别是暂停的计划）的情况下正常启动，并且数据库保持一致性。

## 前置条件

- .NET 9.0 SDK 已安装
- SQLite 已安装（用于数据库检查）
- 项目已编译：`dotnet build`

## 测试场景

### 场景 1：正常启动（无活动计划）

**步骤：**
1. 确保数据库中没有活动计划
2. 启动服务器：`cd BlazorIdle.Server && dotnet run`
3. 观察日志输出

**预期结果：**
```
info: Program[0]
      Executing SQLite WAL checkpoint...
info: Program[0]
      SQLite WAL checkpoint completed.
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      StepBattleHostedService starting; recovering snapshots...
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      Recovering paused plans...
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      Found 0 paused plans to recover
```

服务器应该成功启动，没有错误。

### 场景 2：启动时有运行中的计划

**步骤：**
1. 创建一个战斗计划并启动
2. 让计划保持运行状态（State=1）
3. 重启服务器

**预期结果：**
- 服务器成功启动
- WAL 检查点执行成功
- 运行中的计划继续执行
- 没有数据库锁定错误

### 场景 3：启动时有暂停的计划（修复的关键场景）

**步骤：**
1. 创建一个战斗计划并启动
2. 手动设置计划为暂停状态：
   ```bash
   cd BlazorIdle.Server
   sqlite3 gamedata.db "UPDATE ActivityPlans SET State = 4 WHERE State = 1 LIMIT 1;"
   ```
3. 重启服务器

**预期结果：**
```
info: Program[0]
      Executing SQLite WAL checkpoint...
info: Program[0]
      SQLite WAL checkpoint completed.
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      StepBattleHostedService starting; recovering snapshots...
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      Recovering paused plans...
info: BlazorIdle.Server.Application.Battles.Step.StepBattleHostedService[0]
      Found 1 paused plans to recover
```

服务器应该：
- ✅ 成功启动
- ✅ 找到暂停的计划
- ✅ 尝试恢复计划（如果角色在线）或保持暂停状态（如果角色离线）
- ✅ 没有数据库相关的异常

### 场景 4：验证 WAL 检查点

**步骤：**
1. 记录启动前的 WAL 文件大小：
   ```bash
   ls -lh BlazorIdle.Server/gamedata.db-wal
   ```
2. 启动服务器
3. 等待服务器完全启动（约 10-15 秒）
4. 停止服务器
5. 再次检查 WAL 文件大小

**预期结果：**
- 启动后 WAL 文件大小应该显著减少
- 例如：从 198KB 减少到 8-16KB
- 这表明 WAL 检查点成功执行

### 场景 5：压力测试（多次重启）

**步骤：**
1. 创建多个战斗计划
2. 启动并运行一段时间
3. 停止服务器
4. 重复启动-停止循环 5-10 次

**预期结果：**
- 每次启动都成功
- 没有数据库损坏
- WAL 文件保持合理大小
- 没有累积的错误

## 验证检查清单

运行测试后，检查以下各项：

- [ ] 服务器启动时执行了 WAL 检查点
- [ ] 服务器在有活动计划时能够启动
- [ ] 暂停的计划被正确识别
- [ ] 没有 "database is locked" 错误
- [ ] 没有 "DbContext disposed" 错误
- [ ] WAL 文件大小在合理范围内（< 100KB）
- [ ] 前端能够连接到服务器
- [ ] 战斗通知服务正常工作
- [ ] 数据库查询正常执行

## 常见问题排查

### 问题 1：服务器无法启动

**可能原因：**
- 数据库文件损坏
- 端口被占用
- 配置文件错误

**解决方法：**
1. 检查服务器日志中的错误信息
2. 验证数据库完整性：`sqlite3 gamedata.db "PRAGMA integrity_check;"`
3. 检查端口：`netstat -ano | grep 5000`

### 问题 2：WAL 文件过大

**可能原因：**
- 检查点未执行
- 数据库高负载

**解决方法：**
1. 手动执行检查点：
   ```bash
   sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE);"
   ```
2. 检查是否有长时间运行的事务

### 问题 3：计划恢复失败

**可能原因：**
- 计划数据不完整
- 角色数据缺失

**解决方法：**
1. 检查日志中的具体错误信息
2. 验证计划数据：
   ```bash
   sqlite3 gamedata.db "SELECT * FROM ActivityPlans WHERE State = 4;"
   ```
3. 验证角色数据：
   ```bash
   sqlite3 gamedata.db "SELECT Id, LastSeenAtUtc FROM Characters;"
   ```

## 性能基准

修复后的预期性能指标：

- **启动时间**：增加约 1 秒（用于 WAL 检查点和延迟）
- **WAL 检查点时间**：通常 < 100ms（取决于 WAL 大小）
- **计划恢复时间**：每个计划约 50-200ms
- **内存开销**：每个独立作用域约 1-2MB（临时）

## 自动化测试脚本

可以使用以下脚本自动化测试：

```bash
#!/bin/bash
# 自动化测试脚本

cd /home/runner/work/BlazorIdle/BlazorIdle

echo "=== 测试 1: 编译项目 ==="
dotnet build BlazorIdle.Server/BlazorIdle.Server.csproj
if [ $? -ne 0 ]; then
    echo "❌ 编译失败"
    exit 1
fi
echo "✅ 编译成功"

echo ""
echo "=== 测试 2: 检查数据库 ==="
cd BlazorIdle.Server
if [ -f gamedata.db ]; then
    echo "✅ 数据库文件存在"
else
    echo "❌ 数据库文件不存在"
    exit 1
fi

echo ""
echo "=== 测试 3: 测试服务器启动 ==="
timeout 20 dotnet run --no-build > /tmp/test_output.log 2>&1 &
SERVER_PID=$!
sleep 15

if kill -0 $SERVER_PID 2>/dev/null; then
    echo "✅ 服务器成功启动"
    kill $SERVER_PID
else
    echo "❌ 服务器启动失败"
    cat /tmp/test_output.log
    exit 1
fi

echo ""
echo "=== 测试 4: 验证 WAL 检查点 ==="
if grep -q "SQLite WAL checkpoint completed" /tmp/test_output.log; then
    echo "✅ WAL 检查点执行成功"
else
    echo "❌ WAL 检查点未执行"
    exit 1
fi

echo ""
echo "=== 所有测试通过 ==="
```

## 总结

这些测试确保了修复后的系统能够：
1. 正确处理 DbContext 作用域
2. 在启动时执行 WAL 检查点
3. 恢复暂停的计划而不导致数据库错误
4. 在各种场景下稳定运行

如果所有测试都通过，可以确认修复已经成功解决了原始问题。
