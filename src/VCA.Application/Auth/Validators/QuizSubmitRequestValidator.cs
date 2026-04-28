using FluentValidation;

namespace VCA.Application.Auth.Validators;

public sealed record QuizSubmitRequest(Guid LessonId, IReadOnlyList<QuizAnswer> Answers);
public sealed record QuizAnswer(Guid QuizId, int SelectedIndex);

/// <summary>
/// Validação estrutural de submissão de quiz. A validação semântica
/// (questões pertencem ao usuário, sem repetidas) é feita no handler com acesso ao banco.
/// </summary>
public sealed class QuizSubmitRequestValidator : AbstractValidator<QuizSubmitRequest>
{
    public const int MaxAnswersPerSubmission = 10;

    public QuizSubmitRequestValidator()
    {
        RuleFor(r => r.LessonId).NotEmpty();
        RuleFor(r => r.Answers)
            .NotEmpty().WithMessage("Pelo menos uma resposta é obrigatória.")
            .Must(a => a.Count <= MaxAnswersPerSubmission)
                .WithMessage($"Máximo de {MaxAnswersPerSubmission} respostas por submissão.")
            .Must(a => a.Select(x => x.QuizId).Distinct().Count() == a.Count)
                .WithMessage("Respostas duplicadas para a mesma questão.");

        RuleForEach(r => r.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuizId).NotEmpty();
            answer.RuleFor(a => a.SelectedIndex)
                .InclusiveBetween(0, 3)
                .WithMessage("Índice de alternativa fora do intervalo válido (0-3).");
        });
    }
}
