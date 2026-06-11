using ExcelDataReader;

namespace EBAssistant;

public static class ExcelAttributeImporter
{
    public static List<ImportedAttributeRow> Read(string path, IEnumerable<string> existingNames)
    {
        var rows = new List<ImportedAttributeRow>();
        var types = AttributeTypeMappingStore.GetDictionary();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var rowNumber = 0;
        do
        {
            while (reader.Read())
            {
                rowNumber++;
                if (rowNumber == 1)
                {
                    continue;
                }

                var name = Convert.ToString(reader.GetValue(1))?.Trim() ?? "";
                var sourceType = Convert.ToString(reader.GetValue(2))?.Trim() ?? "";
                if (name.Length == 0 && sourceType.Length == 0)
                {
                    continue;
                }

                rows.Add(new ImportedAttributeRow
                {
                    RowNumber = rowNumber,
                    Name = name,
                    SourceType = sourceType,
                    EbType = types.TryGetValue(sourceType, out var type) ? type : ""
                });
            }
            break;
        } while (reader.NextResult());

        Validate(rows, existingNames);
        return rows;
    }

    private static void Validate(List<ImportedAttributeRow> rows, IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(existingNames.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        var duplicates = rows.Where(x => x.Name.Length > 0)
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var errors = new List<string>();
            if (row.Name.Length == 0) errors.Add("属性名称为空");
            if (row.SourceType.Length == 0) errors.Add("属性类型为空");
            else if (row.EbType.Length == 0) errors.Add("不支持的属性类型");
            if (duplicates.Contains(row.Name)) errors.Add("表格内名称重复");
            if (existing.Contains(row.Name)) errors.Add("EB 中已存在同名属性");
            row.Validation = errors.Count == 0 ? "有效" : string.Join("；", errors);
        }
    }
}
