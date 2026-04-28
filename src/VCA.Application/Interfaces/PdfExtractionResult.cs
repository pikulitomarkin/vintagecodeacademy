namespace VCA.Application.Interfaces;

/// <summary>
/// Resultado bruto da extração de um PDF.
/// </summary>
public sealed record PdfExtractionResult(
    string FullText,
    int PageCount,
    long ByteSize,
    IReadOnlyList<PdfPageText> Pages);

public sealed record PdfPageText(int PageNumber, string Text);
