using System.Text.Json;
using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.AI.GenerateQuiz;

/// <summary>
/// Handler para geração automática de questões de quiz usando DeepSeek.
/// </summary>
public class GenerateQuizHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IDeepSeekService _deepSeek;
    private readonly ILogger<GenerateQuizHandler> _logger;

    public GenerateQuizHandler(IUnitOfWork uow, IDeepSeekService deepSeek, ILogger<GenerateQuizHandler> logger)
    {
        _uow = uow;
        _deepSeek = deepSeek;
        _logger = logger;
    }

    public async Task<Result<int>> HandleAsync(GenerateQuizCommand command, CancellationToken cancellationToken = default)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<int>($"Aula '{command.LessonId}' não encontrada.");

        var result = await _deepSeek.GenerateQuizQuestionsAsync(
            lesson.Title, lesson.ContentJson, command.QuestionCount, cancellationToken);

        _logger.LogInformation("Quiz gerado para aula '{LessonId}'. Questões: {Count}", command.LessonId, command.QuestionCount);

        // Deserializar e persistir as questões geradas
        var questions = JsonSerializer.Deserialize<List<QuizQuestionDto>>(result.ContentJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (questions is null || questions.Count == 0)
            return Result.Failure<int>("A IA não retornou questões válidas.");

        foreach (var q in questions)
        {
            var quiz = Quiz.Create(
                lesson.Id,
                q.Question,
                JsonSerializer.Serialize(q.Options),
                q.CorrectIndex,
                q.Explanation);
            await _uow.Quizzes.AddAsync(quiz, cancellationToken);
        }

        // Log de geração
        var aiLog = AiGenerationLog.Create(lesson.Id, result.Model, result.PromptTokens, result.CompletionTokens, result.CostUsd);
        await _uow.AiGenerationLogs.AddAsync(aiLog, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(questions.Count);
    }

    private record QuizQuestionDto(string Question, List<string> Options, int CorrectIndex, string Explanation);
}
