namespace EBAssistant;

public static class AppDisplay
{
    public const string ProductName = "EBAssistant";

    public static string MainTitle => BuildMainTitle(typeof(AppDisplay).Assembly.GetName().Version);

    public static string BuildMainTitle(Version? version)
    {
        version ??= new Version(1, 0, 0);
        return $"{ProductName} v{version.Major}.{version.Minor}.{version.Build}";
    }
}
