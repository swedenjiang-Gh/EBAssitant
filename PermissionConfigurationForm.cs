using System.Diagnostics;

namespace EBAssistant;

public sealed class PermissionConfigurationForm : Form
{
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, SplitterDistance = 380 };
    private readonly TreeView _usersTree = new() { Dock = DockStyle.Fill, CheckBoxes = true, HideSelection = false, ShowNodeToolTips = true };
    private readonly TreeView _directoriesTree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
    private readonly Button _openLogs = new() { Text = "打开日志目录", Width = 130, Height = 34 };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8) };
    private readonly ContextMenuStrip _directoryMenu = new();
    private EbAdapterClient? _client;
    private PermissionConfigurationIdentity? _identity;
    private bool _updatingChecks;

    public PermissionConfigurationForm()
    {
        Text = "权限配置";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(960, 680);
        Font = new Font("Microsoft YaHei UI", 10F);

        var leftLabel = new Label
        {
            Text = "用户及用户组",
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Height = 30,
            Padding = new Padding(7, 4, 7, 0),
            Dock = DockStyle.Top
        };

        var leftToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(7),
            FlowDirection = FlowDirection.LeftToRight
        };
        leftToolbar.Controls.Add(_refresh);
        leftToolbar.Controls.Add(_openLogs);

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(_usersTree);
        leftPanel.Controls.Add(leftToolbar);
        leftPanel.Controls.Add(leftLabel);

        var rightLabel = new Label
        {
            Text = "权限控制目录",
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Height = 30,
            Padding = new Padding(7, 4, 7, 0),
            Dock = DockStyle.Top
        };

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_directoriesTree);
        rightPanel.Controls.Add(rightLabel);

        _split.Panel1.Controls.Add(leftPanel);
        _split.Panel2.Controls.Add(rightPanel);

        Controls.Add(_split);
        Controls.Add(_status);

        _usersTree.AfterCheck += UsersTreeAfterCheck;
        _refresh.Click += async (_, _) => await LoadFromEbAsync();
        _openLogs.Click += (_, _) =>
        {
            try
            {
                Process.Start("explorer.exe", PermissionConfigurationLogWriter.GetDirectory());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法打开日志目录：{ex.Message}", "权限配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _directoryMenu.Items.Add("展开", null, (_, _) =>
        {
            MessageBox.Show(this, "展开功能将在后续开发中实现。", "权限配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        _directoriesTree.NodeMouseClick += (sender, e) =>
        {
            if (e.Button != MouseButtons.Right || e.Node is null) return;
            _directoriesTree.SelectedNode = e.Node;
            _directoryMenu.Show(_directoriesTree, e.Location);
        };

        Shown += async (_, _) => await ConnectAndLoadAsync();
    }

    private async Task ConnectAndLoadAsync()
    {
        _status.Text = "正在连接当前 EB...";
        SetBusy(true);

        var active = await EbAdapterClient.FindActiveAsync();
        if (active.Count == 0)
        {
            SetBusy(false);
            _status.Text = "未检测到当前活动 EB。";
            var detail = string.IsNullOrWhiteSpace(EbAdapterClient.LastDiscoveryMessage)
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}诊断信息：{Environment.NewLine}{EbAdapterClient.LastDiscoveryMessage}";
            MessageBox.Show(this, $"请先打开 EB 并连接数据库。{detail}", "权限配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _client = active.Count == 1 ? active[0] : SelectAdapter(active);
        if (_client is null) return;

        _status.Text = "正在获取权限配置身份...";
        var identityResponse = await _client.GetPermissionConfigurationIdentityAsync();
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            SetBusy(false);
            _status.Text = identityResponse.Message;
            MessageBox.Show(this, identityResponse.Message, "权限配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _identity = identityResponse.Data;

        var cached = PermissionConfigurationCache.Load(_identity);
        if (cached is not null)
        {
            DisplayStructure(cached, "缓存");
            WriteLog("缓存");
            SetBusy(false);
            _status.Text = $"已从缓存加载权限配置，用户及用户组节点 {CountTreeNodes(_usersTree.Nodes)} 个；权限控制目录 {_directoriesTree.Nodes.Count} 个。点击「刷新」可重新读取 EB。";
            return;
        }

        await LoadFromEbAsync();
    }

    private EbAdapterClient? SelectAdapter(List<EbAdapterClient> active)
    {
        using var dialog = new Form
        {
            Text = "选择当前 EB",
            Size = new Size(480, 230),
            StartPosition = FormStartPosition.CenterParent,
            Font = Font
        };
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
        _status.Text = $"正在从 EB {_client.Connection.Version} 读取...";
        SetBusy(true);

        var response = await _client.GetPermissionConfigurationStructureAsync();
        if (!response.Success || response.Data is null)
        {
            SetBusy(false);
            _status.Text = response.Message;
            MessageBox.Show(this, response.Message, "读取权限配置失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        response.Data.RightNodes = PermissionRootDirectoryFilter.Filter(
            response.Data.RightNodes,
            response.Data.Identity.MessagesId,
            response.Data.Identity.UsersAndGroupsId);

        PermissionConfigurationCache.Save(response.Data);
        DisplayStructure(response.Data, "EB");
        WriteLog("EB");
        SetBusy(false);

        var leftCount = CountTreeNodes(_usersTree.Nodes);
        var rightCount = _directoriesTree.Nodes.Count;
        _status.Text = $"已从 EB 读取；用户及用户组节点 {leftCount} 个；权限控制目录 {rightCount} 个。";
    }

    private void DisplayStructure(PermissionConfigurationStructureResult structure, string source)
    {
        _usersTree.BeginUpdate();
        _directoriesTree.BeginUpdate();

        _usersTree.Nodes.Clear();
        foreach (var node in structure.LeftNodes)
        {
            _usersTree.Nodes.Add(ToTreeNode(node));
        }

        _directoriesTree.Nodes.Clear();
        foreach (var node in structure.RightNodes)
        {
            _directoriesTree.Nodes.Add(new TreeNode(node.Name)
            {
                Tag = node,
                ToolTipText = node.FullPath
            });
        }

        _usersTree.EndUpdate();
        _directoriesTree.EndUpdate();
        _usersTree.CollapseAll();
        _directoriesTree.CollapseAll();
    }

    private static TreeNode ToTreeNode(PermissionDirectoryNode item)
    {
        var node = new TreeNode(item.Name) { Tag = item, ToolTipText = item.FullPath };
        foreach (var child in item.Children)
        {
            node.Nodes.Add(ToTreeNode(child));
        }
        return node;
    }

    private void WriteLog(string source)
    {
        try
        {
            var log = new PermissionConfigurationReadLog
            {
                Source = source,
                EbVersion = _identity?.Version ?? "",
                IdentityRootName = _identity?.RootName ?? "",
                IdentityUsersAndGroupsName = _identity?.UsersAndGroupsName ?? "",
                LeftNodeCount = CountTreeNodes(_usersTree.Nodes),
                RightNodeCount = _directoriesTree.Nodes.Count,
                LeftNodeNames = CollectNames(_usersTree.Nodes),
                RightNodeNames = CollectNames(_directoriesTree.Nodes)
            };
            PermissionConfigurationLogWriter.Write(log);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"写入日志失败：{ex.Message}", "权限配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static int CountTreeNodes(TreeNodeCollection nodes)
    {
        int count = nodes.Count;
        foreach (TreeNode node in nodes)
            count += CountTreeNodes(node.Nodes);
        return count;
    }

    private static List<string> CollectNames(TreeNodeCollection nodes)
    {
        var names = new List<string>();
        foreach (TreeNode node in nodes)
        {
            names.Add(node.Text);
            names.AddRange(CollectNames(node.Nodes));
        }
        return names;
    }

    private void UsersTreeAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_updatingChecks || e.Node is null) return;
        _updatingChecks = true;

        SetChildrenChecked(e.Node, e.Node.Checked);
        UpdateParents(e.Node);

        _updatingChecks = false;
    }

    private static void SetChildrenChecked(TreeNode node, bool checkedState)
    {
        foreach (TreeNode child in node.Nodes)
        {
            child.Checked = checkedState;
            SetChildrenChecked(child, checkedState);
        }
    }

    private static void UpdateParents(TreeNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            parent.Checked = parent.Nodes.Cast<TreeNode>().All(n => n.Checked);
            parent = parent.Parent;
        }
    }

    private void SetBusy(bool busy)
    {
        _usersTree.Enabled = _directoriesTree.Enabled = _refresh.Enabled = !busy;
        UseWaitCursor = busy;
    }
}