using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TransparentProxyDemo;

public interface IHttpsCertificateSelector
{
    X509Certificate2 GetCertificate(string? domainName);
}

public static class HttpsCertificateSelectorExtensions
{
    public static void UseHttpsCertificateSelector(this IServiceCollection services) => services.ConfigureOptions<ConfigureOptions>();

    private sealed class ConfigureOptions(IHttpsCertificateSelector selector) : IConfigureOptions<KestrelServerOptions>
    {
        public void Configure(KestrelServerOptions options) => options.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificateSelector = (_, domainName) => selector.GetCertificate(domainName));
    }
}