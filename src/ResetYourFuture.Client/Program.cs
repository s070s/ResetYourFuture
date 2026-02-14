using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ResetYourFuture.Client;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Client.Services;
using System.Globalization;
using System.Text.RegularExpressions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7003";

// Suppress noisy info-level authorization logs that fire for every
// AuthorizeView evaluation on anonymous users (expected behaviour).
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);

builder.RootComponents.Add<App>(selector: "#app");
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

// Set defaults first; these may be overridden below if a culture is supplied in the URL or cookie.
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var host = builder.Build();

// Read culture from query string (preferred) or from the RequestLocalization cookie, then apply it
try
{
    var js = host.Services.GetRequiredService<IJSRuntime>();

    // Try URL query parameter first: e.g. ?culture=el-GR or ?culture=el
    var requestedCulture = await js.InvokeAsync<string>("eval", "new URL(window.location.href).searchParams.get('culture')");
    if (!string.IsNullOrWhiteSpace(requestedCulture))
    {
        var normalized = requestedCulture.ToLowerInvariant() switch
        {
            "en" => "en-GB",
            "el" => "el-GR",
            _ => requestedCulture
        };

        var ci = new CultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;

        // Remove culture query param from URL to keep it clean
        await js.InvokeVoidAsync("eval", "const u=new URL(window.location.href); u.searchParams.delete('culture'); history.replaceState({}, document.title, u.pathname + u.search);");
    }
    else
    {
        // Fallback: if server set the RequestLocalization cookie for the API host and the cookie is visible on this origin (rare),
        // read it and apply. Cookie format: c=<culture>|uic=<ui-culture>
        var cookieVal = await js.InvokeAsync<string>("eval", "document.cookie.split('; ').find(c=>c.startsWith('__RequestCulture='))?.split('=')[1] ?? null");
        if (!string.IsNullOrWhiteSpace(cookieVal))
        {
            var match = Regex.Match(cookieVal, @"c=([^|;]+)");
            if (match.Success)
            {
                var normalized = match.Groups[1].Value;
                var ci = new CultureInfo(normalized);
                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
        }
    }
}
catch
{
    // If JSInterop fails for any reason, keep the default culture.
}

await host.RunAsync();
