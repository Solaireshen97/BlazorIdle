using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using BlazorIdle.Server.Infrastructure.SignalR.Services;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. ������ܷ���
// ���ӻ��ڿ������� API ֧�֣�����·�� / ������ / ģ�Ͱ󶨵ȣ�
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer(); // Ϊ��С API / Controller ��������
builder.Services.AddSwaggerGen();           // ���� swagger.json + UI�������ڵ����ã�

// 3. ҵ��ֲ�ע��
builder.Services
    .AddInfrastructure(builder.Configuration)   // ע�������ʩ��DbContext / �ִ����ڲ��ѵ��� AddRepositories��
    .AddApplication();                          // ע��Ӧ�ò�����������Command/Query Handler �ȣ�

// 3.5 SignalR服务配置
// 添加SignalR核心服务和连接管理
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddSignalR(options =>
{
    // 开发环境启用详细错误信息，便于调试
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    
    // 最大消息大小：100KB，防止过大的消息影响性能
    options.MaximumReceiveMessageSize = 102400;
    
    // 握手超时时间：15秒
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    
    // 心跳间隔：15秒，用于检测连接是否活跃
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    
    // 客户端超时时间：30秒，超过此时间未收到心跳则断开连接
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddMessagePackProtocol(options =>
{
    // 使用MessagePack协议提升性能
    // Lz4Block压缩算法可以减少30-50%的数据传输量
    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
        .WithCompression(MessagePack.MessagePackCompression.Lz4Block);
});

// 4. CORS������
// Ŀ�ģ�����ǰ�� Blazor WebAssembly�����ؿ����˿ڣ����ʱ� API��
// ע�⣺�����ɸ�Ϊ��ȷ��Դ������ö�ȡ������ƾ�����ټ� AllowCredentials().
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001", // �����Ǳ��� HTTPS ��̬��Դ�򿪷�����
                "http://localhost:5000")  // HTTP �汾
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR需要启用凭证支持以建立WebSocket连接
    });
});

var app = builder.Build();

// 5. �Զ�Ǩ�ƣ���������
// - ����һ����ʱ Scope ȡ�� DbContext �뻷��
// - Development �����Զ�ִ�� Migrate() ���ڿ��ٵ���
// - ����������ã�Ԥ��Ǩ�ƣ�CI/CD�����˹���˺�ִ��
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // ����Ǩ�ƻ����쳣������ִ��
        // dotnet ef migrations add InitBattle
        // dotnet ef database update
        db.Database.Migrate();
    }
    else
    {
        // ������ѡ���ԣ�
        // 1) �ֶ�Ǩ�ƣ��Ƽ���
        // 2) �����Զ�Ǩ�ƣ����գ����ܿؽṹ�Ķ���
        // 3) ����Ⲣ��־�澯:
        // if (db.Database.GetPendingMigrations().Any()) { /* log warning */ }
    }
}

// 6. �м������
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseHttpsRedirection();      // ǿ���ض����� HTTPS��ȷ�� swagger Ҳ��ͨ�� https ���ʣ�
app.UseCors("AllowBlazorClient"); // ������ MapControllers ֮ǰ��������֤/��Ȩǰ������еĻ���

// �����������������֤��˳��ͨ���� UseAuthentication -> UseAuthorization
app.MapControllers(); // ӳ��������˵㵽·�ɱ�

// 映射SignalR Hub端点
// /hubs/game 是统一的SignalR连接入口
app.MapHub<GameHub>("/hubs/game");

// TODO����ѡ��չ����app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // ������������ͬ��������ֱ������ֹͣ��