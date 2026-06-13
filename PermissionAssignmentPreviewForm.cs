namespace EBAssistant;

public sealed class PermissionAssignmentPreviewForm : Form
{
    public PermissionAssignmentPreviewForm(PermissionAssignmentSelectionResult selection)
    {
        Text = "确认权限配置";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 560);
        Font = new Font("Microsoft YaHei UI", 10F);

        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 74,
            Padding = new Padding(10),
            Text = $"将 {selection.Members.Count} 个用户或用户组添加到 {selection.Directories.Count} 个权限目录，" +
                   $"共 {selection.CombinationCount} 项。\r\n只添加权限成员，不设置具体权限；已存在项会跳过。"
        };
        var lists = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360 };
        lists.Panel1.Controls.Add(CreateList("用户及用户组", selection.Members));
        lists.Panel2.Controls.Add(CreateList("权限目录", selection.Directories));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft
        };
        var apply = new Button { Text = "确认添加", DialogResult = DialogResult.OK, Width = 110, Height = 34 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90, Height = 34 };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);

        Controls.Add(lists);
        Controls.Add(buttons);
        Controls.Add(summary);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static Control CreateList(string title, IEnumerable<PermissionDirectoryNode> nodes)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var label = new Label { Text = title, Dock = DockStyle.Top, Height = 30, Padding = new Padding(6) };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Items.AddRange(nodes.Select(node => $"{node.Name}  [{node.Id}]").ToArray());
        panel.Controls.Add(list);
        panel.Controls.Add(label);
        return panel;
    }
}
