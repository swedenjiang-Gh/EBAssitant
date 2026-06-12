using System.Text.Json;

namespace EBAssistant;

public sealed class TypeDefinitionsForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, CheckBoxes = true, HideSelection = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "正在连接当前 EB..." };
    private readonly Button _reload = new() { Text = "重新读取", Width = 110, Height = 34 };
    private readonly Button _dialog = new() { Text = "定义对话框", Width = 120, Height = 34 };
    private readonly List<Form> _children = [];
    private EbAdapterClient? _client;
    private bool _updatingChecks;

    public TypeDefinitionsForm()
    {
        Text = "类型定义";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(850, 680);
        Font = new Font("Microsoft YaHei UI", 10F);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(7) };
        toolbar.Controls.Add(_reload);
        toolbar.Controls.Add(_dialog);
        Controls.Add(_tree);
        Controls.Add(toolbar);
        Controls.Add(_status);
        _tree.AfterCheck += TreeAfterCheck;
        _reload.Click += async (_, _) => await LoadFromEbAsync();
        _dialog.Click += (_, _) => OpenDialogDefinition();
        Shown += async (_, _) => await ConnectAndLoadAsync();
    }

    private async Task ConnectAndLoadAsync()
    {
        SetBusy(true, "正在连接当前 EB...");
        var active = await EbAdapterClient.FindActiveAsync();
        if (active.Count == 0)
        {
            SetBusy(false, "未检测到当前活动 EB。");
            var detail = string.IsNullOrWhiteSpace(EbAdapterClient.LastDiscoveryMessage)
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}诊断信息：{Environment.NewLine}{EbAdapterClient.LastDiscoveryMessage}";
            MessageBox.Show(this, $"未检测到正在运行的 EB，请先打开 EB 并连接数据库。{detail}", "类型定义", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _client = active[0];
        var identityResponse = await _client.GetTypeDefinitionIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            SetBusy(false, identityResponse.Message);
            MessageBox.Show(this, identityResponse.Message, "类型定义", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var cached = TypeDefinitionCache.Load(identityResponse.Data);
        if (cached is not null)
        {
            DisplayTree(cached);
            SetBusy(false, $"已从缓存加载类型定义目录，共 {_tree.GetNodeCount(true)} 个节点。");
            return;
        }
        await LoadFromEbAsync();
    }

    private async Task LoadFromEbAsync()
    {
        if (_client is null) return;
        SetBusy(true, "正在从 EB 重新读取完整类型定义树...");
        var response = await _client.GetTypeDefinitionTreeAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "读取类型定义失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        TypeDefinitionCache.Save(response.Data);
        DisplayTree(response.Data);
        SetBusy(false, $"已读取并缓存类型定义目录，共 {_tree.GetNodeCount(true)} 个节点。");
    }

    private void DisplayTree(TypeDefinitionTreeResult tree)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var node in tree.Nodes) _tree.Nodes.Add(ToTreeNode(node));
        _tree.EndUpdate();
        _tree.CollapseAll();
    }

    private static TreeNode ToTreeNode(TypeDefinitionNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children) node.Nodes.Add(ToTreeNode(child));
        return node;
    }

    private void TreeAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_updatingChecks || e.Node is null) return;
        _updatingChecks = true;
        try
        {
            SetChildrenChecked(e.Node, e.Node.Checked);
            UpdateParents(e.Node.Parent);
        }
        finally { _updatingChecks = false; }
    }

    private static void SetChildrenChecked(TreeNode node, bool value)
    {
        foreach (TreeNode child in node.Nodes)
        {
            child.Checked = value;
            SetChildrenChecked(child, value);
        }
    }

    private static void UpdateParents(TreeNode? node)
    {
        while (node is not null)
        {
            node.Checked = node.Nodes.Cast<TreeNode>().All(x => x.Checked);
            node = node.Parent;
        }
    }

    private void OpenDialogDefinition()
    {
        if (_client is null) return;
        var leaves = GetCheckedLeaves(_tree.Nodes).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
        if (leaves.Count == 0)
        {
            MessageBox.Show(this, "请至少选择一个包含叶级类型对象的目录。", "定义对话框", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var form = new TypeDefinitionDialogForm(_client, leaves);
        _children.Add(form);
        form.FormClosed += (_, _) => _children.Remove(form);
        form.Show();
    }

    private static IEnumerable<TypeDefinitionNode> GetCheckedLeaves(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Checked && node.Tag is TypeDefinitionNode item && item.IsActionable) yield return item;
            foreach (var nested in GetCheckedLeaves(node.Nodes)) yield return nested;
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _tree.Enabled = _reload.Enabled = _dialog.Enabled = !busy;
        _status.Text = message;
        UseWaitCursor = busy;
    }
}
