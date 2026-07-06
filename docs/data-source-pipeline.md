# 数据源管线

DataTables 运行时采用**传输层处理优先**的模型：`IDataSource` 及其装饰器负责网络、缓存、回退、解压和解密，进入 `DataTableManager` 的字节流应当已经是明文 DataTables payload。二进制 header 中的 flags 当前作为扩展位保留，运行时仍会拒绝非零 payload flags。

## Manifest 条目约定

`DataSourceManifestEntry` 用于描述可加载资源的稳定元数据：

| 字段 | 说明 |
| --- | --- |
| `Name` | 逻辑表名或资源名，不包含 `.bytes` 扩展名。 |
| `Length` | 资源字节长度；未知时可为空。 |
| `Version` | 单个资源版本；为空时可继承 manifest 级 `Version`。 |
| `Hash` | 资源内容 hash，推荐使用小写 hex SHA-256。 |
| `SourceName` | 提供该条目的数据源诊断名，主要由 fallback 合并 manifest 时填充。 |

## VersionedDataSource

`VersionedDataSource` 默认把逻辑名 `Hero` 映射到 `{version}/Hero` 加载，例如版本 `v2` 会加载 `v2/Hero.bytes`。读取 manifest 时会反向过滤当前版本前缀，只把 `v2/Hero`、`v2/Item` 暴露为逻辑名 `Hero`、`Item`，并把缺失的条目版本补为当前版本。

如果项目使用自定义路径规则，可以传入自定义 `nameResolver` 和 `manifestEntryMapper`，确保加载路径和 manifest 逻辑名保持一致。

## FallbackDataSource 诊断

`FallbackDataSource` 按顺序尝试每个数据源：

1. `ExistsAsync` 返回 `false` 时记录 `not found`。
2. 加载异常时记录对应数据源和错误消息。
3. 成功加载时更新 `LastHitSource`，方便日志和性能面板展示最终命中来源。
4. 合并 manifest 时保留第一个出现的逻辑名，并把来源写入 `SourceName`。

当所有数据源都失败时，抛出的 `FileNotFoundException` 会包含每个数据源的尝试结果，便于区分“本地缓存缺失”“远端不可用”和“资源损坏”。
