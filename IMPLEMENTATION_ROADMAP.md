# 战斗系统拓展实施路线图 (Implementation Roadmap)

## 📅 时间线总览 (Timeline Overview)

```
Month 1          Month 2          Month 3          Month 4          Month 5
│                │                │                │                │
├─ Phase 1 ──────┤                │                │                │
  (Week 1-2)     │                │                │                │
  基础抽象        ├─ Phase 2 ──────┤                │                │
                   (Week 3-4)     │                │                │
                   目标选取        ├─ Phase 3 ──────────────┤        │
                                    (Week 5-7)              │        │
                                    死亡复活                ├─ Phase 4 ──────────┤
                                                              (Week 8-10)         │
                                                              怪物攻击            ├─ Phase 5 ──────────────┤
                                                                                   (Week 11-13)            │
                                                                                   怪物技能                ├─ Phase 6 ─────┤
                                                                                                            (Week 14-15)   │
                                                                                                            强化副本       ├─ Phase 7 ─────┤
                                                                                                                            (Week 16-17)   │
                                                                                                                            RNG一致性      ├─ Phase 8 ───────┤
                                                                                                                                            (Week 18-20)     │
                                                                                                                                            集成优化         │

▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
```

---

## 🏗️ 架构演进 (Architecture Evolution)

### 当前状态 (Current State)
```
┌─────────────────────────────────────────────┐
│           BattleEngine                      │
│  ┌──────────────────────────────────────┐   │
│  │      EventScheduler                  │   │
│  │  - AttackTickEvent                   │   │
│  │  - SpecialPulseEvent                 │   │
│  │  - SkillCastEvent                    │   │
│  └──────────────────────────────────────┘   │
│                                             │
│  ┌──────────────┐      ┌────────────────┐  │
│  │ CharacterStats│      │ EncounterGroup │  │
│  │  (Player)    │ ───► │   (Enemies)    │  │
│  │              │      │                │  │
│  │ - HP: Always │      │ - Multiple     │  │
│  │   100%       │      │   targets      │  │
│  │ - No damage  │      │ - Fixed select │  │
│  └──────────────┘      └────────────────┘  │
└─────────────────────────────────────────────┘

问题 (Issues):
❌ 玩家不受伤害
❌ 怪物无攻击能力
❌ 固定目标选择
❌ 无技能系统
```

### 目标状态 (Target State)
```
┌──────────────────────────────────────────────────────────────┐
│                    BattleEngine                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │           EventScheduler (Enhanced)                  │    │
│  │  - AttackTickEvent                                   │    │
│  │  - SpecialPulseEvent                                 │    │
│  │  - SkillCastEvent                                    │    │
│  │  + PlayerDeathEvent      ◄─ New                      │    │
│  │  + PlayerReviveEvent     ◄─ New                      │    │
│  │  + EnemyAttackEvent      ◄─ New                      │    │
│  │  + EnemySkillCastEvent   ◄─ New                      │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────┐         ┌────────────────────┐      │
│  │  ICombatant        │◄────────┤  TargetSelector    │      │
│  │  (抽象层)           │         │  (加权随机)         │      │
│  └────────┬───────────┘         └────────────────────┘      │
│           │                                                  │
│     ┌─────┴──────┐                                          │
│     │            │                                          │
│ ┌───▼────────┐ ┌─▼────────────┐                            │
│ │PlayerCombat│ │EnemyCombatant│                            │
│ │            │ │              │                            │
│ │+ CurrentHp │ │+ AttackTrack │                            │
│ │+ State     │ │+ Skills[]    │                            │
│ │+ ReviveAt  │ │+ BaseDamage  │                            │
│ └────────────┘ └──────────────┘                            │
└──────────────────────────────────────────────────────────────┘

优势 (Benefits):
✅ 玩家可受伤、死亡、复活
✅ 怪物可攻击玩家
✅ 随机目标选择 + 仇恨系统
✅ 怪物技能系统
✅ 战斗回放一致性
```

---

## 🎯 Phase 依赖关系图 (Phase Dependencies)

```
           Phase 1 (基础抽象)
          ICombatant Interface
                  │
                  │ 依赖 (depends on)
                  ▼
           Phase 2 (目标选取)
          TargetSelector
                  │
          ┌───────┴───────┐
          │               │
          ▼               ▼
   Phase 3 (玩家)    Phase 4 (怪物)
   死亡复活          攻击能力
          │               │
          └───────┬───────┘
                  │
                  ▼
           Phase 5 (怪物技能)
          EnemySkillSystem
                  │
          ┌───────┴───────┐
          │               │
          ▼               ▼
   Phase 6 (副本)    Phase 7 (RNG)
   强化模式          一致性验证
          │               │
          └───────┬───────┘
                  │
                  ▼
           Phase 8 (集成)
          测试 & 优化

关键路径 (Critical Path): 1 → 2 → 3 → 4 → 8
可并行 (Parallel): Phase 6 & 7 可在 Phase 5 后并行
```

---

## 📦 交付物矩阵 (Deliverables Matrix)

| Phase | 核心类 | 事件类 | 测试类 | 文档 |
|-------|--------|--------|--------|------|
| 1 | ICombatant<br>PlayerCombatant<br>EnemyCombatant | - | CombatantTests | 接口设计文档 |
| 2 | TargetSelector | - | TargetSelectorTests | 目标选取算法文档 |
| 3 | PlayerCombatant (扩展) | PlayerDeathEvent<br>PlayerReviveEvent | PlayerDeathReviveTests | 死亡复活流程文档 |
| 4 | EnemyCombatant (扩展) | EnemyAttackEvent | EnemyAttackTests | 怪物攻击设计文档 |
| 5 | EnemySkillDefinition<br>EnemySkillManager | EnemySkillCastEvent | EnemySkillTests | 技能系统配置指南 |
| 6 | DungeonDefinition (扩展) | - | EnhancedDungeonTests | 强化副本说明 |
| 7 | (审计 & 更新) | - | BattleReplayTests | RNG一致性报告 |
| 8 | - | - | IntegrationTests<br>PerformanceTests | 完整系统文档 |

---

## 🔄 数据流图 (Data Flow)

### 玩家受伤流程 (Player Damage Flow)
```
┌─────────────┐
│ Enemy Attack│
│   Event     │
└──────┬──────┘
       │
       ▼
┌─────────────────────┐
│  TargetSelector     │
│  SelectTarget()     │
│  → Player (当前单人) │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────────┐
│ Player.ReceiveDamage()  │
│  CurrentHp -= damage    │
└──────┬──────────────────┘
       │
       ▼
    HP > 0 ?
       │
   ┌───┴───┐
   │ Yes   │ No
   ▼       ▼
 Continue  ┌─────────────────┐
           │ PlayerDeathEvent│
           └────────┬────────┘
                    │
                    ▼
           ┌─────────────────────┐
           │ - Pause Tracks      │
           │ - Cancel Skills     │
           │ - State = Dead      │
           └────────┬────────────┘
                    │
             AutoRevive?
                    │
              ┌─────┴─────┐
              │ Yes       │ No
              ▼           ▼
    ┌──────────────┐  ┌───────────┐
    │Schedule      │  │Battle     │
    │ReviveEvent   │  │Failed     │
    └──────┬───────┘  └───────────┘
           │
           ▼ (10s later)
    ┌──────────────────┐
    │PlayerReviveEvent │
    │ - HP = MaxHp     │
    │ - State = Alive  │
    │ - Resume Tracks  │
    └──────────────────┘
```

### 怪物技能触发流程 (Enemy Skill Trigger Flow)
```
┌─────────────────┐
│ Event Loop Tick │
└────────┬────────┘
         │
         ▼
┌──────────────────────────┐
│ EnemySkillManager        │
│ CheckTriggerConditions() │
└────────┬─────────────────┘
         │
         ▼
    Skill Ready?
    (CD, Condition, Probability)
         │
     ┌───┴───┐
     │ Yes   │ No
     ▼       ▼
┌─────────┐  Continue
│Schedule │
│SkillCast│
└────┬────┘
     │
     ▼
┌──────────────────────┐
│EnemySkillCastEvent   │
│Execute()             │
└────┬─────────────────┘
     │
     ▼
  Effect Type?
     │
┌────┴────────────┐
│                 │
▼                 ▼
Damage         ApplyBuff
│              │
▼              ▼
TargetSelector BuffManager
SelectTarget() ApplyBuff()
DamageCalc()
```

---

## 📈 进度跟踪模板 (Progress Tracking)

### Phase 状态追踪

```
[ ] Phase 1: 基础架构准备 (Week 1-2)
    [ ] P1.1: 定义 ICombatant 接口
    [ ] P1.2: 创建 CombatantState 枚举
    [ ] P1.3: 创建 PlayerCombatant 包装类
    [ ] P1.4: 创建 EnemyCombatant 包装类
    [ ] P1.5: 更新 BattleContext
    [ ] P1.6: 单元测试
    
[x] Phase 2: 目标选取系统 (Week 3-4)
    [x] P2.1: 创建 TargetSelector 类
    [x] P2.2: 在 BattleContext 中添加 TargetSelector
    [x] P2.3: 更新攻击事件
    [x] P2.4: 更新技能释放
    [x] P2.5: 添加 ThreatWeight 配置
    [x] P2.6: 单元测试
    
[x] Phase 3: 玩家死亡与复活 (Week 5-7) ✅ COMPLETED
    [x] P3.1: 扩展 PlayerCombatant
    [x] P3.2: 实现死亡检测
    [x] P3.3: 创建 PlayerDeathEvent
    [x] P3.4: 创建 PlayerReviveEvent
    [x] P3.5: 更新 AttackTickEvent
    [x] P3.6: 更新 SpecialPulseEvent
    [x] P3.7: 更新 BattleEngine 结束条件
    [x] P3.8: 配置复活时长
    [x] P3.9: UI 状态同步
    [x] P3.10: 单元测试
    
[x] Phase 4: 怪物攻击能力 (Week 8-10) ✅ COMPLETED (2025-01)
    [x] P4.1: 扩展 EnemyDefinition (添加 BaseDamage, AttackDamageType, AttackIntervalSeconds)
    [x] P4.2: 初始化怪物攻击轨道 (在 BattleEngine 中创建 EnemyAttackTracks)
    [x] P4.3: 创建 EnemyAttackEvent (实现怪物攻击玩家逻辑)
    [x] P4.4: 怪物无目标时暂停 (玩家死亡暂停，复活恢复)
    [x] P4.5: 伤害平衡调整 (配置所有注册怪物的攻击属性)
    [x] P4.6: 单元测试 (15个测试用例，全部通过)
    
[ ] Phase 5: 怪物技能系统 (Week 11-13)
    [ ] P5.1: 定义 EnemySkillDefinition
    [ ] P5.2: 创建 EnemySkillSlot
    [ ] P5.3: 创建 EnemySkillManager
    [ ] P5.4: 创建 EnemySkillCastEvent
    [ ] P5.5: 集成到 BattleEngine
    [ ] P5.6: 配置示例技能
    [ ] P5.7: 单元测试
    
[ ] Phase 6: 强化型地下城 (Week 14-15)
    [ ] P6.1: 扩展 DungeonDefinition
    [ ] P6.2: 更新 PlayerCombatant
    [ ] P6.3: 更新 PlayerDeathEvent
    [ ] P6.4: 实现副本重置机制
    [ ] P6.5: 强化掉落倍率
    [ ] P6.6: UI 提示
    [ ] P6.7: 单元测试
    
[ ] Phase 7: RNG 一致性 (Week 16-17)
    [ ] P7.1: 审计所有随机事件
    [ ] P7.2: 记录 RNG 范围
    [ ] P7.3: 实现战斗回放工具
    [ ] P7.4: 离线快进验证
    [ ] P7.5: 单元测试
    
[ ] Phase 8: 集成测试与优化 (Week 18-20)
    [ ] P8.1: 端到端测试
    [ ] P8.2: 性能基准测试
    [ ] P8.3: 负载测试
    [ ] P8.4: 文档更新
    [ ] P8.5: 代码审查与重构
    [ ] P8.6: 最终验收测试
```

---

## 🎨 UI 变化预览 (UI Changes Preview)

### 战斗状态显示 (Before → After)

**Before:**
```
┌─────────────────────────────┐
│ 战斗中                       │
├─────────────────────────────┤
│ 玩家 HP: ████████████ 100%  │ ◄─ 始终 100%
│                             │
│ 怪物 1 HP: █████░░░░░ 50%   │
│ 怪物 2 HP: ███░░░░░░░ 30%   │
│                             │
│ 下次攻击: 2.5s              │
└─────────────────────────────┘
```

**After:**
```
┌─────────────────────────────┐
│ 战斗中                       │
├─────────────────────────────┤
│ 玩家 HP: ████░░░░░░ 40%     │ ◄─ 动态血量
│ 状态: 存活                   │ ◄─ 状态显示
│                             │
│ 怪物 1 HP: █████░░░░░ 50%   │
│  → 下次攻击: 1.2s           │ ◄─ 怪物攻击倒计时
│                             │
│ 怪物 2 HP: ███░░░░░░░ 30%   │
│  → 技能: 重击 (3.5s)        │ ◄─ 技能冷却
│                             │
│ 下次攻击: 2.5s              │
└─────────────────────────────┘

┌─────────────────────────────┐
│ 战斗中 - 复活中              │ ◄─ 死亡状态
├─────────────────────────────┤
│ 玩家 HP: ░░░░░░░░░░░ 0%     │
│ 状态: 💀 复活中... 7s       │ ◄─ 复活倒计时
│                             │
│ 怪物 1 HP: █████░░░░░ 50%   │
│  → 等待目标...              │ ◄─ 怪物暂停
│                             │
│ 怪物 2 HP: ███░░░░░░░ 30%   │
│  → 等待目标...              │
└─────────────────────────────┘
```

---

## 🚦 验收标准清单 (Acceptance Criteria Checklist)

### 功能完整性 (Functional Completeness)
- [ ] 玩家可以受到伤害
- [ ] 玩家 HP 降至 0 时进入死亡状态
- [ ] 死亡时攻击进度暂停
- [ ] 10 秒后自动复活
- [ ] 复活后恢复满血并继续战斗
- [ ] 怪物可以攻击玩家
- [ ] 怪物在无存活玩家时暂停攻击
- [ ] 玩家复活后怪物恢复攻击
- [ ] 攻击随机选择目标（多目标情况）
- [ ] 技能随机选择目标
- [ ] 怪物可以释放技能
- [ ] 技能冷却正确工作
- [ ] 技能触发条件正确判断
- [ ] 强化副本可禁用自动复活
- [ ] 禁用复活时死亡触发重置
- [ ] 强化副本掉落倍率生效

### 技术质量 (Technical Quality)
- [ ] 单元测试覆盖率 > 85%
- [ ] 所有测试通过
- [ ] 性能下降 < 5%
- [ ] 相同 seed 战斗结果一致
- [ ] 离线快进与在线战斗结果匹配
- [ ] RNG Index 正确记录
- [ ] 代码符合项目规范
- [ ] 文档完整且准确

### 兼容性 (Compatibility)
- [ ] 现有战斗不受影响
- [ ] 向后兼容性测试通过
- [ ] API 变更已文档化
- [ ] 配置迁移脚本（如需要）

---

## 📞 联系与反馈 (Contact & Feedback)

- **项目负责人**: [待定]
- **技术审查**: [待定]
- **问题跟踪**: GitHub Issues
- **设计讨论**: [待定]

---

**文档版本**: 1.0  
**创建日期**: 2025-01  
**状态**: 📝 等待审核和启动

**下一步行动**: 
1. 团队审核设计方案
2. 确认资源分配
3. 设置开发环境
4. 启动 Phase 1 开发
