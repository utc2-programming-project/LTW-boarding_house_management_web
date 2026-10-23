namespace Web.Infrastructure.Security;

/// <summary>
/// Một thử thách CAPTCHA cần hiển thị cho người dùng.
/// <paramref name="Token"/> là dữ liệu đã mã hoá (không lộ đáp án) để server xác minh ở lần gửi sau.
/// </summary>
public sealed record CaptchaChallenge(string Question, string Token);
