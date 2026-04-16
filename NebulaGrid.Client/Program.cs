using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NebulaGrid.Client;
using NebulaGrid.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hostBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = hostBaseAddress;

if (hostBaseAddress.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
	&& hostBaseAddress.Port == 5028)
{
	apiBaseAddress = new Uri("http://localhost:5237/");
}

if (hostBaseAddress.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
	&& hostBaseAddress.Port == 7255)
{
	apiBaseAddress = new Uri("https://localhost:7162/");
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddSingleton<ShipService>();
builder.Services.AddSingleton<PlayerService>();
builder.Services.AddSingleton<GameStatus>();

await builder.Build().RunAsync();
