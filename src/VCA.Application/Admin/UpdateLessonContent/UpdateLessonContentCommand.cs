using System.Text.Json;
using MediatR;
using VCA.Application.Admin.Common;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;
using VCA.Domain.ValueObjects;

namespace VCA.Application.Admin.UpdateLessonContent;

/// <summary>
/// Atualiza o JSON de conteúdo gamificado, título, XP e (opcionalmente) o pool de quizzes
/// de uma aula em revisão.
/// </summary>
public sealed record UpdateLessonContentCommand(
    Guid LessonId,
    string ContentJson,
    int XpReward,
    string Title,
    IReadOnlyList<UpdatedQuizDto>? Quizzes
) : IRequest<Result>;

public sealed class UpdateLessonContentHandler : IRequestHandler<UpdateLessonContentCommand, Result>
{
    private readonly IUnitOfWork _uow;

    public UpdateLessonContentHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(UpdateLessonContentCommand request, CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(request.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure($"Aula '{request.LessonId}' não encontrada.");

        // Valida o conteúdo via VO (lança DomainException se inválido).
        try
        {
            _ = LessonContent.FromJson(request.ContentJson);
        }
        catch (DomainException ex)
        {
            return Result.Failure($"Conteúdo inválido: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure("Título é obrigatório.");

        lesson.SetContent(request.ContentJson);
        lesson.Update(request.Title, lesson.Order, request.XpReward);

        if (request.Quizzes is { Count: > 0 })
        {
            var existingById = (await _uow.Quizzes.GetByLessonAsync(lesson.Id, cancellationToken))
                .ToDictionary(q => q.Id);

            foreach (var q in request.Quizzes)
            {
                if (q.Options is null || q.Options.Count < 2)
                    return Result.Failure($"Quiz '{q.Id}' precisa de ao menos 2 opções.");
                if (q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Count)
                    return Result.Failure($"Quiz '{q.Id}': CorrectIndex fora do intervalo.");

                if (!existingById.TryGetValue(q.Id, out var existing))
                    continue;

                var optionsJson = JsonSerializer.Serialize(q.Options);
                existing.Update(q.Question, optionsJson, q.CorrectIndex, q.Explanation);
                _uow.Quizzes.Update(existing);
            }
        }

        _uow.Lessons.Update(lesson);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
