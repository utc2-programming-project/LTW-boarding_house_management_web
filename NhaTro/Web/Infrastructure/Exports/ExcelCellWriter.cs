using ClosedXML.Excel;
using Web.Infrastructure.Time;

namespace Web.Infrastructure.Exports;

/// <summary>Ghi gia tri Excel, khong bao gio ghi cong thuc tu du lieu nguoi dung.</summary>
public static class ExcelCellWriter
{
    public static void WriteText(IXLCell cell, string? value)
    {
        if (value is null)
        {
            cell.Clear();
            return;
        }

        cell.Value = value.Length <= 32767 ? value : value[..32767];
        cell.Style.NumberFormat.Format = "@";
    }

    public static void WriteNumber(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "#,##0";
    }

    public static void WriteDate(IXLCell cell, DateOnly value)
    {
        cell.Value = value.ToDateTime(TimeOnly.MinValue);
        cell.Style.NumberFormat.Format = "dd/MM/yyyy";
    }

    public static void WriteDateTime(IXLCell cell, DateTime utc)
    {
        cell.Value = VietnamTime.ToVietnam(utc);
        cell.Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
    }
}
