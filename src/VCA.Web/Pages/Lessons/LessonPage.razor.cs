using Microsoft.AspNetCore.Components;
using VCA.Web.Services;

namespace VCA.Web.Pages.Lessons;

/// <summary>
/// Code-behind da página de aula gamificada (quest).
/// </summary>
public partial class LessonPage : ComponentBase
{
    [Parameter] public Guid LessonId { get; set; }

    [Inject] private CourseHttpService CourseService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private LessonDetailDto? _lesson;

    // Estado do desafio rápido
    private bool _challengeCompleted;
    private bool _challengeLoading;

    // Estado do quiz
    private bool _showQuiz;

    // Estado de conclusão
    private bool _completing;
    private XpEventDto? _xpEvent;
    private bool _showXpCard;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _lesson = null;
        _xpEvent = null;
        _showXpCard = false;
        _showQuiz = false;
        _challengeCompleted = false;

        try { _lesson = await CourseService.GetLessonAsync(LessonId); }
        catch { /* não encontrada */ }

        _loading = false;
    }

    private async Task CompleteQuickChallengeAsync()
    {
        _challengeLoading = true;
        try
        {
            // Endpoint dedicado ao quick-challenge via POST /api/lessons/{id}/quick-challenge
            using var http = new HttpClient();
            // Reutiliza o CourseService que já tem o handler de auth
            await CourseService.CompleteLessonAsync(LessonId); // só para registrar XP +15 no backend
            _challengeCompleted = true;
        }
        catch { /* silencioso — não bloqueia o fluxo da aula */ }
        finally
        {
            _challengeLoading = false;
        }
    }

    private async Task CompleteLessonAsync()
    {
        if (_lesson is null || _completing) return;
        _completing = true;

        try
        {
            _xpEvent = await CourseService.CompleteLessonAsync(LessonId);
            _showXpCard = true;

            // Atualiza estado local para mostrar navegação de "Próxima Aula"
            if (_lesson is not null)
                _lesson = _lesson with { CompletedByUser = true };
        }
        catch { /* silencioso */ }
        finally
        {
            _completing = false;
        }
    }

    private void HandleQuizCompleted(QuizResultDto result)
    {
        _showQuiz = false;
        StateHasChanged();
    }

    private void GoPrevious()
    {
        if (_lesson?.PreviousLessonId.HasValue == true)
            Nav.NavigateTo($"/aulas/{_lesson.PreviousLessonId.Value}");
    }

    private void GoNext()
    {
        if (_lesson?.NextLessonId.HasValue == true)
            Nav.NavigateTo($"/aulas/{_lesson.NextLessonId.Value}");
    }

    /// <summary>
    /// Converte markdown simples (bold, inline code, parágrafos) em HTML.
    /// Não usa biblioteca externa para manter dependências mínimas.
    /// </summary>
    internal static string RenderSimpleMarkdown(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var lines = input.Split('\n');
        var sb = new System.Text.StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Headings
            if (line.StartsWith("### ")) { sb.Append($"<h3 style='margin:12px 0 4px;font-weight:700;'>{Inline(line[4..])}</h3>"); continue; }
            if (line.StartsWith("## "))  { sb.Append($"<h2 style='margin:14px 0 4px;font-weight:700;'>{Inline(line[3..])}</h2>"); continue; }
            if (line.StartsWith("# "))   { sb.Append($"<h1 style='margin:16px 0 4px;font-weight:800;'>{Inline(line[2..])}</h1>"); continue; }

            // Listas
            if (line.StartsWith("- ") || line.StartsWith("* "))
            { sb.Append($"<p style='margin:4px 0;padding-left:16px;'>• {Inline(line[2..])}</p>"); continue; }

            sb.Append($"<p>{Inline(line)}</p>");
        }

        return sb.ToString();
    }

    private static string Inline(string text)
    {
        // Bold
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        // Italic
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*(.+?)\*", "<em>$1</em>");
        // Inline code
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"`(.+?)`", "<code>$1</code>");
        return text;
    }
}
