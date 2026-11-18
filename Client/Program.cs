using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Client;
var host = WebAssemblyHostBuilder.CreateDefault(args);
host.RootComponents.Add<App>("#app");
await host.Build().RunAsync();
