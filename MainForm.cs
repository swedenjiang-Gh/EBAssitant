namespace EBAssistant;

public sealed class MainForm : Form
{
    private readonly Label _statusLabel;
    private readonly List<Form> _openWindows = [];

    public MainForm()
    {
        Text = AppDisplay.MainTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 480);
        Size = new Size(900, 560);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Microsoft YaHei UI", 10F);

        var menuStrip = CreateMainMenu();

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = AppDisplay.MainTitle,
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

        var logMenu = new ToolStripMenuItem("日志");
        logMenu.Click += (_, _) => OpenLogFolder();

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(logMenu);
        menuStrip.Items.Add(aboutMenu);
        return menuStrip;
    }

    private void DownloadExcelTemplates()
    {
        var templates = GetExcelTemplatePaths();
        if (templates.Count == 0)
        {
            MessageBox.Show(this, "Templates 目录下未找到 Excel 模板文件。", "下载模板", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择模板保存目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var existing = templates
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(dialog.SelectedPath, name)))
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
        foreach (var source in templates)
        {
            var name = Path.GetFileName(source);
            var target = Path.Combine(dialog.SelectedPath, name);
            if (File.Exists(target) && !overwrite) continue;
            File.Copy(source, target, overwrite);
            copied.Add(name);
        }

        _statusLabel.Text = $"已下载模板：{copied.Count} 个";
        MessageBox.Show(this, $"已保存 {copied.Count} 个模板到：\r\n{dialog.SelectedPath}", "下载模板", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenHelpManual()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "操作手册.md");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "未找到 docs\\操作手册.md。", "帮助", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe",
                ArgumentList = { path },
                UseShellExecute = true
            });
        }
    }

    private void ShowVersionInfo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "未找到 CHANGELOG.md。", "版本信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
        MessageBox.Show(this, text, "版本信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenLogFolder()
    {
        var directory = GetLogsDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
            _statusLabel.Text = $"已打开日志目录：{directory}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开日志目录：{ex.Message}", "日志", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetTemplatePath(string fileName)
    {
        return Path.Combine(GetTemplatesDirectory(), fileName);
    }

    private static string GetTemplatesDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    private static List<string> GetExcelTemplatePaths()
    {
        var directory = GetTemplatesDirectory();
        if (!Directory.Exists(directory)) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".xls", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetLogsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs");
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

        if (functionName == "工具面板配置" || functionName == "宸ュ叿闈㈡澘閰嶇疆")
        {
            var form = new ToolPanelConfigurationForm();
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
