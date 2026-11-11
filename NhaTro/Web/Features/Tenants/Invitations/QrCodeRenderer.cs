using QRCoder;

namespace Web.Features.Tenants.Invitations;

/// <summary>PLAN-024: sinh ảnh PNG từ link mời, không gọi dịch vụ ngoài.</summary>
public sealed class QrCodeRenderer
{
    public string ToPngDataUrl(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(8));
    }
}
