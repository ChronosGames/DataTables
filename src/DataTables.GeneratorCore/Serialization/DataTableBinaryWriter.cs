using System;
using System.IO;
using System.Text;
using DataTables;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal sealed class DataTableBinaryWriter : IDataTableBinaryWriter
{
    private readonly GenerationContext m_Context;
    private readonly Func<ISheet, BinaryWriter, int> m_WriteDataRows;

    public DataTableBinaryWriter(GenerationContext context, Func<ISheet, BinaryWriter, int> writeDataRows)
    {
        m_Context = context;
        m_WriteDataRows = writeDataRows;
    }

    public void GenerateDataFile(string outputDir, string comparisonOutputDir, bool forceOverwrite, ISheet sheet, ILogger logger)
    {
        int startTickCount = Environment.TickCount;
        string outputFileName = Path.Combine(outputDir, m_Context.GetDataOutputFilePath());
        string comparisonOutputFileName = Path.Combine(comparisonOutputDir, m_Context.GetDataOutputFilePath());

        try
        {
            using (var fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            using (var binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true))
            {
                long countPosition = DataTableBinaryFormat.WriteHeader(
                    binaryWriter,
                    DataTableSchemaHash.Compute(m_Context),
                    GetGeneratorVersion(),
                    m_Context.DataTableClassFullName);

                int dataRowCount = m_WriteDataRows(sheet, binaryWriter);

                DataTableBinaryFormat.PatchRowCount(binaryWriter, countPosition, dataRowCount);
            }

            if (!forceOverwrite && File.Exists(comparisonOutputFileName) && FilesEqual(outputFileName, comparisonOutputFileName))
            {
                File.Delete(outputFileName);
                m_Context.Skiped = true;
                logger.Debug("  > Generate {0}.bytes to: {1} (skipped) - {2}ms", m_Context.DataRowClassName, comparisonOutputFileName, Environment.TickCount - startTickCount);
                return;
            }

            logger.Debug("  > Generate {0}.bytes to: {1}. - {2}ms", m_Context.DataRowClassName, comparisonOutputFileName, Environment.TickCount - startTickCount);
        }
        catch (Exception exception)
        {
            logger.Error("  > Generate {0}.bytes failure, exception is '{1}'. - {2}ms", m_Context.DataRowClassName, exception, Environment.TickCount - startTickCount);
            Console.ResetColor();
            m_Context.Failed = true;
            m_Context.FailureException = exception;
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
        }
    }

    private static string GetGeneratorVersion()
    {
        return typeof(DataTableBinaryWriter).Assembly.GetName().Version?.ToString() ?? string.Empty;
    }

    private static bool FilesEqual(string left, string right)
    {
        return new FileInfo(left).Length == new FileInfo(right).Length
            && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
    }
}
