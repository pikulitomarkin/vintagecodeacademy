namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato para extração e chunking de texto de arquivos PDF usando PdfPig.
/// </summary>
public interface IPdfExtractorService
{
    /// <summary>
    /// Extrai o texto completo de um PDF e divide em chunks de tamanho configurável.
    /// </summary>
    Task<IReadOnlyList<string>> ExtractChunksAsync(
        Stream pdfStream,
        int chunkSize = 1500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrai o texto completo do PDF página a página, sem chunking.
    /// </summary>
    Task<PdfExtractionResult> ExtractAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}
