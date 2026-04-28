using System.Net.Http.Json;

namespace VCA.Web.Services;

/// <summary>
/// Serviço HTTP tipado para trilhas, aulas e quizzes da VCA API.
/// </summary>
public class CourseHttpService
{
    private readonly HttpClient _http;

    public CourseHttpService(HttpClient http) => _http = http;

    // ── Trilhas ───────────────────────────────────────────────────────────────

    public Task<List<TrailDto>?> GetTrailsAsync()
        => _http.GetFromJsonAsync<List<TrailDto>>("api/trails");

    public Task<TrailDetailDto?> GetTrailDetailAsync(Guid id)
        => _http.GetFromJsonAsync<TrailDetailDto>($"api/trails/{id}");

    public Task<TrailProgressDto?> GetTrailProgressAsync(Guid id)
        => _http.GetFromJsonAsync<TrailProgressDto>($"api/trails/{id}/progress");

    // ── Aulas ─────────────────────────────────────────────────────────────────

    public Task<LessonDetailDto?> GetLessonAsync(Guid id)
        => _http.GetFromJsonAsync<LessonDetailDto>($"api/lessons/{id}");

    public async Task<XpEventDto?> CompleteLessonAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/lessons/{id}/complete", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<XpEventDto>();
    }

    // ── Quizzes ───────────────────────────────────────────────────────────────

    public Task<QuizDto?> GetQuizAsync(Guid lessonId)
        => _http.GetFromJsonAsync<QuizDto>($"api/quizzes/lesson/{lessonId}");

    public async Task<QuizResultDto?> SubmitQuizAsync(QuizSubmitRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/quizzes/submit", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<QuizResultDto>();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record TrailDto(
    Guid Id,
    string Title,
    string Description,
    string Stack,
    string Level,
    int TotalLessons,
    bool IsPublished);

public record TrailDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Stack,
    string Level,
    List<ModuleDto> Modules);

public record ModuleDto(
    Guid Id,
    string Title,
    int Order,
    List<LessonSummaryDto> Lessons);

public record LessonSummaryDto(
    Guid Id,
    string Title,
    int Order,
    int XpReward,
    string Status);

public record TrailProgressDto(
    int TotalLessons,
    int CompletedLessons,
    int TotalXpEarned,
    Guid? NextLessonId);

public record LessonDetailDto(
    Guid Id,
    string Title,
    int XpReward,
    bool CompletedByUser,
    LessonContentDto Content,
    Guid? PreviousLessonId,
    Guid? NextLessonId);

public record LessonContentDto(
    string Mission,
    string RealContext,
    string Concept,
    string QuickChallenge,
    string Example,
    string Summary);

public record XpEventDto(
    Guid UserId,
    int XpAwarded,
    int NewTotalXp,
    string PreviousLevel,
    string? NewLevel);

public record QuizDto(
    Guid LessonId,
    string LessonTitle,
    List<QuizQuestionDto> Questions);

public record QuizQuestionDto(
    Guid QuestionId,
    string Question,
    List<string> Options);

public record QuizSubmitRequest(
    Guid LessonId,
    List<QuizAnswerDto> Answers);

public record QuizAnswerDto(
    Guid QuestionId,
    int SelectedIndex);

public record QuizResultDto(
    int CorrectAnswers,
    int TotalQuestions,
    int XpEarned,
    int AttemptNumber,
    int Score,
    List<QuizAnswerResultDto> Results);

public record QuizAnswerResultDto(
    Guid QuestionId,
    string Question,
    int SelectedIndex,
    int CorrectIndex,
    bool IsCorrect,
    string Explanation);
