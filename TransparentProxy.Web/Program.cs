using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using TransparentProxy.Forwarder;
using TransparentProxy.Web;

var certPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "../../../../TransparentProxy.pfx"));

var builder = WebApplication.CreateBuilder(args);
builder.Services.UseHttpsCertificateSelector();
builder.Services.AddSslBump(certPath);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps());
});

var app = builder.Build();

app.MapGet("api/hello", () => "Hello from Transparent Proxy!").RequireHost("localhost");

app.MapFallback(HttpForwarder.Invoke);

app.Run();