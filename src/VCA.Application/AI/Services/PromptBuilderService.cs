using VCA.Application.AI.Common;
using VCA.Domain.Enums;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.Services;

/// <summary>
/// Constrói prompts versionados para a IA. Persona "Professor Vintage", saída JSON estrita.
/// </summary>
public sealed class PromptBuilderService
{
    public const string CurrentVersion = "v1";

    private const string SystemPersona =
        """
        Você é o Professor Vintage, instrutor pragmático e direto da Vintage Code Academy.
        Voz: técnica, com analogias do mundo real, sem floreios.
        Você foi contratado para transformar conteúdo técnico em aulas gamificadas curtas (3-7 min de leitura)
        que fazem alunos brasileiros aprenderem programação aplicada ao mercado.
        REGRAS ABSOLUTAS:
        - Responda EXCLUSIVAMENTE com JSON válido, sem markdown, sem comentários, sem prosa antes ou depois.
        - Português brasileiro. Tom direto, exemplos reais. Nunca inicie frases com "Bem-vindo".
        - Código sempre em blocos puros, sem cercas markdown dentro do JSON.
        """;

    public string GetSystemPrompt() => SystemPersona;

    public string BuildLessonPrompt(PdfChunk chunk, DifficultyLevel difficulty, string stack)
    {
        var difficultyLabel = difficulty switch
        {
            DifficultyLevel.Beginner => "iniciante (zero pré-requisitos)",
            DifficultyLevel.Intermediate => "intermediário (assume fundamentos da linguagem)",
            DifficultyLevel.Advanced => "avançado (assume fluência prática)",
            _ => "intermediário"
        };

        var stackLabel = string.IsNullOrWhiteSpace(stack) ? "stack genérica" : stack.Trim();

        return $$"""
        Gere uma aula gamificada baseada estritamente no CONTEÚDO_FONTE abaixo.

        DIFICULDADE: {{difficultyLabel}}
        STACK_ALVO: {{stackLabel}}

        ESTRUTURA OBRIGATÓRIA (JSON exato, todos os campos obrigatórios):
        {
          "mission": "1 parágrafo: o que o aluno vai conquistar ao final (verbo de ação no início).",
          "realContext": "1 parágrafo curto sobre como isso é usado no mercado real (cite cenário concreto).",
          "concept": "Explicação técnica clara e progressiva (3-6 parágrafos, sem listas longas).",
          "quickChallenge": {
            "description": "Desafio prático curto que o aluno consegue resolver em 5 minutos. Inclua entrada esperada e saída esperada.",
            "xp": 15
          },
          "example": {
            "language": "linguagem de programação detectada do conteúdo (ex: 'csharp', 'javascript', 'python', 'sql')",
            "code": "código de exemplo funcional, comentado, demonstrando o conceito principal",
            "explanation": "explicação linha-a-linha curta do código acima"
          },
          "summary": "3 a 5 bullets curtos, separados por '\\n• '. Comece com '• '.",
          "xpReward": 30
        }

        REGRAS:
        - Não invente fatos fora do CONTEÚDO_FONTE. Se faltar informação, mantenha o exemplo simples.
        - "xpReward" entre 10 e 100 conforme densidade técnica do chunk.
        - Nunca use crases triplas dentro de strings JSON.

        --- CONTEÚDO_FONTE (chunk #{{chunk.ChunkIndex}}) ---
        {{chunk.RawText}}
        --- FIM_CONTEÚDO_FONTE ---
        """;
    }

    public string BuildQuizPrompt(LessonContent lessonContent, string lessonTitle, int count = 10)
    {
        return $$"""
        Gere {{count}} questões de múltipla escolha para a aula "{{lessonTitle}}".

        DISTRIBUIÇÃO OBRIGATÓRIA:
        - 4 questões CONCEITUAIS (testam compreensão de definição/teoria, marque "type":"conceptual").
        - 4 questões PRÁTICAS (mostram um trecho de código curto e perguntam o resultado/erro/correção, "type":"practical").
        - 2 questões PEGADINHA (distratores muito plausíveis, exigem leitura cuidadosa, "type":"trick").

        ESTRUTURA OBRIGATÓRIA — RETORNE UM ARRAY JSON com exatamente {{count}} objetos:
        [
          {
            "question": "Texto da pergunta. Para questões práticas, inclua o código entre aspas escapadas.",
            "options": ["alternativa A", "alternativa B", "alternativa C", "alternativa D"],
            "correctIndex": 0,
            "explanation": "Por que essa é a resposta correta E por que as outras estão erradas (1-2 linhas).",
            "type": "conceptual"
          }
        ]

        REGRAS:
        - Sempre 4 alternativas, todas plausíveis, nenhuma absurda.
        - "correctIndex" é índice 0-3.
        - Embaralhe a posição da resposta correta entre as questões (não concentre tudo em 0).
        - Nunca repita pergunta ou alternativa.
        - Português brasileiro técnico.

        --- CONTEÚDO_DA_AULA ---
        Missão: {{lessonContent.Mission}}
        Conceito: {{lessonContent.Concept}}
        Exemplo ({{lessonContent.Example.Language}}):
        {{lessonContent.Example.Code}}
        Resumo: {{lessonContent.Summary}}
        --- FIM_CONTEÚDO_DA_AULA ---
        """;
    }
}
