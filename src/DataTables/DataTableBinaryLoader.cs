using System;
using System.IO;

namespace DataTables
{
    internal static class DataTableBinaryLoader
    {
        public static DataTableBase Load(TypeNamePair typeNamePair, Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            var header = DataTableBinaryFormat.ReadHeader(reader);
            if (header.Signature != DataTableBinaryFormat.Signature)
            {
                throw new InvalidDataException($"Invalid data table file format for '{typeNamePair}'.");
            }
            if (header.Version != DataTableBinaryFormat.Version)
            {
                throw new InvalidDataException($"Unsupported data table version {header.Version} for '{typeNamePair}'. This major runtime requires binary format version {DataTableBinaryFormat.Version}; regenerate the .bytes data and generated code together.");
            }

            var dataTable = CreateDataTableInstance(typeNamePair, header.RowCount);
            ValidateHeader(typeNamePair, dataTable, header);
            if (IsMatrixTable(typeNamePair.Type))
            {
                for (var index = 0; index < header.RowCount; index++)
                {
                    if (!dataTable.ParseDataRow(index, reader))
                    {
                        throw new InvalidDataException($"Can not parse matrix table '{typeNamePair}' at index '{index}'.");
                    }
                }
            }
            else
            {
                for (var index = 0; index < header.RowCount; index++)
                {
                    var dataRow = CreateDataRowInstance(typeNamePair);
                    if (!dataRow.Deserialize(reader))
                    {
                        throw new InvalidDataException($"Can not parse data table '{typeNamePair}' at index '{index}'.");
                    }
                    dataTable.AddDataRow(index, dataRow);
                }
            }

            if (reader.BaseStream.ReadByte() != -1)
            {
                throw new InvalidDataException($"Data table '{typeNamePair}' contains trailing bytes not described by protocol version {DataTableBinaryFormat.Version}.");
            }
            return dataTable;
        }

        private static void ValidateHeader(TypeNamePair pair, DataTableBase table, DataTableBinaryHeaderData header)
        {
            if (header.Flags != DataTableBinaryFormat.FlagsNone)
            {
                throw new InvalidDataException($"Unsupported data table flags 0x{header.Flags:X8} for '{pair}'. This runtime cannot load compressed, encrypted, or extended payloads marked by these flags.");
            }
            var expectedFullName = pair.Type.FullName ?? pair.Type.ToString();
            if (!string.Equals(header.TableFullName, expectedFullName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Data table header mismatch for '{pair}': .bytes table '{header.TableFullName}' does not match generated code table '{expectedFullName}'. Regenerate code and data together.");
            }
            if (table.SchemaHash != header.SchemaHash)
            {
                throw new InvalidDataException($"Data table schema mismatch for '{pair}': generated code schema hash 0x{table.SchemaHash:X16} does not match .bytes data schema hash 0x{header.SchemaHash:X16}. The generated code and .bytes data are out of sync; regenerate both from the same source table.");
            }
        }

        private static bool IsMatrixTable(Type tableType)
        {
            var baseType = tableType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(DataMatrixBase<,,>)) return true;
                baseType = baseType.BaseType;
            }
            return false;
        }

        private static DataTableBase CreateDataTableInstance(TypeNamePair pair, int capacity)
        {
            var table = DataTableFactoryManager.CreateDataTable(pair.Type, pair.Name, capacity);
            if (table != null) return table;
            return (DataTableBase)Activator.CreateInstance(pair.Type, pair.Name, capacity)!;
        }

        private static DataRowBase CreateDataRowInstance(TypeNamePair pair)
        {
            var dataRow = DataTableFactoryManager.CreateDataRow(pair.Type);
            if (dataRow != null) return dataRow;
            var dataRowType = pair.Type.BaseType!.GetGenericArguments()[0];
            return (DataRowBase)Activator.CreateInstance(dataRowType)!;
        }
    }
}
