# 战斗日志配置文件说明

## 概述

`battle-log-config.json` 用于配置前端战斗日志的显示和行为。

## 配置项说明

### battleLog

主配置对象，包含所有战斗日志相关设置。

#### enabled
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 是否启用战斗日志面板

#### maxMessages
- **类型**: `integer`
- **默认值**: `50`
- **范围**: 10-500
- **说明**: 内存中保存的最大消息数量，超过此数量时自动删除最早的消息

#### displayLatestCount
- **类型**: `integer`
- **默认值**: `20`
- **范围**: 5-100
- **说明**: 在界面上显示的最近消息数量

#### autoScroll
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 是否自动滚动到最新消息

#### showTimestamps
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 是否在消息前显示时间戳

#### timestampFormat
- **类型**: `string`
- **默认值**: `"HH:mm:ss"`
- **说明**: 时间戳格式，使用C# DateTime格式字符串
- **示例**: 
  - `"HH:mm:ss"` - 14:30:25
  - `"HH:mm:ss.fff"` - 14:30:25.123
  - `"yyyy-MM-dd HH:mm:ss"` - 2025-10-14 14:30:25

#### animateNewMessages
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 新消息出现时是否显示淡入动画

### eventTypes

控制不同类型事件的显示。

#### enableAttackStarted
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 显示"角色开始攻击XX"消息

#### enableDamageApplied
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 显示"角色对XX造成XX伤害"消息

#### enableDamageReceived
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 显示"角色受到XX伤害"消息

#### enableEnemyAttackStarted
- **类型**: `boolean`
- **默认值**: `true`
- **说明**: 显示"敌人开始攻击角色"消息

### ui

UI样式自定义设置。

#### panelHeight
- **类型**: `string`
- **默认值**: `"300px"`
- **说明**: 战斗日志面板的高度（CSS height值）
- **示例**: `"300px"`, `"20vh"`, `"auto"`

#### fontSize
- **类型**: `string`
- **默认值**: `"14px"`
- **说明**: 日志文字大小（CSS font-size值）
- **示例**: `"12px"`, `"14px"`, `"1rem"`

#### entryPadding
- **类型**: `string`
- **默认值**: `"4px 8px"`
- **说明**: 每条日志的内边距（CSS padding值）

#### backgroundColor
- **类型**: `string`
- **默认值**: `"#1a1a1a"`
- **说明**: 面板背景颜色（CSS颜色值）

#### textColor
- **类型**: `string`
- **默认值**: `"#e0e0e0"`
- **说明**: 默认文字颜色（CSS颜色值）

#### timestampColor
- **类型**: `string`
- **默认值**: `"#888888"`
- **说明**: 时间戳颜色（CSS颜色值）

### colors

不同事件类型的颜色主题。

#### attackStarted
- **类型**: `string`
- **默认值**: `"#4a9eff"` (蓝色)
- **说明**: 攻击开始事件的颜色

#### damageDealt
- **类型**: `string`
- **默认值**: `"#ff6b6b"` (红色)
- **说明**: 造成伤害事件的颜色

#### damageReceived
- **类型**: `string`
- **默认值**: `"#ffa94d"` (橙色)
- **说明**: 受到伤害事件的颜色

#### criticalHit
- **类型**: `string`
- **默认值**: `"#ffdd57"` (黄色)
- **说明**: 暴击伤害的颜色

#### enemyAttack
- **类型**: `string`
- **默认值**: `"#ff4757"` (深红色)
- **说明**: 敌人攻击事件的颜色

## 配置示例

### 简洁模式
仅显示伤害事件，不显示攻击开始事件：

```json
{
  "battleLog": {
    "enabled": true,
    "maxMessages": 30,
    "displayLatestCount": 15,
    "eventTypes": {
      "enableAttackStarted": false,
      "enableDamageApplied": true,
      "enableDamageReceived": true,
      "enableEnemyAttackStarted": false
    }
  }
}
```

### 大屏幕模式
增加显示区域和字体大小：

```json
{
  "battleLog": {
    "enabled": true,
    "ui": {
      "panelHeight": "500px",
      "fontSize": "16px"
    }
  }
}
```

### 高性能模式
减少保存的消息数量，优化性能：

```json
{
  "battleLog": {
    "enabled": true,
    "maxMessages": 20,
    "displayLatestCount": 10,
    "animateNewMessages": false
  }
}
```

### 详细日志模式
显示所有事件和毫秒级时间戳：

```json
{
  "battleLog": {
    "enabled": true,
    "maxMessages": 100,
    "displayLatestCount": 50,
    "timestampFormat": "HH:mm:ss.fff",
    "eventTypes": {
      "enableAttackStarted": true,
      "enableDamageApplied": true,
      "enableDamageReceived": true,
      "enableEnemyAttackStarted": true
    }
  }
}
```

## 注意事项

1. **性能考虑**: `maxMessages` 和 `displayLatestCount` 不宜设置过大，否则可能影响浏览器性能
2. **配置验证**: 配置文件带有JSON Schema，在支持的编辑器中会有自动提示和验证
3. **热重载**: 修改配置后需要刷新页面才能生效
4. **后端配置**: 消息内容模板在服务端配置文件中管理（`appsettings.json`的`BattleMessages`节）

## 相关文档

- [战斗消息系统使用指南](../../../docs/战斗消息系统使用指南.md)
- [战斗消息系统实现总结](../../../战斗消息系统实现总结.md)
