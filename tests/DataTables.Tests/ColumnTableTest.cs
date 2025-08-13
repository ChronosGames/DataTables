using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class ColumnTableTest
    {
        [Fact]
        public async Task Generate_And_Load_From_ColumnTable()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "dt_column_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var codeOut = Path.Combine(tempDir, "code");
            var dataOut = Path.Combine(tempDir, "data");
            Directory.CreateDirectory(codeOut);
            Directory.CreateDirectory(dataOut);

            // 在临时目录创建一个 column 布局的 xlsx
            var xlsxPath = Path.Combine(tempDir, "ColumnTable.Sample.xlsx");
            using (var wb = new XSSFWorkbook())
            {
                var sh = wb.CreateSheet("Sheet1");
                // 信息行
                var r0 = sh.CreateRow(0);
                r0.CreateCell(0, CellType.String).SetCellValue("dtgen=column, class=ItemConfig, title=Item Table");
                // 字段行1
                var r1 = sh.CreateRow(1);
                r1.CreateCell(0, CellType.String).SetCellValue("道具ID");
                r1.CreateCell(1, CellType.String).SetCellValue("Id");
                r1.CreateCell(2, CellType.String).SetCellValue("int");
                r1.CreateCell(3, CellType.String).SetCellValue("1001");
                r1.CreateCell(4, CellType.String).SetCellValue("1002");
                r1.CreateCell(5, CellType.String).SetCellValue("1003");
                // 字段行2
                var r2 = sh.CreateRow(2);
                r2.CreateCell(0, CellType.String).SetCellValue("名称");
                r2.CreateCell(1, CellType.String).SetCellValue("Name");
                r2.CreateCell(2, CellType.String).SetCellValue("string");
                r2.CreateCell(3, CellType.String).SetCellValue("Wooden Sword");
                r2.CreateCell(4, CellType.String).SetCellValue("Iron Sword");
                r2.CreateCell(5, CellType.String).SetCellValue("Steel Sword");
                // 字段行3
                var r3 = sh.CreateRow(3);
                r3.CreateCell(0, CellType.String).SetCellValue("稀有度");
                r3.CreateCell(1, CellType.String).SetCellValue("Rarity");
                r3.CreateCell(2, CellType.String).SetCellValue("int");
                r3.CreateCell(3, CellType.String).SetCellValue("1");
                r3.CreateCell(4, CellType.String).SetCellValue("2");
                r3.CreateCell(5, CellType.String).SetCellValue("3");

                using var fsx = File.Create(xlsxPath);
                wb.Write(fsx);
            }

            var generator = new DataTableGenerator();
            await generator.GenerateFile(
                inputDirectories: new[] { Path.GetDirectoryName(xlsxPath)! },
                searchPatterns: new[] { Path.GetFileName(xlsxPath) },
                codeOutputDir: codeOut,
                dataOutputDir: dataOut,
                usingNamespace: "DataTables.Tests.Generated",
                dataRowClassPrefix: string.Empty,
                importNamespaces: string.Empty,
                filterColumnTags: string.Empty,
                forceOverwrite: true,
                logger: _ => { }
            );

            // Assert data file exists
            // 类名 ItemConfig → DataTable 名称前缀 DT，输出文件 DataTables.Tests.Generated.DTItemConfig.bytes
            var dataFile = Directory.GetFiles(dataOut, "*.bytes", SearchOption.AllDirectories)
                                     .FirstOrDefault(p => Path.GetFileName(p).Contains("DTItemConfig"));
            dataFile.Should().NotBeNull();

            // 运行时加载验证：通过 DataTable 基础设施读取并计数（无需生成代码类）
            using var fs = File.OpenRead(dataFile!);
            using var br = new BinaryReader(fs);
            // 读取签名
            var signature = br.ReadString();
            signature.Should().Be("DTABLE");
            // 读取版本
            var version = br.ReadInt32();
            version.Should().BeGreaterThan(0);
            // 读取行数 7-bit 编码
            int rowCount = DataTables.BinaryExtension.Read7BitEncodedInt32(br);
            // 示例中：三条数据（1001/1002/1003）
            rowCount.Should().Be(3);
        }
    }
}

