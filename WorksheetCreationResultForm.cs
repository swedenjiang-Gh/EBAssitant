using System.Diagnostics;

namespace EBAssistant;

public sealed class WorksheetCreationResultForm : Form
{
    public WorksheetCreationResultForm(CreateWorksheetsResult result, string logDirectory)
    {
        Text = "工作表创建结果";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1120, 650);
        Font = new Font("Microsoft YaHei UI", 10F);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = true,
            AllowUserToAddRows = false,
            DataSource = result.Records
        };
        var logText = string.IsNullOrWhiteSpace(logDirectory) ? "日志保存失败" : logDirectory;
        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；成功 {result.Records.Count(x => x.Status == "created")}；失败 {result.Records.Count(x => x.Status == "failed")}；校验跳过 {result.Records.Count(x => x.Status == "validation_skipped")}；未处理 {result.Records.Count(x => x.Status == "unprocessed")}{Environment.NewLine}日志目录：{logText}"
        };
        var open = new Button { Text = "打开日志目录", Dock = DockStyle.Bottom, Height = 42, Enabled = !string.IsNullOrWhiteSpace(logDirectory) };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
