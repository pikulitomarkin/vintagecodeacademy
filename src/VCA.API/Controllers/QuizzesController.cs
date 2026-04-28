using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Application.Gamification.SubmitQuiz;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de quiz — entrega questões personalizadas e avalia respostas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuizzesController : ControllerBase
{
    private const int QuestionsPerAttempt = 5;

    private readonly IUnitOfWork _uow;
    private readonly SubmitQuizHandler _submitHandler;

    public QuizzesController(IUnitOfWork uow, SubmitQuizHandler submitHandler)
    {
        _uow = uow;
        _submitHandler = submitHandler;
    }

    /// <summary>
    /// Retorna as 5 questões personalizadas para o usuário autenticado na aula informada.
    /// A seleção e ordem são determinísticas (seed = userId + lessonId).
    /// O campo correctIndex é omitido da resposta para não revelar o gabarito.
    /// </summary>
    [HttpGet("lesson/{lessonId:guid}")]
    [ProducesResponseType(typeof(PersonalizedQuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Questões personalizadas do quiz para o usuário na aula")]
    public async Task<ActionResult<PersonalizedQuizDto>> GetByLesson(Guid lessonId, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();

        var lesson = await _uow.Lessons.GetByIdAsync(lessonId, cancellationToken);
        if (lesson is null) return NotFound();

        var attemptCount = await _uow.QuizAttempts.CountByUserAndLessonAsync(userId, lessonId, cancellationToken);

        var allQuestions = await _uow.Quizzes.GetByLessonAsync(lessonId, cancellationToken);
        if (allQuestions.Count == 0)
            return NotFound();

        var seed = HashSeed(userId, lessonId);
        var selected = allQuestions
            .OrderBy(q => HashOffset(seed, q.Id))
            .Take(QuestionsPerAttempt)
            .Select(q => new QuizQuestionDto(
                q.Id,
                q.Question,
                JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? []))
            .ToList();

        return Ok(new PersonalizedQuizDto(lessonId, selected, attemptCount, maxAttempts: 2));
    }

    /// <summary>
    /// Avalia as respostas do usuário, registra a tentativa e concede XP se aprovado (≥ 60%).
    /// Máximo de 2 tentativas por aula.
    /// </summary>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(QuizResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Submete respostas do quiz e retorna resultado")]
    public async Task<ActionResult<QuizResultDto>> Submit(
        [FromBody] QuizSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var command = new SubmitQuizCommand(userId, request.LessonId, request.SelectedAnswers);
        var result = await _submitHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error!));

        var v = result.Value!;
        return Ok(new QuizResultDto(
            v.CorrectAnswers,
            v.TotalQuestions,
            v.XpEarned,
            v.AttemptNumber,
            v.CorrectAnswers * 100 / v.TotalQuestions));
    }

    private static int HashSeed(Guid userId, Guid lessonId)
        => (userId.ToString() + lessonId.ToString()).GetHashCode();

    private static int HashOffset(int seed, Guid questionId)
        => (seed ^ questionId.GetHashCode()) & int.MaxValue;
}

public record PersonalizedQuizDto(
    Guid LessonId,
    List<QuizQuestionDto> Questions,
    int AttemptsDone,
    int MaxAttempts);

public record QuizQuestionDto(Guid Id, string Question, List<string> Options);
public record QuizSubmitRequest(Guid LessonId, List<int> SelectedAnswers);
public record QuizResultDto(int CorrectAnswers, int TotalQuestions, int XpEarned, int AttemptNumber, int ScorePercent);
