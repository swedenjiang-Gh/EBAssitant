using System.Diagnostics;

namespace EBAssist;

public sealed class TypeDefinitionResultForm : Form
{
    public TypeDefinitionResultForm(ApplyTypeDefinitionDialogsResult result, string logDirectory)
    {
        Text = "类型定义操作结果";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 620);
        Font = new Font("Microsoft YaHei UI", 10F);
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, DataSource = result.Records };
        var summary = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(8),
            Text = $"状态：{result.Status}；新增 {result.Records.Count(x => x.Status == "added")}；已存在跳过 {result.Records.Count(x => x.Status == "skipped_existing")}；失败 {result.Records.Count(x => x.Status == "failed")}；未处理类型 {result.UnprocessedTypeItemIds.Count}；未处理操作 {result.UnprocessedOperations.Count}"
        };
        var open = new Button { Text = "打开日志目录", Dock = DockStyle.Bottom, Height = 42 };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });
        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
