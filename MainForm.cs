namespace EBAssistant;

public sealed class MainForm : Form
{
    private static readonly string[] ExcelTemplateNames =
    [
        "创建属性模板.xlsx",
        "类型定义模板.xlsx",
        "工作表模板.xlsx",
        "权限配置模板.xlsx"
    ];

    private readonly Label _statusLabel;
    private readonly List<Form> _openWindows = [];

    public MainForm()
    {
        Text = "EB Assistant";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 480);
        Size = new Size(900, 560);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Microsoft YaHei UI", 10F);

        var menuStrip = CreateMainMenu();

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "EB Assistant",
            Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 43, 59),
            Margin = new Padding(0, 0, 0, 6)
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            Text = "请选择需要使用的配置功能",
            ForeColor = Color.FromArgb(100, 112, 128),
            Margin = new Padding(0, 0, 0, 24)
        };

        var buttonGrid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        string[] functions =
        [
            "属性",
            "类型定义",
            "工作表",
            "权限配置",
            "图形模板",
            "工具面板配置"
        ];

        foreach (var functionName in functions)
        {
            buttonGrid.Controls.Add(CreateFunctionButton(functionName));
        }

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "就绪",
            ForeColor = Color.FromArgb(100, 112, 128),
            Margin = new Padding(0, 24, 0, 0)
        };

        var content = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(54, 42, 54, 42)
        };
        content.Controls.Add(titleLabel);
        content.Controls.Add(descriptionLabel);
        content.Controls.Add(buttonGrid);
        content.Controls.Add(_statusLabel);

        Controls.Add(content);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private MenuStrip CreateMainMenu()
    {
        var menuStrip = new MenuStrip { Dock = DockStyle.Top };

        var fileMenu = new ToolStripMenuItem("文件");
        var downloadTemplateItem = new ToolStripMenuItem("下载模板");
        downloadTemplateItem.Click += (_, _) => DownloadExcelTemplates();
        fileMenu.DropDownItems.Add(downloadTemplateItem);

        var aboutMenu = new ToolStripMenuItem("关于");
        var helpItem = new ToolStripMenuItem("帮助");
        helpItem.Click += (_, _) => OpenHelpManual();
        var versionItem = new ToolStripMenuItem("版本信息");
        versionItem.Click += (_, _) => ShowVersionInfo();
        aboutMenu.DropDownItems.Add(helpItem);
        aboutMenu.DropDownItems.Add(versionItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(aboutMenu);
        return menuStrip;
    }

    private void DownloadExcelTemplates()
    {
        var missing = ExcelTemplateNames
            .Where(name => !File.Exists(GetTemplatePath(name)))
            .ToList();
        if (missing.Count > 0)
        {
            MessageBox.Show(this, $"模板文件缺失：{string.Join("、", missing)}", "下载模板", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择模板保存目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var existing = ExcelTemplateNames
            .Where(name => File.Exists(Path.Combine(dialog.SelectedPath, name)))
            .ToList();
        var overwrite = false;
        if (existing.Count > 0)
        {
            var choice = MessageBox.Show(
                this,
                $"目标目录已存在：{string.Join("、", existing)}\r\n是否覆盖这些文件？",
                "下载模板",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return;
            overwrite = choice == DialogResult.Yes;
        }

        var copied = new List<string>();
        foreach (var name in ExcelTemplateNames)
        {
            var target = Path.Combine(dialog.SelectedPath, name);
            if (File.Exists(target) && !overwrite) continue;
            File.Copy(GetTemplatePath(name), target, overwrite);
            copied.Add(name);
        }

        _statusLabel.Text = $"已下载模板：{copied.Count} 个";
        MessageBox.Show(this, $"已保存 {copied.Count} 个模板到：\r\n{dialog.SelectedPath}", "下载模板", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenHelpManual()
    {
        var path = GetTemplatePath("帮助手册.pdf");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "未找到帮助手册.pdf。", "帮助", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void ShowVersionInfo()
    {
        var path = GetTemplatePath("版本信息.txt");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "未找到版本信息.txt。", "版本信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
        MessageBox.Show(this, text, "版本信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string GetTemplatePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Templates", fileName);
    }

    private Button CreateFunctionButton(string functionName)
    {
        var button = new Button
        {
            Text = functionName,
            Size = new Size(330, 88),
            Margin = new Padding(0, 0, 18, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(32, 43, 59),
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(218, 224, 232);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 242, 255);
        button.Click += (_, _) => OpenFunction(functionName);
        return button;
    }

    private void OpenFunction(string functionName)
    {
        _statusLabel.Text = $"已选择：{functionName}";
        if (functionName == "属性")
        {
            var form = new AttributeFoldersForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }
        if (functionName == "类型定义")
        {
            var form = new TypeDefinitionsForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }
        if (functionName == "工作表")
        {
            var form = new ProjectTemplatesForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }
        if (functionName == "权限配置")
        {
            var form = new PermissionConfigurationForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }

        if (functionName == "图形模板" || functionName == "鍥惧舰妯℃澘")
        {
            var form = new GraphicTemplatesForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }

        MessageBox.Show(
            $"{functionName}功能界面将在后续开发中实现。",
            functionName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
