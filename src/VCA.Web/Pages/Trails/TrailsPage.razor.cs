using Microsoft.AspNetCore.Components;
using MudBlazor;
using VCA.Web.Services;

namespace VCA.Web.Pages.Trails;

/// <summary>
/// Code-behind da página de listagem de trilhas.
/// </summary>
public partial class TrailsPage : ComponentBase
{
    [Inject] private CourseHttpService CourseService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private List<TrailDto> _trails = [];
    private List<TrailDto> _filteredTrails = [];
    private Dictionary<Guid, TrailProgressDto> _progressMap = [];
    private MudChip? _selectedChip;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;

        var trails = await CourseService.GetTrailsAsync();
        _trails = trails ?? [];
        ApplyFilter();

        // Carrega progresso para cada trilha em paralelo
        var progressTasks = _trails.Select(async t =>
        {
            try
            {
                var p = await CourseService.GetTrailProgressAsync(t.Id);
                return (t.Id, Progress: p);
            }
            catch { return (t.Id, Progress: (TrailProgressDto?)null); }
        });

        var results = await Task.WhenAll(progressTasks);
        _progressMap = results
            .Where(r => r.Progress is not null)
            .ToDictionary(r => r.Id, r => r.Progress!);

        _loading = false;
    }

    private void ApplyFilter()
    {
        var selected = _selectedChip?.Value?.ToString() ?? "Todos";

        _filteredTrails = selected == "Todos"
            ? [.. _trails]
            : [.. _trails.Where(t => string.Equals(t.Level, selected, StringComparison.OrdinalIgnoreCase))];
    }

    // Invocado pelo MudChipSet via bind
    protected void OnChipChanged()
    {
        ApplyFilter();
        StateHasChanged();
    }

    internal static string GetTrailEmoji(string stack) => stack.ToLowerInvariant() switch
    {
        var s when s.Contains("python") => "🐍",
        var s when s.Contains("javascript") || s.Contains("js") => "🟨",
        var s when s.Contains("typescript") => "🔷",
        var s when s.Contains("react") => "⚛️",
        var s when s.Contains("blazor") || s.Contains(".net") || s.Contains("csharp") || s.Contains("c#") => "💜",
        var s when s.Contains("rust") => "🦀",
        var s when s.Contains("go") || s.Contains("golang") => "🐹",
        var s when s.Contains("devops") || s.Contains("docker") => "🐳",
        var s when s.Contains("sql") || s.Contains("database") => "🗄️",
        var s when s.Contains("mobile") || s.Contains("flutter") => "📱",
        _ => "📦"
    };

    internal static string LevelLabel(string level) => level.ToLowerInvariant() switch
    {
        "beginner" => "🌱 Iniciante",
        "intermediate" => "🔥 Intermediário",
        "advanced" => "⚡ Avançado",
        _ => level
    };
}
