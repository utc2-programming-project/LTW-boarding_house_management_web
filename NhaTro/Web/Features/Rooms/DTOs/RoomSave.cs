using System.ComponentModel.DataAnnotations;
using Web.Features.Rooms.Enums;
using Web.Resources;

namespace Web.Features.Rooms.DTOs;

// DTO tao/sua phong (Phuc: form VHT-16).
// Gioi han khop DB: Code nvarchar(50) NOT NULL, AreaM2 decimal(10,2) NULL,
// StandardPrice decimal(18,2) NOT NULL, Status int NOT NULL.
// Trung (PropertyId, Code) voi phong chua xoa mem se do service 1C kiem (unique filter [IsDeleted]=0).
public sealed class RoomSave
{
    [Range(1, int.MaxValue,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomPropertyRequired))]
    public int PropertyId { get; set; }

    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomCodeRequired))]
    [MaxLength(50,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomCodeMaxLength))]
    public string Code { get; set; } = string.Empty;

    [Range(0, 1000,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomFloorInvalid))]
    public int? Floor { get; set; }

    [Range(0, 100000,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomAreaInvalid))]
    public decimal? AreaM2 { get; set; }

    [Range(0, (double)decimal.MaxValue,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomPriceInvalid))]
    public decimal StandardPrice { get; set; }

    [EnumDataType(typeof(RoomStatus),
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.RoomStatusInvalid))]
    public RoomStatus Status { get; set; } = RoomStatus.Available;

    public bool IsActive { get; set; } = true;

    public void Normalize()
    {
        Code = Code.Trim();
    }
}
