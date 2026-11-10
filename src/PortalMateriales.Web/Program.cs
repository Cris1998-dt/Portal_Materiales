using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PortalMateriales.Web;
using PortalMateriales.Web.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// La UI se sirve desde la misma API: mismo origen, sin CORS.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Sesion>();
builder.Services.AddScoped<Api>();
builder.Services.AddScoped<Avisos>();

var host = builder.Build();
await host.Services.GetRequiredService<Sesion>().CargarAsync();
await host.RunAsync();
