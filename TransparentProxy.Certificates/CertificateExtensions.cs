using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TransparentProxy.Certificates;

public static class CertificateExtensions
{
    private const X509KeyStorageFlags LoadFlags =  X509KeyStorageFlags.UserKeySet
                                                 | X509KeyStorageFlags.PersistKeySet
                                                 | X509KeyStorageFlags.Exportable;
    private const int DefaultKeySize = 2048;
    private const int DefaultRootCaValidityDays = 1000;
    private const int DefaultDomainCertValidityDays = 30;
    private const int ValidityMarginSeconds = 1;
    private static readonly HashAlgorithmName DefaultHashAlgorithm = HashAlgorithmName.SHA256;
    private static readonly RSASignaturePadding DefaultSignaturePadding = RSASignaturePadding.Pss;

    public static byte[] ToPfxBytes(this X509Certificate2 cert) => cert.Export(X509ContentType.Pfx);

    public static X509Certificate2 FromPfxBytes(byte[] pfx) => X509CertificateLoader.LoadPkcs12(pfx, null, LoadFlags);

    public static X509Certificate2 DeepClone(this X509Certificate2 cert) => FromPfxBytes(cert.ToPfxBytes());

    public static X509Certificate2 CreateRootCA(string subjectName, int validityDays = DefaultRootCaValidityDays, int keySize = DefaultKeySize)
    {
        return CreateRootCA(subjectName, TimeSpan.FromDays(validityDays), keySize);
    }

    public static X509Certificate2 CreateRootCA(string subjectName, TimeSpan validity, int keySize = DefaultKeySize)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest($"CN={subjectName}", rsa, DefaultHashAlgorithm, DefaultSignaturePadding);

        // Mark certificate as a CA with Basic Constraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

        // (Optional) Add KeyUsage extension so it can sign child certificates
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

        // Subject Key Identifier
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, X509SubjectKeyIdentifierHashAlgorithm.Sha1, critical: false));

        var (notBefore, notAfter) = GetValidityDates(validity);
        using var rootCa = request.CreateSelfSigned(notBefore, notAfter);

        return rootCa.DeepClone();
    }

    public static X509Certificate2 GenerateDomainCertificate(this X509Certificate2 rootCa, string domainName, int validityDays = DefaultDomainCertValidityDays, int keySize = DefaultKeySize)
    {
        return GenerateDomainCertificate(rootCa, domainName, TimeSpan.FromDays(validityDays), keySize);
    }

    public static X509Certificate2 GenerateDomainCertificate(this X509Certificate2 rootCa, string domainName, TimeSpan validity, int keySize = DefaultKeySize)
    {
        using var rsa = RSA.Create(keySize);

        var request = new CertificateRequest($"CN={domainName}", rsa, DefaultHashAlgorithm, DefaultSignaturePadding);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domainName);  
        request.CertificateExtensions.Add(sanBuilder.Build());

        var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // TLS Web Server Authentication
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: false));

        var (notBefore, notAfter) = GetValidityDates(validity);
        var serialNumber = Guid.NewGuid().ToByteArray();

        using var issuedCertWithoutKey = request.Create(issuerCertificate: rootCa, notBefore: notBefore, notAfter: notAfter, serialNumber: serialNumber);
        using var finalCert = issuedCertWithoutKey.CopyWithPrivateKey(rsa);

        return finalCert.DeepClone();
    }
    
    private static (DateTimeOffset NotBefore, DateTimeOffset NotAfter) GetValidityDates(TimeSpan validity)
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-ValidityMarginSeconds);
        var notAfter  = now + validity;
        return (notBefore, notAfter);
    }
}