namespace EBAssistant;

public sealed class GraphicTemplateSharedLoadResult
{
    public bool Success { get; set; }
    public bool FromCache { get; set; }
    public string Message { get; set; } = "";
    public GraphicTemplateIdentity? Identity { get; set; }
    public GraphicTemplateTreeResult? Tree { get; set; }
}

public static class GraphicTemplateSharedLoader
{
    public static async Task<GraphicTemplateSharedLoadResult> LoadAsync(EbAdapterClient client, IProgress<string>? progress = null)
    {
        var identityResponse = await client.GetGraphicTemplateIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            return new GraphicTemplateSharedLoadResult { Message = identityResponse.Message };
        }

        var cached = GraphicTemplateCache.Load(identityResponse.Data);
        if (cached is not null)
        {
            return new GraphicTemplateSharedLoadResult
            {
                Success = true,
                FromCache = true,
                Message = "已从共享图形模板缓存加载。",
                Identity = identityResponse.Data,
                Tree = cached
            };
        }

        var treeResponse = await client.GetGraphicTemplateTreeAsync(progress);
        if (!treeResponse.Success || treeResponse.Data is null)
        {
            return new GraphicTemplateSharedLoadResult
            {
                Message = treeResponse.Message,
                Identity = identityResponse.Data
            };
        }

        GraphicTemplateCache.Save(treeResponse.Data);
        return new GraphicTemplateSharedLoadResult
        {
            Success = true,
            Message = "已从 EB 首次读取并生成共享图形模板缓存。",
            Identity = identityResponse.Data,
            Tree = treeResponse.Data
        };
    }
}
