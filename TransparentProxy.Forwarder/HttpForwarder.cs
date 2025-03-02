#pragma warning disable CA1848
#pragma warning disable CA2254
#pragma warning disable CA1305
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TransparentProxy.Forwarder;

public class HttpForwarder(ILogger<HttpForwarder> logger)
{
    private static readonly FrozenSet<string> ExcludedRequestHeaders = new[]
    {
        "Host",
        "Accept-Encoding"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> ExcludedResponseHeaders = new[]
    {
        "Transfer-Encoding",
        "Connection"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public async Task Forward(HttpContext context)
    {
        try
        {
            await Forward(context, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Forwarding request was canceled");
            throw;
        }
    }

    private async Task Forward(HttpContext context, CancellationToken cancellationToken)
    {
        var scheme = context.Request.Scheme;
        var protocol = context.Request.Protocol;
        var method = context.Request.Method;
        var host = context.Request.Host.Host;
        var port = context.Request.Host.Port;
        var baseAddress = $"{scheme}://{host}{(port.HasValue ? $":{port}" : "")}";
        var path = context.Request.Path;
        var query = context.Request.QueryString;
        var pathAndQuery = $"{path}{query}";
        var requestHeaders = context.Request.Headers;

        StringBuilder requestMessageBuilder = new();
        requestMessageBuilder.AppendLine("Forwarding request:");
        requestMessageBuilder.AppendLine($"  RequestProtocol: {protocol}");
        requestMessageBuilder.AppendLine($"  Base Address: {baseAddress}");
        requestMessageBuilder.AppendLine($"  Method: {method}");
        requestMessageBuilder.AppendLine($"  Path: {pathAndQuery}");
        requestMessageBuilder.AppendLine("  Headers:");
        foreach (var (name, value) in requestHeaders)
            requestMessageBuilder.AppendLine($"    {name}: {string.Join(", ", (IEnumerable<string?>)value)}");
        logger.LogInformation(requestMessageBuilder.ToString());

        var client = GetClient(baseAddress);
        
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(method),
            RequestUri = new Uri(pathAndQuery, UriKind.Relative),
            Content = new StreamContent(context.Request.Body)
        };
        
        foreach (var (name, value) in requestHeaders)
            if (!ExcludedRequestHeaders.Contains(name))
                requestMessage.Headers.TryAddWithoutValidation(name, (IEnumerable<string?>)value);
        
        var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        var statusCode = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.ToString();
        var contentLength = response.Content.Headers.ContentLength;
        
        StringBuilder responseMessageBuilder = new();
        responseMessageBuilder.AppendLine("Forwarding response:");
        responseMessageBuilder.AppendLine($"  StatusCode: {statusCode}");
        responseMessageBuilder.AppendLine($"  ContentType: {contentType}");
        responseMessageBuilder.AppendLine($"  ContentLength: {contentLength}");
        responseMessageBuilder.AppendLine("  Headers:");
        foreach (var (name, value) in response.Headers)
            responseMessageBuilder.AppendLine($"    {name}: {string.Join(", ", value)}");
        logger.LogInformation(responseMessageBuilder.ToString());

        context.Response.StatusCode = statusCode;
        foreach (var (name, value) in response.Headers)
            if (!ExcludedResponseHeaders.Contains(name))
                context.Response.Headers[name] = new([.. value]);

        context.Response.ContentType = contentType;
        context.Response.ContentLength = contentLength;

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await responseStream.CopyToAsync(context.Response.Body, cancellationToken);

        logger.LogInformation("Forwarding completed");
    }

    private HttpClient GetClient(string baseAddress) => _clients.GetOrAdd(baseAddress, CreateClient);

    private HttpClient CreateClient(string baseAddress)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        ConfigureSocketsHandler(handler);
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    protected virtual void ConfigureSocketsHandler(SocketsHttpHandler handler) { }
}