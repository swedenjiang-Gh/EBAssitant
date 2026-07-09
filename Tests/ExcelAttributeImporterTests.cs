using System.IO.Compression;
using System.Security;

namespace EBAssistant.Tests;

internal static class ExcelAttributeImporterTests
{
    public static void Run()
    {
        ReadsOptionalCommentColumn();
    }

    private static void ReadsOptionalCommentColumn()
    {
        var path = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"), "attributes.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteWorkbook(path, new[]
        {
            new[] { "序号", "属性名称", "属性类型", "注释" },
            new[] { "1", "测试属性", "文本", "用于测试的属性注释" }
        });

        var rows = ExcelAttributeImporter.Read(path, Array.Empty<string>());

        Assert.Equal(1, rows.Count);
        Assert.Equal("测试属性", rows[0].Name);
        Assert.Equal("文本", rows[0].SourceType);
        Assert.Equal("String", rows[0].EbType);
        Assert.Equal("用于测试的属性注释", rows[0].Comment);
        Assert.Equal("有效", rows[0].Validation);
    }

    private static void WriteWorkbook(string path, string[][] rows)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        WriteEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
    }

    private static string BuildWorksheetXml(string[][] rows)
    {
        var xml = new StringWriter();
        xml.WriteLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        xml.WriteLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            xml.Write("<row r=\"");
            xml.Write(rowIndex + 1);
            xml.WriteLine("\">");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var cell = GetCellName(columnIndex, rowIndex);
                xml.Write("<c r=\"");
                xml.Write(cell);
                xml.WriteLine("\" t=\"inlineStr\"><is><t>" + SecurityElement.Escape(rows[rowIndex][columnIndex]) + "</t></is></c>");
            }
            xml.WriteLine("</row>");
        }
        xml.WriteLine("</sheetData></worksheet>");
        return xml.ToString();
    }

    private static string GetCellName(int columnIndex, int rowIndex)
    {
        return ((char)('A' + columnIndex)).ToString() + (rowIndex + 1).ToString();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
        writer.Write(content);
    }
}
