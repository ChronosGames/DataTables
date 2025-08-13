using FluentAssertions;
using Xunit;
using DataTables.GeneratorCore;

namespace DataTables.Tests;

public class CsvParserTests
{
	[Fact]
	public void RowTableParser_With_Csv_Should_Parse()
	{
		// header, comment/title, name, type, data...
		var csv = string.Join("\n", new[]
		{
			"dtgen=table,class=Cfg",
			"标题",
			"id",
			"int",
			"1"
		});
		var sheet = CsvSheetReader.FromString(csv);
		var ctx = new GenerationContext { SheetName = sheet.Name };
		var parser = new RowTableParser();
		var next = parser.Parse(sheet, ctx, new ParseOptions(), new DiagnosticsCollector());
		next.Should().Be(3 + 1);
		ctx.Fields.Should().NotBeEmpty();
		ctx.Fields[0].Name.Should().Be("id");
		ctx.Fields[0].TypeName.Should().Be("int");
	}

	[Fact]
	public void ColumnTableParser_With_Csv_Should_Parse()
	{
		// header, then field rows (Title,Name,Type)
		var csv = string.Join("\n", new[]
		{
			"dtgen=column,class=Cfg",
			"Id,id,int,1",
			"Name,name,string,Tom"
		});
		var sheet = CsvSheetReader.FromString(csv);
		var ctx = new GenerationContext { SheetName = sheet.Name };
		var parser = new ColumnTableParser();
		var next = parser.Parse(sheet, ctx, new ParseOptions(), new DiagnosticsCollector());
		next.Should().Be(1);
		ctx.Fields.Should().HaveCount(2);
		ctx.ColumnFirstDataColIndex.Should().Be(3);
	}

	[Fact]
	public void MatrixTableParser_With_Csv_Should_Skip_DefaultValues()
	{
		// dtgen=matrix with default value, header row contains key2, data rows contain key1 and value cells
		var csv = string.Join("\n", new[]
		{
			"dtgen=matrix,class=Mat,matrix=int&string&int,matrixdefaultvalue=0",
			",K1,,K3",  // 稀疏 Key2，包含空列
			"1,0,5",  // first value is default (skip), second is 5
			"2,3,0"   // first is 3, second default (skip)
		});
		var sheet = CsvSheetReader.FromString(csv);
		var ctx = new GenerationContext { SheetName = sheet.Name };
		var parser = new MatrixTableParser();
		var diags = new DiagnosticsCollector();
		parser.Parse(sheet, ctx, new ParseOptions(), diags).Should().Be(2);
		ctx.ColumnIndexToKey2.Should().ContainKey(1);
	}

	[Fact]
	public void ColumnTableParser_With_Csv_Should_Handle_EmptyRows_And_EmptyColumns()
	{
		var csv = string.Join("\n", new[]
		{
			"dtgen=column,class=Cfg",
			"Id,id,int",
			"",                // empty row should be ignored
			"Name,name,string,," // trailing empty cells should be ignored
		});
		var sheet = CsvSheetReader.FromString(csv);
		var ctx = new GenerationContext { SheetName = sheet.Name };
		var parser = new ColumnTableParser();
		parser.Parse(sheet, ctx, new ParseOptions(), new DiagnosticsCollector()).Should().Be(1);
		ctx.Fields.Should().HaveCount(2);
	}
}

