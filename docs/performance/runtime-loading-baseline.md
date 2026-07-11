# Runtime loading baseline

本基线用于跟踪 `DataTableContext` 流式加载主路径，不作为单元测试中的硬时间门槛。提交性能优化时，应在同一机器、同一 runtime 和相同电源计划下重复运行，并比较 BenchmarkDotNet 原始报告。

## 复现

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet run --project sandbox/Benchmark/Benchmark.csproj -c Release -- --filter "*RuntimeLoadingBenchmarks*"
```

基准固定使用 BenchmarkDotNet `ShortRun`：1 次 launch、3 次 warmup、3 次 measurement。报告输出到 `BenchmarkDotNet.Artifacts/results/`。

## 2026-07-12 基线

- Windows 10 22H2，AMD Ryzen 7 3700X，8 核 / 16 线程
- .NET SDK 10.0.109
- 项目目标 `net8.0`；本机未安装 .NET 8 runtime，本次通过 `DOTNET_ROLL_FORWARD=Major` 在 .NET 9.0.6、X64 RyuJIT AVX2 上执行
- BenchmarkDotNet 0.13.10，Concurrent Workstation GC，高性能电源计划
- Payload：1,024 行，每行包含一个 `Int32` 和一个字符串

| 场景 | Mean | Allocated |
|---|---:|---:|
| 冷上下文：打开流、解析并实例化 1,024 行、释放上下文 | 199.452 μs | 138,623 B |
| 热上下文：`LoadAsync` 缓存命中 | 76.719 ns | 0 B |
| 兼容扩展：将同一输入流完整复制为 `byte[]` | 5.436 μs | 45,200 B |

“兼容扩展”只度量完整 payload 物化成本，不包含协议解析，因此不能与冷加载耗时直接比较；它用于观察旧 `LoadAsync` 字节数组适配层的额外分配趋势。
