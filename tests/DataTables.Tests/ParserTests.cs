using System;
using FluentAssertions;
using NPOI.XSSF.UserModel;
using Xunit;
using DataTables.GeneratorCore;

namespace DataTables.Tests;

public class ParserTests
{
	[Fact]
	public void RowTableParser_Should_Parse_Header_Name_Type()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=table,class=Hero");
		var r1 = sh.CreateRow(1); r1.CreateCell(0).SetCellValue("Id"); r1.CreateCell(1).SetCellValue("#行注释标志");
		var r2 = sh.CreateRow(2); r2.CreateCell(0).SetCellValue("id");
		var r3 = sh.CreateRow(3); r3.CreateCell(0).SetCellValue("int");

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new RowTableParser();
		var opts = new ParseOptions();
		var diags = new DiagnosticsCollector();
		var next = parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags);

		next.Should().Be(4);
		ctx.Fields.Should().HaveCountGreaterThan(0);
		ctx.Fields[0].Name.Should().Be("id");
		ctx.Fields[0].TypeName.Should().Be("int");
	}

	[Fact]
	public void ColumnTableParser_Should_Parse_Fields_And_Set_DataColIndex()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=column,class=Cfg");
		var r1 = sh.CreateRow(1); r1.CreateCell(0).SetCellValue("Id"); r1.CreateCell(1).SetCellValue("id"); r1.CreateCell(2).SetCellValue("int");
		var r2 = sh.CreateRow(2); r2.CreateCell(0).SetCellValue("Name"); r2.CreateCell(1).SetCellValue("name"); r2.CreateCell(2).SetCellValue("string");

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new ColumnTableParser();
		var opts = new ParseOptions();
		var diags = new DiagnosticsCollector();
		var next = parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags);

		next.Should().Be(1);
		ctx.ColumnFirstDataColIndex.Should().Be(3);
		ctx.Fields.Should().HaveCount(2);
		ctx.Fields[1].Name.Should().Be("name");
	}

	[Fact]
	public void MatrixTableParser_Should_Parse_Key2_Map()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=matrix,class=Mat,matrix=int&int&int");
		var r1 = sh.CreateRow(1); r1.CreateCell(1).SetCellValue("K1"); r1.CreateCell(3).SetCellValue("K3"); // 稀疏 Key2：跳过空列2

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new MatrixTableParser();
		var opts = new ParseOptions();
		var diags = new DiagnosticsCollector();
		var next = parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags);

		next.Should().Be(2);
		ctx.ColumnIndexToKey2.Should().ContainKey(1);
		ctx.ColumnIndexToKey2[1].Should().Be("K1");
		ctx.ColumnIndexToKey2.Should().ContainKey(3);
		ctx.ColumnIndexToKey2[3].Should().Be("K3");
	}

	[Fact]
	public void RowTableParser_Should_Handle_EmptyRows_And_LooseNames_When_StrictOff()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=table,class=Cfg");
		var r1 = sh.CreateRow(1); // empty row, should be skipped
		var r2 = sh.CreateRow(2); r2.CreateCell(0).SetCellValue("标题");
		var r3 = sh.CreateRow(3); r3.CreateCell(0).SetCellValue("9bad"); // invalid name
		var r4 = sh.CreateRow(4); r4.CreateCell(0).SetCellValue("int");

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new RowTableParser();
		var opts = new ParseOptions { StrictNameValidation = false };
		var diags = new DiagnosticsCollector();
		var next = parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags);
		next.Should().Be(5);
		ctx.Fields[0].Name.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void ColumnTableParser_Should_Skip_Columns_By_Comment_Row()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=column,class=Cfg");
		var r1 = sh.CreateRow(1); r1.CreateCell(0).SetCellValue("Id"); r1.CreateCell(1).SetCellValue("id"); r1.CreateCell(2).SetCellValue("int");
		var r2 = sh.CreateRow(2); r2.CreateCell(1).SetCellValue("#列注释标志");

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new ColumnTableParser();
		var opts = new ParseOptions();
		var diags = new DiagnosticsCollector();

		parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags).Should().Be(1);
		ctx.ColumnCommentRowIndex.Should().BeGreaterOrEqualTo(0);
	}

	[Fact]
	public void ColumnTableParser_Should_Filter_By_Tag_Boolean_Expression()
	{
		var wb = new XSSFWorkbook();
		var sh = wb.CreateSheet("Sheet1");
		var r0 = sh.CreateRow(0); r0.CreateCell(0).SetCellValue("dtgen=column,class=Cfg");
		// Title 含标签： A 列在 @CLIENT， B 列在 @SERVER， C 列在 @CLIENT 和 @SERVER
		var r1 = sh.CreateRow(1); r1.CreateCell(0).SetCellValue("Hp@C"); r1.CreateCell(1).SetCellValue("hp"); r1.CreateCell(2).SetCellValue("int");
		var r2 = sh.CreateRow(2); r2.CreateCell(0).SetCellValue("Md@S"); r2.CreateCell(1).SetCellValue("md"); r2.CreateCell(2).SetCellValue("int");
		var r3 = sh.CreateRow(3); r3.CreateCell(0).SetCellValue("Atk@CS"); r3.CreateCell(1).SetCellValue("atk"); r3.CreateCell(2).SetCellValue("int");

		var ctx = new GenerationContext { SheetName = "Sheet1" };
		var parser = new ColumnTableParser();
		var opts = new ParseOptions { FilterColumnTags = "C" };
		var diags = new DiagnosticsCollector();
		parser.Parse(new NpoiSheetReader(sh), ctx, opts, diags);

		// 预期：Hp 保留（C），Md 被过滤（S），Atk 保留（既有 C 又有 S）
		ctx.Fields.Should().HaveCount(3);
		ctx.Fields[0].IsIgnore.Should().BeFalse();
		ctx.Fields[1].IsTagFiltered.Should().BeTrue();
		ctx.Fields[2].IsTagFiltered.Should().BeFalse();
	}
}

