using System.Diagnostics;

namespace EBAssistant;

public sealed class PermissionAssignmentResultForm : Form
{
    public PermissionAssignmentResultForm(PermissionMemberAssignmentResult result, string logDirectory)
    {
        Text = "权限配置结果";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 640);
        Font = new Font("Microsoft YaHei UI", 10F);

        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(8),
            Text = $"状态：{result.Status}；总数 {result.TotalCount}；新增 {result.AddedCount}；" +
                   $"已存在跳过 {result.SkippedCount}；失败 {result.FailedCount}"
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
