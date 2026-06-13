namespace EBAssistant.Tests;

internal static class AuthorizationCryptoTests
{
    public static void Run()
    {
        VerifiesManagerPasswordWithoutPlainTextConstant();
        LicenseFileDoesNotExposePlainMachineData();
        ValidatesLicenseAgainstMachineAndExpiry();
        RejectsMismatchedExpiredAndTamperedLicense();
    }

    private static void VerifiesManagerPasswordWithoutPlainTextConstant()
    {
        var password = new string(['E', 'B', 'A', 's', 's', 'i', 's', 't', 'a', 'n', 't']);

        Assert.True(AuthorizationCrypto.VerifyManagerPassword(password));
        Assert.True(AuthorizationCrypto.VerifyManagerPassword("prefix-" + password + "-suffix"));
        Assert.True(AuthorizationCrypto.VerifyManagerPassword("prefix-" + password + "X"));
        Assert.Equal(false, AuthorizationCrypto.VerifyManagerPassword("wrong-password"));
        Assert.Equal(false, AuthorizationCrypto.VerifyManagerPassword("prefix-EBAssistan"));
    }

    private static void LicenseFileDoesNotExposePlainMachineData()
    {
        var text = AuthorizationCrypto.CreateLicenseFileText(
            "TEST-COMPUTER",
            "AA-BB-CC-DD-EE-FF",
            new DateTime(2026, 12, 31));

        Assert.True(text.Contains("AES-CBC-HMACSHA256"));
        Assert.True(text.Contains("CipherText"));
        Assert.Equal(false, text.Contains("TEST-COMPUTER"));
        Assert.Equal(false, text.Contains("AA-BB-CC-DD-EE-FF"));
        Assert.Equal(false, text.Contains("2026-12-31"));
    }

    private static void ValidatesLicenseAgainstMachineAndExpiry()
    {
        var path = WriteTemporaryLicense(
            "TEST-COMPUTER",
            "AA-BB-CC-DD-EE-FF",
            new DateTime(2026, 12, 31));

        try
        {
            var result = AuthorizationCrypto.ValidateLicenseFile(
                path,
                "test-computer",
                ["aa:bb:cc:dd:ee:ff"],
                new DateTime(2026, 1, 1));

            Assert.True(result.IsValid);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RejectsMismatchedExpiredAndTamperedLicense()
    {
        var path = WriteTemporaryLicense(
            "TEST-COMPUTER",
            "AA-BB-CC-DD-EE-FF",
            new DateTime(2026, 12, 31));

        try
        {
            Assert.Equal(false, AuthorizationCrypto.ValidateLicenseFile(
                path,
                "OTHER-COMPUTER",
                ["AA-BB-CC-DD-EE-FF"],
                new DateTime(2026, 1, 1)).IsValid);

            Assert.Equal(false, AuthorizationCrypto.ValidateLicenseFile(
                path,
                "TEST-COMPUTER",
                ["11-22-33-44-55-66"],
                new DateTime(2026, 1, 1)).IsValid);

            Assert.Equal(false, AuthorizationCrypto.ValidateLicenseFile(
                path,
                "TEST-COMPUTER",
                ["AA-BB-CC-DD-EE-FF"],
                new DateTime(2027, 1, 1)).IsValid);

            File.AppendAllText(path, "x");
            Assert.Equal(false, AuthorizationCrypto.ValidateLicenseFile(
                path,
                "TEST-COMPUTER",
                ["AA-BB-CC-DD-EE-FF"],
                new DateTime(2026, 1, 1)).IsValid);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemporaryLicense(string computerName, string macAddress, DateTime expiresOn)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ealic");
        File.WriteAllText(path, AuthorizationCrypto.CreateLicenseFileText(computerName, macAddress, expiresOn));
        return path;
    }
}
