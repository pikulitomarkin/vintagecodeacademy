using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Domain.Common;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.PublishLesson;

public sealed class PublishLessonHandler : IRequestHandler<PublishLessonCommand, Result>
{
    public const int MinQuizQuestions = 5;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<PublishLessonHandler> _logger;

    public PublishLessonHandler(IUnitOfWork uow, ILogger<PublishLessonHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> Handle(PublishLessonCommand request, CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(request.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure($"Aula '{request.LessonId}' não encontrada.");

        if (lesson.Status == LessonStatus.Published)
            return Result.Failure("Aula já está publicada.");
        if (lesson.Status == LessonStatus.Archived)
            return Result.Failure("Aula arquivada não pode ser publicada.");

        // Validar conteúdo gamificado
        try
        {
            _ = LessonContent.FromJson(lesson.ContentJson);
        }
        catch (DomainException ex)
        {
            return Result.Failure($"Conteúdo da aula inválido: {ex.Message} (code={ex.Code}).");
        }

        // Validar pool de quiz
        var quizzes = await _uow.Quizzes.GetByLessonAsync(lesson.Id, cancellationToken);
        if (quizzes.Count < MinQuizQuestions)
            return Result.Failure($"Aula deve ter pelo menos {MinQuizQuestions} questões. Atual: {quizzes.Count}.");

        lesson.Publish();
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Aula publicada: {LessonId} '{Title}'", lesson.Id, lesson.Title);
        return Result.Success();
    }
}
