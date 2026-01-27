using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ResetYourFuture.Client;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Client.Services;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7003";

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- Localization (required for culture-aware InputDate, formatting, validation) ---
builder.Services.AddLocalization();

// --- LocalStorage for token persistence ---
builder.Services.AddBlazoredLocalStorage();

// --- Auth State Provider ---
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

// --- Auth Header Handler (attaches JWT to requests) ---
builder.Services.AddScoped<AuthHeaderHandler>();

// --- Auth Service ---
builder.Services.AddScoped<IAuthService, AuthService>();

// --- HttpClient with auth header for API calls ---
builder.Services.AddHttpClient("AuthenticatedApi", c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("AuthenticatedApi");
});

// --- Course Service ---
builder.Services.AddHttpClient<ICourseService, CourseService>(c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHeaderHandler>();

// --- Authorization ---
builder.Services.AddAuthorizationCore();

// --- Set cultures and enforce short date format dd/MM/yy ---
// Apply this BEFORE builder.Build().RunAsync()
 

var supportedCultures = new[]
{
    new CultureInfo("en-GB"),
    new CultureInfo("el-GR")
};
var defaultCulture = supportedCultures[0];

CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
 

await builder.Build().RunAsync();
