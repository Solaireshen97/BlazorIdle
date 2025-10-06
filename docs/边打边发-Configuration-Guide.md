# 边打边发 - 配置指南

## 概述

本文档说明如何配置和调优边打边发系统。

## 配置文件

### appsettings.json

```json
{
  "Combat": {
    "EnablePeriodicRewards": true,
    "RewardFlushIntervalSeconds": 10.0
  },
  "Economy": {
    "DefaultDropMode": "expected"
  }
}
```

## 配置项详解

### Combat 配置

#### EnablePeriodicRewards
- **类型**: Boolean
- **默认值**: true
- **说明**: 是否启用边打边发功能
- **使用场景**:
  - `true`: 战斗过程中周期性发放奖励
  - `false`: 仅在战斗结束时发放奖励（传统模式）

**何时禁用**:
- 调试战斗逻辑时
- 性能测试时暂时关闭
- 经济系统维护期间

#### RewardFlushIntervalSeconds
- **类型**: Double
- **默认值**: 10.0
- **单位**: 秒（模拟时间）
- **说明**: 奖励发放的时间间隔
- **建议值**:
  - 快节奏战斗: 5.0 秒
  - 标准战斗: 10.0 秒
  - 慢节奏战斗: 15.0 - 30.0 秒

**调优建议**:
- 值越小，发放越频繁，玩家体验更实时，但数据库压力更大
- 值越大，发放越少，数据库压力小，但玩家体验延迟增加
- 建议根据服务器负载和玩家反馈调整

### Economy 配置

#### DefaultDropMode
- **类型**: String
- **可选值**: "expected", "sampled"
- **默认值**: "expected"
- **说明**: 默认的掉落计算模式
- **注意**: 边打边发仅在 sampled 模式下工作

**模式对比**:
- `expected`: 期望值模式，计算数学期望，不实际抽样
- `sampled`: 抽样模式，实际随机抽取，结果会有波动

## 环境特定配置

### 开发环境 (appsettings.Development.json)

```json
{
  "Combat": {
    "EnablePeriodicRewards": true,
    "RewardFlushIntervalSeconds": 5.0
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Application.Economy": "Debug",
      "BlazorIdle.Server.Application.Battles": "Debug"
    }
  }
}
```

**开发环境建议**:
- 使用较短的间隔（5秒）便于快速测试
- 启用 Debug 日志查看详细信息
- 可以临时禁用 EnablePeriodicRewards 来测试传统模式

### 生产环境 (appsettings.Production.json)

```json
{
  "Combat": {
    "EnablePeriodicRewards": true,
    "RewardFlushIntervalSeconds": 10.0
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Application.Economy": "Information",
      "BlazorIdle.Server.Application.Battles": "Warning"
    }
  }
}
```

**生产环境建议**:
- 使用标准间隔（10秒）平衡体验和性能
- 仅记录 Warning 及以上日志
- 启用应用监控和告警

## 性能调优

### 数据库连接池

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=gamedata.db;Pooling=true;Max Pool Size=100;Min Pool Size=10;"
  }
}
```

### 并发控制

虽然当前实现没有直接暴露并发配置，但可以通过调整 StepBattleHostedService 的推进频率来间接控制：

```csharp
// StepBattleHostedService.cs
await Task.Delay(50, stoppingToken);  // 调整这个值
```

- 值越小（如 25ms），推进越频繁，奖励发放也越及时
- 值越大（如 100ms），CPU 占用越少，但响应性降低

## 监控配置

### 日志级别

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BlazorIdle.Server.Application.Economy.RewardGrantService": "Information",
      "BlazorIdle.Server.Application.Battles.Step": "Information"
    }
  }
}
```

**日志级别说明**:
- `Debug`: 记录详细的发放过程，包括幂等性检查
- `Information`: 记录成功的奖励发放
- `Warning`: 记录发放失败和重试
- `Error`: 记录严重错误

### 关键日志消息

**成功发放**:
```
[Information] Granted rewards to character {CharacterId}: Gold={Gold}, Exp={Exp}, Items={ItemCount}
```

**幂等拦截**:
```
[Debug] Reward already granted for key: {Key}
```

**发放失败**:
```
[Error] Failed to grant rewards for key: {Key}
```

## 故障排除

### 问题：奖励发放过于频繁，数据库压力大

**解决方案**:
1. 增加 `RewardFlushIntervalSeconds` 到 15-30 秒
2. 检查是否有多余的战斗实例未清理
3. 考虑使用数据库连接池优化

### 问题：玩家反馈奖励延迟

**解决方案**:
1. 减少 `RewardFlushIntervalSeconds` 到 5-7 秒
2. 检查服务器负载和数据库响应时间
3. 优化数据库索引

### 问题：测试时需要禁用边打边发

**解决方案**:
```json
{
  "Combat": {
    "EnablePeriodicRewards": false
  }
}
```

### 问题：开发环境奖励发放太慢

**解决方案**:
```json
{
  "Combat": {
    "RewardFlushIntervalSeconds": 2.0
  }
}
```

## 最佳实践

### 1. 生产部署前检查清单
- [ ] 确认 EnablePeriodicRewards 已启用
- [ ] 验证 RewardFlushIntervalSeconds 设置合理
- [ ] 检查数据库连接池配置
- [ ] 确认日志级别不是 Debug
- [ ] 测试数据库性能是否满足需求

### 2. 性能基线
建议在生产环境建立性能基线：
- 平均奖励发放延迟 < 100ms
- 数据库连接池利用率 < 80%
- CPU 占用率 < 70%
- 内存占用稳定

### 3. 容量规划
根据玩家数量调整配置：

| 在线玩家数 | RewardFlushInterval | 数据库连接池 | 预期 TPS |
|-----------|---------------------|-------------|---------|
| < 100     | 5.0s                | 20          | ~20     |
| 100-500   | 10.0s               | 50          | ~50     |
| 500-1000  | 15.0s               | 80          | ~70     |
| > 1000    | 20.0s               | 100+        | ~100    |

## 配置验证

### 启动时检查

在应用启动时，可以添加配置验证：

```csharp
var enablePeriodicRewards = config.GetValue<bool>("Combat:EnablePeriodicRewards");
var flushInterval = config.GetValue<double>("Combat:RewardFlushIntervalSeconds");

if (flushInterval < 1.0 || flushInterval > 300.0)
{
    logger.LogWarning("RewardFlushIntervalSeconds={Interval} is out of recommended range (1-300)", flushInterval);
}
```

### 运行时监控

建议监控以下指标：
- 每分钟奖励发放次数
- 奖励发放平均延迟
- 幂等性拦截率
- 数据库事务失败率

## 总结

边打边发系统的配置主要通过 `RewardFlushIntervalSeconds` 来平衡玩家体验和系统性能。建议：
1. 开发环境使用较短间隔（5秒）
2. 生产环境使用标准间隔（10秒）
3. 根据监控数据动态调整
4. 定期评估和优化配置
