# 快速测试指南 - 服务器优雅关闭修复

## 测试前准备

1. 拉取最新代码:
```bash
git pull origin copilot/fix-server-shutdown-issues
```

2. 构建项目:
```bash
dotnet build
```

3. 运行单元测试验证:
```bash
dotnet test --filter "FullyQualifiedName~GracefulShutdownTests"
```
预期结果: 3 个测试全部通过 ✅

## 测试场景 1: 基本功能测试 (5分钟)

### 步骤:
1. **启动服务器**
   ```bash
   cd BlazorIdle.Server
   dotnet run
   ```

2. **创建角色并开始战斗**
   - 打开浏览器访问前端
   - 创建一个新角色
   - 设置战斗计划任务（例如：持续战斗 60 秒）
   - 确认战斗已开始运行

3. **优雅关闭服务器**
   - 在服务器终端按 `Ctrl+C`
   - 观察日志输出

4. **验证日志**
   应该看到以下关键日志:
   ```
   ✅ 离线检测服务正在关闭，执行最后一次检查...
   ✅ StepBattleHostedService shutting down gracefully...
   ✅ Saved X running battle snapshots during shutdown
   ✅ 数据库清理服务正在关闭，执行数据库清理...
   ✅ 已执行 SQLite WAL 检查点
   ```

5. **重启服务器**
   ```bash
   dotnet run
   ```

6. **验证恢复**
   - 服务器应该正常启动（无数据库错误）
   - 登录前端
   - 查看角色的战斗计划状态
   - 应该显示为 "Paused"（暂停）
   - 可以继续战斗

### ✅ 成功标准:
- [ ] 服务器正常关闭（无错误）
- [ ] 日志显示优雅关闭消息
- [ ] 服务器可以正常重启
- [ ] 战斗计划恢复为暂停状态
- [ ] 可以继续战斗

## 测试场景 2: 多角色压力测试 (10分钟)

### 步骤:
1. **创建 3 个角色**
   - 角色1：战士，设置持续战斗
   - 角色2：游侠，设置持续战斗
   - 角色3：战士，设置持续战斗

2. **启动所有战斗**
   - 确认所有 3 个角色的战斗都在运行

3. **关闭服务器**
   - `Ctrl+C`
   - 观察日志应该显示暂停了 3 个计划

4. **重启并验证**
   - 所有 3 个角色的战斗计划都应该是暂停状态
   - 所有战斗状态都应该保存

### ✅ 成功标准:
- [ ] 3 个战斗计划都被暂停
- [ ] 3 个战斗状态都被保存
- [ ] 服务器重启后可以恢复所有计划

## 测试场景 3: 数据库完整性验证

### 步骤:
1. **关闭服务器后检查数据库**
   ```bash
   sqlite3 BlazorIdle.Server/gamedata.db "PRAGMA integrity_check;"
   ```
   预期输出: `ok`

2. **检查 WAL 文件**
   ```bash
   ls -lh BlazorIdle.Server/*.db*
   ```
   应该看到:
   - `gamedata.db` (主数据库)
   - `gamedata.db-wal` (WAL 文件，可能很小或不存在)
   - `gamedata.db-shm` (共享内存文件)

3. **WAL 检查点验证**
   ```bash
   sqlite3 BlazorIdle.Server/gamedata.db "PRAGMA wal_checkpoint(PASSIVE);"
   ```
   预期输出: `0|X|Y` (表示成功)

### ✅ 成功标准:
- [ ] 数据库完整性检查通过
- [ ] WAL 文件存在且较小（< 100KB）
- [ ] 检查点执行成功

## 测试场景 4: 异常情况测试

### 测试 4.1: 战斗运行中强制关闭
⚠️ **注意**: 这是测试容错性，不是推荐的关闭方式

1. 启动战斗
2. 使用 `kill` (不是 `kill -9`) 终止进程
3. 重启服务器
4. 验证数据库仍然可以正常加载

### 测试 4.2: 快速重复关闭/启动
1. 启动服务器
2. 立即按 `Ctrl+C` 关闭
3. 重复 3-5 次
4. 验证数据库没有损坏

## 常见问题排查

### 问题 1: 服务器关闭时间过长（超过 10 秒）

**原因**: 可能有大量运行中的战斗或数据库操作

**解决**: 
- 查看日志，找出哪个步骤耗时
- 减少同时运行的战斗数量
- 检查数据库大小（如果太大考虑清理）

### 问题 2: 重启后战斗计划仍显示 Running

**原因**: 可能优雅关闭未完成

**解决**:
- 检查关闭时的日志
- 确认看到 "StepBattleHostedService shutting down gracefully"
- 如果没有，说明服务器被强制终止

### 问题 3: 数据库损坏错误

**原因**: 可能使用了 `kill -9` 或断电

**解决**:
- 尝试 SQLite 修复工具
- 从备份恢复（如果有）
- 检查 WAL 文件是否存在

### 问题 4: WAL 文件过大

**原因**: 长时间运行未执行检查点

**解决**:
- 正常关闭一次服务器（会自动执行检查点）
- 或手动执行: `PRAGMA wal_checkpoint(TRUNCATE);`

## 性能监控

### 关闭时间监控
正常情况下的关闭时间:
- 0 个战斗: < 1 秒
- 1-5 个战斗: 1-3 秒
- 5-20 个战斗: 3-5 秒
- > 20 个战斗: 可能需要更长时间

### 内存使用监控
关闭前后内存使用应该相差不大（< 10MB）

## 日志级别设置

如果需要更详细的调试信息，修改 `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "BlazorIdle.Server.Services": "Debug",
      "BlazorIdle.Server.Application.Battles": "Debug"
    }
  }
}
```

## 快速命令参考

```bash
# 运行单元测试
dotnet test --filter "GracefulShutdownTests"

# 检查数据库完整性
sqlite3 BlazorIdle.Server/gamedata.db "PRAGMA integrity_check;"

# 查看数据库 WAL 状态
sqlite3 BlazorIdle.Server/gamedata.db "PRAGMA wal_checkpoint;"

# 查看数据库大小
ls -lh BlazorIdle.Server/*.db*

# 查看运行中的计划（需要先启动服务器）
sqlite3 BlazorIdle.Server/gamedata.db "SELECT Id, CharacterId, State FROM ActivityPlans WHERE State = 'Running';"
```

## 测试报告模板

完成测试后，请记录:

```
测试日期: ____________________
测试人: ____________________

场景 1 - 基本功能:
[ ] 通过  [ ] 失败  备注: _______________________

场景 2 - 多角色压力:
[ ] 通过  [ ] 失败  备注: _______________________

场景 3 - 数据库完整性:
[ ] 通过  [ ] 失败  备注: _______________________

场景 4 - 异常情况:
[ ] 通过  [ ] 失败  备注: _______________________

发现的问题:
_________________________________________________
_________________________________________________

总体评价:
[ ] 符合预期  [ ] 需要改进  [ ] 存在严重问题
```

## 获取帮助

如果遇到问题:
1. 查看 `docs/GRACEFUL_SHUTDOWN_FIX.md` 详细技术文档
2. 查看 `SHUTDOWN_FIX_SUMMARY_CN.md` 中文总结
3. 检查服务器日志
4. 提供以下信息寻求帮助:
   - 操作系统和版本
   - .NET 版本
   - 完整的错误日志
   - 重现步骤

祝测试顺利！🎉
