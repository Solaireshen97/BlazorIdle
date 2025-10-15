# Phase 8 实施进度 - 硬编码常量迁移

**项目**: BlazorIdle  
**阶段**: Phase 8 - 硬编码常量迁移  
**开始日期**: 2025-10-15  
**状态**: 🔄 进行中

---

## 📋 总体进度

| 任务 | 状态 | 进度 | 说明 |
|------|------|------|------|
| 8.1 常量清单与分类 | 🔄 | 50% | 已识别10处硬编码常量 |
| 8.2 配置文件设计 | ⏳ | 0% | 待设计 CombatEngineOptions |
| 8.3 迁移实施 | ⏳ | 0% | 待开始 |
| 8.4 测试验证 | ⏳ | 0% | 待验证 |

**总体进度**: 10%

---

## 🎯 优化目标

根据《服务端代码优化方案.md》Phase 8 目标：
1. 识别所有硬编码常量（战斗引擎、伤害计算、装备系统）
2. 设计统一的配置类结构
3. 迁移硬编码为配置注入
4. 保持向后兼容，提供合理的默认值
5. 更新 appsettings.json

**核心原则**：
- ✅ 零功能改动 - 不修改业务逻辑
- ✅ 维持代码风格 - 保持现有命名规范
- ✅ 向后兼容 - 提供默认值，确保现有行为不变
- ✅ 完善注释 - 配置类添加详细说明

---

## 📊 已识别的硬编码常量

### 战斗引擎常量

| 常量名 | 位置 | 当前值 | 说明 |
|--------|------|--------|------|
| FAR_FUTURE | EnemyAttackEvent.cs:34 | 1e10 | 远未来时间戳 |
| FAR_FUTURE | PlayerDeathEvent.cs:26 | 1e10 | 远未来时间戳（重复） |
| FAR_FUTURE | BattleEngine.cs:359 | 1e10 | 远未来时间戳（重复） |
| FAR_FUTURE | BattleEngine.cs:420 | 1e10 | 远未来时间戳（重复） |
| SKILL_CHECK_INTERVAL | BattleEngine.cs:1049 | 0.5 | 技能检查间隔（秒） |
| BUFF_TICK_INTERVAL | BattleEngine.cs:1061 | 1.0 | Buff刷新间隔（秒） |

**总计**: 6处（4处重复的 FAR_FUTURE）

### 伤害计算常量

| 常量名 | 位置 | 当前值 | 说明 |
|--------|------|--------|------|
| K | DamageCalculator.cs:52 | 50.0 | 伤害减免系数K |
| C | DamageCalculator.cs:55 | 400.0 | 伤害减免常量C |

**总计**: 2处

### 战斗人员常量

| 常量名 | 位置 | 当前值 | 说明 |
|--------|------|--------|------|
| baseAttackDamage | AttackTickEvent.cs:65 | 10 | 基础攻击伤害 |
| defaultAttackerLevel | PlayerCombatant.cs:123 | 50 | 默认攻击者等级 |

**总计**: 2处

### 汇总

- **战斗引擎常量**: 4个独特值（FAR_FUTURE出现4次）
- **伤害计算常量**: 2个
- **战斗人员常量**: 2个
- **总计**: 10处硬编码位置，8个独特常量

---

## 🏗️ 配置类设计

### CombatEngineOptions 结构

```csharp
/// <summary>
/// 战斗引擎配置选项
/// </summary>
public class CombatEngineOptions
{
    /// <summary>
    /// 远未来时间戳，用于标记未激活的事件
    /// </summary>
    /// <remarks>
    /// 用于将事件设置为"不会发生"的状态，例如：
    /// - 死亡角色的下次攻击时间
    /// - 被禁用的技能的下次触发时间
    /// 默认值：1e10（约317年后）
    /// </remarks>
    public double FarFutureTimestamp { get; set; } = 1e10;
    
    /// <summary>
    /// 技能检查间隔（秒）
    /// </summary>
    /// <remarks>
    /// 战斗引擎检查技能是否就绪的时间间隔
    /// 较小的值会更精确但增加计算量
    /// 默认值：0.5秒
    /// </remarks>
    public double SkillCheckIntervalSeconds { get; set; } = 0.5;
    
    /// <summary>
    /// Buff刷新间隔（秒）
    /// </summary>
    /// <remarks>
    /// 持续性Buff（如生命恢复、毒伤害等）的触发间隔
    /// 默认值：1.0秒
    /// </remarks>
    public double BuffTickIntervalSeconds { get; set; } = 1.0;
    
    /// <summary>
    /// 基础攻击伤害
    /// </summary>
    /// <remarks>
    /// 角色在没有装备和属性加成时的基础伤害值
    /// 主要用于测试和最低伤害保底
    /// 默认值：10
    /// </remarks>
    public int BaseAttackDamage { get; set; } = 10;
    
    /// <summary>
    /// 默认攻击者等级
    /// </summary>
    /// <remarks>
    /// 当攻击者等级未设置时使用的默认值
    /// 用于某些特殊战斗场景（如测试、特殊事件）
    /// 默认值：50
    /// </remarks>
    public int DefaultAttackerLevel { get; set; } = 50;
    
    /// <summary>
    /// 伤害减免参数
    /// </summary>
    public DamageReductionOptions DamageReduction { get; set; } = new();
}

/// <summary>
/// 伤害减免计算参数
/// </summary>
/// <remarks>
/// 用于计算防御力对伤害的减免效果
/// 公式：减免率 = Defense / (Defense + K * Level + C)
/// </remarks>
public class DamageReductionOptions
{
    /// <summary>
    /// 伤害减免系数K
    /// </summary>
    /// <remarks>
    /// 影响防御力随等级的缩放
    /// 较大的K值会降低高等级的防御效果
    /// 默认值：50.0
    /// </remarks>
    public double CoefficientK { get; set; } = 50.0;
    
    /// <summary>
    /// 伤害减免常量C
    /// </summary>
    /// <remarks>
    /// 影响低等级和低防御时的基础减免
    /// 较大的C值会降低低防御时的效果
    /// 默认值：400.0
    /// </remarks>
    public double ConstantC { get; set; } = 400.0;
}
```

### appsettings.json 新增配置节

```json
{
  "CombatEngine": {
    "FarFutureTimestamp": 1e10,
    "SkillCheckIntervalSeconds": 0.5,
    "BuffTickIntervalSeconds": 1.0,
    "BaseAttackDamage": 10,
    "DefaultAttackerLevel": 50,
    "DamageReduction": {
      "CoefficientK": 50.0,
      "ConstantC": 400.0
    }
  }
}
```

---

## 📝 实施计划

### 步骤 1: 创建配置类（预计 1 小时）
- [x] 创建 `CombatEngineOptions.cs`
- [x] 创建 `DamageReductionOptions.cs`（包含在同一文件中）
- [x] 添加完整的 XML 文档注释
- [x] 在 Program.cs 中注册配置
- [x] 在 appsettings.json 中添加配置节

### 步骤 2: 修改 DamageCalculator（预计 1 小时）
- [x] 注入 IOptions<CombatEngineOptions>
- [x] 替换硬编码 K 和 C 常量
- [ ] 更新相关测试（稍后验证）

### 步骤 3: 修改战斗引擎（预计 2 小时）
- [ ] 修改 BattleEngine.cs（FAR_FUTURE, SKILL_CHECK_INTERVAL, BUFF_TICK_INTERVAL）
- [ ] 修改 EnemyAttackEvent.cs（FAR_FUTURE）
- [ ] 修改 PlayerDeathEvent.cs（FAR_FUTURE）
- [ ] 注入配置到相关类

### 步骤 4: 修改战斗人员（预计 0.5 小时）
- [ ] 修改 AttackTickEvent.cs（baseAttackDamage）
- [ ] 修改 PlayerCombatant.cs（defaultAttackerLevel）

### 步骤 5: 更新配置文件（预计 0.5 小时）
- [ ] 更新 appsettings.json
- [ ] 更新 appsettings.Development.json（可选）
- [ ] 创建配置说明文档

### 步骤 6: 测试验证（预计 1 小时）
- [ ] 运行所有单元测试
- [ ] 验证默认值行为一致
- [ ] 测试自定义配置
- [ ] 性能基准对比

**预计总工作量**: 6 小时

---

## ✅ 验收标准

### 功能验收
- [ ] 所有硬编码常量已迁移到配置
- [ ] 配置类有完整的XML文档注释
- [ ] 默认配置值与原硬编码值一致
- [ ] 配置可通过 appsettings.json 修改
- [ ] 所有单元测试通过

### 质量验收
- [ ] 构建成功，无新增警告
- [ ] 代码风格保持一致
- [ ] 遵循零功能改动原则
- [ ] 配置验证逻辑完善（可选）

### 文档验收
- [ ] 生成 Phase8 实施总结文档
- [ ] 更新实施进度总览文档
- [ ] 创建配置参数说明文档
- [ ] 代码提交信息清晰

---

## 📈 进度跟踪

### 2025-10-15 上午
- ✅ 创建 Phase8-实施进度.md
- ✅ 识别所有硬编码常量（10处，8个独特值）
- ✅ 设计配置类结构
- ✅ 创建 CombatEngineOptions 和 DamageReductionOptions
- ✅ 在 Program.cs 注册配置
- ✅ 在 appsettings.json 添加 CombatEngine 配置节
- ✅ 重构 DamageCalculator 使用配置（K和C常量）
- ✅ 构建成功，无新增警告

### 2025-10-15 下午
- 🔄 继续实施战斗引擎其他硬编码常量...

---

**当前状态**: 🔄 规划完成，准备实施  
**下一步**: 创建配置类并注册到依赖注入
