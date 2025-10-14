# 战斗循环配置

## 文件说明

- `combat-loop-config.json` - 基础配置文件
- `combat-loop-config.Development.json` - 开发环境配置覆盖
- `combat-loop-config.Production.json` - 生产环境配置覆盖

## 配置参数说明

### 全局配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `AttackStartsImmediately` | bool | false | 攻击轨道是否在战斗开始时立即触发 |
| `SpecialStartsImmediately` | bool | false | 特殊轨道是否在战斗开始时立即触发 |
| `PauseAttackDuringSpawnWait` | bool | true | 刷新等待时是否暂停攻击轨道 |
| `PauseSpecialDuringSpawnWait` | bool | true | 刷新等待时是否暂停特殊轨道 |
| `MinimumSpawnDelayForPause` | double | 0.001 | 最小刷新延迟阈值（秒） |
| `EnableTargetConsistency` | bool | true | 是否启用攻击和技能的目标一致性 |

### 职业特定配置

通过 `ProfessionOverrides` 可以为特定职业设置不同的行为：

```json
{
  "ProfessionOverrides": {
    "Warrior": {
      "Description": "战士的特殊轨道持续触发",
      "SpecialStartsImmediately": true,
      "PauseSpecialDuringSpawnWait": false
    }
  }
}
```

支持的职业名称：
- `Warrior` - 战士
- `Mage` - 法师
- `Ranger` - 游侠
- `Rogue` - 盗贼

## 使用场景

### 场景 1: 战士持续积累怒气

战士的特殊轨道代表怒气积累机制，即使没有怪物也应该持续触发：

```json
{
  "ProfessionOverrides": {
    "Warrior": {
      "SpecialStartsImmediately": true,
      "PauseSpecialDuringSpawnWait": false
    }
  }
}
```

### 场景 2: 测试旧行为

如果需要回退到旧的立即攻击行为（用于测试或兼容性）：

```json
{
  "AttackStartsImmediately": true,
  "SpecialStartsImmediately": true
}
```

### 场景 3: 禁用目标一致性

如果需要让技能独立选择目标（用于特殊战斗策略）：

```json
{
  "EnableTargetConsistency": false
}
```

## 配置加载顺序

1. 加载 `combat-loop-config.json`（基础配置）
2. 根据环境加载对应的覆盖配置：
   - Development: `combat-loop-config.Development.json`
   - Production: `combat-loop-config.Production.json`
3. 职业特定配置覆盖全局配置

## 修改配置

1. 编辑对应的 JSON 文件
2. 重启应用使配置生效
3. 开发环境支持热重载（如果启用）

## 验证配置

配置会在应用启动时自动验证。如果配置无效，应用将使用默认值并记录警告日志。

## 相关文档

- [战斗循环优化实施方案](../../../docs/战斗循环优化实施方案.md)
- [战斗循环优化总览](../../../docs/战斗循环优化总览.md)
