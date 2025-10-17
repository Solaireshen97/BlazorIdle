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
        // 配置 SQLite 连接以提高并发性和安全性
        // 在连接打开时设置 PRAGMA
        Database.OpenConnection();
        if (Database.GetDbConnection() is Microsoft.Data.Sqlite.SqliteConnection sqliteConn && sqliteConn.State == System.Data.ConnectionState.Open)
        {
            using var cmd = sqliteConn.CreateCommand();
            
            // 启用 WAL 模式以支持并发读写
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
            
            // 设置同步模式为 NORMAL (更快但仍然安全)
            // FULL 最安全但最慢，NORMAL 在大多数情况下足够安全
            cmd.CommandText = "PRAGMA synchronous = NORMAL;";
            cmd.ExecuteNonQuery();
            
            // 设置 WAL 自动检查点阈值（1000 页，约 4MB）
            // 这确保 WAL 文件不会无限增长
            cmd.CommandText = "PRAGMA wal_autocheckpoint = 1000;";
            cmd.ExecuteNonQuery();
            
            // 启用外键约束
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }
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

    /// <summary>
    /// 重写 Dispose 以确保 WAL 检查点在关闭前执行
    /// </summary>
    public override void Dispose()
    {
        // 在关闭连接前执行 WAL 检查点，确保所有数据写入主数据库文件
        try
        {
            if (Database.GetDbConnection() is Microsoft.Data.Sqlite.SqliteConnection sqliteConn && 
                sqliteConn.State == System.Data.ConnectionState.Open)
            {
                using var cmd = sqliteConn.CreateCommand();
                // TRUNCATE 模式会尝试将所有 WAL 数据写入主数据库并截断 WAL 文件
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // 静默失败，不阻止 Dispose
        }
        
        base.Dispose();
    }

    /// <summary>
    /// 异步 Dispose 版本
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        // 在关闭连接前执行 WAL 检查点
        try
        {
            if (Database.GetDbConnection() is Microsoft.Data.Sqlite.SqliteConnection sqliteConn && 
                sqliteConn.State == System.Data.ConnectionState.Open)
            {
                using var cmd = sqliteConn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // 静默失败，不阻止 Dispose
        }
        
        await base.DisposeAsync();
    }
}