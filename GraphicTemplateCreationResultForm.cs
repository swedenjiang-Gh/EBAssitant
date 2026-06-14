using System.Diagnostics;

namespace EBAssistant;

public sealed class GraphicTemplateCreationResultForm : Form
{
    public GraphicTemplateCreationResultForm(CreateGraphicTemplatesResult result, string logDirectory)
    {
        Text = "新建模板图形结果";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 620);
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
            Height = 96,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；请求总数量 {result.RequestedTotalCount}；成功创建 {result.CreatedCount}；最终确认 {result.ConfirmedFinalCount}" +
                $"{Environment.NewLine}目录：{result.DirectoryPath}{Environment.NewLine}日志目录：{(string.IsNullOrWhiteSpace(logDirectory) ? "日志保存失败" : logDirectory)}"
        };
        var open = new Button
        {
            Text = "打开日志目录",
            Dock = DockStyle.Bottom,
            Height = 42,
            Enabled = !string.IsNullOrWhiteSpace(logDirectory)
        };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
