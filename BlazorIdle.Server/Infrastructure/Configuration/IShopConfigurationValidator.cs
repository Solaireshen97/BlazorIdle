using BlazorIdle.Server.Domain.Shop.Configuration;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置验证器接口
/// </summary>
public interface IShopConfigurationValidator
{
    /// <summary>
    /// 验证商店定义配置
    /// </summary>
    /// <param name="config">商店定义配置</param>
    /// <returns>验证结果，包含是否有效和错误信息列表</returns>
    (bool isValid, List<string> errors) ValidateShopDefinitions(ShopDefinitionsConfig config);
    
    /// <summary>
    /// 验证商品配置
    /// </summary>
    /// <param name="config">商品配置</param>
    /// <param name="shopDefinitions">商店定义配置（用于引用验证）</param>
    /// <returns>验证结果，包含是否有效和错误信息列表</returns>
    (bool isValid, List<string> errors) ValidateShopItems(ShopItemsConfig config, ShopDefinitionsConfig shopDefinitions);
}
