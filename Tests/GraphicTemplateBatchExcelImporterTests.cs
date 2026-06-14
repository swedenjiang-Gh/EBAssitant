namespace EBAssistant.Tests;

internal static class GraphicTemplateBatchExcelImporterTests
{
    public static void Run()
    {
        ReadsMigrationTemplateRows();
    }

    private static void ReadsMigrationTemplateRows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "迁移模板图形模板.xlsx");

        var rows = GraphicTemplateBatchExcelImporter.Read(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].ExcelRowNumber);
        Assert.Equal("1", rows[0].Sequence);
        Assert.Equal("器件/仪表/传感器,速度或频率/test", rows[0].SourceDirectoryPath);
        Assert.Equal("A", rows[0].TemplateName);
        Assert.Equal("器件/常规/test", rows[0].TargetDirectoryPath);
    }
}
