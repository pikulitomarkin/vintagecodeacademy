using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VCA.Application.AI.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Enums;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.Services;

/// <summary>
/// Orquestra geração de aula: prompt-building → IA → validação → LessonContent.
/// </summary>
public sealed class ContentGeneratorService : IAiContentGenerator
{
    private readonly IAiCompletionClient _ai;
    private readonly PromptBuilderService _prompts;
    private readonly ILogger<ContentGeneratorService> _logger;

    public ContentGeneratorService(
        IAiCompletionClient ai,
        PromptBuilderService prompts,
        ILogger<ContentGeneratorService> logger)
    {
        _ai = ai;
        _prompts = prompts;
        _logger = logger;
    }

    public async Task<LessonGenerationResult> GenerateLessonAsync(
        LessonGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        var systemPrompt = _prompts.GetSystemPrompt();
        var userPrompt = _prompts.BuildLessonPrompt(request.Chunk, request.Difficulty, request.Stack);

        _logger.LogInformation(
            "Gerando aula via IA: title={Title} chunk={Index} estTokens={Tokens} difficulty={Difficulty}",
            request.LessonTitle, request.Chunk.ChunkIndex, request.Chunk.EstimatedTokenCount, request.Difficulty);

        var completion = await _ai.CompleteJsonAsync(
            systemPrompt,
            userPrompt,
            temperature: 0.7,
            maxTokens: 4000,
            cancellationToken: cancellationToken);

        LessonContent content;
        try
        {
            content = LessonContent.FromJson(completion.Content);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex,
                "Conteúdo gerado pela IA falhou na validação: chunk={Index} code={Code}",
                request.Chunk.ChunkIndex, ex.Code);
            throw;
        }

        sw.Stop();
        return new LessonGenerationResult(
            content,
            completion.Model,
            completion.PromptTokens,
            completion.CompletionTokens,
            completion.CostUsd,
            PromptBuilderService.CurrentVersion,
            sw.Elapsed);
    }

    public async Task<QuizGenerationResult> GenerateQuizAsync(
        QuizGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        var systemPrompt = _prompts.GetSystemPrompt();
        var userPrompt = _prompts.BuildQuizPrompt(request.LessonContent, request.LessonTitle, request.QuestionCount);

        _logger.LogInformation(
            "Gerando quiz via IA: title={Title} count={Count}",
            request.LessonTitle, request.QuestionCount);

        var completion = await _ai.CompleteJsonAsync(
            systemPrompt,
            userPrompt,
            temperature: 0.3,
            maxTokens: 6000,
            cancellationToken: cancellationToken);

        IReadOnlyList<QuizQuestion> questions;
        try
        {
            questions = QuizQuestion.ListFromJson(completion.Content);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Quiz gerado falhou na validação: code={Code}", ex.Code);
            throw;
        }

        if (questions.Count < request.QuestionCount)
        {
            _logger.LogWarning(
                "Quiz retornou {Actual} questões, esperado {Expected}.",
                questions.Count, request.QuestionCount);
        }

        sw.Stop();
        return new QuizGenerationResult(
            questions,
            completion.Model,
            completion.PromptTokens,
            completion.CompletionTokens,
            completion.CostUsd,
            PromptBuilderService.CurrentVersion,
            sw.Elapsed);
    }
}
