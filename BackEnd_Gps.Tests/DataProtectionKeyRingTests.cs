using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BackEnd_Gps.Tests;

public sealed class DataProtectionKeyRingTests
{
    [Fact]
    public void CertificateRotation_PreviousCertificateCanDecryptExistingKeyRingAndPayload()
    {
        var keyRingPath = Path.Combine(Path.GetTempPath(), $"fletflow-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyRingPath);

        using var previousCertificate = CreateCertificate("CN=FletFlow Data Protection Previous Test");
        using var currentCertificate = CreateCertificate("CN=FletFlow Data Protection Current Test");

        try
        {
            string protectedPayload;
            using (var firstProvider = CreateProvider(keyRingPath, previousCertificate))
            {
                var protector = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("FletFlow.Tests.EmailOutbox");
                protectedPayload = protector.Protect("private-reset-token-payload");
            }

            var keyFile = Assert.Single(Directory.GetFiles(keyRingPath, "*.xml"));
            var persistedKey = File.ReadAllText(keyFile);
            Assert.Contains("<encryptedSecret", persistedKey, StringComparison.Ordinal);

            using (var rotationProvider = CreateProvider(keyRingPath, currentCertificate, previousCertificate))
            {
                var rotationProtector = rotationProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("FletFlow.Tests.EmailOutbox");

                Assert.Equal("private-reset-token-payload", rotationProtector.Unprotect(protectedPayload));

                // New keys must use the current certificate, while the previous one remains
                // available only as a decryption fallback for keys already in the ring.
                rotationProvider.GetRequiredService<IKeyManager>().CreateNewKey(
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow.AddDays(90));
            }

            // Restart so the provider's in-memory key ring includes the newly persisted key.
            using var rotatedProvider = CreateProvider(keyRingPath, currentCertificate, previousCertificate);
            var rotatedProtector = rotatedProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("FletFlow.Tests.EmailOutbox");
            var rotatedPayload = rotatedProtector.Protect("payload-created-after-rotation");
            Assert.Equal("payload-created-after-rotation", rotatedProtector.Unprotect(rotatedPayload));

            var keyFiles = Directory.GetFiles(keyRingPath, "*.xml");
            Assert.Equal(2, keyFiles.Length);
            Assert.All(keyFiles, keyFile => Assert.Contains(
                "<encryptedSecret",
                File.ReadAllText(keyFile),
                StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(keyRingPath, recursive: true);
        }
    }

    private static X509Certificate2 CreateCertificate(string subject)
    {
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static ServiceProvider CreateProvider(
        string keyRingPath,
        X509Certificate2 currentCertificate,
        params X509Certificate2[] previousCertificates)
    {
        var builder = new ServiceCollection();
        builder.AddLogging();
        var dataProtection = builder.AddDataProtection()
            .SetApplicationName("FletFlow.Tests.CertificateProtectedKeyRing")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .ProtectKeysWithCertificate(currentCertificate);
        dataProtection.UnprotectKeysWithAnyCertificate(
            new[] { currentCertificate }.Concat(previousCertificates).ToArray());
        return builder.BuildServiceProvider();
    }
}
