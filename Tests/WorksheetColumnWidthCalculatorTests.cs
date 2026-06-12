namespace EBAssistant.Tests;

internal static class WorksheetColumnWidthCalculatorTests
{
    public static void Run()
    {
        Assert.Equal(80, WorksheetColumnWidthCalculator.Calculate("A"));
        Assert.True(WorksheetColumnWidthCalculator.Calculate("设备名称") >
                    WorksheetColumnWidthCalculator.Calculate("Name"));
        Assert.Equal(600, WorksheetColumnWidthCalculator.Calculate(new string('长', 100)));
    }
}
