using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal sealed class DataTableBinaryWriter : IDataTableBinaryWriter
{
    private const string DATA_TABLE_SIGNATURE = "DTABLE";
    private const int DATA_TABLE_VERSION = 2;

    private readonly GenerationContext m_Context;
    private readonly Func<ISheet, BinaryWriter, int> m_WriteDataRows;

    public DataTableBinaryWriter(GenerationContext context, Func<ISheet, BinaryWriter, int> writeDataRows)
    {
        m_Context = context;
        m_WriteDataRows = writeDataRows;
    }

    public void GenerateDataFile(string filePath, string outputDir, bool forceOverwrite, ISheet sheet, ILogger logger)
    {
        int startTickCount = Environment.TickCount;
        string outputFileName = Path.Combine(outputDir, m_Context.GetDataOutputFilePath());

        if (!forceOverwrite)
        {
            var processPath = Process.GetCurrentProcess().MainModule!.FileName;
            var processLastWriteTime = File.GetLastWriteTime(processPath);
            var excelLastWriteTime = File.GetLastWriteTime(filePath);

            if (File.Exists(outputFileName))
            {
                var dataLastWriteTime = File.GetLastWriteTime(outputFileName);
                if (dataLastWriteTime > excelLastWriteTime && dataLastWriteTime > processLastWriteTime)
                {
                    m_Context.Skiped = true;
                    logger.Debug("  > Generate {0}.bytes to: {1} (skiped) - {2}ms", m_Context.DataRowClassName, outputFileName, Environment.TickCount - startTickCount);
                    return;
                }
            }
        }

        try
        {
            using var fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            using var binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true);

            binaryWriter.Write(DATA_TABLE_SIGNATURE);
            binaryWriter.Write(DATA_TABLE_VERSION);
            binaryWriter.Write(ushort.MinValue);

            int dataRowCount = m_WriteDataRows(sheet, binaryWriter);

            byte[] signatureBytes = Encoding.UTF8.GetBytes(DATA_TABLE_SIGNATURE);
            long countPosition = DataTableProcessor.GetCompactIntSize(signatureBytes.Length) + signatureBytes.Length + sizeof(int);
            fileStream.Seek(countPosition, SeekOrigin.Begin);
            binaryWriter.Write((ushort)dataRowCount);

            logger.Debug("  > Generate {0}.bytes to: {1}. - {2}ms", m_Context.DataRowClassName, outputFileName, Environment.TickCount - startTickCount);
        }
        catch (Exception exception)
        {
            logger.Error("  > Generate {0}.bytes failure, exception is '{1}'. - {2}ms", m_Context.DataRowClassName, exception, Environment.TickCount - startTickCount);
            Console.ResetColor();
            m_Context.Failed = true;
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
        }
    }
}
