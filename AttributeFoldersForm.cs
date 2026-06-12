namespace EBAssistant;

public sealed class AttributeFoldersForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "正在连接当前 EB..." };
    private readonly ContextMenuStrip _menu = new();
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly List<Form> _childWindows = [];
    private EbAdapterClient? _client;
    private AttributeFolderIdentity? _identity;
    private FolderTreeResult? _treeResult;

    public AttributeFoldersForm()
    {
        Text = "属性目录";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        Font = new Font("Microsoft YaHei UI", 10F);
        _menu.Items.Add("创建属性", null, (_, _) => OpenCreateDialog());
        _menu.Items.Add("新建目录", null, async (_, _) => await CreateFolderAsync());
        _tree.ContextMenuStrip = _menu;
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
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
            MessageBox.Show(this, $"未检测到正在运行的 EB，请先打开 EB 并连接数据库。{detail}", "属性", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null)
        {
            Close();
            return;
        }
        var identityResponse = await _client.GetAttributeFolderIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            SetBusy(false, identityResponse.Message);
            MessageBox.Show(this, identityResponse.Message, "属性", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _identity = identityResponse.Data;
        var cached = AttributeFolderCache.Load(_identity);
        if (cached is not null)
        {
            DisplayTree(cached);
            SetBusy(false, $"已从缓存加载属性目录，共 {_tree.GetNodeCount(true)} 个目录。点击“刷新”可重新读取 EB。");
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
        SetBusy(true, $"正在从 EB {_client.Connection.Version} 重新读取属性目录...");
        var response = await _client.GetAttributeFolderTreeAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "读取属性目录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        AttributeFolderCache.Save(_identity, response.Data);
        DisplayTree(response.Data);
        SetBusy(false, $"已从 EB 读取并缓存 {_tree.GetNodeCount(true)} 个属性目录。");
    }

    private void DisplayTree(FolderTreeResult tree)
    {
        _treeResult = tree;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var folder in tree.Folders)
        {
            _tree.Nodes.Add(ToTreeNode(folder));
        }
        _tree.EndUpdate();
        _tree.ExpandAll();
    }

    private static TreeNode ToTreeNode(AttributeFolderNode folder)
    {
        var node = new TreeNode(folder.Name) { Tag = folder, ToolTipText = folder.FullPath };
        foreach (var child in folder.Children) node.Nodes.Add(ToTreeNode(child));
        return node;
    }

    private void OpenCreateDialog()
    {
        if (_client is null || _treeResult is null || _tree.SelectedNode?.Tag is not AttributeFolderNode folder) return;
        var dialog = new CreateAttributesForm(_client, folder, _treeResult.ExistingAttributes.Select(x => x.Name));
        _childWindows.Add(dialog);
        dialog.FormClosed += (_, _) =>
        {
            _childWindows.Remove(dialog);
            if (dialog.CreatedNames.Count > 0) UpdateCreatedAttributes(dialog.CreatedNames);
        };
        dialog.Show();
    }

    private void UpdateCreatedAttributes(IEnumerable<string> createdNames)
    {
        if (_treeResult is null || _identity is null) return;

        var existing = new HashSet<string>(
            _treeResult.ExistingAttributes.Select(x => x.Name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in createdNames.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            if (existing.Add(name))
            {
                _treeResult.ExistingAttributes.Add(new ExistingAttribute { Name = name });
            }
        }

        AttributeFolderCache.Save(_identity, _treeResult);
        SetBusy(false, $"已创建 {createdNames.Count()} 个属性；目录树未重新读取，缓存已同步更新。");
    }

    private async Task CreateFolderAsync()
    {
        if (_client is null || _tree.SelectedNode?.Tag is not AttributeFolderNode folder) return;
        var name = TextInputDialog.Show(this, "新建目录", $"在“{folder.FullPath}”下新建目录：");
        if (string.IsNullOrWhiteSpace(name)) return;
        SetBusy(true, "正在新建属性目录...");
        var response = await _client.CreateAttributeFolderAsync(new CreateFolderRequest { ParentFolderId = folder.Id, Name = name.Trim() });
        if (!response.Success)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "新建目录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        await LoadFromEbAsync();
        MessageBox.Show(this, $"目录“{name.Trim()}”已创建。", "新建目录", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetBusy(bool busy, string message)
    {
        _tree.Enabled = !busy;
        _status.Text = message;
        UseWaitCursor = busy;
    }
}
