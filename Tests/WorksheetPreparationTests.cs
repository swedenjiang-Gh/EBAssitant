namespace EBAssistant.Tests;

internal static class WorksheetPreparationTests
{
    public static void Run()
    {
        var sheets = new List<WorksheetImportSheet>
        {
            new()
            {
                SheetIndex = 1,
                OriginalName = "设备",
                Validation = "待 EB 校验",
                Columns =
                [
                    new WorksheetColumnDefinition { Position = 0, Label = "名称", AttributeId = 5, Validation = "待 EB 校验" }
                ]
            },
            new()
            {
                SheetIndex = 2,
                OriginalName = "跳过项",
                Validation = "待 EB 校验",
                Columns =
                [
                    new WorksheetColumnDefinition { ExcelColumnNumber = 2, Position = 0, Label = "未知", AttributeId = 999, Validation = "待 EB 校验" }
                ]
            },
            new()
            {
                SheetIndex = 3,
                OriginalName = "跳过项",
                Validation = "待 EB 校验",
                Columns =
                [
                    new WorksheetColumnDefinition { ExcelColumnNumber = 2, Position = 0, Label = "名称", AttributeId = 5, Validation = "待 EB 校验" }
                ]
            }
        };

        WorksheetPreparation.Apply(sheets, new[] { "设备" }, new[] { 999 });

        Assert.Equal("设备 (2)", sheets[0].FinalName);
        Assert.Equal("有效", sheets[0].Validation);
        Assert.Equal("有效；列标签仅预览，不写入 EB", sheets[0].Columns[0].Validation);
        Assert.Equal("属性 ID 999 在 EB 中不存在", sheets[1].Columns[0].Validation);
        Assert.Equal("Excel 列 2 属性 ID 999 在 EB 中不存在", sheets[1].Validation);
        Assert.Equal("跳过项", sheets[2].FinalName);
    }
}
