using System;
using System.Buffers;
using System.Diagnostics;
using DataTables;

namespace ConsoleApp
{
    public enum ColorT
    {
        Red,
        Green,
        Blue
    }

    class ByteBufferWriter : IBufferWriter<byte>
    {
        byte[] buffer;
        int index;

        public int CurrentOffset => index;
        public ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, index);
        public ReadOnlyMemory<byte> WrittenMemory => new ReadOnlyMemory<byte>(buffer, 0, index);

        public ByteBufferWriter()
        {
            buffer = new byte[1024];
            index = 0;
        }

        public void Advance(int count)
        {
            index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            AGAIN:
            var nextSize = index + sizeHint;
            if (buffer.Length < nextSize)
            {
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, nextSize));
            }

            if (sizeHint == 0)
            {
                var result = new Memory<byte>(buffer, index, buffer.Length - index);
                if (result.Length == 0)
                {
                    sizeHint = 1024;
                    goto AGAIN;
                }
                return result;
            }
            else
            {
                return new Memory<byte>(buffer, index, sizeHint);
            }
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return GetMemory(sizeHint).Span;
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            var manager = new DataTableManager();
            manager.SetDataTableHelper(new DefaultDataTableHelper("DataTables"));
            manager.Preload(() => Console.WriteLine("数据表全部加载完毕"));

            Debug.Assert(manager.HasDataTable<DTDataTableSample>(), "加载配置表失败");

            var dtSample = manager.GetDataTable<DTDataTableSample>();
            Debug.Assert(dtSample!.GetAllDataRows()[dtSample.Count - 1].CustomFieldType.Raw == "aaa");
            Debug.Assert(dtSample.GetDataRowById(1) != null, "加载配置表失败1");
            Debug.Assert(dtSample!.GetDataRowById(3).ArrayStringValue.Length == 2 && dtSample!.GetDataRowById(3).ArrayStringValue[0] == "a", "加载配置表失败2");
            manager.DestroyDataTable(dtSample);

            Debug.Assert(manager.GetDataTable<DTMatrixSample>() != null, "加载MatrixSample失败");
            Debug.Assert(manager.GetDataTable<DTMatrixSample>()!.Get(2, 1) == false, "加载MatrixSample失败");
            Debug.Assert(manager.GetDataTable<DTMatrixSample>()!.Get(5, 3) == true, "加载MatrixSample失败");
        }

    }


}


