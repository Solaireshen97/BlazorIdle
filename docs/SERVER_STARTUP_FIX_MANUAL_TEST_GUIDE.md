# 服务器启动故障修复 - 手动测试指南

## 问题背景
当角色设置了战斗的计划任务并开始战斗后，如果在前端仍然在线的情况下直接关闭服务器，下次启动会失败。这是因为ActivityPlan保持在Running状态，但对应的内存中的战斗实例已经丢失。

## 修复方案
1. **启动时清理**：检测并修复孤立的Running状态计划（没有对应战斗实例）
2. **关闭时暂停**：关闭服务器时自动暂停所有运行中的计划
3. **自动恢复**：下次启动时可以正确恢复这些暂停的计划

## 手动测试场景

### 测试1：前端在线时关闭服务器（主要场景）
**目标**：验证在前端在线时关闭服务器，重启后能够正常启动和恢复

**步骤**：
1. 启动服务器
   ```bash
   cd BlazorIdle.Server
   dotnet run
   ```

2. 打开前端并登录，创建角色并开始战斗任务
   - 访问 http://localhost:5000 或配置的前端地址
   - 创建角色
   - 在活动计划页面创建战斗计划
   - 启动战斗（确认计划状态变为Running）

3. **保持前端打开**（这是关键 - 模拟前端在线状态）

4. 关闭服务器（使用 Ctrl+C）
   - 观察日志，应该看到：
     ```
     StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照并暂停计划...
     已保存战斗快照: {BattleId}
     战斗快照保存完成: 成功 X 个，失败 0 个
     开始暂停 X 个运行中的计划
     已暂停计划 {PlanId} (角色 {CharacterId})
     计划暂停完成: 成功 X 个，失败 0 个
     ```

5. 重新启动服务器
   ```bash
   dotnet run
   ```

6. 观察启动日志：
   ```
   StepBattleHostedService starting; cleaning up orphaned running plans...
   没有发现孤立的运行中计划  # 应该是0个，因为已经暂停了
   Recovering battle snapshots...
   Recovering paused plans...
   服务器重启后恢复暂停的计划 {PlanId} (玩家 {CharacterId} 在线)
   ```

7. 刷新前端页面，验证：
   - 服务器正常运行
   - 战斗计划可以查看
   - 战斗状态正确显示（应该已恢复为Running状态，如果心跳仍然活跃）

**预期结果**：
✅ 服务器成功重启
✅ Swagger可以访问 (http://localhost:5xxx/swagger)
✅ 前端可以正常连接
✅ 战斗计划正确恢复

---

### 测试2：模拟崩溃重启（无优雅关闭）
**目标**：验证服务器崩溃或kill -9后能够恢复

**步骤**：
1. 启动服务器并开始战斗任务（同测试1的步骤1-2）

2. 强制终止服务器进程
   ```bash
   # 在另一个终端查找进程ID
   ps aux | grep "dotnet.*BlazorIdle.Server"
   
   # 强制终止
   kill -9 <PID>
   ```

3. 重新启动服务器

4. 观察启动日志：
   ```
   StepBattleHostedService starting; cleaning up orphaned running plans...
   发现 X 个孤立的运行中计划，将它们标记为暂停状态
   已将孤立的计划 {PlanId} (角色 {CharacterId}) 标记为暂停状态
   孤立计划清理完成，已处理 X 个计划
   ```

**预期结果**：
✅ 服务器成功启动（即使有孤立的Running计划）
✅ 孤立的计划被自动标记为Paused
✅ 数据库没有损坏
✅ 可以通过Swagger访问API

---

### 测试3：前端离线后关闭服务器
**目标**：验证前端离线后的正常流程

**步骤**：
1. 启动服务器并开始战斗任务

2. 关闭前端或等待60秒让服务器检测到离线
   - OfflineDetectionService会自动暂停离线玩家的计划

3. 等待观察日志：
   ```
   检测到玩家 {CharacterId} 已离线 XX 秒，暂停计划 {PlanId}
   ```

4. 正常关闭服务器（Ctrl+C）

5. 重新启动服务器

6. 观察日志：
   ```
   没有发现孤立的运行中计划  # 因为已经被离线检测暂停了
   ```

**预期结果**：
✅ 离线检测正常工作
✅ 服务器关闭前计划已暂停
✅ 重启时没有孤立计划需要清理

---

### 测试4：多个角色同时战斗
**目标**：验证多角色场景

**步骤**：
1. 创建3-5个角色，全部开始战斗任务

2. 在前端在线时关闭服务器

3. 重新启动并验证所有计划都正确恢复

**预期结果**：
✅ 所有计划都被正确暂停
✅ 重启后可以恢复所有计划
✅ 日志显示处理了所有计划

---

## 验证检查清单

在每次测试后，请验证以下内容：

### 服务器启动
- [ ] 服务器成功启动（没有异常或错误）
- [ ] 启动日志显示清理/恢复操作的信息
- [ ] Swagger UI可以访问 (http://localhost:5xxx/swagger)

### 数据库完整性
- [ ] 数据库文件没有损坏
- [ ] 可以查询ActivityPlans表
- [ ] 没有处于Running状态的孤立计划

```bash
# 检查数据库完整性
sqlite3 gamedata.db "PRAGMA integrity_check"
# 应该输出: ok

# 查看ActivityPlan状态
sqlite3 gamedata.db "SELECT Id, State, BattleId FROM ActivityPlans WHERE State = 'Running';"
# 如果服务器运行正常，应该有Running计划；如果刚启动清理完成，应该没有或很少
```

### 功能验证
- [ ] 前端可以连接到服务器
- [ ] 可以创建新的战斗计划
- [ ] 可以启动战斗计划
- [ ] 战斗状态正确更新
- [ ] 可以暂停和恢复计划

---

## 关键日志消息

### 启动时（正常情况 - 没有孤立计划）
```
StepBattleHostedService starting; cleaning up orphaned running plans...
没有发现孤立的运行中计划
Recovering battle snapshots...
Recovering paused plans...
StepBattleHostedService started.
```

### 启动时（有孤立计划需要清理）
```
StepBattleHostedService starting; cleaning up orphaned running plans...
发现 2 个孤立的运行中计划，将它们标记为暂停状态
已将孤立的计划 a1b2c3d4-... (角色 e5f6g7h8-...) 标记为暂停状态
已将孤立的计划 i9j0k1l2-... (角色 m3n4o5p6-...) 标记为暂停状态
孤立计划清理完成，已处理 2 个计划
```

### 关闭时（优雅关闭）
```
StepBattleHostedService 收到停止信号
StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照并暂停计划...
已保存战斗快照: a1b2c3d4-...
战斗快照保存完成: 成功 1 个，失败 0 个
开始暂停 1 个运行中的计划
已暂停计划 e5f6g7h8-... (角色 i9j0k1l2-...)
计划暂停完成: 成功 1 个，失败 0 个
StepBattleHostedService stopped.
```

---

## 故障排除

### 问题1：服务器仍然无法启动
**症状**：启动时崩溃或挂起

**解决方案**：
1. 检查数据库完整性
   ```bash
   sqlite3 gamedata.db "PRAGMA integrity_check"
   ```

2. 如果数据库损坏，执行WAL检查点
   ```bash
   sqlite3 gamedata.db "PRAGMA wal_checkpoint(TRUNCATE)"
   ```

3. 查看详细日志
   ```bash
   dotnet run --verbosity detailed
   ```

### 问题2：计划状态不正确
**症状**：计划显示Running但实际没有战斗

**解决方案**：
1. 手动运行清理查询
   ```sql
   -- 查看所有Running计划
   SELECT * FROM ActivityPlans WHERE State = 'Running';
   
   -- 如果需要，手动标记为Paused
   UPDATE ActivityPlans SET State = 'Paused', BattleId = NULL WHERE State = 'Running';
   ```

2. 重启服务器让清理逻辑自动处理

### 问题3：找不到日志消息
**症状**：看不到清理或暂停的日志

**解决方案**：
1. 确认日志级别配置（appsettings.json）
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "BlazorIdle.Server.Application.Battles.Step": "Information"
       }
     }
   }
   ```

2. 使用控制台输出查看实时日志

---

## 性能影响

### 启动时
- **清理孤立计划**: < 1秒（取决于计划数量）
- **恢复战斗快照**: 1-5秒（取决于快照数量和大小）
- **恢复暂停计划**: < 1秒

### 关闭时
- **保存战斗快照**: < 1秒
- **暂停所有计划**: < 1秒
- **总延迟**: < 3秒（加上GracefulShutdownCoordinator的2秒缓冲）

---

## 总结

这个修复确保了：
1. ✅ 服务器可以安全地在任何时候重启
2. ✅ 战斗计划不会进入不一致状态
3. ✅ 数据库不会损坏
4. ✅ 用户体验不受影响（自动恢复）

如果所有测试都通过，这个修复就可以部署到生产环境了！
