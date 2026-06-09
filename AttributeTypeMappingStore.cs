using System.Text.Json;

namespace EBAssist;

public static class AttributeTypeMappingStore
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EBAssist",
        "attribute-type-mappings.json");

    public static List<AttributeTypeMapping> Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<AttributeTypeMapping>>(File.ReadAllText(ConfigPath));
                if (loaded is { Count: > 0 }) return loaded;
            }
            catch { }
        }
        return Defaults();
    }

    public static Dictionary<string, string> GetDictionary() =>
        Load().Where(x => !string.IsNullOrWhiteSpace(x.UserType) && IsValidEbType(x.EbType))
            .GroupBy(x => x.UserType.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().EbType, StringComparer.OrdinalIgnoreCase);

    public static void Save(IEnumerable<AttributeTypeMapping> mappings)
    {
        var list = mappings.Where(x => !string.IsNullOrWhiteSpace(x.UserType) && IsValidEbType(x.EbType))
            .Select(x => new AttributeTypeMapping { UserType = x.UserType.Trim(), EbType = x.EbType })
            .ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsValidEbType(string type) =>
        new[] { "String", "Date", "Time", "DateTime", "Boolean", "Number", "Float", "Formula" }.Contains(type);

    private static List<AttributeTypeMapping> Defaults() =>
    [
        new() { UserType = "文本", EbType = "String" },
        new() { UserType = "文本属性", EbType = "String" },
        new() { UserType = "字符串", EbType = "String" },
        new() { UserType = "string", EbType = "String" },
        new() { UserType = "aucAttributeTypeString", EbType = "String" },
        new() { UserType = "日期", EbType = "Date" },
        new() { UserType = "date", EbType = "Date" },
        new() { UserType = "时间", EbType = "Time" },
        new() { UserType = "time", EbType = "Time" },
        new() { UserType = "日期时间", EbType = "DateTime" },
        new() { UserType = "日期和时间", EbType = "DateTime" },
        new() { UserType = "datetime", EbType = "DateTime" },
        new() { UserType = "布尔", EbType = "Boolean" },
        new() { UserType = "是/否", EbType = "Boolean" },
        new() { UserType = "boolean", EbType = "Boolean" },
        new() { UserType = "数字", EbType = "Number" },
        new() { UserType = "号码", EbType = "Number" },
        new() { UserType = "number", EbType = "Number" },
        new() { UserType = "浮点", EbType = "Float" },
        new() { UserType = "小数", EbType = "Float" },
        new() { UserType = "float", EbType = "Float" },
        new() { UserType = "公式", EbType = "Formula" },
        new() { UserType = "formula", EbType = "Formula" }
    ];
}
