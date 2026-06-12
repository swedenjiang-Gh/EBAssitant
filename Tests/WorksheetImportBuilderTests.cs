// 人工验证清单：
// worksheet-import.xlsx / worksheet-import.xls:
// - 2 个页签，顺序为 设备清单、备用设备
// - 第一列内容不进入预览
// - 第 2 列起第一行为标签、第二行为 AID
// - 第三行故意填写内容但不进入预览

namespace EBAssistant.Tests;

internal static class WorksheetImportBuilderTests
{
    public static void Run()
    {
        BuildsValidSheetPreview();
        ReportsInvalidSheetAndColumnReasons();
    }

    private static void BuildsValidSheetPreview()
    {
        var result = WorksheetImportBuilder.Build(new[]
        {
            new WorksheetRawSheet
            {
                SheetIndex = 2,
                SheetName = " 设备 ",
                Columns =
                [
                    new WorksheetRawColumn { ExcelColumnNumber = 3, Label = "备注", AttributeIdText = "25" },
                    new WorksheetRawColumn { ExcelColumnNumber = 2, Label = "名称", AttributeIdText = "5" }
                ]
            }
        });

        Assert.Equal(1, result.Count);
        WorksheetImportSheet sheet = result[0];
        Assert.Equal(2, sheet.SheetIndex);
        Assert.Equal("设备", sheet.OriginalName);
        Assert.Equal("", sheet.FinalName);
        Assert.Equal("待 EB 校验", sheet.Validation);
        Assert.Equal(2, sheet.Columns.Count);

        Assert.Equal(2, sheet.Columns[0].ExcelColumnNumber);
        Assert.Equal(0, sheet.Columns[0].Position);
        Assert.Equal("名称", sheet.Columns[0].Label);
        Assert.Equal(5, sheet.Columns[0].AttributeId);
        Assert.Equal(WorksheetColumnWidthCalculator.Calculate("名称"), sheet.Columns[0].Width);
        Assert.Equal("待 EB 校验", sheet.Columns[0].Validation);

        Assert.Equal(3, sheet.Columns[1].ExcelColumnNumber);
        Assert.Equal(1, sheet.Columns[1].Position);
        Assert.Equal("备注", sheet.Columns[1].Label);
        Assert.Equal(25, sheet.Columns[1].AttributeId);
        Assert.Equal(WorksheetColumnWidthCalculator.Calculate("备注"), sheet.Columns[1].Width);
        Assert.Equal("待 EB 校验", sheet.Columns[1].Validation);
    }

    private static void ReportsInvalidSheetAndColumnReasons()
    {
        var result = WorksheetImportBuilder.Build(new[]
        {
            new WorksheetRawSheet
            {
                SheetIndex = 2,
                SheetName = " ",
                Columns = []
            },
            new WorksheetRawSheet
            {
                SheetIndex = 1,
                SheetName = "设备",
                Columns =
                [
                    new WorksheetRawColumn { ExcelColumnNumber = 4, Label = "重复", AttributeIdText = "5" },
                    new WorksheetRawColumn { ExcelColumnNumber = 2, Label = " ", AttributeIdText = "0" },
                    new WorksheetRawColumn { ExcelColumnNumber = 3, Label = "无效", AttributeIdText = "abc" },
                    new WorksheetRawColumn { ExcelColumnNumber = 5, Label = "重复二", AttributeIdText = "5" }
                ]
            }
        });

        Assert.Equal(2, result.Count);
        WorksheetImportSheet invalidColumns = result[0];
        Assert.Equal(1, invalidColumns.SheetIndex);
        AssertContains("Excel 列 2", invalidColumns.Validation);
        AssertContains("列标签为空", invalidColumns.Validation);
        AssertContains("属性 ID 必须是正整数", invalidColumns.Validation);
        AssertContains("Excel 列 3", invalidColumns.Validation);
        AssertContains("属性 ID 不是有效整数", invalidColumns.Validation);
        AssertContains("Excel 列 5", invalidColumns.Validation);
        AssertContains("同一页签内属性 ID 重复", invalidColumns.Validation);
        Assert.Equal("列标签为空；属性 ID 必须是正整数", invalidColumns.Columns[0].Validation);
        Assert.Equal("属性 ID 不是有效整数", invalidColumns.Columns[1].Validation);
        Assert.Equal("待 EB 校验", invalidColumns.Columns[2].Validation);
        Assert.Equal("同一页签内属性 ID 重复", invalidColumns.Columns[3].Validation);

        WorksheetImportSheet invalidSheet = result[1];
        Assert.Equal(2, invalidSheet.SheetIndex);
        AssertContains("页签名称为空", invalidSheet.Validation);
        AssertContains("没有可创建的列", invalidSheet.Validation);
    }

    private static void AssertContains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
        }
    }
}
