using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ResetYourFuture.Client;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Client.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7003";

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// typed HttpClient for the API consumer
builder.Services.AddHttpClient<IStudentService, StudentConsumer>(c => c.BaseAddress = new Uri(apiBase));

await builder.Build().RunAsync();
