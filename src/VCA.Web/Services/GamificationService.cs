using System.Net.Http.Json;

namespace VCA.Web.Services;

/// <summary>
/// Serviço Blazor para ações de gamificação: conclusão de aulas e submissão de quizzes.
/// </summary>
public class GamificationService
{
    private readonly HttpClient _http;

    public GamificationService(HttpClient http) => _http = http;

    public async Task<CompleteLessonResponse?> CompleteLessonAsync(Guid lessonId)
    {
        var response = await _http.PostAsync($"api/gamification/lessons/{lessonId}/complete", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompleteLessonResponse>();
    }

    public async Task<SubmitQuizResponse?> SubmitQuizAsync(Guid lessonId, List<int> selectedAnswers)
    {
        var payload = new { selectedAnswers };
        var response = await _http.PostAsJsonAsync($"api/gamification/lessons/{lessonId}/quiz", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmitQuizResponse>();
    }

    public record CompleteLessonResponse(int XpEarned, int TotalXp, string NewLevel);
    public record SubmitQuizResponse(int CorrectAnswers, int TotalQuestions, int XpEarned, int AttemptNumber);
}
