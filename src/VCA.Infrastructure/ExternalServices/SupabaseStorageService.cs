using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Gerenciamento de arquivos no Supabase Storage (PDFs e avatares).
/// </summary>
public class SupabaseStorageService : IStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(HttpClient httpClient, IConfiguration config, ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var supabaseUrl = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url não configurado.");
        var serviceKey = config["Supabase:ServiceKey"]
            ?? throw new InvalidOperationException("Supabase:ServiceKey não configurado.");

        _baseUrl = $"{supabaseUrl}/storage/v1/object";
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");
        _httpClient.DefaultRequestHeaders.Add("apikey", serviceKey);
    }

    public async Task<string> UploadPdfAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var path = $"pdfs/{Guid.NewGuid()}-{fileName}";
        return await UploadAsync("vca-pdfs", path, content, "application/pdf", cancellationToken);
    }

    public async Task<string> UploadAvatarAsync(Guid userId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = $"avatars/{userId}";
        return await UploadAsync("vca-avatars", path, content, contentType, cancellationToken);
    }

    public async Task DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"{_baseUrl}/{filePath}", cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Arquivo '{Path}' deletado do storage.", filePath);
    }

    private async Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await _httpClient.PostAsync($"{_baseUrl}/{bucket}/{path}", streamContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Arquivo carregado para '{Bucket}/{Path}'.", bucket, path);
        return $"{_baseUrl}/public/{bucket}/{path}";
    }
}
