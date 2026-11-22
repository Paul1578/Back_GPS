using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BackEnd_Gps.Tests;

public sealed class DataProtectionKeyRingTests
{
    [Fact]
    public void CertificateProtectedKeyRing_IsEncryptedAndSurvivesProviderRestart()
    {
        var keyRingPath = Path.Combine(Path.GetTempPath(), $"fletflow-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyRingPath);

        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=FletFlow Data Protection Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));

        try
        {
            string protectedPayload;
            using (var firstProvider = CreateProvider(keyRingPath, certificate))
            {
                var protector = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("FletFlow.Tests.EmailOutbox");
                protectedPayload = protector.Protect("private-reset-token-payload");
            }

            var keyFile = Assert.Single(Directory.GetFiles(keyRingPath, "*.xml"));
            var persistedKey = File.ReadAllText(keyFile);
            Assert.Contains("<encryptedSecret", persistedKey, StringComparison.Ordinal);

            using var restartedProvider = CreateProvider(keyRingPath, certificate);
            var restartedProtector = restartedProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("FletFlow.Tests.EmailOutbox");

            Assert.Equal("private-reset-token-payload", restartedProtector.Unprotect(protectedPayload));
        }
        finally
        {
            Directory.Delete(keyRingPath, recursive: true);
        }
    }

    private static ServiceProvider CreateProvider(string keyRingPath, X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection()
            .SetApplicationName("FletFlow.Tests.CertificateProtectedKeyRing")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .ProtectKeysWithCertificate(certificate);
        return services.BuildServiceProvider();
    }
}
