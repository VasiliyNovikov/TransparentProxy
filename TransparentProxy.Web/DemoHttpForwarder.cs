using System;
using Microsoft.Extensions.Logging;
using TransparentProxy.Forwarder;

namespace TransparentProxy.Web;

public class DemoHttpForwarder(ILogger<DemoHttpForwarder> logger) : HttpForwarder(logger)
{
    protected override string GetForwardedHost(string host) => host.EndsWith(".proxy", StringComparison.OrdinalIgnoreCase) ? host[..^6] : "unknown.com";
}