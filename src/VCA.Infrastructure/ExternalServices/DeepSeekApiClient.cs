using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VCA.Application.AI.Common;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Cliente robusto para a API DeepSeek (chat/completions com response_format JSON).
/// Polly cuida do retry/timeout no pipeline HTTP. Esta classe trata parsing, custo e logging estruturado.
/// </summary>
public sealed class DeepSeekApiClient : IAiCompletionClient
{
    public const string Model = "deepseek-chat";

    // DeepSeek V3: $0.27 / 1M input tokens, $1.10 / 1M output tokens (cache miss).
    private const decimal InputCostPerToken = 0.27m / 1_000_000m;
    private const decimal OutputCostPerToken = 1.10m / 1_000_000m;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<DeepSeekApiClient> _logger;

    public DeepSeekApiClient(HttpClient http, IConfiguration config, ILogger<DeepSeekApiClient> logger)
    {
        _http = http;
        _logger = logger;

        var apiKey = config["DeepSeek:ApiKey"]
            ?? throw new InvalidOperationException("DeepSeek:ApiKey não configurado em appsettings.");

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.deepseek.com/");
        if (!_http.DefaultRequestHeaders.Contains("Authorization"))
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<AiCompletionResult> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.7,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" },
            temperature,
            max_tokens = maxTokens
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("v1/chat/completions", requestBody, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DeepSeekApiException("Timeout ao chamar a API DeepSeek.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new DeepSeekApiException("Falha de rede ao chamar a API DeepSeek.", ex);
        }

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "DeepSeek API erro: status={Status} body={Body}",
                (int)response.StatusCode, Truncate(rawBody, 1000));

            // Erros 4xx (exceto 429) são logicamente irrecuperáveis — retry só se 5xx/timeout (Polly trata).
            throw new DeepSeekApiException(
                $"DeepSeek API retornou status {(int)response.StatusCode}.",
                statusCode: (int)response.StatusCode,
                errorBody: Truncate(rawBody, 2000));
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new DeepSeekApiException("DeepSeek API: resposta sem 'choices'.", errorBody: Truncate(rawBody, 1000));

            var content = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            int promptTokens = 0, completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct)) completionTokens = ct.GetInt32();
            }

            var cost = (promptTokens * InputCostPerToken) + (completionTokens * OutputCostPerToken);
            sw.Stop();

            _logger.LogInformation(
                "DeepSeek OK: model={Model} promptTokens={PT} completionTokens={CT} costUsd={Cost} elapsedMs={Elapsed}",
                Model, promptTokens, completionTokens, cost, sw.ElapsedMilliseconds);

            return new AiCompletionResult(content, Model, promptTokens, completionTokens, cost, sw.Elapsed);
        }
        catch (JsonException ex)
        {
            throw new DeepSeekApiException(
                "DeepSeek API: corpo da resposta não é JSON válido.",
                ex,
                errorBody: Truncate(rawBody, 1000));
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
