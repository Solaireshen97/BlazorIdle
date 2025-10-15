using System;

namespace BlazorIdle.Server.Domain.Common.Utilities;

/// <summary>
/// 参数验证辅助工具类
/// 提供统一的参数验证方法，消除重复的验证代码
/// </summary>
/// <remarks>
/// 职责：
/// 1. 验证 Guid 参数不为空（Guid.Empty）
/// 2. 验证对象引用不为 null
/// 3. 验证数值为正数
/// 4. 验证数值在指定范围内
/// 
/// 使用场景：
/// - 服务类的方法参数验证
/// - API 控制器的输入验证
/// - 领域模型的构造函数验证
/// </remarks>
public static class ValidationHelper
{
    /// <summary>
    /// 验证 Guid 参数不为空（Guid.Empty）
    /// </summary>
    /// <param name="value">要验证的 Guid 值</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentException">当 Guid 为 Guid.Empty 时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateGuid(characterId, nameof(characterId));
    /// </code>
    /// </example>
    public static void ValidateGuid(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{paramName} 不能为空", paramName);
        }
    }

    /// <summary>
    /// 验证对象引用不为 null
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="value">要验证的对象</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentNullException">当对象为 null 时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateNotNull(character, nameof(character));
    /// </code>
    /// </example>
    public static void ValidateNotNull<T>(T? value, string paramName) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName, $"{paramName} 不能为 null");
        }
    }

    /// <summary>
    /// 验证整数值为正数（大于0）
    /// </summary>
    /// <param name="value">要验证的整数值</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentException">当值小于等于0时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidatePositive(level, nameof(level));
    /// </code>
    /// </example>
    public static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{paramName} 必须为正数（大于0）", paramName);
        }
    }

    /// <summary>
    /// 验证浮点数值为正数（大于0）
    /// </summary>
    /// <param name="value">要验证的浮点数值</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentException">当值小于等于0时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidatePositive(damage, nameof(damage));
    /// </code>
    /// </example>
    public static void ValidatePositive(double value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{paramName} 必须为正数（大于0）", paramName);
        }
    }

    /// <summary>
    /// 验证整数值在指定范围内（闭区间）
    /// </summary>
    /// <param name="value">要验证的整数值</param>
    /// <param name="min">最小值（包含）</param>
    /// <param name="max">最大值（包含）</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentOutOfRangeException">当值不在指定范围内时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateRange(level, 1, 100, nameof(level));
    /// </code>
    /// </example>
    public static void ValidateRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName,
                $"{paramName} 必须在 {min} 和 {max} 之间（当前值: {value}）");
        }
    }

    /// <summary>
    /// 验证浮点数值在指定范围内（闭区间）
    /// </summary>
    /// <param name="value">要验证的浮点数值</param>
    /// <param name="min">最小值（包含）</param>
    /// <param name="max">最大值（包含）</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentOutOfRangeException">当值不在指定范围内时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateRange(criticalChance, 0.0, 1.0, nameof(criticalChance));
    /// </code>
    /// </example>
    public static void ValidateRange(double value, double min, double max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName,
                $"{paramName} 必须在 {min} 和 {max} 之间（当前值: {value}）");
        }
    }

    /// <summary>
    /// 验证整数值不为负数（大于等于0）
    /// </summary>
    /// <param name="value">要验证的整数值</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentException">当值小于0时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateNonNegative(count, nameof(count));
    /// </code>
    /// </example>
    public static void ValidateNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentException($"{paramName} 不能为负数", paramName);
        }
    }

    /// <summary>
    /// 验证浮点数值不为负数（大于等于0）
    /// </summary>
    /// <param name="value">要验证的浮点数值</param>
    /// <param name="paramName">参数名称，用于异常消息</param>
    /// <exception cref="ArgumentException">当值小于0时抛出</exception>
    /// <example>
    /// <code>
    /// ValidationHelper.ValidateNonNegative(amount, nameof(amount));
    /// </code>
    /// </example>
    public static void ValidateNonNegative(double value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentException($"{paramName} 不能为负数", paramName);
        }
    }
}
