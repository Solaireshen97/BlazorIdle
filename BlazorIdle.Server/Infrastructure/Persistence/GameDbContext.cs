using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// EF Core �����ģ��ۺ�����ʵ�弯�� (DbSet) ��ģ�����á�
/// �� Program.cs ��ע���������ڣ�ͨ��Ϊ Scoped����
/// ְ��
///   * ��¶ DbSet ���ϲ�ͨ�� LINQ ������ѯ
///   * �� OnModelCreating ��Ӧ��ʵ��ӳ������
///   * ������Ҫʱ��д SaveChanges / SaveChangesAsync ����ơ������¼������������
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
    public DbSet<BattleRecord> Battles => Set<BattleRecord>();
    public DbSet<BattleSegmentRecord> BattleSegments => Set<BattleSegmentRecord>();

    /// <summary>
    /// ģ�͹������ӣ����� Fluent ���á�
    /// ��ǰ�� ApplyConfigurationsFromAssembly �Զ�ɨ��ʵ�� IEntityTypeConfiguration<> �������ࡣ
    /// ����ʱ�����״�ʹ�ø���������Ҫģ�ͣ������һ�β�ѯ������Ǩ�ƣ�ʱ��
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // �Զ�����ͬ�����µ� *Configuration �ࣨ�� CharacterConfiguration��BattleConfiguration��
        // �ŵ㣺�����ֹ�������ã�����ʵ��ֻ����������༴�ɡ�
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        // ���ڴ˴����ȫ��Լ�����磺ͳһ�ַ����г��ȡ���ɾ����������ʱ��ת�����ȣ�

        base.OnModelCreating(modelBuilder);
    }
}