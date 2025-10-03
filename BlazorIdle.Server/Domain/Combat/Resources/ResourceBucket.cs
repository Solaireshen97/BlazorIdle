using System;

namespace BlazorIdle.Server.Domain.Combat.Resources;

/// <summary>
/// 通用“资源桶”
/// 用于表示一种可增减、具有上限，并且在溢出时有策略（截断 / 转化 / 忽略）的数值资源。
/// 典型用途：能量、怒气、连击点、特殊点数等。
/// </summary>
public class ResourceBucket
{
    /// <summary>资源标识（便于在集合或日志中区分）</summary>
    public string Id { get; }

    /// <summary>当前值（始终保持 0..Max）</summary>
    public int Current { get; private set; }

    /// <summary>最大上限（构造后暂不可变；若未来需要动态调整可添加方法）</summary>
    public int Max { get; private set; }

    /// <summary>溢出策略：Clamp / Convert / Ignore</summary>
    public OverflowPolicy OverflowPolicy { get; }

    /// <summary>
    /// Convert 模式下：溢出值达到多少触发一次“转换”。
    /// 例如溢出 50 点资源，ConvertUnit=10 → 产生 5 次转换。
    /// </summary>
    public int ConvertUnit { get; }

    /// <summary>
    /// 转换时打到外部统计（例如 <see cref="SegmentCollector"/>）的标签；
    /// 为 null 表示即便是 Convert 策略也不输出统计（仍然只做截断）。
    /// </summary>
    public string? ConversionTag { get; }

    /// <param name="id">资源唯一标识</param>
    /// <param name="max">最大值（必须 &gt; 0）</param>
    /// <param name="initial">初始值（会被 Clamp 到 0..max）</param>
    /// <param name="overflowPolicy">溢出策略</param>
    /// <param name="convertUnit">Convert 策略下的单位（&lt;=0 或 tag 为空时不会产生转换）</param>
    /// <param name="conversionTag">转换统计标签</param>
    public ResourceBucket(
        string id,
        int max,
        int initial = 0,
        OverflowPolicy overflowPolicy = OverflowPolicy.Clamp,
        int convertUnit = 0,
        string? conversionTag = null)
    {
        if (max <= 0) throw new ArgumentException("max must > 0");
        Id = id;
        Max = max;
        Current = Math.Clamp(initial, 0, max);
        OverflowPolicy = overflowPolicy;
        ConvertUnit = convertUnit;
        ConversionTag = conversionTag;
    }

    /// <summary>
    /// 增减资源。
    /// 正数：增加，可能触发溢出逻辑；负数：消耗（底线 0）；0：无操作。
    /// 返回结果包含“实际变动量（AppliedDelta）”与“转换次数（ConversionCount）”。
    /// 注意：当 amount 为负时，ConversionCount 始终为 0。
    /// </summary>
    /// <param name="amount">增量（可为负）</param>
    /// <returns>资源实际变化与转换次数</returns>
    public ResourceChangeResult Add(int amount)
    {
        if (amount == 0)
            return new ResourceChangeResult(0, 0);

        // 负数：直接扣减，最低不超过 0。这里不走溢出逻辑也不统计转换。
        if (amount < 0)
        {
            var before = Current;
            Current = Math.Max(0, Current + amount);
            return new ResourceChangeResult(Current - before, 0); // AppliedDelta 为负或 0
        }

        // 正数：尝试增加
        int beforeAdd = Current;
        Current += amount;

        int conversions = 0;

        // 只在超过上限时进入溢出策略分支
        if (Current > Max)
        {
            int overflow = Current - Max;
            switch (OverflowPolicy)
            {
                case OverflowPolicy.Clamp:
                    // 简单截断：丢弃全部溢出
                    Current = Max;
                    break;

                case OverflowPolicy.Convert:
                    // Convert：将溢出部分按单位产生“转换次数”，当前资源仍然保持在 Max
                    if (ConvertUnit > 0 && ConversionTag != null)
                    {
                        conversions = overflow / ConvertUnit; // 只按完整单位计算次数
                        Current = Max; // 溢出本身不保留
                    }
                    else
                    {
                        // 未配置有效 ConvertUnit 或 Tag，退化为 Clamp
                        Current = Max;
                    }
                    break;

                case OverflowPolicy.Ignore:
                    // 当前实现与 Clamp 相同：溢出被忽略并截断。
                    // 如果希望“Ignore”表示“直接允许超过 Max”，需要调整这里的逻辑。
                    Current = Max;
                    break;
            }
        }

        // 实际应用的增量（不含被截断 / 转换掉的那部分）
        int deltaApplied = Current - beforeAdd;
        return new ResourceChangeResult(deltaApplied, conversions);
    }

    /// <summary>
    /// Add 操作的反馈：
    /// AppliedDelta: 实际写入 Current 的变化量；
    /// ConversionCount: 溢出被转换的次数（仅 Convert 策略且条件满足）。
    /// </summary>
    public record ResourceChangeResult(int AppliedDelta, int ConversionCount);
}