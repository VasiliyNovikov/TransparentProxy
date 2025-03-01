using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using TransparentProxyDemo.Certificates;

const string defaultDomainName = "localhost";

var certPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "../../../../TransparentProxyDemo.pfx"));
X509Certificate2 rootCa;
if (!File.Exists(certPath))
{
    Console.WriteLine($"Certificate does not exist at {certPath}. Creating new certificate...");
    rootCa = CertificateExtensions.CreateRootCA("TransparentProxyDemo", validityYears: 10);
    File.WriteAllBytes(certPath, rootCa.ToPfxBytes());
}
else
{
    Console.WriteLine($"Certificate exists at {certPath}");
    rootCa = CertificateExtensions.FromPfxBytes(File.ReadAllBytes(certPath));
}

ConcurrentDictionary<string, X509Certificate2> domainCertificates = new(StringComparer.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificateSelector = (_, domainName) =>
            {
                var effectiveDomainName = domainName ?? defaultDomainName;
                if (!domainCertificates.TryGetValue(effectiveDomainName, out var cert))
                {
                    cert = rootCa.GenerateDomainCertificate(effectiveDomainName);
                    domainCertificates[effectiveDomainName] = cert;
                }
                return cert;
            };
        });
    });
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();