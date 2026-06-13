using System.Net.NetworkInformation;

namespace EBAssistant;

public static class AuthorizationMachineInfo
{
    public static string ComputerName => Environment.MachineName;

    public static string GetPrimaryMacAddress()
    {
        return GetMacAddresses().FirstOrDefault() ?? "";
    }

    public static List<string> GetMacAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(item =>
                item.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                item.OperationalStatus == OperationalStatus.Up &&
                item.GetPhysicalAddress().GetAddressBytes().Length > 0)
            .OrderBy(item => item.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
            .Select(item => FormatMacAddress(item.GetPhysicalAddress().GetAddressBytes()))
            .ToList();
    }

    public static string NormalizeMacAddress(string value)
    {
        return new string(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string FormatMacAddress(byte[] bytes)
    {
        return string.Join("-", bytes.Select(item => item.ToString("X2")));
    }
}
