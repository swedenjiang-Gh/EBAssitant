namespace EBAssistant;

public static class WorksheetImportBuilder
{
    private const string PendingEbValidation = "待 EB 校验";

    public static List<WorksheetImportSheet> Build(IEnumerable<WorksheetRawSheet> rawSheets)
    {
        return rawSheets
            .OrderBy(sheet => sheet.SheetIndex)
            .Select(BuildSheet)
            .ToList();
    }

    private static WorksheetImportSheet BuildSheet(WorksheetRawSheet raw)
    {
        var sheetErrors = new List<string>();
        string originalName = raw.SheetName.Trim();

        if (originalName.Length == 0)
        {
            sheetErrors.Add("页签名称为空");
        }
        else if (originalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            sheetErrors.Add("页签名称包含非法文件名字符");
        }

        List<WorksheetColumnDefinition> columns = BuildColumns(raw.Columns, sheetErrors);
        if (columns.All(column => column.Validation != PendingEbValidation))
        {
            sheetErrors.Add("没有可创建的列");
        }

        return new WorksheetImportSheet
        {
            SheetIndex = raw.SheetIndex,
            OriginalName = originalName,
            Validation = sheetErrors.Count == 0 ? PendingEbValidation : string.Join("；", sheetErrors),
            Columns = columns
        };
    }

    private static List<WorksheetColumnDefinition> BuildColumns(
        IEnumerable<WorksheetRawColumn> rawColumns,
        List<string> sheetErrors)
    {
        var seenAttributeIds = new HashSet<int>();
        var columns = new List<WorksheetColumnDefinition>();

        foreach (WorksheetRawColumn rawColumn in rawColumns.OrderBy(column => column.ExcelColumnNumber))
        {
            string label = rawColumn.Label.Trim();
            string attributeIdText = rawColumn.AttributeIdText.Trim();
            var columnErrors = new List<string>();

            if (label.Length == 0)
            {
                columnErrors.Add("列标签为空");
            }

            int attributeId = 0;
            if (!int.TryParse(attributeIdText, out attributeId))
            {
                columnErrors.Add("属性 ID 不是有效整数");
            }
            else if (attributeId <= 0)
            {
                columnErrors.Add("属性 ID 必须是正整数");
            }
            else if (!seenAttributeIds.Add(attributeId))
            {
                columnErrors.Add("同一页签内属性 ID 重复");
            }

            string validation = columnErrors.Count == 0
                ? PendingEbValidation
                : string.Join("；", columnErrors);

            if (columnErrors.Count > 0)
            {
                sheetErrors.Add($"Excel 列 {rawColumn.ExcelColumnNumber}: {validation}");
            }

            columns.Add(new WorksheetColumnDefinition
            {
                ExcelColumnNumber = rawColumn.ExcelColumnNumber,
                Position = columns.Count,
                Label = label,
                AttributeId = attributeId,
                Width = WorksheetColumnWidthCalculator.Calculate(label),
                Validation = validation
            });
        }

        return columns;
    }
}
