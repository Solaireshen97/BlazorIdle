# SignalR 配置文件说明

本目录包含 SignalR 系统的配置文件。

## 文件结构

- `signalr-config.json` - 基础配置文件
- `signalr-config.Development.json` - 开发环境覆盖配置
- `signalr-config.Production.json` - 生产环境覆盖配置
- `signalr-config.schema.json` - JSON Schema 定义（可选）

## 配置优先级

配置按以下优先级合并：
1. 基础配置 (`signalr-config.json`)
2. 环境特定配置 (`signalr-config.{Environment}.json`)
3. 环境变量
4. 命令行参数

## 配置项说明

详细配置说明请参考：
- `/docs/SignalR配置优化指南.md`
- `/docs/SignalR性能优化指南.md`

## 最佳实践

1. **开发环境**：启用详细日志和较短的节流窗口便于调试
2. **生产环境**：禁用详细日志，使用标准节流窗口以优化性能
3. **测试环境**：可临时禁用 SignalR 或特定通知类型进行测试

## 验证

启动应用时会自动验证配置，验证失败将终止启动。

手动验证配置：
```bash
dotnet run --no-build -- --validate-config
```
