using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Equipment;

/// <summary>
/// 装备管理服务
/// 负责装备/卸下操作、验证逻辑等
/// </summary>
public class EquipmentService
{
    private readonly IGearInstanceRepository _gearRepo;
    private readonly IGearDefinitionRepository _definitionRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<EquipmentService> _logger;

    public EquipmentService(
        IGearInstanceRepository gearRepo,
        IGearDefinitionRepository definitionRepo,
        ICharacterRepository characterRepo,
        ILogger<EquipmentService> logger)
    {
        _gearRepo = gearRepo;
        _definitionRepo = definitionRepo;
        _characterRepo = characterRepo;
        _logger = logger;
    }

    /// <summary>
    /// 装备物品到指定槽位
    /// </summary>
    public async Task<(bool Success, string Message)> EquipAsync(
        Guid characterId,
        Guid gearInstanceId,
        CancellationToken ct = default)
    {
        // 1. 验证装备实例存在
        var gear = await _gearRepo.GetByIdAsync(gearInstanceId, ct);
        if (gear == null)
        {
            return (false, "装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != characterId)
        {
            return (false, "该装备不属于该角色");
        }

        // 3. 验证装备未被装备
        if (gear.IsEquipped)
        {
            return (false, "该装备已被装备");
        }

        // 4. 获取装备定义
        var definition = await _definitionRepo.GetByIdAsync(gear.DefinitionId, ct);
        if (definition == null)
        {
            return (false, "装备定义不存在");
        }

        // 5. 验证角色等级需求
        var character = await _characterRepo.GetAsync(characterId, ct);
        if (character == null)
        {
            return (false, "角色不存在");
        }

        if (character.Level < definition.RequiredLevel)
        {
            return (false, $"需要等级 {definition.RequiredLevel}");
        }

        // 6. 获取目标槽位
        var slot = definition.Slot;

        // 7. 检查该槽位是否已有装备
        var existingGear = await _gearRepo.GetEquippedGearBySlotAsync(characterId, slot, ct);
        if (existingGear != null)
        {
            // 自动卸下旧装备
            existingGear.IsEquipped = false;
            existingGear.SlotType = null;
            await _gearRepo.UpdateAsync(existingGear, ct);
            
            _logger.LogDebug(
                "Auto-unequipped {OldGearId} from slot {Slot} for character {CharacterId}",
                existingGear.Id, slot, characterId);
        }

        // 8. 装备新装备
        gear.IsEquipped = true;
        gear.SlotType = slot;
        gear.IsBound = true; // 装备后绑定
        await _gearRepo.UpdateAsync(gear, ct);

        _logger.LogInformation(
            "Equipped {GearId} to slot {Slot} for character {CharacterId}",
            gearInstanceId, slot, characterId);

        return (true, "装备成功");
    }

    /// <summary>
    /// 卸下指定槽位的装备
    /// </summary>
    public async Task<(bool Success, string Message)> UnequipAsync(
        Guid characterId,
        EquipmentSlot slot,
        CancellationToken ct = default)
    {
        // 1. 获取该槽位的装备
        var gear = await _gearRepo.GetEquippedGearBySlotAsync(characterId, slot, ct);
        if (gear == null)
        {
            return (false, "该槽位没有装备");
        }

        // 2. 卸下装备
        gear.IsEquipped = false;
        gear.SlotType = null;
        await _gearRepo.UpdateAsync(gear, ct);

        _logger.LogInformation(
            "Unequipped {GearId} from slot {Slot} for character {CharacterId}",
            gear.Id, slot, characterId);

        return (true, "卸下成功");
    }

    /// <summary>
    /// 获取角色的所有已装备的装备
    /// </summary>
    public Task<List<GearInstance>> GetEquippedGearAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        return _gearRepo.GetEquippedGearAsync(characterId, ct);
    }

    /// <summary>
    /// 获取角色的所有装备（包括背包中的）
    /// </summary>
    public Task<List<GearInstance>> GetAllGearAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        return _gearRepo.GetGearByCharacterAsync(characterId, ct);
    }
}
