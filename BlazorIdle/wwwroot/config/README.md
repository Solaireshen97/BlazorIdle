# 前端配置文件说明

## progress-bar-config.json

前端进度条和轮询优化的配置文件。

### ProgressBar（进度条配置）

- `EnableLoopingProgress`: 启用进度条循环滚动（达到100%后基于interval继续循环）
- `AnimationIntervalMs`: 动画刷新间隔（毫秒）
- `MinIntervalForLooping`: 循环进度的最小有效间隔（秒）
- `MaxIntervalForLooping`: 循环进度的最大有效间隔（秒）

### JITPolling（即时轮询配置）

- `EnableJITPolling`: 启用JIT即时轮询机制
- `TriggerWindowMs`: 触发点前的时间窗口（毫秒），在此时间内触发JIT轮询
- `MinPredictionTimeMs`: 最小预测时间（毫秒）
- `MaxJITAttemptsPerCycle`: 每个攻击周期最多尝试JIT轮询次数
- `AdaptivePollingEnabled`: 启用自适应轮询（根据战斗状态动态调整轮询频率）
- `MinPollingIntervalMs`: 最小轮询间隔
- `MaxPollingIntervalMs`: 最大轮询间隔
- `HealthCriticalThreshold`: 血量危急阈值（百分比）
- `HealthLowThreshold`: 血量偏低阈值（百分比）
- `CriticalHealthPollingMs`: 血量危急时的轮询间隔
- `LowHealthPollingMs`: 血量偏低时的轮询间隔
- `NormalPollingMs`: 正常状态下的轮询间隔

### HPAnimation（血量动画配置）

- `TransitionDurationMs`: 默认过渡动画时长（毫秒）
- `TransitionTimingFunction`: CSS过渡函数（linear、ease、ease-in-out等）
- `EnableSmoothTransition`: 启用平滑过渡效果
- `PlayerHPTransitionMs`: 玩家血量条过渡时长
- `EnemyHPTransitionMs`: 敌人血量条过渡时长

### Debug（调试配置）

- `LogProgressCalculations`: 记录进度计算详情
- `LogJITPollingEvents`: 记录JIT轮询触发事件
- `ShowProgressDebugInfo`: 在UI中显示调试信息

## 配置最佳实践

1. **生产环境**建议关闭所有Debug选项以提高性能
2. **开发环境**可以开启Debug选项方便排查问题
3. 根据服务器性能和网络延迟调整轮询间隔
4. HP动画时长建议保持在100-200ms之间，过长会显得迟钝，过短会显得突兀
