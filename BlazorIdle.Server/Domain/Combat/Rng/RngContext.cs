using System;

namespace BlazorIdle.Server.Domain.Combat.Rng;

/// <summary>
/// 可重放 RNG（SplitMix64）
/// - 线程不安全，按 Battle 实例单独使用
/// - 记录 Index（调用次数）方便保存 seed 消耗区间
/// - 提供 NextUInt64/NextDouble/NextInt/NextBool
/// - 提供 Split(salt) 生成子流（例如技能/掉落使用独立子流）
/// </summary>
public sealed class RngContext
{
    private ulong _state;
    public long Index { get; private set; }

    public RngContext(ulong seed)
    {
        _state = seed;
        Index = 0;
    }

    public static RngContext FromGuid(Guid g)
    {
        Span<byte> b = stackalloc byte[16];
        g.TryWriteBytes(b);
        // 混合为 64 位种子
        ulong hi = BitConverter.ToUInt64(b.Slice(0, 8));
        ulong lo = BitConverter.ToUInt64(b.Slice(8, 8));
        return new RngContext(Hash64(hi ^ lo));
    }

    public RngContext Split(ulong salt)
    {
        // 基于当前状态与 salt 生成新种子，不影响本流
        ulong mixed = Hash64(_state ^ salt);
        return new RngContext(mixed);
    }

    public ulong NextUInt64()
    {
        // SplitMix64
        ulong z = (_state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z = z ^ (z >> 31);
        Index++;
        return z;
    }

    public double NextDouble()
    {
        // 53-bit mantissa to [0,1)
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentException("maxExclusive must be > minInclusive");
        uint range = (uint)(maxExclusive - minInclusive);
        // 使用 32 位避免取模偏差的简化方案（对游戏 RNG 足够）
        uint r = (uint)(NextUInt64() & 0xFFFFFFFF);
        return minInclusive + (int)(r % range);
    }

    public bool NextBool(double p = 0.5)
    {
        if (p <= 0) return false;
        if (p >= 1) return true;
        return NextDouble() < p;
    }

    public void Skip(long count)
    {
        for (long i = 0; i < count; i++) _ = NextUInt64();
    }

    public static ulong Hash64(ulong x)
    {
        // 同 SplitMix64 的一步哈希，可用于派生子种子
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x = x ^ (x >> 31);
        return x;
    }
}