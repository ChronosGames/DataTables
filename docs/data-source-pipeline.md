# 数据源管线

DataTables 运行时采用**传输层处理优先**的模型：`IDataSource` 及其装饰器负责网络、缓存、回退、解压和解密，进入 `DataTableManager` 的字节流应当已经是明文 DataTables payload。二进制 header 中的 flags 当前作为扩展位保留，运行时仍会拒绝非零 payload flags。

## Manifest 条目约定

`DataSourceManifestEntry` 用于描述可加载资源的稳定元数据：

| 字段 | 说明 |
| --- | --- |
| `Name` | 逻辑表名或资源名，不包含 `.bytes` 扩展名。 |
| `Length` | 资源字节长度；未知时可为空。 |
| `Version` | 单个资源版本；为空时可继承 manifest 级 `Version`。 |
| `Hash` | 资源内容 hash，固定为解码后的 DataTables payload 的 SHA-256 小写 hex（64 字符）。 |
| `SourceName` | 提供该条目的数据源诊断名，主要由 fallback 合并 manifest 时填充。 |

## HashValidatedDataSource

`HashValidatedDataSource` 是可选的 `IDataSource` 装饰器，用 manifest 条目的 `Hash` 在 `LoadAsync` 返回前校验 payload：

- **算法与大小写**：固定为 SHA-256 lowercase hex，长度 64；大写 hex 或其他长度会被视为 manifest 格式错误。
- **覆盖范围**：校验传入下一层运行时解析前的 DataTables payload。对于压缩/加密链路，应放在 `CompressedDataSource`、`EncryptedDataSource` 之后，以校验解压/解密后的明文字节；如需校验 CDN 原始传输包，可在外层另行增加传输包 hash。
- **校验时机**：`LoadAsync` 从内部数据源取到字节后、返回给 `DataTableManager` 解析前立即校验，便于在热更新或 CDN 截断/污染时提前失败。
- **未配置条目**：manifest 中没有对应 `Hash` 的资源会透传，不影响渐进接入。

示例：

```csharp
var manifest = await cdn.GetManifestAsync(CancellationToken.None);
IDataSource source = new HashValidatedDataSource(
    new CompressedDataSource(cdn),
    manifest);
```

## VersionedDataSource

`VersionedDataSource` 默认把逻辑名 `Hero` 映射到 `{version}/Hero` 加载，例如版本 `v2` 会加载 `v2/Hero.bytes`。读取 manifest 时会反向过滤当前版本前缀，只把 `v2/Hero`、`v2/Item` 暴露为逻辑名 `Hero`、`Item`，并把缺失的条目版本补为当前版本。

如果项目使用自定义路径规则，可以传入自定义 `nameResolver` 和 `manifestEntryMapper`，确保加载路径和 manifest 逻辑名保持一致。

## FallbackDataSource 诊断

`FallbackDataSource` 按顺序尝试每个数据源：

1. `ExistsAsync` 返回 `false` 时记录 `not found`。
2. 加载异常时记录对应数据源和错误消息。
3. 成功加载时更新 `LastHitSource` 并写入 info 日志，方便日志和性能面板展示最终命中来源。
4. 如果某个来源因为 hash 校验失败而抛出 `InvalidDataException`，会写入 warning 日志并继续尝试后续来源。
5. 合并 manifest 时保留第一个出现的逻辑名，并把来源写入 `SourceName`。

当所有数据源都失败时，抛出的 `FileNotFoundException` 会包含每个数据源的尝试结果，便于区分“本地缓存缺失”“远端不可用”和“资源损坏”。
