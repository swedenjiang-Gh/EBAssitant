using System.Diagnostics;

namespace EBAssistant;

public sealed class AttributeCreationResultForm : Form
{
    public AttributeCreationResultForm(CreateAttributesResult result, string logDirectory)
    {
        Text = "属性创建结果";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(950, 620);
        Font = new Font("Microsoft YaHei UI", 10F);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = true,
            AllowUserToAddRows = false,
            DataSource = result.Records
        };
        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；成功 {result.Records.Count(x => x.Status == "创建成功")}；失败 {result.Records.Count(x => x.Status == "创建失败")}；已回滚 {result.Records.Count(x => x.Status == "已回滚")}；回滚失败 {result.Records.Count(x => x.Status == "回滚失败")}；未处理 {result.Records.Count(x => x.Status == "未处理")}{Environment.NewLine}日志目录：{logDirectory}"
        };
        var open = new Button { Text = "打开日志目录", Dock = DockStyle.Bottom, Height = 42 };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
