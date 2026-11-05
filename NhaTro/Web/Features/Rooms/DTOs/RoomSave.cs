using System.ComponentModel.DataAnnotations;
using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// DTO tao/sua phong (Phuc: form VHT-16).
// Gioi han khop DB: Code nvarchar(50) NOT NULL, AreaM2 decimal(10,2) NULL,
// StandardPrice decimal(18,2) NOT NULL, Status int NOT NULL.
// Trung (PropertyId, Code) voi phong chua xoa mem se do service 1C kiem (unique filter [IsDeleted]=0).
public sealed class RoomSave
{
    [Range(1, int.MaxValue)]
    public int PropertyId { get; set; }

    [Required(ErrorMessage = "Nhap ma phong.")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Range(0, 1000)]
    public int? Floor { get; set; }

    [Range(0, 100000)]
    public decimal? AreaM2 { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal StandardPrice { get; set; }

    [EnumDataType(typeof(RoomStatus))]
    public RoomStatus Status { get; set; } = RoomStatus.Available;

    public bool IsActive { get; set; } = true;

    public void Normalize()
    {
        Code = Code.Trim();
    }
}
