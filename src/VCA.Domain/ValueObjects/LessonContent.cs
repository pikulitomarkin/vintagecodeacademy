using System.Text.Json;
using System.Text.Json.Serialization;
using VCA.Domain.Common;

namespace VCA.Domain.ValueObjects;

/// <summary>
/// Value Object representando o conteúdo gamificado completo de uma aula.
/// Estrutura: Missão → Contexto Real → Conceito → Desafio Rápido → Exemplo → Resumo + XP.
/// </summary>
public sealed record LessonContent(
    [property: JsonPropertyName("mission")] string Mission,
    [property: JsonPropertyName("realContext")] string RealContext,
    [property: JsonPropertyName("concept")] string Concept,
    [property: JsonPropertyName("quickChallenge")] QuickChallenge QuickChallenge,
    [property: JsonPropertyName("example")] CodeExample Example,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("xpReward")] int XpReward)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Desserializa um JSON em um LessonContent validado. Lança DomainException em caso de inválido.
    /// </summary>
    public static LessonContent FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DomainException("LessonContent JSON está vazio.", "lesson_content.empty");

        LessonContent? content;
        try
        {
            content = JsonSerializer.Deserialize<LessonContent>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new DomainException($"LessonContent JSON inválido: {ex.Message}", ex, "lesson_content.invalid_json");
        }

        if (content is null)
            throw new DomainException("LessonContent JSON desserializou para null.", "lesson_content.null");

        content.Validate();
        return content;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Mission))
            throw new DomainException("LessonContent.Mission é obrigatório.", "lesson_content.mission_missing");
        if (string.IsNullOrWhiteSpace(RealContext))
            throw new DomainException("LessonContent.RealContext é obrigatório.", "lesson_content.context_missing");
        if (string.IsNullOrWhiteSpace(Concept))
            throw new DomainException("LessonContent.Concept é obrigatório.", "lesson_content.concept_missing");
        if (QuickChallenge is null || string.IsNullOrWhiteSpace(QuickChallenge.Description))
            throw new DomainException("LessonContent.QuickChallenge é obrigatório.", "lesson_content.challenge_missing");
        if (Example is null || string.IsNullOrWhiteSpace(Example.Code))
            throw new DomainException("LessonContent.Example.Code é obrigatório.", "lesson_content.example_missing");
        if (string.IsNullOrWhiteSpace(Summary))
            throw new DomainException("LessonContent.Summary é obrigatório.", "lesson_content.summary_missing");
        if (XpReward < 0 || XpReward > 1000)
            throw new DomainException($"LessonContent.XpReward fora do intervalo (0-1000): {XpReward}.", "lesson_content.xp_invalid");
    }
}

public sealed record QuickChallenge(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("xp")] int Xp);

public sealed record CodeExample(
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("explanation")] string Explanation);
