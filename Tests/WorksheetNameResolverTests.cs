namespace EBAssistant.Tests;

internal static class WorksheetNameResolverTests
{
    public static void Run()
    {
        Assert.Equal("设备", WorksheetNameResolver.Resolve(" 设备 ", Array.Empty<string>()));
        Assert.Equal("设备 (2)", WorksheetNameResolver.Resolve("设备", new[] { "设备" }));
        Assert.Equal("设备 (3)", WorksheetNameResolver.Resolve("设备", new[] { "设备", "设备 (2)" }));
        Assert.Equal("设备 (2)", WorksheetNameResolver.Resolve("设备", new[] { "设备", "设备 (3)" }));
    }
}
