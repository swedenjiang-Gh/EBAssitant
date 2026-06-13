using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class AuthorizationCrypto
{
    private const int PasswordIterations = 150000;
    private const string PasswordSalt = "RUJBc3Npc3RhbnQtMjAyNg==";
    private const string PasswordHash = "sMaodLylF6boRMfj9yG0nX8ZTwhRJC0luwFgbtP37iE=";
    private const string LicenseKey = "rd256xpvETKXRPCzDwmX7R0AGdJ7mFEmugO4h32/5AQ=";

    public static bool VerifyManagerPassword(string password)
    {
        if (password.Length < 11) return false;
        for (var index = 0; index <= password.Length - 11; index++)
        {
            if (VerifyExactPassword(password.Substring(index, 11))) return true;
        }
        return false;
    }

    private static bool VerifyExactPassword(string password)
    {
        var salt = Convert.FromBase64String(PasswordSalt);
        var expected = Convert.FromBase64String(PasswordHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string CreateLicenseFileText(string computerName, string macAddress, DateTime expiresOn)
    {
        var payload = new AuthorizationPayload
        {
            ComputerName = computerName.Trim(),
            MacAddress = macAddress.Trim(),
            ExpiresOn = expiresOn.Date
        };
        var plainText = JsonSerializer.Serialize(payload);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var key = Convert.FromBase64String(LicenseKey);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var macInput = aes.IV.Concat(cipherBytes).ToArray();
        using var hmac = new HMACSHA256(key);

        var file = new AuthorizationFile
        {
            Version = 1,
            Algorithm = "AES-CBC-HMACSHA256",
            Iv = Convert.ToBase64String(aes.IV),
            CipherText = Convert.ToBase64String(cipherBytes),
            Hmac = Convert.ToBase64String(hmac.ComputeHash(macInput))
        };
        return JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
    }

    public static AuthorizationValidationResult ValidateLicenseFile(
        string path,
        string currentComputerName,
        IEnumerable<string> currentMacAddresses,
        DateTime today)
    {
        if (!File.Exists(path))
            return AuthorizationValidationResult.Fail($"未找到授权文件：{path}");

        try
        {
            var payload = ReadLicensePayload(File.ReadAllText(path, Encoding.UTF8));
            if (!string.Equals(payload.ComputerName, currentComputerName, StringComparison.OrdinalIgnoreCase))
                return AuthorizationValidationResult.Fail("授权文件与当前计算机名不匹配。");

            var licensedMac = AuthorizationMachineInfo.NormalizeMacAddress(payload.MacAddress);
            var currentMacs = currentMacAddresses
                .Select(AuthorizationMachineInfo.NormalizeMacAddress)
                .Where(value => value.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!currentMacs.Contains(licensedMac))
                return AuthorizationValidationResult.Fail("授权文件与当前 MAC 地址不匹配。");

            if (payload.ExpiresOn.Date < today.Date)
                return AuthorizationValidationResult.Fail($"授权已过期：{payload.ExpiresOn:yyyy-MM-dd}");

            return AuthorizationValidationResult.Ok($"授权有效，到期日期：{payload.ExpiresOn:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            return AuthorizationValidationResult.Fail($"授权文件无效：{ex.Message}");
        }
    }

    private static AuthorizationPayload ReadLicensePayload(string licenseText)
    {
        var file = JsonSerializer.Deserialize<AuthorizationFile>(licenseText)
            ?? throw new InvalidOperationException("授权文件格式错误。");
        if (file.Version != 1 || file.Algorithm != "AES-CBC-HMACSHA256")
            throw new InvalidOperationException("授权文件版本或算法不受支持。");

        var key = Convert.FromBase64String(LicenseKey);
        var iv = Convert.FromBase64String(file.Iv);
        var cipherBytes = Convert.FromBase64String(file.CipherText);
        var expectedHmac = Convert.FromBase64String(file.Hmac);
        using var hmac = new HMACSHA256(key);
        var actualHmac = hmac.ComputeHash(iv.Concat(cipherBytes).ToArray());
        if (!CryptographicOperations.FixedTimeEquals(actualHmac, expectedHmac))
            throw new InvalidOperationException("授权文件校验失败。");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return JsonSerializer.Deserialize<AuthorizationPayload>(Encoding.UTF8.GetString(plainBytes))
            ?? throw new InvalidOperationException("授权内容格式错误。");
    }

    private sealed class AuthorizationPayload
    {
        public string ComputerName { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public DateTime ExpiresOn { get; set; }
    }

    private sealed class AuthorizationFile
    {
        public int Version { get; set; }
        public string Algorithm { get; set; } = "";
        public string Iv { get; set; } = "";
        public string CipherText { get; set; } = "";
        public string Hmac { get; set; } = "";
    }
}

public sealed class AuthorizationValidationResult
{
    public bool IsValid { get; private init; }
    public string Message { get; private init; } = "";

    public static AuthorizationValidationResult Ok(string message)
    {
        return new AuthorizationValidationResult { IsValid = true, Message = message };
    }

    public static AuthorizationValidationResult Fail(string message)
    {
        return new AuthorizationValidationResult { IsValid = false, Message = message };
    }
}
