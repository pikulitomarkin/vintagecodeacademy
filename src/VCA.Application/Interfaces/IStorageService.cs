namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato para upload e gerenciamento de arquivos no Supabase Storage.
/// </summary>
public interface IStorageService
{
    /// <summary>Faz upload de um PDF para o bucket de PDFs.</summary>
    Task<string> UploadPdfAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Faz upload de um avatar de usuário.</summary>
    Task<string> UploadAvatarAsync(Guid userId, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deleta um arquivo pelo caminho no bucket.</summary>
    Task DeleteAsync(string filePath, CancellationToken cancellationToken = default);
}
