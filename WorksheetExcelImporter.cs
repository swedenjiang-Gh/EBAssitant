using ExcelDataReader;

namespace EBAssistant;

public static class WorksheetExcelImporter
{
    public static List<WorksheetRawSheet> Read(string path)
    {
        var sheets = new List<WorksheetRawSheet>();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var sheetIndex = 1;

        do
        {
            var firstRow = reader.Read() ? ReadRow(reader) : [];
            var secondRow = reader.Read() ? ReadRow(reader) : [];
            var fieldCount = Math.Max(firstRow.Count, secondRow.Count);
            var columns = new List<WorksheetRawColumn>();

            for (var index = 1; index < fieldCount; index++)
            {
                var label = index < firstRow.Count ? firstRow[index] : "";
                var attributeIdText = index < secondRow.Count ? secondRow[index] : "";
                if (label.Length == 0 && attributeIdText.Length == 0)
                {
                    continue;
                }

                columns.Add(new WorksheetRawColumn
                {
                    ExcelColumnNumber = index + 1,
                    Label = label,
                    AttributeIdText = attributeIdText
                });
            }

            sheets.Add(new WorksheetRawSheet
            {
                SheetIndex = sheetIndex,
                SheetName = reader.Name ?? "",
                Columns = columns
            });

            sheetIndex++;
        } while (reader.NextResult());

        return sheets;
    }

    private static List<string> ReadRow(IExcelDataReader reader)
    {
        var values = new List<string>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values.Add(Convert.ToString(reader.GetValue(index))?.Trim() ?? "");
        }

        return values;
    }
}
