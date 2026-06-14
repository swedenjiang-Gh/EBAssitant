namespace EBAssistant;

public sealed class GraphicTemplateDestinationForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8) };
    private readonly Button _ok = new() { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100, Enabled = false };

    public GraphicTemplateDestinationForm(GraphicTemplateTreeResult tree)
    {
        Text = "迁往目录";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 620);
        Font = new Font("Microsoft YaHei UI", 10F);

        foreach (var node in tree.Nodes)
        {
            _tree.Nodes.Add(ToTreeNode(node));
        }
        _tree.AfterSelect += (_, _) => UpdateSelection();

        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(cancel);

        Controls.Add(_tree);
        Controls.Add(_status);
        Controls.Add(buttons);
        AcceptButton = _ok;
        CancelButton = cancel;
    }

    public GraphicTemplateDirectoryNode? SelectedDirectory { get; private set; }

    private void UpdateSelection()
    {
        SelectedDirectory = _tree.SelectedNode?.Tag as GraphicTemplateDirectoryNode;
        var isLeaf = SelectedDirectory is not null && GraphicTemplateSelection.IsLeafDirectory(SelectedDirectory);
        _ok.Enabled = isLeaf;
        _status.Text = isLeaf ? SelectedDirectory!.FullPath : "只能选择最后一级目录。";
    }

    private static TreeNode ToTreeNode(GraphicTemplateDirectoryNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children)
        {
            node.Nodes.Add(ToTreeNode(child));
        }
        return node;
    }
}
