namespace VCA.Application.AI.Common;

/// <summary>
/// Fragmento de texto extraído de um PDF com metadados para o pipeline de IA.
/// </summary>
public sealed record PdfChunk(
    int ChunkIndex,
    string RawText,
    int EstimatedTokenCount,
    int? StartPage = null,
    int? EndPage = null);

/// <summary>
/// Resultado da ingestão de um PDF — chunks + metadados do arquivo.
/// </summary>
public sealed record PdfIngestionResult(
    IReadOnlyList<PdfChunk> Chunks,
    int PageCount,
    long ByteSize,
    string FileName);
