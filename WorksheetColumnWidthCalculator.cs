namespace EBAssistant;

public static class WorksheetColumnWidthCalculator
{
    public static int Calculate(string label)
    {
        int visualUnits = label.Sum(character => character <= 0x7f ? 1 : 2);
        return Math.Clamp(visualUnits * 9 + 24, 80, 600);
    }
}
