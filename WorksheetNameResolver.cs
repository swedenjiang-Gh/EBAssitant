namespace EBAssistant;

public static class WorksheetNameResolver
{
    public static string Resolve(string baseName, IEnumerable<string> reservedNames)
    {
        string trimmedBaseName = baseName.Trim();
        var reserved = new HashSet<string>(
            reservedNames.Select(name => name.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (!reserved.Contains(trimmedBaseName))
        {
            return trimmedBaseName;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{trimmedBaseName} ({suffix})";
            if (!reserved.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
