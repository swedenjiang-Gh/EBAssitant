using ExcelDataReader;

namespace EBAssistant;

public static class GraphicTemplateBatchExcelImporter
{
    public static List<GraphicTemplateBatchRawRow> Read(string path)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var rows = new List<GraphicTemplateBatchRawRow>();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var rowNumber = 0;

        while (reader.Read())
        {
            rowNumber++;
            if (rowNumber == 1) continue;

            var sequence = ReadCell(reader, 0);
            var sourcePath = ReadCell(reader, 1);
            var templateName = ReadCell(reader, 2);
            var targetPath = ReadCell(reader, 3);
            if (sourcePath.Length == 0 && templateName.Length == 0 && targetPath.Length == 0) continue;

            rows.Add(new GraphicTemplateBatchRawRow
            {
                ExcelRowNumber = rowNumber,
                Sequence = sequence,
                SourceDirectoryPath = sourcePath,
                TemplateName = templateName,
                TargetDirectoryPath = targetPath
            });
        }

        return rows;
    }

    private static string ReadCell(IExcelDataReader reader, int index)
    {
        return index < reader.FieldCount ? Convert.ToString(reader.GetValue(index))?.Trim() ?? "" : "";
    }
}
