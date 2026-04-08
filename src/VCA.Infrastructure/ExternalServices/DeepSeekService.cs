using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Integração com a API DeepSeek V3 para geração de conteúdo de aulas e quizzes.
/// </summary>
public class DeepSeekService : IDeepSeekService
{
    private const string Model = "deepseek-chat";
    // Custo aproximado DeepSeek V3: $0.27/1M input tokens, $1.10/1M output tokens
    private const decimal InputCostPerToken = 0.00000027m;
    private const decimal OutputCostPerToken = 0.0000011m;

    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekService> _logger;

    public DeepSeekService(HttpClient httpClient, IConfiguration config, ILogger<DeepSeekService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = config["DeepSeek:ApiKey"]
            ?? throw new InvalidOperationException("DeepSeek:ApiKey não configurado.");
        _httpClient.BaseAddress = new Uri("https://api.deepseek.com/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<DeepSeekGenerationResult> GenerateLessonContentAsync(
        string lessonTitle,
        IEnumerable<string> textChunks,
        CancellationToken cancellationToken = default)
    {
        var combinedText = string.Join("\n\n---\n\n", textChunks);
        var prompt = BuildLessonPrompt(lessonTitle, combinedText);
        return await CallApiAsync(prompt, cancellationToken);
    }

    public async Task<DeepSeekGenerationResult> GenerateQuizQuestionsAsync(
        string lessonTitle,
        string lessonContentJson,
        int questionCount = 10,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildQuizPrompt(lessonTitle, lessonContentJson, questionCount);
        return await CallApiAsync(prompt, cancellationToken);
    }

    private async Task<DeepSeekGenerationResult> CallApiAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = Model,
            messages = new[] { new { role = "user", content = prompt } },
            response_format = new { type = "json_object" },
            temperature = 0.7
        };

        var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        var usage = root.GetProperty("usage");
        var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var completionTokens = usage.GetProperty("completion_tokens").GetInt32();
        var cost = (promptTokens * InputCostPerToken) + (completionTokens * OutputCostPerToken);

        return new DeepSeekGenerationResult(content, Model, promptTokens, completionTokens, cost);
    }

    private static string BuildLessonPrompt(string title, string text)
    {
        return $"""
        Você é um instrutor especialista em programação. Gere uma aula gamificada em JSON para o título "{title}".

        Baseie-se no seguinte conteúdo extraído de um PDF:
        {text}

        Retorne APENAS um JSON válido com esta estrutura exata:
        {{
          "mission": "Descrição da missão/objetivo da aula",
          "realContext": "Contexto real de uso no mercado",
          "concept": "Explicação clara e concisa do conceito principal",
          "quickChallenge": "Um desafio rápido para praticar",
          "example": {{ "code": "código de exemplo", "language": "linguagem", "explanation": "explicação" }},
          "summary": "Resumo dos pontos principais",
          "xpReward": 10
        }}
        """;
    }

    private static string BuildQuizPrompt(string title, string contentJson, int count)
    {
        return $"""
        Gere {count} questões de múltipla escolha (4 opções) para a aula "{title}".

        Conteúdo da aula: {contentJson}

        Retorne APENAS um JSON válido com esta estrutura:
        [
          {{
            "question": "pergunta aqui",
            "options": ["opção A", "opção B", "opção C", "opção D"],
            "correctIndex": 0,
            "explanation": "explicação da resposta correta"
          }}
        ]
        """;
    }
}
