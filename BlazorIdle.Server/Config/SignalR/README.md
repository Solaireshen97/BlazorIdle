# SignalR 配置文件说明

本目录包含 SignalR 系统的配置文件。

**最后更新**: 2025-10-14  
**当前状态**: ✅ 生产就绪，已完成所有后端功能验证

## 文件结构

- `signalr-config.json` - 基础配置文件（所有配置的默认值）
- `signalr-config.Development.json` - 开发环境覆盖配置（详细日志 + 短节流窗口）
- `signalr-config.Production.json` - 生产环境覆盖配置（优化性能）
- `signalr-config.schema.json` - JSON Schema 定义（可选，用于 IDE 自动完成）

## 配置优先级

配置按以下优先级合并：
1. 基础配置 (`signalr-config.json`)
2. 环境特定配置 (`signalr-config.{Environment}.json`)
3. 环境变量
4. 命令行参数

## 配置项说明

### 主要配置类别

1. **基础配置** (8 个参数)
   - Hub 端点路径
   - 启用/禁用开关
   - 连接超时设置
   - 重连策略

2. **通知配置** (7 个参数)
   - 玩家死亡/复活通知
   - 敌人击杀通知
   - 目标切换通知
   - Wave/技能/Buff 通知（预留）

3. **性能配置** (5 个参数)
   - 通知节流
   - 批量发送（预留）
   - 移动端降级（预留）

详细配置说明请参考：
- `/docs/SignalR配置优化指南.md` - 完整配置参考
- `/docs/SignalR性能优化指南.md` - 性能优化建议
- `/docs/SignalR_实施完成报告_2025-10-14.md` - 最新验证报告

## 最佳实践

1. **开发环境**：启用详细日志和较短的节流窗口便于调试
2. **生产环境**：禁用详细日志，使用标准节流窗口以优化性能
3. **测试环境**：可临时禁用 SignalR 或特定通知类型进行测试

## 验证

### 自动验证

启动应用时会自动验证配置（通过 `SignalRStartupValidator`），验证失败将终止启动并输出详细错误信息。

验证内容包括：
- Hub 端点格式
- 数值范围（重连次数、延迟、超时等）
- 必填参数

### 手动验证

运行测试验证配置正确性：
```bash
dotnet test --filter "SignalRConfiguration"
```

### 测试覆盖

✅ 56 个 SignalR 测试全部通过  
✅ 包含配置验证、集成测试、性能测试
