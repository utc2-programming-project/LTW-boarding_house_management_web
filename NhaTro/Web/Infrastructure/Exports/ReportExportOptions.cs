namespace Web.Infrastructure.Exports;

/// <summary>Han muc de tranh tao tep Excel qua lon trong request web.</summary>
public sealed record ReportExportOptions(
    int MaxRowsPerSection = 5000,
    int MaxRowsPerFile = 20000,
    int PageChunk = 100)
{
    public void Validate()
    {
        if (MaxRowsPerSection < 1 || MaxRowsPerFile < MaxRowsPerSection
            || PageChunk is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ReportExportOptions));
        }
    }
}
