using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace TransparentProxy.Proxy;

public static class HttpProxyService
{
    public static async Task Invoke(HttpContext context)
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
    }
}