using ExcelDataReader;

namespace EBAssist;

public static class DialogDefinitionExcelImporter
{
    public static List<DialogDefinitionRow> Read(string path)
    {
        var rows = new List<DialogDefinitionRow>();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var rowNumber = 0;
        while (reader.Read())
        {
            rowNumber++;
            if (rowNumber == 1) continue;
            var tab = Convert.ToString(reader.GetValue(1))?.Trim() ?? "";
            var aidText = Convert.ToString(reader.GetValue(2))?.Trim() ?? "";
            if (tab.Length == 0 && aidText.Length == 0) continue;
            int.TryParse(aidText, out var aid);
            rows.Add(new DialogDefinitionRow { RowNumber = rowNumber, TabName = tab, AttributeIdText = aidText, AttributeId = aid });
        }
        var duplicates = rows.Where(x => x.TabName.Length > 0 && x.AttributeId > 0)
            .GroupBy(x => $"{x.TabName.ToUpperInvariant()}\u001f{x.AttributeId}")
            .Where(x => x.Count() > 1).SelectMany(x => x.Skip(1)).ToHashSet();
        foreach (var row in rows)
        {
            var errors = new List<string>();
            if (row.TabName.Length == 0) errors.Add("选项卡名称为空");
            if (row.AttributeId <= 0) errors.Add("属性 ID 必须为正整数");
            row.IsDuplicate = duplicates.Contains(row);
            row.Validation = errors.Count > 0 ? string.Join("；", errors) : row.IsDuplicate ? "重复行，已合并" : "待校验";
        }
        return rows;
    }
}
