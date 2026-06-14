namespace EBAssistant;

public static class GraphicTemplateCreation
{
    public static bool TryParseTotalCount(string text, out int count, out string validation)
    {
        if (!int.TryParse(text, out count) || count < 2 || count > 200)
        {
            count = 0;
            validation = "请输入 2-200 之间的正整数。";
            return false;
        }

        validation = "有效";
        return true;
    }

    public static void ApplySummary(CreateGraphicTemplatesResult result)
    {
        var expectedCreated = Math.Max(0, result.RequestedTotalCount - result.OriginalCount);
        result.Status = expectedCreated > 0 && result.CreatedCount >= expectedCreated
            ? "completed"
            : result.CreatedCount > 0 ? "partial" : "failed";
    }
}
