using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. 基础框架服务
// 添加基于控制器的 API 支持（属性路由 / 过滤器 / 模型绑定等）
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer(); // 为最小 API / Controller 生成描述
builder.Services.AddSwaggerGen();           // 生成 swagger.json + UI（开发期调试用）

// 3. 业务分层注入
builder.Services
    .AddInfrastructure(builder.Configuration)   // 注册基础设施：DbContext / 仓储（内部已调用 AddRepositories）
    .AddApplication();                          // 注册应用层用例、服务（Command/Query Handler 等）

// 4. CORS（跨域）
// 目的：允许前端 Blazor WebAssembly（本地开发端口）访问本 API。
// 注意：生产可改为精确来源或从配置读取；若需凭据需再加 AllowCredentials().
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001", // 可能是本地 HTTPS 静态资源或开发宿主
                "http://localhost:5000")  // HTTP 版本
            .AllowAnyHeader()
            .AllowAnyMethod();
        // .AllowCredentials(); // 如未来使用 Cookie/授权头并需要携带凭据
    });
});

var app = builder.Build();

// 5. 自动迁移（仅开发）
// - 创建一个临时 Scope 取出 DbContext 与环境
// - Development 环境自动执行 Migrate() 便于快速迭代
// - 生产建议改用：预先迁移（CI/CD）或人工审核后执行
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // 若无迁移会抛异常：需先执行
        // dotnet ef migrations add InitBattle
        // dotnet ef database update
        db.Database.Migrate();
    }
    else
    {
        // 生产可选策略：
        // 1) 手动迁移（推荐）
        // 2) 启用自动迁移（风险：不受控结构改动）
        // 3) 仅检测并日志告警:
        // if (db.Database.GetPendingMigrations().Any()) { /* log warning */ }
    }
}

// 6. 中间件管线
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseHttpsRedirection();      // 强制重定向至 HTTPS（确保 swagger 也可通过 https 访问）
app.UseCors("AllowBlazorClient"); // 必须在 MapControllers 之前，且在认证/授权前（如果有的话）

// 如果后续引入身份认证：顺序通常是 UseAuthentication -> UseAuthorization
app.MapControllers(); // 映射控制器端点到路由表

// TODO（可选扩展）：app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // 启动服务器（同步阻塞，直到主机停止）