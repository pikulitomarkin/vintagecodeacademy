using VCA.Domain.Entities;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.Services;

/// <summary>
/// Personalização de quizzes: dado um pool de N questões, seleciona K de forma
/// determinística por (userId, lessonId) e embaralha alternativas mantendo o índice correto.
/// </summary>
public sealed class QuizSelectionService
{
    public const int QuestionsPerAttempt = 5;

    /// <summary>
    /// Seleciona QuestionsPerAttempt questões para o aluno. Determinístico por (userId, lessonId, attemptNumber).
    /// AttemptNumber começa em 1.
    /// </summary>
    public IReadOnlyList<PersonalizedQuizQuestion> SelectForUser(
        Guid userId,
        Guid lessonId,
        int attemptNumber,
        IReadOnlyList<Quiz> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (pool.Count == 0) return Array.Empty<PersonalizedQuizQuestion>();

        var seed = ComputeSeed(userId, lessonId, attemptNumber);
        var rng = new Random(seed);

        var indices = Enumerable.Range(0, pool.Count).ToList();
        Shuffle(indices, rng);

        var take = Math.Min(QuestionsPerAttempt, pool.Count);
        var picks = indices.Take(take).Select(i => pool[i]).ToList();

        var result = new List<PersonalizedQuizQuestion>(picks.Count);
        foreach (var quiz in picks)
        {
            var question = new QuizQuestion(
                quiz.Question,
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(quiz.OptionsJson) ?? new List<string>(),
                quiz.CorrectIndex,
                quiz.Explanation);
            result.Add(ShuffleOptions(quiz.Id, question, rng));
        }
        return result;
    }

    internal static int ComputeSeed(Guid userId, Guid lessonId, int attemptNumber)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + userId.GetHashCode();
            hash = hash * 31 + lessonId.GetHashCode();
            hash = hash * 31 + attemptNumber;
            return hash;
        }
    }

    private static PersonalizedQuizQuestion ShuffleOptions(Guid quizId, QuizQuestion q, Random rng)
    {
        var pairs = q.Options
            .Select((opt, i) => (opt, isCorrect: i == q.CorrectIndex))
            .ToList();
        Shuffle(pairs, rng);

        var newOptions = pairs.Select(p => p.opt).ToList();
        var newCorrect = pairs.FindIndex(p => p.isCorrect);

        return new PersonalizedQuizQuestion(
            quizId,
            q.Question,
            newOptions,
            newCorrect,
            q.Explanation);
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>
/// Questão de quiz personalizada (alternativas embaralhadas) entregue ao aluno.
/// </summary>
public sealed record PersonalizedQuizQuestion(
    Guid QuizId,
    string Question,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string Explanation);
