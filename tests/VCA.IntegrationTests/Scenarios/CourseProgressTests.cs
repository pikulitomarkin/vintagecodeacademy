using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;
using VCA.Domain.ValueObjects;
using VCA.IntegrationTests.Infrastructure;

namespace VCA.IntegrationTests.Scenarios;

/// <summary>
/// Fluxo completo de progresso do aluno:
///   - Acessa aula → completa → recebe XP → nível atualizado.
///   - Submete quiz com score perfeito → recebe XP de quiz.
/// </summary>
[Collection("Vca")]
public class CourseProgressTests
{
    private readonly VcaWebApplicationFactory _factory;
    public CourseProgressTests(VcaWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Lesson_FullFlow_GrantsXpAndUpdatesLevel()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        var (userId, lessonId) = await SeedUserAndLessonAsync(xpReward: 500);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        // GET aula
        var detail = await client.GetAsync($"/api/lessons/{lessonId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        // Complete
        var complete = await client.PostAsync($"/api/lessons/{lessonId}/complete", content: null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await complete.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("xpEarned").GetInt32().Should().Be(500);
        body.GetProperty("totalXp").GetInt32().Should().Be(500);
        body.GetProperty("newLevel").GetString().Should().Be(nameof(UserLevel.Apprentice));

        // Tentativa duplicada → 400
        var dup = await client.PostAsync($"/api/lessons/{lessonId}/complete", content: null);
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Quiz_SubmitWithPerfectScore_GrantsXp()
    {
        var (userId, lessonId) = await SeedUserAndLessonAsync(xpReward: 10);
        await SeedQuizPoolAsync(lessonId, count: 10);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        // Buscar as questões personalizadas (5)
        var quizResp = await client.GetAsync($"/api/quizzes/lesson/{lessonId}");
        quizResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var quiz = await quizResp.Content.ReadFromJsonAsync<JsonElement>();
        var questions = quiz.GetProperty("questions");
        questions.GetArrayLength().Should().Be(5);

        // Mapear correctIndex via DB (todas as questões do seed têm correctIndex=0).
        var selectedAnswers = Enumerable.Range(0, 5).Select(_ => 0).ToList();

        var submit = await client.PostAsJsonAsync("/api/quizzes/submit",
            new { lessonId, selectedAnswers });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await submit.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("correctAnswers").GetInt32().Should().Be(5);
        result.GetProperty("xpEarned").GetInt32().Should().Be(30);
    }

    // ── Seeders ───────────────────────────────────────────────────────────────

    private async Task<(Guid userId, Guid lessonId)> SeedUserAndLessonAsync(int xpReward)
    {
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = User.Create(Guid.NewGuid(), $"u-{Guid.NewGuid():N}@t.com", "Tester");
        await uow.Users.AddAsync(user);

        var trail = Trail.Create("T", "desc", "csharp", TrailLevel.Beginner, 1);
        trail.Publish();
        await uow.Trails.AddAsync(trail);

        var module = Module.Create(trail.Id, "M", 1);
        await uow.Modules.AddAsync(module);

        var lesson = Lesson.Create(module.Id, "Aula Teste", 1, xpReward);
        lesson.SetContent(BuildValidLessonContentJson());
        lesson.Publish();
        await uow.Lessons.AddAsync(lesson);

        await uow.SaveChangesAsync();
        return (user.Id, lesson.Id);
    }

    private async Task SeedQuizPoolAsync(Guid lessonId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        for (int i = 0; i < count; i++)
        {
            var options = JsonSerializer.Serialize(new[] { "correct", "x", "y", "z" });
            var q = Quiz.Create(lessonId, $"Q{i}", options, correctIndex: 0, $"exp-{i}");
            await uow.Quizzes.AddAsync(q);
        }
        await uow.SaveChangesAsync();
    }

    private static string BuildValidLessonContentJson() => new LessonContent(
        Mission: "Aprender X",
        RealContext: "Contexto real",
        Concept: "Conceito",
        QuickChallenge: new QuickChallenge("Desafio", 15),
        Example: new CodeExample("csharp", "Console.WriteLine(1);", "exemplo"),
        Summary: "• a\n• b\n• c",
        XpReward: 30
    ).ToJson();
}
