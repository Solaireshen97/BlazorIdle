# 数据库读取优化 - Phase 1 实施完成文档

**项目**: BlazorIdle 数据库读取优化  
**阶段**: Phase 1 - 缓存层基础设施  
**版本**: 1.1  
**完成日期**: 2025-10-19  
**状态**: ✅ 已完成并验证

---

## 📋 目录

1. [实施概述](#实施概述)
2. [核心功能](#核心功能)
3. [配置说明](#配置说明)
4. [API 端点](#api-端点)
5. [测试结果](#测试结果)
6. [使用指南](#使用指南)
7. [监控和维护](#监控和维护)
8. [故障排查](#故障排查)

---

## 实施概述

### 实施目标

Phase 1 的目标是建立数据库读取缓存的基础设施，为后续的 Repository 迁移（Phase 2）做好准备。

### 已完成功能

✅ **MemoryStateManager<T> 增强**
- 缓存优先读取（TryGetAsync）
- 批量预加载（PreloadBatch, PreloadFromDatabaseAsync）
- 缓存统计（GetCacheStatistics, GetCacheHitRate）
- TTL 过期清理（ClearExpired）
- 缓存失效（InvalidateCache）

✅ **CacheCoordinator 后台服务**
- 启动时预加载静态配置数据
- 定期清理过期缓存
- 定期输出缓存统计信息

✅ **配置系统**
- 完全配置化（无硬编码）
- 实体特定缓存策略
- 全局缓存设置
- 配置验证

✅ **监控端点**
- `/api/database/cache-stats` - 缓存统计
- `/api/database/health` - 健康检查
- `/api/database/metrics` - 性能指标
- `/api/database/status` - 详细状态

✅ **测试覆盖**
- 26个单元测试，100%通过
- 缓存功能测试
- 配置验证测试
- 集成测试

---

## 核心功能

### 1. 缓存读取策略

#### 工作流程

```
客户端请求
    ↓
Repository.GetAsync()
    ↓
MemoryStateManager.TryGetAsync()
    ↓
检查内存缓存
    ├─ 命中 → 更新访问时间 → 返回数据 ✅
    └─ 未命中 → 查询数据库 → 加载到内存 → 返回数据
```

#### 代码示例

```csharp
// 使用 TryGetAsync 进行缓存优先读取
var entity = await _memoryManager.TryGetAsync(
    id,
    async (entityId, ct) => {
        // 数据库加载器（仅在缓存未命中时调用）
        return await _db.Set<T>().FindAsync(new object[] { entityId }, ct);
    },
    cancellationToken
);
```

### 2. 缓存策略类型

#### Permanent（永久缓存）

**适用场景**: 静态配置数据
- 装备定义（GearDefinition）
- 词缀定义（Affix）
- 装备套装（GearSet）

**特点**:
- 启动时预加载
- 不过期（除非手动刷新）
- 命中率预期：95-100%

#### Temporary（临时缓存）

**适用场景**: 用户数据
- 角色（Character）
- 装备实例（GearInstance）
- 活动计划（ActivityPlan）
- 战斗快照（RunningBattleSnapshot）

**特点**:
- 按需加载
- 带 TTL 过期时间
- LRU 清理策略
- 命中率预期：70-90%

#### None（无缓存）

**适用场景**: 不适合缓存的数据
- 实时性要求极高的数据
- 更新频率极高的数据

### 3. 预加载机制

#### 启动时预加载

**时机**: 应用启动时

**流程**:
```
应用启动
    ↓
CacheCoordinator.StartAsync()
    ↓
检查 EnableReadCaching
    ↓
遍历 EntityStrategies
    ↓
筛选 PreloadOnStartup = true
    ↓
分批加载（PreloadBatchSize）
    ↓
记录统计信息
```

**日志输出**:
```
=== 缓存协调器启动 CacheCoordinator Starting ===
✓ GearDefinition 预加载完成: 500 条记录，耗时 125ms
✓ Affix 预加载完成: 1000 条记录，耗时 89ms
✓ GearSet 预加载完成: 50 条记录，耗时 12ms
=== 缓存预加载完成 Cache Preloading Completed ===
```

### 4. 缓存清理

#### TTL 过期清理

**触发条件**:
- 定期清理（每 N 分钟）
- 仅清理 Temporary 策略的缓存

**清理逻辑**:
```csharp
var cutoff = DateTime.UtcNow.AddSeconds(-ttlSeconds);
// 查找过期实体（lastAccessTime < cutoff）
// 排除 Dirty 实体（还未保存）
// 从缓存中移除
```

#### LRU 清理

**触发条件**:
- 缓存数量超过 MaxCachedCount

**清理逻辑**:
```csharp
// 按 lastAccessTime 排序
// 移除最久未访问的实体
// 保留 Dirty 实体（还未保存）
// 清理到 90% 阈值
```

---

## 配置说明

### 配置文件位置

- **开发环境**: `appsettings.Development.json`
- **生产环境**: `appsettings.Production.json`
- **基础配置**: `appsettings.json`

### 配置结构

```json
{
  "CacheConfiguration": {
    "EntityStrategies": {
      "<实体类型名称>": {
        "Strategy": "Permanent|Temporary|None",
        "PreloadOnStartup": true|false,
        "PreloadBatchSize": 100-10000,
        "MaxCachedCount": 100-1000000,
        "TtlSeconds": 60-86400
      }
    },
    "GlobalSettings": {
      "EnableReadCaching": true|false,
      "CleanupIntervalMinutes": 1-60,
      "TrackCacheHitRate": true|false,
      "HitRateLogIntervalMinutes": 1-60
    }
  }
}
```

### 推荐配置

#### 静态配置数据（Permanent）

```json
{
  "GearDefinition": {
    "Strategy": "Permanent",
    "PreloadOnStartup": true,
    "PreloadBatchSize": 500,
    "MaxCachedCount": 10000
  },
  "Affix": {
    "Strategy": "Permanent",
    "PreloadOnStartup": true,
    "PreloadBatchSize": 1000,
    "MaxCachedCount": 10000
  },
  "GearSet": {
    "Strategy": "Permanent",
    "PreloadOnStartup": true,
    "PreloadBatchSize": 100,
    "MaxCachedCount": 1000
  }
}
```

#### 用户数据（Temporary）

```json
{
  "Character": {
    "Strategy": "Temporary",
    "TtlSeconds": 3600,           // 1 小时
    "MaxCachedCount": 10000,
    "PreloadOnStartup": false
  },
  "GearInstance": {
    "Strategy": "Temporary",
    "TtlSeconds": 1800,           // 30 分钟
    "MaxCachedCount": 50000,
    "PreloadOnStartup": false
  },
  "ActivityPlan": {
    "Strategy": "Temporary",
    "TtlSeconds": 600,            // 10 分钟
    "MaxCachedCount": 20000,
    "PreloadOnStartup": false
  },
  "RunningBattleSnapshot": {
    "Strategy": "Temporary",
    "TtlSeconds": 300,            // 5 分钟
    "MaxCachedCount": 5000,
    "PreloadOnStartup": false
  }
}
```

### 配置调优建议

#### 内存优先场景

```json
{
  "GlobalSettings": {
    "CleanupIntervalMinutes": 10,    // 减少清理频率
    "TrackCacheHitRate": true
  },
  "Character": {
    "TtlSeconds": 7200,              // 增加 TTL
    "MaxCachedCount": 50000          // 增加容量
  }
}
```

#### 内存受限场景

```json
{
  "GlobalSettings": {
    "CleanupIntervalMinutes": 2,     // 增加清理频率
    "TrackCacheHitRate": false       // 节省开销
  },
  "Character": {
    "TtlSeconds": 1800,              // 减少 TTL
    "MaxCachedCount": 5000           // 减少容量
  }
}
```

---

## API 端点

### 1. 缓存统计端点

**端点**: `GET /api/database/cache-stats`

**功能**: 获取实时缓存统计信息

**响应示例**:
```json
{
  "timestamp": "2025-10-19T03:33:56.063Z",
  "cacheEnabled": true,
  "entityMetrics": {
    "Character": {
      "cachedCount": 150,
      "dirtyCount": 5,
      "cacheHits": 1250,
      "cacheMisses": 50,
      "hitRate": 96.15
    }
  },
  "overallStatistics": {
    "totalHits": 2810,
    "totalMisses": 240,
    "totalRequests": 3050,
    "overallHitRate": 92.13
  }
}
```

**使用示例**:
```bash
# 基本查询
curl http://localhost:5000/api/database/cache-stats

# 使用 jq 格式化输出
curl http://localhost:5000/api/database/cache-stats | jq .

# 提取命中率
curl http://localhost:5000/api/database/cache-stats | jq '.overallStatistics.overallHitRate'
```

### 2. 健康检查端点

**端点**: `GET /api/database/health`

**功能**: 获取整体健康状态

**响应示例**:
```json
{
  "status": "healthy",
  "timestamp": "2025-10-19T03:33:56.063Z",
  "memoryBuffering": {
    "enabled": true,
    "characters": {
      "cached": 150,
      "dirty": 5
    }
  }
}
```

### 3. 详细状态端点

**端点**: `GET /api/database/status`

**功能**: 获取详细系统状态

### 4. 性能指标端点

**端点**: `GET /api/database/metrics?minutes=10`

**功能**: 获取性能指标摘要

---

## 测试结果

### 测试统计

```
Total tests: 26
     Passed: 26 (100%)
     Failed: 0
    Skipped: 0
 Total time: 4.21 seconds
```

### 测试分类

#### 缓存功能测试（5个）

- ✅ ClearExpired_ShouldRemoveOldEntries_BasedOnTTL
- ✅ GetAll_ShouldReturnAllCachedEntities
- ✅ InvalidateCache_ShouldRemoveSpecificEntity
- ✅ PreloadBatch_ShouldAddEntitiesWithoutDirtyFlag
- ✅ InvalidateCache_ShouldNotRemoveDirtyEntity

#### 内存管理测试（21个）

- ✅ GetAsync 相关测试（缓存命中/未命中）
- ✅ Add/Update/Remove 操作测试
- ✅ Dirty 跟踪测试
- ✅ LRU 清理测试
- ✅ 快照隔离测试

### 运行测试

```bash
# 运行所有数据库优化测试
dotnet test --filter "FullyQualifiedName~DatabaseOptimization"

# 运行特定测试文件
dotnet test --filter "FullyQualifiedName~CacheEnhancementTests"

# 运行单个测试
dotnet test --filter "FullyQualifiedName~ClearExpired_ShouldRemoveOldEntries"
```

---

## 使用指南

### 在 Repository 中使用缓存

#### 基础用法

```csharp
public class CharacterRepository
{
    private readonly IMemoryStateManager<Character> _memoryManager;
    private readonly GameDbContext _db;
    
    public async Task<Character?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // 使用缓存优先读取
        return await _memoryManager.TryGetAsync(
            id,
            async (entityId, cancellationToken) => 
                await _db.Characters.FindAsync(new object[] { entityId }, cancellationToken),
            ct
        );
    }
}
```

#### 带关联查询

```csharp
public async Task<Character?> GetWithEquipmentAsync(Guid id, CancellationToken ct)
{
    return await _memoryManager.TryGetAsync(
        id,
        async (entityId, cancellationToken) => 
            await _db.Characters
                .Include(c => c.Equipment)
                .Include(c => c.ActiveSkills)
                .FirstOrDefaultAsync(c => c.Id == entityId, cancellationToken),
        ct
    );
}
```

### 批量预加载

```csharp
public async Task PreloadAllCharactersAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    
    // 从数据库批量预加载
    await _characterManager.PreloadFromDatabaseAsync(db, batchSize: 1000, ct);
    
    // 获取统计信息
    var stats = _characterManager.GetCacheStatistics();
    _logger.LogInformation(
        "预加载完成: {Count} 个角色",
        stats.CachedCount
    );
}
```

### 手动缓存失效

```csharp
public void InvalidateCharacterCache(Guid characterId)
{
    _characterManager.InvalidateCache(characterId);
}

public void RefreshCache()
{
    // 清理所有过期缓存
    var removed = _characterManager.ClearExpired(ttlSeconds: 3600);
    _logger.LogInformation("清理了 {Count} 个过期缓存", removed);
}
```

---

## 监控和维护

### 监控指标

#### 关键指标

| 指标 | 目标 | 监控方法 |
|------|------|---------|
| 缓存命中率 | ≥80% | `/api/database/cache-stats` |
| 内存使用 | <500MB | `/api/database/status` |
| Dirty 占比 | <50% | `/api/database/memory-state` |
| 响应时间 P95 | <100ms | 应用性能监控 |

#### 监控脚本示例

```bash
#!/bin/bash
# 缓存监控脚本

# 获取缓存命中率
HIT_RATE=$(curl -s http://localhost:5000/api/database/cache-stats | \
    jq '.overallStatistics.overallHitRate')

echo "当前缓存命中率: ${HIT_RATE}%"

# 如果命中率低于阈值，发送告警
if (( $(echo "$HIT_RATE < 70" | bc -l) )); then
    echo "警告：缓存命中率过低！"
    # 发送告警...
fi
```

### 日志监控

#### 关键日志模式

**启动日志**:
```
=== 缓存协调器启动 CacheCoordinator Starting ===
✓ GearDefinition 预加载完成: 500 条记录，耗时 125ms
=== 缓存预加载完成 Cache Preloading Completed ===
```

**清理日志**:
```
过期缓存清理完成: 总计移除 15 个实体
清理 Character 过期缓存: 10 个实体
```

**统计日志**（每 10 分钟）:
```
=== 缓存统计报告 Cache Statistics Report ===
Character: 缓存 150 个, Dirty 5 个, 命中 1250 次, 未命中 50 次, 命中率 96.15%
==========================================
```

### 性能调优

#### 调优参数

1. **TTL 调整**
   - 命中率低 → 增加 TTL
   - 内存压力大 → 减少 TTL

2. **清理频率**
   - 内存使用高 → 增加清理频率
   - CPU 使用高 → 减少清理频率

3. **缓存容量**
   - 命中率低且内存充足 → 增加 MaxCachedCount
   - 内存不足 → 减少 MaxCachedCount

#### 调优示例

**场景 1：命中率低（< 70%）**
```json
{
  "Character": {
    "TtlSeconds": 7200,        // 从 3600 增加到 7200
    "MaxCachedCount": 20000    // 从 10000 增加到 20000
  }
}
```

**场景 2：内存使用高（> 80%）**
```json
{
  "GlobalSettings": {
    "CleanupIntervalMinutes": 2  // 从 5 减少到 2
  },
  "Character": {
    "TtlSeconds": 1800,          // 从 3600 减少到 1800
    "MaxCachedCount": 5000       // 从 10000 减少到 5000
  }
}
```

---

## 故障排查

### 常见问题

#### 1. 缓存命中率低

**症状**: 命中率 < 70%

**可能原因**:
- TTL 过短
- 缓存容量不足
- 访问模式不适合缓存

**解决方案**:
1. 查看日志，确认清理频率
2. 增加 TTL 时间
3. 增加 MaxCachedCount
4. 检查访问模式

#### 2. 内存使用高

**症状**: 内存使用 > 80%

**可能原因**:
- MaxCachedCount 设置过大
- TTL 过长
- 清理不及时

**解决方案**:
1. 减少 MaxCachedCount
2. 减少 TTL
3. 增加清理频率
4. 检查是否有内存泄漏

#### 3. 缓存预加载失败

**症状**: 启动时日志显示预加载失败

**可能原因**:
- 数据库连接问题
- 实体未实现 IEntity 接口
- 配置错误

**解决方案**:
1. 检查数据库连接
2. 确认实体实现 IEntity
3. 验证配置正确性
4. 查看详细错误日志

#### 4. Dirty 实体占比高

**症状**: Dirty 占比 > 50%

**可能原因**:
- 更新频率过高
- 保存间隔过长
- 写入性能问题

**解决方案**:
1. 检查 PersistenceCoordinator 配置
2. 调整保存间隔
3. 手动触发保存

### 诊断命令

```bash
# 1. 检查缓存状态
curl http://localhost:5000/api/database/cache-stats | jq

# 2. 检查健康状态
curl http://localhost:5000/api/database/health | jq

# 3. 检查详细状态
curl http://localhost:5000/api/database/status | jq

# 4. 触发立即保存
curl -X POST http://localhost:5000/api/database/trigger-save

# 5. 检查日志
tail -f logs/app.log | grep -i "cache\|memory"
```

---

## 附录

### A. 相关文档

1. **设计文档**:
   - 数据库读取优化方案-完整分析.md
   - 数据库读取优化实施方案-上篇.md

2. **完成报告**:
   - 数据库读取优化-Phase1完成总结.md
   - 数据库读取优化-Phase1改进完成报告.md

3. **项目总览**:
   - 数据库读取优化-项目总览.md

### B. 代码位置

```
BlazorIdle.Server/
├── Infrastructure/DatabaseOptimization/
│   ├── MemoryStateManager.cs           # 内存状态管理器
│   ├── CacheCoordinator.cs             # 缓存协调器
│   ├── DatabaseMetricsCollector.cs     # 指标收集器
│   └── Abstractions/
│       └── IMemoryStateManager.cs      # 接口定义
├── Config/DatabaseOptimization/
│   ├── CacheConfiguration.cs           # 缓存配置
│   ├── EntityCacheStrategy.cs          # 实体策略
│   ├── GlobalCacheSettings.cs          # 全局设置
│   └── CacheStrategyType.cs            # 策略类型
└── Api/
    └── DatabaseHealthController.cs     # 监控端点

tests/BlazorIdle.Tests/DatabaseOptimization/
├── MemoryStateManagerTests.cs          # 内存管理器测试
├── CacheEnhancementTests.cs            # 缓存增强测试
└── PersistenceIntegrationTests.cs      # 集成测试
```

### C. 性能基准

**预期性能改善**:
- 数据库读取减少：85-90%
- API 响应时间改善：30-50%
- 系统吞吐量提升：100%+

**内存使用**:
- 静态数据：~50MB
- 用户数据：~150MB
- 总计：<200MB

---

**文档版本**: 1.1  
**最后更新**: 2025-10-19  
**维护者**: 开发团队  
**下次审查**: Phase 2 完成后
