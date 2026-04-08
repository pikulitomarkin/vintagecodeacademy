using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VCA.Web;
using VCA.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// URL base da API — configurada via appsettings ou environment
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";

// HttpClient principal para a VCA API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// MudBlazor
builder.Services.AddMudServices();

// Serviços da camada Web
builder.Services.AddScoped<TrailService>();
builder.Services.AddScoped<GamificationService>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddScoped<AuthService>();

await builder.Build().RunAsync();
