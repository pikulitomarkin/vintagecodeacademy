using VCA.Application.AI.Common;

namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato de baixo nível para chamadas à API de geração de conteúdo (DeepSeek/OpenAI/etc).
/// Implementação deve cuidar de retry, timeouts, parsing de tokens e cálculo de custo.
/// </summary>
public interface IAiCompletionClient
{
    /// <summary>
    /// Realiza uma chamada de completion solicitando saída em JSON.
    /// </summary>
    /// <param name="systemPrompt">Mensagem de sistema (persona).</param>
    /// <param name="userPrompt">Mensagem do usuário (instrução + contexto).</param>
    /// <param name="temperature">Temperatura amostral (0.0-2.0).</param>
    /// <param name="maxTokens">Limite máximo de tokens de saída.</param>
    Task<AiCompletionResult> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.7,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default);
}
