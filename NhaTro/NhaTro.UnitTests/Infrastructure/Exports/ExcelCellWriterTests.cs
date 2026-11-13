using ClosedXML.Excel;
using Web.Infrastructure.Exports;

namespace NhaTro.UnitTests.Infrastructure.Exports;

public sealed class ExcelCellWriterTests
{
    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+1")]
    [InlineData("-2+3")]
    [InlineData("@cmd")]
    [InlineData("\tfoo")]
    public void WriteText_WritesPotentialFormulaAsText(string input)
    {
        using var workbook = new XLWorkbook();
        var cell = workbook.AddWorksheet().Cell(1, 1);

        ExcelCellWriter.WriteText(cell, input);

        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(input, cell.GetString());
    }

    [Fact]
    public void WriteNumberAndDate_KeepNativeExcelTypes()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet();

        ExcelCellWriter.WriteNumber(sheet.Cell(1, 1), 1234m);
        ExcelCellWriter.WriteDate(sheet.Cell(1, 2), new DateOnly(2026, 11, 12));

        Assert.Equal(XLDataType.Number, sheet.Cell(1, 1).DataType);
        Assert.Equal("#,##0", sheet.Cell(1, 1).Style.NumberFormat.Format);
        Assert.Equal(XLDataType.DateTime, sheet.Cell(1, 2).DataType);
        Assert.Equal("dd/MM/yyyy", sheet.Cell(1, 2).Style.NumberFormat.Format);
    }

    [Fact]
    public void WriteText_Null_WritesEmptyText()
    {
        using var workbook = new XLWorkbook();
        var cell = workbook.AddWorksheet().Cell(1, 1);

        ExcelCellWriter.WriteText(cell, null);

        Assert.False(cell.HasFormula);
        Assert.Equal(string.Empty, cell.GetString());
    }
}
