using System.Text;
using UglyToad.PdfPig;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Extração de texto de PDFs usando PdfPig com chunking por número de caracteres.
/// </summary>
public class PdfExtractorService : IPdfExtractorService
{
    public Task<IReadOnlyList<string>> ExtractChunksAsync(
        Stream pdfStream,
        int chunkSize = 1500,
        CancellationToken cancellationToken = default)
    {
        var fullText = new StringBuilder();

        using var pdf = PdfDocument.Open(pdfStream);
        foreach (var page in pdf.GetPages())
        {
            fullText.Append(page.Text);
            fullText.Append(' ');
        }

        var text = fullText.ToString().Trim();
        var chunks = new List<string>();

        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var length = Math.Min(chunkSize, text.Length - i);
            chunks.Add(text.Substring(i, length));
        }

        return Task.FromResult<IReadOnlyList<string>>(chunks);
    }

    public Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var startPosition = pdfStream.CanSeek ? pdfStream.Position : 0L;
        long byteSize = pdfStream.CanSeek ? pdfStream.Length : 0L;

        using var pdf = PdfDocument.Open(pdfStream);
        var pages = new List<PdfPageText>(pdf.NumberOfPages);
        var fullText = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.Text ?? string.Empty;
            pages.Add(new PdfPageText(page.Number, text));
            fullText.Append(text).Append('\n');
        }

        if (pdfStream.CanSeek) pdfStream.Position = startPosition;

        var result = new PdfExtractionResult(
            FullText: fullText.ToString().Trim(),
            PageCount: pdf.NumberOfPages,
            ByteSize: byteSize,
            Pages: pages);

        return Task.FromResult(result);
    }
}
