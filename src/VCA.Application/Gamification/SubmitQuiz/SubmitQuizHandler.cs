using System.Text.Json;
using Microsoft.Extensions.Logging;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification.SubmitQuiz;

/// <summary>
/// Handler para avaliação de respostas de quiz e concessão de XP.
/// </summary>
public class SubmitQuizHandler
{
    private const int MaxAttempts = 2;
    private const int QuizXpReward = 30;
    private const int QuestionsPerAttempt = 5;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<SubmitQuizHandler> _logger;

    public SubmitQuizHandler(IUnitOfWork uow, ILogger<SubmitQuizHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<SubmitQuizResult>> HandleAsync(SubmitQuizCommand command, CancellationToken cancellationToken = default)
    {
        var attemptCount = await _uow.QuizAttempts.CountByUserAndLessonAsync(command.UserId, command.LessonId, cancellationToken);
        if (attemptCount >= MaxAttempts)
            return Result.Failure<SubmitQuizResult>($"Número máximo de {MaxAttempts} tentativas atingido para esta aula.");

        var allQuestions = await _uow.Quizzes.GetByLessonAsync(command.LessonId, cancellationToken);
        if (allQuestions.Count == 0)
            return Result.Failure<SubmitQuizResult>("Nenhuma questão encontrada para esta aula.");

        // Selecionar 5 questões baseado no seed (userId + lessonId)
        var seed = HashSeed(command.UserId, command.LessonId);
        var selectedQuestions = allQuestions
            .OrderBy(_ => seed)
            .Take(QuestionsPerAttempt)
            .ToList();

        // Calcular pontuação
        int correct = 0;
        for (int i = 0; i < Math.Min(command.SelectedAnswers.Count, selectedQuestions.Count); i++)
        {
            if (command.SelectedAnswers[i] == selectedQuestions[i].CorrectIndex)
                correct++;
        }

        // Registrar tentativa
        var answersJson = JsonSerializer.Serialize(command.SelectedAnswers);
        var attempt = QuizAttempt.Create(command.UserId, command.LessonId, correct, answersJson);
        await _uow.QuizAttempts.AddAsync(attempt, cancellationToken);

        // Conceder XP se acertou pelo menos 60%
        int xpEarned = 0;
        if (correct >= (int)Math.Ceiling(QuestionsPerAttempt * 0.6))
        {
            var user = await _uow.Users.GetByIdAsync(command.UserId, cancellationToken);
            if (user is not null)
            {
                user.AddXp(QuizXpReward);
                xpEarned = QuizXpReward;
                _logger.LogInformation("Usuário '{UserId}' ganhou {XP} XP no quiz da aula '{LessonId}'.", command.UserId, QuizXpReward, command.LessonId);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubmitQuizResult(correct, QuestionsPerAttempt, xpEarned, attemptCount + 1));
    }

    private static int HashSeed(Guid userId, Guid lessonId)
        => (userId.ToString() + lessonId.ToString()).GetHashCode();
}
