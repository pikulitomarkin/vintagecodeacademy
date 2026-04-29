using System.Text;
using FluentAssertions;
using VCA.Application.AI.Services;

namespace VCA.UnitTests.Application.AITests;

/// <summary>
/// Estratégia de chunking: prefere headings markdown, com fallback para parágrafos
/// e respeito ao alvo de 800 tokens.
/// </summary>
public class PdfChunkingTests
{
    [Fact]
    public void BuildChunks_WithMarkdownHeadings_SplitsBySection()
    {
        var text = """
            # Capítulo 1
            Conteúdo do primeiro capítulo, com várias frases e parágrafos para criar
            uma seção razoavelmente substancial em termos de tokens estimados.

            Mais texto para complementar a seção e garantir que ela tenha um tamanho
            mínimo de chunking.

            ## Seção 1.1
            Detalhes da subseção, com mais conteúdo para gerar tokens suficientes.
            Continuação do texto da subseção 1.1 para manter densidade.

            # Capítulo 2
            Conteúdo do segundo capítulo, novamente preenchendo material para
            avaliar o particionamento por headings.

            Texto adicional para reforçar o tamanho mínimo do chunk gerado.
            """;

        var chunks = PdfIngestionService.BuildChunks(text);

        chunks.Should().NotBeEmpty();
        // Pelo menos uma seção começa com cabeçalho markdown.
        chunks.Any(c => c.RawText.TrimStart().StartsWith("#")).Should().BeTrue();
        chunks.Should().AllSatisfy(c => c.EstimatedTokenCount.Should().BeGreaterThan(0));
    }

    [Fact]
    public void BuildChunks_FallsBackToParagraphsWhenNoHeadings()
    {
        var paragraph = string.Concat(Enumerable.Repeat(
            "Este é um parágrafo razoavelmente longo, projetado para acumular tokens estimados ao longo do tempo. ",
            10));
        var text = string.Join("\n\n", Enumerable.Repeat(paragraph, 8));

        var chunks = PdfIngestionService.BuildChunks(text);

        chunks.Should().NotBeEmpty();
        // Sem headings — chunking por parágrafos.
        chunks.Should().AllSatisfy(c => c.RawText.Should().NotStartWith("#"));
    }

    [Fact]
    public void BuildChunks_RespectsTargetTokenSize()
    {
        // Texto enorme em parágrafos — múltiplos chunks, cada um próximo do alvo.
        var paragraph = new string('a', 4000) + "."; // ~ 1142 tokens
        var text = string.Join("\n\n", Enumerable.Repeat(paragraph, 6));

        var chunks = PdfIngestionService.BuildChunks(text);

        chunks.Count.Should().BeGreaterThan(1);
        chunks.Should().AllSatisfy(c =>
            c.EstimatedTokenCount.Should().BeLessOrEqualTo(PdfIngestionService.MaxTokensPerChunk + 200));
    }

    [Fact]
    public void BuildChunks_LayoutSemQuebrasClaras_StillProducesAtLeastOneChunk()
    {
        // Layout não-padrão: texto contínuo sem cabeçalhos nem quebras duplas claras.
        var sb = new StringBuilder();
        for (int i = 0; i < 30; i++)
            sb.Append("linha contínua sem estrutura clara ");
        var chunks = PdfIngestionService.BuildChunks(sb.ToString());

        chunks.Should().NotBeEmpty();
        chunks.Should().AllSatisfy(c => c.RawText.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void BuildChunks_EmptyOrWhitespace_ReturnsEmpty()
    {
        PdfIngestionService.BuildChunks(string.Empty).Should().BeEmpty();
        PdfIngestionService.BuildChunks("   \n\n   ").Should().BeEmpty();
    }

    [Fact]
    public void EstimateTokens_ProportionalToLength()
    {
        var t1 = PdfIngestionService.EstimateTokens(new string('a', 100));
        var t2 = PdfIngestionService.EstimateTokens(new string('a', 200));
        t2.Should().BeGreaterThan(t1);
    }

    [Fact]
    public void SplitByHeadings_ReturnsEmptyForTextWithoutHeadings()
    {
        var result = PdfIngestionService.SplitByHeadings("apenas texto sem qualquer cabeçalho.");
        result.Should().BeEmpty();
    }
}
