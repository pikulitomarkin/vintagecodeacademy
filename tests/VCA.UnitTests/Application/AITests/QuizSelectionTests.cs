using System.Text.Json;
using FluentAssertions;
using VCA.Application.AI.Services;
using VCA.Domain.Entities;

namespace VCA.UnitTests.Application.AITests;

/// <summary>
/// Determinismo da seleção, variação por user/attempt e preservação do índice correto
/// após embaralhamento das alternativas.
/// </summary>
public class QuizSelectionTests
{
    private static IReadOnlyList<Quiz> BuildPool(int n = 10)
    {
        var lessonId = Guid.NewGuid();
        var list = new List<Quiz>(n);
        for (int i = 0; i < n; i++)
        {
            var options = JsonSerializer.Serialize(new[]
            {
                $"correct-{i}",   // index 0 é o correto
                $"wrong-A-{i}",
                $"wrong-B-{i}",
                $"wrong-C-{i}"
            });
            list.Add(Quiz.Create(lessonId, $"Q{i}", options, correctIndex: 0,
                explanation: $"exp-{i}"));
        }
        return list;
    }

    [Fact]
    public void SelectForUser_SameSeed_ReturnsSameQuestionsInSameOrder()
    {
        var svc = new QuizSelectionService();
        var pool = BuildPool();
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        var first = svc.SelectForUser(userId, lessonId, attemptNumber: 1, pool);
        var second = svc.SelectForUser(userId, lessonId, attemptNumber: 1, pool);

        first.Select(q => q.QuizId).Should().Equal(second.Select(q => q.QuizId));
        first.Select(q => q.Options).Should().BeEquivalentTo(second.Select(q => q.Options),
            opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void SelectForUser_DifferentUserId_ProducesDifferentSelectionOrAlternatives()
    {
        var svc = new QuizSelectionService();
        var pool = BuildPool(10);
        var lessonId = Guid.NewGuid();

        var u1 = svc.SelectForUser(Guid.NewGuid(), lessonId, 1, pool);
        var u2 = svc.SelectForUser(Guid.NewGuid(), lessonId, 1, pool);

        // Ao menos um indicador (ordem dos QuizIds OU ordem das alternativas) difere.
        var sameIds = u1.Select(q => q.QuizId).SequenceEqual(u2.Select(q => q.QuizId));
        var sameOptions = u1.Zip(u2, (a, b) => a.Options.SequenceEqual(b.Options)).All(x => x);
        (sameIds && sameOptions).Should().BeFalse(
            "user_id diferente deve produzir seleção/embaralhamento diferente");
    }

    [Fact]
    public void SelectForUser_DifferentAttemptNumber_ChangesSelectionOrAlternatives()
    {
        var svc = new QuizSelectionService();
        var pool = BuildPool(10);
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        var first = svc.SelectForUser(userId, lessonId, attemptNumber: 1, pool);
        var second = svc.SelectForUser(userId, lessonId, attemptNumber: 2, pool);

        var sameIds = first.Select(q => q.QuizId).SequenceEqual(second.Select(q => q.QuizId));
        var sameOptions = first.Zip(second, (a, b) => a.Options.SequenceEqual(b.Options)).All(x => x);
        (sameIds && sameOptions).Should().BeFalse(
            "segunda tentativa deve produzir seleção/embaralhamento diferente");
    }

    [Fact]
    public void SelectForUser_ShufflesOptionsButPreservesCorrectIndex()
    {
        var svc = new QuizSelectionService();
        var pool = BuildPool(10);
        var selected = svc.SelectForUser(Guid.NewGuid(), Guid.NewGuid(), 1, pool);

        foreach (var q in selected)
        {
            // A opção correta sempre começa com "correct-" no nosso pool.
            q.Options.Should().HaveCount(4);
            q.CorrectIndex.Should().BeInRange(0, 3);
            q.Options[q.CorrectIndex].Should().StartWith("correct-",
                "embaralhar alternativas deve atualizar o CorrectIndex para manter a correta");
        }
    }

    [Fact]
    public void SelectForUser_SeedIsDeterministicForSameInputs()
    {
        var u = Guid.NewGuid();
        var l = Guid.NewGuid();

        QuizSelectionService.ComputeSeed(u, l, 1)
            .Should().Be(QuizSelectionService.ComputeSeed(u, l, 1));
        QuizSelectionService.ComputeSeed(u, l, 1)
            .Should().NotBe(QuizSelectionService.ComputeSeed(u, l, 2));
    }

    [Fact]
    public void SelectForUser_ReturnsAtMostQuestionsPerAttempt()
    {
        var svc = new QuizSelectionService();
        var pool = BuildPool(20);
        var selected = svc.SelectForUser(Guid.NewGuid(), Guid.NewGuid(), 1, pool);
        selected.Should().HaveCount(QuizSelectionService.QuestionsPerAttempt);
    }

    [Fact]
    public void SelectForUser_EmptyPool_ReturnsEmpty()
    {
        var svc = new QuizSelectionService();
        var selected = svc.SelectForUser(Guid.NewGuid(), Guid.NewGuid(), 1, Array.Empty<Quiz>());
        selected.Should().BeEmpty();
    }
}
