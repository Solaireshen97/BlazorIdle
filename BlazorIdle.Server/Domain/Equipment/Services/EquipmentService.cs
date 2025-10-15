using BlazorIdle.Server.Domain.Common.Utilities;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备管理服务
/// 负责装备/卸下操作、装备验证等
/// </summary>
public class EquipmentService
{
    private readonly GameDbContext _context;
    private readonly EquipmentValidator _validator;
    private readonly ILogger<EquipmentService> _logger;

    public EquipmentService(GameDbContext context, EquipmentValidator validator, ILogger<EquipmentService> logger)
    {
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>操作结果</returns>
    /// <exception cref="ArgumentException">当ID无效时抛出</exception>
    public async Task<EquipmentResult> EquipAsync(Guid characterId, Guid gearInstanceId)
    {
        // 参数验证
        ValidationHelper.ValidateGuid(characterId, nameof(characterId));
        ValidationHelper.ValidateGuid(gearInstanceId, nameof(gearInstanceId));

        _logger.LogInformation(
            "装备穿戴开始，CharacterId={CharacterId}, GearInstanceId={GearInstanceId}",
            characterId, gearInstanceId);
        
        // 1. 获取装备实例
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            _logger.LogWarning("装备不存在，CharacterId={CharacterId}, GearInstanceId={GearInstanceId}", characterId, gearInstanceId);
            return EquipmentResult.Failure("装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != null && gear.CharacterId != characterId)
        {
            return EquipmentResult.Failure("该装备属于其他角色");
        }

        // 3. 验证装备状态
        if (gear.IsEquipped)
        {
            return EquipmentResult.Failure("装备已装备");
        }

        // 4. 确定装备槽位
        var slot = DetermineEquipmentSlot(gear);
        if (slot == null)
        {
            return EquipmentResult.Failure("无法确定装备槽位");
        }

        // 5. 获取角色信息并验证职业和等级限制
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null)
        {
            return EquipmentResult.Failure("角色不存在");
        }

        if (gear.Definition != null)
        {
            var validation = _validator.ValidateEquip(
                gear.Definition,
                character.Profession,
                character.Level,
                slot.Value
            );

            if (!validation.IsSuccess)
            {
                return EquipmentResult.Failure(validation.ErrorMessage ?? "装备验证失败");
            }
        }

        // 6. 处理双手武器特殊逻辑
        if (slot == EquipmentSlot.TwoHand)
        {
            // 卸下主手和副手
            await UnequipSlotAsync(characterId, EquipmentSlot.MainHand);
            await UnequipSlotAsync(characterId, EquipmentSlot.OffHand);
        }
        else if (slot == EquipmentSlot.MainHand || slot == EquipmentSlot.OffHand)
        {
            // 如果当前装备了双手武器，需要先卸下
            await UnequipSlotAsync(characterId, EquipmentSlot.TwoHand);
        }

        // 7. 卸下该槽位现有装备
        await UnequipSlotAsync(characterId, slot.Value);

        // 8. 装备新物品
        gear.CharacterId = characterId;
        gear.SlotType = slot;
        gear.IsEquipped = true;
        gear.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "装备穿戴完成，CharacterId={CharacterId}, GearInstanceId={GearInstanceId}, Slot={Slot}, GearName={GearName}, TierLevel={TierLevel}",
            characterId, gearInstanceId, slot, gear.Definition?.Name, gear.TierLevel);

        return EquipmentResult.Success($"成功装备 {gear.Definition?.Name ?? "装备"}");
    }

    /// <summary>
    /// 卸下装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">装备槽位</param>
    /// <returns>操作结果</returns>
    /// <exception cref="ArgumentException">当角色ID无效时抛出</exception>
    public async Task<EquipmentResult> UnequipAsync(Guid characterId, EquipmentSlot slot)
    {
        // 参数验证
        ValidationHelper.ValidateGuid(characterId, nameof(characterId));

        _logger.LogInformation("装备卸下开始，CharacterId={CharacterId}, Slot={Slot}", characterId, slot);
        
        var gear = await _context.Set<GearInstance>()
            .FirstOrDefaultAsync(g => g.CharacterId == characterId 
                                   && g.SlotType == slot 
                                   && g.IsEquipped);

        if (gear == null)
        {
            _logger.LogDebug("该槽位没有装备，CharacterId={CharacterId}, Slot={Slot}", characterId, slot);
            return EquipmentResult.Success("该槽位没有装备");
        }

        gear.SlotType = null;
        gear.IsEquipped = false;
        gear.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("装备卸下完成，CharacterId={CharacterId}, Slot={Slot}, GearInstanceId={GearInstanceId}", 
            characterId, slot, gear.Id);

        return EquipmentResult.Success("成功卸下装备");
    }

    /// <summary>
    /// 获取角色所有已装备的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>装备列表，如果没有装备则返回空列表</returns>
    /// <exception cref="ArgumentException">当角色ID无效时抛出</exception>
    public async Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId)
    {
        // 参数验证
        ValidationHelper.ValidateGuid(characterId, nameof(characterId));
        
        return await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .Where(g => g.CharacterId == characterId && g.IsEquipped)
            .ToListAsync();
    }

    /// <summary>
    /// 获取指定槽位的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">装备槽位</param>
    /// <returns>装备实例，如果槽位没有装备则返回null</returns>
    /// <exception cref="ArgumentException">当角色ID无效时抛出</exception>
    public async Task<GearInstance?> GetEquippedGearInSlotAsync(Guid characterId, EquipmentSlot slot)
    {
        // 参数验证
        ValidationHelper.ValidateGuid(characterId, nameof(characterId));
        
        return await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.CharacterId == characterId 
                                   && g.SlotType == slot 
                                   && g.IsEquipped);
    }

    /// <summary>
    /// 卸下指定槽位的装备（内部方法）
    /// </summary>
    private async Task UnequipSlotAsync(Guid characterId, EquipmentSlot slot)
    {
        var gear = await _context.Set<GearInstance>()
            .FirstOrDefaultAsync(g => g.CharacterId == characterId 
                                   && g.SlotType == slot 
                                   && g.IsEquipped);

        if (gear != null)
        {
            gear.SlotType = null;
            gear.IsEquipped = false;
            gear.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 确定装备槽位
    /// </summary>
    private EquipmentSlot? DetermineEquipmentSlot(GearInstance gear)
    {
        if (gear.Definition == null)
        {
            return null;
        }

        return gear.Definition.Slot;
    }
}

/// <summary>
/// 装备操作结果
/// </summary>
public class EquipmentResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";

    public static EquipmentResult Success(string message) => new() { IsSuccess = true, Message = message };
    public static EquipmentResult Failure(string message) => new() { IsSuccess = false, Message = message };
}
