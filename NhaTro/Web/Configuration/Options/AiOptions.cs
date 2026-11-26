namespace Web.Configuration.Options;

/// <summary>
/// Cấu hình model cho Trợ lý AI. Provider=Demo trả đề xuất theo mẫu cục bộ (không gọi mạng)
/// để luồng duyệt/áp dụng chạy được khi chưa có model; OpenAICompatible gọi endpoint
/// /chat/completions với JSON Schema (OpenAI, Ollama, vLLM… cùng hợp đồng).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"Demo" hoặc "OpenAICompatible".</summary>
    public string Provider { get; set; } = "Demo";

    /// <summary>Ví dụ https://api.openai.com/v1 hoặc http://localhost:11434/v1.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 45;

    public int MaxOutputTokens { get; set; } = 1600;

    /// <summary>Hạn mức mặc định mỗi tài khoản/ngày khi Chủ trọ chưa lưu cài đặt.</summary>
    public int DefaultDailyLimit { get; set; } = 50;

    /// <summary>Chi phí ước tính trên 1.000 token để ghi AiUsageLog.</summary>
    public decimal CostPer1kTokens { get; set; }
}
