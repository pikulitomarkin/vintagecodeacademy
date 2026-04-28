using Microsoft.AspNetCore.Components;
using VCA.Web.Services;

namespace VCA.Web.Components.Quiz;

/// <summary>
/// Code-behind do componente de quiz reutilizável.
/// Ciclo de estados: Loading → Answering → Submitting → Completed (ou Error).
/// </summary>
public partial class QuizComponent : ComponentBase
{
    [Parameter, EditorRequired] public Guid LessonId { get; set; }

    /// <summary>Invocado quando o quiz é concluído com o resultado final.</summary>
    [Parameter] public EventCallback<QuizResultDto> OnQuizCompleted { get; set; }

    [Inject] private CourseHttpService CourseService { get; set; } = default!;

    private QuizState _state = QuizState.Loading;
    private QuizDto? _quiz;
    private QuizResultDto? _result;

    private int _currentIndex;
    private int[] _selectedIndexes = [];
    private bool _submitting;

    protected override async Task OnParametersSetAsync()
    {
        _state = QuizState.Loading;
        _quiz = null;
        _result = null;
        _currentIndex = 0;

        try
        {
            _quiz = await CourseService.GetQuizAsync(LessonId);

            if (_quiz is null || _quiz.Questions.Count == 0)
            {
                _state = QuizState.Error;
                return;
            }

            _selectedIndexes = Enumerable.Repeat(-1, _quiz.Questions.Count).ToArray();
            _state = QuizState.Answering;
        }
        catch
        {
            _state = QuizState.Error;
        }
    }

    internal QuizQuestionDto? CurrentQuestion
        => _quiz is not null && _currentIndex < _quiz.Questions.Count
            ? _quiz.Questions[_currentIndex]
            : null;

    internal double ProgressValue
        => _quiz is null ? 0 : (_currentIndex + 1) * 100.0 / _quiz.Questions.Count;

    internal bool AllAnswered
        => _selectedIndexes.All(i => i >= 0);

    internal void SelectOption(int optionIndex)
    {
        if (_currentIndex < _selectedIndexes.Length)
        {
            _selectedIndexes[_currentIndex] = optionIndex;
            StateHasChanged();
        }
    }

    internal void NextQuestion()
    {
        if (_quiz is null) return;
        if (_currentIndex < _quiz.Questions.Count - 1)
        {
            _currentIndex++;
            StateHasChanged();
        }
    }

    internal void PreviousQuestion()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            StateHasChanged();
        }
    }

    internal async Task SubmitAsync()
    {
        if (_quiz is null || _submitting) return;
        _submitting = true;

        try
        {
            var answers = _quiz.Questions
                .Select((q, i) => new QuizAnswerDto(q.QuestionId, _selectedIndexes[i]))
                .ToList();

            var request = new QuizSubmitRequest(LessonId, answers);
            _result = await CourseService.SubmitQuizAsync(request);

            if (_result is not null)
            {
                _state = QuizState.Completed;
                await OnQuizCompleted.InvokeAsync(_result);
            }
        }
        catch
        {
            _state = QuizState.Error;
        }
        finally
        {
            _submitting = false;
        }
    }

    internal string ScoreColor()
    {
        if (_result is null) return "#00C9C9";
        var ratio = (double)_result.CorrectAnswers / _result.TotalQuestions;
        return ratio >= 0.8 ? "#00E676" : ratio >= 0.5 ? "#FFEA00" : "#FF5252";
    }

    internal static string OptionLetter(int index) => index switch
    {
        0 => "A", 1 => "B", 2 => "C", 3 => "D", _ => ((char)('A' + index)).ToString()
    };
}

public enum QuizState { Loading, Answering, Submitting, Reviewing, Completed, Error }
