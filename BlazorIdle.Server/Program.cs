using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using BlazorIdle.Server.Infrastructure.SignalR.Services;
using BlazorIdle.Server.Auth;
using BlazorIdle.Server.Auth.Services;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. 配置基础 Web 功能
// 添加基于控制器的 API 支持（路由/格式化/模型绑定等）
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer(); // 为 Minimal API / Controller 生成元数据
builder.Services.AddSwaggerGen();           // 生成 swagger.json 与 UI（开发环境使用）

// 3. 业务/基础设施依赖注入
builder.Services
    .AddInfrastructure(builder.Configuration)   // 注册基础设施：DbContext、仓储等（内部会调用 AddRepositories 等）
    .AddApplication();                          // 注册应用层：命令/查询处理器等

// 3.1 用户认证系统服务注册
// 注册用户存储服务（Singleton确保测试账户在应用生命周期内保持）
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();

// 配置JWT选项（从appsettings.json读取）
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
jwtOptions.Validate(); // 验证配置有效性
builder.Services.AddSingleton(jwtOptions);

// 注册认证服务（Scoped生命周期，每个请求独立实例）
builder.Services.AddScoped<IAuthService, AuthService>();

// 3.2 配置JWT Bearer认证
// 配置ASP.NET Core身份认证系统使用JWT Bearer令牌方案
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 配置JWT令牌验证参数
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // 验证JWT发行者（Issuer），确保令牌来自受信任的源
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            // 验证JWT受众（Audience），确保令牌是为此应用程序签发的
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            // 验证JWT过期时间，确保令牌未过期
            ValidateLifetime = true,

            // 验证JWT签名密钥，确保令牌未被篡改
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

            // 移除默认的5分钟时钟偏移（ClockSkew）
            // 默认情况下，ASP.NET Core会允许5分钟的时间误差
            // 设置为Zero使令牌过期时间更加精确
            ClockSkew = TimeSpan.Zero
        };

        // 配置JWT Bearer事件处理器
        options.Events = new JwtBearerEvents
        {
            // 当接收到消息时触发，用于从查询字符串中提取Token
            // 主要用于SignalR连接，因为WebSocket无法设置自定义HTTP头
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // 如果请求路径是SignalR Hub（/hubs/*），从查询字符串读取Token
                // 这是SignalR推荐的Token传递方式
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

            // 当认证失败时触发
            OnAuthenticationFailed = context =>
            {
                // 如果是因为Token过期导致的认证失败
                // 在响应头中添加标记，客户端可以据此判断是否需要刷新Token
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

// 3.3 配置授权策略
// 添加授权服务，支持基于策略的访问控制
builder.Services.AddAuthorization();

// 3.5 SignalR服务配置
// 加载SignalR配置
var signalROptions = new SignalROptions();
builder.Configuration.GetSection(SignalROptions.SectionName).Bind(signalROptions);
signalROptions.Validate(); // 验证配置有效性
builder.Services.AddSingleton(signalROptions);

// 添加SignalR核心服务和连接管理
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddSingleton<ISignalRDispatcher, SignalRDispatcher>();

builder.Services.AddSignalR(options =>
{
    // 从配置文件读取详细错误设置，开发环境可以覆盖
    options.EnableDetailedErrors = signalROptions.EnableDetailedErrors || builder.Environment.IsDevelopment();

    // 最大消息大小，防止过大的消息影响性能
    options.MaximumReceiveMessageSize = signalROptions.MaximumReceiveMessageSize;

    // 握手超时时间
    options.HandshakeTimeout = TimeSpan.FromSeconds(signalROptions.HandshakeTimeoutSeconds);

    // 心跳间隔，用于检测连接是否活跃
    options.KeepAliveInterval = TimeSpan.FromSeconds(signalROptions.KeepAliveIntervalSeconds);

    // 客户端超时时间，超过此时间未收到心跳则断开连接
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(signalROptions.ClientTimeoutSeconds);
})
.AddMessagePackProtocol(options =>
{
    // 使用MessagePack协议提升性能
    // 根据配置决定是否启用Lz4Block压缩
    if (signalROptions.EnableMessagePackCompression)
    {
        options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
            .WithCompression(MessagePack.MessagePackCompression.Lz4Block);
    }
    else
    {
        options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard;
    }
});

// 4. CORS 配置
// 目的：允许前端 Blazor WebAssembly（本地开发端口）访问 API
// 注意：生产请改为精确来源列表；仅在需要时启用 AllowCredentials()
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001", // 本地 HTTPS 静态资源/开发服务器
                "http://localhost:5000")  // 本地 HTTP 端口
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR需要启用凭证支持以建立WebSocket连接
    });
});

var app = builder.Build();

// 5. 自动迁移（仅开发环境）
// - 创建一个临时 Scope 获取 DbContext 与环境
// - Development 环境自动执行 Migrate() 便于快速迭代
// - 生产环境建议预先迁移（CI/CD 或运维人员执行）
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // 如缺少迁移或遇到异常，可手动执行：
        // dotnet ef migrations add InitBattle
        // dotnet ef database update
        db.Database.Migrate();
    }
    else
    {
        // 生产环境可选策略：
        // 1) 手动迁移（推荐）
        // 2) 启用自动迁移（有风险，可能导致结构变更）
        // 3) 仅检测待迁移并记录/告警：
        // if (db.Database.GetPendingMigrations().Any()) { /* log warning */ }
    }
}

// 6. 中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseHttpsRedirection();       // 强制重定向到 HTTPS，确保 swagger 也通过 https 访问
app.UseCors("AllowBlazorClient"); // 必须在 MapControllers 之前；按需与认证/授权中间件配合

// 6.1 启用认证和授权中间件
// 注意：必须按照以下顺序调用，且必须在MapControllers和MapHub之前
// UseAuthentication: 从请求中提取JWT令牌并验证，设置HttpContext.User
// UseAuthorization: 根据授权策略检查用户是否有权限访问资源
app.UseAuthentication();
app.UseAuthorization();

// 若启用身份验证，请遵循调用顺序：UseAuthentication -> UseAuthorization
app.MapControllers(); // 映射控制器端点到路由表

// 映射SignalR Hub端点
// /hubs/game 是统一的SignalR连接入口
app.MapHub<GameHub>("/hubs/game");

// TODO 可选扩展：app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // 启动应用（同步阻塞，直到进程停止）