using System.Text.Json.Nodes;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;

namespace NhaTro.UnitTests.Features.Assistant;

public class AiOutputSchemasTests
{
    [Theory]
    [InlineData(AiUseCase.ReportSummary, "insights", 10)]
    [InlineData(AiUseCase.ReportSummary, "actions", 10)]
    [InlineData(AiUseCase.ReportSummary, "metrics", 12)]
    [InlineData(AiUseCase.RoomDraft, "rooms", 200)]
    [InlineData(AiUseCase.DailySummary, "items", 15)]
    [InlineData(AiUseCase.ContractDraft, "tenantIds", 10)]
    public void Schema_gioi_han_mang_giong_bo_kiem_duyet(AiUseCase useCase, string field, int maxItems)
    {
        var schema = JsonNode.Parse(AiOutputSchemas.Json(useCase))!;
        var array = schema["properties"]![field]!;
        Assert.Equal(maxItems, array["maxItems"]!.GetValue<int>());
    }
}
