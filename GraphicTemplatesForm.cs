namespace EBAssistant;

public sealed class GraphicTemplatesForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly CheckedListBox _templates = new() { Dock = DockStyle.Fill, CheckOnClick = true, DisplayMember = nameof(GraphicTemplateItem.Name) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "正在连接当前 EB..." };
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly Button _move = new() { Text = "迁移模板图形", Width = 140, Height = 34, Enabled = false };
    private readonly List<Form> _childWindows = [];
    private EbAdapterClient? _client;
    private GraphicTemplateIdentity? _identity;
    private GraphicTemplateTreeResult? _cache;

    public GraphicTemplatesForm()
    {
        Text = "图形模板";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 680);
        Font = new Font("Microsoft YaHei UI", 10F);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(7), FlowDirection = FlowDirection.LeftToRight };
        toolbar.Controls.Add(_refresh);
        toolbar.Controls.Add(_move);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 430 };
        split.Panel1.Controls.Add(_tree);
        split.Panel2.Controls.Add(_templates);

        _tree.AfterSelect += (_, _) => DisplaySelectedDirectoryTemplates();
        _templates.ItemCheck += (_, _) => BeginInvoke(new Action(UpdateMoveButton));
        _refresh.Click += async (_, _) => await RefreshSelectedDirectoryAsync();
        _move.Click += async (_, _) => await MoveSelectedTemplatesAsync();

        Controls.Add(split);
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
            MessageBox.Show(this, $"未检测到正在运行的 EB，请先打开 EB 并连接数据库。{detail}", "图形模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null)
        {
            Close();
            return;
        }

        var identityResponse = await _client.GetGraphicTemplateIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            SetBusy(false, identityResponse.Message);
            MessageBox.Show(this, identityResponse.Message, "图形模板", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _identity = identityResponse.Data;
        _cache = GraphicTemplateCache.Load(_identity);
        if (_cache is not null)
        {
            DisplayTree(_cache, null);
            SetBusy(false, $"已从缓存加载图形模板目录，共 {_tree.GetNodeCount(true)} 个目录。");
            return;
        }

        await LoadAllFromEbAsync();
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

    private async Task LoadAllFromEbAsync()
    {
        if (_client is null) return;
        SetBusy(true, $"正在从 EB {_client.Connection.Version} 读取图形模板目录...");
        var response = await _client.GetGraphicTemplateTreeAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "读取图形模板失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _cache = response.Data;
        GraphicTemplateCache.Save(_cache);
        DisplayTree(_cache, null);
        SetBusy(false, $"已从 EB 读取并缓存 {_tree.GetNodeCount(true)} 个图形模板目录。");
    }

    private async Task RefreshSelectedDirectoryAsync()
    {
        if (_client is null || _cache is null)
        {
            await LoadAllFromEbAsync();
            return;
        }
        if (_tree.SelectedNode?.Tag is not GraphicTemplateDirectoryNode selected)
        {
            await LoadAllFromEbAsync();
            return;
        }
        await RefreshDirectoryByIdAsync(selected.Id, true);
    }

    private async Task RefreshDirectoryByIdAsync(string directoryId, bool showMessage)
    {
        if (_client is null || _cache is null) return;
        SetBusy(true, "正在刷新所选图形模板目录...");
        var response = await _client.GetGraphicTemplateDirectoryAsync(directoryId);
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            if (showMessage) MessageBox.Show(this, response.Message, "刷新图形模板失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        GraphicTemplateCache.MergeDirectoryShallow(_cache, response.Data);
        GraphicTemplateCache.Save(_cache);
        DisplayTree(_cache, response.Data.Id);
        SetBusy(false, $"已刷新：{response.Data.FullPath}");
    }

    private async Task MoveSelectedTemplatesAsync()
    {
        if (_client is null || _cache is null) return;
        var selectedTemplates = _templates.CheckedItems.Cast<GraphicTemplateItem>().ToList();
        if (selectedTemplates.Count == 0) return;

        using var destination = new GraphicTemplateDestinationForm(_cache);
        if (destination.ShowDialog(this) != DialogResult.OK || destination.SelectedDirectory is null) return;

        SetBusy(true, $"正在迁移 {selectedTemplates.Count} 个模板图形...");
        var response = await _client.MoveGraphicTemplatesAsync(new MoveGraphicTemplatesRequest
        {
            TargetDirectoryId = destination.SelectedDirectory.Id,
            TemplateIds = selectedTemplates.Select(item => item.Id).ToList()
        });
        var result = response.Data;
        if (result is null)
        {
            SetBusy(false, response.Message);
            MessageBox.Show(this, response.Message, "迁移模板图形失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string logDirectory;
        try { logDirectory = GraphicTemplateMigrationLogWriter.Write(result); }
        catch { logDirectory = ""; }

        var resultForm = new GraphicTemplateMigrationResultForm(result, logDirectory);
        _childWindows.Add(resultForm);
        resultForm.FormClosed += (_, _) => _childWindows.Remove(resultForm);
        resultForm.Show();
        await RefreshAfterMoveAsync(selectedTemplates, destination.SelectedDirectory.Id);
        SetBusy(false, response.Message);
    }

    private async Task RefreshAfterMoveAsync(List<GraphicTemplateItem> movedTemplates, string targetDirectoryId)
    {
        var directoryIds = movedTemplates
            .Select(item => item.ParentDirectoryId)
            .Append(targetDirectoryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var id in directoryIds)
        {
            await RefreshDirectoryByIdAsync(id, false);
        }
    }

    private void DisplayTree(GraphicTemplateTreeResult tree, string? selectedId)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var node in tree.Nodes)
        {
            _tree.Nodes.Add(ToTreeNode(node));
        }
        _tree.EndUpdate();
        _tree.CollapseAll();
        if (!string.IsNullOrWhiteSpace(selectedId)) SelectNode(selectedId);
        DisplaySelectedDirectoryTemplates();
    }

    private void SelectNode(string id)
    {
        foreach (TreeNode node in _tree.Nodes)
        {
            var found = FindTreeNode(node, id);
            if (found is null) continue;
            _tree.SelectedNode = found;
            found.EnsureVisible();
            return;
        }
    }

    private void DisplaySelectedDirectoryTemplates()
    {
        _templates.Items.Clear();
        if (_tree.SelectedNode?.Tag is GraphicTemplateDirectoryNode selected)
        {
            foreach (var template in selected.Templates)
            {
                _templates.Items.Add(template, false);
            }
            _status.Text = $"{selected.FullPath}：{selected.Templates.Count} 个模板图形";
        }
        UpdateMoveButton();
    }

    private void UpdateMoveButton()
    {
        _move.Enabled = !UseWaitCursor && _templates.CheckedItems.Count > 0;
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

    private static TreeNode? FindTreeNode(TreeNode node, string id)
    {
        if (node.Tag is GraphicTemplateDirectoryNode directory &&
            string.Equals(directory.Id, id, StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (TreeNode child in node.Nodes)
        {
            var found = FindTreeNode(child, id);
            if (found is not null) return found;
        }
        return null;
    }

    private void SetBusy(bool busy, string message)
    {
        _tree.Enabled = _templates.Enabled = _refresh.Enabled = !busy;
        _move.Enabled = !busy && _templates.CheckedItems.Count > 0;
        _status.Text = message;
        UseWaitCursor = busy;
    }
}
