using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorIdle.Server.Infrastructure.Persistence.Configurations;

public class RunningBattleSnapshotConfiguration : IEntityTypeConfiguration<RunningBattleSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<RunningBattleSnapshotRecord> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.StepBattleId).IsUnique();   // 每个 StepBattleId 只保留最新一行
        b.Property(x => x.EnemyId).HasMaxLength(64);
        b.Property(x => x.Seed).HasMaxLength(64);
        b.Property(x => x.SnapshotJson).HasMaxLength(8000);
    }
}