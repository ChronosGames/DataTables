using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DataTables;

namespace Benchmark
{
    [Config(typeof(BenchmarkConfig))]
    public class RuntimeLoadingBenchmarks
    {
        private const int RowCount = 1_024;
        private byte[] _payload = null!;
        private MemoryDataSource _source = null!;
        private DataTableContext _warmContext = null!;

        [GlobalSetup]
        public async Task Setup()
        {
            _payload = CreatePayload();
            _source = new MemoryDataSource(_payload);
            _warmContext = new DataTableContext(_source);
            await _warmContext.LoadAsync<RuntimeBenchmarkTable>();
        }

        [GlobalCleanup]
        public void Cleanup() => _warmContext.Dispose();

        [Benchmark(Description = "Cold context: stream parse 1,024 rows")]
        public async ValueTask<int> ColdStreamLoad()
        {
            using var context = new DataTableContext(_source);
            return (await context.LoadAsync<RuntimeBenchmarkTable>())!.Count;
        }

        [Benchmark(Description = "Warm context: cached LoadAsync")]
        public ValueTask<RuntimeBenchmarkTable?> WarmCachedLoad()
            => _warmContext.LoadAsync<RuntimeBenchmarkTable>();

        [Benchmark(Description = "Compatibility: materialize stream to byte[]")]
        public ValueTask<byte[]> CompatibilityByteMaterialization()
            => _source.LoadAsync(nameof(RuntimeBenchmarkTable), CancellationToken.None);

        private static byte[] CreatePayload()
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            var rowCountPosition = DataTableBinaryProtocol.WriteHeader(
                writer,
                RuntimeBenchmarkTable.ExpectedSchemaHash,
                "benchmark",
                typeof(RuntimeBenchmarkTable).FullName!);
            for (var index = 0; index < RowCount; index++)
            {
                writer.Write(index);
                writer.Write("benchmark-row-" + index);
            }
            DataTableBinaryProtocol.PatchRowCount(writer, rowCountPosition, RowCount);
            return output.ToArray();
        }

        private sealed class MemoryDataSource : IDataSource
        {
            private readonly byte[] _payload;

            public MemoryDataSource(byte[] payload)
            {
                _payload = payload;
            }

            public DataSourceType SourceType => DataSourceType.Memory;

            public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult<Stream>(new MemoryStream(_payload, writable: false));
            }

            public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

            public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

            public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
        }
    }

    public sealed class RuntimeBenchmarkTable : DataTable<RuntimeBenchmarkRow>
    {
        public const ulong ExpectedSchemaHash = 0x62B96872BC49D285UL;

        public RuntimeBenchmarkTable(string name, int capacity) : base(name, capacity)
        {
        }

        public override ulong SchemaHash => ExpectedSchemaHash;
    }

    public sealed class RuntimeBenchmarkRow : DataRowBase
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;

        public override bool Deserialize(BinaryReader binaryReader)
        {
            Id = binaryReader.ReadInt32();
            Name = binaryReader.ReadString();
            return true;
        }
    }
}
