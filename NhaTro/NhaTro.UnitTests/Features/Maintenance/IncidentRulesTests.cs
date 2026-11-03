using Web.Features.Maintenance;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Rules;

namespace NhaTro.UnitTests.Features.Maintenance;

public sealed class IncidentRulesTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void PendingCreationCannotDeclarePhotoCountOutsideReportQuota(int count)
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest, IncidentRules.ValidateCreate(ValidCreate() with { ExpectedPhotoCount = count }));
    }
    [Fact]
    public void CreateRequest_IsValidWhenRequiredFieldsAndEnumsAreValid()
    {
        Assert.Null(IncidentRules.ValidateCreate(ValidCreate()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRequest_RejectsInvalidRoomId(int roomId)
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateCreate(ValidCreate() with { RoomId = roomId }));
    }

    [Fact]
    public void CreateRequest_RejectsShortTitle()
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateCreate(ValidCreate() with { Title = "Leak" }));
    }

    [Fact]
    public void CreateRequest_RejectsShortDescription()
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateCreate(ValidCreate() with { Description = "Leak here" }));
    }

    [Fact]
    public void CreateRequest_RejectsUndefinedCategory()
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateCreate(ValidCreate() with { Category = (IncidentCategory)5 }));
    }

    [Fact]
    public void CreateRequest_RejectsUndefinedUrgency()
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateCreate(ValidCreate() with { Urgency = (IncidentUrgency)3 }));
    }

    [Theory]
    [InlineData(IncidentStatus.New, IncidentStatus.InProgress, true)]
    [InlineData(IncidentStatus.InProgress, IncidentStatus.Completed, true)]
    [InlineData(IncidentStatus.New, IncidentStatus.Completed, false)]
    [InlineData(IncidentStatus.InProgress, IncidentStatus.New, false)]
    [InlineData(IncidentStatus.Completed, IncidentStatus.Completed, false)]
    public void Transition_OnlyMovesOneStepForward(IncidentStatus current, IncidentStatus next, bool expected)
    {
        Assert.Equal(expected, IncidentRules.CanTransition(current, next));
    }

    [Fact]
    public void UpdateRequest_RejectsNegativeRepairCost()
    {
        Assert.Equal(IncidentErrorCodes.RepairCostInvalid,
            IncidentRules.ValidateUpdate(new IncidentUpdateRequest { Status = IncidentStatus.InProgress, RepairCost = -1 }));
    }

    [Fact]
    public void UpdateRequest_RejectsFractionalVndRepairCost()
    {
        Assert.Equal(IncidentErrorCodes.RepairCostInvalid,
            IncidentRules.ValidateUpdate(new IncidentUpdateRequest { Status = IncidentStatus.InProgress, RepairCost = 1.5m }));
    }

    [Fact]
    public void UpdateRequest_RejectsTechnicalNotesLongerThan2000Characters()
    {
        Assert.Equal(IncidentErrorCodes.InvalidRequest,
            IncidentRules.ValidateUpdate(new IncidentUpdateRequest
            {
                Status = IncidentStatus.InProgress,
                TechnicalNotes = new string('x', 2001),
            }));
    }

    private static IncidentCreateRequest ValidCreate() => new()
    {
        RoomId = 7,
        Title = "Water leakage",
        Description = "Water is leaking in the bathroom.",
        Category = IncidentCategory.Water,
        Urgency = IncidentUrgency.Normal,
    };
}
