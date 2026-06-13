namespace EBAssistant;

public static class WorksheetPreparation
{
    private const string PendingEbValidation = "待 EB 校验";

    public static void Apply(
        IEnumerable<WorksheetImportSheet> sheets,
        IEnumerable<string> existingNames,
        IEnumerable<int> missingAttributeIds)
    {
        var reservedNames = new List<string>(existingNames);
        var missing = new HashSet<int>(missingAttributeIds);

        foreach (var sheet in sheets.OrderBy(x => x.SheetIndex))
        {
            if (sheet.Validation != PendingEbValidation)
            {
                sheet.FinalName = sheet.OriginalName;
                continue;
            }

            var errors = new List<string>();
            foreach (var column in sheet.Columns)
            {
                if (column.Validation != PendingEbValidation)
                {
                    continue;
                }

                if (missing.Contains(column.AttributeId))
                {
                    column.Validation = $"属性 ID {column.AttributeId} 在 EB 中不存在";
                    errors.Add($"Excel 列 {column.ExcelColumnNumber} 属性 ID {column.AttributeId} 在 EB 中不存在");
                }
                else
                {
                    column.Validation = "有效；列标签仅预览，不写入 EB";
                }
            }

            sheet.Validation = errors.Count == 0 ? "有效" : string.Join("；", errors);
            if (sheet.IsValid)
            {
                sheet.FinalName = WorksheetNameResolver.Resolve(sheet.OriginalName, reservedNames);
                reservedNames.Add(sheet.FinalName);
            }
            else
            {
                sheet.FinalName = sheet.OriginalName;
            }
        }
    }
}
