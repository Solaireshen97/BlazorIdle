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
        // 配置将通过连接字符串和 OnConfiguring 应用
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
        ExecuteWalCheckpoint();
        base.Dispose();
    }

    /// <summary>
    /// 异步 Dispose 版本
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        // 在关闭连接前执行 WAL 检查点
        await ExecuteWalCheckpointAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// 执行 WAL 检查点（同步版本）
    /// </summary>
    private void ExecuteWalCheckpoint()
    {
        try
        {
            var connection = Database.GetDbConnection();
            if (connection is Microsoft.Data.Sqlite.SqliteConnection sqliteConn)
            {
                var needsClose = false;
                if (sqliteConn.State != System.Data.ConnectionState.Open)
                {
                    sqliteConn.Open();
                    needsClose = true;
                }

                try
                {
                    using var cmd = sqliteConn.CreateCommand();
                    // TRUNCATE 模式会尝试将所有 WAL 数据写入主数据库并截断 WAL 文件
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    cmd.ExecuteNonQuery();
                }
                finally
                {
                    if (needsClose && sqliteConn.State == System.Data.ConnectionState.Open)
                    {
                        sqliteConn.Close();
                    }
                }
            }
        }
        catch
        {
            // 静默失败，不阻止 Dispose
        }
    }

    /// <summary>
    /// 执行 WAL 检查点（异步版本）
    /// </summary>
    private async Task ExecuteWalCheckpointAsync()
    {
        try
        {
            var connection = Database.GetDbConnection();
            if (connection is Microsoft.Data.Sqlite.SqliteConnection sqliteConn)
            {
                var needsClose = false;
                if (sqliteConn.State != System.Data.ConnectionState.Open)
                {
                    await sqliteConn.OpenAsync();
                    needsClose = true;
                }

                try
                {
                    using var cmd = sqliteConn.CreateCommand();
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (needsClose && sqliteConn.State == System.Data.ConnectionState.Open)
                    {
                        await sqliteConn.CloseAsync();
                    }
                }
            }
        }
        catch
        {
            // 静默失败，不阻止 Dispose
        }
    }
}