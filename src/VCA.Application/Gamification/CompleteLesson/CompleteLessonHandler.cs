using Microsoft.Extensions.Logging;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification.CompleteLesson;

/// <summary>
/// Handler que registra a conclusão de aula, concede XP e dispara verificação de badges.
/// </summary>
public class CompleteLessonHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CompleteLessonHandler> _logger;

    public CompleteLessonHandler(IUnitOfWork uow, ILogger<CompleteLessonHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<CompleteLessonResult>> HandleAsync(CompleteLessonCommand command, CancellationToken cancellationToken = default)
    {
        // Verifica se já completou a aula
        var alreadyCompleted = await _uow.UserProgresses.HasCompletedAsync(command.UserId, command.LessonId, cancellationToken);
        if (alreadyCompleted)
            return Result.Failure<CompleteLessonResult>("Aula já foi concluída anteriormente.");

        var user = await _uow.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result.Failure<CompleteLessonResult>("Usuário não encontrado.");

        var lesson = await _uow.Lessons.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<CompleteLessonResult>("Aula não encontrada.");

        // Conceder XP
        user.AddXp(lesson.XpReward);

        // Registrar progresso
        var progress = UserProgress.Create(user.Id, lesson.Id, lesson.XpReward);
        await _uow.UserProgresses.AddAsync(progress, cancellationToken);

        _logger.LogInformation("Usuário '{UserId}' concluiu aula '{LessonId}', +{XP} XP.", user.Id, lesson.Id, lesson.XpReward);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new CompleteLessonResult(lesson.XpReward, user.Xp, user.Level));
    }
}
