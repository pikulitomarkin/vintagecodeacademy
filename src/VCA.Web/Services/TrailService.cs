using System.Net.Http.Json;

namespace VCA.Web.Services;

/// <summary>
/// Serviço Blazor para consumo dos endpoints de trilhas da VCA API.
/// </summary>
public class TrailService
{
    private readonly HttpClient _http;

    public TrailService(HttpClient http) => _http = http;

    public async Task<List<TrailDto>?> GetTrailsAsync()
        => await _http.GetFromJsonAsync<List<TrailDto>>("api/trails");

    public record TrailDto(Guid Id, string Title, string Description, string Stack, string Level);
}
