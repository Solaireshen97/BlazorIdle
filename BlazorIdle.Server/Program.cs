using BlazorIdle.Server.Application;
using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Infrastructure;
using BlazorIdle.Server.Services;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. ������ܷ���
// ���ӻ��ڿ������� API ֧�֣�����·�� / ������ / ģ�Ͱ󶨵ȣ�
builder.Services.AddControllers();

// 2. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer(); // Ϊ��С API / Controller ��������
builder.Services.AddSwaggerGen(options =>   // ���� swagger.json + UI�������ڵ����ã�
{
    // ����JWT��֤��֧��
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

// 4. ҵ��ֲ�ע��
builder.Services
    .AddInfrastructure(builder.Configuration)   // ע�������ʩ��DbContext / �ִ����ڲ��ѵ��� AddRepositories��
    .AddApplication();                          // ע��Ӧ�ò�����������Command/Query Handler �ȣ�

// 5. ע�����߼�⺧̨����
builder.Services.AddHostedService<OfflineDetectionService>();

// 6. CORS������
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

// 6. �Զ�Ǩ�ƣ���������
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

// 7. �м������
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseHttpsRedirection();      // ǿ���ض����� HTTPS��ȷ�� swagger Ҳ��ͨ�� https ���ʣ�
app.UseCors("AllowBlazorClient"); // ������ MapControllers ֮ǰ��������֤/��Ȩǰ������еĻ���

// �����������������֤��˳��ͨ���� UseAuthentication -> UseAuthorization
app.UseAuthentication(); // ��֤�м��
app.UseAuthorization();  // ��Ȩ�м��

app.MapControllers(); // ӳ��������˵㵽·�ɱ�

// TODO����ѡ��չ����app.MapHealthChecks("/health"); app.MapGet("/version", ...);
app.Run(); // ������������ͬ��������ֱ������ֹͣ��