using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TransparentProxyDemo.Certificates;

namespace TransparentProxyDemo.Tests;

[TestClass]
public class CertificateExtensionsTests
{
    [TestMethod]
    public void CreateRootCertificateAuthority_Test()
    {
        const string subjectName = "Test CA";
        const int keySize = 4096;
        const int validityDays = 7;

        using var rootCa = CertificateExtensions.CreateRootCA(subjectName, validityDays, keySize);

        Assert.IsTrue(rootCa.HasPrivateKey);
        Assert.AreEqual(rootCa.Subject, rootCa.Issuer);
        Assert.AreEqual($"CN={subjectName}", rootCa.Subject);
        Assert.IsTrue(rootCa.NotBefore < DateTimeOffset.UtcNow);
        Assert.IsTrue(rootCa.NotAfter > DateTimeOffset.UtcNow);
        Assert.IsTrue(rootCa.NotAfter < DateTimeOffset.UtcNow.AddDays(validityDays));
        Assert.IsTrue(rootCa.Extensions.Count > 0);
    }

    [TestMethod]
    public void GenerateDomainCertificate_Test()
    {
        const string rootCaSubjectName = "Test CA";
        const string domainName = "test.com";
        const int rootCaKeySize = 4096;
        const int rootCaValidityDays = 7;
        const int domainCertValidityMinutes = 10;
        const int domainCertKeySize = 3072;

        using var rootCa = CertificateExtensions.CreateRootCA(rootCaSubjectName, rootCaValidityDays, rootCaKeySize);
        using var domainCert = rootCa.GenerateDomainCertificate(domainName, TimeSpan.FromMinutes(domainCertValidityMinutes), domainCertKeySize);

        Assert.IsTrue(domainCert.HasPrivateKey);
        Assert.AreEqual(rootCa.Subject, domainCert.Issuer);
        Assert.AreEqual($"CN={domainName}", domainCert.Subject);
        Assert.IsTrue(domainCert.NotBefore < DateTimeOffset.UtcNow);
        Assert.IsTrue(domainCert.NotAfter > DateTimeOffset.UtcNow);
        Assert.IsTrue(domainCert.NotAfter < DateTimeOffset.UtcNow.AddMinutes(domainCertValidityMinutes));
        Assert.IsTrue(domainCert.Extensions.Count > 0);
    }
}