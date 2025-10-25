# HttpClient 循环依赖问题解决方案

## 问题描述

在原始的依赖注入配置中，存在以下循环依赖：

```
HttpClient → AuthorizingHttpMessageHandler → IAuthenticationService (AuthenticationService) → HttpClient
```

### 详细说明

1. **AuthenticationService** 需要 `HttpClient` 来调用认证API（登录、注册、刷新令牌）
2. **AuthorizingHttpMessageHandler** 需要 `IAuthenticationService` 来获取Token并自动附加到请求头
3. **HttpClient** 使用 `AuthorizingHttpMessageHandler` 作为消息处理器

这形成了一个循环依赖链。

## 解决方案

使用 **命名HttpClient模式** 来打破循环依赖：

### 1. 添加依赖包

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
```

### 2. 配置两个不同的HttpClient

#### 未认证的HttpClient（用于认证服务）
- 名称：`UnauthenticatedHttpClient`
- 用途：登录、注册、刷新令牌等认证操作
- 特点：**不使用** `AuthorizingHttpMessageHandler`，因此不需要Token

```csharp
builder.Services.AddHttpClient("UnauthenticatedHttpClient", client =>
{
    client.BaseAddress = new Uri(apiBase);
});
```

#### 已认证的HttpClient（用于受保护的API）
- 名称：`AuthenticatedHttpClient`
- 用途：所有需要Token的API调用
- 特点：使用 `AuthorizingHttpMessageHandler` 自动附加Token

```csharp
builder.Services.AddHttpClient("AuthenticatedHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(apiBase);
    })
    .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();
```

### 3. 更新服务注册

#### AuthenticationService
使用未认证的HttpClient，打破循环依赖：

```csharp
builder.Services.AddScoped<IAuthenticationService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
    var localStorage = sp.GetRequiredService<ILocalStorageService>();
    var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
    
    return new AuthenticationService(httpClient, localStorage, logger);
});
```

#### 默认HttpClient（向后兼容）
为了保持向后兼容性，默认的HttpClient注入使用已认证的版本：

```csharp
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("AuthenticatedHttpClient");
});
```

## 依赖关系图

### 修复前（有循环依赖）
```
┌─────────────────┐
│   HttpClient    │◄───┐
└────────┬────────┘    │
         │             │
         ▼             │
┌─────────────────────────────┐
│AuthorizingHttpMessageHandler│
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────┐
│IAuthenticationService   │
│(AuthenticationService)  │
└────────┬────────────────┘
         │
         └───────────────────┘ (循环！)
```

### 修复后（无循环依赖）
```
┌──────────────────────────┐
│ IHttpClientFactory       │
└────┬─────────────┬───────┘
     │             │
     ▼             ▼
┌─────────────┐ ┌──────────────────────────┐
│Unauthenticated│ │Authenticated HttpClient  │
│HttpClient     │ │+ AuthorizingHttpMessageHandler│
└────┬──────────┘ └────────┬─────────────────┘
     │                     │
     ▼                     ▼
┌─────────────────────┐ ┌────────────┐
│AuthenticationService│ │ ApiClient  │
└─────────────────────┘ └────────────┘

AuthorizingHttpMessageHandler 使用 IAuthenticationService
但 AuthenticationService 不再依赖使用 Handler 的 HttpClient
```

## 测试验证

创建了 `CircularDependencyTests.cs` 来验证解决方案：

### 测试用例

1. **ServiceRegistration_ShouldNotHaveCircularDependency**
   - 验证所有服务可以成功注册和解析
   - 确认没有循环依赖异常

2. **AuthenticationService_ShouldUseUnauthenticatedHttpClient**
   - 验证 AuthenticationService 使用未认证的 HttpClient

3. **ApiClient_ShouldUseAuthenticatedHttpClient**
   - 验证 ApiClient 使用已认证的 HttpClient

4. **MultipleServiceInstances_ShouldBeCreatedSuccessfully**
   - 验证可以创建多个服务实例
   - 验证作用域服务的生命周期管理

### 测试结果
- ✅ 所有新测试通过（4/4）
- ✅ 所有现有认证测试通过（8/8）
- ✅ 总体测试套件：256个通过，7个预存在的失败（与本次修改无关）

## 优势

1. **解决循环依赖**：彻底消除了循环依赖问题
2. **关注点分离**：认证服务和受保护的API调用使用不同的HttpClient
3. **向后兼容**：默认的HttpClient注入保持不变，现有代码无需修改
4. **可测试性**：使用IHttpClientFactory使测试更容易
5. **可维护性**：清晰的依赖关系，易于理解和维护

## 使用说明

### 对于开发者

1. **认证操作**（登录、注册、刷新令牌）
   - 已自动配置，无需更改代码
   - AuthenticationService 内部使用未认证的HttpClient

2. **受保护的API调用**
   - 继续直接注入 `HttpClient`
   - 或使用 `ApiClient` 服务
   - Token会自动附加到请求头

### 示例代码

```csharp
// 在其他服务中使用（自动附加Token）
public class MyService
{
    private readonly HttpClient _httpClient;
    
    public MyService(HttpClient httpClient)
    {
        _httpClient = httpClient; // 这会得到已认证的HttpClient
    }
    
    public async Task<MyData> GetDataAsync()
    {
        // Token会自动附加
        return await _httpClient.GetFromJsonAsync<MyData>("/api/mydata");
    }
}
```

## 相关文件

- `BlazorIdle/Program.cs` - 依赖注入配置
- `BlazorIdle/Services/Auth/AuthenticationService.cs` - 认证服务
- `BlazorIdle/Services/Auth/AuthorizingHttpMessageHandler.cs` - HTTP消息处理器
- `tests/BlazorIdle.Tests/Auth/CircularDependencyTests.cs` - 循环依赖测试
- `tests/BlazorIdle.Tests/Auth/ClientAuthenticationServiceTests.cs` - 认证服务测试
