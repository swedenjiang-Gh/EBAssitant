using System.Diagnostics;

namespace EBAssistant;

public sealed class ToolPanelConfigurationResultForm : Form
{
    public ToolPanelConfigurationResultForm(AddGraphicTemplatesToToolPanelResult result, string logDirectory)
    {
        Text = "添加到工具面板结果";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1050, 620);
        Font = new Font("Microsoft YaHei UI", 10F);

        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 82,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；总数 {result.TotalCount}；新增 {result.AddedCount}；失败 {result.FailedCount}\r\n目标条目：{result.TargetDirectoryPath}\r\n提示：添加成功后需要重启 EB 才能查看生效结果。"
        };
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            DataSource = result.Records
        };
        var open = new Button { Text = "打开日志目录", Dock = DockStyle.Bottom, Height = 42 };
        open.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(open);
    }
}
