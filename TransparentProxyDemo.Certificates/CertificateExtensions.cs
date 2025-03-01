using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TransparentProxyDemo.Certificates;

public static class CertificateExtensions
{
    private const X509KeyStorageFlags LoadFlags =  X509KeyStorageFlags.UserKeySet
                                                 | X509KeyStorageFlags.PersistKeySet
                                                 | X509KeyStorageFlags.Exportable;
    private const int DefaultKeySize = 2048;
    private const int DefaultRootCaValidityYears = 10;
    private const int DefaultDomainCertValidityYears = 2;
    private static readonly HashAlgorithmName DefaultHashAlgorithm = HashAlgorithmName.SHA256;
    private static readonly RSASignaturePadding DefaultSignaturePadding = RSASignaturePadding.Pss;

    public static byte[] ToPfxBytes(this X509Certificate2 cert) => cert.Export(X509ContentType.Pfx);

    public static X509Certificate2 FromPfxBytes(byte[] pfx) => X509CertificateLoader.LoadPkcs12(pfx, null, LoadFlags);

    public static X509Certificate2 DeepClone(this X509Certificate2 cert) => FromPfxBytes(cert.ToPfxBytes());

    public static X509Certificate2 CreateRootCA(string subjectName, int keySize = DefaultKeySize, int validityYears = DefaultRootCaValidityYears)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest($"CN={subjectName}", rsa, DefaultHashAlgorithm, DefaultSignaturePadding);

        // Mark certificate as a CA with Basic Constraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

        // (Optional) Add KeyUsage extension so it can sign child certificates
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

        // Subject Key Identifier
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, X509SubjectKeyIdentifierHashAlgorithm.Sha1, critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter  = notBefore.AddYears(validityYears);
        using var rootCa = request.CreateSelfSigned(notBefore, notAfter);

        return rootCa.DeepClone();
    }

    public static X509Certificate2 GenerateDomainCertificate(this X509Certificate2 rootCa, string domainName, int validityYears = DefaultDomainCertValidityYears, int keySize = DefaultKeySize)
    {
        using var rsa = RSA.Create(keySize);

        var request = new CertificateRequest($"CN={domainName}", rsa, DefaultHashAlgorithm, DefaultSignaturePadding);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domainName);  
        request.CertificateExtensions.Add(sanBuilder.Build());

        var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // TLS Web Server Authentication
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter  = notBefore.AddYears(validityYears);

        var serialNumber = Guid.NewGuid().ToByteArray();

        using var issuedCertWithoutKey = request.Create(issuerCertificate: rootCa, notBefore: notBefore, notAfter: notAfter, serialNumber: serialNumber);
        using var finalCert = issuedCertWithoutKey.CopyWithPrivateKey(rsa);

        return finalCert.DeepClone();
    }
}