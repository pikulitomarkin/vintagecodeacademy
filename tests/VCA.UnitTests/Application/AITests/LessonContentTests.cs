using FluentAssertions;
using VCA.Domain.Common;
using VCA.Domain.ValueObjects;

namespace VCA.UnitTests.Application.AITests;

/// <summary>
/// Validação do JSON gerado pela IA (DeepSeek) — campos obrigatórios e rejeição de malformado.
/// </summary>
public class LessonContentTests
{
    private const string ValidJson = """
        {
          "mission": "Aprender a usar HTTP em .NET",
          "realContext": "Quase toda API moderna depende de chamadas HTTP eficientes.",
          "concept": "HttpClient é a forma idiomática de consumir APIs em .NET.",
          "quickChallenge": { "description": "Faça um GET em https://api.exemplo.com", "xp": 15 },
          "example": {
            "language": "csharp",
            "code": "var client = new HttpClient(); await client.GetAsync(\"...\");",
            "explanation": "Cria cliente e dispara requisição."
          },
          "summary": "• HttpClient\n• Async\n• REST",
          "xpReward": 30
        }
        """;

    [Fact]
    public void FromJson_ValidJson_DeserializesAllFields()
    {
        var content = LessonContent.FromJson(ValidJson);

        content.Mission.Should().Contain("HTTP");
        content.RealContext.Should().NotBeNullOrWhiteSpace();
        content.Concept.Should().NotBeNullOrWhiteSpace();
        content.QuickChallenge.Description.Should().Contain("GET");
        content.QuickChallenge.Xp.Should().Be(15);
        content.Example.Language.Should().Be("csharp");
        content.Example.Code.Should().Contain("HttpClient");
        content.XpReward.Should().Be(30);
    }

    [Fact]
    public void FromJson_RoundTrip_PreservesContent()
    {
        var original = LessonContent.FromJson(ValidJson);
        var json = original.ToJson();
        var parsed = LessonContent.FromJson(json);
        parsed.Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData("", "lesson_content.empty")]
    [InlineData("   ", "lesson_content.empty")]
    public void FromJson_EmptyInput_Throws(string json, string code)
    {
        var act = () => LessonContent.FromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == code);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        var act = () => LessonContent.FromJson("{ this is not valid json }");
        act.Should().Throw<DomainException>().Where(e => e.Code == "lesson_content.invalid_json");
    }

    [Fact]
    public void FromJson_MissingMission_Throws()
    {
        var json = ValidJson.Replace("\"mission\": \"Aprender a usar HTTP em .NET\",", "");
        var act = () => LessonContent.FromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == "lesson_content.mission_missing");
    }

    [Fact]
    public void FromJson_EmptyConcept_Throws()
    {
        var json = ValidJson.Replace(
            "\"concept\": \"HttpClient é a forma idiomática de consumir APIs em .NET.\"",
            "\"concept\": \"\"");
        var act = () => LessonContent.FromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == "lesson_content.concept_missing");
    }

    [Fact]
    public void FromJson_XpRewardOutOfRange_Throws()
    {
        var json = ValidJson.Replace("\"xpReward\": 30", "\"xpReward\": 5000");
        var act = () => LessonContent.FromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == "lesson_content.xp_invalid");
    }

    [Fact]
    public void QuizQuestion_ListFromJson_ValidArray_Deserializes()
    {
        var json = """
            [
              {
                "question": "Qual o tipo principal para chamadas HTTP em .NET?",
                "options": ["HttpClient", "WebClient", "RestSharp", "Axios"],
                "correctIndex": 0,
                "explanation": "HttpClient é o padrão atual.",
                "type": "conceptual"
              }
            ]
            """;

        var list = QuizQuestion.ListFromJson(json);
        list.Should().HaveCount(1);
        list[0].CorrectIndex.Should().Be(0);
        list[0].Options.Should().HaveCount(4);
    }

    [Fact]
    public void QuizQuestion_ListFromJson_RejectsThreeOptions()
    {
        var json = """[{"question":"q","options":["a","b","c"],"correctIndex":0,"explanation":"e"}]""";
        var act = () => QuizQuestion.ListFromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == "quiz.options_count");
    }

    [Fact]
    public void QuizQuestion_ListFromJson_RejectsInvalidCorrectIndex()
    {
        var json = """[{"question":"q","options":["a","b","c","d"],"correctIndex":7,"explanation":"e"}]""";
        var act = () => QuizQuestion.ListFromJson(json);
        act.Should().Throw<DomainException>().Where(e => e.Code == "quiz.correct_index");
    }

    [Fact]
    public void QuizQuestion_ListFromJson_AcceptsObjectWithQuestionsProperty()
    {
        var json = """
            { "questions": [
              {"question":"q","options":["a","b","c","d"],"correctIndex":1,"explanation":"e"}
            ]}
            """;
        var list = QuizQuestion.ListFromJson(json);
        list.Should().HaveCount(1);
        list[0].CorrectIndex.Should().Be(1);
    }
}
