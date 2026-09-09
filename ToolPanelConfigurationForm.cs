namespace EBAssistant;

public sealed class ToolPanelConfigurationForm : Form
{
    private readonly TreeView _graphicDirectories = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly CheckedListBox _graphicTemplates = new() { Dock = DockStyle.Fill, CheckOnClick = true, DisplayMember = nameof(GraphicTemplateItem.Name) };
    private readonly TreeView _toolPanelDirectories = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly Button _add = new() { Text = "添加到工具面板", Width = 150, Height = 34, Enabled = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8) };
    private EbAdapterClient? _client;
    private GraphicTemplateTreeResult? _graphicTree;
    private ToolPanelConfigurationTreeResult? _toolPanelTree;
    private ToolPanelConfigurationIdentity? _toolPanelIdentity;
    private RefreshTarget _refreshTarget;

    public ToolPanelConfigurationForm()
    {
        Text = "工具面板配置";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1350, 720);
        Font = new Font("Microsoft YaHei UI", 10F);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(7) };
        toolbar.Controls.Add(_refresh);
        toolbar.Controls.Add(_add);

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29F));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        columns.Controls.Add(CreateSection("图形模板目录", _graphicDirectories), 0, 0);
        columns.Controls.Add(CreateSection("模板图形", _graphicTemplates), 1, 0);
        columns.Controls.Add(CreateSection("数据库 / 模板 / 工具面板配置", _toolPanelDirectories), 2, 0);

        Controls.Add(columns);
        Controls.Add(toolbar);
        Controls.Add(_status);

        _graphicDirectories.AfterSelect += (_, _) =>
        {
            _refreshTarget = RefreshTarget.GraphicTemplates;
            DisplayTemplates();
        };
        _graphicTemplates.ItemCheck += (_, _) => BeginInvoke(new Action(UpdateAddState));
        _toolPanelDirectories.AfterSelect += (_, _) =>
        {
            _refreshTarget = RefreshTarget.ToolPanelConfiguration;
            UpdateAddState();
        };
        _refresh.Click += async (_, _) => await RefreshSelectedDirectoryAsync();
        _add.Click += async (_, _) => await AddSelectedAsync();
        Shown += async (_, _) => await ConnectAndLoadAsync();
    }

    private static Panel CreateSection(string title, Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        panel.Controls.Add(content);
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(4, 6, 4, 0),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
        });
        return panel;
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
            MessageBox.Show(this, $"请先打开 EB 并连接数据库。{detail}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null) return;

        var toolPanelIdentityResponse = await _client.GetToolPanelConfigurationIdentityAsync();
        if (!toolPanelIdentityResponse.Success || toolPanelIdentityResponse.Data is null)
        {
            SetBusy(false, toolPanelIdentityResponse.Message);
            MessageBox.Show(this, toolPanelIdentityResponse.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _toolPanelIdentity = toolPanelIdentityResponse.Data;
        var shared = await GraphicTemplateSharedLoader.LoadAsync(_client, CreateGraphicReadProgress());
        if (!shared.Success || shared.Tree is null)
        {
            SetBusy(false, shared.Message);
            MessageBox.Show(this, shared.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _graphicTree = shared.Tree;

        var toolPanelTree = ToolPanelConfigurationCache.Load(_toolPanelIdentity);
        if (toolPanelTree is not null)
        {
            _toolPanelTree = toolPanelTree;
            Display(_graphicTree, toolPanelTree);
            SetBusy(false, $"{shared.Message} 已从工具面板配置缓存加载。");
            return;
        }

        await LoadToolPanelFromEbAsync();
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

    private async Task LoadToolPanelFromEbAsync()
    {
        if (_client is null || _toolPanelIdentity is null || _graphicTree is null) return;
        SetBusy(true, $"正在从 EB {_client.Connection.Version} 读取工具面板配置...");
        var toolPanelResponse = await _client.GetToolPanelConfigurationTreeAsync();
        if (!toolPanelResponse.Success || toolPanelResponse.Data is null)
        {
            SetBusy(false, toolPanelResponse.Message);
            MessageBox.Show(this, toolPanelResponse.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ToolPanelConfigurationCache.Save(toolPanelResponse.Data);
        _toolPanelTree = toolPanelResponse.Data;
        Display(_graphicTree, toolPanelResponse.Data);
        SetBusy(false, "已从 EB 读取工具面板配置并更新缓存。");
    }

    private void Display(GraphicTemplateTreeResult graphicTree, ToolPanelConfigurationTreeResult toolPanelTree)
    {
        DisplayGraphicTree(graphicTree, null);
        DisplayToolPanelTree(toolPanelTree, null);
    }

    private void DisplayGraphicTree(GraphicTemplateTreeResult graphicTree, string? selectedId)
    {
        _graphicDirectories.Nodes.Clear();
        foreach (var node in graphicTree.Nodes) _graphicDirectories.Nodes.Add(ToGraphicTreeNode(node));
        _graphicDirectories.CollapseAll();
        if (!string.IsNullOrWhiteSpace(selectedId)) SelectNode(_graphicDirectories.Nodes, selectedId);
        DisplayTemplates();
    }

    private void DisplayToolPanelTree(ToolPanelConfigurationTreeResult toolPanelTree, string? selectedId)
    {
        _toolPanelDirectories.Nodes.Clear();
        foreach (var node in toolPanelTree.Nodes) _toolPanelDirectories.Nodes.Add(ToToolPanelTreeNode(node));
        _toolPanelDirectories.CollapseAll();
        if (!string.IsNullOrWhiteSpace(selectedId)) SelectNode(_toolPanelDirectories.Nodes, selectedId);
        UpdateAddState();
    }

    private async Task RefreshSelectedDirectoryAsync()
    {
        if (_refreshTarget == RefreshTarget.GraphicTemplates &&
            _graphicDirectories.SelectedNode?.Tag is GraphicTemplateDirectoryNode graphicDirectory)
        {
            await RefreshGraphicDirectoryAsync(graphicDirectory.Id, true);
            return;
        }
        if (_refreshTarget == RefreshTarget.ToolPanelConfiguration &&
            _toolPanelDirectories.SelectedNode?.Tag is ToolPanelDirectoryNode toolPanelDirectory)
        {
            await RefreshToolPanelDirectoryAsync(toolPanelDirectory.Id, true);
            return;
        }

        MessageBox.Show(this, "请先在左侧或右侧选择需要刷新的目录。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RefreshGraphicDirectoryAsync(string directoryId, bool showMessage)
    {
        if (_client is null || _graphicTree is null) return;
        SetBusy(true, "正在刷新所选图形模板目录...");
        var response = await _client.GetGraphicTemplateDirectoryAsync(directoryId, CreateGraphicReadProgress());
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            if (showMessage) MessageBox.Show(this, response.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        GraphicTemplateCache.MergeDirectoryShallow(_graphicTree, response.Data);
        GraphicTemplateCache.Save(_graphicTree);
        DisplayGraphicTree(_graphicTree, response.Data.Id);
        SetBusy(false, $"已刷新左侧目录：{response.Data.FullPath}");
    }

    private async Task RefreshToolPanelDirectoryAsync(string directoryId, bool showMessage)
    {
        if (_client is null || _toolPanelTree is null) return;
        SetBusy(true, "正在刷新所选工具面板配置目录...");
        var response = await _client.GetToolPanelConfigurationDirectoryAsync(directoryId);
        if (!response.Success || response.Data is null)
        {
            SetBusy(false, response.Message);
            if (showMessage) MessageBox.Show(this, response.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ToolPanelConfigurationCache.MergeDirectoryShallow(_toolPanelTree, response.Data);
        ToolPanelConfigurationCache.Save(_toolPanelTree);
        DisplayToolPanelTree(_toolPanelTree, response.Data.Id);
        SetBusy(false, $"已刷新右侧目录：{response.Data.FullPath}");
    }

    private void DisplayTemplates()
    {
        _graphicTemplates.Items.Clear();
        if (_graphicDirectories.SelectedNode?.Tag is GraphicTemplateDirectoryNode selected)
        {
            foreach (var template in selected.Templates) _graphicTemplates.Items.Add(template, false);
        }
        UpdateAddState();
    }

    private async Task AddSelectedAsync()
    {
        if (_client is null || _toolPanelDirectories.SelectedNode?.Tag is not ToolPanelDirectoryNode target) return;
        var templates = _graphicTemplates.CheckedItems.Cast<GraphicTemplateItem>().ToList();
        if (templates.Count == 0 || !ToolPanelConfigurationSelection.IsToolPanelEntry(target)) return;

        var confirmation = MessageBox.Show(
            this,
            $"将 {templates.Count} 个模板图形关联到工具面板条目：\r\n{target.FullPath}\r\n\r\n该操作会写入 EB 全局工具面板配置，是否继续？",
            "确认添加到工具面板",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes) return;

        SetBusy(true, $"正在添加 {templates.Count} 个模板图形...");
        var response = await _client.AddGraphicTemplatesToToolPanelAsync(new AddGraphicTemplatesToToolPanelRequest
        {
            TargetDirectoryId = target.Id,
            TemplateIds = templates.Select(item => item.Id).ToList()
        });
        var result = response.Data ?? CreateFailureResult(target, templates, response.Message);

        string logDirectory;
        try { logDirectory = ToolPanelConfigurationLogWriter.Write(result); }
        catch (Exception ex)
        {
            logDirectory = ToolPanelConfigurationLogWriter.GetDirectory();
            MessageBox.Show(this, $"操作已完成，但保存日志失败：{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        SetBusy(false, response.Message);
        new ToolPanelConfigurationResultForm(result, logDirectory).Show(this);
        await RefreshToolPanelDirectoryAsync(target.Id, false);
    }

    private static AddGraphicTemplatesToToolPanelResult CreateFailureResult(
        ToolPanelDirectoryNode target,
        List<GraphicTemplateItem> templates,
        string message) =>
        new()
        {
            Status = "failed",
            TargetDirectoryId = target.Id,
            TargetDirectoryPath = target.FullPath,
            TotalCount = templates.Count,
            FailedCount = templates.Count,
            Records = templates.Select(item => new ToolPanelAdditionRecord
            {
                TemplateId = item.Id,
                TemplateName = item.Name,
                TargetDirectoryId = target.Id,
                TargetDirectoryPath = target.FullPath,
                Status = "failed",
                Message = message
            }).ToList()
        };

    private void UpdateAddState()
    {
        _add.Enabled = !UseWaitCursor &&
            _graphicTemplates.CheckedItems.Count > 0 &&
            _toolPanelDirectories.SelectedNode?.Tag is ToolPanelDirectoryNode target &&
            ToolPanelConfigurationSelection.IsToolPanelEntry(target);
    }

    private IProgress<string> CreateGraphicReadProgress() => new Progress<string>(message =>
    {
        if (!IsDisposed && !_refresh.Enabled) _status.Text = message;
    });

    private void SetBusy(bool busy, string message)
    {
        _graphicDirectories.Enabled = _graphicTemplates.Enabled = _toolPanelDirectories.Enabled = _refresh.Enabled = !busy;
        UseWaitCursor = busy;
        _status.Text = message;
        UpdateAddState();
    }

    private static TreeNode ToGraphicTreeNode(GraphicTemplateDirectoryNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children) node.Nodes.Add(ToGraphicTreeNode(child));
        return node;
    }

    private static TreeNode ToToolPanelTreeNode(ToolPanelDirectoryNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children) node.Nodes.Add(ToToolPanelTreeNode(child));
        return node;
    }

    private static bool SelectNode(TreeNodeCollection nodes, string id)
    {
        foreach (TreeNode node in nodes)
        {
            var itemId = node.Tag switch
            {
                GraphicTemplateDirectoryNode graphic => graphic.Id,
                ToolPanelDirectoryNode toolPanel => toolPanel.Id,
                _ => ""
            };
            if (string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase))
            {
                var tree = node.TreeView;
                if (tree is not null) tree.SelectedNode = node;
                node.EnsureVisible();
                return true;
            }
            if (SelectNode(node.Nodes, id)) return true;
        }
        return false;
    }

    private enum RefreshTarget
    {
        None,
        GraphicTemplates,
        ToolPanelConfiguration
    }
}
