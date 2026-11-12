namespace Web.Features.Reports.DTOs;

/// <summary>Cac muc bao cao co the xuat Excel.</summary>
public enum ReportExportSection
{
    Revenue = 1,
    Rooms = 2,
    Tenants = 3,
    Contracts = 4,
    Costs = 5,
    Incidents = 6,
}

public static class ReportExportSectionExtensions
{
    public static string ToSheetName(this ReportExportSection section) => section switch
    {
        ReportExportSection.Revenue => "DoanhThu",
        ReportExportSection.Rooms => "Phong",
        ReportExportSection.Tenants => "KhachThue",
        ReportExportSection.Contracts => "HopDong",
        ReportExportSection.Costs => "ChiPhi",
        ReportExportSection.Incidents => "SuCo",
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };
}
