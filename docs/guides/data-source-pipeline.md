# 数据源管线

DataTables 运行时采用传输层处理优先的模型：`IDataSource` 负责定位与读取资源，装饰器负责版本映射、解密、解压、hash 校验、fallback 和原始字节缓存。进入运行时二进制解析器的流必须已经是明文 DataTables payload；header 中的非零 payload flags 当前仍会被拒绝。

一个常见的安全组合顺序是：

```text
原始来源 -> 解密/解压 -> 该来源自己的 hash 校验
                                      \
原始来源 -> 解密/解压 -> 该来源自己的 hash 校验 -> Fallback -> 有界原始字节缓存 -> DataTableContext
```

装饰器按 C# 嵌套顺序由内向外提供数据。例如传输包先压缩、后加密时，读取侧可以使用 `new CompressedDataSource(new EncryptedDataSource(raw, key, iv))`，先解密再解压。

## Manifest 条目

`DataSourceManifestEntry` 描述单个可加载资源：

| 字段 | 契约 |
| --- | --- |
| `Name` | 逻辑表名或资源名，不包含 `.bytes` 扩展名。 |
| `Length` | 资源字节长度；未知时可为空。 |
| `Version` | 单资源版本；为空时可继承 manifest 级 `Version`。 |
| `Hash` | 解码后 DataTables payload 的 SHA-256 小写 hex，共 64 字符。 |
| `SourceName` | 提供条目的来源诊断名。fallback 合并 manifest 时会填充。 |

manifest 的 hash 描述逻辑 payload，不是 CDN 压缩包或加密包。如果还需要校验原始传输包，应在传输层单独维护另一个 hash，不能复用该字段。

## 每个来源独立校验 hash

`HashValidatedDataSource` 在流被读到末尾时完成 SHA-256 校验：

- hash 必须是 64 字符的小写 hex；大写或长度错误会抛出 `InvalidDataException`；
- manifest 没有对应 hash 的资源直接透传，便于渐进接入；
- 校验器应放在解密、解压装饰器之外，使其看到明文 payload；
- 校验失败发生在消费流时，而不一定发生在 `OpenReadAsync` 返回时。

fallback 场景必须让每个候选来源使用自己的 manifest 和校验器，然后再把候选来源交给 `FallbackDataSource`：

```csharp
var patchPayload = new CompressedDataSource(patchSource);
var packagePayload = new CompressedDataSource(packageSource);

var verifiedPatch = new HashValidatedDataSource(
    patchPayload,
    await patchPayload.GetManifestAsync(cancellationToken));
var verifiedPackage = new HashValidatedDataSource(
    packagePayload,
    await packagePayload.GetManifestAsync(cancellationToken));

IDataSource fallback = new FallbackDataSource(verifiedPatch, verifiedPackage);
```

不要先合并多个来源的 manifest，再在整个 fallback 外只放一个 `HashValidatedDataSource`。不同来源可能提供同名但不同版本的 payload，它们需要不同 hash。`FallbackDataSource.GetManifestAsync` 只在每个来源都提供相同非空 hash 时保留合并条目的 hash；只要来源缺项、缺 hash 或 hash 冲突，合并条目的 hash 就会被清空。因此合并 manifest 适合发现和诊断，不替代每来源校验。

## Fallback 的成功边界

`FallbackDataSource` 按构造顺序尝试来源。对每个候选来源，它会：

1. 调用 `ExistsAsync`；不存在时记录 `not found` 并继续。
2. 调用该来源的 `LoadAsync`，把返回流完整读取为 `byte[]`。
3. 只有完整读取、解码和 hash 校验全部成功后，才设置 `LastHitSource` 并返回只读 `MemoryStream`。
4. 除调用取消外，打开流、延迟 I/O、解密、解压、截断和 hash 错误都会记录后继续下一个来源。

完整缓冲是 fallback 的契约，而不是可选优化。它确保“流已经返回、读取到中途才失败”的候选来源仍能安全回退，不会把半份 payload 暴露给运行时。

当所有来源失败时，`FileNotFoundException` 会汇总每个来源的尝试结果。hash 失败还会写入 warning；成功来源会写入 info。合并 manifest 采用第一个出现的逻辑名，并把该来源写入 `SourceName`。

完整缓冲也意味着大 payload 会产生相应的瞬时 `byte[]`。后续运行时解析和外层缓存可能在短时间内持有其他副本，容量规划不能只看缓存预算。

## 有界原始字节缓存

`CachedDataSource` 缓存其内部来源完整物化后的 payload，使用按 payload 字节数计数的 LRU 预算：

- 默认预算是 64 MiB，也可通过构造函数显式传入 `maxCacheBytes`；
- `CachedBytes` 始终表示当前缓存中 payload 数组的字节合计；
- 插入新项前会淘汰最久未使用的项，使已缓存 payload 合计不超过预算；
- 单个 payload 大于预算时仍会返回给调用者，但不会进入缓存；
- `Clear()` 会清空条目与字节计数；
- 预算不包含字典、链表、`MemoryStream` 等对象开销，也不包含当前加载产生的瞬时副本。

把缓存放在 fallback 外面会缓存最终验证成功的 payload，通常是最直观的配置：

```csharp
IDataSource source = new CachedDataSource(
    new FallbackDataSource(verifiedPatch, verifiedPackage),
    maxCacheBytes: 32L * 1024 * 1024);

using var context = new DataTableContext(source);
var scenes = await context.LoadAsync<DTScene>(cancellationToken: cancellationToken);
```

如果把缓存放在单个来源内部，则每个来源应设置各自的预算，并确认缓存位置对应的是解码前还是解码后的字节。不要把 `CachedDataSource` 的字节预算与 `EnableEstimatedMemoryBudget` 混为一谈：前者缓存 payload `byte[]`，后者管理已解析数据表的估算 LRU 预算。

## VersionedDataSource

`VersionedDataSource` 默认把逻辑名 `Hero` 映射为 `{version}/Hero`。例如版本 `v2` 会加载 `v2/Hero.bytes`。读取 manifest 时，它只暴露当前版本前缀下的条目，把 `v2/Hero` 映射回逻辑名 `Hero`，并为缺失的条目版本补上当前版本。

自定义目录规则时，同时提供 `nameResolver` 和 `manifestEntryMapper`，保证实际加载路径、逻辑名和 hash 条目一致。版本映射通常应放在原始来源附近，使该来源的 manifest 与实际读取路径保持同一语义。

## 组合检查清单

- 运行时看到的是已经解密、解压的 DataTables payload。
- 每个 fallback 候选来源在进入 fallback 前使用自己的 manifest 做 hash 校验。
- fallback 只有在候选流完整消费成功后才宣布命中。
- 原始字节缓存配置明确的 byte budget，并考虑瞬时副本。
- 已解析表缓存使用 `EnableEstimatedMemoryBudget`，其数值是估算预算而非进程硬上限。
- 版本映射后的逻辑名与 manifest `Name` 一致。
