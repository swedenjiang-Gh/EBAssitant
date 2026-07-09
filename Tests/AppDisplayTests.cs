namespace EBAssistant.Tests;

internal static class AppDisplayTests
{
    public static void Run()
    {
        BuildsMainTitleFromVersion();
    }

    private static void BuildsMainTitleFromVersion()
    {
        Assert.Equal("EBAssistant v1.2.3", AppDisplay.BuildMainTitle(new Version(1, 2, 3, 4)));
    }
}
