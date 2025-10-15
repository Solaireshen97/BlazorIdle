using BlazorIdle.Server.Application;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Hubs;
using BlazorIdle.Server.Infrastructure;
using BlazorIdle.Server.Services;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. 核心服务注册
// 添加基于控制器的 API 支持（包含路由 / 参数绑定 / 模型绑定等）
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer(); // 为最小 API / Controller 生成端点
builder.Services.AddSwaggerGen(options =>   // 生成 swagger.json + UI（用于调试/测试）
{
    // 配置JWT认证的支持
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 3. JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();

// 4. 业务层注册
builder.Services
    .AddInfrastructure(builder.Configuration)   // 注册基础设施层（DbContext / 仓储等，内部已调用 AddRepositories）
    .AddApplication();                          // 注册应用层服务（如Command/Query Handler 等）

// 4.5 SignalR 配置
builder.Services.Configure<SignalROptions>(builder.Configuration.GetSection("SignalR"));
builder.Services.Configure<BattleMessageOptions>(builder.Configuration.GetSection("BattleMessages"));

// 4.6 战斗引擎配置
builder.Services.Configure<BlazorIdle.Server.Infrastructure.Configuration.CombatEngineOptions>(
    builder.Configuration.GetSection("CombatEngine"));

// 初始化战斗系统静态配置（静态类需要手动初始化）
var combatOptions = Microsoft.Extensions.Options.Options.Create(
    builder.Configuration.GetSection("CombatEngine").Get<BlazorIdle.Server.Infrastructure.Configuration.CombatEngineOptions>() 
    ?? new BlazorIdle.Server.Infrastructure.Configuration.CombatEngineOptions());
BlazorIdle.Server.Domain.Combat.DamageCalculator.Initialize(combatOptions);
BlazorIdle.Server.Domain.Combat.CombatConstants.Initialize(combatOptions);

builder.Services.AddSignalR(options =>
{
    var signalRConfig = builder.Configuration.GetSection("SignalR").Get<SignalROptions>() ?? new SignalROptions();
    options.KeepAliveInterval = TimeSpan.FromSeconds(signalRConfig.KeepAliveIntervalSeconds);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(signalRConfig.ServerTimeoutSeconds);
});
builder.Services.AddSingleton<IBattleNotificationService, BattleNotificationService>();
builder.Services.AddSingleton<BattleMessageFormatter>();

// SignalR Stage 4 服务：配置管理与监控
builder.Services.AddSingleton<SignalRConfigurationService>();
builder.Services.AddSingleton<SignalRMetricsCollector>();

// SignalR 启动验证器（确保配置正确）
builder.Services.AddHostedService<SignalRStartupValidator>();

// SignalR 可选扩展组件（过滤器支持）
// 如需使用过滤器，取消注释以下行：
// builder.Services.AddSingleton<NotificationFilterPipeline>();
// builder.Services.AddTransient<INotificationFilter, EventTypeFilter>();
// builder.Services.AddTransient<INotificationFilter, RateLimitFilter>();

// 5. 注册离线检测后台服务
builder.Services.AddHostedService<OfflineDetectionService>();

// 6. CORS策略
// 目的：允许前端 Blazor WebAssembly（运行在其他端口）访问后端 API。
// 注意：生产环境可改为精确来源，或者动态读取配置。如果需要携带凭据再加 AllowCredentials().
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001", // 如果这是本地 HTTPS 静态资源或开发服务器
                "http://localhost:5001",  // HTTP 版本
                "http://localhost:5000")  // HTTP 版本
            .AllowAnyHeader()
            .AllowAnyMethod();
        // .AllowCredentials(); // 若未来使用 Cookie/授权头需要携带凭据
    });
});

var app = builder.Build();

// 6. 自动迁移（仅开发环境）
// - 创建一个临时 Scope 取得 DbContext 和环境
// - Development 下自动执行 Migrate() 方便快速迭代
// - 生产环境建议：预先迁移（CI/CD、运维管理员手动执行）
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // 如果迁移或数据库异常，这里会执行
        // dotnet ef migrations add InitBattle
        // dotnet ef database update
        db.Database.Migrate();
    }
    else
    {
        // 生产环境选择题：
        // 1) 手动迁移（推荐）
        // 2) 仍然自动迁移（不推荐，容易控制结构的变更）
        // 3) 检查待迁移并日志警告:
        // if (db.Database.GetPendingMigrations().Any()) { /* log warning */ }
    }
}

// 7. 中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseHttpsRedirection();      // 强制重定向到 HTTPS（确保 swagger 也能通过 https 访问）
app.UseCors("AllowBlazorClient"); // 放在所有 MapControllers 之前，必须在认证/授权前（如果有的话）

// 重要：认证和授权中间件的顺序通常是： UseAuthentication -> UseAuthorization
app.UseAuthentication(); // 认证中间件
app.UseAuthorization();  // 授权中间件

app.MapControllers(); // 映射控制器端点到路由表

// SignalR Hub 映射
var signalRConfig = app.Configuration.GetSection("SignalR").Get<SignalROptions>() ?? new SignalROptions();
if (signalRConfig.EnableSignalR)
{
    app.MapHub<BattleNotificationHub>(signalRConfig.HubEndpoint);
}

// TODO：可选扩展（如app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // ������������ͬ��������ֱ������ֹͣ��