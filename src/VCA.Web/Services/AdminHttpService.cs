using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace VCA.Web.Services;

/// <summary>
/// Cliente HTTP tipado para os endpoints administrativos (/api/admin/*).
/// Inclui parser SSE para o pipeline de processamento de PDF.
/// </summary>
public class AdminHttpService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AdminHttpService(HttpClient http) => _http = http;

    // ── Dashboard ────────────────────────────────────────────────────────────
    public Task<DashboardMetricsDto?> GetDashboardAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<DashboardMetricsDto>("api/admin/dashboard", ct);

    // ── Drafts ───────────────────────────────────────────────────────────────
    public Task<LessonDraftsPageDto?> GetDraftsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => _http.GetFromJsonAsync<LessonDraftsPageDto>($"api/admin/lessons/drafts?page={page}&pageSize={pageSize}", ct);

    public Task<LessonDraftDetailDto?> GetDraftDetailAsync(Guid lessonId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<LessonDraftDetailDto>($"api/admin/lessons/{lessonId}/draft-detail", ct);

    public async Task<bool> UpdateContentAsync(Guid lessonId, UpdateLessonContentRequest body, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/admin/lessons/{lessonId}/content", body, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> PublishAsync(Guid lessonId, CancellationToken ct = default)
    {
        var resp = await _http.PutAsync($"api/admin/lessons/{lessonId}/publish", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(Guid lessonId, CancellationToken ct = default)
    {
        var resp = await _http.PutAsync($"api/admin/lessons/{lessonId}/archive", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<RegenerateChunkResponseDto?> RegenerateChunkAsync(
        Guid lessonId,
        int chunkIndex,
        string difficulty = "Intermediate",
        string stack = "csharp",
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsync(
            $"api/admin/lessons/{lessonId}/regenerate-chunk/{chunkIndex}?difficulty={difficulty}&stack={Uri.EscapeDataString(stack)}",
            null, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RegenerateChunkResponseDto>(JsonOpts, ct);
    }

    // ── PDF Processing com SSE ───────────────────────────────────────────────

    /// <summary>
    /// Faz upload do PDF e consome o stream SSE chamando <paramref name="onProgress"/> a cada evento.
    /// Retorna o evento final.
    /// </summary>
    public async Task<ProcessPdfProgressEventDto?> ProcessPdfStreamAsync(
        Guid lessonId,
        Stream pdfStream,
        string fileName,
        string difficulty,
        string stack,
        bool generateQuiz,
        int quizQuestionCount,
        Func<ProcessPdfProgressEventDto, Task> onProgress,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(pdfStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "pdf", fileName);
        content.Add(new StringContent(difficulty), "difficulty");
        content.Add(new StringContent(stack), "stack");
        content.Add(new StringContent(generateQuiz.ToString()), "generateQuiz");
        content.Add(new StringContent(quizQuestionCount.ToString()), "quizQuestionCount");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/lessons/{lessonId}/process-pdf-stream")
        {
            Content = content
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        ProcessPdfProgressEventDto? lastEvent = null;
        var dataBuffer = new StringBuilder();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                if (dataBuffer.Length > 0) dataBuffer.AppendLine();
                dataBuffer.Append(line[6..]);
            }
            else if (string.IsNullOrEmpty(line) && dataBuffer.Length > 0)
            {
                var json = dataBuffer.ToString();
                dataBuffer.Clear();
                ProcessPdfProgressEventDto? evt = null;
                try
                {
                    evt = JsonSerializer.Deserialize<ProcessPdfProgressEventDto>(json, JsonOpts);
                }
                catch
                {
                    // ignora malformados
                }
                if (evt is null) continue;

                lastEvent = evt;
                await onProgress(evt);
                if (evt.IsFinal) break;
            }
        }

        return lastEvent;
    }
}

// ── DTOs (espelho dos DTOs do servidor) ──────────────────────────────────────

public record DashboardMetricsDto(
    int PublishedLessons,
    int DraftLessons,
    int PendingReviewLessons,
    int TotalQuizQuestions,
    decimal TotalAiCostUsd,
    decimal MonthlyAiCostUsd,
    double AverageQuizScore,
    int ActiveUsersLastWeek,
    List<XpDayPointDto> XpDistributionLast7Days);

public record XpDayPointDto(DateTime Day, int TotalXp);

public record LessonDraftsPageDto(
    List<LessonDraftItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record LessonDraftItemDto(
    Guid LessonId,
    Guid ModuleId,
    string Title,
    string Status,
    int ChunksProcessed,
    int QuizCount,
    DateTime CreatedAt,
    decimal TotalCostUsd);

public record LessonDraftDetailDto(
    Guid LessonId,
    Guid ModuleId,
    string ModuleTitle,
    string Title,
    string Status,
    int XpReward,
    int Order,
    string ContentJson,
    DateTime CreatedAt,
    decimal TotalCostUsd,
    List<DraftQuizDto> Quizzes,
    List<DraftChunkDto> Chunks);

public record DraftQuizDto(
    Guid Id,
    string Question,
    List<string> Options,
    int CorrectIndex,
    string Explanation);

public record DraftChunkDto(int ChunkIndex, string RawText);

public record UpdateLessonContentRequest(
    string ContentJson,
    int XpReward,
    string Title,
    List<UpdatedQuizDto>? Quizzes);

public record UpdatedQuizDto(
    Guid Id,
    string Question,
    List<string> Options,
    int CorrectIndex,
    string Explanation);

public record ProcessPdfProgressEventDto(
    string Stage,
    int Current,
    int Total,
    string? Message,
    bool IsFinal,
    bool IsError,
    JsonElement? Result);

public record RegenerateChunkResponseDto(decimal CostUsd);

// LessonContent (espelho do VO do domínio para preview)
public record AdminLessonContentDto(
    string Mission,
    string RealContext,
    string Concept,
    AdminQuickChallengeDto QuickChallenge,
    AdminCodeExampleDto Example,
    string Summary,
    int XpReward);

public record AdminQuickChallengeDto(string Description, int Xp);
public record AdminCodeExampleDto(string Language, string Code, string Explanation);
