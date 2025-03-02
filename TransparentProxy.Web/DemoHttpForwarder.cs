using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using DnsClient;
using Microsoft.Extensions.Logging;
using TransparentProxy.Forwarder;

namespace TransparentProxy.Web;

public class DemoHttpForwarder(ILogger<DemoHttpForwarder> logger) : HttpForwarder(logger)
{
    private readonly LookupClient _lookupClient = new(IPAddress.Parse("8.8.8.8"));

    protected override void ConfigureSocketsHandler(SocketsHttpHandler handler)
    {
        base.ConfigureSocketsHandler(handler);
        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;
            var dnsResponse = await _lookupClient.QueryAsync(host, QueryType.A, QueryClass.IN, cancellationToken).ConfigureAwait(false);
            var address = dnsResponse.Answers.ARecords().FirstOrDefault()?.Address;
            if (address is null)
                throw new SocketException((int)SocketError.HostNotFound, $"Could not resolve host '{host}'");

            var port = context.DnsEndPoint.Port;
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        };
    }
}