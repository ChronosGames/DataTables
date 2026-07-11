using System;
using System.IO;

namespace DataTables
{
    public readonly struct DataTableBinaryHeader
    {
        internal DataTableBinaryHeader(DataTableBinaryHeaderData header)
        {
            Signature = header.Signature;
            Version = header.Version;
            SchemaHash = header.SchemaHash;
            GeneratorVersion = header.GeneratorVersion;
            TableFullName = header.TableFullName;
            RowCount = header.RowCount;
            Flags = header.Flags;
        }

        public string Signature { get; }
        public int Version { get; }
        public ulong SchemaHash { get; }
        public string GeneratorVersion { get; }
        public string TableFullName { get; }
        public ushort RowCount { get; }
        public int Flags { get; }
    }

    public static class DataTableBinaryProtocol
    {
        public const string Signature = DataTableBinaryFormat.Signature;
        public const int Version = DataTableBinaryFormat.Version;
        public const int FlagsNone = DataTableBinaryFormat.FlagsNone;

        public static long WriteHeader(BinaryWriter writer, ulong schemaHash, string generatorVersion, string tableFullName, int flags = FlagsNone)
        {
            return DataTableBinaryFormat.WriteHeader(writer, schemaHash, generatorVersion, tableFullName, flags);
        }

        public static void PatchRowCount(BinaryWriter writer, long rowCountPosition, int rowCount)
        {
            DataTableBinaryFormat.PatchRowCount(writer, rowCountPosition, rowCount);
        }

        public static DataTableBinaryHeader ReadHeader(BinaryReader reader)
        {
            return new DataTableBinaryHeader(DataTableBinaryFormat.ReadHeader(reader));
        }
    }
}
