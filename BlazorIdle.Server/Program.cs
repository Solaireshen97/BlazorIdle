using BlazorIdle.Server.Application;
using BlazorIdle.Server.Infrastructure;
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
                "http://localhost:5001",  // HTTP �汾
                "http://localhost:5000")  // HTTP �汾
            .AllowAnyHeader()
            .AllowAnyMethod();
        // .AllowCredentials(); // ��δ��ʹ�� Cookie/��Ȩͷ����ҪЯ��ƾ��
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

// TODO����ѡ��չ����app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // ������������ͬ��������ֱ������ֹͣ��