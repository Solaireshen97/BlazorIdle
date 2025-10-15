using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 装备系统 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 查询角色装备栏和总属性
/// - 装备/卸下装备
/// - 查询角色背包中的装备列表
/// - 分解装备获取材料（单个/批量）
/// - 预览分解产出
/// - 重铸装备提升品级
/// - 预览重铸成本
/// 
/// 认证要求：
/// - 所有接口均无需认证（基于角色ID操作）
/// 
/// 基础路由：/api/equipment
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsAggregationService;
    private readonly DisenchantService _disenchantService;
    private readonly ReforgeService _reforgeService;
    private readonly GameDbContext _context;

    public EquipmentController(
        EquipmentService equipmentService,
        StatsAggregationService statsAggregationService,
        DisenchantService disenchantService,
        ReforgeService reforgeService,
        GameDbContext context)
    {
        _equipmentService = equipmentService;
        _statsAggregationService = statsAggregationService;
        _disenchantService = disenchantService;
        _reforgeService = reforgeService;
        _context = context;
    }
    /// <summary>
    /// 获取角色的装备栏
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>装备栏信息，包含所有装备槽和总属性</returns>
    /// <response code="200">返回完整装备栏信息</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回角色的所有17个装备槽位（头、颈、肩、背、胸、腕、手、腰、腿、脚、2戒指、2饰品、主手、副手、双手）
    /// - 计算并返回装备总属性加成
    /// - 显示武器类型信息（单手/双手/双持/空手）
    /// - 如果装备盾牌，额外显示格挡率
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/equipment/123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "characterName": "我的战士",
    ///   "slots": [
    ///     {
    ///       "slotType": "Head",
    ///       "slotName": "头盔",
    ///       "item": {
    ///         "id": "...",
    ///         "name": "传奇头盔",
    ///         "rarity": "Legendary",
    ///         "itemLevel": 50,
    ///         "stats": { "Armor": 150, "Strength": 25 }
    ///       }
    ///     }
    ///   ],
    ///   "totalStats": { "Armor": 1500, "Strength": 200, ... },
    ///   "weaponInfo": "双手武器: 双手剑"
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<object>> GetEquipment(Guid characterId)
    {
        // 获取所有装备
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);
        
        // 获取角色信息
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null)
        {
            return NotFound(new { error = "角色不存在" });
        }

        // 定义所有17个槽位
        var allSlots = new[]
        {
            EquipmentSlot.Head, EquipmentSlot.Neck, EquipmentSlot.Shoulder,
            EquipmentSlot.Back, EquipmentSlot.Chest, EquipmentSlot.Wrist,
            EquipmentSlot.Hands, EquipmentSlot.Waist, EquipmentSlot.Legs,
            EquipmentSlot.Feet, EquipmentSlot.Finger1, EquipmentSlot.Finger2,
            EquipmentSlot.Trinket1, EquipmentSlot.Trinket2,
            EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.TwoHand
        };

        // 构建槽位信息
        var slots = allSlots.Select(slot =>
        {
            var gear = equippedGear.FirstOrDefault(g => g.SlotType == slot);
            return new
            {
                SlotType = slot.ToString(),
                SlotName = GetSlotDisplayName(slot),
                Item = gear != null ? new
                {
                    Id = gear.Id,
                    DefinitionId = gear.DefinitionId,
                    Name = gear.Definition?.Name ?? "未知装备",
                    Icon = gear.Definition?.Icon ?? "?",
                    Rarity = gear.Rarity.ToString(),
                    Tier = (int)gear.Rarity, // Tier mapping to rarity for now
                    ItemLevel = gear.ItemLevel,
                    QualityScore = gear.QualityScore,
                    ArmorType = gear.Definition?.ArmorType.ToString(),
                    WeaponType = gear.Definition?.WeaponType.ToString(),
                    Affixes = gear.Affixes.Select(a => new
                    {
                        Id = a.AffixId,
                        Name = a.AffixId, // Using ID as name for now
                        StatId = a.StatType.ToString(),
                        Value = a.RolledValue,
                        DisplayText = $"+{a.RolledValue:F0} {a.StatType}"
                    }).ToList(),
                    SetId = gear.SetId,
                    Stats = gear.RolledStats.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value
                    )
                } : null,
                IsLocked = false
            };
        }).ToArray();

        // 获取总属性
        var stats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);
        
        // 获取武器信息（Phase 5）
        var mainHandType = await _statsAggregationService.GetMainHandWeaponTypeAsync(characterId);
        var offHandType = await _statsAggregationService.GetOffHandWeaponTypeAsync(characterId);
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);
        var blockChance = await _statsAggregationService.CalculateBlockChanceAsync(characterId);
        
        var weaponInfo = GetWeaponDisplayInfo(mainHandType, offHandType, isDualWielding);
        
        // 添加格挡率到统计（如果装备盾牌）
        if (blockChance > 0)
        {
            stats[StatType.BlockChance] = blockChance;
        }

        var response = new
        {
            characterId,
            characterName = character.Name,
            slots,
            totalStats = stats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            weaponInfo
        };

        return Ok(response);
    }
    
    /// <summary>
    /// 获取武器显示信息
    /// </summary>
    /// <param name="mainHand">主手武器类型</param>
    /// <param name="offHand">副手武器类型</param>
    /// <param name="isDualWielding">是否双持</param>
    /// <returns>武器信息的中文显示文本</returns>
    /// <remarks>
    /// 根据装备情况返回不同的显示文本：
    /// - 双持：显示"双持: 武器1 + 武器2"
    /// - 双手武器：显示"双手武器: 武器名称"
    /// - 单手武器：显示"单手武器: 武器名称"
    /// - 空手：显示"空手"
    /// </remarks>
    private static string GetWeaponDisplayInfo(WeaponType mainHand, WeaponType offHand, bool isDualWielding)
    {
        if (isDualWielding)
        {
            return $"双持: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)} + {AttackSpeedCalculator.GetWeaponTypeName(offHand)}";
        }
        else if (mainHand != WeaponType.None)
        {
            if (AttackSpeedCalculator.IsTwoHandedWeapon(mainHand))
            {
                return $"双手武器: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)}";
            }
            return $"单手武器: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)}";
        }
        return "空手";
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">装备请求（包含装备实例ID）</param>
    /// <returns>装备结果</returns>
    /// <response code="200">装备成功</response>
    /// <response code="400">装备失败（如：装备不属于该角色、槽位类型不匹配等）</response>
    /// <remarks>
    /// 功能说明：
    /// - 将背包中的装备实例装备到对应槽位
    /// - 如果槽位已有装备，会自动卸下并放回背包
    /// - 装备双手武器时会自动卸下主手和副手
    /// - 验证装备归属和槽位类型
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/equipment/123e4567-e89b-12d3-a456-426614174000/equip
    /// {
    ///   "gearInstanceId": "456e7890-e89b-12d3-a456-426614174111"
    /// }
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "装备成功",
    ///   "gear": {
    ///     "id": "456e7890-e89b-12d3-a456-426614174111",
    ///     "name": "传奇之剑",
    ///     "slotType": "MainHand",
    ///     "isEquipped": true
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{characterId:guid}/equip")]
    public async Task<ActionResult<object>> EquipItem(Guid characterId, [FromBody] EquipRequest request)
    {
        var result = await _equipmentService.EquipAsync(characterId, request.GearInstanceId);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        // 返回更新后的装备信息
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == request.GearInstanceId);

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = gear != null ? new
            {
                Id = gear.Id,
                Name = gear.Definition?.Name ?? "未知装备",
                SlotType = gear.SlotType?.ToString(),
                IsEquipped = gear.IsEquipped
            } : null
        });
    }

    /// <summary>
    /// 卸下指定槽位的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">槽位类型（如：Head, Chest, MainHand等）</param>
    /// <returns>卸下结果</returns>
    /// <response code="200">卸下成功或槽位本就为空</response>
    /// <response code="400">无效的槽位类型</response>
    /// <remarks>
    /// 功能说明：
    /// - 卸下指定槽位的装备并放回背包
    /// - 如果槽位本就为空，操作仍然成功
    /// - 卸下装备后角色属性会重新计算
    /// 
    /// 请求示例：
    /// <code>
    /// DELETE /api/equipment/123e4567-e89b-12d3-a456-426614174000/Head
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "卸下成功",
    ///   "slot": "Head"
    /// }
    /// </code>
    /// </remarks>
    [HttpDelete("{characterId:guid}/{slot}")]
    public async Task<ActionResult<object>> UnequipItem(Guid characterId, string slot)
    {
        if (!Enum.TryParse<EquipmentSlot>(slot, true, out var slotEnum))
        {
            return BadRequest(new { error = "无效的槽位类型" });
        }

        var result = await _equipmentService.UnequipAsync(characterId, slotEnum);

        return Ok(new
        {
            success = result.IsSuccess,
            message = result.Message,
            slot = slot
        });
    }

    /// <summary>
    /// 获取角色装备的总属性加成
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>总属性字典</returns>
    /// <response code="200">返回装备统计信息</response>
    /// <remarks>
    /// 功能说明：
    /// - 计算所有已装备物品的属性总和
    /// - 返回装备数量和总品质分数
    /// - 包含护甲、攻击力、暴击等所有属性类型
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/equipment/123e4567-e89b-12d3-a456-426614174000/stats
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "stats": {
    ///     "Armor": 1500,
    ///     "Strength": 200,
    ///     "CritChance": 15.5
    ///   },
    ///   "equippedCount": 12,
    ///   "totalQualityScore": 850
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{characterId:guid}/stats")]
    public async Task<ActionResult<object>> GetEquipmentStats(Guid characterId)
    {
        var summary = await _statsAggregationService.GetEquipmentStatsSummaryAsync(characterId);

        return Ok(new
        {
            characterId,
            stats = summary.Stats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            equippedCount = summary.EquippedCount,
            totalQualityScore = summary.TotalQualityScore
        });
    }

    /// <summary>
    /// 分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">分解请求（包含装备实例ID）</param>
    /// <returns>分解结果</returns>
    /// <response code="200">分解成功</response>
    /// <response code="400">分解失败（如：装备不属于该角色、装备已装备等）</response>
    /// <remarks>
    /// 功能说明：
    /// - 将装备分解为制造材料
    /// - 装备被永久删除，此操作不可逆
    /// - 已装备的装备不能分解，需先卸下
    /// - 材料产出基于装备的品级和稀有度
    /// - 材料自动添加到角色背包
    /// 
    /// 材料计算规则：
    /// - 普通品质：基础材料 x1
    /// - 稀有品质：基础材料 x2 + 中级材料 x1
    /// - 史诗品质：基础材料 x3 + 高级材料 x1
    /// - 传奇品质：基础材料 x5 + 高级材料 x2 + 精华 x1
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/equipment/123e4567-e89b-12d3-a456-426614174000/disenchant
    /// {
    ///   "gearInstanceId": "456e7890-e89b-12d3-a456-426614174111"
    /// }
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "分解成功",
    ///   "materials": {
    ///     "BasicOre": 5,
    ///     "AdvancedOre": 2,
    ///     "Essence": 1
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{characterId:guid}/disenchant")]
    public async Task<ActionResult<object>> DisenchantItem(Guid characterId, [FromBody] DisenchantRequest request)
    {
        var result = await _disenchantService.DisenchantAsync(characterId, request.GearInstanceId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            materials = result.Materials
        });
    }

    /// <summary>
    /// 批量分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">批量分解请求（包含多个装备实例ID）</param>
    /// <returns>批量分解结果</returns>
    /// <response code="200">返回批量分解统计结果</response>
    /// <remarks>
    /// 功能说明：
    /// - 一次性分解多件装备
    /// - 统计成功和失败的数量
    /// - 汇总所有获得的材料
    /// - 如果某件装备分解失败，其他装备继续处理
    /// - 失败原因会在errors字段中返回
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/equipment/123e4567-e89b-12d3-a456-426614174000/disenchant-batch
    /// {
    ///   "gearInstanceIds": [
    ///     "456e7890-e89b-12d3-a456-426614174111",
    ///     "456e7890-e89b-12d3-a456-426614174222"
    ///   ]
    /// }
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "successCount": 2,
    ///   "failCount": 0,
    ///   "totalMaterials": {
    ///     "BasicOre": 10,
    ///     "AdvancedOre": 3
    ///   },
    ///   "errors": []
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{characterId:guid}/disenchant-batch")]
    public async Task<ActionResult<object>> DisenchantBatch(Guid characterId, [FromBody] DisenchantBatchRequest request)
    {
        var result = await _disenchantService.DisenchantBatchAsync(characterId, request.GearInstanceIds);

        return Ok(new
        {
            successCount = result.SuccessCount,
            failCount = result.FailCount,
            totalMaterials = result.TotalMaterials,
            errors = result.Errors
        });
    }

    /// <summary>
    /// 预览装备分解
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>预览分解产出</returns>
    /// <response code="200">返回预览的材料产出</response>
    /// <remarks>
    /// 功能说明：
    /// - 在实际分解前预览可以获得的材料
    /// - 不会真正分解装备
    /// - 用于玩家决策是否要分解该装备
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/equipment/disenchant-preview/456e7890-e89b-12d3-a456-426614174111
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "gearInstanceId": "456e7890-e89b-12d3-a456-426614174111",
    ///   "materials": {
    ///     "BasicOre": 5,
    ///     "AdvancedOre": 2
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("disenchant-preview/{gearInstanceId:guid}")]
    public async Task<ActionResult<object>> PreviewDisenchant(Guid gearInstanceId)
    {
        var materials = await _disenchantService.PreviewDisenchantAsync(gearInstanceId);

        return Ok(new
        {
            gearInstanceId,
            materials
        });
    }

    /// <summary>
    /// 获取角色背包中的装备（未装备的装备实例）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>背包装备列表</returns>
    /// <response code="200">返回背包中的装备列表</response>
    /// <remarks>
    /// 功能说明：
    /// - 查询属于该角色但未装备的所有装备
    /// - 按稀有度、物品等级、品质分数排序
    /// - 用于装备选择界面和背包管理
    /// 
    /// 排序规则：
    /// 1. 稀有度优先（传奇 > 史诗 > 稀有 > 普通）
    /// 2. 物品等级次之
    /// 3. 品质分数最后
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/equipment/123e4567-e89b-12d3-a456-426614174000/inventory
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "count": 25,
    ///   "items": [
    ///     {
    ///       "id": "...",
    ///       "name": "传奇之剑",
    ///       "rarity": "Legendary",
    ///       "itemLevel": 50,
    ///       "qualityScore": 95
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{characterId:guid}/inventory")]
    public async Task<ActionResult<object>> GetInventoryGear(Guid characterId)
    {
        // 查询属于该角色但未装备的装备实例
        var inventoryGear = await _context.GearInstances
            .Include(g => g.Definition)
            .Where(g => g.CharacterId == characterId && g.SlotType == null)
            .OrderByDescending(g => g.Rarity)
            .ThenByDescending(g => g.ItemLevel)
            .ThenByDescending(g => g.QualityScore)
            .Select(g => new
            {
                Id = g.Id,
                DefinitionId = g.DefinitionId,
                Name = g.Definition != null ? g.Definition.Name : "未知装备",
                Icon = g.Definition != null ? g.Definition.Icon : "?",
                Rarity = g.Rarity.ToString(),
                TierLevel = g.TierLevel,
                ItemLevel = g.ItemLevel,
                QualityScore = g.QualityScore,
                ArmorType = g.Definition != null ? g.Definition.ArmorType.ToString() : null,
                WeaponType = g.Definition != null ? g.Definition.WeaponType.ToString() : null,
                SetId = g.SetId,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            characterId,
            count = inventoryGear.Count,
            items = inventoryGear
        });
    }

    /// <summary>
    /// 重铸装备（提升品级）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">重铸请求（包含装备实例ID）</param>
    /// <returns>重铸结果</returns>
    /// <response code="200">重铸成功</response>
    /// <response code="400">重铸失败（如：装备已达最高品级、材料不足等）</response>
    /// <remarks>
    /// 功能说明：
    /// - 消耗材料提升装备的品级（TierLevel）
    /// - 品级提升会增加装备的属性值和品质分数
    /// - 装备必须未装备状态才能重铸
    /// - 每次重铸需要消耗相应材料
    /// 
    /// 品级提升规则：
    /// - T1 → T2: 消耗基础材料 x10
    /// - T2 → T3: 消耗中级材料 x10
    /// - T3 → T4: 消耗高级材料 x10
    /// - T4 → T5: 消耗高级材料 x20 + 精华 x5
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/equipment/123e4567-e89b-12d3-a456-426614174000/reforge
    /// {
    ///   "gearInstanceId": "456e7890-e89b-12d3-a456-426614174111"
    /// }
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "重铸成功，品级提升至T2",
    ///   "gear": {
    ///     "id": "456e7890-e89b-12d3-a456-426614174111",
    ///     "name": "传奇之剑",
    ///     "tierLevel": 2,
    ///     "qualityScore": 85,
    ///     "rolledStats": { "AttackPower": 125, "CritChance": 5.5 }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{characterId:guid}/reforge")]
    public async Task<ActionResult<object>> ReforgeItem(Guid characterId, [FromBody] ReforgeRequest request)
    {
        var result = await _reforgeService.ReforgeAsync(characterId, request.GearInstanceId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = result.ReforgedGear != null ? new
            {
                Id = result.ReforgedGear.Id,
                Name = result.ReforgedGear.Definition?.Name ?? "未知装备",
                TierLevel = result.ReforgedGear.TierLevel,
                QualityScore = result.ReforgedGear.QualityScore,
                RolledStats = result.ReforgedGear.RolledStats.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value
                )
            } : null
        });
    }

    /// <summary>
    /// 预览重铸成本
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>重铸预览</returns>
    /// <response code="200">返回重铸预览信息</response>
    /// <remarks>
    /// 功能说明：
    /// - 在实际重铸前预览成本和属性提升
    /// - 显示当前品级和下一品级
    /// - 显示所需材料成本
    /// - 对比当前属性和预期属性
    /// - 不会真正执行重铸
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/equipment/reforge-preview/456e7890-e89b-12d3-a456-426614174111
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "canReforge": true,
    ///   "message": "可以重铸",
    ///   "currentTier": 1,
    ///   "nextTier": 2,
    ///   "cost": {
    ///     "BasicOre": 10
    ///   },
    ///   "currentStats": { "AttackPower": 100, "CritChance": 5.0 },
    ///   "previewStats": { "AttackPower": 120, "CritChance": 6.0 }
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("reforge-preview/{gearInstanceId:guid}")]
    public async Task<ActionResult<object>> PreviewReforge(Guid gearInstanceId)
    {
        var preview = await _reforgeService.PreviewReforgeCostAsync(gearInstanceId);

        return Ok(new
        {
            canReforge = preview.CanReforge,
            message = preview.Message,
            currentTier = preview.CurrentTier,
            nextTier = preview.NextTier,
            cost = preview.Cost,
            currentStats = preview.CurrentStats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            previewStats = preview.PreviewStats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            )
        });
    }

    /// <summary>
    /// 获取装备槽位的中文显示名称
    /// </summary>
    /// <param name="slot">装备槽位枚举</param>
    /// <returns>槽位的中文名称</returns>
    private static string GetSlotDisplayName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => "头盔",
            EquipmentSlot.Neck => "项链",
            EquipmentSlot.Shoulder => "护肩",
            EquipmentSlot.Back => "披风",
            EquipmentSlot.Chest => "胸甲",
            EquipmentSlot.Wrist => "护腕",
            EquipmentSlot.Hands => "手套",
            EquipmentSlot.Waist => "腰带",
            EquipmentSlot.Legs => "护腿",
            EquipmentSlot.Feet => "鞋子",
            EquipmentSlot.Finger1 => "戒指1",
            EquipmentSlot.Finger2 => "戒指2",
            EquipmentSlot.Trinket1 => "饰品1",
            EquipmentSlot.Trinket2 => "饰品2",
            EquipmentSlot.MainHand => "主手",
            EquipmentSlot.OffHand => "副手",
            EquipmentSlot.TwoHand => "双手武器",
            _ => slot.ToString()
        };
    }
}

/// <summary>
/// 装备请求数据传输对象
/// </summary>
public class EquipRequest
{
    /// <summary>
    /// 要装备的装备实例ID
    /// </summary>
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 分解请求数据传输对象
/// </summary>
public class DisenchantRequest
{
    /// <summary>
    /// 要分解的装备实例ID
    /// </summary>
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 批量分解请求数据传输对象
/// </summary>
public class DisenchantBatchRequest
{
    /// <summary>
    /// 要批量分解的装备实例ID列表
    /// </summary>
    public List<Guid> GearInstanceIds { get; set; } = new();
}

/// <summary>
/// 重铸请求数据传输对象
/// </summary>
public class ReforgeRequest
{
    /// <summary>
    /// 要重铸的装备实例ID
    /// </summary>
    public Guid GearInstanceId { get; set; }
}
