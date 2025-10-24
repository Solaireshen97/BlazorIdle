#!/bin/bash
# 开发环境启动脚本
# Development Environment Startup Script

echo "========================================"
echo "BlazorIdle 开发环境启动"
echo "Starting BlazorIdle Development Environment"
echo "========================================"
echo ""

# 检查 .NET 版本
echo "检查 .NET SDK 版本..."
echo "Checking .NET SDK version..."
dotnet --version
echo ""

# 启动后端服务器
echo "启动后端服务器 (Backend Server)..."
echo "后端将监听: https://localhost:7056 和 http://localhost:5056"
echo "Backend will listen on: https://localhost:7056 and http://localhost:5056"
cd BlazorIdle.Server
dotnet run --launch-profile https &
SERVER_PID=$!
cd ..

# 等待服务器启动
echo "等待后端服务器启动..."
echo "Waiting for backend server to start..."
sleep 8

# 启动前端
echo ""
echo "启动前端应用 (Frontend Application)..."
echo "前端将监听: http://localhost:5000"
echo "Frontend will listen on: http://localhost:5000"
cd BlazorIdle
dotnet run &
CLIENT_PID=$!
cd ..

echo ""
echo "========================================"
echo "✅ 应用已启动！"
echo "✅ Application started!"
echo ""
echo "前端地址 (Frontend): http://localhost:5000"
echo "后端地址 (Backend): https://localhost:7056"
echo "API 文档 (API Docs): https://localhost:7056/swagger"
echo ""
echo "按 Ctrl+C 停止所有服务"
echo "Press Ctrl+C to stop all services"
echo "========================================"

# 等待用户中断
wait
