using System.Text.Json;
using System.Text.Json.Serialization;
using VCA.Domain.Common;

namespace VCA.Domain.ValueObjects;

/// <summary>
/// Value Object representando uma questão de quiz validada (4 alternativas, índice correto e explicação).
/// </summary>
public sealed record QuizQuestion(
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("options")] IReadOnlyList<string> Options,
    [property: JsonPropertyName("correctIndex")] int CorrectIndex,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("type")] string? Type = null)
{
    private static readonly JsonSerializerOptions Options_ = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<QuizQuestion> ListFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DomainException("Quiz JSON está vazio.", "quiz.empty");

        // Aceita tanto array direto quanto objeto { "questions": [...] }
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement arrayElement = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("questions", out var qs) => qs,
                _ => throw new DomainException("Quiz JSON deve ser um array ou objeto com a propriedade 'questions'.", "quiz.invalid_shape")
            };

            var list = JsonSerializer.Deserialize<List<QuizQuestion>>(arrayElement.GetRawText(), Options_)
                ?? throw new DomainException("Quiz desserializado como null.", "quiz.null");

            foreach (var q in list) q.Validate();
            return list;
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Quiz JSON inválido: {ex.Message}", ex, "quiz.invalid_json");
        }
    }

    public string OptionsToJson() => JsonSerializer.Serialize(Options, Options_);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Question))
            throw new DomainException("QuizQuestion.Question é obrigatório.", "quiz.question_missing");
        if (Options is null || Options.Count != 4)
            throw new DomainException($"QuizQuestion deve ter exatamente 4 alternativas. Recebido: {Options?.Count ?? 0}.", "quiz.options_count");
        if (Options.Any(string.IsNullOrWhiteSpace))
            throw new DomainException("Todas as alternativas devem ser não vazias.", "quiz.option_empty");
        if (CorrectIndex < 0 || CorrectIndex > 3)
            throw new DomainException($"QuizQuestion.CorrectIndex inválido (0-3): {CorrectIndex}.", "quiz.correct_index");
        if (string.IsNullOrWhiteSpace(Explanation))
            throw new DomainException("QuizQuestion.Explanation é obrigatório.", "quiz.explanation_missing");
    }
}
