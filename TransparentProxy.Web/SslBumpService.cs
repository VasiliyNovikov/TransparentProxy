using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using TransparentProxy.Certificates;

namespace TransparentProxy.Web;

internal sealed class SslBumpService(string rootCaPath, string defaultDomainName, TimeSpan domainCertLifetime) : IHttpsCertificateSelector
{
    internal const string DefaultDomainName = "localhost";
    internal const int DefaultDomainCertLifetimeMinutes = 15;
    private const int DomainCertLifetimeGracePeriodMinutes = 5;

    private readonly X509Certificate2 _rootCa = CertificateExtensions.FromPfxBytes(File.ReadAllBytes(rootCaPath));
    private readonly ConcurrentDictionary<string, (X509Certificate2 Cert, DateTime ExpirationTime)> _domainCertificates = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _domainCertEffectiveLifetime = domainCertLifetime + TimeSpan.FromMinutes(DomainCertLifetimeGracePeriodMinutes);
    
    public X509Certificate2 GetCertificate(string? domainName)
    {
        var effectiveDomainName = domainName ?? defaultDomainName;
        if (!_domainCertificates.TryGetValue(effectiveDomainName, out var certBucket) || certBucket.ExpirationTime < DateTime.UtcNow)
        {
            var cert = _rootCa.GenerateDomainCertificate(effectiveDomainName, _domainCertEffectiveLifetime);
            _domainCertificates[effectiveDomainName] = certBucket = (cert, DateTime.UtcNow);
        }
        return certBucket.Cert;
    }
}

public static class SslBumpServiceExtensions
{
    public static IServiceCollection AddSslBump(this IServiceCollection services,
                                                string rootCaPath,
                                                string defaultDomainName = SslBumpService.DefaultDomainName,
                                                int domainCertLifetimeMinutes = SslBumpService.DefaultDomainCertLifetimeMinutes)
    {
        return services.AddSingleton<IHttpsCertificateSelector>(_ => new SslBumpService(rootCaPath, defaultDomainName, TimeSpan.FromMinutes(domainCertLifetimeMinutes)));
    }
}