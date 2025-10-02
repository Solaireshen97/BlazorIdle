using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. 基础框架服务
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // 如果你用的是 AddOpenApi() 也可以保留

// 3. 业务分层注入
builder.Services
    .AddInfrastructure(builder.Configuration)   // 注册 DbContext / 仓储
    .AddRepositories()
    .AddApplication();                          // 注册 UseCase / Runner 等

// 4. CORS（可把 Origins 放配置文件）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001",
                "http://localhost:5000")   // 视前端端口调整
            .AllowAnyHeader()
            .AllowAnyMethod();
        // 若未来需要 Cookie/身份: .AllowCredentials();
    });
});

var app = builder.Build();

// 5. 自动迁移（开发环境执行）
//    生产环境推荐用命令行迁移或 CI/CD 脚本，不要直接在启动执行 Migrate()
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // 确保使用 migrations（如果没有迁移，会抛错，先执行: dotnet ef migrations add InitBattle）
        db.Database.Migrate();
    }
    else
    {
        // 可选：生产是否允许自动迁移
        // db.Database.Migrate();
        // 或仅检测
        // if (db.Database.GetPendingMigrations().Any()) { /* 日志告警 */ }
    }
}

// 6. 中间件
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorClient");

// 如果后续引入身份/授权，此处按顺序添加 UseAuthentication / UseAuthorization
app.MapControllers();

//（以后可添加 /health, /metrics）
app.Run();