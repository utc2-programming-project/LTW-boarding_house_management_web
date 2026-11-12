using System.Globalization;
using System.Text;
using QRCoder;
using SkiaSharp;

namespace Web.Features.Invoices;

/// <summary>
/// Sinh mã QR chuyển khoản VietQR theo chuẩn EMV QR Code (giống cấu trúc
/// của repo tham chiếu: payload chuỗi + CRC16-CCITT, render bằng QRCoder + SkiaSharp).
/// Nội dung chuyển khoản phải là ASCII (giống mã đơn hàng/hóa đơn).
/// </summary>
public static class VietQr
{
    /// <summary>Tạo chuỗi payload VietQR có sẵn số tiền + nội dung + CRC.</summary>
    public static string GeneratePayload(string bankCode, string accountNumber, decimal amount, string message)
    {
        var bankId = VietQrBankCatalog.GetBin(bankCode);
        if (string.IsNullOrEmpty(bankId))
        {
            throw new ArgumentException($"Không tìm thấy mã ngân hàng cho: {bankCode}", nameof(bankCode));
        }

        var accountNumberTrimmed = accountNumber.Replace(" ", string.Empty);
        var integerAmount = decimal.Round(amount).ToString("0", CultureInfo.InvariantCulture);
        var content = message.Trim();

        // Field 38 (Merchant Account Information): GUID VTT + BIN/STK + QRIBFTTA.
        var bankAndAccount = Field("00", bankId) + Field("01", accountNumberTrimmed);
        var merchantInfo = "0010A000000727"
            + Field("01", bankAndAccount)
            + Field("02", "QRIBFTTA");
        var field38 = Field("38", merchantInfo);

        // Field 62 (Additional Data) — tag 08 là nội dung chuyển khoản.
        var additionalData = Field("08", content);

        var body = new StringBuilder()
            .Append("000201")
            .Append("010212")
            .Append(field38)
            .Append(Field("53", "704"))
            .Append(Field("54", integerAmount))
            .Append(Field("58", "VN"))
            .Append(Field("62", additionalData))
            .Append("6304");

        return body.Append(GenerateCheckSum(body.ToString()).ToUpperInvariant()).ToString();
    }

    /// <summary>Render payload thành ảnh PNG bytes (ECC M, màu đen/trắng chuẩn quét ngân hàng).</summary>
    public static byte[] GenerateImageBytes(string payload, int size = 300)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        byte[] qrCodeBytes = qrCode.GetGraphic(20);

        using var originalBitmap = SKBitmap.Decode(qrCodeBytes);
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(size, size), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("Không resize được ảnh QR.");
        using var customBitmap = new SKBitmap(size, size);
        SKColor[] pixels = customBitmap.Pixels;
        SKColor[] originalPixels = resizedBitmap.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            SKColor original = originalPixels[i];
            var gray = (original.Red + original.Green + original.Blue) / 3;
            pixels[i] = gray < 128 ? SKColors.Black : SKColors.White;
        }

        customBitmap.Pixels = pixels;
        canvas.DrawBitmap(customBitmap, 0, 0, SKSamplingOptions.Default);

        using SKImage image = surface.Snapshot();
        using SKData dataBytes = image.Encode(SKEncodedImageFormat.Png, 100);
        return dataBytes.ToArray();
    }

    public static string ImageDataUri(string payload, int size = 300) =>
        "data:image/png;base64," + Convert.ToBase64String(GenerateImageBytes(payload, size));

    private static string Field(string id, string value) =>
        id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

    /// <summary>CRC16-CCITT (poly 0x1021, init 0xFFFF) trên byte UTF-8 — ISO/IEC 13239.</summary>
    private static string GenerateCheckSum(string text)
    {
        ushort crc = 0xFFFF;
        const ushort polynomial = 0x1021;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ polynomial)
                    : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
