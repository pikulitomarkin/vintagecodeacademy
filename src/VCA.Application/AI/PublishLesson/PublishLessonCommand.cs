using MediatR;
using VCA.Domain.Common;

namespace VCA.Application.AI.PublishLesson;

/// <summary>
/// Publica uma aula em Draft/PendingReview após validações de conteúdo e quiz.
/// </summary>
public sealed record PublishLessonCommand(Guid LessonId) : IRequest<Result>;
