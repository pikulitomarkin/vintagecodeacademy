namespace VCA.Application.AI.GenerateLessonFromPdf;

/// <summary>
/// Comando para processar um PDF e gerar o conteúdo gamificado de uma aula via DeepSeek.
/// Fluxo: upload PDF → PdfPig chunking → DeepSeek API → JSON gamificado → revisão admin.
/// </summary>
public record GenerateLessonFromPdfCommand(
    Guid LessonId,
    Stream PdfStream,
    string PdfFileName
);
