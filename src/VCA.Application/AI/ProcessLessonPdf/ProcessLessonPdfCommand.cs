using MediatR;
using VCA.Application.AI.Common;
using VCA.Domain.Common;
using VCA.Domain.Enums;

namespace VCA.Application.AI.ProcessLessonPdf;

/// <summary>
/// Pipeline completo: PDF → chunks → IA → LessonContent + Quiz pool → Draft pronto para revisão.
/// O Stream NÃO é fechado pelo handler — quem chama deve descartar o Stream.
/// </summary>
public sealed record ProcessLessonPdfCommand(
    Guid LessonId,
    Stream PdfStream,
    string FileName,
    DifficultyLevel Difficulty,
    string Stack,
    bool GenerateQuiz = true,
    int QuizQuestionCount = 10,
    IProgress<ProcessLessonPdfProgress>? Progress = null
) : IRequest<Result<ContentGenerationResult>>;

public sealed record ProcessLessonPdfProgress(
    string Stage,
    int Current,
    int Total,
    string? Message = null);
