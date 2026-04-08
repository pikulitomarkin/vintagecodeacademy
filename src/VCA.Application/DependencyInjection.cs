using Microsoft.Extensions.DependencyInjection;
using VCA.Application.AI.GenerateLessonFromPdf;
using VCA.Application.AI.GenerateQuiz;
using VCA.Application.Courses.GetTrails;
using VCA.Application.Gamification.CompleteLesson;
using VCA.Application.Gamification.SubmitQuiz;
using VCA.Application.Users.RegisterUser;

namespace VCA.Application;

/// <summary>
/// Extensões de injeção de dependência para a camada Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handlers de IA
        services.AddScoped<GenerateLessonFromPdfHandler>();
        services.AddScoped<GenerateQuizHandler>();

        // Handlers de Gamificação
        services.AddScoped<CompleteLessonHandler>();
        services.AddScoped<SubmitQuizHandler>();

        // Handlers de Cursos
        services.AddScoped<GetTrailsHandler>();

        // Handlers de Usuários
        services.AddScoped<RegisterUserHandler>();

        return services;
    }
}
