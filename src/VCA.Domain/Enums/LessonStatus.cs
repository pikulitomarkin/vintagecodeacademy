namespace VCA.Domain.Enums;

/// <summary>
/// Status de publicação de uma aula.
/// </summary>
public enum LessonStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Archived = 3
}
