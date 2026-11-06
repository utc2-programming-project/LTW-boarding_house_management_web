using System.ComponentModel.DataAnnotations;
using Web.Resources;

namespace Web.Features.Properties.DTOs;

// DTO tao/sua nha tro (Phuc: form VHT-16).
// OwnerId KHONG nhan tu client: service lay tu user hien tai + PropertyScope (VHT-6).
// Gioi han khop DB: Name nvarchar(200) NOT NULL, Address nvarchar(500) NULL.
public sealed class PropertySave
{
    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.PropertyNameRequired))]
    [MaxLength(200,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.PropertyNameMaxLength))]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.PropertyAddressMaxLength))]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public void Normalize()
    {
        Name = Name.Trim();
        Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
    }
}
