using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;

namespace Web.Features.Assistant.Services;

/// <summary>
/// Client chuẩn OpenAI /chat/completions với response_format=json_schema (OpenAI, Ollama,
/// vLLM… cùng hợp đồng). Chọn Demo khi chưa cấu hình BaseUrl hoặc Provider=Demo.
/// </summary>
public sealed class AiChatClient(
    IOptions<AiOptions> options,
    IHttpClientFactory httpClientFactory,
    DemoAiChatClient demo,
    ILogger<AiChatClient> logger) : IAiChatClient
{
    public const string HttpClientName = "assistant-ai";

    public async Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.Equals(settings.Provider, "Demo", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return demo.Complete(request);
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["temperature"] = 0.2,
            ["max_tokens"] = settings.MaxOutputTokens,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = request.SystemPrompt },
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = request.UserPrompt },
            },
            ["response_format"] = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["json_schema"] = new Dictionary<string, object?>
                {
                    ["name"] = AiOutputSchemas.Name(request.UseCase),
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(AiOutputSchemas.Json(request.UseCase)),
                },
            },
        };

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var message = new HttpRequestMessage(
                HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/chat/completions");
            message.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            }

            using var response = await client.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "AI provider trả {Status} khi xử lý {UseCase}.",
                    (int)response.StatusCode, request.UseCase);
                return AiChatResult.Failure(
                    Assistant.AiErrorCodes.ProviderFailed,
                    "Model AI không phản hồi hợp lệ. Vui lòng thử lại sau.",
                    settings.Model);
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0
                || !choices[0].TryGetProperty("message", out var messageNode)
                || !messageNode.TryGetProperty("content", out var contentNode)
                || contentNode.ValueKind != JsonValueKind.String)
            {
                return AiChatResult.Failure(
                    Assistant.AiErrorCodes.ProviderFailed,
                    "Model AI trả về nội dung trống.",
                    settings.Model);
            }

            var promptTokens = 0;
            var completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt)) promptTokens = prompt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completion)) completionTokens = completion.GetInt32();
            }

            var cost = (promptTokens + completionTokens) / 1000m * settings.CostPer1kTokens;
            return AiChatResult.Success(
                StripCodeFence(contentNode.GetString() ?? string.Empty),
                settings.Model,
                promptTokens,
                completionTokens,
                cost);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiChatResult.Failure(
                Assistant.AiErrorCodes.ProviderFailed,
                "Model AI phản hồi quá lâu. Vui lòng thử lại.",
                settings.Model);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning("Gọi AI provider thất bại cho {UseCase}: {Error}",
                request.UseCase, ex.GetType().Name);
            return AiChatResult.Failure(
                Assistant.AiErrorCodes.ProviderFailed,
                "Không kết nối được model AI. Đề xuất chưa được tạo.",
                settings.Model);
        }
    }

    private static string StripCodeFence(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return text;
        }

        var body = text[(firstLineEnd + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? body[..closing] : body).Trim();
    }
}
