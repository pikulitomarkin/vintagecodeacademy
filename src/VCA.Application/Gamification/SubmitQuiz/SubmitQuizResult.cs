namespace VCA.Application.Gamification.SubmitQuiz;

/// <summary>
/// Resultado da submissão de um quiz.
/// </summary>
public record SubmitQuizResult(int CorrectAnswers, int TotalQuestions, int XpEarned, int AttemptNumber);
