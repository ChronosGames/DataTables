using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;

namespace Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            // run quickly:)
            //var baseConfig = Job.ShortRun.WithIterationCount(1).WithWarmupCount(1);

            // Add(baseConfig.With(Runtime.Clr).With(Jit.RyuJit).With(Platform.X64));
            // Add(baseConfig.With(Runtime.).With(Jit.RyuJit).With(Platform.X64));
            // Add(baseConfig.With(InProcessEmitToolchain.Instance));

            //AddExporter(MarkdownExporter.GitHub);
            AddExporter(CsvExporter.Default);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }

    [Config(typeof(BenchmarkConfig))]
    public class SimpleRun
    {
        DataMatrix1 m_Matrix1;
        DataMatrix2 m_Matrix2;

        public SimpleRun()
        {
            m_Matrix1 = new DataMatrix1("Matrix1");
            m_Matrix2 = new DataMatrix2("Matrix2");
        }

        [Benchmark(Baseline = true)]
        public void DataMatrixV1_FindKey1()
        {
            m_Matrix1.FindKey1(Key2Enum.Key21, ValueEnum.Value3);
        }

        [Benchmark]
        public void DataMatrixV2_FindKey1()
        {
            m_Matrix2.FindKey1(Key2Enum.Key21, ValueEnum.Value3);
        }

        [Benchmark]
        public void DataMatrixV1_FindKey2()
        {
            m_Matrix1.FindKey2(Key1Enum.Key09, ValueEnum.Value3);
        }

        [Benchmark]
        public void DataMatrixV2_FindKey2()
        {
            m_Matrix2.FindKey2(Key1Enum.Key09, ValueEnum.Value3);
        }
    }


    //public class SQLite_Test
    //{
    //    private string _filename;
    //    public SQLiteConnection _db;
    //    private int _count;

    //    public int Count { get { return _count; } }

    //    public SQLite_Test(int count, string password, bool journal, bool memory = false)
    //    {
    //        _count = count;
    //        _filename = "sqlite-" + Guid.NewGuid().ToString("n") + ".db";

    //        if (memory)
    //        {
    //            var cs = "Data Source=:memory:;New=True;";
    //            _db = new SQLiteConnection(cs);
    //        }
    //        else
    //        {
    //            var cs = "Data Source=" + _filename;
    //            if (password != null) cs += "; Password=" + password;
    //            if (journal == false) cs += "; Journal Mode=Off";
    //            _db = new SQLiteConnection(cs);
    //        }
    //    }

    //    public void Prepare()
    //    {
    //        _db.Open();

    //        var table = new SQLiteCommand("CREATE TABLE col (id INTEGER NOT NULL PRIMARY KEY, name TEXT, lorem TEXT)", _db);
    //        table.ExecuteNonQuery();

    //        var table2 = new SQLiteCommand("CREATE TABLE col_bulk (id INTEGER NOT NULL PRIMARY KEY, name TEXT, lorem TEXT)", _db);
    //        table2.ExecuteNonQuery();
    //    }

    //    public void CreateIndex()
    //    {
    //        var cmd = new SQLiteCommand("CREATE INDEX idx1 ON col (name)", _db);

    //        cmd.ExecuteNonQuery();
    //    }

    //    public void Query()
    //    {
    //        var cmd = new SQLiteCommand("SELECT * FROM col WHERE id = @id", _db);

    //        cmd.Parameters.Add(new SQLiteParameter("id", DbType.Int32));

    //        for (var i = 0; i < _count; i++)
    //        {
    //            cmd.Parameters["id"].Value = i;

    //            var r = cmd.ExecuteReader();

    //            r.Read();

    //            var name = r.GetString(1);
    //            var lorem = r.GetString(2);

    //            r.Close();
    //        }
    //    }


    //    public void Dispose()
    //    {
    //        _db.Dispose();
    //    }
    //}

    //class Dummy : IOptions<MemcachedClientOptions>
    //{
    //    public MemcachedClientOptions Value => new MemcachedClientOptions
    //    {
    //        Servers = new List<Server> { new Server { Address = "127.0.0.1", Port = 11211 } },
    //        Protocol = MemcachedProtocol.Binary,
    //        // Transcoder = new BinaryFormatterTranscoder()
    //    };
    //}

    //class LoggerDummy : ILoggerFactory
    //{
    //    public void AddProvider(ILoggerProvider provider)
    //    {
    //    }

    //    public ILogger CreateLogger(string categoryName)
    //    {
    //        return new NullLogger();
    //    }

    //    public void Dispose()
    //    {

    //    }
    //    class NullLogger : ILogger
    //    {
    //        public IDisposable BeginScope<TState>(TState state)
    //        {
    //            return new EmptyDisposable();
    //        }

    //        public bool IsEnabled(LogLevel logLevel)
    //        {
    //            return false;
    //        }

    //        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    //        {
    //        }

    //        class EmptyDisposable : IDisposable
    //        {
    //            public void Dispose()
    //            {
    //            }
    //        }
    //    }
    //}
}
