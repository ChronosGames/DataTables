using MasterMemory;
using System.Linq;
using MessagePack;
using System;
using System.IO;
using System.Buffers;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Globalization;
using DataTables;

namespace ConsoleApp
{
    public enum CardType
    {

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

    [MemoryTable(nameof(Test1))]
    public class Test1
    {
        [PrimaryKey]
        public int Id { get; set; }
    }

    [MessagePackObject(false)]
    [MemoryTable(nameof(Test2))]
    public class Test2
    {
        [PrimaryKey]
        public int Id { get; set; }
    }



    class Program
    {
        static void Main(string[] args)
        {
            var manager = new DataTableManager();

            var raw = File.ReadAllBytes("card.bin");
            manager.CreateDataTable<DRCard>("card", raw, 0, raw.Length);

            manager.HasDataTable<DRCard>();

        }

    }


}


