using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Resources;

/// <summary>
/// 资源桶集合的简单聚合容器：
/// - 按字符串 Id 存多个 <see cref="ResourceBucket"/>
/// - 提供按需创建 (Ensure) / 获取 (Get / TryGet) / 快照 (Snapshot)
/// 设计目标：在战斗上下文中集中管理同一角色的各种资源（能量、怒气、连击点等）。
/// </summary>
public class ResourceSet
{
    /// <summary>
    /// 内部存放桶的字典。
    /// Key: ResourceBucket.Id
    /// Value: ResourceBucket 实例
    /// 说明：未做线程安全封装——假定战斗循环在单线程（或外层聚合根串行调度）中运行。
    /// </summary>
    private readonly Dictionary<string, ResourceBucket> _buckets = new();

    /// <summary>
    /// 只读视图（仍然指向原对象；调用者能修改 Bucket 的状态，但不能增删集合）。
    /// </summary>
    public IReadOnlyDictionary<string, ResourceBucket> Buckets => _buckets;

    /// <summary>
    /// 确保指定 Id 的资源桶存在；不存在则创建并返回，新建时可指定初始参数。
    /// 若已存在，会直接返回旧实例，忽略本次传入的 max / initial / policy 等参数。
    /// （这一点需要调用方知悉：同一个 Id 第二次传入不同的参数不会生效，也不会校验冲突）
    /// </summary>
    /// <param name="id">资源 Id（字典键）</param>
    /// <param name="max">最大值</param>
    /// <param name="initial">初始值（只在新建时使用）</param>
    /// <param name="policy">溢出策略</param>
    /// <param name="convertUnit">Convert 策略单位</param>
    /// <param name="conversionTag">转换标签</param>
    /// <returns>已有或新建的资源桶实例</returns>
    public ResourceBucket Ensure(
        string id,
        int max,
        int initial = 0,
        OverflowPolicy policy = OverflowPolicy.Clamp,
        int convertUnit = 0,
        string? conversionTag = null)
    {
        if (_buckets.TryGetValue(id, out var existing))
            return existing; // 已存在：不修改任何属性

        var bucket = new ResourceBucket(id, max, initial, policy, convertUnit, conversionTag);
        _buckets[id] = bucket;
        return bucket;
    }

    /// <summary>
    /// 直接按 Id 取资源桶；若不存在会抛出 KeyNotFoundException。
    /// 在逻辑保证前置 Ensure 后使用，或用于发现配置缺失的“硬失败”。
    /// </summary>
    public ResourceBucket Get(string id) => _buckets[id];

    /// <summary>
    /// 安全尝试获取；不存在返回 false。
    /// </summary>
    public bool TryGet(string id, out ResourceBucket bucket) => _buckets.TryGetValue(id, out bucket);

    /// <summary>
    /// 生成一个当前状态的轻量快照（只复制数值，不复制引用）。
    /// 返回的枚举元素为 (id, current, max) 元组。
    /// 注意：
    /// - 惰性执行（延迟枚举）：如果外部在迭代时修改了桶内容，看到的是修改后的值。
    ///   如果需要“瞬时冻结”，可在调用处立即 .ToList()。
    /// </summary>
    public IEnumerable<(string id, int current, int max)> Snapshot() =>
        _buckets.Values.Select(b => (b.Id, b.Current, b.Max));
}