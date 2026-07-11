using System;
using System.IO;

namespace DataTables
{
    internal readonly struct DataTableBinaryHeaderData
    {
        public DataTableBinaryHeaderData(string signature, int version, ulong schemaHash, string generatorVersion, string tableFullName, ushort rowCount, int flags)
        {
            Signature = signature;
            Version = version;
            SchemaHash = schemaHash;
            GeneratorVersion = generatorVersion;
            TableFullName = tableFullName;
            RowCount = rowCount;
            Flags = flags;
        }

        public string Signature { get; }
        public int Version { get; }
        public ulong SchemaHash { get; }
        public string GeneratorVersion { get; }
        public string TableFullName { get; }
        public ushort RowCount { get; }
        public int Flags { get; }
    }

    internal static class DataTableBinaryFormat
    {
        public const string Signature = "DTABLE";
        public const int Version = 3;
        public const int FlagsNone = 0;

        public static long WriteHeader(BinaryWriter writer, ulong schemaHash, string generatorVersion, string tableFullName, int flags = FlagsNone)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (generatorVersion == null) throw new ArgumentNullException(nameof(generatorVersion));
            if (string.IsNullOrWhiteSpace(tableFullName)) throw new ArgumentException("Table full name is required.", nameof(tableFullName));

            writer.Write(Signature);
            writer.Write(Version);
            writer.Write(schemaHash);
            writer.Write(generatorVersion);
            writer.Write(tableFullName);
            var rowCountPosition = writer.BaseStream.Position;
            writer.Write(ushort.MinValue);
            writer.Write(flags);
            return rowCountPosition;
        }

        public static void PatchRowCount(BinaryWriter writer, long rowCountPosition, int rowCount)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if ((uint)rowCount > ushort.MaxValue)
            {
                throw new InvalidDataException($"Data table row count {rowCount} exceeds the protocol limit of {ushort.MaxValue}.");
            }
            if (!writer.BaseStream.CanSeek)
            {
                throw new NotSupportedException("The binary output stream must support seeking to patch the row count.");
            }

            var returnPosition = writer.BaseStream.Position;
            writer.BaseStream.Seek(rowCountPosition, SeekOrigin.Begin);
            writer.Write((ushort)rowCount);
            writer.BaseStream.Seek(returnPosition, SeekOrigin.Begin);
        }

        public static DataTableBinaryHeaderData ReadHeader(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            try
            {
                return new DataTableBinaryHeaderData(
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadUInt64(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadUInt16(),
                    reader.ReadInt32());
            }
            catch (Exception exception) when (exception is EndOfStreamException or IOException or FormatException)
            {
                throw new InvalidDataException("The data table binary header is truncated or malformed.", exception);
            }
        }
    }
}
