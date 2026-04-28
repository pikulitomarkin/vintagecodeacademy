using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VCA.Application.AI.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;

namespace VCA.Application.AI.Services;

/// <summary>
/// Serviço de ingestão de PDFs: validação de tamanho/páginas e chunking inteligente
/// (preferindo cabeçalhos markdown e parágrafos como pontos de quebra).
/// </summary>
public sealed class PdfIngestionService
{
    public const long MaxBytes = 50L * 1024 * 1024; // 50 MB
    public const int MaxPages = 500;
    public const int TargetTokensPerChunk = 800;
    public const int MinTokensPerChunk = 200;
    public const int MaxTokensPerChunk = 1200;

    // Aproximação: 1 token ≈ 4 caracteres em inglês / 3 em português técnico misturado.
    private const double CharsPerToken = 3.5;

    private static readonly Regex HeadingRegex = new(@"^\s{0,3}(#{1,6})\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ParagraphSplitRegex = new(@"\r?\n\s*\r?\n", RegexOptions.Compiled);

    private readonly IPdfExtractorService _extractor;
    private readonly ILogger<PdfIngestionService> _logger;

    public PdfIngestionService(IPdfExtractorService extractor, ILogger<PdfIngestionService> logger)
    {
        _extractor = extractor;
        _logger = logger;
    }

    public async Task<PdfIngestionResult> IngestAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Nome do arquivo é obrigatório.", "pdf.filename_missing");

        if (pdfStream.CanSeek && pdfStream.Length > MaxBytes)
            throw new DomainException(
                $"PDF excede o tamanho máximo de {MaxBytes / (1024 * 1024)} MB. Tamanho: {pdfStream.Length / (1024 * 1024)} MB.",
                "pdf.too_large");

        var extraction = await _extractor.ExtractAsync(pdfStream, cancellationToken);

        if (extraction.PageCount > MaxPages)
            throw new DomainException(
                $"PDF excede o número máximo de {MaxPages} páginas. Páginas: {extraction.PageCount}.",
                "pdf.too_many_pages");

        if (string.IsNullOrWhiteSpace(extraction.FullText))
            throw new DomainException("PDF não contém texto extraível (provável documento escaneado sem OCR).", "pdf.no_text");

        var chunks = BuildChunks(extraction.FullText);

        _logger.LogInformation(
            "PDF ingerido: file={File} pages={Pages} bytes={Bytes} chunks={Chunks}",
            fileName, extraction.PageCount, extraction.ByteSize, chunks.Count);

        return new PdfIngestionResult(chunks, extraction.PageCount, extraction.ByteSize, fileName);
    }

    /// <summary>
    /// Divide o texto em chunks preferindo cabeçalhos markdown como pontos de quebra.
    /// Caso não existam cabeçalhos, divide por parágrafos respeitando o alvo de tokens.
    /// </summary>
    internal static IReadOnlyList<PdfChunk> BuildChunks(string fullText)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return Array.Empty<PdfChunk>();

        var sections = SplitByHeadings(fullText);
        if (sections.Count == 0 || sections.All(s => EstimateTokens(s) < MinTokensPerChunk * 2))
            sections = SplitByParagraphs(fullText);

        var chunks = new List<PdfChunk>();
        var buffer = new StringBuilder();
        int bufferTokens = 0;
        int index = 0;

        void Flush()
        {
            if (buffer.Length == 0) return;
            var text = buffer.ToString().Trim();
            if (text.Length > 0)
            {
                chunks.Add(new PdfChunk(index++, text, EstimateTokens(text)));
            }
            buffer.Clear();
            bufferTokens = 0;
        }

        foreach (var section in sections)
        {
            var sectionTokens = EstimateTokens(section);

            // Seção gigante: subdividir por parágrafos
            if (sectionTokens > MaxTokensPerChunk)
            {
                Flush();
                foreach (var para in SplitByParagraphs(section))
                {
                    var paraTokens = EstimateTokens(para);
                    if (bufferTokens + paraTokens > MaxTokensPerChunk && bufferTokens >= MinTokensPerChunk)
                        Flush();
                    if (buffer.Length > 0) buffer.Append("\n\n");
                    buffer.Append(para);
                    bufferTokens += paraTokens;
                }
                continue;
            }

            if (bufferTokens + sectionTokens > MaxTokensPerChunk && bufferTokens >= MinTokensPerChunk)
                Flush();

            if (buffer.Length > 0) buffer.Append("\n\n");
            buffer.Append(section);
            bufferTokens += sectionTokens;

            if (bufferTokens >= TargetTokensPerChunk)
                Flush();
        }

        Flush();
        return chunks;
    }

    internal static List<string> SplitByHeadings(string text)
    {
        var matches = HeadingRegex.Matches(text);
        if (matches.Count == 0) return new List<string>();

        var parts = new List<string>();
        int previousStart = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            if (m.Index > previousStart)
            {
                var slice = text.Substring(previousStart, m.Index - previousStart).Trim();
                if (!string.IsNullOrWhiteSpace(slice)) parts.Add(slice);
            }
            previousStart = m.Index;
        }
        var tail = text[previousStart..].Trim();
        if (!string.IsNullOrWhiteSpace(tail)) parts.Add(tail);
        return parts;
    }

    internal static List<string> SplitByParagraphs(string text)
    {
        return ParagraphSplitRegex
            .Split(text)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    internal static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / CharsPerToken);
}
