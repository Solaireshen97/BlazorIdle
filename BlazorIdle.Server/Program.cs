using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. ������ܷ���
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // ������õ��� AddOpenApi() Ҳ���Ա���

// 3. ҵ��ֲ�ע��
builder.Services
    .AddInfrastructure(builder.Configuration)   // ע�� DbContext / �ִ�
    .AddRepositories()
    .AddApplication();                          // ע�� UseCase / Runner ��

// 4. CORS���ɰ� Origins �������ļ���
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001",
                "http://localhost:5000")   // ��ǰ�˶˿ڵ���
            .AllowAnyHeader()
            .AllowAnyMethod();
        // ��δ����Ҫ Cookie/���: .AllowCredentials();
    });
});

var app = builder.Build();

// 5. �Զ�Ǩ�ƣ���������ִ�У�
//    ���������Ƽ���������Ǩ�ƻ� CI/CD �ű�����Ҫֱ��������ִ�� Migrate()
using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (env.IsDevelopment())
    {
        // ȷ��ʹ�� migrations�����û��Ǩ�ƣ����״���ִ��: dotnet ef migrations add InitBattle��
        db.Database.Migrate();
    }
    else
    {
        // ��ѡ�������Ƿ������Զ�Ǩ��
        // db.Database.Migrate();
        // ������
        // if (db.Database.GetPendingMigrations().Any()) { /* ��־�澯 */ }
    }
}

// 6. �м��
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorClient");

// ��������������/��Ȩ���˴���˳����� UseAuthentication / UseAuthorization
app.MapControllers();

//���Ժ����� /health, /metrics��
app.Run();