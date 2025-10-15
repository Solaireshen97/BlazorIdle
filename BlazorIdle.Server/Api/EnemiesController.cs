using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 敌人数据控制器
/// </summary>
/// <remarks>
/// 提供游戏中所有敌人的静态数据查询功能。
/// 
/// <strong>核心功能</strong>：
/// 1. 获取所有敌人列表
/// 2. 查询单个敌人详情
/// 
/// <strong>数据来源</strong>：
/// - 所有敌人数据定义在 EnemyRegistry 中
/// - 数据是静态配置，不涉及运行时状态
/// - 不包含实例化的敌人（如战斗中的敌人状态）
/// 
/// <strong>敌人属性</strong>：
/// - 基础信息：ID、名称、等级
/// - 战斗属性：生命值、护甲、魔法抗性
/// 
/// <strong>用途</strong>：
/// - 前端显示敌人列表供玩家选择
/// - 战斗预览（查看敌人属性）
/// - 活动计划创建时选择目标敌人
/// 
/// <strong>注意</strong>：
/// - 此控制器返回的是静态定义，不包含战斗状态
/// - 实际战斗中的敌人状态通过 BattlesController 获取
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class EnemiesController : ControllerBase
{
    /// <summary>
    /// 获取所有敌人列表
    /// </summary>
    /// <returns>所有敌人的基础信息列表</returns>
    /// <response code="200">成功返回敌人列表</response>
    /// <remarks>
    /// GET /api/enemies
    /// 
    /// 返回游戏中所有已定义敌人的基础信息。
    /// 
    /// <strong>返回内容</strong>：
    /// - ID: 敌人唯一标识符
    /// - Name: 敌人名称（中文）
    /// - Level: 等级
    /// - MaxHp: 最大生命值
    /// - Armor: 物理护甲
    /// - MagicResist: 魔法抗性
    /// 
    /// <strong>使用场景</strong>：
    /// - 前端下拉列表显示可选敌人
    /// - 游戏百科/图鉴功能
    /// - 战斗选择界面
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/enemies
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// [
    ///   {
    ///     "id": "goblin",
    ///     "name": "哥布林",
    ///     "level": 5,
    ///     "maxHp": 150,
    ///     "armor": 10,
    ///     "magicResist": 5
    ///   },
    ///   {
    ///     "id": "orc",
    ///     "name": "兽人",
    ///     "level": 10,
    ///     "maxHp": 300,
    ///     "armor": 25,
    ///     "magicResist": 10
    ///   }
    /// ]
    /// ```
    /// </remarks>
    [HttpGet]
    public ActionResult<IEnumerable<EnemyDto>> GetAll()
    {
        // 标准化：直接枚举 EnemyRegistry 全量定义
        var list = EnemyRegistry.All()
            .Select(def => new EnemyDto
            {
                Id = def.Id,
                Name = def.Name,
                Level = def.Level,
                MaxHp = def.MaxHp,
                Armor = def.Armor,
                MagicResist = def.MagicResist
            })
            .ToList();
        return Ok(list);
    }

    /// <summary>
    /// 获取单个敌人详情
    /// </summary>
    /// <param name="id">敌人ID</param>
    /// <returns>敌人的详细信息</returns>
    /// <response code="200">成功返回敌人信息</response>
    /// <remarks>
    /// GET /api/enemies/{id}
    /// 
    /// 根据敌人ID查询其详细信息。
    /// 
    /// <strong>注意</strong>：
    /// - 如果敌人ID不存在，EnemyRegistry.Resolve 会抛出异常
    /// - 返回的是静态定义数据
    /// - 战斗中的敌人实例状态需通过战斗API获取
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/enemies/goblin
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "id": "goblin",
    ///   "name": "哥布林",
    ///   "level": 5,
    ///   "maxHp": 150,
    ///   "armor": 10,
    ///   "magicResist": 5
    /// }
    /// ```
    /// </remarks>
    [HttpGet("{id}")]
    public ActionResult<EnemyDto> GetOne(string id)
    {
        var def = EnemyRegistry.Resolve(id);
        return Ok(new EnemyDto
        {
            Id = def.Id,
            Name = def.Name,
            Level = def.Level,
            MaxHp = def.MaxHp,
            Armor = def.Armor,
            MagicResist = def.MagicResist
        });
    }

    /// <summary>
    /// 敌人数据传输对象
    /// </summary>
    /// <remarks>
    /// 包含敌人的基础属性信息，用于API响应。
    /// </remarks>
    public sealed class EnemyDto
    {
        /// <summary>敌人唯一标识符（如 "goblin", "orc"）</summary>
        public string Id { get; set; } = "";
        
        /// <summary>敌人名称（中文显示名）</summary>
        public string Name { get; set; } = "";
        
        /// <summary>敌人等级</summary>
        public int Level { get; set; }
        
        /// <summary>最大生命值</summary>
        public int MaxHp { get; set; }
        
        /// <summary>物理护甲值</summary>
        public double Armor { get; set; }
        
        /// <summary>魔法抗性值</summary>
        public double MagicResist { get; set; }
    }
}