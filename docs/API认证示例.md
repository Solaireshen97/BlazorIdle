# API 认证使用示例

本文档提供 BlazorIdle API 认证系统的实际使用示例。

## 前提条件

- 服务器运行在 `http://localhost:5000` 或 `https://localhost:5001`
- 安装了 `curl` 工具（用于命令行测试）

## 示例 1: 用户注册

### 请求
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "myusername",
    "email": "user@example.com",
    "password": "MySecurePass123"
  }'
```

### 成功响应 (200 OK)
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI3ZjhhNjk4Ny0zMjE0LTQ1YjAtOGU4Zi0zZjE2NDVhNTY4YTIiLCJ1bmlxdWVfbmFtZSI6Im15dXNlcm5hbWUiLCJlbWFpbCI6InVzZXJAZXhhbXBsZS5jb20iLCJqdGkiOiJkYzA5NmVhOS0yYWEzLTRkMTQtYjU5Zi05ZWUyZTMyODg3YzQiLCJleHAiOjE3MDQ3MjE0NzYsImlzcyI6IkJsYXpvcklkbGUuU2VydmVyIiwiYXVkIjoiQmxhem9ySWRsZS5DbGllbnQifQ.VF8KI5CcJ_Y9T8sY6cE4W5zVxE8QlLgTJxH1UDmrwIk",
  "userId": "7f8a6987-3214-45b0-8e8f-3f1645a568a2",
  "username": "myusername",
  "email": "user@example.com"
}
```

### 错误响应
```json
// 400 Bad Request - 用户名已存在
{
  "message": "用户名已存在"
}

// 400 Bad Request - 邮箱已存在
{
  "message": "邮箱已被注册"
}
```

## 示例 2: 用户登录

### 请求（使用用户名）
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "usernameOrEmail": "myusername",
    "password": "MySecurePass123"
  }'
```

### 请求（使用邮箱）
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "usernameOrEmail": "user@example.com",
    "password": "MySecurePass123"
  }'
```

### 成功响应 (200 OK)
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "7f8a6987-3214-45b0-8e8f-3f1645a568a2",
  "username": "myusername",
  "email": "user@example.com"
}
```

### 错误响应
```json
// 401 Unauthorized - 认证失败
{
  "message": "用户名或密码错误"
}
```

## 示例 3: 获取当前用户信息

### 请求
```bash
# 从登录响应中获取 token
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X GET http://localhost:5000/api/users/me \
  -H "Authorization: Bearer $TOKEN"
```

### 成功响应 (200 OK)
```json
{
  "id": "7f8a6987-3214-45b0-8e8f-3f1645a568a2",
  "username": "myusername",
  "email": "user@example.com",
  "createdAt": "2024-01-01T10:00:00Z",
  "lastLoginAt": "2024-01-02T14:30:00Z",
  "characters": [
    {
      "id": "a1b2c3d4-5678-90ab-cdef-123456789abc",
      "name": "MyWarrior",
      "level": 5,
      "profession": "Warrior"
    },
    {
      "id": "b2c3d4e5-6789-01bc-def1-23456789abcd",
      "name": "MyRanger",
      "level": 3,
      "profession": "Ranger"
    }
  ]
}
```

## 示例 4: 创建角色（自动绑定到用户）

### 请求（已认证用户）
```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X POST http://localhost:5000/api/characters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "MyNewWarrior",
    "profession": "Warrior"
  }'
```

**说明**: 角色会自动绑定到认证用户，并分配 RosterOrder

### 请求（未认证用户）
```bash
curl -X POST http://localhost:5000/api/characters \
  -H "Content-Type: application/json" \
  -d '{
    "name": "UnboundCharacter",
    "profession": "Ranger"
  }'
```

**说明**: 角色创建成功，但不会绑定到任何用户（UserId 为 NULL）

### 成功响应 (200 OK)
```json
{
  "id": "c3d4e5f6-7890-12cd-ef12-3456789abcde",
  "name": "MyNewWarrior"
}
```

## 示例 5: 绑定现有角色到用户

### 请求
```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
CHARACTER_ID="c3d4e5f6-7890-12cd-ef12-3456789abcde"

curl -X PUT "http://localhost:5000/api/characters/$CHARACTER_ID/bind-user" \
  -H "Authorization: Bearer $TOKEN"
```

### 成功响应 (200 OK)
```json
{
  "message": "角色绑定成功"
}
```

### 错误响应
```json
// 400 Bad Request - 角色已绑定
{
  "message": "角色已绑定到其他用户"
}

// 404 Not Found - 角色不存在
{
  "message": "角色不存在"
}
```

## 示例 6: 调整角色顺序

### 请求
```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
CHARACTER_ID="a1b2c3d4-5678-90ab-cdef-123456789abc"

curl -X PUT "http://localhost:5000/api/characters/$CHARACTER_ID/reorder" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "rosterOrder": 0
  }'
```

### 成功响应 (200 OK)
```json
{
  "message": "角色顺序调整成功"
}
```

## 示例 7: 修改密码

### 请求
```bash
curl -X POST http://localhost:5000/api/auth/change-password \
  -H "Content-Type: application/json" \
  -d '{
    "username": "myusername",
    "oldPassword": "MySecurePass123",
    "newPassword": "MyNewSecurePass456"
  }'
```

### 成功响应 (200 OK)
```json
{
  "message": "密码修改成功"
}
```

### 错误响应
```json
// 404 Not Found - 用户不存在
{
  "message": "用户不存在"
}

// 400 Bad Request - 旧密码错误
{
  "message": "旧密码错误"
}
```

## 示例 8: 获取用户的所有角色

### 请求
```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
USER_ID="7f8a6987-3214-45b0-8e8f-3f1645a568a2"

curl -X GET "http://localhost:5000/api/users/$USER_ID/characters" \
  -H "Authorization: Bearer $TOKEN"
```

### 成功响应 (200 OK)
```json
[
  {
    "id": "a1b2c3d4-5678-90ab-cdef-123456789abc",
    "name": "MyWarrior",
    "level": 5,
    "profession": "Warrior",
    "rosterOrder": 0,
    "createdAt": "2024-01-01T10:00:00Z"
  },
  {
    "id": "b2c3d4e5-6789-01bc-def1-23456789abcd",
    "name": "MyRanger",
    "level": 3,
    "profession": "Ranger",
    "rosterOrder": 1,
    "createdAt": "2024-01-01T11:00:00Z"
  }
]
```

**说明**: 角色按 rosterOrder 排序返回

## 完整工作流示例

### 完整的用户注册和创建角色流程
```bash
#!/bin/bash

# 1. 注册用户
echo "1. 注册用户..."
REGISTER_RESPONSE=$(curl -s -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "player1",
    "email": "player1@game.com",
    "password": "GamePass123"
  }')

TOKEN=$(echo $REGISTER_RESPONSE | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
USER_ID=$(echo $REGISTER_RESPONSE | grep -o '"userId":"[^"]*"' | cut -d'"' -f4)

echo "Token: $TOKEN"
echo "User ID: $USER_ID"

# 2. 创建第一个角色（自动绑定）
echo -e "\n2. 创建第一个角色..."
curl -s -X POST http://localhost:5000/api/characters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "WarriorHero",
    "profession": "Warrior"
  }'

# 3. 创建第二个角色（自动绑定）
echo -e "\n3. 创建第二个角色..."
curl -s -X POST http://localhost:5000/api/characters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "RangerHero",
    "profession": "Ranger"
  }'

# 4. 查看用户的所有角色
echo -e "\n4. 查看用户的所有角色..."
curl -s -X GET "http://localhost:5000/api/users/$USER_ID/characters" \
  -H "Authorization: Bearer $TOKEN"

# 5. 查看用户信息
echo -e "\n5. 查看用户信息..."
curl -s -X GET http://localhost:5000/api/users/me \
  -H "Authorization: Bearer $TOKEN"
```

## 使用 JavaScript 的示例

### 浏览器中使用 Fetch API
```javascript
// 注册用户
async function registerUser(username, email, password) {
  const response = await fetch('http://localhost:5000/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, email, password })
  });
  
  if (response.ok) {
    const data = await response.json();
    localStorage.setItem('jwt_token', data.token);
    localStorage.setItem('user_id', data.userId);
    return data;
  } else {
    const error = await response.json();
    throw new Error(error.message);
  }
}

// 登录
async function login(usernameOrEmail, password) {
  const response = await fetch('http://localhost:5000/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ usernameOrEmail, password })
  });
  
  if (response.ok) {
    const data = await response.json();
    localStorage.setItem('jwt_token', data.token);
    localStorage.setItem('user_id', data.userId);
    return data;
  } else {
    const error = await response.json();
    throw new Error(error.message);
  }
}

// 获取当前用户信息
async function getCurrentUser() {
  const token = localStorage.getItem('jwt_token');
  const response = await fetch('http://localhost:5000/api/users/me', {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  if (response.ok) {
    return await response.json();
  } else if (response.status === 401) {
    // Token 过期，需要重新登录
    localStorage.removeItem('jwt_token');
    localStorage.removeItem('user_id');
    throw new Error('认证已过期，请重新登录');
  }
}

// 创建角色
async function createCharacter(name, profession) {
  const token = localStorage.getItem('jwt_token');
  const response = await fetch('http://localhost:5000/api/characters', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({ name, profession })
  });
  
  if (response.ok) {
    return await response.json();
  }
}

// 使用示例
(async () => {
  try {
    // 注册
    await registerUser('player1', 'player1@game.com', 'GamePass123');
    
    // 获取用户信息
    const user = await getCurrentUser();
    console.log('当前用户:', user);
    
    // 创建角色
    const character = await createCharacter('MyWarrior', 'Warrior');
    console.log('创建的角色:', character);
  } catch (error) {
    console.error('错误:', error.message);
  }
})();
```

## 常见错误处理

### 1. 401 Unauthorized - Token 无效或过期
```javascript
if (response.status === 401) {
  // 清除本地 token
  localStorage.removeItem('jwt_token');
  // 重定向到登录页面
  window.location.href = '/login';
}
```

### 2. 403 Forbidden - 权限不足
```javascript
if (response.status === 403) {
  alert('您没有权限执行此操作');
}
```

### 3. 400 Bad Request - 请求参数错误
```javascript
if (response.status === 400) {
  const error = await response.json();
  console.error('请求错误:', error.message);
}
```

## 相关文档

- 📘 [JWT认证系统文档](./JWT认证系统文档.md)
- 📦 [用户系统文档](./用户系统文档.md)
- 🚀 [用户系统快速开始](./用户系统快速开始.md)
