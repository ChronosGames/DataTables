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
            manager.SetDataTableHelper(new DefaultDataTableHelper("Generated"));
            manager.Preload(() => Console.WriteLine("数据表全部加载完毕"));

            Debug.Assert(manager.HasDataTable<DTDataTableSample>(), "加载配置表失败");

            var card = manager.GetDataTable<DTDataTableSample>();
            Debug.Assert(card.GetDataRowById(1) != null, "加载配置表失败1");
            manager.DestroyDataTable(card);

            //Debug.Assert(!manager.HasDataTable<DTDataTableSplitSample>(), "加载配置表失败");
            //manager.CreateDataTable<DTDataTableSplitSample>();
            //Debug.Assert(manager.HasDataTable<DTDataTableSplitSample>(), "加载配置表失败");
        }

    }


}


