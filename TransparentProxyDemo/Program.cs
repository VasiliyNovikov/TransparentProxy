using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using TransparentProxyDemo;

var certPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "../../../../TransparentProxyDemo.pfx"));

var builder = WebApplication.CreateBuilder(args);
builder.Services.UseHttpsCertificateSelector();
builder.Services.AddSslBump(certPath);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps());
});

var app = builder.Build();

app.MapGet("/hello", () => "Hello World!").RequireHost("localhost");

app.MapFallback(async context =>
{
    var protocol = context.Request.Protocol;
    var method = context.Request.Method;
    var path = context.Request.Path;
    var query = context.Request.QueryString;
    
    var requestHeaders = context.Request.Headers;
    string requestBody;
    using (var reader = new StreamReader(context.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    // Just for demonstration: gather the request headers into a string
    // (in a real scenario, you may handle them differently)
    var headerInfo = string.Join("\n", requestHeaders.Select(h => $"  {h.Key}: {h.Value}"));

    // 3. Set response headers
    context.Response.StatusCode = 200;
    context.Response.Headers["X-Custom-Header"] = "MyCustomValue";
    context.Response.ContentType = "text/plain";

    var responseBody = $"Protocol: {protocol}\nMethod: {method}\nPath: {path}\nQuery: {query}\nHeaders:\n{headerInfo}\nBody:\n  {requestBody}";

    await context.Response.WriteAsync(responseBody);
});

app.Run();