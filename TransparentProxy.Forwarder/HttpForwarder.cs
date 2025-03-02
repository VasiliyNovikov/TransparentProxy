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
    private static readonly FrozenSet<string> ExcludedRequestHeaders = new[] { "Host", "Accept-Encoding" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> ExcludedResponseHeaders = new[] { "Transfer-Encoding", "Connection" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    protected virtual void ConfigureSocketsHandler(SocketsHttpHandler handler) { }

    protected virtual async Task<HttpResponseMessage> Forward(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
    {
        var baseAddress = requestMessage.RequestUri!.GetLeftPart(UriPartial.Authority);
        var client = GetClient(baseAddress);
        return await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public async Task Forward(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        try
        {
            var requestMessage = CreateRequest(context);

            LogRequest(requestMessage);

            var response = await Forward(requestMessage, cancellationToken);

            LogResponse(response);

            await WriteResponse(context, response, cancellationToken);

            logger.LogInformation("Forwarding completed");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Forwarding request was canceled");
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpContext context)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host.Host;
        var port = context.Request.Host.Port;
        var path = context.Request.Path;
        var query = context.Request.QueryString;
        var url = $"{scheme}://{host}{(port.HasValue ? $":{port}" : "")}{path}{query}";

        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(url),
            Content = new StreamContent(context.Request.Body)
        };
        foreach (var (name, value) in context.Request.Headers)
            if (!ExcludedRequestHeaders.Contains(name))
                requestMessage.Headers.TryAddWithoutValidation(name, (IEnumerable<string?>)value);
        
        return requestMessage;
    }

    private static async Task WriteResponse(HttpContext context, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.ToString();
        var contentLength = response.Content.Headers.ContentLength;

        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var (name, value) in response.Headers)
            if (!ExcludedResponseHeaders.Contains(name))
                context.Response.Headers[name] = new([.. value]);

        context.Response.ContentType = contentType;
        context.Response.ContentLength = contentLength;

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await responseStream.CopyToAsync(context.Response.Body, cancellationToken);
    }

    private HttpClient GetClient(string baseAddress) => _clients.GetOrAdd(baseAddress, CreateClient);

    private HttpClient CreateClient(string baseAddress)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        ConfigureSocketsHandler(handler);
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    private void LogRequest(HttpRequestMessage requestMessage)
    {
        StringBuilder requestMessageBuilder = new();
        requestMessageBuilder.AppendLine("Forwarding request:");
        requestMessageBuilder.AppendLine($"  Method: {requestMessage.Method}");
        requestMessageBuilder.AppendLine($"  Url: {requestMessage.RequestUri}");
        requestMessageBuilder.AppendLine("  Headers:");
        foreach (var (name, value) in requestMessage.Headers)
            requestMessageBuilder.AppendLine($"    {name}: {string.Join(", ", value)}");
        logger.LogInformation(requestMessageBuilder.ToString());
    }

    private void LogResponse(HttpResponseMessage response)
    {
        StringBuilder responseMessageBuilder = new();
        responseMessageBuilder.AppendLine("Forwarding response:");
        responseMessageBuilder.AppendLine($"  StatusCode: {(int)response.StatusCode}");
        responseMessageBuilder.AppendLine($"  ContentType: {response.Content.Headers.ContentType}");
        responseMessageBuilder.AppendLine($"  ContentLength: {response.Content.Headers.ContentLength}");
        responseMessageBuilder.AppendLine("  Headers:");
        foreach (var (name, value) in response.Headers)
            responseMessageBuilder.AppendLine($"    {name}: {string.Join(", ", value)}");

        var contentType = response.Content.Headers.ContentType?.ToString();
        if (contentType is not null)
            responseMessageBuilder.AppendLine($"    Content-Type: {contentType}");
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is not null)
            responseMessageBuilder.AppendLine($"    Content-Length: {contentLength}");
        logger.LogInformation(responseMessageBuilder.ToString());
    }
}