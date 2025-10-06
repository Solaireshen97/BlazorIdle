using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BlazorIdle.Server.Infrastructure.Persistence
{
    // 设计时工厂：让 `dotnet ef` 在不启动完整 Host 的情况下创建 GameDbContext
    public sealed class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var conn = cfg.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";

            var builder = new DbContextOptionsBuilder<GameDbContext>();
            builder.UseSqlite(conn);

            return new GameDbContext(builder.Options);
        }
    }
}