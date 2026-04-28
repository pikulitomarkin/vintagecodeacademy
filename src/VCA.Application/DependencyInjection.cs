using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VCA.Application.AI.GenerateLessonFromPdf;
using VCA.Application.AI.GenerateQuiz;
using VCA.Application.AI.Services;
using VCA.Application.Auth.Commands;
using VCA.Application.Courses.GetTrails;
using VCA.Application.Gamification;
using VCA.Application.Gamification.Commands;
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
        // MediatR — registra todos os handlers da camada Application
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<AwardXpCommandHandler>());

        // FluentValidation — registra todos os validators da camada Application
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        // Serviços de gamificação
        services.AddScoped<BadgeCheckerService>();
        services.AddScoped<RankingUpdaterService>();

        // VCA Intelligence — pipeline de IA
        services.AddScoped<PdfIngestionService>();
        services.AddSingleton<PromptBuilderService>();
        services.AddScoped<Interfaces.IAiContentGenerator, ContentGeneratorService>();
        services.AddScoped<ContentGeneratorService>();
        services.AddSingleton<QuizSelectionService>();

        // Handlers legados (sem MediatR) — mantidos por compatibilidade
        services.AddScoped<GenerateLessonFromPdfHandler>();
        services.AddScoped<GenerateQuizHandler>();
        services.AddScoped<CompleteLessonHandler>();
        services.AddScoped<SubmitQuizHandler>();
        services.AddScoped<GetTrailsHandler>();
        services.AddScoped<RegisterUserHandler>();

        return services;
    }
}
