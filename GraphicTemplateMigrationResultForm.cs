using System.Diagnostics;

namespace EBAssistant;

public sealed class GraphicTemplateMigrationResultForm : Form
{
    public GraphicTemplateMigrationResultForm(MoveGraphicTemplatesResult result, string logDirectory)
    {
        Text = "图形模板迁移结果";
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
            Height = 72,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；成功 {result.MovedCount}；失败 {result.FailedCount}{Environment.NewLine}迁往目录：{result.TargetDirectoryPath}{Environment.NewLine}日志目录：{logText}"
        };
        var open = new Button { Text = "打开日志目录", Dock = DockStyle.Bottom, Height = 42, Enabled = !string.IsNullOrWhiteSpace(logDirectory) };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
