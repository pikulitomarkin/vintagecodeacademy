using Microsoft.AspNetCore.Components.Authorization;
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

const string ApiClientName = "VCA.API";

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
builder.Services.AddScoped<AuthenticationStateProvider, VcaAuthenticationStateProvider>();

builder.Services.AddHttpClient(ApiClientName, client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient(ApiClientName));

// MudBlazor
builder.Services.AddMudServices();

// Serviços da camada Web
builder.Services.AddHttpClient<TrailService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddHttpClient<GamificationService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddHttpClient<RankingService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddHttpClient<CourseHttpService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddHttpClient<UserHttpService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

builder.Services.AddHttpClient<AdminHttpService>(client =>
	{
		client.BaseAddress = new Uri(apiBaseUrl);
		client.Timeout = TimeSpan.FromMinutes(10); // SSE de longa duração
	})
	.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

await builder.Build().RunAsync();
