using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Domain.Shop;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// EF Core �����ģ��ۺ�����ʵ�弯�� (DbSet) ��ģ�����á�
/// �� Program.cs ��ע���������ڣ�ͨ��Ϊ Scoped����
/// ְ��
///   * ��¶ DbSet ���ϲ�ͨ�� LINQ ������ѯ
///   * �� OnModelCreating ��Ӧ��ʵ��ӳ������
///   * ������Ҫʱ��д SaveChanges / SaveChangesAsync ����ơ������¼�������������
/// </summary>
public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
        // �˴�ͨ��������ʱ����������ע���ѹ���� options�����Ӵ� / �ṩ���� / �������ȣ�
    }

    // === �ۺϸ� / ʵ�弯�� ===
    // DbSet<T> ֻ�ǲ�ѯ/������ڣ�������ӳ��ϸ�����������Configuration��
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BattleRecord> Battles => Set<BattleRecord>();
    public DbSet<BattleSegmentRecord> BattleSegments => Set<BattleSegmentRecord>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<EconomyEventRecord> EconomyEvents => Set<EconomyEventRecord>();
    public DbSet<RunningBattleSnapshotRecord> RunningBattleSnapshots => Set<RunningBattleSnapshotRecord>();
    public DbSet<ActivityPlan> ActivityPlans => Set<ActivityPlan>();
    
    // === 装备系统 ===
    public DbSet<GearDefinition> GearDefinitions => Set<GearDefinition>();
    public DbSet<GearInstance> GearInstances => Set<GearInstance>();
    public DbSet<Affix> Affixes => Set<Affix>();
    public DbSet<GearSet> GearSets => Set<GearSet>();

    // === 商店系统 ===
    public DbSet<ShopDefinition> ShopDefinitions => Set<ShopDefinition>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<PurchaseRecord> PurchaseRecords => Set<PurchaseRecord>();
    public DbSet<PurchaseCounter> PurchaseCounters => Set<PurchaseCounter>();

    /// <summary>
    /// ģ�͹������ӣ����� Fluent ���á�
    /// ��ǰ�� ApplyConfigurationsFromAssembly �Զ�ɨ��ʵ�� IEntityTypeConfiguration<> �������ࡣ
    /// ����ʱ�����״�ʹ�ø���������Ҫģ�ͣ������һ�β�ѯ������Ǩ�ƣ�ʱ��
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // �Զ�����ͬ�����µ� *Configuration �ࣨ�� CharacterConfiguration��BattleConfiguration��
        // �ŵ㣺�����ֹ�������ã�����ʵ��ֻ�����������༴�ɡ�
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        // ���ڴ˴�����ȫ��Լ�����磺ͳһ�ַ����г��ȡ���ɾ����������ʱ��ת�����ȣ�
        
        // 添加装备系统种子数据
        modelBuilder.SeedEquipmentData();

        // 添加商店系统种子数据
        modelBuilder.SeedShops();

        base.OnModelCreating(modelBuilder);
    }
}