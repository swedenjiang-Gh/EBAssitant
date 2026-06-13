namespace EBAssistant;

public sealed class ProjectTemplatesForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "正在连接当前 EB..." };
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly ContextMenuStrip _menu = new();
    private readonly List<Form> _childWindows = [];
    private EbAdapterClient? _client;
    private ProjectTemplateIdentity? _identity;

    public ProjectTemplatesForm()
    {
        Text = "工作表";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 620);
        Font = new Font("Microsoft YaHei UI", 10F);

        _menu.Items.Add("新建工作表", null, (_, _) => ShowCreateWorksheets());
        _tree.NodeMouseClick += TreeNodeMouseClick;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(7), FlowDirection = FlowDirection.LeftToRight };
        toolbar.Controls.Add(_refresh);

        _refresh.Click += async (_, _) => await LoadFromEbAsync();
        Controls.Add(_tree);
        Controls.Add(toolbar);
        Controls.Add(_status);
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
            MessageBox.Show(this, $"未检测到正在运行的 EB，请先打开 EB 并连接数据库。{detail}", "工作表", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null)
        {
            Close();
            return;
        }

        var identityResponse = await _client.GetProjectTemplateIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            SetBusy(false, identityResponse.Message);
            MessageBox.Show(this, identityResponse.Message, "工作表", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _identity = identityResponse.Data;
        var cached = ProjectTemplateCache.Load(_identity);
        if (cached is not null)
        {
            DisplayTree(cached);
            SetBusy(false, $"已从缓存加载项目模板，共 {_tree.GetNodeCount(true)} 个节点。点击“刷新”可重新读取 EB。");
            return;
        }

        await LoadFromEbAsync();
    }

    private EbAdapterClient? SelectAdapter(List<EbAdapterClient> active)
    {
        using var dialog = new Form { Text = "选择当前 EB", Size = new Size(480, 230), StartPosition = FormStartPosition.CenterParent, Font = Font };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Items.AddRange(active.ToArray());
        list.SelectedIndex = 0;
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 40 };
        dialog.Controls.Add(list);
        dialog.Controls.Add(ok);
        dialog.AcceptButton = ok;
        return dialog.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as EbAdapterClient : null;
    }

    private async Task LoadFromEbAsync()
    {
        if (_client is null || _identity is null) return;
        SetBusy(true, $"正在从 EB {_client.Connection.Version} 读取项目模板...");
        var response = await _client.GetProjectTemplateTreeAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "读取项目模板失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ProjectTemplateCache.Save(response.Data);
        DisplayTree(response.Data);
        SetBusy(false, $"已从 EB 读取并缓存 {_tree.GetNodeCount(true)} 个项目模板节点。");
    }

    private void DisplayTree(ProjectTemplateTreeResult tree)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var node in tree.Nodes)
        {
            _tree.Nodes.Add(ToTreeNode(node));
        }
        _tree.EndUpdate();
        _tree.CollapseAll();
    }

    private static TreeNode ToTreeNode(ProjectTemplateNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children)
        {
            node.Nodes.Add(ToTreeNode(child));
        }
        return node;
    }

    private void TreeNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node is null) return;
        _tree.SelectedNode = e.Node;
        if (e.Node.Tag is ProjectTemplateNode { IsTemplateProject: true })
            _menu.Show(_tree, e.Location);
    }

    private void ShowCreateWorksheets()
    {
        if (_client is null || _tree.SelectedNode?.Tag is not ProjectTemplateNode { IsTemplateProject: true } project) return;
        var form = new CreateWorksheetsForm(_client, project);
        _childWindows.Add(form);
        form.FormClosed += (_, _) => _childWindows.Remove(form);
        form.Show();
    }

    private void SetBusy(bool busy, string message)
    {
        _tree.Enabled = _refresh.Enabled = !busy;
        _status.Text = message;
        UseWaitCursor = busy;
    }
}
