namespace EBAssist;

public sealed class AttributeFoldersForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "正在连接当前 EB..." };
    private readonly ContextMenuStrip _menu = new();
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly List<Form> _childWindows = [];
    private EbAdapterClient? _client;
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
        _refresh.Click += async (_, _) => await LoadTreeAsync();
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
            MessageBox.Show(this, "未检测到正在运行的 EB，请先打开 EB 并连接数据库。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null)
        {
            Close();
            return;
        }
        await LoadTreeAsync();
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

    private async Task LoadTreeAsync()
    {
        if (_client is null) return;
        SetBusy(true, $"正在读取 {_client.Connection.Version} 属性目录...");
        var response = await _client.GetAttributeFolderTreeAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "读取属性目录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _treeResult = response.Data;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var folder in response.Data.Folders)
        {
            _tree.Nodes.Add(ToTreeNode(folder));
        }
        _tree.EndUpdate();
        _tree.ExpandAll();
        SetBusy(false, $"已连接 {_client.Connection.ApplicationName}，共读取 {_tree.GetNodeCount(true)} 个属性目录。");
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
        dialog.FormClosed += async (_, _) =>
        {
            _childWindows.Remove(dialog);
            if (dialog.CreatedSuccessfully) await LoadTreeAsync();
        };
        dialog.Show();
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
        await LoadTreeAsync();
        MessageBox.Show(this, $"目录“{name.Trim()}”已创建。", "新建目录", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetBusy(bool busy, string message)
    {
        _tree.Enabled = !busy;
        _status.Text = message;
        UseWaitCursor = busy;
    }
}
