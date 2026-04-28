using Microsoft.AspNetCore.Components;
using VCA.Web.Services;

namespace VCA.Web.Pages.Trails;

/// <summary>
/// Code-behind da página de detalhe de uma trilha com módulos e aulas.
/// </summary>
public partial class TrailDetailPage : ComponentBase
{
    [Parameter] public Guid TrailId { get; set; }

    [Inject] private CourseHttpService CourseService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private TrailDetailDto? _trail;
    private TrailProgressDto? _progress;
    private Guid? _nextLessonId;

    private HashSet<Guid> _completedLessonIds = [];

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _trail = null;
        _progress = null;
        _completedLessonIds.Clear();

        var detailTask = CourseService.GetTrailDetailAsync(TrailId);
        var progressTask = SafeGetProgress();

        await Task.WhenAll(detailTask, progressTask);

        _trail = await detailTask;
        _progress = await progressTask;

        if (_progress is not null)
            _nextLessonId = _progress.NextLessonId;

        // Calcula quais aulas estão concluídas com base no número de concluídas por ordem
        if (_trail is not null && _progress is not null)
            BuildCompletedSet();

        _loading = false;
    }

    private async Task<TrailProgressDto?> SafeGetProgress()
    {
        try { return await CourseService.GetTrailProgressAsync(TrailId); }
        catch { return null; }
    }

    private void BuildCompletedSet()
    {
        if (_trail is null || _progress is null) return;

        var allLessons = _trail.Modules
            .OrderBy(m => m.Order)
            .SelectMany(m => m.Lessons.OrderBy(l => l.Order))
            .ToList();

        var completed = _progress.CompletedLessons;
        _completedLessonIds = allLessons.Take(completed).Select(l => l.Id).ToHashSet();
    }

    internal bool IsLessonCompleted(Guid lessonId) => _completedLessonIds.Contains(lessonId);

    internal LessonDisplayStatus GetLessonStatus(Guid lessonId, ModuleDto module, LessonSummaryDto lesson)
    {
        if (IsLessonCompleted(lessonId)) return LessonDisplayStatus.Completed;

        // Primeira aula não concluída fica disponível
        if (_trail is null) return LessonDisplayStatus.Locked;

        var allLessons = _trail.Modules
            .OrderBy(m => m.Order)
            .SelectMany(m => m.Lessons.OrderBy(l => l.Order))
            .ToList();

        var index = allLessons.FindIndex(l => l.Id == lessonId);
        if (index <= 0) return LessonDisplayStatus.Available; // primeira sempre disponível
        if (IsLessonCompleted(allLessons[index - 1].Id)) return LessonDisplayStatus.Available;

        return LessonDisplayStatus.Locked;
    }

    internal void GoToNextLesson()
    {
        if (_nextLessonId.HasValue)
            Nav.NavigateTo($"/aulas/{_nextLessonId.Value}");
    }
}

public enum LessonDisplayStatus { Completed, Available, Locked }
