using System.Collections.Concurrent;
using VCA.Application.AI.Common;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.IntegrationTests.Infrastructure.Fakes;

/// <summary>
/// Fake do Supabase Auth — guarda usuários em memória e emite tokens determinísticos.
/// Suficiente para validar fluxos register/login/refresh/logout sem chamar a rede.
/// </summary>
public sealed class FakeSupabaseAuthService : ISupabaseAuthService
{
    private record StoredUser(SupabaseUser User, string Password);

    private readonly ConcurrentDictionary<string, StoredUser> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SupabaseUser> _byToken = new();
    private readonly ConcurrentDictionary<string, SupabaseUser> _byRefresh = new();

    public Task<SupabaseUser> RegisterAsync(string email, string password, string name, CancellationToken ct = default)
    {
        if (_byEmail.ContainsKey(email))
            throw new InvalidOperationException("User already registered.");

        var user = new SupabaseUser(Guid.NewGuid(), email, name, null, "email", true);
        _byEmail[email] = new StoredUser(user, password);
        return Task.FromResult(user);
    }

    public Task<SupabaseSession> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (!_byEmail.TryGetValue(email, out var stored) || stored.Password != password)
            throw new UnauthorizedAccessException("Invalid credentials.");

        return Task.FromResult(BuildSession(stored.User));
    }

    public string LoginWithGoogleUrl(string redirectTo) => $"https://fake.supabase/google?redirect={redirectTo}";
    public string LoginWithGitHubUrl(string redirectTo) => $"https://fake.supabase/github?redirect={redirectTo}";

    public Task<SupabaseSession> RefreshSessionAsync(string refreshToken, CancellationToken ct = default)
    {
        if (!_byRefresh.TryGetValue(refreshToken, out var user))
            throw new UnauthorizedAccessException("Invalid refresh token.");
        return Task.FromResult(BuildSession(user));
    }

    public Task SignOutAsync(string accessToken, CancellationToken ct = default)
    {
        _byToken.TryRemove(accessToken, out _);
        return Task.CompletedTask;
    }

    public Task<SupabaseUser?> GetUserFromTokenAsync(string accessToken, CancellationToken ct = default)
    {
        _byToken.TryGetValue(accessToken, out var user);
        return Task.FromResult<SupabaseUser?>(user);
    }

    private SupabaseSession BuildSession(SupabaseUser user)
    {
        var access = $"fake-access-{Guid.NewGuid():N}";
        var refresh = $"fake-refresh-{Guid.NewGuid():N}";
        _byToken[access] = user;
        _byRefresh[refresh] = user;
        return new SupabaseSession(access, refresh, "bearer", 3600,
            DateTime.UtcNow.AddHours(1), user);
    }
}

/// <summary>
/// Fake IUserSyncService — faz upsert direto no UnitOfWork sem nenhuma camada externa.
/// </summary>
public sealed class FakeUserSyncService : IUserSyncService
{
    private readonly IUnitOfWork _uow;

    public FakeUserSyncService(IUnitOfWork uow) => _uow = uow;

    public async Task<User> UpsertFromSupabaseAsync(SupabaseUser supabaseUser, CancellationToken ct = default)
    {
        var existing = await _uow.Users.GetByIdAsync(supabaseUser.Id, ct);
        if (existing is not null) return existing;

        var user = User.Create(supabaseUser.Id, supabaseUser.Email,
            supabaseUser.Name ?? supabaseUser.Email, supabaseUser.AvatarUrl);
        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return user;
    }
}

/// <summary>
/// Fake do cliente de IA — devolve LessonContent ou QuizQuestion[] pré-definido.
/// O modo é decidido pelo conteúdo do user prompt (palavras-chave do PromptBuilder).
/// </summary>
public sealed class FakeAiCompletionClient : IAiCompletionClient
{
    public const string LessonJson = """
        {
          "mission": "Aprender o conceito principal da aula.",
          "realContext": "Aplicação no mercado real.",
          "concept": "Explicação do conceito em detalhe.",
          "quickChallenge": { "description": "Resolva o desafio em 5 minutos.", "xp": 15 },
          "example": { "language": "csharp", "code": "Console.WriteLine(1);", "explanation": "exemplo." },
          "summary": "• ponto 1\n• ponto 2\n• ponto 3",
          "xpReward": 30
        }
        """;

    public static string QuizArrayJson(int count = 10)
    {
        var items = Enumerable.Range(0, count).Select(i => $$"""
            {
              "question": "Pergunta {{i}}?",
              "options": ["a", "b", "c", "d"],
              "correctIndex": {{i % 4}},
              "explanation": "explicação {{i}}",
              "type": "conceptual"
            }
            """);
        return "[" + string.Join(",", items) + "]";
    }

    public Task<AiCompletionResult> CompleteJsonAsync(
        string systemPrompt, string userPrompt,
        double temperature = 0.7, int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        var content = userPrompt.Contains("questões de múltipla escolha", StringComparison.OrdinalIgnoreCase)
            ? QuizArrayJson()
            : LessonJson;

        return Task.FromResult(new AiCompletionResult(
            Content: content,
            Model: "fake-model",
            PromptTokens: 100,
            CompletionTokens: 200,
            CostUsd: 0.0001m,
            Duration: TimeSpan.FromMilliseconds(50)));
    }
}

/// <summary>
/// Fake extractor — devolve um texto markdown previsível com headings.
/// </summary>
public sealed class FakePdfExtractor : IPdfExtractorService
{
    public const string DefaultText = """
        # Introdução
        Conteúdo introdutório com algumas frases para gerar tokens suficientes para o pipeline.
        Mais texto para garantir tamanho mínimo do chunk.

        # Capítulo 1
        Detalhamento do primeiro capítulo, com várias linhas de conteúdo técnico.
        Frases adicionais reforçam o tamanho do chunk gerado pelo serviço de ingestão.
        """;

    public Task<IReadOnlyList<string>> ExtractChunksAsync(Stream pdfStream, int chunkSize = 1500, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(new[] { DefaultText });

    public Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
        => Task.FromResult(new PdfExtractionResult(
            DefaultText, PageCount: 2, ByteSize: DefaultText.Length,
            Pages: new[] { new PdfPageText(1, DefaultText) }));
}

public sealed class FakeEmailService : IEmailService
{
    public Task SendWelcomeEmailAsync(string toEmail, string userName, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendBadgeEarnedEmailAsync(string toEmail, string userName, string badgeName, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class FakeStorageService : IStorageService
{
    public Task<string> UploadPdfAsync(string fileName, Stream content, CancellationToken ct = default)
        => Task.FromResult($"https://fake-storage/pdfs/{fileName}");
    public Task<string> UploadAvatarAsync(Guid userId, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult($"https://fake-storage/avatars/{userId}");
    public Task DeleteAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class FakeRankingBroadcaster : IRankingBroadcaster
{
    public Task BroadcastAsync(RankingBroadcastEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Fake do IDeepSeekService legado (mantido por compatibilidade com handlers antigos).
/// </summary>
public sealed class FakeDeepSeekService : IDeepSeekService
{
    public Task<DeepSeekGenerationResult> GenerateLessonContentAsync(string lessonTitle, IEnumerable<string> textChunks, CancellationToken ct = default)
        => Task.FromResult(new DeepSeekGenerationResult(FakeAiCompletionClient.LessonJson, "fake", 100, 200, 0.0001m));

    public Task<DeepSeekGenerationResult> GenerateQuizQuestionsAsync(string lessonTitle, string lessonContentJson, int questionCount = 10, CancellationToken ct = default)
        => Task.FromResult(new DeepSeekGenerationResult(FakeAiCompletionClient.QuizArrayJson(questionCount), "fake", 100, 200, 0.0001m));
}
